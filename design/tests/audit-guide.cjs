const assert = require('node:assert/strict');
const path = require('node:path');
const os = require('node:os');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const errors = [], downloads = [];
    page.on('pageerror', error => errors.push(error.message));
    page.on('download', download => downloads.push(download.suggestedFilename()));
    const url = pathToFileURL(path.join(__dirname, '..', 'index.html')).href;
    const next = () => page.locator('[data-tour="next"]').click();
    const screenshot = async name => {
      await page.waitForTimeout(420);
      await page.screenshot({ path: path.join(os.tmpdir(), 'vex-guide-' + name + '.png') });
      const bounds = await page.locator('.tour-card').boundingBox();
      const viewport = page.viewportSize();
      assert.ok(bounds.x >= 0 && bounds.y >= 0 && bounds.x + bounds.width <= viewport.width && bounds.y + bounds.height <= viewport.height, 'Guide card stays inside the viewport');
    };
    await page.goto(url + '?palette=aurora&theme=light');
    const original = await page.locator('#markdown-input').inputValue();
    await page.locator('#guide-launch').click();
    assert.equal(await page.locator('#vex-guide').isVisible(), true);
    assert.match(await page.locator('#document-name').textContent(), /演练/);
    await screenshot('01-perspectives');
    await page.locator('#view-read').click();
    assert.equal(await page.locator('#reading-pane').isVisible(), true, 'The spotlight lets users operate real controls');
    await page.waitForFunction(() => document.querySelector('.tour-feedback').classList.contains('done'));
    await next();
    await page.locator('#instant-content > .instant-block:nth-child(2)').click();
    await page.locator('.instant-input').fill('在演练文档里输入 **一个新想法**。');
    assert.equal(await page.locator('.instant-live-preview').count(), 0);
    assert.match(await page.locator('#markdown-input').inputValue(), /一个新想法/);
    await screenshot('02-writing');
    await next();
    await page.locator('[data-typography-key="OrangeHeart"]').click();
    assert.equal(await page.locator('html').getAttribute('data-typography'), 'OrangeHeart');
    await screenshot('03-typography');
    await next();
    await page.locator('#summary-length').fill('12');
    assert.equal(await page.locator('#summary-label').textContent(), '摘要 12 字');
    await screenshot('04-summary');
    await page.locator('.map-node[data-kind="paragraph"] .map-node-button').first().click();
    assert.match(await page.locator('#inspector-content').textContent(), /一个新想法/);
    await page.locator('[data-tour="summary"]').click();
    await page.locator('[data-tour="fulltext"]').click();
    await screenshot('04-fulltext');
    await page.locator('#node-inspector [data-action="locate-source"]').click();
    assert.equal(await page.locator('#source-pane').isVisible(), true);
    assert.match(await page.locator('#markdown-input').evaluate(input => input.value.slice(input.selectionStart, input.selectionEnd)), /一个新想法/);
    await next();
    assert.equal(await page.locator('.menu-flyout').isVisible(), true);
    await screenshot('05-save');
    assert.deepEqual(downloads, [], 'Opening and advancing the guide never downloads a file');
    const [download] = await Promise.all([
      page.waitForEvent('download'),
      page.locator('.menu-item[data-menu-label="保存"]').click()
    ]);
    assert.match(download.suggestedFilename(), /演练/);
    await page.locator('[data-tour="exports"]').click();
    const [htmlExport] = await Promise.all([
      page.waitForEvent('download'),
      page.locator('.menu-item[data-menu-action="export-html"]').click()
    ]);
    assert.match(htmlExport.suggestedFilename(), /演练\.html$/);
    await next();
    assert.equal(await page.locator('#markdown-input').inputValue(), original, 'Completing restores the original content');
    assert.equal(await page.locator('html').getAttribute('data-typography'), 'Simple');
    assert.equal(await page.locator('#summary-label').textContent(), '摘要 30 字');
    assert.equal(await page.locator('.file-row').filter({ hasText: '演练' }).count(), 0);
    await screenshot('06-ready');
    await page.locator('.tour-next').click();
    assert.equal(await page.locator('#vex-guide').isVisible(), false);
    assert.equal(await page.locator('#guide-launch').isVisible(), false);
    await page.reload();
    assert.equal(await page.locator('#guide-launch').isVisible(), false, 'The first-use entry stays dismissed after reload');
    await page.locator('.tb-menuitem[data-menu="帮助(H)"]').click();
    await page.locator('.menu-item[data-menu-action="guide"]').click();
    assert.equal(await page.locator('#vex-guide').getAttribute('data-step'), '1', 'Help can always restart the guide');
    await page.keyboard.press('Escape');

    // 中途退出必须保留未保存的正文、原来视图与显示偏好。
    await page.locator('#edit-source').click();
    const unsaved = '# 不能丢失的文档\n\n这是进入引导之前尚未保存的文字。';
    await page.locator('#markdown-input').fill(unsaved);
    await page.locator('#view-mindmap').click();
    await page.locator('#map-settings-button').click();
    await page.locator('#summary-length').fill('17');
    await page.locator('[data-action="close-settings"]').click();
    await page.locator('.side-tab[data-pane="outline"]').click();
    await page.locator('#sidebar-toggle').click();
    await page.keyboard.press('F8');
    await page.waitForTimeout(350);
    await page.locator('[data-action="zoom-in"]').click();
    const previousZoom = await page.locator('#zoom-value').textContent();
    await page.evaluate(() => VexTypography.choose('InkBlack'));
    await page.locator('.tb-menuitem[data-menu="帮助(H)"]').click();
    await page.locator('.menu-item[data-menu-action="guide"]').click();
    await page.keyboard.press('ArrowRight');
    await page.locator('#instant-content > .instant-block:nth-child(2)').click();
    await page.locator('.instant-input').fill('演练中的临时编辑');
    await page.keyboard.press('Escape');
    await page.waitForTimeout(350);
    assert.equal(await page.locator('#markdown-input').inputValue(), unsaved);
    assert.equal(await page.locator('#dirty-dot').isVisible(), true);
    assert.equal(await page.locator('#view-mindmap').getAttribute('aria-selected'), 'true');
    assert.equal(await page.locator('#summary-label').textContent(), '摘要 17 字');
    assert.equal(await page.locator('html').getAttribute('data-typography'), 'InkBlack');
    assert.equal(await page.locator('.app-window').evaluate(element => element.classList.contains('focus-mode')), true);
    assert.equal(await page.locator('.side-tab[data-pane="outline"]').evaluate(element => element.classList.contains('on')), true);
    assert.equal(await page.locator('#sidebar-toggle').getAttribute('aria-pressed'), 'false');
    assert.equal(await page.locator('#zoom-value').textContent(), previousZoom);
    await page.keyboard.press('F8');
    await page.locator('#view-edit').click();
    assert.equal(await page.locator('#edit-source').getAttribute('aria-pressed'), 'true');
    assert.equal(await page.locator('#reading-pane').isVisible(), false);

    // 窄窗口 / 深色下全部步骤可达；Tab 限于引导与可操作的聚光控件。
    await page.setViewportSize({ width: 980, height: 640 });
    await page.evaluate(() => VexTheme.setMode('dark'));
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.locator('.tb-menuitem[data-menu="帮助(H)"]').click();
    await page.locator('.menu-item[data-menu-action="guide"]').click();
    for (let index = 0; index < 5; index++) {
      await screenshot('narrow-dark-' + (index + 1));
      const ringRects = await page.locator('.tour-focus').evaluateAll(items => items.map(item => {
        const rect = item.getBoundingClientRect(); return { x: rect.left, y: rect.top, width: rect.width, height: rect.height };
      }));
      ringRects.forEach(rect => assert.ok(rect.x >= 0 && rect.y >= 0 && rect.x + rect.width <= 980 && rect.y + rect.height <= 640));
      await page.keyboard.press('Tab');
      assert.equal(await page.evaluate(() => document.activeElement !== document.body), true);
      if (index < 4) await next();
    }
    assert.equal(await page.locator('.tour-focus').first().evaluate(element => getComputedStyle(element, '::after').animationName), 'none');
    await page.keyboard.press('Escape');
    assert.equal(await page.locator('#markdown-input').inputValue(), unsaved);
    assert.deepEqual(errors, []);
    console.log('Passed: five interactive guide steps, real editing/themes/summary, opt-in save, restore on finish/skip, help restart, preference persistence, keyboard and narrow dark layout.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
