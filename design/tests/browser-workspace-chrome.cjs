/* Optional headless browser checks; requires Playwright and a running preview server. */
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const {chromium} = require('playwright');
const base = process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/';
const output = path.resolve(process.env.VEX_CAPTURE_DIR || path.join(__dirname, '../../artifacts/prototype-workspace-chrome'));
fs.mkdirSync(output, {recursive:true});

(async () => {
  const browser = await chromium.launch({headless:true, ...(process.env.VEX_BROWSER_CHANNEL ? {channel:process.env.VEX_BROWSER_CHANNEL} : {})});
  const page = await browser.newPage({viewport:{width:1440,height:900}}), errors = [];
  page.on('pageerror', error => errors.push(error.message));
  async function open(query = '') {
    await page.goto(base + 'Vex.html?preview=1&device=desktop&' + query);
    await page.waitForFunction(() => window.VexAuxiliary && window.VexChapters);
    await page.waitForTimeout(250);
  }
  async function shot(name) { await page.waitForTimeout(300); await page.screenshot({path:path.join(output, name + '.png')}); }
  async function shown(selector) { await page.locator(selector).waitFor({state:'visible'}); return page.locator(selector).isVisible(); }
  async function closed() { return page.locator('#structure-sidebar').evaluate(el => el.classList.contains('collapsed')); }
  try {
    await open();
    await page.locator('#vex-appearance-menu > summary').click();
    assert.equal(await page.locator('#vex-appearance-menu [data-mode]').count(), 3);
    await page.locator('#vex-appearance-menu [data-mode="light"]').click();
    assert.equal(await page.locator('html').getAttribute('data-theme-mode'), 'light');
    assert.equal(await page.locator('html').getAttribute('data-theme'), 'light');
    assert.equal(await page.locator('#vex-appearance-menu').getAttribute('open'), '', 'Choosing a mode keeps the menu open');
    await shot('appearance-mode-light');
    await page.locator('#vex-appearance-menu [data-mode="dark"]').click();
    assert.equal(await page.locator('html').getAttribute('data-theme-mode'), 'dark');
    assert.equal(await page.locator('html').getAttribute('data-theme'), 'dark');
    await shot('appearance-mode-dark');
    await page.emulateMedia({colorScheme:'light'});
    await page.locator('#vex-appearance-menu [data-mode="system"]').click();
    assert.equal(await page.locator('html').getAttribute('data-theme'), 'light');
    await page.emulateMedia({colorScheme:'dark'});
    await page.waitForFunction(() => document.documentElement.dataset.theme === 'dark');
    assert.equal(await page.locator('html').getAttribute('data-theme'), 'dark');
    assert.equal(await page.locator('#vex-appearance-menu [data-mode="system"]').getAttribute('aria-pressed'), 'true', 'Mode saves without an Apply button');
    assert.equal(await page.evaluate(() => JSON.parse(VexStorage.getItem('vex-design-appearance')).mode), 'system');
    await page.locator('#vex-appearance-menu [data-mode="light"]').click();
    await page.locator('#vex-appearance-menu [data-palette="indigo"]').click();
    assert.equal(await page.locator('html').getAttribute('data-theme-mode'), 'light');
    assert.equal(await page.locator('html').getAttribute('data-palette'), 'indigo');
    await shot('appearance-radio-light');
    await page.locator('#vex-appearance-menu [data-palette="teal"]').click();
    assert.equal(await page.locator('html').getAttribute('data-palette'), 'teal');
    await page.keyboard.press('Escape');
    assert.equal(await page.locator('.view-switch, #edit-source').count(), 0, 'The desktop has one view selector');
    assert.equal(await closed(), true, 'Auxiliary panel starts closed');
    assert.equal(await page.locator('#view-follow-controls').isVisible(), false);
    assert.equal(await page.locator('#edit-options [data-action=typography]').isVisible(), true, 'Single editor keeps formatting accessible');
    const original = await page.evaluate(() => VexWorkbench.text);
    const layouts = ['instant','source','read','map','source-render','source-map','instant-map','three'];
    for (const layout of layouts) {
      await page.locator('#workspace-layout').click();
      await page.locator('[data-layout=' + layout + ']').click();
      assert.equal(await page.evaluate(() => VexWorkspace.current), layout);
      assert.equal(await page.locator('#workspace-layout-menu').isVisible(), false);
      assert.equal(await page.locator('#workspace-layout').evaluate(el => el === document.activeElement), true, 'Choosing a view returns focus');
      assert.equal(await page.evaluate(() => VexWorkbench.text), original, 'View changes preserve the document');
      assert.equal(await page.locator('#view-surface > .view-panel.active-view, #view-surface .view-split .view-panel.active-view').count(), 1);
    }
    await page.locator('#workspace-layout').click();
    await page.keyboard.press('Home');
    assert.equal(await page.evaluate(() => document.activeElement.dataset.layout), 'instant');
    await page.keyboard.press('ArrowRight'); await page.keyboard.press('Enter');
    assert.equal(await page.evaluate(() => VexWorkspace.current), 'source');
    await page.locator('#workspace-layout').click(); await page.keyboard.press('Escape');
    assert.equal(await page.locator('#workspace-layout-menu').isVisible(), false);
    assert.equal(await page.locator('#workspace-layout').evaluate(el => el === document.activeElement), true);

    await page.evaluate(() => VexWorkspace.setLayout('read'));
    assert.equal(await page.locator('#edit-options').isVisible(), false);
    const width = await page.locator('#markdown-preview').evaluate(el => el.getBoundingClientRect().width);
    assert.ok(width <= 820, 'Reading has a comfortable maximum line width');
    await page.locator('#outline-toggle').click();
    assert.equal(await closed(), false);
    await page.locator('#auxiliary-outline').waitFor({state:'visible'});
    assert.equal(await page.locator('#auxiliary-outline').isVisible(), true);
    await page.locator('#outline-filter').fill('没有匹配的章节');
    assert.match(await page.locator('#outline-body').innerText(), /没有匹配的标题/);
    await page.locator('[data-action=clear-outline-search]').click();
    assert.ok(await page.locator('#outline-body .structure-item').count() > 0);
    await shot('reading-outline');
    await page.locator('#sb-wordcount').click();
    assert.equal(await shown('#auxiliary-info'), true);
    assert.match(await page.locator('#auxiliary-info').innerText(), /中文按字，英文按词/);
    assert.equal(await page.locator('#outline-toggle').getAttribute('aria-pressed'), 'false', 'Outline button represents the active panel');
    await page.keyboard.press('Escape');
    assert.equal(await closed(), true);
    assert.equal(await page.locator('#sb-wordcount').evaluate(el => el === document.activeElement), true);

    await page.locator('#sb-wordcount').click();
    await page.locator('#auxiliary-info').waitFor({state:'visible'});
    const count = await page.locator('#article-info-count').innerText();
    await page.evaluate(() => VexWorkbench.applyText(VexWorkbench.text + '\n\n新增的字数。'));
    assert.notEqual(await page.locator('#article-info-count').innerText(), count, 'Article information updates during editing');
    assert.equal(await page.locator('.titlebar-document .doc-dot').isVisible(), true, 'Titlebar shows the modified marker');
    await shot('article-info');
    // 辅助面板已按产品要求去掉标题栏与关闭按钮；关闭统一走 Esc（焦点回到触发按钮）。
    await page.keyboard.press('Escape'); assert.equal(await closed(), true);
    assert.equal(await page.locator('#sb-wordcount').evaluate(el => el === document.activeElement), true);
    await page.locator('#sb-wordcount').click();
    assert.equal(await shown('#auxiliary-info'), true);
    await page.locator('#outline-toggle').click();
    assert.equal(await page.locator('#auxiliary-outline').isVisible(), true, 'Outline shortcut switches from information to outline');
    await page.locator('#structure-splitter').focus();
    const beforeResize = Number(await page.locator('#structure-splitter').getAttribute('aria-valuenow'));
    await page.keyboard.press('ArrowLeft');
    assert.ok(Number(await page.locator('#structure-splitter').getAttribute('aria-valuenow')) > beforeResize);
    await page.keyboard.press('Escape'); assert.equal(await closed(), true);

    await open('layout=map');
    const sample = '# 文章\n\n## 甲章\n\n甲的正文。\n\n### 子节\n\n子节保留。\n\n## 乙章\n\n乙的正文。\n';
    await page.evaluate(value => {
      VexWorkbench.applyText(value); VexWorkbench.fit();
      VexWorkbench.selectNode(VexWorkbench.state.nodes.find(node => node.text === '甲章'));
    }, sample);
    assert.equal(await shown('#node-inspector'), true, 'Selecting a chapter opens the map inspector');
    await page.locator('#chapter-title-input').fill('修改后的甲章');
    await page.locator('#chapter-title-input').press('Tab');
    assert.match(await page.evaluate(() => VexWorkbench.text), /## 修改后的甲章/);
    await page.locator('#chapter-body-input').fill('在统一面板中修改正文。');
    await page.locator('#chapter-body-input').press('Tab');
    assert.match(await page.evaluate(() => VexWorkbench.text), /在统一面板中修改正文。[\s\S]*### 子节\n\n子节保留。/);
    await page.waitForFunction(() => { const f = document.querySelector('.chapter-feedback'); return f && !f.hidden; }, null, {timeout: 5000});
    await page.evaluate(() => document.querySelector('.chapter-feedback button').click());
    assert.match(await page.evaluate(() => VexWorkbench.text), /甲的正文。/);
    await page.evaluate(() => VexWorkbench.selectNode(VexWorkbench.state.nodes.find(node => node.text === '修改后的甲章')));
    await shot('chapter-details');
    await page.locator('#outline-toggle').click();
    await page.keyboard.press('Escape');
    assert.equal(await closed(), true);
    await page.locator('#outline-toggle').click();
    await page.locator('#sb-wordcount').click();
    assert.equal(await page.locator('#article-info-count').isVisible(), true, 'Closing and reopening preserves the auxiliary panel');
    await page.evaluate(() => VexWorkbench.loadDocument(1));
    await page.locator('#layer-unsaved [data-intent=cancel]').click();
    assert.match(await page.evaluate(() => VexWorkbench.text), /修改后的甲章/, 'Cancelling a document switch preserves changes');

    await open('layout=read');
    await page.locator('#sb-wordcount').click();
    await page.locator('#auxiliary-info').waitFor({state:'visible'});
    const guideOriginal = await page.evaluate(() => VexWorkbench.text);
    await page.evaluate(() => VexGuide.start());
    await page.locator('[data-tour=start]').click();
    await page.locator('[data-tour=skip]').click();
    await page.waitForFunction(() => document.querySelector('.app-window').dataset.guideStep === '2');
    await page.waitForFunction(() => document.querySelector('.tour-feedback')?.textContent.includes('查看文档大纲'));
    await page.locator('[data-tour=next]:not([disabled])').click();
    await page.waitForFunction(() => VexAuxiliary.active === 'outline');
    await page.waitForFunction(() => document.querySelector('.tour-feedback')?.textContent.includes('大纲会列出所有标题'));
    await page.locator('[data-tour=skip]').click();
    await page.waitForFunction(() => document.querySelector('.app-window').dataset.guideStep === '3');
    await page.keyboard.press('Escape');
    assert.equal(await page.evaluate(() => VexGuide.active), false);
    assert.equal(await page.evaluate(() => VexWorkbench.text), guideOriginal, 'Guide restores the document');
    assert.equal(await page.evaluate(() => VexWorkspace.current), 'read', 'Guide restores the layout');
    assert.equal(await page.evaluate(() => VexAuxiliary.active), 'info', 'Guide restores the auxiliary panel');

    for (const viewport of [{width:1440,height:900},{width:1024,height:768},{width:800,height:480}]) {
      await page.setViewportSize(viewport);
      for (const layout of ['instant','read','three']) {
        await open('layout=' + layout + '&scenario=long-files');
        const before = await page.locator('#view-surface').boundingBox();
        await page.locator('#sb-wordcount').click();
        await page.waitForTimeout(300);
        const result = await page.evaluate(() => {
          const r = selector => document.querySelector(selector).getBoundingClientRect().toJSON();
          return {sidebar:r('#structure-sidebar'), surface:r('#view-surface'), bar:r('.document-statusbar'),
            title:r('.titlebar-document'), selector:r('#workspace-layout'), find:r('.find-launch'),
            appearanceLeft:document.querySelector('#vex-appearance-menu').getBoundingClientRect().left,
            overflow:document.querySelector('.document-statusbar').scrollWidth-document.querySelector('.document-statusbar').clientWidth,
            overlay:document.querySelector('.workspace').classList.contains('auxiliary-overlay')};
        });
        assert.ok(result.sidebar.right <= viewport.width && result.sidebar.bottom <= viewport.height, 'Auxiliary panel stays inside the window ' + JSON.stringify({vp: viewport, layout: layout, sidebar: result.sidebar}));
        assert.ok(result.find.right <= result.appearanceLeft + 1, 'Find button sits left of the appearance entry');
        assert.ok(result.surface.bottom <= result.bar.top + 1 && result.overflow <= 1, 'Status bar does not cover or overflow the document');
        if (result.overlay) assert.ok(Math.abs(result.surface.width - before.width) < 1, 'Overlay panel preserves document width');
        await shot(layout + '-info-' + viewport.width);
        await page.keyboard.press('Escape');
        await shot(layout + '-' + viewport.width);
      }
    }
    await page.setViewportSize({width:1440,height:900});
    await open('theme=dark&layout=three'); await shot('three-dark');
    await page.locator('#sb-wordcount').click(); await shot('three-info-dark');
    await open('theme=dark&layout=map');
    await page.evaluate(() => VexWorkbench.selectNode(VexWorkbench.state.nodes.find(node => node.kind === 'heading' && !node.isRoot)));
    await shot('chapter-dark');
    assert.deepEqual(errors, [], 'Browser runtime errors');
    console.log('PASS: eight view combinations, keyboard/focus, reading width, unified panels, live information, chapter editing/undo, file menu, long names and 800–1440px light/dark layouts.');
    console.log('Screenshots: ' + output);
  } catch (error) { await shot('failure'); throw error; }
  finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
