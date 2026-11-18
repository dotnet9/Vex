// 图鉴缩略图导出（一次性工具）：在原型页面里渲染 24 张 Mermaid（页面自带 CSS，
// foreignObject 文字正常渲染，所见即所得）→ 520x220 白底容器 2x 高清 PNG。
// 产物 src/Vex/Assets/MermaidGallery/*.png 由 XAML 静态绑定（零解析零光栅化，弹出即现）。
const { chromium } = require('playwright');
const OUT = 'E:/github/Apps/Vex/src/Vex/Assets/MermaidGallery';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 1000 }, deviceScaleFactor: 2 });
  await page.goto('http://127.0.0.1:4319/design/Vex.html?preview=1', { waitUntil: 'networkidle' });
  await page.waitForTimeout(2000);

  // 截图容器：固定在视口左上角，白底，图等比适配。
  await page.evaluate(() => {
    const box = document.createElement('div');
    box.id = 'shotbox';
    box.style.cssText = 'position:fixed;left:0;top:0;width:520px;height:220px;background:#ffffff;'
      + 'display:flex;align-items:center;justify-content:center;overflow:hidden;z-index:99999;';
    document.body.appendChild(box);
  });

  const items = await page.evaluate(() => (window.VexMermaid ? VexMermaid.items.map(i => ({ key: i.key, source: i.source })) : []));
  let count = 0;
  for (const item of items) {
    await page.evaluate(async ({ key, source }) => {
      const box = document.getElementById('shotbox');
      const svg = await VexDiagramRendering.render('shot-' + key, source);
      box.innerHTML = svg.replace('<svg ', '<svg style="max-width:500px;max-height:200px;width:auto;height:auto;" ');
    }, item);
    await page.waitForTimeout(120);
    await page.locator('#shotbox').screenshot({ path: OUT + '/' + item.key + '.png' });
    count++;
    console.log('png:', item.key);
  }
  await browser.close();
  console.log('exported:', count, 'pngs to', OUT);
  process.exit(count === 24 ? 0 : 1);
})();
