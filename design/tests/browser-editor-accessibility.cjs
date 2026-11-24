const assert = require('node:assert/strict');
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto((process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/') + 'Vex.html?preview=1&layout=instant');
    await page.waitForFunction(() => window.VexWorkbench && window.VexWorkspace && window.VexTheme);
    await page.addStyleTag({ content: '*,*::before,*::after{transition:none!important;animation:none!important}' });

    const hint = await page.locator('#instant-hint').evaluate(element => ({
      display: getComputedStyle(element).display,
      text: element.textContent,
      describedBy: document.querySelector('#instant-pane').getAttribute('aria-describedby')
    }));
    assert.notEqual(hint.display, 'none');
    assert.match(hint.text, /Ctrl\+Enter/);
    assert.match(hint.text, /Esc/);
    assert.match(hint.text, /Shift\+Enter/);
    assert.equal(hint.describedBy, 'instant-hint');
    await page.locator('#instant-content').click();
    await page.keyboard.type(' hint check');
    assert.equal(await page.locator('#instant-hint').isVisible(), true);

    await page.locator('#format-toolbar [data-action="undo"]').focus();
    await page.keyboard.press('ArrowRight');
    assert.equal(await page.evaluate(() => document.activeElement.dataset.action), 'redo');
    assert.equal(await page.evaluate(() => document.activeElement.matches(':focus-visible')), true);
    assert.equal(await page.evaluate(() => getComputedStyle(document.activeElement).outlineWidth), '2px');

    await page.keyboard.press('Control+f');
    await page.locator('#find-input').fill('正文');
    assert.equal(await page.locator('#find-count').getAttribute('role'), 'status');
    assert.equal(await page.locator('#find-count').getAttribute('aria-live'), 'polite');
    for (const [option, label] of [['regex', '正则匹配'], ['case', '区分大小写'], ['word', '整词匹配']]) {
      assert.equal(await page.locator(`[data-find-option="${option}"]`).getAttribute('aria-label'), label);
    }
    await page.locator('[data-find-option="regex"]').click();
    await page.locator('#find-input').fill('[');
    assert.equal(await page.locator('#find-input').getAttribute('aria-invalid'), 'true');
    assert.equal(await page.locator('#find-error').isVisible(), true);
    assert.match(await page.locator('#find-error').textContent(), /正则表达式无效/);
    assert.equal(await page.locator('#find-count').isVisible(), false);
    await page.locator('[data-find-option="regex"]').click();
    assert.equal(await page.locator('#find-input').getAttribute('aria-invalid'), 'false');
    assert.equal(await page.locator('#find-error').isVisible(), false);
    await page.keyboard.press('Escape');

    const swatchColors = await page.evaluate(() => {
      VexTheme.setMode('light');
      const light = getComputedStyle(document.querySelector('.span-glyph.bg')).backgroundColor;
      VexTheme.setMode('dark');
      const dark = getComputedStyle(document.querySelector('.span-glyph.bg')).backgroundColor;
      VexTheme.setMode('light');
      return { light, dark };
    });
    assert.notEqual(swatchColors.light, swatchColors.dark);
    assert.notEqual(swatchColors.light, 'rgb(255, 243, 191)');

    const mapCenter = await page.evaluate(() => {
      VexWorkbench.applyText('# Root\n\n## Alpha\n\nParagraph\n\n## Beta\n\nParagraph');
      VexWorkbench.setView('mindmap');
      VexWorkbench.renderMap();
      VexWorkbench.fit(true);
      const canvas = document.querySelector('#map-viewport');
      return {
        canvasWidth: canvas.clientWidth,
        contentCenter: VexWorkbench.state.x + VexWorkbench.state.bounds.width * VexWorkbench.state.scale / 2
      };
    });
    assert.ok(Math.abs(mapCenter.contentCenter - mapCenter.canvasWidth / 2) < 1);

    const density = await page.evaluate(() => {
      const probe = document.createElement('div');
      probe.style.height = 'var(--control-h-md)';
      document.body.appendChild(probe);
      VexTheme.setDensity('compact');
      const compact = probe.getBoundingClientRect().height;
      VexTheme.setDensity('normal');
      const normal = probe.getBoundingClientRect().height;
      probe.remove();
      return { compact, normal };
    });
    assert.ok(density.compact < density.normal);
    assert.deepEqual(errors, []);
    console.log('PASS persistent editor hints, toolbar keyboard focus, find status/errors, theme tokens, centered map and density tiers');
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
