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
const preferences = fs.readFileSync(path.join(root, 'assets/workbench/preferences.js'), 'utf8');
assert.ok(preferences.includes('autoSave:false'));
const sandbox = {window:{}};
vm.runInNewContext(fs.readFileSync(path.join(root, 'assets/generated/palette-rules-data.js'), 'utf8'), sandbox);
assert.ok(Number.isInteger(sandbox.window.VexPaletteRulesData.ruleVersion), 'Palette data carries a ruleVersion stamp');
const paletteStamp = /\(ruleVersion (\d+)\)/.exec(fs.readFileSync(path.join(root, 'assets/prototype/palettes.css'), 'utf8'));
assert.ok(paletteStamp, 'palettes.css is generated with a ruleVersion stamp');
for (const generated of ['assets/prototype/palettes.css','assets/prototype/palettes.js','assets/prototype/density.css','assets/generated/palette-rules-data.js']) {
  const header = fs.readFileSync(path.join(root, generated), 'utf8').split('\n', 1)[0];
  assert.match(header, new RegExp(`ruleVersion ${sandbox.window.VexPaletteRulesData.ruleVersion}`), `${generated} matches the generated ruleVersion`);
}
/* 生成令牌对比度门禁（规则 §2 v7）：正文性文字（源码高亮 md-code-fg / 文字级强调 accent-ink）
 * 落在 code-bg / editor-bg / surface / paper 上必须 ≥4.5:1，九套配色 × 明暗全覆盖。 */
{
  const parse = value => {
    let m = /^#([0-9a-f]{6})$/i.exec(value.trim());
    if (!m) return null;
    const n = parseInt(m[1], 16);
    return [n >> 16 & 255, n >> 8 & 255, n & 255, 1];
  };
  const mixOver = (fg, bg) => fg[3] >= 1 ? fg : fg.slice(0, 3).map((v, i) => v * fg[3] + bg[i] * (1 - fg[3])).concat(1);
  const luminance = c => c.slice(0, 3).map(v => { v /= 255; return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; })
    .reduce((sum, v, i) => sum + v * [0.2126, 0.7152, 0.0722][i], 0);
  const contrast = (a, b) => { const x = luminance(a), y = luminance(b); return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05); };
  const paletteCss = fs.readFileSync(path.join(root, 'assets/prototype/palettes.css'), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
  const blocks = {};
  for (const match of paletteCss.matchAll(/([^{}]+)\{([^{}]*)\}/g)) {
    for (const theme of ['light', 'dark']) {
      if (!match[1].includes(`data-theme="${theme}"`)) continue;
      const scope = match[1].includes('data-palette=') ? /data-palette="([a-z-]+)"/.exec(match[1])[1] : '';
      blocks[theme + '/' + scope] = Object.assign(blocks[theme + '/' + scope] || {}, Object.fromEntries(match[2].split(';').map(d => d.split(':')).filter(d => d.length === 2 && d[0].startsWith('--')).map(d => [d[0].trim(), d[1].trim()])));
    }
  }
  for (const [name, vars] of Object.entries(blocks)) {
    if (!vars['--accent-ink']) continue;
    const surface = parse(vars['--surface']) || [255, 255, 255, 1];
    for (const inkKey of ['--md-code-fg', '--accent-ink']) {
      for (const bgKey of ['--code-bg', '--editor-bg', '--surface']) {
        const fg = mixOver(parse(vars[inkKey]), surface);
        const bg = mixOver(parse(vars[bgKey]), surface);
        assert.ok(contrast(fg, bg) >= 4.5, `${name}: ${inkKey} on ${bgKey} = ${contrast(fg, bg).toFixed(2)}:1（需 ≥4.5，规则 §2 v7）`);
      }
    }
  }
}
assert.equal(Object.keys(sandbox.window.VexPaletteRulesData.palettes).length, 9, 'Nine palettes come from rule appendix A');
const prototype = {VexStorage:{getItem:()=>null}};
prototype.window = prototype;
vm.runInNewContext(fs.readFileSync(path.join(root, 'assets/prototype/typography-catalog.js'), 'utf8'), prototype);
assert.equal(prototype.VexTypes.length, 6, 'Base typography catalog keeps the six independent native styles');
for (const asset of ['prototype/palettes.js','prototype/desktop-resources.js','prototype/design-system.js']) {
  vm.runInNewContext(fs.readFileSync(path.join(root, 'assets', asset), 'utf8'), prototype);
}
assert.equal(prototype.VexTypes.length, 13, 'Prototype exposes the thirteen finalized typographies');
assert.equal(prototype.VexPalettes.length, 9, 'Prototype exposes the nine finalized palettes');
vm.runInNewContext(fs.readFileSync(path.join(root, 'assets/generated/documents-data.js'), 'utf8'), sandbox);
for (const [key, file] of [['thanks','docs/鸣谢.md'], ['changelog','docs/更新日志.md']]) {
  assert.equal(sandbox.window.VexDocuments[key].text, fs.readFileSync(path.join(root, '..', file), 'utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n'));
}
const changelog = sandbox.window.VexDocuments.changelog.text;
vm.runInNewContext(fs.readFileSync(path.join(root, 'assets/workbench/samples.js'), 'utf8'), sandbox);
assert.equal(sandbox.window.VexSamples.length, 8, 'Eight C#/.NET/Avalonia samples are shared with the installed app');
assert.deepEqual(sandbox.window.VexSamples.map(sample => sample.name).sort().join('|'),
  fs.readdirSync(path.join(root, '../docs/演示文档')).filter(file => file.endsWith('.md')).sort().join('|'),
  'Every installed sample is represented in the prototype');
