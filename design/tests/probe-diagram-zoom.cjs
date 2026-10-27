/* 临时探针：阅读视图里 mermaid 图表块（含右上角徽标）在各档内容缩放下的真实几何。
 * 需要本地静态服务：python -m http.server 4317 --bind 127.0.0.1
 * node design/tests/probe-diagram-zoom.cjs */
const {chromium} = require('playwright');
const base = process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/';

(async () => {
  const browser = await chromium.launch({headless:true});
  const page = await browser.newPage({viewport:{width:1280, height:820}, deviceScaleFactor:1});
  await page.goto(base + 'Vex.html?preview=1&lang=zh-CN&theme=light&palette=indigo&typography=Simple', {waitUntil:'domcontentloaded'});
  await page.waitForFunction(() => window.VexAuxiliary && window.VexWorkspace);
  await page.waitForTimeout(500);
  await page.evaluate(() => { VexWorkspace.setLayout('read'); const o = document.getElementById('outline-toggle'); if (o && o.getAttribute('aria-pressed') === 'true') o.click(); });
  await page.waitForTimeout(400);
  // 换到含 flowchart LR 的「Mermaid 图表图鉴」样例，和原生探针用同一张图。
  const found = await page.evaluate(() => {
    const rows = [...document.querySelectorAll('#file-tree [data-doc-index], #file-tree button, .file-row')];
    const hit = rows.find(el => (el.textContent || '').includes('Mermaid'));
    if (hit) hit.click();
    return !!hit;
  });
  await page.waitForTimeout(1200);
  console.log('SWITCHED ' + found);

  for (const zoom of [50, 100, 150, 200]) {
    const data = await page.evaluate((z) => {
      VexPreferences.set('zoom', z);
      const num = v => +v.toFixed(2);
      const box = el => { const r = el.getBoundingClientRect(); return {x:num(r.x), y:num(r.y), w:num(r.width), h:num(r.height)}; };
      const host = document.querySelector('.md-diagram');
      if (!host) return {missing:true, zoom:z};
      host.scrollIntoView({block:'center'});
      const preview = host.querySelector('.diagram-preview');
      const svg = host.querySelector('svg');
      const badge = host.querySelector('.diagram-badge');
      const body = document.getElementById('markdown-preview');
      const inter = (a, b) => {
        if (!a || !b) return null;
        const w = Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x);
        const h = Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y);
        return w > 0 && h > 0 ? {w:+w.toFixed(1), h:+h.toFixed(1)} : null;
      };
      const pb = box(preview), sb = svg && box(svg), bb = badge && box(badge), hb = box(host);
      const cs = getComputedStyle(preview);
      // 图表节点（flowchart 的矩形/菱形）真实外框：用 svg 的可视内容包围盒，判断徽标是否压在图形上。
      const ink = svg ? (() => { const b = svg.getBBox(); const c = svg.getScreenCTM(); const p = svg.createSVGPoint();
        p.x = b.x; p.y = b.y; const a = p.matrixTransform(c); p.x = b.x + b.width; p.y = b.y + b.height; const d = p.matrixTransform(c);
        return {x:num(Math.min(a.x,d.x)), y:num(Math.min(a.y,d.y)), w:num(Math.abs(d.x-a.x)), h:num(Math.abs(d.y-a.y))}; })() : null;
      const nodes = svg ? [...svg.querySelectorAll('.node, .edgePath, .label')].slice(0, 40).map(box) : [];
      const nodeHits = badge ? nodes.filter(n => inter(box(badge), n)) : [];
      return {
        zoom: getComputedStyle(body).zoom, vp: window.innerWidth,
        host: hb, preview: pb, svg: sb, ink, badge: bb,
        previewPad: cs.padding, previewMinH: cs.minHeight, badgeFont: badge && getComputedStyle(badge).font,
        overlapBadgeSvg: inter(bb, sb), overlapBadgeInk: inter(bb, ink), badgeOverNodes: nodeHits.length,
        scroll: {scrollW: document.querySelector('.reading-scroll').scrollWidth,
                 clientW: document.querySelector('.reading-scroll').clientWidth}
      };
    }, zoom);
    console.log(JSON.stringify(data));
    await page.waitForTimeout(400);
  }
  await page.screenshot({path: process.env.VEX_DIAGRAM_SHOT || 'D:/github/temp_files/zoom-shots/proto-diagram-200.png'});
  await browser.close();
})().catch(e => { console.error(e); process.exit(1); });
