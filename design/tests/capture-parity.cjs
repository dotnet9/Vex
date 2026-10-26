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

const SAMPLE = [
  '# 成都，慢慢走', '',
  '这个周末，去成都。把脚步放慢一点，给茶香和街巷多留些时间。', '',
  '## 出发清单', '',
  '- [ ] 带上相机', '- [ ] 预订车票', '- [x] 收拾行李', '',
  '> 慢一点，才能看见城市的细节。', '',
  '## 第一天行程', '',
  '| 时间 | 地点 | 说明 |', '| --- | --- | --- |', '| 09:00 | 人民公园 | 喝一碗盖碗茶 |', '| 14:00 | 宽窄巷子 | 沿青砖小巷慢慢走 |', '',
  '### 人民公园', '',
  '坐在茶社里，看本地人摆龙门阵，一下午就过去了。', ''
].join('\n');

(async () => {
  const browser = await chromium.launch({headless:true, ...(process.env.VEX_BROWSER_CHANNEL ? {channel:process.env.VEX_BROWSER_CHANNEL} : {})});
  const page = await browser.newPage({viewport:{width:1488, height:948}, deviceScaleFactor:1});
  await page.goto(base + 'Vex.html?preview=1&lang=zh-CN&theme=light&palette=indigo&typography=Simple', {waitUntil:'domcontentloaded'});
  await page.waitForFunction(() => window.VexAuxiliary && window.VexChapters);
  await page.waitForTimeout(400);
  // 与原生截图器对齐：注入同一段样例文档。
  await page.evaluate(text => { if (window.VexWorkbench && VexWorkbench.applyText) VexWorkbench.applyText(text); }, SAMPLE);
  // 数据形态不在对比范围（原生列真实磁盘目录）：拍平原型分组树并按名称排序后再截图。
  await page.evaluate(() => {
    const tree = document.querySelector('#file-tree, .file-tree, [class*=file-list]') || document.querySelector('.sidebar .file-list, nav');
    if (!tree) return;
    const group = tree.querySelector('details, .file-directory');
    const rows = group ? Array.from(group.querySelectorAll('.file-children > *')) : Array.from(tree.children);
    if (group) group.remove();
    const flat = tree.querySelector('.file-children');
    const host = flat && flat.parentElement === tree ? flat : tree;
    rows.forEach(row => host.appendChild(row));
    const ordered = Array.from(host.children).sort((a, b) => (a.textContent || '').trim() < (b.textContent || '').trim() ? -1 : 1);
    ordered.forEach(row => host.appendChild(row));
  });

  // 通过「更多」布局菜单切换八布局；三段按钮只负责 source/read/map 直选。
  const layoutMenu = async key => {
    await page.evaluate(() => { const more = document.querySelector('#workspace-layout'); if (more && document.querySelector('#workspace-layout-menu[hidden]')) more.click(); });
    await page.waitForTimeout(200);
    await page.evaluate(k => { const item = document.querySelector('#workspace-layout-menu [data-layout="' + k + '"]'); if (item) item.click(); }, key);
    await page.waitForTimeout(450);
  };
  const segment = async key => {
    await page.evaluate(k => { const link = document.querySelector('.workspace-viewswitch [data-view-link="' + k + '"]'); if (link) link.click(); }, key);
    await page.waitForTimeout(450);
  };
  const shot = async name => {
    await page.waitForTimeout(350);
    // 布局菜单点击后焦点会落回「更多」段，产生原生没有的焦点环：截图前失焦。
    await page.evaluate(() => document.activeElement && document.activeElement.blur());
    await page.waitForTimeout(120);
    // 原型窗口卡片固定内缩 24px：对卡片元素截图得到与原生客户端区一致的 1440x900。
    await page.locator('.app-window').screenshot({path:path.join(output, name + '.png')});
    console.log('CAPTURED ' + name);
  };

  await layoutMenu('instant');        await shot('01-instant');
  await segment('source');            await shot('02-source');
  await segment('read');              await shot('03-read');
  await segment('map');               await shot('04-map');
  await layoutMenu('source-render');  await shot('05-source-render');
  await layoutMenu('three');          await shot('06-three');
  // 07 外观弹出层（回到即时编辑再开）
  await layoutMenu('instant');
  await page.evaluate(() => document.querySelector('#vex-appearance-menu > summary').click());
  await shot('07-appearance-menu');
  // 08 偏好设置：文件菜单 → 偏好设置… → 外观分类
  await page.evaluate(() => document.querySelector('#vex-appearance-menu > summary').click());
  await page.getByText('文件(F)').first().click();
  await page.waitForTimeout(300);
  await page.getByText('偏好设置…').first().click();
  await page.waitForTimeout(600);
  await page.evaluate(() => { const nav = document.querySelector('.vex-settings-host [data-vex-section="appearance"]'); if (nav) nav.click(); });
  await shot('08-preferences');
  // 09 排版画廊：Esc 关偏好，点排版入口
  await page.keyboard.press('Escape');
  await page.waitForTimeout(300);
  await page.evaluate(() => { const trigger = document.querySelector('[data-action="typography"]'); if (trigger) trigger.click(); });
  await shot('09-typography-gallery');
  // 10 演示：Esc 关画廊，F5 进演示
  await page.keyboard.press('Escape');
  await page.waitForTimeout(300);
  await page.keyboard.press('F5');
  await shot('10-presentation');
  await browser.close();
})().catch(error => { console.error(error); process.exit(1); });
