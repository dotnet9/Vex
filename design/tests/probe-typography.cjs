/* 临时探针：打开原型排版弹层，输出关键元素的计算样式与几何，供原生实现对齐。
 * node design/tests/probe-typography.cjs */
const {chromium} = require('playwright');
const base = process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/';

(async () => {
  const browser = await chromium.launch({headless:true});
  const page = await browser.newPage({viewport:{width:1488, height:948}, deviceScaleFactor:1});
  await page.goto(base + 'Vex.html?preview=1&lang=zh-CN&theme=light&palette=indigo&typography=Simple', {waitUntil:'domcontentloaded'});
  await page.waitForFunction(() => window.VexAuxiliary && window.VexChapters);
  await page.waitForTimeout(500);
  await page.evaluate(() => { const trigger = document.querySelector('[data-action="typography"]'); if (trigger) trigger.click(); });
  await page.waitForTimeout(500);
  const report = await page.evaluate(() => {
    const out = {};
    const rect = el => { const r = el.getBoundingClientRect(); const w = document.querySelector('.app-window').getBoundingClientRect(); return {x:+(r.x-w.x).toFixed(2),y:+(r.y-w.y).toFixed(2),w:+r.width.toFixed(2),h:+r.height.toFixed(2)}; };
    const style = (el, props) => { const cs = getComputedStyle(el); const o = {}; for (const p of props) o[p] = cs.getPropertyValue(p); return o; };
    const panel = document.querySelector('#typography-panel');
    out.panel = {rect:rect(panel), style:style(panel,['background-color','padding','border-radius','width','height','box-shadow','border-color'])};
    const title = panel.querySelector('.popover-title');
    out.title = {rect:rect(title), style:style(title,['font-size','padding','margin','color'])};
    const density = panel.querySelector('.typography-density');
    out.density = {rect:rect(density), style:style(density,['gap','padding','border-bottom-color'])};
    const label = density.querySelector('strong');
    out.densityLabel = {rect:rect(label), style:style(label,['font-size','color','font-weight'])};
    const options = density.querySelector('.density-options');
    out.densityOptions = {rect:rect(options), style:style(options,['max-width','padding','background-color','border-radius','display','gap'])};
    out.densityButtons = Array.from(options.querySelectorAll('button')).map(b => ({text:b.textContent.trim(), rect:rect(b), style:style(b,['flex','min-height','padding','font-size','border-radius','background-color','color','border-width'])}));
    const grid = panel.querySelector('.typography-grid');
    out.grid = {rect:rect(grid), style:style(grid,['gap','padding','grid-template-columns','max-height'])};
    const cards = Array.from(grid.querySelectorAll('.typography-card'));
    out.cardCount = cards.length;
    out.cards = cards.slice(0,4).map(c => ({rect:rect(c), style:style(c,['padding','border-radius','background-color','border-color','border-width','box-shadow','overflow','height'])}));
    const swatches = Array.from(grid.querySelectorAll('.type-swatch'));
    out.swatch = swatches.slice(0,2).map(s => ({rect:rect(s), style:style(s,['height','padding','background-color','border-top-width','border-top-color'])}));
    const docs = Array.from(grid.querySelectorAll('.type-document'));
    out.doc = docs.slice(0,2).map(d => ({rect:rect(d), style:style(d,['padding','font-size','--type-size','transform','line-height'])}));
    const h2 = docs[0].querySelector('h2');
    out.h2 = {rect:rect(h2), style:style(h2,['font-size','line-height','margin','font-weight','color','letter-spacing','border-bottom-width','border-bottom-color','padding'])};
    const p = docs[0].querySelector('p');
    out.p = {rect:rect(p), style:style(p,['font-size','line-height','margin','color'])};
    const q = docs[0].querySelector('blockquote');
    out.quote = {rect:rect(q), style:style(q,['font-size','line-height','margin','padding','background-color','border-left-width','border-left-color','color','border-radius'])};
    const add = grid.querySelector('.typography-add');
    out.add = {rect:rect(add), style:style(add,['height','border-radius','border-style','border-color','font-size'])};
    const badge = cards[0].querySelector('.palette-badge');
    out.badge = badge ? {rect:rect(badge), style:style(badge,['font-size','padding','background-color','color','border-radius','top','right'])} : null;
    const trigger = document.querySelector('[data-action="typography"]');
    out.trigger = trigger ? {rect:rect(trigger), style:style(trigger,['height','padding','font-size','border-radius'])} : null;
    return out;
  });
  console.log(JSON.stringify(report, null, 1));
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });

