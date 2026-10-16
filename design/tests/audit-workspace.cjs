const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const { chromium } = require('playwright');
const fixture = require('./standalone-fixture.cjs');

(async () => {
  const standalone = fixture(path.resolve(__dirname, '../Vex.html'));
  const browser = await chromium.launch({ headless: true, executablePath: process.env.VEX_BROWSER_EXECUTABLE || undefined });
  const context = await browser.newContext({ viewport: { width: 1440, height: 980 } });
  const errors = [];
  context.on('page', page => page.on('pageerror', error => errors.push(error.message)));
  const page = await context.newPage();
  const output = path.resolve(process.env.CODEWF_TEMP_ROOT || require('node:os').tmpdir(), 'vex-workspace');
  fs.mkdirSync(output, { recursive: true });
  const panel = (id, target = page) => target.locator(`.view-surface [data-document="${id}"]`);
  const choose = async (id, keepOpen = false) => {
    if (!await page.locator('#workspace-layout-menu').isVisible()) await page.locator('#workspace-layout').click();
    await page.locator(`[data-layout="${id}"]`).click();
    assert.equal(await page.locator('#workspace-layout-menu').isVisible(), true, '切换布局保留面板，方便连续比较');
    assert.equal(await page.locator(`[data-layout="${id}"]`).getAttribute('aria-pressed'), 'true');
    if (!keepOpen) await page.locator('#workspace-layout-menu [aria-label="关闭布局菜单"]').click();
  };
  const selectChapter = async title => {
    const node = page.locator('.map-node-button').filter({ has: page.locator('.map-node-text').filter({ hasText: new RegExp('^' + title + '$') }) });
    await node.evaluate(element => element.click());
  };
  try {
    await standalone.attach(page);
    await page.goto(standalone.url + '?layout=three&theme=light&palette=aurora');
    assert.equal(await page.locator('.workspace-file-tabs, .dock-document, [data-drag-panel], #tool-options').count(), 0, '单文档界面没有文档标签和 Dock 操作');
    const combinations = {
      source: ['editor'], instant: ['editor'], read: ['render'], map: ['map'],
      'source-render': ['editor', 'render'], 'source-map': ['editor', 'map'], 'instant-map': ['editor', 'map'], three: ['editor', 'render', 'map']
    };
    for (const [layout, ids] of Object.entries(combinations)) {
      await choose(layout, true);
      assert.deepEqual(await page.locator('.view-surface .view-panel').evaluateAll(items => items.map(item => item.dataset.document)), ids);
      assert.equal(await page.locator('#source-pane').isVisible() && await page.locator('#instant-pane').isVisible(), false, '源码和即时编辑始终互斥');
    }
    await choose('three');
    const bounds = await page.locator('.view-surface .view-panel').evaluateAll(items => items.map(item => {
      const rect = item.getBoundingClientRect(); return { id: item.dataset.document, x: rect.x, y: rect.y, width: rect.width, height: rect.height };
    }));
    assert.ok(bounds[0].y === bounds[1].y && bounds[2].y > bounds[0].y && bounds[2].width > bounds[0].width * 1.8);
    await page.locator('#chapter-headings-only').click();
    assert.equal(await page.locator('.map-node.content').count(), 0);
    await page.waitForTimeout(280);
    await page.screenshot({ path: path.join(output, 'workspace-three-light.png'), animations: 'disabled' });
    await page.evaluate(() => VexTheme.setMode('dark'));
    await page.screenshot({ path: path.join(output, 'workspace-three-dark.png'), animations: 'disabled' });
    await page.evaluate(() => VexTheme.setMode('light'));
    await choose('source-map');
    const original = await page.locator('#markdown-input').inputValue();
    await selectChapter('主题色体系');
    await page.locator('#chapter-title-input').fill('新的主题色体系');
    await page.locator('#chapter-title-input').press('Enter');
    assert.match(await page.locator('#markdown-input').inputValue(), /## 新的主题色体系/);
    assert.match(await page.locator('#outline-body').textContent(), /新的主题色体系/);
    await page.locator('#chapter-body-input').fill('这段正文来自导图，并随章节一起移动。');
    await page.locator('#chapter-body-input').press('Tab');
    assert.match(await page.locator('#markdown-input').inputValue(), /这段正文来自导图/);
    await page.locator('[data-chapter-command="up"]').click();
    const moved = await page.locator('#markdown-input').inputValue();
    assert.ok(moved.indexOf('## 新的主题色体系') < moved.indexOf('## 设计原则'));
    assert.match(moved, /## 新的主题色体系\n\n这段正文来自导图，并随章节一起移动。/);
    await page.locator('[data-chapter-command="down"]').click();
    await page.locator('[data-chapter-command="demote"]').click();
    assert.match(await page.locator('#markdown-input').inputValue(), /### 新的主题色体系/);
    await page.locator('[data-chapter-command="promote"]').click();
    assert.match(await page.locator('#markdown-input').inputValue(), /\n## 新的主题色体系/);
    // 真实画布内输入标题，不仅通过侧边详情操作。
    await page.locator('[data-chapter-command="child"]').click();
    await page.locator('.chapter-rename').fill('刚加入的子章节');
    await page.locator('.chapter-rename').press('Enter');
    assert.match(await page.locator('#markdown-input').inputValue(), /### 刚加入的子章节/);
    await page.locator('[data-chapter-command="delete"]').click();
    await page.locator('#chapter-delete-cancel').click();
    assert.match(await page.locator('#markdown-input').inputValue(), /刚加入的子章节/);
    await page.locator('[data-chapter-command="delete"]').click();
    await page.locator('#chapter-delete-confirm').click();
    assert.doesNotMatch(await page.locator('#markdown-input').inputValue(), /刚加入的子章节/);
    await page.keyboard.press('Control+z');
    assert.match(await page.locator('#markdown-input').inputValue(), /刚加入的子章节/);
    assert.equal(await panel('map').isVisible(), true, '撤销章节修改保持当前布局');
    // 切编辑方式保留导图，正文与撤销历史贯通。
    await page.locator('#edit-instant').click();
    assert.equal(await panel('map').isVisible(), true);
    assert.equal(await page.locator('#instant-pane').isVisible(), true);
    assert.equal(await page.locator('#source-pane').isVisible(), false);
    assert.equal(await page.evaluate(() => VexWorkspace.current), 'instant-map');
    await page.screenshot({ path: path.join(output, 'workspace-instant-map.png'), animations: 'disabled' });
    await page.locator('#edit-source').click();
    await choose('three');
    // 从面板菜单真实拆出两个窗口，校验同一文件与双向编辑。
    const readingPromise = context.waitForEvent('page');
    await panel('render').locator('[data-float-view]').click();
    const reading = await readingPromise;
    await reading.waitForFunction(() => document.getElementById('detached-file-name')?.textContent === '主题设计笔记.md');
    const mapPromise = context.waitForEvent('page');
    await panel('map').locator('[data-float-view]').click();
    const map = await mapPromise;
    await map.waitForFunction(() => document.getElementById('detached-file-name')?.textContent === '主题设计笔记.md');
    assert.deepEqual(await page.evaluate(() => VexWorkspace.floating.sort()), ['map', 'render']);
    const source = await page.locator('#markdown-input').inputValue();
    await page.locator('#markdown-input').fill(source + '\n\n## 三屏同步测试\n\n这句话应该出现在另两个窗口。\n');
    await reading.waitForFunction(() => document.getElementById('markdown-preview').textContent.includes('三屏同步测试'));
    await map.waitForFunction(() => [...document.querySelectorAll('.map-node-text')].some(node => node.textContent === '三屏同步测试'));
    await map.locator('.map-node-button').filter({ hasText: '三屏同步测试' }).evaluate(element => element.click());
    await map.locator('#chapter-title-input').fill('从副屏修改的章节');
    await map.locator('#chapter-title-input').press('Enter');
    await page.waitForFunction(() => document.getElementById('markdown-input').value.includes('从副屏修改的章节'));
    await reading.waitForFunction(() => document.getElementById('markdown-preview').textContent.includes('从副屏修改的章节'));
    await map.locator('#return-to-workspace').click();
    await page.waitForFunction(() => VexWorkspace.floating.length === 1);
    assert.equal(await panel('map').isVisible(), true);
    // 文件切换原子刷新全部窗口，保留布局。
    await page.locator('.file-row').nth(1).click();
    await page.locator('#layer-unsaved [data-close]').filter({ hasText: '不保存' }).click();
    await reading.waitForFunction(() => document.getElementById('detached-file-name').textContent === '每周复盘.md');
    assert.match(await reading.locator('#markdown-preview').textContent(), /本周进展/);
    assert.doesNotMatch(await reading.locator('#markdown-preview').textContent(), /从副屏修改的章节/);
    assert.match(await page.locator('#markdown-input').inputValue(), /每周复盘/);
    await reading.locator('#return-to-workspace').click();
    await page.waitForFunction(() => VexWorkspace.floating.length === 0);
    await choose('source-map');
    const editorPromise = context.waitForEvent('page');
    await panel('editor').locator('[data-float-view]').click();
    const editor = await editorPromise;
    await editor.waitForFunction(() => document.getElementById('detached-file-name')?.textContent === '每周复盘.md');
    const beforeDetachedEdit = await editor.locator('#markdown-input').inputValue();
    assert.equal(await editor.locator('#detached-view-name').textContent(), '源码');
    await editor.locator('#markdown-input').fill(beforeDetachedEdit + '\n\n副屏源码输入。');
    await page.waitForFunction(() => document.getElementById('markdown-input').value.includes('副屏源码输入'));
    assert.equal(await editor.locator('#markdown-input').evaluate(input => document.activeElement === input), true, '同步回声保留副屏输入焦点');
    await editor.keyboard.press('Control+z');
    await page.waitForFunction(() => !document.getElementById('markdown-input').value.includes('副屏源码输入'));
    await editor.locator('#edit-instant').click();
    await page.waitForFunction(() => VexWorkspace.editorMode === 'instant');
    assert.equal(await page.evaluate(() => VexWorkspace.current), 'instant-map');
    assert.equal(await editor.locator('#detached-view-name').textContent(), '即时编辑');
    assert.equal(await editor.locator('#source-pane').isVisible(), false);
    await editor.locator('.instant-block').filter({ hasText: /^完成写作工作台/ }).click();
    await editor.locator('.instant-input').fill('副屏即时输入保持活动块。');
    await page.waitForFunction(() => document.getElementById('markdown-input').value.includes('副屏即时输入'));
    assert.equal(await editor.locator('.instant-input').evaluate(input => document.activeElement === input), true);
    await editor.locator('.instant-input').press('Control+Enter');
    await editor.screenshot({ path: path.join(output, 'workspace-detached-editor.png'), animations: 'disabled' });
    await editor.locator('#return-to-workspace').click();
    await page.waitForFunction(() => VexWorkspace.floating.length === 0);
    assert.equal(await page.locator('#instant-pane').isVisible(), true);
    // 使用真实拖放把章节及正文移到另一个章节之前。
    await page.locator('[data-action="map-fit"]').click();
    const moving = page.locator('.map-node[data-kind="heading"]').filter({ hasText: '下周计划' });
    const target = page.locator('.map-node[data-kind="heading"]').filter({ hasText: '遇到的问题' });
    await moving.dragTo(target, { targetPosition: { x: 30, y: 3 } });
    const reordered = await page.locator('#markdown-input').inputValue();
    assert.ok(reordered.indexOf('## 下周计划') < reordered.indexOf('## 遇到的问题'));
    assert.match(reordered, /## 下周计划\n\n先验证原型/);
    await page.setViewportSize({ width: 980, height: 700 });
    await page.screenshot({ path: path.join(output, 'workspace-narrow.png'), animations: 'disabled' });
    const geometry = await page.evaluate(() => ({ app: document.querySelector('.app-window').getBoundingClientRect().right, width: innerWidth, panels: [...document.querySelectorAll('.view-surface .view-panel')].map(panel => panel.getBoundingClientRect().width) }));
    assert.ok(geometry.app <= geometry.width && geometry.panels.every(width => width > 200));
    await page.locator('#workspace-layout').click();
    await page.waitForTimeout(200);
    await page.screenshot({ path: path.join(output, 'workspace-layout-menu.png'), animations: 'disabled' });
    await standalone.verify(page);
    assert.deepEqual(errors, []);
    console.log('Passed: eight layouts, exclusive editors, chapter title/body/add/move/hierarchy/delete/undo, real floating windows, bidirectional content sync, global file switching, single current document without document tabs or docking, and narrow geometry.');
  } finally { await browser.close(); standalone.dispose(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
