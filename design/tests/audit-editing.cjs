const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    const url = pathToFileURL(path.join(__dirname, '..', 'index.html')).href;
    const mainMenu = label => page.locator('.tb-menuitem').filter({ hasText: label });
    const menuItem = label => page.locator('.menu-item:visible').filter({
      has: page.locator(':scope > .menu-label').filter({ hasText: new RegExp('^' + label + '$') })
    });
    const openSub = async label => {
      await mainMenu('视图(V)').click();
      await menuItem(label).hover();
    };
    const editCommand = async label => {
      await mainMenu('编辑(E)').click();
      await menuItem(label).click();
    };
    await page.goto(url + '?palette=aurora&theme=light');
    assert.equal(await page.locator('#edit-instant').getAttribute('aria-pressed'), 'true');
    assert.equal(await page.locator('#instant-pane').isVisible(), true);
    assert.equal(await page.locator('#source-pane').isVisible(), false);
    const formatButtons = page.locator('#format-toolbar .format-button:not(.format-more)');
    assert.equal(await formatButtons.count(), 12, '常用格式按钮数量保持稳定');
    assert.equal(await formatButtons.locator('svg').count(), 12, '格式快捷栏统一使用图标');
    assert.equal(await page.locator('#format-toolbar .format-button').filter({ hasText: /加粗|斜体|标题|引用|列表|链接|图片|代码块|表格/ }).count(), 0, '格式快捷栏不直接铺开文字');
    assert.ok(Number.parseFloat(await page.locator('.statusbar').evaluate(element => getComputedStyle(element).fontSize)) >= 11.5, '状态栏字号可读');
    const original = await page.locator('#markdown-input').inputValue();
    const output = path.join(process.env.CODEWF_TEMP_ROOT || 'E:/github/temp', 'vex-prototype-editing');
    fs.mkdirSync(output, { recursive: true });
    // 复制操作不得触发活动块编辑，使用浏览器隔离剪贴板桩验证内容。
    await page.evaluate(() => Object.defineProperty(navigator, 'clipboard', { configurable: true, value: {
      writeText: text => { window.copiedCode = text; return Promise.resolve(); }
    } }));
    const code = page.locator('#instant-content .md-code-block').first();
    assert.equal(await code.locator('.md-code-header span').textContent(), 'markdown');
    assert.equal(await code.locator('.md-code-lines').textContent(), '1');
    const copy = code.locator('.md-code-copy');
    assert.ok((await copy.boundingBox()).height <= 24);
    await copy.click();
    assert.equal(await page.evaluate(() => window.copiedCode), await code.locator('code').textContent());
    assert.equal(await page.locator('.instant-input').count(), 0, 'Copy keeps the code block rendered');

    // 真正输入而非直接调用内部方法：编辑块保持光标，排版、源码、导图共享内容。
    await page.locator('.instant-block').filter({ hasText: /^把注意力/ }).click();
    const draft = '新的文字 **加粗内容**，即时确认效果。';
    await page.locator('.instant-input').fill(draft);
    assert.equal(await page.locator('.instant-live-preview').count(), 0, 'Active block shows only Markdown source');
    assert.ok(await page.locator('.instant-block:not(.is-editing) h2').count() > 0, 'Other blocks keep rendered typography');
    assert.equal(await page.locator('.instant-input').inputValue(), draft, 'Live rendering preserves the editing surface');
    assert.match(await page.locator('#markdown-input').inputValue(), /新的文字 \*\*加粗内容\*\*/);
    await page.screenshot({ path: path.join(output, 'vex-instant-active-light.png') });
    await page.evaluate(() => VexTheme.setMode('dark'));
    await page.waitForTimeout(350);
    await page.screenshot({ path: path.join(output, 'vex-instant-active-dark.png') });
    await page.locator('.instant-input').press('Control+Enter');
    assert.equal(await page.locator('.instant-block strong').filter({ hasText: '加粗内容' }).count(), 1);
    const edited = await page.locator('#markdown-input').inputValue();
    await page.locator('#view-read').click();
    assert.equal(await page.locator('textarea:visible').count(), 0, 'Reading has no editable surface');
    assert.match(await page.locator('#markdown-preview').textContent(), /新的文字 加粗内容/);
    await page.locator('#view-mindmap').click();
    assert.equal(await page.locator('.map-node[data-kind="paragraph"]').filter({ hasText: '新的文字 加粗内容' }).count(), 1);
    await page.locator('#view-edit').click();
    await page.locator('.instant-block').filter({ hasText: /^新的文字/ }).click();
    await page.locator('.instant-input').fill('不应保留的修改');
    await page.locator('.instant-input').press('Escape');
    assert.equal(await page.locator('#markdown-input').inputValue(), edited, 'Escape cancels only the current block');
    await editCommand('撤销');
    assert.equal(await page.locator('#markdown-input').inputValue(), original);
    await editCommand('重做');
    assert.equal(await page.locator('#markdown-input').inputValue(), edited);

    // 一块新增多个段落之后，点击下一块仍准确定位，不覆盖相邻正文。
    await page.locator('.instant-block').filter({ hasText: /^新的文字/ }).click();
    await page.locator('.instant-input').fill(draft + '\n\n补充段落。\n\n#### 新增标题');
    await page.locator('.instant-block').filter({ hasText: /^降低视觉噪音$/ }).click();
    assert.equal(await page.locator('.instant-input').inputValue(), '### 降低视觉噪音');
    await page.locator('.instant-input').press('Control+Enter');
    assert.equal(await page.locator('.instant-block h4').filter({ hasText: '新增标题' }).count(), 1);
    assert.match(await page.locator('#markdown-input').inputValue(), /补充段落。\n\n#### 新增标题\n\n### 降低视觉噪音/);

    // 与原生菜单对照的主题名全部可点击；设置同步到菜单、正文与刷新后的页面。
    await page.locator('#edit-options [data-action="typography"]').click();
    const keys = await page.locator('#typography-panel [data-typography-key]').evaluateAll(items => items.map(item => item.dataset.typographyKey));
    assert.equal(keys.length, 18);
    for (const key of keys) {
      await page.locator(`#typography-panel [data-typography-key="${key}"]`).click();
      assert.equal(await page.locator('html').getAttribute('data-typography'), key);
      assert.equal(await page.locator(`#typography-panel [data-typography-key="${key}"]`).getAttribute('aria-pressed'), 'true');
    }
    await page.locator('#typography-compact').check();
    await page.evaluate(() => VexTheme.setMode('light'));
    await page.waitForTimeout(350);
    await page.screenshot({ path: path.join(output, 'vex-typography-gallery.png') });
    await page.reload();
    assert.equal(await page.locator('html').getAttribute('data-typography'), 'WeChatFormat');
    assert.equal(await page.locator('html').getAttribute('data-compact'), 'true');
    await page.locator('#edit-options [data-action=typography]').click();
    assert.equal(await page.locator('[data-typography-key=WeChatFormat]').getAttribute('aria-pressed'), 'true');
    await page.locator('[data-typography-key=TechnologyBlue]').click();
    await page.locator('#edit-options [data-action=typography]').click();
    assert.equal(await page.locator('#edit-options [data-typography-label]').textContent(), '科技蓝');
    const headingStyle = await page.locator('.instant-block h2').first().evaluate(element => {
      const css = getComputedStyle(element); return { color: css.color, borderWidth: css.borderLeftWidth };
    });
    assert.equal(headingStyle.borderWidth, '3px');

    // 快捷键采用无背景的文本列，各行保持右对齐；菜单统一无装饰 SVG。
    await mainMenu('文件(F)').click();
    assert.equal(await page.locator('.menu-item:visible svg, .menu-item:visible .mi-ico').count(), 0);
    const shortcuts = await page.locator('.menu-item:visible .mi-gesture').evaluateAll(items => items.map(item => ({
      background: getComputedStyle(item).backgroundColor,
      border: getComputedStyle(item).borderTopWidth,
      right: item.getBoundingClientRect().right
    })));
    assert.ok(shortcuts.length > 4);
    shortcuts.forEach(item => { assert.equal(item.background, 'rgba(0, 0, 0, 0)'); assert.equal(item.border, '0px'); });
    assert.ok(Math.max(...shortcuts.map(item => item.right)) - Math.min(...shortcuts.map(item => item.right)) < 1);

    // 独立 HTML 保留选中的主题和紧凑设置，并能再次在浏览器中渲染。
    await menuItem('导出').hover();
    const pendingDownload = page.waitForEvent('download');
    await menuItem('HTML').click();
    const downloaded = await pendingDownload;
    const html = fs.readFileSync(await downloaded.path(), 'utf8');
    const exported = await browser.newPage();
    await exported.setContent(html);
    const exportHeading = await exported.locator('h2').first().evaluate(element => {
      const css = getComputedStyle(element); return { color: css.color, borderWidth: css.borderLeftWidth };
    });
    assert.deepEqual(exportHeading, headingStyle);
    assert.equal(await exported.locator('body').evaluate(element => getComputedStyle(element).fontSize), '14px');
    await exported.close();

    await page.keyboard.press('Control+p');
    assert.equal(await page.locator('#file-filter').evaluate(element => element === document.activeElement), true);
    await page.keyboard.press('Control+Shift+p');
    assert.match(await page.locator('.proto-toast').last().textContent(), /打印预览/);
    await page.locator('#edit-source').click();
    assert.equal(await page.locator('#source-pane').isVisible(), true);
    assert.equal(await page.locator('#reading-pane').isVisible(), false);
    assert.equal(await page.locator('#source-preview-toggle').count(), 0);
    await page.locator('#edit-instant').click();
    await page.keyboard.press('F8');
    assert.equal(await page.locator('.statusbar').isVisible(), false);
    await page.keyboard.press('F8');
    assert.equal(await page.locator('.statusbar').isVisible(), true);
    await openSub('写作辅助');
    await menuItem('界面动效').click();
    assert.equal(await page.locator('.view-switch').evaluate(element => getComputedStyle(element, '::before').transitionDuration), '0s');

    // 菜单键盘导航进入 / 返回子菜单，长主题菜单在窄窗口可滚动且不越界。
    await mainMenu('视图(V)').press('ArrowDown');
    await menuItem('编辑方式').focus();
    await page.keyboard.press('ArrowRight');
    assert.equal(await menuItem('即时渲染').evaluate(element => element === document.activeElement), true);
    await page.keyboard.press('ArrowDown');
    assert.equal(await menuItem('源码').evaluate(element => element === document.activeElement), true);
    await page.keyboard.press('ArrowLeft');
    assert.equal(await menuItem('编辑方式').evaluate(element => element === document.activeElement), true);
    await page.keyboard.press('Escape');
    await page.setViewportSize({ width: 980, height: 640 });
    await mainMenu('插入(I)').click();
    assert.equal(await page.locator('.menu-flyout > .flyout > .menu-item > .submenu').count(), 0, 'Insert commands are fully flat');
    await menuItem('清除格式').scrollIntoViewIfNeeded();
    const subBounds = await page.locator('.menu-flyout > .flyout').evaluate(element => {
      const rect = element.getBoundingClientRect();
      return { left: rect.left, right: rect.right, top: rect.top, bottom: rect.bottom, height: innerHeight, width: innerWidth };
    });
    assert.ok(subBounds.left >= 0 && subBounds.right <= subBounds.width && subBounds.top >= 0 && subBounds.bottom <= subBounds.height);
    await page.screenshot({ path: path.join(output, 'vex-typography-menu-narrow.png') });
    await page.keyboard.press('Escape');
    await page.locator('#edit-options [data-action=typography]').click();
    await page.locator('[data-typography-key=WeChatFormat]').click();
    await page.locator('#edit-options [data-action=typography]').click();
    assert.equal(await page.locator('html').getAttribute('data-typography'), 'WeChatFormat');
    // 真实指针拖拽、窄宽窗口、收起恢复均使用同一列宽。
    for (const width of [1280, 980, 1920]) {
      await page.setViewportSize({width,height:width===980?640:1032});
      await page.locator('#view-edit').click(); await page.locator('#edit-instant').click();
      for (const delta of [90,-40,220,-400,70]) {
        const handle=await page.locator('#sidebar-splitter').boundingBox(),before=await page.locator('.sidebar').boundingBox();
        await page.mouse.move(handle.x+handle.width/2,handle.y+handle.height/2);await page.mouse.down();
        await page.mouse.move(handle.x+handle.width/2+delta,handle.y+handle.height/2,{steps:8});await page.mouse.up();
        const side=await page.locator('.sidebar').boundingBox(),main=await page.locator('.workspace-main').boundingBox();
        assert.ok(side.width>=200&&side.width<=560);assert.ok(Math.abs(main.x-side.x-side.width-5)<1);
        assert.ok(main.width>=360);assert.ok(Math.abs(side.width-before.width)>1||side.width===200||side.width===560);
      }
      const dragged=(await page.locator('.sidebar').boundingBox()).width;
      await page.locator('#sidebar-toggle').click();await page.locator('#sidebar-toggle').click();
      assert.equal((await page.locator('.sidebar').boundingBox()).width,dragged);
      await page.locator('.side-tab[data-pane=outline]').click();await page.locator('.side-tab[data-pane=files]').click();
      assert.equal((await page.locator('.sidebar').boundingBox()).width,dragged);
      if(width===1920){
        await page.locator('#sidebar-splitter').focus();for(let i=0;i<35;i++)await page.keyboard.press('ArrowLeft');
        for(const view of ['edit','read']){await page.locator('#view-'+view).click();const article=await page.locator(view==='edit'?'#instant-content':'#markdown-preview').boundingBox();assert.ok(article.width>=1100&&article.width<=1280);}
      }
    }
    assert.deepEqual(errors, []);
    console.log('Passed: instant rendering, source/map sync, cancel/undo, block boundaries, eighteen typography themes, export, plain menus, shortcuts and keyboard/narrow layout.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
