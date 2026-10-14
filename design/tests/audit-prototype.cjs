const assert = require('node:assert/strict');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');

// 检查真实原型事件与系统主题变化，浏览器在后台运行。
(async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(pathToFileURL(path.join(__dirname, '..', 'index.html')).href);
    await page.locator('#vex-theme-fab').click();
    const palettes = await page.locator('#vex-theme-panel [data-palette]').evaluateAll(items => items.map(item => item.dataset.palette));
    assert.equal(palettes.length, 8);
    for (const palette of palettes) {
      await page.locator(`#vex-theme-panel [data-palette="${palette}"]`).click();
      for (const mode of ['light', 'dark']) {
        await page.locator(`#vex-theme-panel [data-mode="${mode}"]`).click();
        assert.equal(await page.locator('html').getAttribute('data-theme'), mode);
        assert.equal(await page.locator('html').getAttribute('data-palette'), palette);
        assert.equal(await page.locator(`#vex-theme-panel [data-palette="${palette}"]`).getAttribute('aria-pressed'), 'true');
        const bounds = await page.evaluate(() => {
          const title = document.querySelector('.app-window > .titlebar').getBoundingClientRect();
          const content = document.querySelector('.app-window > .window-body').getBoundingClientRect();
          return [title.bottom, content.top];
        });
        assert.ok(Math.abs(bounds[0] - bounds[1]) <= 1, 'No blank row between title and workspace');
      }
    }
    await page.locator('#vex-theme-panel [data-mode="system"]').click();
    for (const mode of ['light', 'dark']) {
      await page.emulateMedia({ colorScheme: mode });
      await page.waitForFunction(value => document.documentElement.dataset.theme === value, mode);
      assert.equal(await page.locator('html').getAttribute('data-theme-mode'), 'system');
    }
    // 通过标题菜单点击，避免仅验证悬浮色板而漏掉实际菜单接线。
    await page.locator('#vex-theme-fab').click();
    const openThemes = async () => {
      await page.locator('.tb-menuitem').filter({ hasText: '帮助(H)' }).click();
      await page.locator('.menu-flyout > .flyout > .menu-item').filter({ hasText: '主题色' }).hover();
    };
    await openThemes();
    await page.locator('.submenu:visible > .menu-item').filter({ hasText: /^AI 极光$/ }).click();
    assert.equal(await page.locator('html').getAttribute('data-palette'), 'aurora');
    await openThemes();
    await page.locator('.submenu:visible > .menu-item').filter({ hasText: /^浅色$/ }).click();
    assert.equal(await page.locator('html').getAttribute('data-theme-mode'), 'light');
    await openThemes();
    assert.equal(await page.locator('.submenu:visible > .menu-item').filter({ hasText: /^AI 极光$/ }).evaluate(item => item.classList.contains('checked')), true);
    assert.equal(await page.locator('.submenu:visible > .menu-item').filter({ hasText: /^浅色$/ }).evaluate(item => item.classList.contains('checked')), true);
    await page.locator('.tb-menuitem').filter({ hasText: '帮助(H)' }).click();
    await page.emulateMedia({ colorScheme: 'dark' });
    assert.equal(await page.locator('html').getAttribute('data-theme'), 'light');
    assert.deepEqual(errors, []);
    console.log('Passed: eight palettes, sixteen light/dark combinations, system mode, menu adapter, and title layout.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
