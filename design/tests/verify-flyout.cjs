// 图鉴浮层视觉验证：贴按钮、无遮罩无标题、tab 切换过滤、缩略图渲染
const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
  await page.goto('http://127.0.0.1:4319/design/Vex.html?preview=1', { waitUntil: 'networkidle' });
  await page.waitForTimeout(1500);
  await page.click('[data-action="insert-mermaid-picker"]');
  await page.waitForTimeout(5000);
  const stats = await page.evaluate(() => {
    const tabs = [...document.querySelectorAll('.dg-tabs .dg-tab')];
    const on = tabs.filter(t => t.classList.contains('on'));
    const cards = [...document.querySelectorAll('.dg-card')];
    const visible = cards.filter(c => !c.hidden);
    const withSvg = visible.filter(c => c.querySelector('.dg-thumb svg')).length;
    return { tabs: tabs.length, on: on.length, cards: cards.length, visible: visible.length, withSvg };
  });
  console.log('stats:', JSON.stringify(stats));
  await page.screenshot({ path: 'artifacts/flyout-final.png' });
  // 切组 2 验证过滤
  await page.evaluate(() => document.querySelectorAll('.dg-tabs .dg-tab')[2].click());
  await page.waitForTimeout(3000);
  const after = await page.evaluate(() => [...document.querySelectorAll('.dg-card')].filter(c => !c.hidden).length);
  console.log('visible after group switch:', after);
  const ok = stats.tabs === 4 && stats.cards === 24 && stats.visible === 4 && after === 9;
  console.log(ok ? 'PASS' : 'FAIL');
  await browser.close();
  process.exit(ok ? 0 : 1);
})();
