const assert = require('node:assert/strict');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: process.env.VEX_BROWSER_EXECUTABLE || undefined });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    const url = pathToFileURL(path.join(__dirname, '..', 'Vex.html')).href;
    await page.goto(url + '?view=mindmap&palette=aurora&theme=light');
    await page.locator('#view-mindmap').getAttribute('aria-selected').then(value => assert.equal(value, 'true'));
    assert.equal(await page.locator('#source-pane').isVisible(), false);
    assert.equal(await page.locator('.map-node[data-kind="heading"]').count(), 7, 'Code headings do not become branches');
    await page.locator('#map-settings-button').click();
    await page.locator('#summary-length').fill('12');
    const snippets = await page.locator('.map-node.content .map-node-text').allTextContents();
    assert.ok(snippets.length > 0);
    snippets.forEach(text => assert.ok(Array.from(text.replace(/…$/, '')).length <= 12));
    await page.locator('#summary-length').fill('0');
    assert.equal(await page.locator('#summary-label').textContent(), '摘要 12 字', 'Invalid limits preserve the last valid configuration');
    await page.locator('#summary-length').fill('30');
    await page.locator('#show-paragraphs').uncheck();
    await page.locator('#show-lists').uncheck();
    assert.equal(await page.locator('.map-node.content').count(), 0);
    assert.equal(await page.locator('#summary-label').textContent(), '仅标题');
    await page.reload();
    assert.equal(await page.locator('.map-node.content').count(), 0, 'Preferences survive reload');
    await page.locator('#map-settings-button').click();
    await page.locator('[data-action="reset-settings"]').click();
    await page.locator('[data-action="close-settings"]').click();

    // 节点详情保留全文，源码定位必须选中原段落，而不是截断后的摘要。
    const paragraph = page.locator('.map-node[data-kind="paragraph"]').filter({ hasText: '把注意力' });
    await paragraph.locator('.map-node-button').click();
    assert.match(await page.locator('#inspector-content').textContent(), /让文档占据更多空间/);
    await page.locator('[data-action="locate-source"]').click();
    assert.equal(await page.locator('#source-pane').isVisible(), true);
    const selectedText = await page.locator('#markdown-input').evaluate(input => input.value.slice(input.selectionStart, input.selectionEnd));
    assert.match(selectedText, /^把注意力留给内容/);
    assert.match(selectedText, /让文档占据更多空间/);
    await page.locator('[data-action="close-inspector"]').click();
    await page.locator('#map-source-toggle').click();
    const originalCount = await page.locator('.map-node').count();
    await page.locator('#collapse-all').click();
    assert.ok(await page.locator('.map-node').count() < originalCount);
    await page.locator('#collapse-all').click();
    assert.equal(await page.locator('.map-node').count(), originalCount);
    const initialZoom = parseInt(await page.locator('#zoom-value').textContent());
    await page.locator('[data-action="zoom-in"]').click();
    assert.ok(parseInt(await page.locator('#zoom-value').textContent()) > initialZoom);
    const zoomed = await page.locator('#zoom-value').textContent();
    await page.locator('#view-read').click();
    await page.locator('#view-mindmap').click();
    assert.equal(await page.locator('#zoom-value').textContent(), zoomed, 'Switching views preserves zoom');
    await page.locator('[data-action="map-fit"]').click();

    // 实际输入多行段落、嵌套任务、代码块和多个 H1，验证转换没有丢失内容。
    const markdown = '# 第一章\n\n这是第一行，\n这是第二行。\n\n- [x] 完成\n  - 子事项\n\n```md\n# 不能变成标题\n```\n\n# 第二章\n\n👩‍💻🙂中文内容超过配置字数时应该完整截取字形。';
    await page.locator('#view-edit').click();
    await page.locator('#edit-source').click();
    await page.locator('#markdown-input').fill(markdown);
    await page.locator('#view-mindmap').click();
    assert.equal(await page.locator('.map-node[data-kind="heading"]').count(), 3, 'Multiple H1 sections share a document root');
    assert.ok((await page.locator('.map-node[data-kind="paragraph"] .map-node-text').allTextContents()).includes('这是第一行， 这是第二行。'));
    assert.equal(await page.locator('#dirty-dot').isVisible(), true);
    const unicode = await page.evaluate(() => VexMindMap.clip('👩‍💻🙂中文', 2));
    assert.equal(unicode, '👩‍💻🙂…', 'A grapheme is not split at a surrogate pair or ZWJ');
    const nested = await page.evaluate(() => {
      const parsed = VexMindMap.parse(document.getElementById('markdown-input').value, '文档');
      return VexMindMap.flatten(parsed.root).find(node => node.text === '完成').children[0].text;
    });
    assert.equal(nested, '子事项');
    await page.locator('.file-row').filter({ hasText: '每周复盘.md' }).click();
    assert.equal(await page.locator('#layer-unsaved').isVisible(), true);
    await page.locator('#layer-unsaved button').filter({ hasText: /^取消$/ }).click();
    assert.equal(await page.locator('#markdown-input').inputValue(), markdown, 'Cancel preserves the edited document');

    // 摘要显示设置不能改变文件，保存副本应保留完整 Markdown。
    const downloadPromise = page.waitForEvent('download');
    await page.locator('.save-button').click();
    const download = await downloadPromise;
    const fs = require('node:fs');
    assert.equal(fs.readFileSync(await download.path(), 'utf8'), markdown);
    assert.equal(await page.locator('#dirty-dot').isVisible(), false);
    await page.locator('.file-row').filter({ hasText: '每周复盘.md' }).click();
    assert.equal(await page.locator('#document-name').textContent(), '每周复盘.md');
    assert.match(await page.locator('#markdown-input').inputValue(), /本周进展/);
    await page.locator('#view-read').click();
    assert.equal(await page.locator('#source-pane').isVisible(), false);
    assert.equal(await page.locator('#reading-pane').isVisible(), true);

    await page.locator('#view-mindmap').click();
    for (const theme of ['light', 'dark']) {
      await page.evaluate(mode => VexTheme.setMode(mode), theme);
      await page.setViewportSize({ width: 980, height: 640 });
      await page.waitForTimeout(300);
      const bounds = await page.evaluate(() => {
        const app = document.querySelector('.app-window').getBoundingClientRect();
        const map = document.getElementById('map-viewport').getBoundingClientRect();
        return { appRight: app.right, appBottom: app.bottom, mapWidth: map.width, mapHeight: map.height, viewportWidth: innerWidth, viewportHeight: innerHeight };
      });
      assert.ok(bounds.appRight <= bounds.viewportWidth && bounds.appBottom <= bounds.viewportHeight);
      assert.ok(bounds.mapWidth > 400 && bounds.mapHeight > 350, 'The map remains usable in a narrow window');
    }
    await page.emulateMedia({ reducedMotion: 'reduce' });
    const duration = await page.locator('.view-switch').evaluate(element => getComputedStyle(element, '::before').transitionDuration);
    assert.equal(duration, '0s');
    // 从系统文件选择入口读取自己的文件，并保持正文与导图一致。
    await page.locator('#open-file-input').setInputFiles({ name: '测试文档.md', mimeType: 'text/markdown', buffer: Buffer.from('# 测试文档\n\n## 第一节\n\n实际导入的内容。', 'utf8') });
    await page.waitForFunction(() => document.getElementById('document-name').textContent === '测试文档.md');
    assert.match(await page.locator('#markdown-input').inputValue(), /实际导入的内容/);
    assert.equal(await page.locator('.map-node.root .map-node-text').textContent(), '测试文档');
    await page.locator('#sb-wordcount').click();
    assert.equal(await page.locator('#layer-statistics').isVisible(), true);
    assert.equal(await page.locator('#layer-statistics .stat-row').filter({ hasText: '标题' }).locator('.v').textContent(), '2');
    await page.keyboard.press('Escape');
    assert.deepEqual(errors, []);
    console.log('Passed: shared views, clipping and Unicode, settings persistence, source mapping, collapse/zoom, live editing, save/cancel, narrow themes, reduced motion.');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
