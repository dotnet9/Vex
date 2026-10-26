/* 桌面工作区：同一辅助面板承载大纲、文章信息和章节详情。 */
(function () {
  'use strict';
  var W = window.VexWorkbench, $ = function (id) { return document.getElementById(id); };
  var sidebar = $('structure-sidebar'), inner = sidebar.querySelector('.structure-inner');
  var active = 'outline', opener = $('outline-toggle'), inspector = $('node-inspector');

  sidebar.setAttribute('aria-label', VexL.t('文档辅助'));
  /* 用户要求：去掉"文档辅助"标题栏（含右侧关闭按钮），内容直接顶格。 */
  var structureHead = sidebar.querySelector('.structure-head');
  if (structureHead) structureHead.remove();
  var labels = { outline:'大纲', info:'文章信息' }, panels = {};
  Object.keys(labels).forEach(function (id) {
    var panel = document.createElement('section'); panel.id = 'auxiliary-' + id; panel.className = 'auxiliary-content';
    panel.setAttribute('role', 'tabpanel'); panel.setAttribute('aria-label', labels[id]); panels[id] = panel;
  });
  /* 大纲面板不再提供"全部折叠"工具按钮（大纲本身不支持折叠，按钮徒增理解成本）。 */
  panels.outline.append(inner.querySelector('.structure-search'), $('outline-body'));
  panels.info.innerHTML = '<div class="article-info-name"></div><div class="article-info-kind">Markdown · UTF-8</div><div class="article-info-hero"><strong id="article-info-count">0</strong><span>字</span><small id="article-info-reading"></small></div><dl class="article-info-metrics"></dl><p class="article-count-rule">字数规则：中文按字，英文按词，不计 Markdown 标记与空白。图片计入替代文字。</p>';

  inspector.setAttribute('aria-label', VexL.t('章节内容'));
  inspector.querySelector('[data-action="close-inspector"]').remove();
  Object.values(panels).forEach(function (panel) { inner.appendChild(panel); });
  $('structure-splitter').setAttribute('aria-label', VexL.t('调整辅助面板宽度'));
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
  }
  function sync() {
    var open = !sidebar.classList.contains('collapsed'), overlay = document.querySelector('.workspace').classList.contains('auxiliary-overlay');
    sidebar.setAttribute('role', overlay ? 'dialog' : 'complementary');
    $('outline-toggle').setAttribute('aria-pressed', String(open && active === 'outline'));
    $('outline-toggle').title = $('outline-toggle').ariaLabel = VexL.t(open && active === 'outline' ? '收起文档大纲' : '展开文档大纲');
    $('sb-wordcount').setAttribute('aria-expanded', String(open && active === 'info'));
    $('sb-wordcount').classList.toggle('on', open && active === 'info');
    Object.keys(panels).forEach(function (id) { panels[id].hidden = id !== active; });
    updateInfo();
  }
  function show(id, trigger, focus) {
    if (!panels[id]) return;
    if (trigger && !sidebar.contains(trigger)) opener = trigger;
    active = id; W.showStructure(true); sync();
    if (focus) { var tab = $('auxiliary-tab-' + id); if (tab) tab.focus({preventScroll:true}); }
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

  ['vex-content-changed', 'vex-save-state-changed', 'vex-document-loaded', 'vex-language-changed'].forEach(function (name) { window.addEventListener(name, updateInfo); });
  document.addEventListener('pointerdown', function (event) {
    if (!sidebar.classList.contains('collapsed') && document.querySelector('.workspace').classList.contains('auxiliary-overlay') && event.target.closest('#view-surface, .sidebar')) close(false);
  });
  document.addEventListener('keydown', function (event) {
    if (event.key !== 'Escape' || sidebar.classList.contains('collapsed') || event.defaultPrevented) return;
    var popup = Array.from(document.querySelectorAll('[role="dialog"], .overlay, .vex-overlay, .typography-popover, .settings-popover, .chapter-delete-dialog, .file-navigation-menu, .file-context-menu, .menu-flyout, .vex-element-menu, .save-details, details[open]#vex-appearance-menu')).some(function (element) { return !sidebar.contains(element) && element.getClientRects().length > 0; });
    if (event.target.closest('.chapter-title-input') || popup) return;
    event.preventDefault(); event.stopPropagation(); close(true);
  }, true);
  var beginGuide = VexProto.beginGuideSession, endGuide = VexProto.endGuideSession;
  VexProto.beginGuideSession = function () { var snapshot = beginGuide(); snapshot.auxiliary = {active:active, open:!sidebar.classList.contains('collapsed')}; return snapshot; };
  VexProto.endGuideSession = function (snapshot) { endGuide(snapshot); if (snapshot?.auxiliary) { active = snapshot.auxiliary.active; W.showStructure(snapshot.auxiliary.open); sync(); } };
  window.VexAuxiliary = {show:show, close:close, sync:sync, handleAction:handleAction, get active() { return active; }};
  sync();
})();

