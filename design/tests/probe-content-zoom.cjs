/* 临时探针：阅读视图内容缩放的真实语义，供原生 MarkdownPreviewView 对齐。
 * 需要本地静态服务：python -m http.server 4317 --bind 127.0.0.1
 * node design/tests/probe-content-zoom.cjs */
const {chromium} = require('playwright');
const base = process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/';

(async () => {
  const browser = await chromium.launch({headless:true});
  const page = await browser.newPage({viewport:{width:1280, height:820}, deviceScaleFactor:1});
  await page.goto(base + 'Vex.html?preview=1&lang=zh-CN&theme=light&palette=indigo&typography=Simple', {waitUntil:'domcontentloaded'});
  await page.waitForFunction(() => window.VexAuxiliary && window.VexWorkspace);
  await page.waitForTimeout(400);
  await page.evaluate(() => { VexWorkspace.setLayout('read'); const o = document.getElementById('outline-toggle'); if (o && o.getAttribute('aria-pressed') === 'true') o.click(); });
  await page.waitForTimeout(400);

  for (const width of [1280, 2560]) {
    await page.setViewportSize({width, height:820});
    await page.evaluate(() => VexPreferences.set('zoom', 100));
    await page.waitForTimeout(300);
    for (const zoom of [50, 100, 150, 200]) {
      const data = await page.evaluate((z) => {
        VexPreferences.set('zoom', z);
        const body = document.getElementById('markdown-preview');
        const scroll = body.closest('.reading-scroll');
        const cs = getComputedStyle(body);
        const r = body.getBoundingClientRect();
        const p = body.querySelector('p');
        const pr = p.getBoundingClientRect();
        const pcs = getComputedStyle(p);
        const pre = body.querySelector('pre');
        const num = v => +parseFloat(v).toFixed(2);
        return {
          vp: window.innerWidth, zoom: cs.zoom,
          body: {physW: num(r.width), ownW: num(body.clientWidth), docH: num(r.height)},
          col: {maxw: cs.maxWidth, pad: cs.padding, font: num(cs.fontSize), lh: num(cs.lineHeight)},
          para: {physW: num(pr.width), physH: num(pr.height), lines: Math.round(pr.height / parseFloat(pcs.lineHeight))},
          pre: pre ? {physW: num(pre.getBoundingClientRect().width), overflow: pre.scrollWidth > pre.clientWidth + 1} : null,
          scroll: {scrollH: scroll.scrollHeight, scrollW: scroll.scrollWidth, clientW: scroll.clientWidth, overflowX: scroll.scrollWidth > scroll.clientWidth + 1}
        };
      }, zoom);
      console.log(JSON.stringify(data));
    }
  }
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
