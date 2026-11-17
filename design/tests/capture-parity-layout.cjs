/* 布局锚点 parity（原型侧）：与 native layout-parity 模式共用锚点表，
   输出每场景的元素几何（相对 app-window 左上角，1440x900），供 layout-score 比对。
   用法: node design/tests/capture-parity-layout.cjs <输出目录>  （需 preview 服务） */
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const base = process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/';
const output = path.resolve(process.argv[2] || path.join(__dirname, '../../artifacts/prototype-layout-parity'));
fs.mkdirSync(output, { recursive: true });

/* 锚点表：scene -> [ [锚点名, 原型选择器], ... ]（与 native 端 LAYOUT_ANCHORS 一一对应）。 */
const SCENES = {
  'instant': [
    ['menu-file', '.tb-menuitem:nth-child(1)'],
    ['menu-view', '.tb-menuitem:nth-child(2)'],
    ['menu-help', '.tb-menuitem:nth-child(3)'],
    ['document-title', '#document-name'],
    ['update-pill', '#upd-pill-wrap'],
    ['band-undo', '#format-toolbar .format-button:nth-child(1)'],
    ['band-heading', '#band-heading'],
    ['band-mermaid', '[data-action="insert-mermaid-picker"]'],
    ['band-span-size', '#span-size-button'],
    ['typography-trigger', '.edit-options > .typography-trigger'],
    ['sidebar-search', '#files-pane .panel-tools'],
    ['files-footer', '.files-footer'],
    ['word-count', '#sb-wordcount'],
  ],
};

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
  await page.goto(base + 'Vex.html?preview=1&lang=zh-CN&theme=light&palette=indigo&typography=Simple', { waitUntil: 'domcontentloaded' });
  await page.waitForFunction(() => window.VexWorkspace && window.VexChapters);
  await page.waitForTimeout(400);
  const sample = fs.readFileSync(path.join(__dirname, 'fixtures/parity-sample.md'), 'utf8');
  await page.evaluate(text => { if (window.VexWorkbench && VexWorkbench.applyText) VexWorkbench.applyText(text); }, sample);
  await page.evaluate(() => { if (window.VexWorkbench) VexWorkbench.setFollow(false); });
  await page.evaluate(() => { const pill = document.getElementById('upd-pill-wrap'); if (pill) pill.hidden = true; });
  await page.waitForTimeout(150);

  const layoutMenu = async key => {
    await page.evaluate(() => { const more = document.querySelector('#workspace-layout'); if (more && document.querySelector('#workspace-layout-menu[hidden]')) more.click(); });
    await page.waitForTimeout(200);
    await page.evaluate(k => { const item = document.querySelector('#workspace-layout-menu [data-layout="' + k + '"]'); if (item) item.click(); }, key);
    await page.waitForTimeout(450);
  };

  const report = {};
  for (const [scene, anchors] of Object.entries(SCENES)) {
    await layoutMenu('instant');
    const rects = await page.evaluate(entries => {
      const app = document.querySelector('.app-window').getBoundingClientRect();
      const out = {};
      for (const [name, selector] of entries) {
        const el = document.querySelector(selector);
        if (!el) { out[name] = null; continue; }
        const r = el.getBoundingClientRect();
        out[name] = { x: +(r.x - app.x).toFixed(1), y: +(r.y - app.y).toFixed(1), w: +r.width.toFixed(1), h: +r.height.toFixed(1) };
      }
      return out;
    }, anchors);
    report[scene] = rects;
  }
  fs.writeFileSync(path.join(output, 'layout-anchors.json'), JSON.stringify(report, null, 1));
  console.log('LAYOUT ANCHORS CAPTURED: ' + Object.keys(report).join(','));
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
