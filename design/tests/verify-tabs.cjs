// 图鉴 TabControl 视觉验证：tab 行连体面板+选中 accent 条+卡片 24 张
const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
  await page.goto('http://127.0.0.1:4319/design/Vex.html?preview=1', { waitUntil: 'networkidle' });
  await page.waitForTimeout(1500);
  await page.click('[data-action="insert-mermaid-picker"]');
  await page.waitForSelector('.dg-picker', { timeout: 10000 });
  await page.waitForTimeout(5000);
  const stats = await page.evaluate(() => {
    const tabs = [...document.querySelectorAll('.dg-tab')];
    const on = tabs.filter(t => t.classList.contains('on'));
    const cards = [...document.querySelectorAll('.dg-card')];
    const visible = cards.filter(c => !c.hidden);
    return { tabs: tabs.length, onTabs: on.length, cards: cards.length, visible: visible.length };
  });
  console.log('stats:', JSON.stringify(stats));
  await page.screenshot({ path: 'artifacts/tabs-final.png' });
  const ok = stats.tabs === 4 && stats.onTabs === 1 && stats.cards === 24 && stats.visible === 4;
  console.log(ok ? 'PASS' : 'FAIL');
  await browser.close();
  process.exit(ok ? 0 : 1);
})();
