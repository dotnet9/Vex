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
for (const [key, file] of [['thanks','docs/鸣谢.md'], ['changelog','docs/更新日志.md']]) {
  assert.equal(sandbox.window.VexDocuments[key].text, fs.readFileSync(path.join(root, '..', file), 'utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n'));
}
const changelog = sandbox.window.VexDocuments.changelog.text;
vm.runInNewContext(fs.readFileSync(path.join(root, 'assets/desktop/samples.js'), 'utf8'), sandbox);
assert.equal(sandbox.window.VexSamples.length, 6, 'Six samples are shared with the installed app');
for (const sample of sandbox.window.VexSamples) {
  const file = path.resolve(root, sample.path);
  const text = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n').replaceAll('(图片/', '(../docs/演示文档/图片/');
  assert.equal(sample.text, text, 'Prototype and packaged sample stay in sync: '+sample.name);
  for (const image of sample.text.matchAll(/!\[[^\]]*\]\(([^)]+)\)/g))
    assert.ok(fs.existsSync(path.resolve(root, image[1])), 'Local sample image exists');
}
const dates = [...changelog.matchAll(/^## V\d+(?:\.\d+)+（(\d{4}-\d{2}-\d{2})）$/gm)].map(match => match[1]);
assert.ok(dates.length > 0, 'Changelog has dated version groups');
assert.equal(new Set(dates).size, dates.length, 'Each date has one changelog group');
for (const line of changelog.split('\n').filter(line => line.startsWith('- '))) {
  assert.match(line, /^- (?:✨\[新增\]|⚡\[优化\]|🐛\[修复\]|😄\[新增\]|🔨\[优化\]|❌\[删除\])-.+$/, 'User-facing change category');
}
console.log(`PASS: ${scripts} scripts, local HTML assets, ink-teal V logo, default auto-save and bundled Markdown.`);
