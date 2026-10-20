/* 桌面工作区：同一辅助面板承载大纲、文章信息和章节详情。 */
(function () {
  'use strict';
  var W = window.VexWorkbench, $ = function (id) { return document.getElementById(id); };
  var sidebar = $('structure-sidebar'), inner = sidebar.querySelector('.structure-inner');
  var active = 'outline', opener = $('outline-toggle'), inspector = $('node-inspector');
  // 不常用的文件展示操作收进“更多”，为文件夹名称留出空间。
  ['collapse-files', 'file-list-toggle'].forEach(function (id) {
    var button = $(id), label = document.createElement('span');
    label.textContent = id === 'collapse-files' ? '全部折叠' : '切换文件列表 / 目录树';
    button.appendChild(label); $('file-navigation-menu').appendChild(button);
  });
  var elementEntry = $('edit-options').querySelector('.vex-element-entry');
  if (elementEntry) { elementEntry.innerHTML = '<span>元素</span><svg class="ico" aria-hidden="true"><use href="#i-chevron-down"/></svg>'; elementEntry.title = '当前元素操作 · Shift+F10'; }
  sidebar.setAttribute('aria-label', '文档辅助');
  sidebar.querySelector('.structure-title').textContent = '文档辅助';
  sidebar.querySelector('.structure-toggle').innerHTML = '<svg class="ico" aria-hidden="true"><use href="#i-x"/></svg>';
  sidebar.querySelector('.structure-toggle').dataset.action = 'close-auxiliary';
  var tabs = document.createElement('div'); tabs.className = 'auxiliary-tabs'; tabs.setAttribute('role', 'tablist'); tabs.setAttribute('aria-label', '文档辅助内容');
  var labels = { outline:'大纲', info:'文章信息', chapter:'章节详情' }, panels = {};
  Object.keys(labels).forEach(function (id) {
    var button = document.createElement('button'); button.type = 'button'; button.id = 'auxiliary-tab-' + id;
    button.dataset.auxiliary = id; button.setAttribute('role', 'tab'); button.setAttribute('aria-controls', 'auxiliary-' + id); button.textContent = labels[id];
    button.onclick = function () { show(id, button, true); };
    tabs.appendChild(button);
    var panel = document.createElement('section'); panel.id = 'auxiliary-' + id; panel.className = 'auxiliary-content';
    panel.setAttribute('role', 'tabpanel'); panel.setAttribute('aria-labelledby', button.id); panels[id] = panel;
  });
  inner.querySelector('.structure-head').after(tabs);
  panels.outline.append(inner.querySelector('.structure-search'), $('outline-body'));
  panels.info.innerHTML = '<div class="article-info-name"></div><div class="article-info-kind">Markdown · UTF-8</div><div class="article-info-hero"><strong id="article-info-count">0</strong><span>字</span><small id="article-info-reading"></small></div><dl class="article-info-metrics"></dl><p class="article-count-rule">字数规则：中文按字，英文按词，不计 Markdown 标记与空白。图片计入替代文字。</p><p class="article-info-save"></p><button type="button" class="article-save-details">查看保存详情</button>';
  panels.info.querySelector('.article-save-details').onclick = function () { $('document-save-status').click(); };
  var emptyChapter = document.createElement('div'); emptyChapter.className = 'chapter-detail-empty';
  emptyChapter.innerHTML = '<svg class="ico" aria-hidden="true"><use href="#i-map"/></svg><strong>选择一个章节</strong><p>在导图中选择节点，即可查看和编辑章节正文。</p><button type="button" class="btn sm">打开思维导图</button>';
  emptyChapter.querySelector('button').onclick = function () { VexWorkspace.openPanel('map'); $('map-viewport').focus({preventScroll:true}); };
  inspector.setAttribute('aria-label', '章节内容');
  inspector.querySelector('[data-action="close-inspector"]').remove();
  panels.chapter.append(emptyChapter, inspector);
  Object.values(panels).forEach(function (panel) { inner.appendChild(panel); });
  $('structure-splitter').setAttribute('aria-label', '调整辅助面板宽度');
  $('structure-splitter').setAttribute('aria-valuemin', '280');

  function updateInfo() {
    if (active !== 'info' || sidebar.classList.contains('collapsed')) return;
    var counts = VexWriting.metrics(W.text), blocks = W.state.parsed.blocks;
    panels.info.querySelector('.article-info-name').textContent = W.name;
    $('article-info-count').textContent = counts.count.toLocaleString();
    $('article-info-reading').textContent = counts.count ? '约 ' + Math.ceil(counts.count / 350) + ' 分钟读完' : '尚未开始写作';
    var metrics = [['正文字符（不含空白）', counts.characters.toLocaleString()], ['汉字等单字数', counts.cjkCount.toLocaleString()], ['英文及数字词数', counts.wordCount.toLocaleString()], ['标题', blocks.filter(function (block) { return block.kind === 'heading'; }).length], ['段落', blocks.filter(function (block) { return block.kind === 'paragraph'; }).length], ['源码行数', W.state.parsed.lines.length], ['文件大小', new Blob([W.text]).size.toLocaleString() + ' 字节']];
    var list = panels.info.querySelector('dl'); list.replaceChildren();
    metrics.forEach(function (metric) { var key = document.createElement('dt'), value = document.createElement('dd'); key.textContent = metric[0]; value.textContent = metric[1]; list.append(key, value); });
    panels.info.querySelector('.article-info-save').textContent = $('document-save-status').textContent;
  }
  function sync() {
    var open = !sidebar.classList.contains('collapsed'), overlay = document.querySelector('.workspace').classList.contains('auxiliary-overlay');
    sidebar.setAttribute('role', overlay ? 'dialog' : 'complementary');
    $('outline-toggle').setAttribute('aria-pressed', String(open && active === 'outline'));
    $('outline-toggle').title = $('outline-toggle').ariaLabel = open && active === 'outline' ? '收起文档大纲' : '展开文档大纲';
    $('sb-wordcount').setAttribute('aria-expanded', String(open && active === 'info'));
    $('sb-wordcount').classList.toggle('on', open && active === 'info');
    tabs.querySelectorAll('button').forEach(function (button) { var selected = button.dataset.auxiliary === active; button.setAttribute('aria-selected', String(selected)); button.tabIndex = selected ? 0 : -1; });
    Object.keys(panels).forEach(function (id) { panels[id].hidden = id !== active; });
    emptyChapter.hidden = !inspector.hidden;
    updateInfo();
  }
  function show(id, trigger, focus) {
    if (!panels[id]) return;
    if (trigger && !sidebar.contains(trigger)) opener = trigger;
    active = id; W.showStructure(true); sync();
    if (focus) $('auxiliary-tab-' + id).focus({preventScroll:true});
  }
  function close(restore) {
    var target = opener && opener.isConnected && !opener.closest('[hidden]') ? opener : $('outline-toggle');
    if (restore || sidebar.contains(document.activeElement)) target.focus({preventScroll:true});
    W.showStructure(false);
  }
  function handleAction(action) {
    if (action === 'dlg-statistics' || action === 'dlg-properties') { W.finish(); show('info', document.activeElement, true); return true; }
    if (action === 'toggle-structure') {
      if (!sidebar.classList.contains('collapsed') && active === 'outline') close(true);
      else show('outline', $('outline-toggle'), true);
      return true;
    }
    if (action === 'pane-outline') { show('outline', $('outline-toggle'), true); return true; }
    if (action === 'close-inspector' || action === 'close-auxiliary') { close(true); return true; }
    return false;
  }
  tabs.addEventListener('keydown', function (event) {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault(); var ids = Object.keys(labels), index = ids.indexOf(active);
    show(ids[event.key === 'Home' ? 0 : event.key === 'End' ? ids.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : ids.length - 1)) % ids.length], null, true);
  });
  window.addEventListener('vex-node-selected', function () { show('chapter', document.querySelector('.map-node.selected .map-node-button'), false); });
  new MutationObserver(function () { emptyChapter.hidden = !inspector.hidden; }).observe(inspector, {attributes:true, attributeFilter:['hidden']});
  ['vex-content-changed', 'vex-save-state-changed', 'vex-document-loaded'].forEach(function (name) { window.addEventListener(name, updateInfo); });
  document.addEventListener('pointerdown', function (event) {
    if (!sidebar.classList.contains('collapsed') && document.querySelector('.workspace').classList.contains('auxiliary-overlay') && event.target.closest('#view-surface, .sidebar')) close(false);
  });
  document.addEventListener('keydown', function (event) {
    if (event.key !== 'Escape' || sidebar.classList.contains('collapsed') || event.defaultPrevented) return;
    var popup = Array.from(document.querySelectorAll('[role="dialog"], .overlay, .vex-overlay, .typography-popover, .settings-popover, .chapter-delete-dialog, .file-navigation-menu, .file-context-menu, .menu-flyout, .vex-element-menu, details[open]#vex-appearance-menu')).some(function (element) { return !sidebar.contains(element) && element.getClientRects().length > 0; });
    if (event.target.closest('.chapter-title-input') || popup) return;
    event.preventDefault(); event.stopPropagation(); close(true);
  }, true);
  var beginGuide = VexProto.beginGuideSession, endGuide = VexProto.endGuideSession;
  VexProto.beginGuideSession = function () { var snapshot = beginGuide(); snapshot.auxiliary = {active:active, open:!sidebar.classList.contains('collapsed')}; return snapshot; };
  VexProto.endGuideSession = function (snapshot) { endGuide(snapshot); if (snapshot?.auxiliary) { active = snapshot.auxiliary.active; W.showStructure(snapshot.auxiliary.open); sync(); } };
  window.VexAuxiliary = {show:show, close:close, sync:sync, handleAction:handleAction, get active() { return active; }};
  sync();
})();
