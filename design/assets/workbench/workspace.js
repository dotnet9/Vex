/* Vex 工作区的单窗口变体：固定视图组合，所有面板共用当前文档。 */
(function () {
  'use strict';
  var W = window.VexWorkbench, $ = function (id) { return document.getElementById(id); };
  var query = new URLSearchParams(location.search), current = 'instant', editorMode = W.state.editMode, tree, changing = false, guideActive = false;
  var panels = {}, area = document.querySelector('.content-area');
  var splitRatios = {}; try { splitRatios = JSON.parse(VexStorage.getItem('vex-design-split-ratio') || '{}'); } catch (_) {}
  var labels = { editor: '编辑', render: '阅读', map: '思维导图' }, icons = { editor: 'edit', render: 'eye', map: 'map' };
  var presets = [
    { id: 'instant', title: '即时编辑', note: '专注连续写作', mode: 'instant', tree: 'editor' },
    { id: 'source', title: '源码编辑', note: '精确编辑 Markdown', mode: 'source', tree: 'editor' },
    { id: 'read', title: '阅读', note: '查看完整正文', tree: 'render' },
    { id: 'map', title: '思维导图', note: '先梳理章节结构', tree: 'map' },
    { id: 'source-render', title: '源码 + 预览', note: '检查 Markdown 排版效果', mode: 'source', tree: ['horizontal', 'editor', 'render'] },
    { id: 'source-map', title: '源码 + 导图', note: '用 Markdown 搭建结构', mode: 'source', tree: ['horizontal', 'editor', 'map'] },
    { id: 'instant-map', title: '即时编辑 + 导图', note: '写正文，也看全局', mode: 'instant', tree: ['horizontal', 'editor', 'map'] },
    { id: 'three', title: '三视图', note: '源码、预览与导图同时对照', mode: 'source', tree: ['vertical', ['horizontal', 'editor', 'render'], 'map'] }
  ];
  function icon(name) { return '<svg class="ico" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; }
  function clone(value) { return JSON.parse(JSON.stringify(value)); }
  function leaves(node) { return typeof node === 'string' ? [node] : leaves(node[1]).concat(leaves(node[2])); }
  function saveLayout() { if (!guideActive && !W.state.guideRestoring) VexStorage.setItem('vex-design-layout', JSON.stringify({ current: current, mode: editorMode })); }
  function makePanel(id, sections) {
    var panel = document.createElement('section'); panel.className = 'view-panel'; panel.dataset.document = id; panel.setAttribute('aria-label', labels[id]);
    var head = document.createElement('div'); head.className = 'view-heading';
    head.innerHTML = '<span class="view-caption">' + icon(icons[id]) + '<span class="view-title"></span></span>';
    var body = document.createElement('div'); body.className = 'view-body';
    sections.forEach(function (section) { body.appendChild($(section)); });
    panel.append(head, body); panels[id] = panel; area.appendChild(panel);
  }
  makePanel('editor', ['source-pane', 'instant-pane']); makePanel('render', ['reading-pane']); makePanel('map', ['map-pane']);
  /* 悬浮工具条钉住开关：每条独立持久化（源码/阅读/导图），默认全部钉住为普通工具行。 */
  var pinKeys = { editor: 'pinEditor', render: 'pinReading', map: 'pinMap' };
  function barPinned(id) { return VexPreferences.values[pinKeys[id]] !== false; }
  function applyToolbarPin() {
    Object.keys(pinKeys).forEach(function (id) {
      var pinned = barPinned(id);
      document.documentElement.dataset['pin' + id.charAt(0).toUpperCase() + id.slice(1)] = String(pinned);
      var entry = pinBars.find(function (item) { return item[0] === id; });
      if (entry && entry[2]) {
        entry[2].setAttribute('aria-pressed', String(pinned));
        entry[2].title = pinned ? '工具条已钉住，点击取消钉住' : '钉住工具条';
      }
    });
  }
  window.addEventListener('vex-preference-changed', function (event) {
    if (event.detail && event.detail.key && Object.keys(pinKeys).some(function (id) { return pinKeys[id] === event.detail.key; })) applyToolbarPin();
  });
  var PIN_SVG = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><g class="pin-on"><path class="pin-head" d="M8 3h8l-1 8H9l-1-8z"/><path d="M12 11v9"/></g><g class="pin-off"><g transform="rotate(45 12 11)"><path d="M8 3h8l-1 8H9l-1-8z"/><path d="M12 11v9"/></g></g></svg>';
  function buildPinButton(bar, id) {
    var pin = document.createElement('button'); pin.type = 'button'; pin.className = 'icon-button toolbar-pin';
    pin.title = '钉住工具条（取消后悬浮自动隐藏）';
    pin.innerHTML = PIN_SVG;
    pin.setAttribute('aria-pressed', String(barPinned(id)));
    pin.onclick = function () { VexPreferences.set(pinKeys[id], VexPreferences.values[pinKeys[id]] === false); applyToolbarPin(); };
    bar.appendChild(pin);
    return pin;
  }
  var parking = document.createElement('div'); parking.hidden = true; parking.id = 'view-parking'; area.appendChild(parking);
  var surface = document.createElement('div'); surface.className = 'view-surface'; surface.id = 'view-surface'; area.appendChild(surface);
  var statusbar = document.querySelector('.document-statusbar'), wordCount = document.getElementById('sb-wordcount');
  var typeButton=$('edit-options').querySelector('[data-action="typography"]');
  typeButton.setAttribute('aria-haspopup','dialog');typeButton.setAttribute('aria-controls','typography-panel');typeButton.title=VexL.t('正文排版');
  /* 排版入口与格式工具栏同处一条悬浮条（右对齐），标题行隐藏。 */
  $('edit-options').appendChild(typeButton);
  panels.editor.insertBefore($('edit-options'),panels.editor.querySelector('.view-body'));
  var readingTypeButton=typeButton.cloneNode(true);panels.render.querySelector('.view-heading').appendChild(readingTypeButton);
  $('reading-pane').querySelector('[data-action="typography"]').remove();
  var mapButton=document.createElement('button');mapButton.className='map-appearance-trigger';mapButton.dataset.action='map-appearance';mapButton.hidden=true;
  mapButton.setAttribute('aria-haspopup','dialog');mapButton.setAttribute('aria-expanded','false');mapButton.setAttribute('aria-controls','map-appearance-panel');mapButton.title='导图样式';
  mapButton.innerHTML=icon('map')+'<span>导图样式</span>'+icon('chevron-down');
  /* 导图样式并入快捷工具栏右侧（对照源码旁），随工具栏悬浮显示。 */
  panels.map.querySelector('.map-toolbar').appendChild(mapButton);
  /* 三个悬浮工具条各加一枚"钉"：独立持久化；默认全部钉住为普通工具行。 */
  var pinBars = [
    ['editor', $('edit-options')],
    ['render', panels.render.querySelector('.view-heading')],
    ['map', panels.map.querySelector('.map-toolbar')]
  ];
  pinBars.forEach(function (entry) { entry.push(buildPinButton(entry[1], entry[0])); });
  applyToolbarPin();
  var mapAppearance=VexMapAppearance.create({parent:document.querySelector('.window-body'),scene:$('map-scene'),getLayout:function(){return $('map-layout').value;},setLayout:function(key){$('map-layout').value=key;$('map-layout').dispatchEvent(new Event('change'));},isTemporary:function(){return guideActive;},beforeOpen:function(){VexTypography.close();closeMenus();$('map-settings').hidden=true;},avoid:function(rect){window.VexMapView&&VexMapView.avoid(rect);}});
  mapAppearance.setTrigger(mapButton); /* 触发钮徽标回显「结构 · 风格」，不必开弹层也知道当前组合。 */
  /* 内容面顶端标签行：源码 / 阅读 / 导图 下划线直选 + 右端布局选择器（即时编辑与组合视图都在里面）。 */
  var tabrow = document.createElement('div'); tabrow.className = 'workspace-tabrow';
  area.insertBefore(tabrow, surface);
  var viewSwitch = document.createElement('div'); viewSwitch.className = 'workspace-viewswitch workspace-tabs'; viewSwitch.setAttribute('role', 'group'); viewSwitch.setAttribute('aria-label', '视图切换');
  [['source', '源码', 'edit'], ['read', '阅读', 'eye'], ['map', '导图', 'map']].forEach(function (segment) {
    var button = document.createElement('button'); button.type = 'button';
    button.dataset.viewLink = segment[0]; button.innerHTML = icon(segment[2]) + '<span>' + segment[1] + '</span>';
    button.setAttribute('aria-pressed', 'false');
    button.onclick = function () { setLayout(segment[0]); };
    viewSwitch.appendChild(button);
  });
  var layoutButton = document.createElement('button'); layoutButton.className = 'workspace-viewswitch-more'; layoutButton.id = 'workspace-layout';
  layoutButton.setAttribute('aria-haspopup', 'dialog'); layoutButton.setAttribute('aria-expanded', 'false');
  layoutButton.setAttribute('aria-controls', 'workspace-layout-menu');
  layoutButton.innerHTML = '更多' + icon('chevron-down');
  layoutButton.lastElementChild.classList.add('vex-expand-icon','layout-chevron');
  tabrow.appendChild(viewSwitch); tabrow.appendChild(layoutButton);
  var layoutMenu = document.createElement('section'); layoutMenu.className = 'workspace-layout-menu'; layoutMenu.id = 'workspace-layout-menu'; layoutMenu.hidden = true;
  layoutMenu.setAttribute('role', 'dialog'); layoutMenu.setAttribute('aria-label', '工作区视图');
  layoutMenu.innerHTML = '<div class="layout-preset-grid"></div>';
  tabrow.appendChild(layoutMenu);
  function miniature(node) { return typeof node === 'string' ? '<i class="mini-pane mini-' + node + '"></i>' : '<span class="mini-split mini-' + node[0] + '">' + miniature(node[1]) + miniature(node[2]) + '</span>'; }
  presets.forEach(function (preset) { var button = document.createElement('button'); button.className = 'layout-preset'; button.dataset.layout = preset.id; button.innerHTML = '<span class="layout-miniature">' + miniature(preset.tree) + '</span><span class="layout-name">' + preset.title + '</span><span class="preset-check">✓</span>'; button.onclick = function () { setLayout(preset.id); closeMenus(true); }; layoutMenu.querySelector('.layout-preset-grid').appendChild(button); });
  function closeMenus(restore) { var wasOpen = !layoutMenu.hidden; layoutMenu.hidden = true; layoutButton.setAttribute('aria-expanded', 'false'); if (restore && wasOpen) layoutButton.focus(); }
  layoutButton.onclick = function () { var show = layoutMenu.hidden; closeMenus(); layoutMenu.hidden = !show; layoutButton.setAttribute('aria-expanded', String(show)); if (show) layoutMenu.querySelector('[data-layout="' + current + '"]').focus(); };
  layoutMenu.addEventListener('keydown', function (event) {
    var buttons = Array.from(layoutMenu.querySelectorAll('.layout-preset')), index = buttons.indexOf(document.activeElement);
    if (index >= 0 && ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Home', 'End'].includes(event.key)) {
      event.preventDefault(); var columns = getComputedStyle(layoutMenu.querySelector('.layout-preset-grid')).gridTemplateColumns.split(' ').length;
      var step = { ArrowLeft:-1, ArrowRight:1, ArrowUp:-columns, ArrowDown:columns }[event.key];
      buttons[event.key === 'Home' ? 0 : event.key === 'End' ? buttons.length - 1 : (index + step + buttons.length) % buttons.length].focus();
    }
    if (event.key === 'Tab') {
      var controls = Array.from(layoutMenu.querySelectorAll('button')), at = controls.indexOf(document.activeElement);
      if (event.shiftKey && at === 0 || !event.shiftKey && at === controls.length - 1) { event.preventDefault(); controls[event.shiftKey ? controls.length - 1 : 0].focus(); }
    }
  });
  function build(node, path) {
    if (typeof node === 'string') return panels[node];
    var split = document.createElement('div'); split.className = 'view-split view-' + node[0];
    var vertical = node[0] === 'vertical';
    var first = build(node[1], path + '0'), second = build(node[2], path + '1');
    split.append(first, makeHandle(split, vertical, current + ':' + path, first, second), second);
    return split;
  }
  /* 分屏比例：按「视图预设 + 路径」持久化，把手悬浮/聚焦时显出握柄，支持方向键。 */
  function makeHandle(split, vertical, key, first, second) {
    var handle = document.createElement('div');
    handle.className = 'split-handle'; handle.tabIndex = 0;
    handle.setAttribute('role', 'separator');
    handle.setAttribute('aria-orientation', vertical ? 'horizontal' : 'vertical');
    handle.setAttribute('aria-valuemin', '20'); handle.setAttribute('aria-valuemax', '80');
    handle.setAttribute('aria-label', VexL.t(vertical ? '调整上下视图高度' : '调整左右视图宽度'));
    function ratio() { return splitRatios[key] || .5; }
    function apply(value, persist) {
      value = Math.max(.2, Math.min(.8, value));
      splitRatios[key] = value;
      first.style.flex = '0 0 ' + (value * 100).toFixed(2) + '%';
      second.style.flex = '';
      handle.setAttribute('aria-valuenow', String(Math.round(value * 100)));
      if (persist) try { VexStorage.setItem('vex-design-split-ratio', JSON.stringify(splitRatios)); } catch (_) {}
    }
    apply(ratio(), false);
    handle.addEventListener('pointerdown', function (event) {
      if (event.button !== 0) return;
      event.preventDefault();
      var rect = split.getBoundingClientRect(), size = vertical ? rect.height : rect.width;
      handle.setPointerCapture(event.pointerId);
      document.body.classList.add('split-resizing');
      function move(event) { apply(vertical ? (event.clientY - rect.top) / size : (event.clientX - rect.left) / size, false); }
      function stop() {
        document.body.classList.remove('split-resizing');
        handle.removeEventListener('pointermove', move);
        apply(ratio(), true);
      }
      handle.addEventListener('pointermove', move);
      ['pointerup', 'pointercancel', 'lostpointercapture'].forEach(function (name) { handle.addEventListener(name, stop, { once: true }); });
    });
    handle.addEventListener('keydown', function (event) {
      var step = { ArrowLeft:-.05, ArrowRight:.05, ArrowUp:-.05, ArrowDown:.05 }[event.key];
      if (step) apply(ratio() + step, true);
      else if (event.key === 'Home' || event.key === 'End') apply(event.key === 'Home' ? .2 : .8, true);
      else return;
      event.preventDefault();
    });
    return handle;
  }
  function renderLayout() {
    var source = editorMode === 'source', displayed = leaves(tree);
    Object.values(panels).forEach(function (panel) { panel.style.flex = ''; parking.appendChild(panel); }); surface.replaceChildren(build(tree, ''));
    $('source-pane').hidden = !source || !displayed.includes('editor'); $('instant-pane').hidden = source || !displayed.includes('editor');
    $('reading-pane').hidden = !displayed.includes('render'); $('map-pane').hidden = !displayed.includes('map');
    panels.editor.querySelector('.view-title').textContent = source ? '源码' : '即时编辑'; panels.render.querySelector('.view-title').textContent = '阅读'; panels.map.querySelector('.view-title').textContent = '思维导图';
    surface.dataset.single = String(displayed.length === 1); $('edit-options').hidden = !displayed.includes('editor');
    typeButton.hidden=!displayed.includes('editor')||source;
    $('edit-options').appendChild(typeButton);
    readingTypeButton.hidden=!displayed.includes('render');
    /* 单阅读视图没有自己的工具行：排版入口并进标签行右端，空标题行整行收起，全窗只留一条上下文行。 */
    var singleRender = displayed.length === 1 && displayed[0] === 'render';
    var renderHead = panels.render.querySelector('.view-heading');
    renderHead.hidden = singleRender;
    if (singleRender) tabrow.insertBefore(readingTypeButton, layoutButton); else renderHead.insertBefore(readingTypeButton, renderHead.querySelector('.toolbar-pin'));
    mapButton.hidden=!displayed.includes('map');
    if(typeButton.hidden&&readingTypeButton.hidden)VexTypography.close();
    if(mapButton.hidden)mapAppearance.close();
    W.state.source = displayed.includes('editor') && source; $('map-source-toggle').setAttribute('aria-pressed', String(W.state.source)); $('map-source-toggle').classList.toggle('on', W.state.source);
    document.querySelector('.workspace').classList.toggle('source-only', source && displayed.length === 1); area.dataset.paneCount = displayed.length;
    var preset = presets.find(function (item) { return item.id === current; });
    /* 四段直选的激活态：源码/阅读/导图各对应一个预设，其余（即时编辑与组合视图）落在"更多"。 */
    var segment = ({ source: 'source', read: 'read', map: 'map' })[current] || 'more';
    viewSwitch.querySelectorAll('[data-view-link]').forEach(function (button) {
      button.setAttribute('aria-pressed', String(button.dataset.viewLink === segment));
    });
    /* 非三段直选时"更多"段显示当前布局名并高亮（与原生底栏 MoreSegmentLabel 一致）。 */
    layoutButton.setAttribute('aria-pressed', String(segment === 'more'));
    var moreLabel = segment === 'more' ? preset.title : '更多';
    if (layoutButton.dataset.moreLabel !== moreLabel) {
      layoutButton.dataset.moreLabel = moreLabel;
      layoutButton.firstChild.textContent = moreLabel;
    }
    layoutButton.setAttribute('aria-label', '当前视图：' + preset.title + '，点击更换');
    /* 状态栏已移除联动入口（usability.js 注明），这里只在旧布局仍提供该容器时同步可见性。 */
    if ($('view-follow-controls')) $('view-follow-controls').hidden = displayed.length < 2;
    document.body.classList.toggle('workspace-multiple', displayed.length > 1);
    if (W.syncStructure) W.syncStructure();
    Object.values(panels).forEach(function (panel) { panel.classList.remove('active-view'); });
    panels[displayed.includes('editor') ? 'editor' : displayed[0]].classList.add('active-view');
    layoutMenu.querySelectorAll('[data-layout]').forEach(function (button) { button.setAttribute('aria-pressed', String(button.dataset.layout === current)); });
    document.dispatchEvent(new CustomEvent('vex-layout-rendered'));
    // Newly opened panes inherit the current chapter position instead of starting at the top.
    requestAnimationFrame(function () { if (W.syncViewsToLine) W.syncViewsToLine(); });
  }
  function setLayout(id) {
    if (id === 'source-read') id = 'source-render';
    var preset = presets.find(function (entry) { return entry.id === id; }); if (!preset) return;
    var hadMap = tree && leaves(tree).includes('map');
    W.finish(); $('node-inspector').hidden = true; current = id; tree = clone(preset.tree); if (preset.mode) editorMode = preset.mode;
    var wanted = leaves(tree); changing = true;
    if (wanted.includes('editor')) W.setEditMode(editorMode); else W.setView(wanted.includes('map') ? 'mindmap' : 'read');
    changing = false; renderLayout(); saveLayout();
    if (id === 'three' && innerWidth < 1250 && W.state.sidebar) {
      VexProto.handleAction('toggle-sidebar');
      VexProto.toastKey('ToastFileNavCollapsed');
    }
    if (wanted.includes('map') && (!hadMap || id === 'three')) requestAnimationFrame(function () { requestAnimationFrame(function () { W.fit(id !== 'three'); }); });
  }
  function openPanel(id) { if (leaves(tree).includes(id)) return; var wanted = new Set(leaves(tree).concat(id)); setLayout(wanted.size === 3 ? 'three' : wanted.has('map') ? (editorMode === 'source' ? 'source-map' : 'instant-map') : 'source-render'); }
  function switchEditorMode(mode) { var hasMap = leaves(tree).includes('map'), hasRender = leaves(tree).includes('render'); setLayout(mode === 'instant' ? hasMap ? 'instant-map' : 'instant' : hasMap ? hasRender ? 'three' : 'source-map' : hasRender ? 'source-render' : 'source'); }
  function locateEditor(line) { editorMode = 'source'; changing = true; W.setEditMode('source'); changing = false; openPanel('editor'); renderLayout(); saveLayout(); W.focusLine(line); }
  function navigate(line) { if (leaves(tree).includes('editor')) W.focusLine(line); W.syncViewsToLine(line); }
  function handleAction(action) {
    if (window.VexAuxiliary && VexAuxiliary.handleAction(action)) return true;
    if(action==='map-appearance'){if(!leaves(tree).includes('map'))openPanel('map');mapAppearance.toggle(mapButton);return true;}
    if(action==='typography'){mapAppearance.close();if(typeButton.hidden&&readingTypeButton.hidden)return true;}
    if (action === 'undo' || action === 'redo') { W.history(action); return true; }
    if (action === 'map-source') { setLayout(leaves(tree).includes('editor') ? 'map' : 'source-map'); return true; }
    if (action === 'locate-source') { var node = W.state.nodes.find(function (candidate) { return candidate.id === W.state.selected; }); if (node) locateEditor(node.line); return true; }
    if (action === 'map-settings' && !$('map-pane').hidden) { var open = !$('map-settings').hidden; $('map-settings').hidden = open; $('map-settings-button').setAttribute('aria-expanded', String(!open)); $('node-inspector').hidden = true; return true; }
    return false;
  }
  window.VexWorkspace = { setLayout: setLayout, openPanel: openPanel, handleAction: handleAction, locateEditor: locateEditor, navigate: navigate, closeAppearance:function(){mapAppearance.close();}, get current() { return current; }, get tree() { return clone(tree); }, get editorMode() { return editorMode; } };
  window.addEventListener('vex-view-changed', function (event) { if (changing) return; var view = event.detail.view, mode = event.detail.editMode; if (view === 'edit' && mode !== editorMode && leaves(tree).includes('editor')) switchEditorMode(mode); else setLayout(view === 'edit' ? mode : view === 'read' ? 'read' : 'map'); });
  document.addEventListener('keydown', function (event) { if (event.key === 'Escape' && !layoutMenu.hidden) { event.preventDefault(); closeMenus(true); } if (event.isComposing || !(event.ctrlKey || event.metaKey) || !['z', 'y'].includes(event.key.toLowerCase())) return; var field = event.target.closest('input, textarea, [contenteditable="true"]'); if (field && field.id !== 'markdown-input' && !field.classList.contains('instant-block')) return; event.preventDefault(); W.finish(); handleAction(event.key.toLowerCase() === 'y' || event.shiftKey ? 'redo' : 'undo'); }, true);
  document.addEventListener('pointerdown', function (event) { if (!layoutMenu.contains(event.target) && !layoutButton.contains(event.target) && !event.target.closest('.tour-card')) closeMenus(); });
  var beginGuide = VexProto.beginGuideSession, endGuide = VexProto.endGuideSession;
  VexProto.beginGuideSession = function () { var layout = { current: current, tree: clone(tree), mode: editorMode, mapStyle: mapAppearance.style }; guideActive = true; var result = beginGuide(); result.workspace = layout; return result; };
  VexProto.endGuideSession = function (snapshot) { endGuide(snapshot); if (snapshot?.workspace) { editorMode = snapshot.workspace.mode; tree = snapshot.workspace.tree; current = snapshot.workspace.current; mapAppearance.choose(snapshot.workspace.mapStyle); renderLayout(); } guideActive = false; requestAnimationFrame(function () { requestAnimationFrame(saveLayout); }); };
  var savedLayout; try { savedLayout = JSON.parse(VexStorage.getItem('vex-design-layout') || 'null'); } catch (_) {}
  var requested = query.get('layout'); if (requested === 'source-read') requested = 'source-render';
  if (requested && presets.some(function (preset) { return preset.id === requested; })) setLayout(requested);
  else if (query.has('view') || query.has('edit')) setLayout(W.state.view === 'mindmap' ? 'map' : W.state.view === 'read' ? 'read' : W.state.editMode);
  else if (savedLayout && presets.some(function (preset) { return preset.id === savedLayout.current; })) setLayout(savedLayout.current);
  else setLayout(editorMode);
})();
