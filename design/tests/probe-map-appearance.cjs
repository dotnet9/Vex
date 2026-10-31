/* 导图外观行为探针：避让、连续比较、徽标、colorful series 填色、排版自动收。 */
const assert = require('node:assert/strict');
const {chromium} = require('playwright');
(async () => {
  const browser = await chromium.launch({headless:true});
  const page = await browser.newPage({viewport:{width:1440,height:900}});
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  await page.goto('http://127.0.0.1:4317/design/Vex.html?preview=1&device=desktop');
  await page.waitForFunction(() => window.VexAuxiliary && window.VexChapters);
  await page.evaluate(() => { VexWorkspace.setLayout('map'); });
  await page.waitForTimeout(600);

  // 1) 打开弹层 → 导图避让（state.y 下移）；关闭 → 还原
  const before = await page.evaluate(() => ({ y: VexWorkbench.state.y, x: VexWorkbench.state.x }));
  await page.locator('.map-appearance-trigger').click();
  await page.waitForTimeout(500);
  const during = await page.evaluate(() => ({ y: VexWorkbench.state.y, open: !document.querySelector('#map-appearance-panel').hidden }));
  assert.equal(during.open, true, '弹层应打开');
  assert.ok(during.y > before.y + 10, '避让应把内容下移（y ' + before.y + ' -> ' + during.y + '）');
  await page.keyboard.press('Escape');
  await page.waitForTimeout(500);
  const restored = await page.evaluate(() => ({ y: VexWorkbench.state.y, open: !document.querySelector('#map-appearance-panel').hidden }));
  assert.equal(restored.open, false, 'Esc 应关闭');
  assert.ok(Math.abs(restored.y - before.y) < 2, '关闭后视口应还原（' + restored.y + ' vs ' + before.y + '）');

  // 2) 徽标和连续比较：选风格后保留焦点，关闭时回到触发按钮。
  await page.locator('.map-appearance-trigger').click();
  await page.waitForTimeout(300);
  await page.locator('[data-map-style-choice="colorful"]').click();
  const badge = await page.evaluate(() => document.querySelector('.map-appearance-trigger span').textContent);
  assert.match(badge, /·/, '徽标应为「结构 · 风格」组合：' + badge);
  assert.equal(await page.evaluate(() => document.querySelector('#map-appearance-panel').hidden), false, '选完风格后可继续比较');
  assert.equal(await page.evaluate(() => document.activeElement.dataset.mapStyleChoice), 'colorful', '选中后焦点留在选项');
  assert.equal(badge, await page.evaluate(() => document.querySelector('.map-appearance-trigger span').textContent));
  await page.keyboard.press('Escape');
  assert.equal(await page.locator('.map-appearance-trigger').evaluate(el => el === document.activeElement), true);

  // 3) colorful series 填色：标题节点吃 series 实色 + --branch-ink
  const inked = await page.evaluate(() => {
    const node = [...document.querySelectorAll('.map-node')].find(n => !n.classList.contains('root') && !n.classList.contains('content'));
    return node ? { color: node.style.getPropertyValue('--branch-color'), ink: node.style.getPropertyValue('--branch-ink'), fill: getComputedStyle(node).backgroundColor } : null;
  });
  assert.ok(inked, '应有标题节点');
  assert.match(inked.color, /^#/, 'colorful 下分支色应为 series 实色：' + inked.color);
  assert.ok(inked.ink && inked.ink.startsWith('#'), '应有可读前景 --branch-ink：' + inked.ink);
  assert.notEqual(inked.fill, 'rgba(0, 0, 0, 0)', '标题节点应为实色填充');
  await page.locator('.map-appearance-trigger').click();
  await page.waitForTimeout(300);
  await page.keyboard.press('Escape');

  // 4) 换回 quiet 不应收起、节点回到强调色
  await page.evaluate(() => { document.querySelector('#map-appearance-panel') ; VexMapAppearance && null; });
  await page.locator('.map-appearance-trigger').click();
  await page.waitForTimeout(300);
  await page.locator('[data-map-style-choice="quiet"]').click();
  await page.waitForTimeout(1100);
  assert.equal(await page.evaluate(() => document.querySelector('#map-appearance-panel').hidden), false, 'quiet 同样允许继续比较');
  const quietColor = await page.evaluate(() => {
    const node = [...document.querySelectorAll('.map-node')].find(n => !n.classList.contains('root') && !n.classList.contains('content'));
    return node.style.getPropertyValue('--branch-color');
  });
  assert.match(quietColor, /var\(--/, 'quiet 应回到强调色家族：' + quietColor);
  await page.keyboard.press('Escape');

  // 5) 阅读排版：选卡自动收
  await page.evaluate(() => { VexWorkspace.setLayout('read'); });
  await page.waitForTimeout(400);
  await page.locator('[data-action="typography"]:not([hidden])').click();
  await page.waitForTimeout(300);
  assert.equal(await page.evaluate(() => !document.querySelector('#typography-panel').hidden), true, '排版弹层应打开');
  await page.locator('#typography-panel [data-typography-key]').nth(1).click();
  await page.waitForTimeout(1100);
  assert.equal(await page.evaluate(() => document.querySelector('#typography-panel').hidden), true, '排版选卡后应自动收起');

  assert.equal(errors.length, 0, '页面不应有 JS 错误：' + errors.join(' | '));
  console.log('PASS: 避让/还原、徽标回显、连续比较、colorful series 填色与回退、排版自动收 全部通过');
  await browser.close();
})().catch(e => { console.error('FAIL:', e.message); process.exit(1); });
