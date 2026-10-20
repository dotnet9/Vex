const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
require('./sync.cjs')();
let scripts = 0;
function scan(folder) {
  for (const entry of fs.readdirSync(folder, {withFileTypes:true})) {
    if (entry.name === 'vendor' || entry.name === 'node_modules') continue;
    const file = path.join(folder, entry.name);
    if (entry.isDirectory()) scan(file);
    else if (/\.(js|cjs)$/.test(file)) {
      new vm.Script(fs.readFileSync(file, 'utf8'), {filename:file});
      scripts++;
    }
  }
}
scan(root);
for (const file of fs.readdirSync(root).filter(file => file.endsWith('.html'))) {
  const html = fs.readFileSync(path.join(root, file), 'utf8');
  for (const match of html.matchAll(/(?:src|href)="([^"?#]+)[^"]*"/g)) {
    if (/^(?:https?:|data:|mailto:|#)/.test(match[1])) continue;
    assert.ok(fs.existsSync(path.resolve(root, match[1])), `${file}: ${match[1]}`);
  }
}
const logo = fs.readFileSync(path.join(root, 'assets/vex-logo.svg'), 'utf8');
assert.ok(logo.includes('#24766B'));
assert.ok(logo.includes('M64 62 L128 198 L192 62'));
const preferences = fs.readFileSync(path.join(root, 'assets/preferences.js'), 'utf8');
assert.ok(preferences.includes('autoSave:false'));
const sandbox = {window:{}};
vm.runInNewContext(fs.readFileSync(path.join(root, 'assets/generated/documents-data.js'), 'utf8'), sandbox);
for (const [key, file] of [['thanks','docs/鸣谢.md'], ['changelog','UpdateLog.md']]) {
  assert.equal(sandbox.window.VexDocuments[key].text, fs.readFileSync(path.join(root, '..', file), 'utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n'));
}
console.log(`PASS: ${scripts} scripts, local HTML assets, ink-teal V logo, default auto-save and bundled Markdown.`);
