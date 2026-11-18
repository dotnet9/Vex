// 图鉴弹层计算样式真值探针：输出 .dg-picker 各部件 getComputedStyle 与关键 CSS 变量，
// 作为 Vex 侧样式对齐的数值基准（98% parity 的对照源）。
const { chromium } = require('playwright');
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 1000 } });
  await page.goto('http://127.0.0.1:4319/design/Vex.html?preview=1&lang=zh-CN&theme=light&palette=indigo&typography=Simple', { waitUntil: 'networkidle' });
  await page.waitForTimeout(1500);
  await page.click('[data-action="insert-mermaid-picker"]');
  await page.waitForSelector('.dg-picker', { timeout: 10000 });
  await page.waitForTimeout(4000);
  const truth = await page.evaluate(() => {
    const pick = (sel, props) => {
      const el = document.querySelector(sel);
      if (!el) return null;
      const cs = getComputedStyle(el);
      return Object.fromEntries(props.map(p => [p, cs.getPropertyValue(p)]));
    };
    const vars = {};
    for (const v of ['--bg', '--surface', '--border', '--muted', '--fg', '--accent',
      '--vex-radius-sm', '--vex-radius-lg', '--panel', '--hover']) {
      vars[v] = getComputedStyle(document.documentElement).getPropertyValue(v).trim();
    }
    return {
      vars,
      picker: pick('.dg-picker', ['width', 'background-color', 'border', 'border-radius', 'box-shadow']),
      tabs: pick('.dg-picker .dg-tabs', ['padding', 'gap', 'border-bottom', 'height']),
      tab: pick('.dg-picker .dg-tab:not(.on)', ['padding', 'font-size', 'color', 'background-color', 'border', 'border-radius']),
      tabOn: pick('.dg-picker .dg-tab.on', ['padding', 'font-size', 'color', 'background-color', 'border-bottom', 'font-weight', 'border-radius']),
      grid: pick('.dg-picker .dg-grid', ['grid-template-columns', 'gap', 'padding', 'height']),
      card: pick('.dg-picker .dg-card:not([hidden])', ['padding', 'gap', 'background-color', 'border', 'border-radius', 'font-size', 'color']),
      thumb: pick('.dg-picker .dg-thumb', ['height', 'border-radius', 'background-color']),
      thumbSvg: pick('.dg-picker .dg-thumb svg', ['max-height', 'max-width']),
      label: pick('.dg-picker .dg-label', ['font-size', 'color']),
      pickerRect: document.querySelector('.dg-picker').getBoundingClientRect().toJSON(),
      tabRect: document.querySelector('.dg-picker .dg-tab').getBoundingClientRect().toJSON(),
      cardRect: document.querySelector('.dg-picker .dg-card:not([hidden])').getBoundingClientRect().toJSON(),
      counts: {
        tabs: document.querySelectorAll('.dg-picker .dg-tab').length,
        cards: document.querySelectorAll('.dg-picker .dg-card').length,
        visible: document.querySelectorAll('.dg-picker .dg-card:not([hidden])').length,
      },
    };
  });
  console.log(JSON.stringify(truth, null, 1));
  await page.screenshot({ path: 'artifacts/dg-proto-ref.png' });
  // 切到组 2（结构与关系，9 张）再截一张
  await page.evaluate(() => document.querySelectorAll('.dg-tabs .dg-tab')[2].click());
  await page.waitForTimeout(500);
  await page.screenshot({ path: 'artifacts/dg-proto-ref-g2.png' });
  await browser.close();
  process.exit(0);
})();