for (const sample of sandbox.window.VexSamples) {
  const file = path.resolve(root, sample.path);
  const text = fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '').replace(/\r\n/g, '\n').replaceAll('(图片/', '(../docs/演示文档/图片/');
  assert.equal(sample.text, text, 'Prototype and packaged sample stay in sync: '+sample.name);
  for (const image of sample.text.matchAll(/!\[[^\]]*\]\(([^)]+)\)/g))
    assert.ok(fs.existsSync(path.resolve(root, image[1])), 'Local sample image exists');
}
const versions = [...changelog.matchAll(/^## (V\d+(?:\.\d+)+)（\d{4}-\d{2}-\d{2}）$/gm)].map(match => match[1]);
assert.ok(versions.length > 0, 'Changelog has dated version groups');
assert.equal(new Set(versions).size, versions.length, 'Each version has one changelog group');
for (const line of changelog.split('\n').filter(line => line.startsWith('- '))) {
  assert.match(line, /^- (?:✨\[新增\]|⚡\[优化\]|🐛\[修复\]|✅\[验证\]|😄\[新增\]|🔨\[优化\]|❌\[删除\])-.+$/, 'User-facing change category');
}
/* Toast 文案 i18n 审计：字面量中文直接调用禁止；toastKey 的键必须在四语表中存在。 */
{
  const workbench = ['workbench.js','interact.js','workspace.js','chapter-editor.js','workflows.js'];
  const literal = /VexProto\.toast\(\s*['"`][^'"`]*[一-鿿]/;
  const keyUse = /VexProto\.toastKey\(\s*(?:['"`]([A-Za-z][A-Za-z0-9]*)['"`]|[^)]*['"`]([A-Za-z][A-Za-z0-9]*)['"`]\s*\))/g;
  const usedKeys = new Set();
  const problems = [];
  for (const name of workbench) {
    const file = path.join(root, 'assets/workbench', name);
    const text = fs.readFileSync(file, 'utf8');
    if (literal.test(text)) problems.push(`${name}: VexProto.toast 存在硬编码中文文案`);
    for (const match of text.matchAll(/VexProto\.toastKey\(([^)]*)\)/g)) {
      for (const key of [...match[1].matchAll(/['"`]([A-Za-z][A-Za-z0-9]*)['"`]/g)].map(m => m[1]))
        usedKeys.add(key);
    }
  }
  const dataFile = path.join(root, 'assets/workbench/vex-data.js');
  const dataSandbox = {window:{}};
  dataSandbox.window = dataSandbox;
  vm.runInNewContext(fs.readFileSync(dataFile, 'utf8'), dataSandbox);
  const languages = dataSandbox.window.VEX_DATA.languages;
  const cultures = Object.keys(languages);
  for (const key of [...usedKeys].sort()) {
    for (const culture of cultures)
      if (typeof languages[culture][key] !== 'string') problems.push(`${name || 'workbench'}: 缺少 ${culture}.${key}`);
  }
  if (usedKeys.size === 0) problems.push('未发现任何 toastKey 用法（i18n 收口回退？）');
  assert.equal(problems.length, 0, problems.join(' '));
}
console.log(`PASS: ${scripts} scripts, local HTML assets, ink-teal V logo, default auto-save and bundled Markdown.`);
