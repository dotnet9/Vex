/* 单文档原型：固定视图组合；所有视图与独立窗口跟随当前文件。 */
(function () {
  'use strict';
  var W = window.VexWorkbench, $ = function (id) { return document.getElementById(id); };
  var query = new URLSearchParams(location.search), detached = query.get('panel');
  var token = query.get('session') || crypto.randomUUID(), receiving = false, changing = false;
  var current = 'instant', editorMode = W.state.editMode, tree;
  var windows = new Map(), revision = 0, remoteRevision = 0, documentEpoch = 0, remoteEpoch = 0;
  var lastPublished = '', lastRemote = '', connected = false, guideActive = false;
  var panels = {}, area = document.querySelector('.content-area');
  var labels = { editor: '编辑', render: '阅读', map: '思维导图' };
  var icons = { editor: 'edit', render: 'eye', map: 'map', files: 'folder', outline: 'list' };
  var presets = [
    { id: 'instant', title: '即时编辑', note: '专注连续写作', mode: 'instant', tree: 'editor' },
    { id: 'source', title: '源码', note: '精确编辑 Markdown', mode: 'source', tree: 'editor' },
    { id: 'read', title: '阅读', note: '查看完整正文', tree: 'render' },
    { id: 'map', title: '思维导图', note: '先梳理章节结构', tree: 'map' },
    { id: 'source-render', title: '源码 + 渲染', note: '边写边检查排版', mode: 'source', tree: ['horizontal', 'editor', 'render'] },
    { id: 'source-map', title: '源码 + 导图', note: '用 Markdown 搭建结构', mode: 'source', tree: ['horizontal', 'editor', 'map'] },
    { id: 'instant-map', title: '即时编辑 + 导图', note: '写正文，也看全局', mode: 'instant', tree: ['horizontal', 'editor', 'map'] },
    { id: 'three', title: '源码 + 渲染 + 导图', note: '上方写作，下方梳理结构', mode: 'source', tree: ['vertical', ['horizontal', 'editor', 'render'], 'map'] }
  ];
  function icon(name) { return '<svg class="ico" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; }
  function esc(value) { return String(value).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
  function clone(value) { return JSON.parse(JSON.stringify(value)); }
  function leaves(node) { return !node ? [] : typeof node === 'string' ? [node] : leaves(node[1]).concat(leaves(node[2])); }
  function saveLayout() {
    if (detached || guideActive || W.state.guideRestoring) return;
    try { localStorage.setItem('vex-design-layout', JSON.stringify({ current: current, mode: editorMode })); } catch (_) {}
  }
  function makePanel(id, sections) {
    var panel = document.createElement('section'); panel.className = 'view-panel'; panel.dataset.document = id; panel.setAttribute('aria-label', labels[id]);
    var head = document.createElement('div'); head.className = 'view-heading';
    head.innerHTML = '<span class="view-caption">' + icon(icons[id]) + '<span class="view-title"></span></span><button class="icon-button" data-float-view="' + id + '" title="在独立窗口中打开" aria-label="独立打开' + labels[id] + '">' + icon('ext') + '</button>';
    head.querySelector('button').onclick = function () { floatPanel(id); };
    var body = document.createElement('div'); body.className = 'view-body';
    sections.forEach(function (section) { body.appendChild($(section)); });
    panel.append(head, body); panels[id] = panel; area.appendChild(panel); return panel;
  }
  makePanel('editor', ['source-pane', 'instant-pane']);
  makePanel('render', ['reading-pane']); makePanel('map', ['map-pane']);
  var parking = document.createElement('div'); parking.hidden = true; parking.id = 'view-parking'; area.appendChild(parking);
  var surface = document.createElement('div'); surface.className = 'view-surface'; surface.id = 'view-surface'; area.appendChild(surface);
  var empty = document.createElement('div'); empty.className = 'view-empty'; empty.hidden = true;
  empty.innerHTML = icon('columns') + '<h2>给思考留一点空间</h2><p>视图已在独立窗口中打开，可移到其他屏幕使用。</p><button class="btn" id="empty-open-view">打开视图</button>';
  area.appendChild(empty);
  var workbar = document.querySelector('.workbar'), actions = document.querySelector('.work-actions');
  var layoutButton = document.createElement('button'); layoutButton.className = 'workspace-layout-button'; layoutButton.id = 'workspace-layout';
  layoutButton.setAttribute('aria-haspopup', 'dialog'); layoutButton.setAttribute('aria-expanded', 'false');
  layoutButton.innerHTML = icon('columns') + '<span>布局</span><span class="layout-chevron">⌄</span>';
  workbar.insertBefore(layoutButton, actions);
  var layoutMenu = document.createElement('section'); layoutMenu.className = 'workspace-layout-menu'; layoutMenu.id = 'workspace-layout-menu'; layoutMenu.hidden = true;
  layoutMenu.setAttribute('role', 'dialog'); layoutMenu.setAttribute('aria-label', '工作区布局');
  layoutMenu.innerHTML = '<div class="workspace-menu-heading"><strong>视图组合</strong><button class="icon-button" aria-label="关闭布局菜单">' + icon('x') + '</button></div>'
    + '<p>当前只打开一份文档，切换文件时所有视图一起更新。</p><div class="layout-preset-grid"></div>'
    + '<div class="workspace-layout-footer"><button id="separate-screens">' + icon('ext') + '分到独立窗口</button><button id="reset-workspace">默认视图</button></div>';
  document.querySelector('.window-body').appendChild(layoutMenu);
  function miniature(node) { return typeof node === 'string' ? '<i class="mini-pane mini-' + node + '"></i>' : '<span class="mini-split mini-' + node[0] + '">' + miniature(node[1]) + miniature(node[2]) + '</span>'; }
  presets.forEach(function (preset) {
    var button = document.createElement('button'); button.className = 'layout-preset'; button.dataset.layout = preset.id;
    button.innerHTML = '<span class="layout-miniature">' + miniature(preset.tree) + '</span><strong>' + preset.title + '</strong><small>' + preset.note + '</small><span class="preset-check">✓</span>';
    button.onclick = function () { setLayout(preset.id); };
    layoutMenu.querySelector('.layout-preset-grid').appendChild(button);
  });
  function showLayouts() {
    var show = layoutMenu.hidden; closeMenus(); layoutMenu.hidden = !show;
    layoutButton.setAttribute('aria-expanded', String(show));
    if (show) layoutMenu.querySelector('[data-layout="' + current + '"]')?.focus();
  }
  layoutButton.onclick = showLayouts; $('empty-open-view').onclick = showLayouts;
  layoutMenu.querySelector('.workspace-menu-heading button').onclick = closeMenus;
  $('reset-workspace').onclick = function () { setLayout('instant'); };
  $('separate-screens').onclick = function () { closeMenus(); leaves(tree).filter(function (id) { return id !== 'editor'; }).forEach(floatPanel); };
  var shelf = document.createElement('div'); shelf.className = 'workspace-window-shelf'; shelf.hidden = true; shelf.id = 'workspace-window-shelf';
  document.querySelector('.workspace-main').insertBefore(shelf, $('edit-options'));
  function closeMenus() { layoutMenu.hidden = true; layoutButton.setAttribute('aria-expanded', 'false'); }
  function setLayout(id) {
    var preset = presets.find(function (entry) { return entry.id === id; }); if (!preset) return;
    W.finish(); current = id; tree = clone(preset.tree);
    if (preset.mode) editorMode = preset.mode;
    var wanted = leaves(tree);
    windows.forEach(function (popup, panel) { if (panels[panel] && !wanted.includes(panel)) { windows.delete(panel); popup.close(); } });
    changing = true;
    if (wanted.includes('editor')) W.setEditMode(editorMode);
    else W.setView(wanted.includes('map') ? 'mindmap' : 'read');
    changing = false; renderLayout(); saveLayout(); publish(true);
    if (wanted.includes('map')) requestAnimationFrame(function () { W.fit(true); });
  }
  function localTree(node) {
    if (typeof node === 'string') return windows.has(node) ? null : node;
    if (!node) return null;
    var a = localTree(node[1]), b = localTree(node[2]); return !a ? b : !b ? a : [node[0], a, b];
  }
  function build(node) {
    if (typeof node === 'string') return panels[node];
    var split = document.createElement('div'); split.className = 'view-split view-' + node[0];
    split.append(build(node[1]), build(node[2])); return split;
  }
  function renderLayout() {
    if (!detached) {
      var matchingPreset = presets.find(function (preset) { return JSON.stringify(preset.tree) === JSON.stringify(tree) && (!preset.mode || preset.mode === editorMode); });
      current = matchingPreset ? matchingPreset.id : current;
    }
    var source = editorMode === 'source', displayed = localTree(tree);
    Object.values(panels).forEach(function (panel) { panel.style.flex = ''; parking.appendChild(panel); });
    surface.replaceChildren(); if (displayed) surface.appendChild(build(displayed, 'root'));
    surface.hidden = !displayed; empty.hidden = !!displayed;
    $('source-pane').hidden = !source; $('instant-pane').hidden = source;
    $('reading-pane').hidden = false; $('map-pane').hidden = false;
    // hidden 放在原视图上，旧的菜单、引导和辅助技术仍能准确判断可见视图。
    if (!leaves(displayed).includes('editor')) { $('source-pane').hidden = true; $('instant-pane').hidden = true; }
    if (!leaves(displayed).includes('render')) $('reading-pane').hidden = true;
    if (!leaves(displayed).includes('map')) $('map-pane').hidden = true;
    panels.editor.querySelector('.view-title').textContent = source ? '源码' : '即时编辑';
    if (detached) {
      var detachedTitle = detached === 'editor' ? source ? '源码' : '即时编辑' : labels[detached] || '视图';
      $('detached-view-name').textContent = detachedTitle;
      document.title = W.name + ' · ' + detachedTitle + ' · Vex';
    }
    panels.render.querySelector('.view-title').textContent = '阅读'; panels.map.querySelector('.view-title').textContent = labels.map;
    surface.dataset.single = String(leaves(displayed).length === 1);
    $('edit-options').hidden = !leaves(displayed).includes('editor');
    $('cursor-position').hidden = !leaves(displayed).includes('editor');
    W.state.source = leaves(displayed).includes('editor') && source;
    $('map-source-toggle').setAttribute('aria-pressed', String(W.state.source)); $('map-source-toggle').classList.toggle('on', W.state.source);
    document.querySelector('.workspace').classList.toggle('source-only', source && leaves(displayed).length === 1);
    area.dataset.paneCount = leaves(displayed).length;
    $('status-view').textContent = leaves(displayed).map(function (id) { return id === 'editor' ? source ? '源码' : '即时编辑' : labels[id]; }).join(' + ');
    if (!displayed) $('status-view').textContent = '视图已移到独立窗口';
    var single = leaves(tree).length === 1 ? leaves(tree)[0] : null;
    var quickView = single === 'editor' ? 'edit' : single === 'render' ? 'read' : single === 'map' ? 'mindmap' : 'custom';
    var quickSwitch = document.querySelector('.view-switch'); quickSwitch.dataset.active = quickView;
    quickSwitch.setAttribute('aria-label', '单视图快捷切换');
    quickSwitch.querySelectorAll('button').forEach(function (button) { button.setAttribute('aria-selected', String(button.dataset.view === quickView)); button.title = '单独打开' + button.textContent.trim() + '视图'; button.tabIndex = button.dataset.view === quickView || quickView === 'custom' && button.dataset.view === 'edit' ? 0 : -1; });
    layoutMenu.querySelectorAll('[data-layout]').forEach(function (button) { button.setAttribute('aria-pressed', String(button.dataset.layout === current)); });
    shelf.innerHTML = '';
    windows.forEach(function (_, id) { var button = document.createElement('button'); button.innerHTML = icon(icons[id]) + esc(id === 'editor' ? source ? '源码' : '即时编辑' : labels[id]) + '<span>独立窗口 ↗</span><b>移回</b>'; button.onclick = function () { returnPanel(id); }; shelf.appendChild(button); });
    shelf.hidden = !windows.size;
    document.dispatchEvent(new CustomEvent('vex-layout-rendered'));
  }
  function openPanel(id) {
    if (windows.has(id)) { windows.get(id).focus(); return; }
    if (leaves(tree).includes(id)) return;
    var wanted = new Set(leaves(tree).concat(id));
    setLayout(wanted.size === 3 ? 'three' : wanted.has('map') ? (editorMode === 'source' ? 'source-map' : 'instant-map') : 'source-render');
  }
  function switchEditorMode(mode) {
    var hasMap = leaves(tree).includes('map'), hasRender = leaves(tree).includes('render');
    setLayout(mode === 'instant' ? hasMap ? 'instant-map' : 'instant' : hasMap ? hasRender ? 'three' : 'source-map' : hasRender ? 'source-render' : 'source');
  }
  function floatPanel(id) {
    if (detached || !panels[id]) return;
    if (windows.has(id)) { windows.get(id).focus(); return; }
    W.finish(); var url = new URL(location.href); url.search = ''; url.hash = '';
    url.searchParams.set('panel', id); url.searchParams.set('session', token);
    url.searchParams.set('theme', document.documentElement.dataset.themeMode || 'system'); url.searchParams.set('palette', document.documentElement.dataset.palette || 'aurora');
    var popup = window.open(url.href, 'vex-' + token + '-' + id, 'popup=yes,width=' + 920 + ',height=740');
    if (!popup) { VexProto.toast('浏览器拦截了独立窗口，请允许此页面弹出窗口后重试。'); return; }
    windows.set(id, popup);
    renderLayout(); publish(true);
  }
  function returnPanel(id) {
    var popup = windows.get(id); windows.delete(id); if (popup && !popup.closed) popup.close();
    if (!leaves(tree).includes(id)) openPanel(id);
    renderLayout();
  }
  function snapshot() { return Object.assign(W.snapshot(), { revision: revision, epoch: documentEpoch, editorMode: editorMode, files: W.documents.map(function (doc) { return doc.name; }), theme: { mode: document.documentElement.dataset.themeMode, palette: document.documentElement.dataset.palette }, typography: clone(VexTypography.state) }); }
  function send(popup, type, data) { if (popup && !popup.closed) popup.postMessage({ vexWorkspace: true, token: token, type: type, data: data }, '*'); }
  function publish(force) {
    if (receiving) return;
    if (detached) {
      if (!window.opener || !lastRemote) return;
      var signature = W.name + '\n' + W.text;
      if (signature !== lastRemote) { lastRemote = signature; send(window.opener, 'edit', { text: W.text, revision: remoteRevision, epoch: remoteEpoch }); }
      return;
    }
    var signature = W.name + '\n' + W.text;
    if (signature !== lastPublished) { lastPublished = signature; revision++; force = true; }
    if (force) windows.forEach(function (popup) { send(popup, 'snapshot', snapshot()); });
  }
  function remoteCommand(action, data) { send(window.opener, action, data); }
  window.addEventListener('message', function (event) {
    var message = event.data;
    if (!message || !message.vexWorkspace || message.token !== token) return;
    if (detached) {
      if (event.source !== window.opener) return;
      if (message.type === 'snapshot') {
        var data = message.data;
        if (data.revision < remoteRevision) return;
        receiving = true; remoteRevision = data.revision; remoteEpoch = data.epoch;
        var changedMode = editorMode !== data.editorMode;
        lastRemote = data.name + '\n' + data.text; W.receive(data); editorMode = data.editorMode;
        if (data.theme) { VexTheme.setMode(data.theme.mode); VexTheme.setPalette(data.theme.palette); }
        if (data.typography) { VexTypography.choose(data.typography.key); if (VexTypography.state.compact !== data.typography.compact) VexTypography.toggleCompact(); }
        $('detached-file-name').textContent = data.name;
        document.title = data.name + ' · ' + (detached === 'editor' ? editorMode === 'source' ? '源码' : '即时编辑' : labels[detached]) + ' · Vex';
        if (detached === 'editor' && (!connected || changedMode)) { changing = true; W.setEditMode(editorMode); changing = false; }
        if (!connected || changedMode) renderLayout(); connected = true; receiving = false;
      }
      if (message.type === 'locate') W.focusLine(message.data);
      return;
    }
    var panel = Array.from(windows.keys()).find(function (id) { return windows.get(id) === event.source; }); if (!panel) return;
    if (message.type === 'ready') send(event.source, 'snapshot', snapshot());
    if (message.type === 'edit') {
      if (message.data.epoch !== documentEpoch || message.data.revision !== revision) { send(event.source, 'snapshot', snapshot()); return; }
      W.applyText(message.data.text); publish(true);
    }
    if (message.type === 'history') { W.history(message.data); publish(true); }
    if (message.type === 'return') returnPanel(panel);
    if (message.type === 'save') { VexProto.handleAction('save'); publish(true); }
    if (message.type === 'locate-editor') locateEditor(message.data);
    if (message.type === 'editor-mode' && panel === 'editor' && ['source', 'instant'].includes(message.data)) {
      switchEditorMode(message.data);
    }
  });
  function locateEditor(line) {
    editorMode = 'source'; changing = true; W.setEditMode('source'); changing = false;
    if (windows.has('editor')) { publish(true); send(windows.get('editor'), 'locate', line); windows.get('editor').focus(); return; }
    openPanel('editor');
    renderLayout(); saveLayout(); publish(true);
    var node = W.state.nodes.find(function (candidate) { return candidate.line === line; }); if (node) {
      if (editorMode === 'source') W.highlightSource(node, true); else W.locate(node);
    }
  }
  function navigate(line) {
    if (detached) { remoteCommand('locate-editor', line); return; }
    if (windows.has('editor')) { send(windows.get('editor'), 'locate', line); windows.get('editor').focus(); }
    else if (leaves(tree).includes('editor')) W.focusLine(line);
    else if (leaves(tree).includes('render')) {
      var block = $('markdown-preview').querySelector('[data-line="' + line + '"]'); block?.scrollIntoView({ block: 'start', behavior: 'smooth' });
    }
  }
  function handleAction(action) {
    if (action === 'undo' || action === 'redo') { if (detached) remoteCommand('history', action); else W.history(action); return true; }
    if (action === 'save' && detached) { W.finish(); publish(); remoteCommand('save'); return true; }
    if (action === 'close' && detached) { remoteCommand('return'); window.close(); return true; }
    if (action === 'map-source') { if (detached) remoteCommand('locate-editor', 1); else setLayout(leaves(tree).includes('editor') ? 'map' : 'source-map'); return true; }
    if (action === 'locate-source') { var node = W.state.nodes.find(function (candidate) { return candidate.id === W.state.selected; }); if (node) { if (detached) remoteCommand('locate-editor', node.line); else locateEditor(node.line); } return true; }
    if (action === 'map-settings' && !$('map-pane').hidden) {
      var open = !$('map-settings').hidden; $('map-settings').hidden = open; $('map-settings-button').setAttribute('aria-expanded', String(!open)); $('node-inspector').hidden = true; return true;
    }
    return false;
  }
  window.VexWorkspace = { setLayout: setLayout, openPanel: openPanel, floatPanel: floatPanel, returnPanel: returnPanel, handleAction: handleAction, locateEditor: locateEditor, navigate: navigate,
    get current() { return current; }, get tree() { return clone(tree); }, get detached() { return detached; },
    get editorMode() { return editorMode; }, get floating() { return Array.from(windows.keys()); } };
  window.addEventListener('vex-view-changed', function (event) {
    if (changing) return;
    if (detached) {
      if (detached === 'editor' && editorMode !== event.detail.editMode) { editorMode = event.detail.editMode; if (!receiving) remoteCommand('editor-mode', editorMode); }
      renderLayout(); return;
    }
    var view = event.detail.view, mode = event.detail.editMode;
    // 编辑方式保持固定组合；即时编辑自带排版，保留导图即可。
    if (view === 'edit' && mode !== editorMode && leaves(tree).includes('editor')) {
      switchEditorMode(mode);
    } else setLayout(view === 'edit' ? mode : view === 'read' ? 'read' : 'map');
  });
  window.addEventListener('vex-content-changed', function () { publish(); });
  window.addEventListener('vex-document-loaded', function () { if (!detached) { documentEpoch++; publish(true); } });
  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') closeMenus();
    if (event.isComposing || !(event.ctrlKey || event.metaKey) || !['z', 'y'].includes(event.key.toLowerCase())) return;
    var field = event.target.closest('input, textarea, [contenteditable="true"]');
    if (field && field.id !== 'markdown-input' && !field.classList.contains('instant-block')) return;
    event.preventDefault(); W.finish(); handleAction(event.key.toLowerCase() === 'y' || event.shiftKey ? 'redo' : 'undo');
  });
  document.addEventListener('pointerdown', function (event) { if (!layoutMenu.contains(event.target) && !layoutButton.contains(event.target)) closeMenus(); });
  document.addEventListener('input', function (event) { if (detached && (event.target.id === 'markdown-input' || event.target.closest('.instant-input'))) queueMicrotask(function () { publish(); }); });
  var beginGuide = VexProto.beginGuideSession, endGuide = VexProto.endGuideSession;
  VexProto.beginGuideSession = function () { var layout = { current: current, tree: clone(tree), mode: editorMode }; guideActive = true; var result = beginGuide(); result.workspace = layout; return result; };
  VexProto.endGuideSession = function (snapshot) { endGuide(snapshot); if (snapshot?.workspace) { editorMode = snapshot.workspace.mode; tree = snapshot.workspace.tree; current = snapshot.workspace.current; renderLayout(); } guideActive = false; requestAnimationFrame(function () { requestAnimationFrame(saveLayout); }); };
  if (detached) {
    document.body.classList.add('detached-window'); document.body.dataset.detachedPanel = detached;
    var header = document.createElement('div'); header.className = 'detached-header';
    header.innerHTML = '<div>' + icon(icons[detached] || 'file') + '<strong id="detached-view-name">' + (labels[detached] || '视图') + '</strong><span id="detached-file-name">正在连接当前文档…</span></div><button class="btn sm" id="return-to-workspace">' + icon('columns') + '移回主窗口</button>';
    document.querySelector('.window-body').prepend(header);
    $('return-to-workspace').onclick = function () { remoteCommand('return'); window.close(); };
    tree = panels[detached] ? detached : null; current = 'detached';
    renderLayout(); remoteCommand('ready');
    if (!window.opener) { $('detached-file-name').textContent = '请从主窗口打开此视图'; }
  } else {
    var savedLayout;
    try { savedLayout = JSON.parse(localStorage.getItem('vex-design-layout') || 'null'); } catch (_) {}
    var requested = query.get('layout');
    if (requested && presets.some(function (preset) { return preset.id === requested; })) setLayout(requested);
    else if (query.has('view') || query.has('edit')) setLayout(W.state.view === 'mindmap' ? 'map' : W.state.view === 'read' ? 'read' : W.state.editMode);
    else if (savedLayout && presets.some(function (preset) { return preset.id === savedLayout.current; })) setLayout(savedLayout.current);
    else setLayout(editorMode);
    setInterval(function () { var closed = []; windows.forEach(function (popup, id) { if (popup.closed) closed.push(id); }); closed.forEach(returnPanel); }, 750);
    window.addEventListener('beforeunload', function () { windows.forEach(function (popup) { if (!popup.closed) popup.close(); }); });
  }
})();
