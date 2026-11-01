/* 与桌面端逐场景离屏对比的原型截图器（配套 scripts/ui-verification 的 visual 模式）。
 * 同尺寸 1440x900、同语言、同明暗、同配色与排版、同文档内容。
 * 需要 Playwright 与运行中的预览服务：node design/scripts/preview.cjs
 * NODE_PATH=CodeWF.Avalonia/design/tests/node_modules node design/tests/capture-parity.cjs <输出目录>
 */
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require('playwright');
const base = process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/';
const output = path.resolve(process.argv[2] || path.join(__dirname, '../../artifacts/prototype-parity'));
fs.mkdirSync(output, {recursive:true});

const SAMPLE = fs.readFileSync(path.join(__dirname, 'fixtures/parity-sample.md'), 'utf8');

(async () => {
  const browser = await chromium.launch({headless:true, ...(process.env.VEX_BROWSER_CHANNEL ? {channel:process.env.VEX_BROWSER_CHANNEL} : {})});
  const page = await browser.newPage({viewport:{width:1488, height:948}, deviceScaleFactor:1});
  await page.goto(base + 'Vex.html?preview=1&lang=zh-CN&theme=light&palette=indigo&typography=Simple', {waitUntil:'domcontentloaded'});
  await page.waitForFunction(() => window.VexWorkspace && window.VexChapters);
  await page.waitForTimeout(400);
  // 与原生截图器对齐：注入同一段样例文档。
  await page.evaluate(text => { if (window.VexWorkbench && VexWorkbench.applyText) VexWorkbench.applyText(text); }, SAMPLE);
  // 固定同一阅读起点，避免来源窗格的滚动同步在截图前改写 scrollTop。
  await page.evaluate(() => VexWorkbench.setFollow(false));
  // 两端都通过真实列表模式呈现同一批演示文档，不手工重排导航DOM。
  await page.evaluate(() => {
    if (document.querySelector('#file-list .file-directory')) document.querySelector('#files-toggle').click();
    document.querySelector('.files-sort-menu [data-sort-key="name-asc"]').click();
  });

  // 通过「更多」布局菜单切换八布局；三段按钮只负责 source/read/map 直选。
  const layoutMenu = async key => {
    await page.evaluate(() => { const more = document.querySelector('#workspace-layout'); if (more && document.querySelector('#workspace-layout-menu[hidden]')) more.click(); });
    await page.waitForTimeout(200);
    await page.evaluate(k => { const item = document.querySelector('#workspace-layout-menu [data-layout="' + k + '"]'); if (item) item.click(); }, key);
    await page.waitForTimeout(450);
  };
  const segment = async key => {
    await page.evaluate(k => { const link = document.querySelector('.workspace-tabs [data-view-link="' + k + '"]'); if (link) link.click(); }, key);
    await page.waitForTimeout(450);
  };
  const shot = async name => {
    await page.waitForTimeout(350);
    await page.evaluate(() => document.querySelectorAll('.reading-scroll,.instant-scroll,.source-scroll').forEach(el => el.scrollTo({top:0, left:0, behavior:'instant'})));
    // 布局菜单点击后焦点会落回「更多」段，产生原生没有的焦点环：截图前失焦。
    await page.evaluate(() => document.activeElement && document.activeElement.blur());
    await page.waitForTimeout(120);
    const offsets = await page.evaluate(() => Array.from(document.querySelectorAll('.reading-scroll,.instant-scroll,.source-scroll'))
      .filter(el => el.getClientRects().length).map(el => ({name:el.className, top:el.scrollTop, left:el.scrollLeft})));
    if (offsets.some(el => el.top !== 0 || el.left !== 0)) throw new Error('Capture must start at document origin: ' + JSON.stringify(offsets));
    // 原型窗口卡片固定内缩 24px：对卡片元素截图得到与原生客户端区一致的 1440x900。
    await page.locator('.app-window').screenshot({path:path.join(output, name + '.png')});
    console.log('CAPTURED ' + name);
  };

  await layoutMenu('instant');        await shot('01-instant');
  await layoutMenu('source');         await shot('02-source');
  await segment('read');              await shot('03-read');
  await segment('map');               await shot('04-map');
  await layoutMenu('source-render');  await shot('05-source-render');
  await layoutMenu('three');          await shot('06-three');
  // 07 外观弹出层（回到即时编辑再开）
  await layoutMenu('instant');
  await page.evaluate(() => document.querySelector('#vex-appearance-menu > summary').click());
  await shot('07-appearance-menu');
  // 08 偏好设置：导航栏设置入口 → 外观分类
  await page.evaluate(() => document.querySelector('#vex-appearance-menu > summary').click());
  await page.locator('.rail-item[data-action="preferences"]').click();
  await page.waitForTimeout(600);
  await page.evaluate(() => { const nav = document.querySelector('.vex-settings-host [data-vex-section="appearance"]'); if (nav) nav.click(); });
  await shot('08-preferences');
  // 09 排版画廊：Esc 关偏好，点排版入口
  await page.keyboard.press('Escape');
  await page.waitForTimeout(300);
  await page.evaluate(() => { const trigger = document.querySelector('[data-action="typography"]:not([hidden])'); if (trigger) trigger.click(); });
  await shot('09-typography-gallery');
  // 10 演示：Esc 关画廊，F6 进演示（workbench.js 的 startPresentation 绑在 F6，不是 F5）
  await page.keyboard.press('Escape');
  await page.waitForTimeout(300);
  // 浏览器进入演示全屏后不能再调用 Browser.setWindowBounds，先固定捕获视口。
  await page.setViewportSize({width:1440, height:900});
  await page.keyboard.press('F6');
  await page.waitForTimeout(600);
  // deck 是 position:fixed 铺满浏览器视口的全屏层，用内缩 24px 的窗口卡片裁它会切掉标题栏。
  // 校验 CSS 全屏层确实使用原生同框尺寸，禁止静默裁剪或取交集。
  const deckSize = await page.evaluate(() => ({width:innerWidth, height:innerHeight}));
  if (deckSize.width !== 1440 || deckSize.height !== 900) throw new Error('Invalid presentation viewport: ' + JSON.stringify(deckSize));
  await page.waitForTimeout(400);
  await page.evaluate(() => document.activeElement && document.activeElement.blur());
  await page.waitForTimeout(120);
  await page.screenshot({path:path.join(output, '10-presentation.png')});
  console.log('CAPTURED 10-presentation');
  await browser.close();
})().catch(error => { console.error(error); process.exit(1); });
