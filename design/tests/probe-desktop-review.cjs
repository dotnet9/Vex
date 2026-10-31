const assert = require('node:assert/strict');
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  async function open(query = '') {
    await page.goto('http://127.0.0.1:4317/design/Vex.html?preview=1&device=desktop' + query);
    await page.waitForFunction(() => window.VexWorkspace && window.VexChapters);
    await page.waitForTimeout(350);
  }

  await open('&scenario=empty');
  assert.equal(await page.locator('#sb-wordcount').innerText(), '字数 0');
  assert.equal(await page.evaluate(() => VexWriting.metrics('').count), 0);
  assert.equal(await page.evaluate(() => getComputedStyle(document.querySelector('.workspace'), '::before').display), 'block');

  await open('&scenario=long-document&layout=three');
  await page.waitForTimeout(500);
  assert.ok(await page.evaluate(() => VexWorkbench.state.scale >= .75), 'long documents open at a readable scale');
  assert.ok(await page.locator('.map-node[data-kind="heading"]').count() > 1);
  await page.locator('#map-viewport').focus();
  const beforePan = await page.evaluate(() => VexWorkbench.state.x);
  await page.keyboard.press('ArrowRight');
  assert.ok(await page.evaluate(() => VexWorkbench.state.x) < beforePan, 'arrow keys pan the canvas');
  const visibleAfterFocus = await page.evaluate(() => {
    const canvas = document.querySelector('#map-viewport');
    const button = Array.from(canvas.querySelectorAll('.map-node-button')).at(-1);
    button.focus({ preventScroll: true });
    const a = canvas.getBoundingClientRect(), b = button.getBoundingClientRect();
    return b.left >= a.left && b.right <= a.right && b.top >= a.top && b.bottom <= a.bottom;
  });
  assert.equal(visibleAfterFocus, true, 'keyboard focus brings an offscreen node into view');

  await open('&layout=map');
  const originalText = await page.evaluate(() => VexWorkbench.text);
  const collapse = page.locator('.map-collapse').first();
  await collapse.focus();
  await page.keyboard.press('Enter');
  assert.equal(await page.evaluate(() => VexWorkbench.text), originalText, 'folding does not edit text');
  assert.equal(await page.locator('.map-collapse').first().getAttribute('aria-expanded'), 'false');
  assert.equal(await page.evaluate(() => document.activeElement.classList.contains('map-collapse')), true, 'folding retains focus');
  await page.keyboard.press('Tab');
  assert.equal(await page.evaluate(() => VexWorkbench.text), originalText, 'Tab navigates rather than creates a chapter');
  await page.locator('.map-appearance-trigger').click();
  await page.locator('[data-map-style-choice="outline"]').focus();
  await page.keyboard.press('Enter');
  assert.equal(await page.evaluate(() => document.activeElement.dataset.mapStyleChoice), 'outline');
  assert.equal(await page.locator('#map-appearance-panel').isVisible(), true);
  const colors = await page.locator('.map-node.root').evaluate(node => ({ bg: getComputedStyle(node).backgroundColor, fg: getComputedStyle(node.querySelector('.map-node-text')).color }));
  assert.notEqual(colors.bg, colors.fg);
  await page.keyboard.press('Escape');
  assert.equal(await page.locator('.map-appearance-trigger').evaluate(node => node === document.activeElement), true);

  await page.setViewportSize({ width: 740, height: 700 });
  await open();
  assert.equal(await page.evaluate(() => VexWorkbench.state.sidebar), true);
  assert.equal(await page.locator('.nav-rail').evaluate(node => getComputedStyle(node).flexDirection), 'column');
  await page.locator('.format-more').click();
  assert.equal(await page.locator('#format-overflow [data-action="insert-table"]').isVisible(), true);
  await page.keyboard.press('Escape');
  assert.equal(await page.locator('.format-more').evaluate(node => node === document.activeElement), true);

  await page.setViewportSize({ width: 1440, height: 900 });
  await page.locator('#vex-appearance-menu > summary').click();
  assert.equal(await page.locator('#vex-appearance-menu [data-palette="teal"] .palette-preview').count(), 1);
  assert.deepEqual(errors, []);
  await browser.close();
  console.log('PASS: desktop review interactions and states');
})().catch(error => { console.error(error); process.exitCode = 1; });
