const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
  await page.goto('http://127.0.0.1:4319/design/Vex.html?preview=1', { waitUntil: 'networkidle' });
  await page.waitForTimeout(1500);
  await page.click('[data-action="insert-mermaid-picker"]');
  await page.waitForTimeout(5000);
  await page.screenshot({ path: 'artifacts/picker-h320.png' });
  const stats = await page.evaluate(() => {
    const grid = document.querySelector('.dg-picker .dg-grid');
    const tabs = [...document.querySelectorAll('.dg-tabs .dg-tab')];
    const visible = [...document.querySelectorAll('.dg-card')].filter(c => !c.hidden);
    return {
      gridH: grid ? grid.getBoundingClientRect().height : -1,
      tabs: tabs.length, visible: visible.length,
      withSvg: visible.filter(c => c.querySelector('.dg-thumb svg')).length,
    };
  });
  console.log('stats:', JSON.stringify(stats));
  console.log('PASS' + (Math.round(stats.gridH) === 375 && stats.visible === 4 ? '' : ' - CHECK'));
  await browser.close();
})();
