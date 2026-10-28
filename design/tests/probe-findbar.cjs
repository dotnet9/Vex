/* findbar 三个开关（正则/大小写/整词）：存在、可切换、aria-pressed 回放。 */
const assert = require('node:assert/strict');
const {chromium} = require('playwright');
(async () => {
  const browser = await chromium.launch({headless:true});
  const page = await browser.newPage({viewport:{width:1440,height:900}});
  const errors = [];
  page.on('pageerror', e => errors.push(e.message));
  await page.goto('http://127.0.0.1:4317/design/Vex.html?preview=1&device=desktop');
  await page.waitForFunction(() => window.VexAuxiliary && window.VexChapters);
  await page.evaluate(() => document.querySelector('.find-launch')?.click() || VexWorkspace.toggleFind?.());
  await page.waitForTimeout(300);
  assert.equal(await page.evaluate(() => !document.querySelector('#findbar').hidden), true, 'findbar 应打开');
  for (const [action, label] of [['find-regex', '正则'], ['find-case', '大小写'], ['find-word', '整词']]) {
    const button = page.locator(`[data-action="${action}"]`);
    assert.equal(await button.count(), 1, `${label}开关应存在`);
    assert.equal(await button.getAttribute('aria-pressed'), 'false', `${label}初始未按下`);
    await button.click();
    assert.equal(await button.getAttribute('aria-pressed'), 'true', `${label}点击后按下`);
    await button.click();
    assert.equal(await button.getAttribute('aria-pressed'), 'false', `${label}再点恢复`);
  }
  assert.equal(errors.length, 0, '页面不应有 JS 错误：' + errors.join(' | '));
  console.log('PASS: findbar 三开关（正则/大小写/整词）存在、可切换、状态回放正确');
  await browser.close();
})().catch(e => { console.error('FAIL:', e.message); process.exit(1); });
