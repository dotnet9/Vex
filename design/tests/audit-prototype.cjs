const standaloneFixture = require('./standalone-fixture.cjs');
const assert = require('node:assert/strict');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');

// 检查真实原型事件与系统主题变化，浏览器在后台运行。
(async () => {
  const standalone = standaloneFixture(path.resolve(__dirname, '../index.html'));
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await standalone.attach(page);
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(standalone.url);
    const selectMode=async mode=>{for(let i=0;i<3&&await page.locator('html').getAttribute('data-theme-mode')!==mode;i++)await page.locator('#vex-mode-toggle').click();};
    const palettes = await page.locator('#vex-theme-panel [data-palette]').evaluateAll(items => items.map(item => item.dataset.palette));
    assert.equal(palettes.length, 8);
    assert.deepEqual(palettes,['dotnet','aurora','ember','default','aquatic','desert','dusk','night-sky']);
    const swatches=()=>page.locator('#vex-theme-panel [data-palette] i').evaluateAll(items=>items.map(el=>({color:getComputedStyle(el).backgroundColor,image:getComputedStyle(el).backgroundImage})));
    const fixedSwatches=await swatches(),expectedColors=['rgb(81, 43, 212)',null,'rgb(189, 103, 77)','rgb(0, 100, 250)','rgb(0, 141, 158)','rgb(181, 117, 50)','rgb(122, 92, 196)','rgb(43, 108, 176)'];
    assert.equal(fixedSwatches.some((item,index)=>index===1?!item.image.includes('rgb(137, 104, 255), rgb(38, 212, 204)'):item.color!==expectedColors[index]),false,'StudioSwatch .21 colors and gradient');
    const normalWidth=await page.locator('.app-window').evaluate(el=>el.getBoundingClientRect().width);
    await page.locator('#caption-btns [data-action=maximize]').click();assert.ok(await page.locator('.app-window').evaluate(el=>el.getBoundingClientRect().width)>normalWidth);await page.locator('#caption-btns [data-action=maximize]').click();assert.equal(await page.locator('.app-window').evaluate(el=>el.getBoundingClientRect().width),normalWidth);
    for(const action of ['minimize','close']){await page.locator('#caption-btns [data-action='+action+']').click();assert.equal(await page.locator('.app-window').isVisible(),false);assert.equal(await page.locator('#restore-window').isVisible(),true);await page.locator('#restore-window').click();assert.equal(await page.locator('.app-window').isVisible(),true);}
    for (const palette of palettes) {
      await page.locator(`#vex-theme-panel [data-palette="${palette}"]`).click();
      for (const mode of ['light', 'dark']) {
        await selectMode(mode);
        assert.deepEqual(await swatches(),fixedSwatches,'Palette dots do not follow theme Accent');
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
    await selectMode('system');
    for (const mode of ['light', 'dark']) {
      await page.emulateMedia({ colorScheme: mode });
      await page.waitForFunction(value => document.documentElement.dataset.theme === value, mode);
      assert.equal(await page.locator('html').getAttribute('data-theme-mode'), 'system');
    }
    // 通过标题菜单点击，避免仅验证悬浮色板而漏掉实际菜单接线。
    const openThemes = async () => {
      await page.locator('.tb-menuitem').filter({ hasText: '视图(V)' }).click();
      await page.locator('.menu-flyout > .flyout > .menu-item').filter({ hasText: '界面配色' }).hover();
    };
    const themeItem = label => page.locator('.submenu:visible > .menu-item').filter({
      has: page.locator('.menu-label').filter({ hasText: new RegExp('^' + label + '$') })
    });
    await openThemes();
    await themeItem('AI 极光').click();
    assert.equal(await page.locator('html').getAttribute('data-palette'), 'aurora');
    await openThemes();
    await themeItem('浅色').click();
    assert.equal(await page.locator('html').getAttribute('data-theme-mode'), 'light');
    await openThemes();
    assert.equal(await themeItem('AI 极光').evaluate(item => item.classList.contains('checked')), true);
    assert.equal(await themeItem('浅色').evaluate(item => item.classList.contains('checked')), true);
    await page.locator('.tb-menuitem').filter({ hasText: '视图(V)' }).click();
    await page.emulateMedia({ colorScheme: 'dark' });
    assert.equal(await page.locator('html').getAttribute('data-theme'), 'light');
    assert.deepEqual(errors, []);
    await standalone.verify(page);
    console.log('Passed: eight palettes, sixteen light/dark combinations, system mode, menu adapter, and title layout.');
  } finally { await browser.close(); standalone.dispose(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