/* 块状工作区：折叠按钮从状态栏迁到块角／窗边，随块宽每帧重算位置。 */
(function () {
  'use strict';
  var $ = function (id) { return document.getElementById(id); };
  var workspace = document.querySelector('.workspace'), statusbar = document.querySelector('.document-statusbar');
  var SIZE = 24, INSET = 8, EDGE = 10;
  /* 展开时按钮落在所属块的内侧下角，折叠时贴到窗体内边框的同侧下角；箭头指向面板收起的方向。 */
  var panes = [
    { btn: $('sidebar-toggle'), side: 'left', pane: document.querySelector('.sidebar'), hidden: 'closed' },
    { btn: $('outline-toggle'), side: 'right', pane: $('structure-sidebar'), hidden: 'collapsed' }
  ];

  panes.forEach(function (entry) {
    var btn = entry.btn;
    btn.classList.add('pane-collapse', 'pane-collapse-' + entry.side);
    btn.querySelector('span').remove();
    var svg = btn.querySelector('.ico');
    svg.removeAttribute('style');
    svg.innerHTML = '<use href="#i-chevron"></use>';
    workspace.appendChild(btn);
  });
  var corners = statusbar.querySelector('.document-corner-actions');
  if (corners) corners.remove();

  function place() {
    var host = workspace.getBoundingClientRect();
    panes.forEach(function (entry) {
      var open = !entry.pane.classList.contains(entry.hidden), rect = open ? entry.pane.getBoundingClientRect() : host;
      entry.btn.classList.toggle('on', open);
      entry.btn.style.left = (entry.side === 'left'
        ? open ? rect.right - host.left - SIZE - INSET : EDGE
        : open ? rect.left - host.left + INSET : host.width - EDGE - SIZE) + 'px';
    });
  }

  new ResizeObserver(place).observe(workspace);
  window.addEventListener('resize', place);
  /* 块宽是渐变的：跟随每一帧重算，按钮才不会停在收起前的旧位置。 */
  var paneWatcher = new ResizeObserver(place);
  panes.forEach(function (entry) {
    paneWatcher.observe(entry.pane);
    new MutationObserver(place).observe(entry.pane, { attributes: true, attributeFilter: ['class', 'style'] });
    new MutationObserver(place).observe(entry.btn, { attributes: true, attributeFilter: ['aria-pressed'] });
  });
  place();
})();

/* 阅读视图右下角缩放坞：与演示正文同一枚 .vex-zoomdock，量程对齐原生 ContentZoom（50–200，还原 100）。 */
(function () {
  'use strict';
  var pane = document.getElementById('reading-pane');
  if (!pane) return;
  var dock = document.createElement('footer');
  dock.className = 'vex-zoomdock reading-zoom';
  dock.innerHTML = '<div><span class="zoom-tools"><input type="range" class="zoom-range" min="50" max="200" step="5" data-l10n-label="内容缩放" aria-label="' + VexL.t('内容缩放') + '"><span class="zoom-value" aria-hidden="true"></span></span><button type="button" class="zoom-reset" data-l10n-tip="还原字号" data-l10n-label="还原字号"><svg class="ico" aria-hidden="true"><use href="#i-fit"/></svg></button></div>';
  pane.appendChild(dock);
  VexZoomDock.attach(dock);
  var range = dock.querySelector('.zoom-range'), reset = dock.querySelector('.zoom-reset');
  function sync() { var zoom = Number(VexPreferences.values.zoom) || 100; range.value = zoom; VexZoomDock.update(dock, Math.round(zoom) + '%'); }
  range.addEventListener('input', function () { VexPreferences.set('zoom', Number(range.value)); });
  reset.onclick = function () { VexPreferences.set('zoom', 100); };
  var tip = VexL.t('还原字号');
  reset.title = tip;
  reset.setAttribute('aria-label', tip);
  window.addEventListener('vex-preference-changed', function (event) { if (event.detail.key === 'zoom') sync(); });
  sync();
})();
