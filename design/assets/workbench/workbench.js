(function () {
  'use strict';
  var $ = function (id) { return document.getElementById(id); }, MM = window.VexMindMap;
  // 默认提供演示文档，评审参数覆盖首次使用与真实文档的边界场景。
  var docs = VexSamples.map(function (doc) { return Object.assign({}, doc); });
  var builtin = window.VexDocuments?.[new URLSearchParams(location.search).get('document')];
  var launched = VexDocumentWindow.read();
  if (launched) docs = [launched];
  else if (builtin) docs = [{name:builtin.name,text:builtin.text,readOnly:false}];
  var bundledDocument = !!builtin || !!launched?.builtin;
  var workspaceFolder = bundledDocument ? '内置文档' : '演示文档';
  var scenario = new URLSearchParams(location.search).get('scenario');
  if (!builtin && !launched) {
    if (scenario === 'empty' || scenario === 'new') {
      docs = [{ name: '未命名文档.md', text: '', isNew: true, placeholder: scenario === 'empty' }]; workspaceFolder = '未打开文件夹';
    } else if (scenario === 'no-headings') {
      docs = [{ name: '还没有标题的想法.md', text: '今天先把想法写下来，不急着整理成章节。\n\n这篇文章没有标题。正文依然可以编辑、查找和保存副本；需要整理时，再添加标题。' }];
    } else if (scenario === 'long-document') {
      docs = [{ name: '长文写作与章节定位.md', text: '# 长文写作与章节定位\n\n' + Array.from({ length: 40 }, function (_, i) { return '## 第 ' + (i + 1) + ' 章：观察与思考\n\n' + Array.from({ length: 4 }, function (_, j) { return '第 ' + (j + 1) + ' 段。先记录观察，再补充依据，最后形成判断。长文需要在不同章节之间查找、对照，并随时返回正在编辑的位置。'; }).join('\n\n'); }).join('\n\n') }];
    } else if (scenario === 'long-files') {
      docs = [
        { name: '关于长期写作习惯与知识整理工作流程的观察和改进建议.md', path: '项目资料/用户研究/访谈整理/第一轮/关于长期写作习惯与知识整理工作流程的观察和改进建议.md', text: '# 长文件名与深目录\n\n悬停文件名查看完整路径。拖动导航边界可以扩展文件列表。' },
        { name: '访谈记录.md', path: '项目资料/用户研究/访谈整理/第二轮/访谈记录.md', text: '# 第二轮访谈\n\n同名文件通过目录区分。' },
        { name: '访谈记录.md', path: '归档/第一轮/访谈记录.md', text: '# 第一轮访谈\n\n这份文件来自另一条目录。' }
      ]; workspaceFolder = '写作项目';
    }
  }
  var storedFileListMode = VexStorage.getItem('vex-design-file-list-mode'), fileListMode = bundledDocument || storedFileListMode === 'list' ? 'list' : 'tree', fileDescending = false;
  var alphaSort = 'default'; /* default | asc | desc，ASCII 序；default 跟随分类（树形） */
  var outlineFolded = false;
  function syncFilesToolbar() {
    var mode = document.querySelector('[data-action="files-mode"]');
    var sort = document.querySelector('[data-action="files-sort"]');
    var fold = document.querySelector('[data-action="files-fold"]');
    if (mode) mode.setAttribute('aria-pressed', String(fileListMode === 'tree' && alphaSort === 'default'));
    if (sort) {
      var arrow = sort.querySelector('.sort-arrow');
      if (arrow) { arrow.textContent = alphaSort === 'asc' ? '↑' : alphaSort === 'desc' ? '↓' : '↑↓'; arrow.hidden = false; }
      sort.setAttribute('aria-pressed', String(alphaSort !== 'default'));
      sort.title = VexL.t(alphaSort === 'asc' ? '按名称排序：升序' : alphaSort === 'desc' ? '按名称排序：降序' : '按名称排序：默认');
    }
    if (fold) {
      var anyOpen = document.querySelectorAll('#file-list details.file-directory[open]').length > 0;
      fold.setAttribute('aria-pressed', String(fileListMode === 'tree' && !anyOpen));
      fold.title = VexL.t(anyOpen ? '全部折叠' : '全部展开');
    }
  }
  var settings = { length: 30, paragraphs: true, lists: true, blocks: false, layout: 'right' };
  try {
    var saved = JSON.parse(VexStorage.getItem('vex-design-mindmap') || '{}');
    if (Number.isInteger(saved.length) && saved.length >= 1 && saved.length <= 120) settings.length = saved.length;
    ['paragraphs', 'lists', 'blocks'].forEach(function (key) { if (typeof saved[key] === 'boolean') settings[key] = saved[key]; });
    if (VexMapLayout.valid(saved.layout)) settings.layout = saved.layout;
  } catch (_) {}
  var editMode = 'instant';
  try { if (VexStorage.getItem('vex-design-edit-method') === 'source') editMode = 'source'; } catch (_) {}
  var requestedEdit = new URLSearchParams(location.search).get('edit');
  if (['instant', 'source'].includes(requestedEdit)) editMode = requestedEdit;
  var state = { view: 'edit', editMode: editMode, autoPair: true, focus: false, typewriter: false, motion: true, topmost: false, language: 'zh-CN', undo: [], redo: [], sidebar: true, source: false, doc: 0, selected: null, collapsed: new Set(), scale: 1, presentation: false, presentationScale: 1.5, x: 0, y: 0, dirty: false, parsed: null, nodes: [], pending: null, followViews: true, editingLine: 1, emptyWorkspace: !!docs[0].placeholder, failNextSave: scenario === 'save-error', failNextExport: scenario === 'export-error' };
  // 原型只持久化隔离的演示宽度，不接触宿主应用设置。
  var sidebar = document.querySelector('.sidebar'), splitter = $('sidebar-splitter'), workspace = document.querySelector('.workspace'), sidebarWidth = 226;
  try { var storedWidth = Number(VexStorage.getItem('vex-design-sidebar-width')); if (storedWidth >= 200 && storedWidth <= 560) sidebarWidth = storedWidth; } catch (_) {}
  function resizeSidebar(width, persist) {
    var available = document.querySelector('.workspace').clientWidth;
    var maximum = Math.min(560, Math.max(200, available - 365));
    sidebarWidth = Math.max(200, Math.min(maximum, width));
    sidebar.style.setProperty('--sidebar-width', sidebarWidth + 'px');
    // Keep the visible edge tied to the pointer even while the layout is busy reflowing.
    sidebar.style.width = sidebarWidth + 'px';
    splitter.setAttribute('aria-valuenow', Math.round(sidebarWidth));
    if (persist) try { VexStorage.setItem('vex-design-sidebar-width', String(sidebarWidth)); } catch (_) {}
  }
  resizeSidebar(sidebarWidth, false);
  var sidebarDrag;
  splitter.addEventListener('pointerdown', function (event) {
    if (event.button !== 0) return; event.preventDefault();
    var current = sidebar.getBoundingClientRect(), host = workspace.getBoundingClientRect();
    sidebarDrag = { offset: event.clientX - current.right, hostLeft: host.left };
    splitter.setPointerCapture(event.pointerId); document.body.classList.add('sidebar-resizing');
  });
  splitter.addEventListener('pointermove', function (event) {
    if (!sidebarDrag) return;
    var samples = event.getCoalescedEvents ? event.getCoalescedEvents() : [event], latest = samples[samples.length - 1];
    resizeSidebar(latest.clientX - sidebarDrag.hostLeft - sidebarDrag.offset, false);
  });
  ['pointerup', 'pointercancel', 'lostpointercapture'].forEach(function (name) {
    splitter.addEventListener(name, function () { if (!sidebarDrag) return; sidebarDrag = null; document.body.classList.remove('sidebar-resizing'); resizeSidebar(sidebarWidth, true); });
  });
  splitter.addEventListener('keydown', function (event) {
    var steps = { ArrowLeft: -10, ArrowRight: 10, PageDown: -40, PageUp: 40 };
    if (event.key in steps) { event.preventDefault(); resizeSidebar(sidebarWidth + steps[event.key], true); }
    else if (event.key === 'Home') { event.preventDefault(); resizeSidebar(200, true); }
    else if (event.key === 'End') { event.preventDefault(); resizeSidebar(560, true); }
  });


  // Right-side outline splitter: independent width, keyboard accessible, persisted in prototype storage.
  var structureSidebar = $('structure-sidebar'), structureSplitter = $('structure-splitter');
  var structureWidth = 312, structureInner = structureSidebar.querySelector('.structure-inner');
  var structurePreferred = VexStorage.getItem('vex-design-structure') === '1';
  function syncStructureVisibility() {
    var narrow = workspace.clientWidth - (state.sidebar ? sidebarWidth : 0) - structureWidth < 640;
    var crowded = window.VexWorkspace?.current === 'three' && workspace.clientWidth < 1400;
    var expanded = structurePreferred;
    workspace.classList.toggle('auxiliary-overlay', narrow || crowded);
    structureSidebar.classList.toggle('collapsed', !expanded);
    structureSidebar.inert = !expanded;
    structureSplitter.hidden = !expanded || narrow || crowded;
    var toggle = structureSidebar.querySelector('.structure-toggle');
    if (toggle) {
      toggle.setAttribute('aria-expanded', String(expanded));
      toggle.title = VexL.t(expanded ? '收起辅助面板' : '展开辅助面板');
      toggle.setAttribute('aria-label', toggle.title);
    }
    $('outline-toggle').setAttribute('aria-pressed', String(expanded));
    $('outline-toggle').title = $('outline-toggle').ariaLabel = VexL.t(expanded ? '收起文档大纲' : '展开文档大纲');
    if (window.VexAuxiliary) VexAuxiliary.sync();
  }
  function showStructure(expanded) {
    structurePreferred = expanded;
    VexStorage.setItem('vex-design-structure', expanded ? '1' : '0');
    syncStructureVisibility();
  }
  try {
    var savedStructureWidth = Number(VexStorage.getItem('vex-design-structure-width'));
    if (savedStructureWidth >= 280 && savedStructureWidth <= 480) structureWidth = savedStructureWidth;
  } catch (_) {}
  function resizeStructure(width, persist) {
    var available = workspace.clientWidth;
    var leftWidth = sidebar.classList.contains('closed') ? 0 : sidebarWidth;
    var maximum = available - leftWidth < 952 ? 480 : Math.min(480, available - leftWidth - 640);
    structureWidth = Math.max(280, Math.min(maximum, width));
    structureSidebar.style.width = structureWidth + 'px';
    structureSidebar.style.minWidth = '0';
    structureInner.style.width = structureWidth + 'px';
    structureSplitter.setAttribute('aria-valuenow', String(Math.round(structureWidth)));
    if (persist) {
      try { VexStorage.setItem('vex-design-structure-width', String(structureWidth)); } catch (_) {}
      showStructure(true);
    }
    syncStructureVisibility();
  }
  resizeStructure(structureWidth, false);
  var structureDrag = null;
  structureSplitter.addEventListener('pointerdown', function (event) {
    if (event.button !== 0) return;
    event.preventDefault();
    showStructure(true);
    var current = structureSplitter.getBoundingClientRect(), host = workspace.getBoundingClientRect();
    structureDrag = { offset: current.right - event.clientX, hostRight: host.right };
    structureSplitter.setPointerCapture(event.pointerId);
    document.body.classList.add('structure-resizing');
  });
  structureSplitter.addEventListener('pointermove', function (event) {
    if (!structureDrag) return;
    var samples = event.getCoalescedEvents ? event.getCoalescedEvents() : [event], latest = samples[samples.length - 1];
    resizeStructure(structureDrag.hostRight - (latest.clientX + structureDrag.offset), false);
  });
  ['pointerup', 'pointercancel', 'lostpointercapture'].forEach(function (name) {
    structureSplitter.addEventListener(name, function () {
      if (!structureDrag) return;
      structureDrag = null;
      document.body.classList.remove('structure-resizing');
      resizeStructure(structureWidth, true);
    });
  });
  structureSplitter.addEventListener('keydown', function (event) {
    var steps = { ArrowLeft: 12, ArrowRight: -12, PageUp: 48, PageDown: -48 };
    if (event.key in steps) { event.preventDefault(); showStructure(true); resizeStructure(structureWidth + steps[event.key], true); }
    else if (event.key === 'Home' || event.key === 'End') { event.preventDefault(); showStructure(true); resizeStructure(event.key === 'Home' ? 280 : 480, true); }
  });
  new ResizeObserver(function () {
    resizeSidebar(sidebarWidth, false);
    resizeStructure(structureWidth, false);
  }).observe(workspace);

  var canvas = $('map-viewport'), scene = $('map-scene'), input = $('markdown-input'), mapElements = new Map();
  /* 导图缩放坞的标记已在 HTML 里，这里只接上「拖动时才浮出百分比气泡」的显隐。 */
  var zoomDock = document.querySelector('.map-controls');
  VexZoomDock.attach(zoomDock);
  var labels = { heading: '标题', paragraph: '段落', quote: '引用', list: '列表项', code: '代码块', table: '表格' };
  var branchColors = ['var(--accent)', 'var(--branch-secondary)', 'var(--branch-tertiary)', 'var(--accent)', 'var(--muted)'];
  var textMeasure = document.createElement('canvas').getContext('2d');
  var typewriter = VexTypewriter.create({
    source: input, instant: $('instant-content'), enabled: function () { return state.typewriter; },
    reducedMotion: function () { return reducedMotion() || document.documentElement.dataset.motion === 'false'; }
  });
  var instant = createVexInstantEditor({
    input: input, blocks: function () { return state.parsed ? state.parsed.blocks : []; },
    renderBlock: function (block) {
      var element = $('markdown-preview').querySelector('[data-line="' + block.line + '"]');
      var listTag = /^\s*\d+[.)]\s/.test(block.raw) ? 'ol' : 'ul';
      return element ? (block.kind === 'list' ? '<' + listTag + '>' + element.outerHTML + '</' + listTag + '>' : element.outerHTML) : '<p></p>';
    },
    refresh: refreshNow, cursor: updateCursor, remember: remember,
    autoPair: function () { return state.autoPair; }, typewriter: function () { return state.typewriter; }
  });
  function remember(text) {
    if (state.undo[state.undo.length - 1] !== text) state.undo.push(text);
    if (state.undo.length > 100) state.undo.shift(); state.redo = [];
  }
  // 撤销/重做保留光标位置，替换整篇正文后不跳到文末。
  function restoreHistory(from, to) {
    var caret = input.selectionStart;
    to.push(input.value); input.value = from.pop();
    var at = Math.min(caret, input.value.length);
    input.setSelectionRange(at, at);
    input.dispatchEvent(new Event('input')); refreshNow();
    if (input.getClientRects().length) input.focus({ preventScroll: true });
  }
  function VexEsc(text) { return text.replace(/[&<>"']/g, function (char) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]; }); }
  function icon(name) { return '<svg class="ico" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; }
  var layerFocusOrigin = null;
  function closeLayers() {
    var wasOpen = Array.prototype.some.call(document.querySelectorAll('.win-layer'), function (layer) { return !layer.hidden; });
    document.querySelectorAll('.win-layer').forEach(function (layer) { layer.hidden = true; });
    if (wasOpen && layerFocusOrigin && layerFocusOrigin.isConnected) { var origin = layerFocusOrigin; layerFocusOrigin = null; requestAnimationFrame(function () { origin.focus({ preventScroll: true }); }); }
  }
  // 弹层、菜单、设置页或元素菜单开着时，编辑类快捷键交给弹层自身，不越层操作正文。
  function overlayOpen() { return !!document.querySelector('.win-layer:not([hidden]), .menu-flyout:not([hidden]), .vex-settings-host:not([hidden]), .vex-element-layer'); }
  function openLayer(name) {
    var target = $('layer-' + name);
    var allClosed = Array.prototype.every.call(document.querySelectorAll('.win-layer'), function (layer) { return layer.hidden; });
    if (allClosed && document.activeElement !== document.body) layerFocusOrigin = document.activeElement;
    document.querySelectorAll('.win-layer').forEach(function (layer) { layer.hidden = layer !== target; });
    if (!target) return;
    requestAnimationFrame(function () {
      if (target.hidden || target.contains(document.activeElement)) return;
      var focusable = target.querySelector('[autofocus]') || target.querySelector('input:not([type="hidden"])') || target.querySelector('button.primary') || target.querySelector('button');
      if (focusable) focusable.focus({ preventScroll: true });
    });
  }
  var pendingSwitch = null, switchOrigin = null;
  function restoreSwitchOrigin() { if(!switchOrigin)return;var origin=switchOrigin;switchOrigin=null;requestAnimationFrame(function(){if(origin.focus?.isConnected)origin.focus.focus({preventScroll:true});input.setSelectionRange(origin.start,origin.end);origin.scrolls.forEach(function(item){item.el.scrollTop=item.top;});}); }
  function confirmSwitch(callback) {
    if (!state.dirty) { callback(); return; }
    pendingSwitch = callback;switchOrigin={focus:document.activeElement,start:input.selectionStart,end:input.selectionEnd,scrolls:Array.from(document.querySelectorAll('.source-scroll,.instant-scroll,.reading-scroll')).map(function(el){return {el:el,top:el.scrollTop};})};
    openLayer('unsaved');
  }
  function loadDoc(index) {
    instant.finish(); state.undo = []; state.redo = [];
    activeDocumentLine = 1; activeOutlineLine = 1; lastMapChapter = null; state.editingLine = 1;
    clearTimeout(state.pending);
    state.doc = index; state.selected = null; state.collapsed.clear(); state.dirty = false; state.mapVisited = false;
    input.value = docs[index].pendingText ?? docs[index].text; state.dirty = input.value !== docs[index].text; $('document-name').textContent = docs[index].name; $('node-inspector').hidden = true;
    input.readOnly = !!docs[index].readOnly; document.body.classList.toggle('builtin-readonly',input.readOnly);
    input.setSelectionRange(0, 0); document.querySelector('.source-scroll').scrollTop = 0;
    document.querySelector('.reading-scroll').scrollTop = 0;
    document.querySelector('.instant-scroll').scrollTop = 0;
    renderFiles(); refresh(); requestAnimationFrame(function () { fit(true); });
    window.dispatchEvent(new CustomEvent('vex-document-loaded'));
  }
  function renderFiles() {
    if ($('folder-name')) $('folder-name').textContent = workspaceFolder;


    var visibleDocs = docs.map(function (doc, index) { return index === state.doc ? Object.assign({}, doc, {text:input.value}) : doc; });
    VexWriting.renderFiles({container:$('file-list'), docs:state.emptyWorkspace ? [] : visibleDocs, current:visibleDocs[state.doc], folder:workspaceFolder, mode:fileListMode, descending:fileDescending, query:$('file-filter').value, icon:icon,
      sort:alphaSort === 'default' ? null : alphaSort,
      onOpen:function (_, index) { if (index !== state.doc) confirmSwitch(function () { loadDoc(index); }); }});
    syncFilesToolbar();
    var empty = $('file-list').querySelector('.empty-state');
    if (empty) {
      empty.textContent = state.emptyWorkspace ? '还没有打开文档。新建一篇，或打开本地文件开始。' : '当前文件名没有匹配项。此处不搜索正文。';
      if (!state.emptyWorkspace) {
        var clear = document.createElement('button'); clear.type = 'button'; clear.textContent = '清除文件名筛选'; clear.onclick = function () { $('file-filter').value = ''; renderFiles(); }; empty.appendChild(clear);
        var find = document.createElement('button'); find.type = 'button'; find.textContent = '查找当前正文'; find.onclick = function () { VexProto.handleAction('find'); }; empty.appendChild(find);
      }
    }

    $('file-empty').hidden = true;
    var collapseFiles = $('collapse-files');
    if (collapseFiles) {
      collapseFiles.disabled = fileListMode !== 'tree' || !document.querySelectorAll('#file-list details.file-directory[open]').length;
    }
  }
  function filterFiles() {
    renderFiles();
  }
  $('file-filter').oninput = filterFiles;
  $('outline-filter').oninput = renderOutline;



  var fileContextMenu = document.createElement('div'), fileContextTarget = null;
  fileContextMenu.className = 'file-context-menu'; fileContextMenu.hidden = true; fileContextMenu.setAttribute('role', 'menu');
  document.body.appendChild(fileContextMenu);
  function closeFileContextMenu() { fileContextMenu.hidden = true; fileContextTarget = null; }
  function showFileContextMenu(target, event) {
    fileContextTarget = target;
    fileContextMenu.innerHTML = '<button type="button" role="menuitem" data-file-context="open-window">在新窗口打开</button><button type="button" role="menuitem" data-file-context="reveal">打开文件位置</button>';
    fileContextMenu.hidden = false;
    var width = fileContextMenu.offsetWidth || 168, height = fileContextMenu.offsetHeight || 72;
    fileContextMenu.style.left = Math.max(8, Math.min(event.clientX, innerWidth - width - 8)) + 'px';
    fileContextMenu.style.top = Math.max(8, Math.min(event.clientY, innerHeight - height - 8)) + 'px';
    fileContextMenu.querySelector('button').focus({ preventScroll: true });
  }
  fileContextMenu.addEventListener('click', function (event) {
    var action = event.target.closest('[data-file-context]'); if (!action || !fileContextTarget) return;
    var target = fileContextTarget, act = action.dataset.fileContext; closeFileContextMenu();
    if (act === 'reveal') {
      if (target.kind === 'folder') VexProto.toastKey('ToastFolderLocation', target.path || '演示文档');
      else {
        var reveal = docs[target.index];
        VexProto.toastKey('ToastFileLocation', (reveal && (reveal.path || reveal.name)) || '演示文档');
      }
      return;
    }
    if (target.kind === 'folder') {
      if (!VexDocumentWindow.open({ intent: 'open-folder' })) VexProto.toastKey('ToastAllowPopups');
      return;
    }
    var doc = docs[target.index];
    if (!doc || !VexDocumentWindow.open({ document: { name: doc.name, text: target.index === state.doc ? input.value : doc.text, readOnly: !!doc.readOnly, path: doc.path } })) VexProto.toastKey('ToastAllowPopups');
  });
  document.addEventListener('contextmenu', function (event) {
    var row = event.target.closest('#file-list .file-row'), folder = event.target.closest('#file-list .file-directory > summary');
    if (!row && !folder) return;
    event.preventDefault(); event.stopPropagation();
    if (row) showFileContextMenu({ kind: 'file', index: Number(row.dataset.fileIndex) }, event);
    else showFileContextMenu({ kind: 'folder', path: folder.dataset.folderPath || '' }, event);
  });
  document.addEventListener('pointerdown', function (event) { if (!fileContextMenu.contains(event.target)) closeFileContextMenu(); });
  document.addEventListener('keydown', function (event) { if (event.key === 'Escape' && !fileContextMenu.hidden) { event.preventDefault(); closeFileContextMenu(); } });
  function inline(text) {
    return VexRendering.inline(text);
  }
  function renderPreview() {
    var html = '', blocks = state.parsed.blocks;
    function listMeta(block) {
      var match = /^(\s*)([-+*]|\d+[.)])\s+(?:\[([ xX])\]\s+)?(.*)$/.exec(block.raw.split('\n')[0]);
      return match ? { indent: block.indent || match[1].replace(/\t/g, '    ').length, ordered: /^\d/.test(match[2]), checked: match[3], text: match[4] } : null;
    }
    function renderListAt(index, indent) {
      var first = listMeta(blocks[index]), ordered = first.ordered, tag = ordered ? 'ol' : 'ul', items = [], current = null;
      while (index < blocks.length && blocks[index].kind === 'list') {
        var meta = listMeta(blocks[index]);
        if (!meta || meta.indent < indent || meta.indent === indent && meta.ordered !== ordered) break;
        if (meta.indent > indent) {
          if (!current) break;
          var nested = renderListAt(index, meta.indent); current.html += nested.html; index = nested.end; continue;
        }
        current = { html: '<li data-line="' + blocks[index].line + '">' + (meta.checked === undefined ? '' : meta.checked.toLowerCase() === 'x' ? '☑ ' : '☐ ') + inline(meta.text) + '</li>' };
        items.push(current); index++;
      }
      return { html: '<' + tag + '>' + items.map(function (item) { return item.html; }).join('') + '</' + tag + '>', end: index };
    }
    var lastEndLine = 0;
    for (var blockIndex = 0; blockIndex < blocks.length;) {
      var block = blocks[blockIndex];
      // 空行规则（与 Typora 相符）：第一个空行不产生额外间距，每多一个空行补一行正文高度。
      if (blockIndex > 0) {
        var prev = blocks[blockIndex - 1];
        var blanks = block.line - (prev.line - 1 + prev.raw.split('\n').length) - 1;
        for (var gap = Math.max(0, blanks - 1); gap > 0; gap--) html += '<div class="md-blank-gap"></div>';
      }
      var attr = ' data-line="' + block.line + '"';
      if (block.kind === 'list') {
        var list = renderListAt(blockIndex, block.indent || 0); html += list.html; blockIndex = list.end; continue;
      }
      if (VexRendering.special(block)) html += VexRendering.renderSpecial(block);
      else if (block.kind === 'rule') html += '<hr' + attr + '>';
      else if (block.kind === 'toc') html += block.html;
      else if (block.kind === 'paragraph' && block.raw.trim() === '[TOC]') {
        html += '<nav class="md-toc" data-line="' + block.line + '" aria-label="目录"><strong>目录</strong>' + state.nodes.filter(function (node) { return node.kind === 'heading' && node.level > 0; }).map(function (node) { return '<a href="#heading-' + (node.line - 1) + '" style="padding-left:' + (node.level - 1) * 14 + 'px">' + inline(node.text) + '</a>'; }).join('') + '</nav>';
      }
      else if (block.kind === 'heading') html += '<h' + block.level + ' id="heading-' + (block.line - 1) + '"' + attr + '>' + inline(block.raw.split('\n')[0].replace(/^ {0,3}#{1,6}\s+/, '').replace(/\s+#+\s*$/, '')) + '</h' + block.level + '>';
      else if (block.kind === 'code') {
        html += '<section class="md-code-block"' + attr + '><pre><code>' + VexEsc(block.text) + '</code></pre></section>';
      }
      else if (block.kind === 'callout') html += block.html;
      else if (block.kind === 'footnote') html += block.html;
      else if (block.kind === 'quote' && /^\s*>\s*\[!(INFO|WARNING)\]/i.test(block.raw)) {
        var callout = /^\s*>\s*\[!(INFO|WARNING)\]/i.exec(block.raw);
        html += '<aside class="md-callout ' + callout[1].toLowerCase() + '"' + attr + '><strong>' + (callout[1].toUpperCase() === 'WARNING' ? '警告' : '信息') + '</strong><div>' + inline(block.raw.replace(/^\s*>\s*\[!(?:INFO|WARNING)\]\s*/i, '').replace(/^\s*>\s?/gm, '').replace(/\n/g, ' ')) + '</div></aside>';
      }
      else if (block.kind === 'paragraph' && /^ {0,3}\[\^([^\]\r\n]+)\]:/.test(block.raw)) {
        var footnote = /^ {0,3}\[\^([^\]\r\n]+)\]:\s*(.*)$/.exec(block.raw);
        html += '<div class="md-footnote" id="vex-footnote-' + encodeURIComponent(footnote[1]) + '"' + attr + '><sup>[' + VexEsc(footnote[1]) + ']</sup> ' + inline(footnote[2]) + ' <a href="#vex-footnote-ref-' + encodeURIComponent(footnote[1]) + '" aria-label="返回正文">↩</a></div>';
      }
      else if (block.kind === 'quote') html += '<blockquote' + attr + '>' + inline(block.raw.replace(/^\s*>\s?/gm, '').replace(/\n/g, ' ')) + '</blockquote>';
      else if (block.kind === 'table') {
        var table = VexDocumentOps.table(block.raw), rows = table?.rows || [];
        html += '<div class="md-table-scroll" tabindex="0" aria-label="表格"><table' + attr + '>' + rows.map(function (row, index) {
          var tag = index === 0 ? 'th' : 'td';
          return '<tr>' + row.map(function (cell,col) { return '<' + tag + ' data-align="' + (table.align[col] || 'left') + '" style="text-align:' + (table.align[col] || 'left') + '">' + inline(cell.replace(/\\\|/g,'|')) + '</' + tag + '>'; }).join('') + '</tr>';
        }).join('') + '</table></div>';
      } else html += '<p' + attr + '>' + inline(block.raw.replace(/\n/g, ' ')) + '</p>';
      blockIndex++;
    }
    $('markdown-preview').innerHTML = html || '<p class="empty-state">输入 Markdown，开始写作。</p>';
  }
  var activeOutlineLine = 1, activeDocumentLine = 1, outlineSyncFrame = 0;
  function activateOutlineAtLine(line, ensureVisible) {
    if (!Number.isFinite(Number(line))) return;
    activeOutlineLine = Math.max(1, Number(line));
    var headings = state.nodes.filter(function (node) { return node.kind === 'heading' && node.level > 0; });
    var active = headings[0] || null;
    headings.forEach(function (node) { if (node.line <= activeOutlineLine) active = node; });
    var activeButton = null;
    $('outline-body').querySelectorAll('.structure-item').forEach(function (item) {
      var selected = !!active && Number(item.dataset.sourceLine) === active.line;
      item.classList.toggle('on', selected);
      item.setAttribute('aria-current', selected ? 'location' : 'false');
      if (selected) activeButton = item;
    });
    if (ensureVisible && activeButton) {
      var list = $('outline-body'), top = activeButton.offsetTop, bottom = top + activeButton.offsetHeight;
      if (top < list.scrollTop) list.scrollTop = top;
      else if (bottom > list.scrollTop + list.clientHeight) list.scrollTop = bottom - list.clientHeight;
    }
  }
  function syncOutlineFromDocumentScroll(scroller, article) {
    if (!scroller || !article || scroller.hidden || !article.isConnected) return;
    queuePeerScroll(scroller, function () {
      var anchors = articleAnchors(scroller, article), top = scroller.scrollTop + 14;
      if (!anchors.length) return 1;
      var index = 0;
      while (index + 1 < anchors.length && anchors[index + 1].top <= top) index++;
      var current = anchors[index], next = anchors[index + 1];
      return current.line + (next ? Math.max(0, Math.min(1, (top - current.top) / Math.max(1, next.top - current.top))) * (next.line - current.line) : 0);
    });
  }
  // Ignore only the exact scroll positions we wrote. User input never waits for a timer.
  var peerScrollPositions = new WeakMap(), lastMapChapter = null;
  function articleAnchors(scroller, article) {
    var origin = scroller.getBoundingClientRect().top - scroller.scrollTop, previous = 0;
    return Array.from(article.querySelectorAll('[data-line]')).map(function (element) {
      return { line: Number(element.dataset.line), top: element.getBoundingClientRect().top - origin };
    }).filter(function (anchor) { if (anchor.line <= previous) return false; previous = anchor.line; return true; });
  }
  function writePeerScroll(scroller, top) {
    if (!scroller || !scroller.getClientRects().length) return;
    top = Math.max(0, Math.min(top, scroller.scrollHeight - scroller.clientHeight));
    if (Math.abs(scroller.scrollTop - top) < .5) return;
    scroller.scrollTop = top;
    peerScrollPositions.set(scroller, scroller.scrollTop);
  }
  function queuePeerScroll(scroller, readLine) {
    if (!scroller.getClientRects().length || state.guideRestoring) return;
    var expected = peerScrollPositions.get(scroller);
    if (expected !== undefined && Math.abs(scroller.scrollTop - expected) < 1) return;
    peerScrollPositions.delete(scroller);
    if (outlineSyncFrame) cancelAnimationFrame(outlineSyncFrame);
    outlineSyncFrame = requestAnimationFrame(function () {
      outlineSyncFrame = 0;
      if (!scroller.getClientRects().length || state.guideRestoring) return;
      syncPeerViewsToLine(readLine(), scroller, false);
    });
  }
  function articleTopAtLine(scroller, article, line) {
    if (!scroller || !article || !scroller.getClientRects().length) return;
    var anchors = articleAnchors(scroller, article), index = 0;
    if (!anchors.length) return 0;
    while (index + 1 < anchors.length && anchors[index + 1].line <= line) index++;
    var current = anchors[index], next = anchors[index + 1];
    return current.top - 14 + (next ? Math.max(0, (line - current.line) / (next.line - current.line)) * (next.top - current.top) : 0);
  }
  function revealAndCenterMapNode(node) {
    if (!node || !$('map-pane').getClientRects().length) return;
    var expanded = false;
    function reveal(current, ancestors) {
      if (current.id === node.id) {
        ancestors.forEach(function (id) { if (state.collapsed.has(id)) { state.collapsed.delete(id); expanded = true; } });
        return true;
      }
      for (var i = 0; i < current.children.length; i++) {
        if (reveal(current.children[i], ancestors.concat(current.id))) return true;
      }
      return false;
    }
    reveal(state.parsed.root, []);
    if (expanded) renderMap();
    var element = mapElements.get(node.id);
    if (!element) return;
    if (state.selected !== node.id) $('node-inspector').hidden = true;
    mapElements.forEach(function (item, id) { item.classList.toggle('selected', id === node.id); });
    state.selected = node.id;
    state.x = canvas.clientWidth / 2 - (node.x + node.width / 2) * state.scale;
    state.y = canvas.clientHeight / 2 - (node.y + node.height / 2) * state.scale;
    lastCanvasSize = { width: canvas.clientWidth, height: canvas.clientHeight };
    transform(true);
  }
  function syncPeerViewsToLine(line, sourceScroller, force) {
    line = Math.max(1, Number(line) || 1);
    activeDocumentLine = line;
    if (sourceScroller) {
      state.followDriver = sourceScroller === canvas ? '导图' : sourceScroller.classList.contains('source-scroll') ? '源码' : sourceScroller.classList.contains('reading-scroll') ? '阅读' : '正文';
      window.dispatchEvent(new CustomEvent('vex-follow-changed'));
    }
    if (!state.followViews && !force) return;
    var instantScroll = document.querySelector('.instant-scroll');
    var readingScroll = document.querySelector('.reading-scroll');
    var sourceScroll = document.querySelector('.source-scroll');
    // Read both article geometries before writing scroll offsets.
    var instantTop = sourceScroller !== instantScroll ? articleTopAtLine(instantScroll, $('instant-content'), line) : undefined;
    var readingTop = sourceScroller !== readingScroll ? articleTopAtLine(readingScroll, $('markdown-preview'), line) : undefined;
    var inputStyle = getComputedStyle(input), lineHeight = parseFloat(inputStyle.lineHeight) || 23;
    if (instantTop !== undefined) writePeerScroll(instantScroll, instantTop);
    if (readingTop !== undefined) writePeerScroll(readingScroll, readingTop);
    if (sourceScroller !== sourceScroll) {
      input.scrollTop = 0;
      writePeerScroll(sourceScroll, (line - 1) * lineHeight + parseFloat(inputStyle.paddingTop) - 14);
    }
    activateOutlineAtLine(line, true);
    var node = state.nodes.find(function (item) { return item.kind === 'heading' && item.line === line; });
    if (!node) {
      state.nodes.forEach(function (item) { if (item.kind === 'heading' && item.line <= line && (!node || item.line > node.line)) node = item; });
    }
    if (node && sourceScroller !== canvas && (force || node.id !== lastMapChapter) && $('map-pane').getClientRects().length) {
      lastMapChapter = node.id;
      revealAndCenterMapNode(node);
    }
  }
  function syncOutlineFromSource() {
    if (!input.getClientRects().length || document.activeElement !== input) return;
    var line = input.value.slice(0, input.selectionStart || 0).split('\n').length;
    state.editingLine = line;
    activateOutlineAtLine(line, true);
    syncPeerViewsToLine(line, document.querySelector('.source-scroll'), false);
  }
  function syncOutlineFromInstantSelection() {
    if (!$('instant-content').getClientRects().length) return;
    var selection = window.getSelection && window.getSelection();
    var node = selection && selection.anchorNode;
    var element = node && (node.nodeType === 1 ? node : node.parentElement);
    var block = element && element.closest ? element.closest('[data-line]') : null;
    if (block) {
      var line = Number(block.dataset.line);
      if ($('instant-content').contains(element)) state.editingLine = line;
      activateOutlineAtLine(line, true);
      syncPeerViewsToLine(line, document.querySelector('.instant-scroll'), false);
    }
  }
  function bindOutlineViewSync() {
    var instantScroll = document.querySelector('.instant-scroll');
    var readingScroll = document.querySelector('.reading-scroll');
    var sourceScroll = document.querySelector('.source-scroll');
    instantScroll.addEventListener('scroll', function () { syncOutlineFromDocumentScroll(instantScroll, $('instant-content')); }, { passive: true });
    readingScroll.addEventListener('scroll', function () { syncOutlineFromDocumentScroll(readingScroll, $('markdown-preview')); }, { passive: true });
    sourceScroll.addEventListener('scroll', function () {
      queuePeerScroll(sourceScroll, function () {
        var style = getComputedStyle(input);
        return Math.max(0, sourceScroll.scrollTop + input.scrollTop + 14 - parseFloat(style.paddingTop)) / (parseFloat(style.lineHeight) || 23) + 1;
      });
    }, { passive: true });
    input.addEventListener('scroll', function () {
      if (!input.scrollTop) return;
      var lineHeight = parseFloat(getComputedStyle(input).lineHeight) || 23;
      var line = (sourceScroll.scrollTop + input.scrollTop) / lineHeight + 1;
      syncPeerViewsToLine(line, sourceScroll, false);
    }, { passive: true });
    [instantScroll, readingScroll, sourceScroll].forEach(function (scroller) {
      ['wheel', 'touchstart', 'pointerdown'].forEach(function (eventName) {
        scroller.addEventListener(eventName, function () { peerScrollPositions.delete(scroller); }, { passive: true });
      });
    });
    ['click', 'keyup', 'select', 'input'].forEach(function (eventName) {
      input.addEventListener(eventName, syncOutlineFromSource);
    });
    ['click', 'keyup', 'input'].forEach(function (eventName) {
      $('instant-content').addEventListener(eventName, syncOutlineFromInstantSelection);
    });
  }
  function renderOutline() {
    $('outline-body').innerHTML = '';
    var query = $('outline-filter').value.trim().toLowerCase();
    var maxLevel = outlineFolded ? 2 : 6;
    var headings = state.nodes.filter(function (node) { return node.kind === 'heading' && node.level > 0 && node.level <= maxLevel && node.text.toLowerCase().includes(query); });
    var counters = [0, 0, 0, 0, 0, 0];
    headings.forEach(function (node) {
      counters[node.level - 1]++; for (var i = node.level; i < 6; i++) counters[i] = 0;
      var number = counters.slice(0, node.level).join('.');
      var button = document.createElement('button'); button.className = 'structure-item'; button.dataset.level = String(node.level);
      button.dataset.sourceLine = node.line;
      button.style.paddingLeft = (Math.max(0, node.level - 1) * 12) + 'px';
      button.innerHTML = '<span class="structure-marker"></span><span class="structure-num">' + number + '</span><span class="structure-text"></span>';
      button.querySelector('.structure-text').textContent = node.text;
      button.onclick = function () {
        if (state.view === 'mindmap') {
          var ancestors = [];
          function search(current) { if (current.id === node.id) return true; for (var child of current.children) { if (search(child)) { ancestors.push(current.id); return true; } } return false; }
          search(state.parsed.root); ancestors.forEach(function (id) { state.collapsed.delete(id); }); renderMap(); selectNode(node);
          var element = mapElements.get(node.id); if (element) { state.x = canvas.clientWidth / 2 - (node.x + node.width / 2) * state.scale; state.y = canvas.clientHeight / 2 - (node.y + node.height / 2) * state.scale; transform(true); }
        } else if (state.view === 'read') {
          var target = $('markdown-preview').querySelector('[data-line="' + node.line + '"]');
          if (target) { target.scrollIntoView({ block: 'start', behavior: reducedMotion() ? 'auto' : 'smooth' }); target.classList.remove('located'); void target.offsetWidth; target.classList.add('located'); }
        } else locate(node);
        activateOutlineAtLine(node.line, true);
        syncPeerViewsToLine(node.line, null, true);
      };
      $('outline-body').appendChild(button);
    });
    if (!headings.length) $('outline-body').innerHTML = '<div class="empty-state">' + (query ? '没有匹配的标题' : '添加标题后，会在这里显示大纲。') + '</div>';
    activateOutlineAtLine(activeOutlineLine, false);
    syncFilesToolbar();
  }
  bindOutlineViewSync();
  function refresh() {
    if (state.parsed && state.lastText === input.value && state.lastDoc === state.doc) { updateMeta(); updateCursor(); return; }
    var collapsedKeys = new Set(state.nodes.filter(function (node) { return state.collapsed.has(node.id); }).map(function (node) { return node.key; }));
    state.parsed = MM.parse(input.value, docs[state.doc].name.replace(/\.[^.]+$/, ''));
    state.nodes = MM.flatten(state.parsed.root);
    state.collapsed = new Set(state.nodes.filter(function (node) { return collapsedKeys.has(node.key); }).map(function (node) { return node.id; }));
    state.lastText = input.value; state.lastDoc = state.doc;
    // 源码变化后关闭旧节点详情，防止重新编号后显示到其他段落。
    state.selected = null; lastMapChapter = null; $('node-inspector').hidden = true;
    $('line-numbers').innerHTML = state.parsed.lines.map(function (_, index) { return '<div>' + (index + 1) + '</div>'; }).join('');
    input.style.height = typewriter.sourceHeight() + 'px';
    updateMeta(); renderPreview(); instant.render(); VexRendering.diagrams(); renderOutline(); renderMap(); updateCursor();
    window.dispatchEvent(new CustomEvent('vex-content-changed'));
  }
  function updateMeta() {
    $('sb-wordcount').textContent = '字数 ' + contentCount().toLocaleString();
    updateSaveStatus();
  }
  function updateSaveStatus() {
    $('dirty-dot').hidden = !state.dirty;
    window.dispatchEvent(new CustomEvent('vex-save-state-changed'));
  }
  function refreshNow() { clearTimeout(state.pending); refresh(); }
  input.addEventListener('input', function () {
    state.dirty = input.value !== docs[state.doc].text;
    $('dirty-dot').hidden = !state.dirty;
    updateSaveStatus();
    clearTimeout(state.pending); state.pending = setTimeout(refresh, 180);
  });
  input.addEventListener('beforeinput', function () { remember(input.value); });
  // 源码视图上色：镜像层跟随输入框刷新，异常时退回纯文本。
  VexSourceHighlight.attach($('source-highlight'), input);
  function updateCursor() {
    if (state.typewriter) typewriter.request();
  }
  ['click', 'keyup', 'select'].forEach(function (event) { input.addEventListener(event, updateCursor); });
  input.addEventListener('keydown', function (event) {
    if (event.isComposing) return;
    if (event.key === 'Tab') { event.preventDefault(); insert('  ', false); }
    var pairs = { '(': ')', '[': ']', '{': '}', '"': '"', "'": "'", '`': '`' };
    if (state.autoPair && !event.ctrlKey && !event.metaKey && pairs[event.key]) {
      event.preventDefault(); remember(input.value);
      var start = input.selectionStart, selected = input.value.slice(start, input.selectionEnd);
      input.setRangeText(event.key + selected + pairs[event.key], start, input.selectionEnd, 'end');
      input.setSelectionRange(start + 1, start + 1 + selected.length); input.dispatchEvent(new Event('input')); updateCursor();
    }
  });
  function reducedMotion() {
    var flag = document.documentElement.dataset.motion;
    return !state.motion || flag === 'off' || flag === 'false' || matchMedia('(prefers-reduced-motion: reduce)').matches;
  }
  function applyPresentation() {
    var root = document.querySelector('.app-window');
    root.classList.toggle('presentation-mode', state.presentation);
    root.style.setProperty('--presentation-scale', state.presentationScale);
    $('presentation-tools').hidden = !state.presentation;
    if (state.presentation) setView('read');
  }
  function startPresentation() {
    if (window.VexPresentation) {
      VexPresentation.open({
        text: function () { return input.value; }, title: function () { return docs[state.doc].name; },
        finish: function () { instant.finish(); closeLayers(); }, language: function () { return state.language; },
        background: document.querySelector('.app-window'), mapMode: function () { return state.view === 'mindmap'; }, mapLayout: function () { return settings.layout; }
      });
      return;
    }
    if (state.presentation) return;
    state.presentation = true; state.presentationScale = 1.5;
    instant.finish(); applyPresentation();
  }
  function stopPresentation() {
    if (!state.presentation) return;
    state.presentation = false; applyPresentation(); setView('edit');
  }

  function setView(view) {
    if (!['edit', 'read', 'mindmap'].includes(view)) view = 'edit';
    if (view !== 'edit') instant.finish();
    refreshNow(); state.view = view; document.querySelector('.workspace').dataset.view = view;
    document.querySelectorAll('[data-view]').forEach(function (button) {
      if (button.tagName === 'BUTTON') { button.setAttribute('aria-selected', String(button.dataset.view === view)); button.tabIndex = button.dataset.view === view ? 0 : -1; }
    });
    $('edit-options').hidden = view !== 'edit';
    $('instant-pane').hidden = view !== 'edit' || state.editMode !== 'instant';
    $('source-pane').hidden = !(view === 'edit' && state.editMode === 'source') && !(view === 'mindmap' && state.source);
    $('reading-pane').hidden = view !== 'read';
    $('map-pane').hidden = view !== 'mindmap';
    $('reading-title').textContent = view === 'read' ? '阅读' : '正文预览';
    document.querySelector('.workspace').classList.toggle('source-only', view === 'edit' && state.editMode === 'source');
    document.querySelectorAll('.content-area > section').forEach(function (pane) { pane.classList.remove('pane-enter'); void pane.offsetWidth; if (!pane.hidden) pane.classList.add('pane-enter'); });
    $('map-settings').hidden = true; $('map-settings-button').setAttribute('aria-expanded', 'false');
    if (view === 'mindmap' && !state.mapVisited) { state.mapVisited = true; requestAnimationFrame(function () { fit(true); }); }
    window.dispatchEvent(new CustomEvent('vex-view-changed', { detail: { view: view, editMode: state.editMode } }));
  }
  function setEditMode(mode) {
    instant.finish(); state.editMode = mode;
    try { VexStorage.setItem('vex-design-edit-method', mode); } catch (_) {}
    setView('edit');
  }
  // 右侧“文档结构”栏：记忆展开状态，收起时箭头翻转。
  syncStructureVisibility();
  function visibleChildren(node) {
    return node.children.filter(function (child) {
      return child.kind === 'heading' || (['paragraph', 'quote'].includes(child.kind) && settings.paragraphs)
        || (child.kind === 'list' && settings.lists) || (['code', 'table'].includes(child.kind) && settings.blocks);
    });
  }
  function nodeText(node) { return node.kind === 'heading' ? node.text : MM.clip(node.text || node.label || '空内容', settings.length); }
  function height(node) {
    var content = nodeText(node), fontSize = node.kind === 'heading' ? 13 : 12, width = node.width - (node.kind === 'heading' ? 58 : 26);
    textMeasure.font = (node.kind === 'heading' ? '600 ' : '') + fontSize + 'px ' + getComputedStyle(document.body).fontFamily;
    var rows = 1, used = 0;
    MM.characters(content).forEach(function (char) { var charWidth = textMeasure.measureText(char).width; if (used + charWidth > width) { rows++; used = 0; } used += charWidth; });
    return 22 + rows * 20;
  }
  function renderMap() {
    if (!state.parsed) return;
    var geometry = VexMapLayout.layout(state.parsed.root, {
      mode: settings.layout,
      children: function (node) { return node.visibleChildren = state.collapsed.has(node.id) ? [] : visibleChildren(node); },
      measure: function (node) { node.width = node.isRoot ? 214 : 224; node.height = height(node); }
    }), rendered = geometry.nodes;
    scene.dataset.layout = settings.layout;
    var visible = new Set(rendered.map(function (node) { return node.id; }));
    mapElements.forEach(function (element, id) { if (!visible.has(id)) { element.remove(); mapElements.delete(id); } });
    rendered.forEach(function (node, index) {
      var element = mapElements.get(node.id), isNew = !element;
      if (isNew) { element = document.createElement('div'); mapElements.set(node.id, element); $('map-nodes').appendChild(element); }
      element.className = 'map-node' + (node.isRoot ? ' root' : node.kind === 'heading' ? '' : ' content') + (state.selected === node.id ? ' selected' : '') + (isNew ? ' enter' : '');
      element.dataset.kind = node.kind; element.dataset.side = node.layoutSide; element.dataset.line = node.line; element.dataset.nodeId = node.id; element.style.left = node.x + 'px'; element.style.top = node.y + 'px';
      element.style.width = node.width + 'px'; element.style.minHeight = node.height + 'px';
      element.style.setProperty('--branch-color', branchColors[node.branch % branchColors.length]);
      element.style.setProperty('--delay', Math.min(index * 14, 180) + 'ms');
      var kind = node.isRoot ? '文档' : node.kind === 'heading' ? 'H' + node.level : node.label || labels[node.kind];
      element.innerHTML = '<button class="map-node-button" title="' + VexEsc(node.text || kind) + '"><span class="map-node-text">' + VexEsc(nodeText(node)) + '</span><span class="map-node-type">' + VexEsc(kind) + '</span></button>';
      element.querySelector('button').onclick = function () { selectNode(node); };
      var children = visibleChildren(node);
      if (children.length) {
        var collapse = document.createElement('button'); collapse.className = 'map-collapse'; collapse.textContent = state.collapsed.has(node.id) ? '+' : '−';
        collapse.setAttribute('aria-label', (state.collapsed.has(node.id) ? '展开' : '折叠') + '：' + node.text); collapse.setAttribute('aria-expanded', String(!state.collapsed.has(node.id)));
        collapse.onclick = function () { if (state.collapsed.has(node.id)) state.collapsed.delete(node.id); else state.collapsed.add(node.id); $('node-inspector').hidden = true; renderMap(); };
        element.appendChild(collapse);
      }
    });
    $('map-links').innerHTML = geometry.links.map(function (link) { return '<path style="--branch-color:' + branchColors[link.node.branch % branchColors.length] + '" d="' + link.path + '"/>'; }).join('');
    state.bounds = { width: geometry.width, height: geometry.height };
    scene.style.width = state.bounds.width + 'px'; scene.style.height = state.bounds.height + 'px';
    $('map-links').setAttribute('width', state.bounds.width); $('map-links').setAttribute('height', state.bounds.height);
    $('map-count').textContent = state.nodes.filter(function (node) { return node.kind === 'heading'; }).length + ' 个标题 · ' + rendered.length + ' 个可见节点';
    $('collapse-all').innerHTML = icon('list') + (state.collapsed.size ? '展开全部' : '收起分支');
    transform();
    window.dispatchEvent(new CustomEvent('vex-map-rendered'));
  }
  function transform(animate) {
    scene.classList.toggle('animating', !!animate);
    scene.style.transform = 'translate(' + state.x + 'px,' + state.y + 'px) scale(' + state.scale + ')';
    var percent = Math.round(state.scale * 100) + '%';
    $('map-zoom-range').value = Math.round(state.scale * 100);
    /* 先回写拖动条再亮气泡：气泡按拖动条当前值算块心位置。 */
    VexZoomDock.update(zoomDock, percent);
  }
  function fit(readable) {
    if (settings.layout !== 'right') readable = false;
    if (window.VexWorkspace?.current === 'three') readable = false;
    if (state.guideRestoring || $('map-pane').hidden || !state.bounds || !canvas.clientWidth) return;
    var verticalPadding = canvas.clientHeight < 180 ? Math.min(24, canvas.clientHeight / 4) : 84;
    state.scale = Math.min(1, Math.max(readable === true ? .75 : .01, Math.min((canvas.clientWidth - 64) / state.bounds.width, (canvas.clientHeight - verticalPadding) / state.bounds.height)));
    state.x = (canvas.clientWidth - state.bounds.width * state.scale) / 2;
    state.y = (canvas.clientHeight - state.bounds.height * state.scale) / 2;
    if (readable === true) state.x = Math.max(24, state.x);
    lastCanvasSize = { width: canvas.clientWidth, height: canvas.clientHeight };
    transform(true);
  }
  function zoom(factor, anchor) {
    anchor = anchor || { x: canvas.clientWidth / 2, y: canvas.clientHeight / 2 };
    var previous = state.scale; state.scale = Math.min(2, Math.max(.01, state.scale * factor));
    state.x = anchor.x - (anchor.x - state.x) * state.scale / previous; state.y = anchor.y - (anchor.y - state.y) * state.scale / previous;
    transform(!anchor.pointer);
  }
  /* 拖动条与旧步进按钮同义：把目标百分比换算成倍率，锚点仍取画布中心，夹取与平移交给 zoom()。 */
  $('map-zoom-range').addEventListener('input', function () { zoom(Number(this.value) / 100 / state.scale); });
  var drag = null;
  canvas.addEventListener('pointerdown', function (event) {
    if (event.target.closest('button, input, textarea, [draggable="true"]') || event.button !== 0) return;
    drag = { x: event.clientX, y: event.clientY, startX: state.x, startY: state.y }; canvas.setPointerCapture(event.pointerId); canvas.classList.add('panning');
  });
  canvas.addEventListener('pointermove', function (event) { if (drag) { state.x = drag.startX + event.clientX - drag.x; state.y = drag.startY + event.clientY - drag.y; transform(); } });
  ['pointerup', 'pointercancel', 'lostpointercapture'].forEach(function (event) { canvas.addEventListener(event, function () { drag = null; canvas.classList.remove('panning'); }); });
  canvas.addEventListener('wheel', function (event) {
    event.preventDefault();
    if (event.ctrlKey || event.metaKey) { var rect = canvas.getBoundingClientRect(); zoom(event.deltaY < 0 ? 1.1 : 1 / 1.1, { x: event.clientX - rect.left, y: event.clientY - rect.top, pointer: true }); }
    else { state.x -= event.deltaX; state.y -= event.deltaY; transform(); }
  }, { passive: false });
  var lastCanvasSize = { width: 0, height: 0 };
  new ResizeObserver(function () {
    if ($('map-pane').hidden || !canvas.clientWidth) return;
    if (state.guideRestoring) { lastCanvasSize = { width: canvas.clientWidth, height: canvas.clientHeight }; return; }
    if (lastCanvasSize.width !== canvas.clientWidth || lastCanvasSize.height !== canvas.clientHeight) {
      if (lastCanvasSize.width && lastCanvasSize.height) {
        state.x += (canvas.clientWidth - lastCanvasSize.width) / 2;
        state.y += (canvas.clientHeight - lastCanvasSize.height) / 2;
        transform();
      } else fit(true);
      lastCanvasSize = { width: canvas.clientWidth, height: canvas.clientHeight };
    }
  }).observe(canvas);
  function selectNode(node) {
    state.selected = node.id;
    activateOutlineAtLine(node.line, true);
    syncPeerViewsToLine(node.line, canvas, false);
    mapElements.forEach(function (element, id) { element.classList.toggle('selected', id === node.id); });
    $('inspector-kind').textContent = node.isRoot ? '文档' : labels[node.kind];
    $('inspector-title').textContent = node.kind === 'heading' ? node.text : '完整' + labels[node.kind];
    $('inspector-content').textContent = node.kind === 'heading' ? (node.children.filter(function (child) { return child.kind !== 'heading'; }).map(function (child) { return child.text; }).join('\n\n') || '此标题下没有直接内容，展开分支查看子章节。') : node.text;
    $('inspector-line').textContent = '第 ' + node.line + (node.endLine > node.line ? '–' + node.endLine : '') + ' 行';
    $('node-inspector').hidden = false; $('map-settings').hidden = true; $('map-settings-button').setAttribute('aria-expanded', 'false');
    if (state.source && state.followViews) highlightSource(node, false);
    window.dispatchEvent(new CustomEvent('vex-node-selected', { detail: node }));
  }
  function highlightSource(node, focus) {
    var start = state.parsed.lines.slice(0, node.line - 1).join('\n').length + (node.line > 1 ? 1 : 0);
    var length = state.parsed.lines.slice(node.line - 1, node.endLine).join('\n').length;
    input.setSelectionRange(start, start + length);
    var style = getComputedStyle(input);
    input.scrollTop = 0;
    writePeerScroll(document.querySelector('.source-scroll'), (node.line - 1) * (parseFloat(style.lineHeight) || 23) + parseFloat(style.paddingTop) - 14);
    if (focus) input.focus({ preventScroll: true }); updateCursor();
  }
  function locate(node) {
    if (window.VexWorkspace) { VexWorkspace.navigate(node.line); return; }
    if (state.view === 'mindmap') { state.source = true; applySource(); highlightSource(node, true); }
    else if (state.editMode === 'instant') { setView('edit'); instant.focusAt(node.line); }
    else { setView('edit'); highlightSource(node, true); }
  }
  function applySource() {
    $('source-pane').hidden = !state.source; $('map-source-toggle').classList.toggle('on', state.source);
    $('map-source-toggle').setAttribute('aria-pressed', String(state.source));
    window.dispatchEvent(new CustomEvent('vex-source-toggled', { detail: state.source }));
    requestAnimationFrame(function () { fit(true); });
  }
  function syncSettings() {
    $('map-layout').value = settings.layout;
    $('summary-length').value = settings.length; $('show-paragraphs').checked = settings.paragraphs;
    $('show-lists').checked = settings.lists; $('show-blocks').checked = settings.blocks;
    $('summary-label').textContent = !settings.paragraphs && !settings.lists && !settings.blocks ? '仅显示标题' : '摘要 ' + settings.length + ' 字';
    $('summary-example').textContent = MM.clip('把注意力留给内容。写作时保留必要工具，阅读和梳理结构时让文档占据更多空间。', settings.length);
    try { VexStorage.setItem('vex-design-mindmap', JSON.stringify(settings)); } catch (_) {}
    renderMap(); requestAnimationFrame(function () { fit(settings.layout === 'right'); });
  }
  $('map-layout').innerHTML = VexMapLayout.items.map(function (item) { return '<option value="' + item.key + '">' + item.label + '</option>'; }).join('');
  $('map-layout').onchange = function () { settings.layout = this.value; syncSettings(); };
  $('summary-length').addEventListener('input', function () {
    var value = Number(this.value); if (!Number.isInteger(value) || value < 1 || value > 120) { this.setCustomValidity('请输入 1–120 之间的整数'); return; }
    this.setCustomValidity(''); settings.length = value; syncSettings();
  });
  $('summary-length').addEventListener('blur', function () { this.value = settings.length; this.setCustomValidity(''); });
  [['show-paragraphs', 'paragraphs'], ['show-lists', 'lists'], ['show-blocks', 'blocks']].forEach(function (pair) { $(pair[0]).onchange = function () { settings[pair[1]] = this.checked; syncSettings(); }; });
  function download(content, name, type) {
    var url = URL.createObjectURL(new Blob([content], { type: type || 'text/plain;charset=utf-8' }));
    var link = document.createElement('a'); link.href = url; link.download = name; link.click(); setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
  }
  function save() {
    instant.finish();
    refreshNow();
    try {
      if (state.failNextSave) { state.failNextSave = false; throw new Error('下载未能开始'); }
      download(input.value, docs[state.doc].name);
      docs[state.doc].text = input.value; delete docs[state.doc].pendingText; delete docs[state.doc].saveError;
      docs[state.doc].lastExport = new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' });
      state.dirty = false; refresh(); window.dispatchEvent(new CustomEvent('vex-workflow-result',{detail:{kind:'save',ok:true}})); VexProto.toastKey('ToastCopyDownloadStarted'); return true;
    } catch (_) {
      docs[state.doc].saveError = true; updateMeta(); window.dispatchEvent(new CustomEvent('vex-workflow-result',{detail:{kind:'save',ok:false}})); VexProto.toastKey('ToastCopyDownloadFailed'); return false;
    }
  }
  /* 另存为：浏览器无系统保存框，用文件名输入代替目的地选择；取消不产生成功提示。 */
  function saveAs() {
    instant.finish();
    refreshNow();
    var name = window.prompt('另存为（副本将以下述文件名下载）', docs[state.doc].name);
    if (name === null) return false;
    name = name.trim();
    if (!name) { VexProto.toastKey('ToastSaveAsEmptyName'); return false; }
    if (!/\.(md|markdown|mdown|txt)$/i.test(name)) name += '.md';
    try {
      if (state.failNextSave) { state.failNextSave = false; throw new Error('下载未能开始'); }
      download(input.value, name);
      docs[state.doc].lastExport = new Date().toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' });
      VexProto.toastKey('ToastCopyDownloadNamed', name); return true;
    } catch (_) {
      docs[state.doc].saveError = true; updateMeta();
      window.dispatchEvent(new CustomEvent('vex-workflow-result', { detail: { kind: 'save', ok: false } }));
      VexProto.toastKey('ToastSaveAsFailed'); return false;
    }
  }
  function exportHtml() {
    try {
      instant.finish();refreshNow();
      if(state.failNextExport){state.failNextExport=false;throw new Error('Export could not start');}
      download('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>' + VexEsc(docs[state.doc].name) + '</title><style>body{max-width:780px;margin:50px auto;font:16px/1.9 system-ui;padding:0 24px;color:#242933;background:#fff}pre{padding:16px;background:#f4f5f7;overflow:auto}table{border-collapse:collapse}td,th{border:1px solid #ddd;padding:6px 12px}blockquote{border-left:3px solid #888;padding-left:16px;color:#666}' + VexTypography.exportStyles() + VexWatermark.exportStyles + '</style><body>' + $('markdown-preview').innerHTML + VexWatermark.exportMark() + '</body></html>', docs[state.doc].name.replace(/\.[^.]+$/, '') + '.html', 'text/html;charset=utf-8');
      delete docs[state.doc].exportError;
      window.dispatchEvent(new CustomEvent('vex-workflow-result',{detail:{kind:'export',ok:true}}));
      VexProto.toastKey('ToastHtmlCopyStarted');return true;
    } catch (_) {
      docs[state.doc].exportError=true;
      window.dispatchEvent(new CustomEvent('vex-workflow-result',{detail:{kind:'export',ok:false}}));
      VexProto.toastKey('ToastHtmlExportFailed');return false;
    }
  }
  function insert(text, activate) {
    if (activate !== false && state.view !== 'edit') setView('edit');
    if (state.view === 'edit' && state.editMode === 'instant') {
      if (instant.insert(text)) return;
      remember(input.value);
      input.setRangeText(text, input.selectionStart, input.selectionEnd, 'end');
      input.dispatchEvent(new Event('input')); refreshNow();
      instant.focusAt(input.value.slice(0, input.selectionEnd).split('\n').length); return;
    }
    remember(input.value);
    input.focus();
    if (!document.execCommand('insertText', false, text)) input.setRangeText(text, input.selectionStart, input.selectionEnd, 'end');
    input.dispatchEvent(new Event('input'));
  }
  function contentCount() { return VexWriting.metrics(input.value).count; }
  function revealMatch(index) {
    var term = $('find-input').value;
    input.setSelectionRange(index, index + term.length);
    if (input.getClientRects().length) input.focus();
    var line = input.value.slice(0, index).split('\n').length;
    state.editingLine = line;
    syncPeerViewsToLine(line, null, true); updateCursor();
    ['instant-content', 'markdown-preview'].forEach(function (id) {
      var block = state.parsed.blocks.find(function (item) { return item.line <= line && item.endLine >= line; });
      var element = block && $(id).querySelector('[data-line="' + block.line + '"]');
      if (element) { element.classList.remove('located'); void element.offsetWidth; element.classList.add('located'); }
    });
  }
  function reportFindMiss() { var miss = $('find-count'); miss.textContent = '没有匹配内容'; miss.hidden = false; }
  function findNext() {
    var term = $('find-input').value;
    if (!term) return;
    var index = input.value.indexOf(term, input.selectionEnd); if (index < 0) index = input.value.indexOf(term);
    if (index < 0) { reportFindMiss(); return; }
    revealMatch(index);
  }
  function findPrev() {
    var term = $('find-input').value;
    if (!term) return;
    var index = input.value.lastIndexOf(term, Math.max(0, input.selectionStart - 1)); if (index < 0) index = input.value.lastIndexOf(term);
    if (index < 0) { reportFindMiss(); return; }
    revealMatch(index);
  }
  function updateFind() {
    var term = $('find-input').value, count = term ? input.value.split(term).length - 1 : 0;
    var badge = $('find-count');
    badge.textContent = term ? (count ? count + ' 处匹配' : '当前正文无匹配') : '';
    badge.hidden = !badge.textContent;
  }
  $('find-input').oninput = updateFind; $('find-input').onkeydown = function (event) { if (event.key !== 'Enter') return; event.preventDefault(); if (event.shiftKey) findPrev(); else findNext(); };
  $('open-file-input').onchange = async function () {
    var file = this.files[0]; if (!file) return;
    try {
      var text = await file.text();
      confirmSwitch(function () { if (state.emptyWorkspace) docs = []; state.emptyWorkspace = false; docs.push({ name: file.name, text: text, path: file.name }); loadDoc(docs.length - 1); VexProto.toastKey('ToastOpenedFile', file.name); });
    } catch (_) { VexProto.toastKey('ToastFileReadFailed'); }
    this.value = '';
  };
  $('open-folder-input').onchange = async function () {
    var files = Array.from(this.files || []).filter(function (file) { return /\.(md|markdown|mdown|txt)$/i.test(file.name); });
    if (!files.length) { VexProto.toastKey('ToastNoMarkdownFiles'); this.value = ''; return; }
    try {
      var loaded = await Promise.all(files.map(async function (file) { return { name: file.name, path: file.webkitRelativePath.split('/').slice(1).join('/'), text: await file.text() }; }));
      var folder = (files[0].webkitRelativePath || '').split('/')[0] || 'Markdown';
      confirmSwitch(function () {
        docs = loaded; state.doc = 0; state.dirty = false; state.emptyWorkspace = false; workspaceFolder = folder;
        loadDoc(0); VexProto.toastKey('ToastOpenedFolder', loaded.length + ' 个 Markdown 文件');
      });
    } catch (_) { VexProto.toastKey('ToastFolderReadFailed'); }
    this.value = '';
  };
  var snippets = { paragraph: '\n\n新段落\n\n', heading: '\n## 新章节\n\n', list: '\n- 列表项\n', ordered: '\n1. 列表项\n', task: '\n- [ ] 待办事项\n', quote: '\n> 引用内容\n', code: '\n```\n代码内容\n```\n', math: '\n$$\nx^2 + y^2 = z^2\n$$\n', rule: '\n\n---\n\n', 'inline-code': '`代码`', table: '\n| 列一 | 列二 |\n| --- | --- |\n| 内容 | 内容 |\n', link: '[链接文字](https://example.com)', bold: '**文字**', italic: '*文字*', image: '![图片描述](image.png)' };
  Object.assign(snippets,{mermaid:'\n```mermaid\nflowchart LR\n  A[写下想法] --> B[看见脉络]\n```\n',footnote:'[^1]\n\n[^1]: 脚注内容\n',strike:'~~文字~~',highlight:'==文字==',superscript:'^上标^',subscript:'~下标~'});
  VexMermaid.items.forEach(function (item) { snippets['mermaid-' + item.key] = VexMermaid.snippet(item.key); });
  [1,2,3,4,5,6].forEach(function (level) { snippets['heading' + level] = '\n' + '#'.repeat(level) + ' 新章节\n\n'; });
  document.getElementById('format-toolbar').addEventListener('mousedown', function (event) { if (event.target.closest('.format-button')) event.preventDefault(); });
  function runAction(action, item) {
    // 动作注册表：一个动作对应一个处理函数；动作体在 click 与快捷键派发时实时求值。未收录的动作返回 false。
    var actionHandlers = {
      "toggle-source": function (action, item) {
        setEditMode(state.editMode === 'source' ? 'instant' : 'source');
      },
      "clear-file-search": function (action, item) {
        $('file-filter').value = ''; renderFiles(); $('file-filter').focus();
      },
      "clear-outline-search": function (action, item) {
        $('outline-filter').value = ''; renderOutline(); $('outline-filter').focus();
      },
      "toggle-structure": function (action, item) {
        showStructure(structureSidebar.classList.contains('collapsed'));
      },
      "files-mode": function (action, item) {
        fileListMode = 'tree'; alphaSort = 'default'; if (!bundledDocument) VexStorage.setItem('vex-design-file-list-mode', 'tree'); renderFiles();
      },
      "files-sort": function (action, item) {
        alphaSort = alphaSort === 'default' ? 'asc' : alphaSort === 'asc' ? 'desc' : 'default'; fileListMode = alphaSort === 'default' ? 'tree' : 'list'; if (!bundledDocument) VexStorage.setItem('vex-design-file-list-mode', fileListMode); renderFiles();
      },
      "files-fold": function (action, item) {
        var groups = document.querySelectorAll('#file-list details.file-directory'); var anyOpen = Array.prototype.some.call(groups, function (g) { return g.open; }); groups.forEach(function (g) { g.open = !anyOpen; }); syncFilesToolbar();
      },
      "file-actions": function (action, item) {
        $('file-navigation-menu').hidden = !$('file-navigation-menu').hidden; document.querySelector('[data-action=file-actions]').setAttribute('aria-expanded', String(!$('file-navigation-menu').hidden));
      },
      "refresh-files": function (action, item) {
        renderFiles();
      },
      "collapse-files": function (action, item) {
        document.querySelectorAll('#file-list details.file-directory[open]').forEach(function (group) { group.open = false; }); renderFiles();
      },
      "sort-files": function (action, item) {
        fileDescending = !fileDescending; renderFiles();
      },
      "new": function (action, item) {
        if (state.emptyWorkspace) { state.emptyWorkspace = false; docs[0].placeholder = false; loadDoc(0); window.dispatchEvent(new CustomEvent('vex-content-changed')); } else if (!VexDocumentWindow.open({document:{name:'未命名文档.md',text:'',isNew:true}})) VexProto.toastKey('ToastAllowPopups');
      },
      "new-window": function (action, item) {
        if (!VexDocumentWindow.open({document:{name:'未命名文档.md',text:''}})) VexProto.toastKey('ToastAllowPopups');
      },
      "document-window": function (action, item) {
        instant.finish(); if (!VexDocumentWindow.open({document:{name:docs[state.doc].name,text:input.value,readOnly:!!docs[state.doc].readOnly}})) VexProto.toastKey('ToastAllowPopups');
      },
      "exit-app": function (action, item) {
        exitGuard.open();
      },
      "open": function (action, item) {
        if (!VexDocumentWindow.open({intent:'open-file'})) VexProto.toastKey('ToastAllowPopups');
      },
      "open-folder": function (action, item) {
        if (!VexDocumentWindow.open({intent:'open-folder'})) VexProto.toastKey('ToastAllowPopups');
      },
      "open-sample": function (action, item) {
        confirmSwitch(function () { loadDoc(Math.min(item.arg, docs.length - 1)); });
      },
      "open-samples": function (action, item) {
        if (!VexDocumentWindow.open({intent:'samples'})) VexProto.toastKey('ToastAllowPopups');
      },
      "quick-open": function (action, item) {
        if (!state.sidebar) VexProto.handleAction('toggle-sidebar'); VexProto.handleAction('pane-files'); $('file-filter').focus();
      },
      "save": function (action, item) {
        save();
      },
      "save-as": function (action, item) {
        saveAs();
      },
      "save-all": function (action, item) {
        save();
      },
      "copy-wechat": function (action, item) {
        instant.finish(); refreshNow();
        VexSocialCopy.copy({platform:action.slice(5),text:input.value,html:TexEditor.render(input.value),typography:VexTypography.state.key,compact:VexTypography.state.compact,toast:VexProto.toast});
      },
      "export-html": function (action, item) {
        exportHtml();
      },
      "export-pdf": function (action, item) {
        instant.finish(); refreshNow(); VexWatermark.preview('PDF · ' + docs[state.doc].name,$('markdown-preview').innerHTML);
      },
      "toggle-watermark": function (action, item) {
        VexPreferences.set('watermarkPreview',!VexPreferences.values.watermarkPreview);
      },
      "toggle-sidebar": function (action, item) {
        state.sidebar = !state.sidebar; document.querySelector('.sidebar').classList.toggle('closed', !state.sidebar); sidebar.inert = !state.sidebar; if (!state.sidebar) sidebar.style.removeProperty('width'); $('sidebar-toggle').classList.toggle('on', state.sidebar); $('sidebar-toggle').setAttribute('aria-pressed', String(state.sidebar)); $('sidebar-toggle').title = $('sidebar-toggle').ariaLabel = VexL.t(state.sidebar ? '收起文件导航' : '展开文件导航'); syncStructureVisibility();
      },
      "pane-outline": function (action, item) {
        showStructure(true); renderOutline();
      },
      "pane-files": function (action, item) {
        if (!state.sidebar) VexProto.handleAction('toggle-sidebar');
      },
      "map-source": function (action, item) {
        state.source = !state.source; applySource();
      },
      "map-settings": function (action, item) {
        var opened = !$('map-settings').hidden; if (state.view !== 'mindmap') setView('mindmap'); $('map-settings').hidden = opened; $('map-settings-button').setAttribute('aria-expanded', String(!opened)); $('node-inspector').hidden = true;
      },
      "close-settings": function (action, item) {
        $('map-settings').hidden = true; $('map-settings-button').setAttribute('aria-expanded', 'false');
      },
      "reset-settings": function (action, item) {
        settings = { length: 30, paragraphs: true, lists: true, blocks: false, layout: 'right' }; syncSettings();
      },
      "map-collapse": function (action, item) {
        if (state.collapsed.size) state.collapsed.clear(); else state.nodes.filter(function (node) { return !node.isRoot && visibleChildren(node).length; }).forEach(function (node) { state.collapsed.add(node.id); }); $('node-inspector').hidden = true; renderMap(); fit();
      },
      "map-fit": function (action, item) {
        fit();
      },
      "close-inspector": function (action, item) {
        state.selected = null; $('node-inspector').hidden = true; renderMap();
      },
      "locate-source": function (action, item) {
        var node = state.nodes.find(function (node) { return node.id === state.selected; }); if (node) locate(node);
      },
      "find": function (action, item) {
        $('findbar').hidden = false; $('replace-input').hidden = action !== 'replace'; $('replace-all').hidden = action !== 'replace'; $('find-input').focus(); updateFind();
      },
      "close-find": function (action, item) {
        $('findbar').hidden = true; if (window.VexWorkbench) VexWorkbench.returnToEditing();
      },
      "find-next": function (action, item) {
        findNext();
      },
      "find-prev": function (action, item) {
        findPrev();
      },
      "replace-all": function (action, item) {
        var term = $('find-input').value; if (term) { VexWorkbench.applyText(input.value.split(term).join($('replace-input').value)); updateFind(); }
      },
      "toggle-lines": function (action, item) {
        document.querySelector('.source-scroll').classList.toggle('no-lines');
      },
      "toggle-pairs": function (action, item) {
        state.autoPair = !state.autoPair;
      },
      "toggle-focus": function (action, item) {
        state.focus = !state.focus; document.querySelector('.app-window').classList.toggle('focus-mode', state.focus);
      },
      "toggle-typewriter": function (action, item) {
        VexPreferences.set('typewriter', !state.typewriter);
      },
      "toggle-motion": function (action, item) {
        state.motion = !state.motion;
        if (window.VexPreferences) VexPreferences.set('motion', state.motion ? 'full' : 'off');
        else document.documentElement.dataset.motion = String(state.motion);
      },
      "presentation": function (action, item) {
        startPresentation();
      },
      "presentation-larger": function (action, item) {
        state.presentationScale = Math.min(2.5, +(state.presentationScale + .1).toFixed(1)); applyPresentation();
      },
      "presentation-smaller": function (action, item) {
        state.presentationScale = Math.max(1, +(state.presentationScale - .1).toFixed(1)); applyPresentation();
      },
      "presentation-exit": function (action, item) {
        stopPresentation();
      
      },
      "typography": function (action, item) {
        VexTypography.toggle(Array.from(document.querySelectorAll('[data-action="typography"]')).find(function(button){return button.getClientRects().length&&!button.hidden;}));
      },
      "typography-select": function (action, item) {
        VexTypography.choose(item.arg);
      },
      "toggle-compact": function (action, item) {
        VexTypography.toggleCompact();
      },
      "language": function (action, item) {
        state.language = item.arg; VexL.set(item.arg);
      },
      "toggle-word-count": function (action, item) {
        VexPreferences.set('wordCountVisible', !VexPreferences.values.wordCountVisible);
      },
      "maximize": function (action, item) {
        document.querySelector('.app-window').classList.toggle('expanded');
      },
      "toggle-fullscreen": function (action, item) {
        document.querySelector('.app-window').classList.toggle('expanded');
      },
      "toggle-topmost": function (action, item) {
        state.topmost = !state.topmost; VexProto.toast(state.topmost ? '窗口置顶（桌面版操作演示）' : '取消置顶（桌面版操作演示）');
      },
      "minimize": function (action, item) {
        VexProto.toastKey('ToastMinimizeByDesktop');
      },
      "close": function (action, item) {
        confirmSwitch(function () { VexProto.toastKey('ToastDocumentClosed'); });
      },
      "theme": function (action, item) {
        document.querySelector('#vex-theme-panel button').focus();
      },
      "refresh": function (action, item) {
        refreshNow();
      },
      "undo": function (action, item) {
        instant.finish();
        var from = action === 'undo' ? state.undo : state.redo, to = action === 'undo' ? state.redo : state.undo;
        if (from.length) restoreHistory(from, to);
      },
      "copy": function (action, item) {
        var selected = input.value.slice(input.selectionStart, input.selectionEnd), copied = selected || input.value;
        if (action === 'copy-plain') copied = MM.plain(copied);
        navigator.clipboard.writeText(copied).then(function () { if (action === 'cut' && selected) insert('', false); VexProto.toastKey('ToastCopied'); }).catch(function () { VexProto.toastKey('ToastCopyInEditor'); });
      },
      "paste": function (action, item) {
        navigator.clipboard.readText().then(function (text) { insert(text); }).catch(function () { VexProto.toastKey('ToastPasteInEditor'); });
      },
      "select-all": function (action, item) {
        setEditMode('source'); input.focus(); input.select();
      },
      "clear-format": function (action, item) {
        var selection = input.value.slice(input.selectionStart, input.selectionEnd); if (selection) insert(MM.plain(selection)); else VexProto.toastKey('ToastSelectTextToClear');
      },
      "guide": function (action, item) {
        if (window.VexGuide) VexGuide.start();
      },
      "print": function (action, item) {
        VexProto.toastKey('ToastPrintPreviewByDesktop');
      }
    };
    actionHandlers["copy-zhihu"] = actionHandlers["copy-wechat"];
    actionHandlers["copy-juejin"] = actionHandlers["copy-wechat"];
    actionHandlers["replace"] = actionHandlers["find"];
    actionHandlers["redo"] = actionHandlers["undo"];
    actionHandlers["copy-plain"] = actionHandlers["copy"];
    actionHandlers["cut"] = actionHandlers["copy"];
    var handler = actionHandlers[action];
    if (!handler) return false;
    handler(action, item);
    return true;
  }

  VexProto.handleAction = function (action, item) {
    if (elementMenu && elementMenu.handleAction(action === 'insert-table' ? 'element-insert-table' : action)) return true;
    if (action === 'preferences' || action === 'preferences-mcp' || action === 'mcp-audit' || action === 'dlg-mcp') { VexPreferences.open(action === 'preferences' ? 'general' : 'mcp'); if (action === 'mcp-audit') document.querySelector('[data-mcp-tab=audit]')?.click(); return true; }
    if (input.readOnly && (action.startsWith('insert-') || ['save','save-as','save-all','undo','redo','cut','paste','replace-all','clear-format','rename','delete-chapter','add-child','add-sibling','move-up','move-down','promote','demote'].includes(action))) { VexProto.toastKey('ToastDocumentReadOnly'); return true; }
    if (window.VexWorkspace && VexWorkspace.handleAction(action, item)) return true;
    if (action.startsWith('insert-')) { if (state.editMode === 'instant' && instant.format(action.slice(7))) return true; insert(snippets[action.slice(7)] || ''); return true; }
    if (action.startsWith('view-')) { setView(action.slice(5)); return true; }
    if (action === 'edit-instant' || action === 'edit-source') { setEditMode(action.slice(5)); return true; }
    if (action.startsWith('dlg-')) {
      if (action === 'dlg-statistics') {
        refreshNow();
        var stats = document.querySelectorAll('#layer-statistics .stat-card .num');
        if (stats[0]) stats[0].textContent = contentCount().toLocaleString();
        var counts = VexWriting.metrics(input.value);
        if (stats[1]) stats[1].textContent = counts.characters.toLocaleString();
        document.querySelectorAll('#layer-statistics .stat-card .lbl')[0].textContent = '字数（中文按字，英文按词）';
        document.querySelectorAll('#layer-statistics .stat-card .lbl')[1].textContent = '正文字符（不含空白）';
        var metrics = [['汉字等单字数', counts.cjkCount], ['英文及数字词数', counts.wordCount], ['源码字符', MM.characters(input.value).length], ['行数', state.parsed.lines.length], ['段落', state.parsed.blocks.filter(function (block) { return block.kind === 'paragraph'; }).length], ['标题', state.parsed.blocks.filter(function (block) { return block.kind === 'heading'; }).length], ['阅读时间', '约 ' + Math.max(1, Math.ceil(contentCount() / 350)) + ' 分钟'], ['编码', 'UTF-8'], ['状态', state.dirty ? '已修改' : '未修改']];
        document.querySelector('#layer-statistics .stat-rows').innerHTML = metrics.map(function (metric) { return '<div class="stat-row"><span class="k">' + VexEsc(metric[0]) + '</span><span class="v">' + VexEsc(metric[1]) + '</span></div>'; }).join('');
      }
      if (action === 'dlg-properties') {
        refreshNow();
        document.querySelector('#layer-properties .prop-name').textContent = docs[state.doc].name;
        var properties = [['状态', state.dirty ? '已修改 · 尚未保存' : '未修改'], ['格式', 'Markdown'], ['编码', 'UTF-8'], ['大小', new Blob([input.value]).size.toLocaleString() + ' 字节']];
        document.querySelector('#layer-properties .prop-rows').innerHTML = properties.map(function (property) { return '<div class="prop-row"><span class="k">' + VexEsc(property[0]) + '</span><span class="v">' + VexEsc(property[1]) + '</span></div>'; }).join('');
      }
      if (action === 'dlg-delete') {
        document.querySelector('#layer-delete .dlg-title').textContent = '从工作区移除「' + docs[state.doc].name + '」？';
        document.querySelector('#layer-delete .dlg-msg').textContent = '原型仅移除工作区中的文档，原文件不会改动。' + (state.dirty ? '当前文档有未保存的修改，移除后这些修改将被丢弃。' : '');
        document.querySelector('#layer-delete .dlg-path').textContent = docs[state.doc].name;
      }
      if (action === 'dlg-doc') {
        if (!VexDocumentWindow.open({builtin:item && item.arg === '鸣谢' ? 'thanks' : 'changelog'})) VexProto.toastKey('ToastAllowPopups');
      } else openLayer(action.slice(4));
      return true;
    }
    return runAction(action, item);
  };
  VexProto.isSidebarVisible = function () { return state.sidebar; };
  // 原型的布局与章节操作共用此文档接口；正文仍只有一份。
  window.VexWorkbench = {
    get state() { return state; }, get settings() { return settings; },
    get text() { return input.value; }, get documents() { return docs; },
    get name() { return docs[state.doc].name; },
    finish: function () { instant.finish(); refreshNow(); },
    setView: setView, setEditMode: setEditMode, fit: fit, renderMap: renderMap,
    selectNode: selectNode, locate: locate, highlightSource: highlightSource,
    syncViewsToLine: function (line) { syncPeerViewsToLine(line || activeDocumentLine, null, true); },
    setFollow: function (value) { state.followViews = value; if (value) syncPeerViewsToLine(state.editingLine, null, true); window.dispatchEvent(new CustomEvent('vex-follow-changed')); },
    returnToEditing: function () { syncPeerViewsToLine(state.editingLine, null, true); if (state.editMode === 'instant') instant.focusAt(state.editingLine); else input.focus({ preventScroll: true }); },
    syncStructure: syncStructureVisibility, showStructure: showStructure,
    focusLine: function (line) {
      var node = state.nodes.find(function (candidate) { return candidate.line === line; });
      if (!node) return;
      if (state.editMode === 'instant') instant.focusAt(line); else highlightSource(node, true);
    },
    loadExternalVersion: function(text) { instant.finish();remember(input.value);docs[state.doc].text=text;delete docs[state.doc].pendingText;delete docs[state.doc].saveError;input.value=text;input.dispatchEvent(new Event('input'));refreshNow(); },
    recoverDrafts: function(recovered) { instant.finish();docs[state.doc].pendingText=input.value;var first=docs.length;recovered.forEach(function(draft){docs.push({name:draft.name.replace(/(\.md)?$/,'.恢复.md'),text:'',pendingText:draft.text,isNew:true,recovered:true});});loadDoc(first); },
    loadDocument: function (index) { confirmSwitch(function () { loadDoc(index); }); },
    applyText: function (text, options) {
      if (input.readOnly) return;
      options = options || {}; instant.finish();
      if (text === input.value) return;
      if (options.history !== false) remember(input.value);
      input.value = text; input.dispatchEvent(new Event('input')); refreshNow();
      if (options.line) {
        var node = state.nodes.find(function (candidate) { return candidate.kind === 'heading' && candidate.line === options.line; });
        if (node) selectNode(node);
      }
    },
    history: function (action) {
      if (input.readOnly) return;
      instant.finish(); var from = action === 'undo' ? state.undo : state.redo;
      var to = action === 'undo' ? state.redo : state.undo;
      if (!from.length) return;
      restoreHistory(from, to);
    },
    snapshot: function () { return { name: docs[state.doc].name, text: input.value, savedText: docs[state.doc].text, index: state.doc, dirty: state.dirty }; },
    receive: function (snapshot) {
      var changedDocument = docs[state.doc].name !== snapshot.name || state.remoteDocumentIndex !== snapshot.index;
      state.remoteDocumentIndex = snapshot.index;
      if (!changedDocument && input.value === snapshot.text) {
        docs[state.doc].text = snapshot.savedText; state.dirty = snapshot.dirty; updateMeta(); return;
      }
      instant.finish(); clearTimeout(state.pending);
      docs = [{ name: snapshot.name, text: snapshot.savedText }]; state.doc = 0;
      input.value = snapshot.text; state.dirty = snapshot.dirty;
      $('document-name').textContent = snapshot.name;
      if (changedDocument) {
        state.lastDoc = -1; state.collapsed.clear(); state.selected = null; state.undo = []; state.redo = [];
        input.setSelectionRange(0, 0);
        ['.source-scroll', '.reading-scroll', '.instant-scroll'].forEach(function (selector) { document.querySelector(selector).scrollTop = 0; });
      }
      renderFiles(); refreshNow();
      if (changedDocument) requestAnimationFrame(function () { fit(true); });
    }
  };
  // 引导演练使用临时文档；结束时还原进入前的内容、视图和显示偏好。
  VexProto.beginGuideSession = function () {
    instant.finish(); refreshNow(); closeLayers(); VexProto.closeMenu();
    var snapshot = {
      // Keep the complete in-memory workspace so the guide's temporary
      // document and any in-memory changes are fully restored on exit.
      documents: docs.map(function (doc) { return Object.assign({}, doc); }),
      workspaceFolder: workspaceFolder,
      doc: state.doc, text: input.value, dirty: state.dirty, view: state.view, editMode: state.editMode,
      sidebar: state.sidebar, focus: state.focus, source: state.source,
      followViews: state.followViews, editingLine: state.editingLine, emptyWorkspace: state.emptyWorkspace,

      fileFilter: $('file-filter').value,
      countHidden: $('sb-wordcount').hidden, findHidden: $('findbar').hidden,
      typography: Object.assign({}, VexTypography.state), settings: Object.assign({}, settings),
      undo: state.undo.slice(), redo: state.redo.slice(), selection: [input.selectionStart, input.selectionEnd],
      scrolls: ['.instant-scroll', '.reading-scroll', '.source-scroll'].map(function (selector) { return document.querySelector(selector).scrollTop; }),
      collapsed: new Set(state.collapsed), selected: state.selected, map: { x: state.x, y: state.y, scale: state.scale, visited: state.mapVisited },
      editPreference: VexStorage.getItem('vex-design-edit-method')
    };
    state.focus = false; document.querySelector('.app-window').classList.remove('focus-mode');
    state.emptyWorkspace = false; state.followViews = true;
    $('sb-wordcount').hidden = false; $('findbar').hidden = true;
    if (!state.sidebar) VexProto.handleAction('toggle-sidebar');
    $('file-filter').value = ''; filterFiles();
    VexProto.handleAction('pane-files'); state.source = false; state.editMode = 'instant';
    settings = { length: 30, paragraphs: true, lists: true, blocks: false, layout: 'right' };
    snapshot.demo = { name: '一分钟上手 · 演练.md', text: '# 让一个想法，长成一篇文章\n\n我想把零散的想法写下来，再用结构把它们连成一条清晰的线索。先写内容，之后慢慢整理。\n\n## 写下想法\n\n点击这段文字试着修改，也可以用 **加粗** 标记重点。\n\n## 看清结构\n\n标题成为导图的主干，段落提供摘要。显示文字可以变短，完整内容一直保留。\n\n## 准备分享\n\n选择舒服的排版，确认保存状态，再把文章导出给读者。' };
    docs.push(snapshot.demo); loadDoc(docs.length - 1); syncSettings(); setView('edit');
    return snapshot;
  };
  VexProto.endGuideSession = function (snapshot) {
    state.guideRestoring = true; document.querySelector('.app-window').classList.add('guide-restoring');
    instant.finish(); VexProto.closeMenu(); closeLayers();
    docs = snapshot.documents.map(function (doc) { return Object.assign({}, doc); });
    workspaceFolder = snapshot.workspaceFolder;
    state.emptyWorkspace = snapshot.emptyWorkspace; state.followViews = snapshot.followViews;
    loadDoc(Math.max(0, Math.min(snapshot.doc, docs.length - 1))); input.value = snapshot.text; state.dirty = snapshot.dirty;
    $('file-filter').value = snapshot.fileFilter; filterFiles();
    state.editMode = snapshot.editMode; state.source = snapshot.source;
    state.focus = snapshot.focus; document.querySelector('.app-window').classList.toggle('focus-mode', snapshot.focus);
    if (state.sidebar !== snapshot.sidebar) VexProto.handleAction('toggle-sidebar');

    $('sb-wordcount').hidden = snapshot.countHidden; $('findbar').hidden = snapshot.findHidden;
    settings = snapshot.settings; syncSettings();
    VexTypography.state.compact = snapshot.typography.compact; VexTypography.choose(snapshot.typography.key);
    try { if (snapshot.editPreference === null) VexStorage.removeItem('vex-design-edit-method'); else VexStorage.setItem('vex-design-edit-method', snapshot.editPreference); } catch (_) {}
    refreshNow(); state.collapsed = snapshot.collapsed; state.undo = snapshot.undo; state.redo = snapshot.redo; state.editingLine = snapshot.editingLine;
    window.dispatchEvent(new CustomEvent('vex-content-changed')); window.dispatchEvent(new CustomEvent('vex-follow-changed'));
    state.mapVisited = snapshot.map.visited; setView(snapshot.view);
    $('typography-panel').hidden = true; $('node-inspector').hidden = true;
    requestAnimationFrame(function () {
      state.x = snapshot.map.x; state.y = snapshot.map.y; state.scale = snapshot.map.scale; renderMap();
      lastCanvasSize = { width: canvas.clientWidth, height: canvas.clientHeight }; transform();
      input.setSelectionRange(snapshot.selection[0], snapshot.selection[1]); updateCursor();
      ['.instant-scroll', '.reading-scroll', '.source-scroll'].forEach(function (selector, scrollIndex) { document.querySelector(selector).scrollTop = snapshot.scrolls[scrollIndex]; });
      requestAnimationFrame(function () {
        lastCanvasSize = { width: canvas.clientWidth, height: canvas.clientHeight };
        state.guideRestoring = false; document.querySelector('.app-window').classList.remove('guide-restoring');
      });
    });
  };
  VexProto.arrangeGuideMap = function () {
    if (state.view !== 'mindmap') return;
    var element = document.querySelector('.map-node[data-kind="paragraph"]');
    var panel = !$('map-settings').hidden ? $('map-settings') : !$('node-inspector').hidden ? $('node-inspector') : null;
    if (!element || !panel) return;
    var rect = element.getBoundingClientRect(), popover = panel.getBoundingClientRect(), bounds = canvas.getBoundingClientRect();
    var desired = Math.max(bounds.left + 20, popover.left - rect.width - 36);
    if (Math.abs(desired - rect.left) > 1) { state.x += desired - rect.left; transform(); }
  };
  VexProto.isActionChecked = function (action, item) {
    var checks = {
      'edit-instant': state.editMode === 'instant', 'edit-source': state.editMode === 'source',
      'toggle-pairs': state.autoPair, 'toggle-focus': state.focus,
      'toggle-typewriter': state.typewriter, 'toggle-motion': state.motion, 'toggle-topmost': state.topmost,
      'toggle-fullscreen': document.querySelector('.app-window').classList.contains('expanded'),
      'toggle-compact': VexTypography.state.compact, 'typography-select': !!item && VexTypography.state.key === item.arg,
      'toggle-watermark': VexPreferences.values.watermarkPreview,
      'language': !!item && state.language === item.arg
    };
    if (Object.prototype.hasOwnProperty.call(checks, action)) return checks[action];
    return action.startsWith('view-') ? state.view === action.slice(5) : action === 'toggle-sidebar' ? state.sidebar
      : action === 'toggle-lines' ? !document.querySelector('.source-scroll').classList.contains('no-lines')
      : action === 'toggle-word-count' ? !$('sb-wordcount').hidden : false;
  };
  document.addEventListener('click', function (event) {
    var button = event.target.closest('[data-action]'); if (button) { event.stopPropagation(); VexProto.handleAction(button.dataset.action); }
    else if (!$('map-settings').contains(event.target)) { $('map-settings').hidden = true; $('map-settings-button').setAttribute('aria-expanded', 'false'); }
  });
  document.addEventListener('click', function (event) {
    var link = event.target.closest('#markdown-preview a[href^="#"]');
    if (!link) return;
    var href = link.getAttribute('href') || '', target = document.getElementById(href.slice(1));
    if (!target) return;
    event.preventDefault();
    target.scrollIntoView({ block: 'start', behavior: reducedMotion() ? 'auto' : 'smooth' });
    target.classList.remove('located'); void target.offsetWidth; target.classList.add('located');
  });
  document.querySelectorAll('.win-layer').forEach(function (layer) {
    layer.onclick = function (event) { if (event.target === layer) { closeLayers(); pendingSwitch = null; restoreSwitchOrigin(); } };
    layer.querySelectorAll('[data-close]').forEach(function (button) {
      button.onclick = function () {
        if (layer.id === 'layer-delete' && button.classList.contains('danger')) {
          closeLayers(); docs.splice(state.doc, 1);
          if (!docs.length) docs.push({ name: '未命名文档.md', text: '' });
          state.lastDoc = -1; state.lastText = null; state.dirty = false; loadDoc(Math.min(state.doc, docs.length - 1)); return;
        }
        if (layer.id === 'layer-unsaved' && pendingSwitch) {
          var next = pendingSwitch; pendingSwitch = null;
          var intent = button.dataset.intent || (button.classList.contains('caption-btn') ? 'cancel' : null);
          if (intent === 'cancel' || !intent) { closeLayers(); restoreSwitchOrigin(); return; }
          if (intent === 'save' && !save()) { closeLayers(); restoreSwitchOrigin(); return; }
          switchOrigin=null;closeLayers(); next(); return;
        }
        closeLayers(); if (button.dataset.close) VexProto.toast(button.dataset.close);
      };
    });
  });
  // 保留对话框的可点击演示。
  if ($('err-toggle')) $('err-toggle').onclick = function () { $('err-detail').style.display = $('err-detail').style.display === 'none' ? '' : 'none'; };
  ['mcp-copy-token', 'mcp-copy-endpoint'].forEach(function (id) { if ($(id)) $(id).onclick = function () { navigator.clipboard.writeText(id.endsWith('token') ? $('mcp-token').value : $('mcp-endpoint') ? $('mcp-endpoint').textContent : 'http://127.0.0.1:17891/mcp/').catch(function () { VexProto.toastKey('ToastCopyManually'); }); }; });
  if ($('mcp-switch')) $('mcp-switch').onclick = function () { this.classList.toggle('on'); this.setAttribute('aria-checked', String(this.classList.contains('on'))); $('mcp-form').style.opacity = this.classList.contains('on') ? '' : '.45'; $('mcp-status-text').textContent = this.classList.contains('on') ? '服务运行中（演示）' : '服务已停止（演示）'; };
  if ($('mcp-save')) $('mcp-save').onclick = function () { closeLayers(); VexProto.toastKey('ToastMcpSettingsSaved'); };
  if ($('mcp-gen')) $('mcp-gen').onclick = function () { $('mcp-token').value = 'vx_' + crypto.randomUUID().replace(/-/g, ''); };
  document.addEventListener('keydown', function (event) {
    if (event.key === 'F6' && !event.isComposing) { event.preventDefault(); startPresentation(); return; }
    if (event.key === 'Escape') {
      if (event.defaultPrevented) return;
      if (state.presentation) { event.preventDefault(); stopPresentation(); return; }
      var anyLayerOpen = Array.prototype.some.call(document.querySelectorAll('.win-layer'), function (layer) { return !layer.hidden; });
      if (anyLayerOpen) { event.preventDefault(); closeLayers(); pendingSwitch = null; restoreSwitchOrigin(); return; }
      if (!$('node-inspector').hidden && !window.VexAuxiliary) { event.preventDefault(); $('node-inspector').hidden = true; return; }
      if (!$('map-settings').hidden) { event.preventDefault(); $('map-settings').hidden = true; $('map-settings-button').setAttribute('aria-expanded', 'false'); return; }
      if (!$('findbar').hidden) { event.preventDefault(); $('findbar').hidden = true; if (window.VexWorkbench) VexWorkbench.returnToEditing(); }
    }
    if ((event.ctrlKey || event.metaKey) && ['s', 'f', 'h', 'o', 'n', 'w', 'p', 'b', 'i'].includes(event.key.toLowerCase())) {
      event.preventDefault(); VexProto.handleAction(event.key.toLowerCase() === 'p' ? (event.shiftKey ? 'print' : 'quick-open') : event.key.toLowerCase() === 'n' && event.shiftKey ? 'new-window' : event.key.toLowerCase() === 's' && event.shiftKey ? 'save-as' : { s: 'save', f: 'find', h: 'replace', o: 'open', n: 'new', w: 'close', b: 'insert-bold', i: 'insert-italic' }[event.key.toLowerCase()]);
    }
    /* 菜单里写出的编辑快捷键要真的能用：正文编辑区内交给浏览器原生，正文之外由应用动作兜底。 */
    if ((event.ctrlKey || event.metaKey) && !event.altKey && !event.isComposing && !overlayOpen() && !window.VexPresentation?.active) {
      var editKey = event.key.toLowerCase();
      var inDocument = event.target === input || $('instant-content').contains(event.target);
      if (inDocument && !event.shiftKey && (editKey === 'z' || editKey === 'y')) { event.preventDefault(); VexProto.handleAction(editKey === 'z' ? 'undo' : 'redo'); return; }
      if (inDocument && event.shiftKey && editKey === 'c') { event.preventDefault(); VexProto.handleAction('copy-plain'); return; }
      if (!event.shiftKey && ['x', 'c', 'v', 'a'].includes(editKey) && !event.target.closest('input, textarea, select, [contenteditable="true"]')) { event.preventDefault(); VexProto.handleAction({ x: 'cut', c: 'copy', v: 'paste', a: 'select-all' }[editKey]); return; }
    }
    if (event.key === 'F8') { event.preventDefault(); VexProto.handleAction('toggle-focus'); }
    if (event.key === 'F11') { event.preventDefault(); VexProto.handleAction('toggle-fullscreen'); }
    if (event.altKey && event.key === 'Enter') { event.preventDefault(); VexProto.handleAction('dlg-properties'); }
    if (!event.ctrlKey && !event.metaKey && event.key === '/' && !event.target.closest('input, textarea, [contenteditable="true"]')) { event.preventDefault(); if (!state.sidebar) VexProto.handleAction('toggle-sidebar'); VexProto.handleAction('pane-files'); $('file-filter').focus(); }
  });
  document.querySelector('.titlebar-document').addEventListener('click', function () { window.dispatchEvent(new Event('vex-open-save-details')); });
  VexProto.attachMenuBar(document.querySelector('.tb-menu'));
  var closedWindow = document.createElement('section');closedWindow.className='prototype-window-closed';closedWindow.hidden=true;
  closedWindow.innerHTML='<img src="assets/vex-logo.svg" alt=""><h2>窗口已关闭</h2><button class="btn primary">重新打开</button>';
  document.querySelector('.window-body').appendChild(closedWindow);
  var exitGuard = VexExitDialog.create({
    parent:document.querySelector('.window-body'),
    beforeOpen:function(){instant.finish();refreshNow();closeLayers();VexProto.closeMenu();VexTypography.close();window.VexWorkspace?.closeAppearance();},
    /* 单文档定位：切换文档必经保存确认，退出时只有当前文档可能未保存。 */
    documents:function(){return state.dirty?[{index:state.doc,name:docs[state.doc].name,path:docs[state.doc].path,text:input.value,savedText:docs[state.doc].text}]:[];},
    edit:function(doc){docs[state.doc].pendingText=input.value;if(doc.index!==state.doc)loadDoc(doc.index);if(window.VexWorkspace)VexWorkspace.setLayout('instant');else setView('edit');},
    save:function(changed){if(state.failNextSave){state.failNextSave=false;throw new Error('Download could not start');}changed.forEach(function(doc){download(doc.text,doc.name);docs[doc.index].text=doc.text;delete docs[doc.index].pendingText;});state.dirty=false;VexStorage.removeItem('vex-design-exit-drafts');refreshNow();},
    discard:function(changed){changed.forEach(function(doc){delete docs[doc.index].pendingText;});input.value=docs[state.doc].text;state.dirty=false;VexStorage.removeItem('vex-design-exit-drafts');refreshNow();},
    close:function(){closedWindow.hidden=false;closedWindow.querySelector('button').focus({preventScroll:true});}
  });
  closedWindow.querySelector('button').onclick=function(){
    closedWindow.hidden=true;
    var stored=VexStorage.getItem('vex-design-exit-drafts');
    if(stored){try{var recovered=JSON.parse(stored);if(!Array.isArray(recovered)||!recovered.every(function(doc){return typeof doc.name==='string'&&typeof doc.text==='string';}))throw new Error('Invalid draft');window.VexWorkflows?.showDrafts(recovered);VexProto.toastKey('ToastRecoveryDraftFound');}catch(_){VexProto.toastKey('ToastDraftUnreadable');}}
  };
  VexProto.previewUnsavedExit=function(){
    docs=[{name:'产品构想.md',path:'写作手记 / 产品构想.md',text:'# 产品构想\n',pendingText:'# 产品构想\n\n让写作更轻松。\n'}];
    loadDoc(0);input.value=docs[0].pendingText;state.dirty=true;refreshNow();exitGuard.open();
  };
  syncSettings(); loadDoc(0); setView(new URLSearchParams(location.search).get('view') || 'edit');
  VexPreferences.configure({
    beforeOpen:function () { VexProto.closeMenu(); closeLayers(); instant.finish(); },
    currentAppearance:function () { return {theme:VexTheme.state.mode,palette:VexTheme.state.palette,typography:VexTypography.state.key,compact:VexTypography.state.compact}; },
    toast:VexProto.toast, getText:function () { return input.value; },
    replaceText:function (text) { if (input.readOnly) return; remember(input.value); input.value = text; input.dispatchEvent(new Event('input')); refreshNow(); },
    apply:function (key,value,all) {
      if (key === 'motion') state.motion = value !== 'off' && value !== 'false';
      if (key === 'theme') VexTheme.setMode(value);
      if (key === 'palette') VexTheme.setPalette(value);
      if (key === 'typography') VexTypography.choose(value);
      if (key === 'uiDensity') VexTheme.setDensity(value);
      if (key === 'mdSize') VexTheme.setMdSize(value);
      if (key === 'compact' && VexTypography.state.compact !== value) VexTypography.toggleCompact();
      if (key === 'language') VexL.set(value);
      if (key === 'font') document.documentElement.dataset.writingFont = value;
      if (key === 'zoom') document.documentElement.style.setProperty('--vex-content-zoom',value / 100);
      if (key === 'lineNumbers') document.querySelector('.source-scroll').classList.toggle('no-lines',!value);
      if (key === 'wordCountVisible') $('sb-wordcount').hidden = !value;
      if (key === 'autoPair') state.autoPair = value;
      if (key === 'typewriter') { state.typewriter = value; typewriter.refresh(); }
      if (key === 'focus') { state.focus = value; document.querySelector('.app-window').classList.toggle('focus-mode',value); }
      if (key === 'toolbarPin') document.documentElement.dataset.toolbarPin = String(value !== false);
      if (['math','chemistry','mermaid','highlight','subscript','softBreak'].includes(key)) { state.lastText = null; cancelAnimationFrame(state.prefRefresh); state.prefRefresh = requestAnimationFrame(refreshNow); }
    }
  });
  if (builtin || launched) document.title = docs[0].name + ' · 维刻 Vex';
  function platformShortcutLabels() {
    if (document.body.dataset.platform !== 'macos') return;
    document.querySelectorAll('[title],[aria-label]').forEach(function (element) {
      ['title','aria-label'].forEach(function (attribute) { var value = element.getAttribute(attribute); if (value && value.includes('Ctrl')) element.setAttribute(attribute,value.replace(/Ctrl\+/g,'⌘').replace(/Ctrl/g,'⌘')); });
    });
  }
  var elementMenu = VexElementMenu.mount({
    source:input, roots:[{element:$('instant-content')},{element:$('markdown-preview'),readOnly:true}],
    getText:function () { return input.value; }, getIdentity:function () { return state.doc + ':' + docs[state.doc].name; },
    blocks:function () { return state.parsed.blocks; }, readOnly:function () { return input.readOnly; }, touch:function () { return false; },
    finish:function () { instant.finish(); }, applyText:function (text) { VexWorkbench.applyText(text); },
    beforeOpen:function () { VexProto.closeMenu(); }, toast:VexProto.toast,
    
    findBlock:function (line) { return Array.from($('instant-content').children).find(function (el) { return state.parsed.blocks[Number(el.dataset.blockIndex)]?.line === line; }); },
    focus:function (line,sourceActive) { if(sourceActive)input.focus({preventScroll:true}); else document.querySelector('#instant-content [data-line="' + line + '"]')?.closest('.instant-block')?.focus({preventScroll:true}); },
    editSource:function (line) { VexWorkspace.locateEditor(line); }
  });
  var launchIntent = new URLSearchParams(location.search).get('intent');
  if (launchIntent === 'open-file' || launchIntent === 'open-folder') {
    setTimeout(function () {
      var picker = $(launchIntent === 'open-file' ? 'open-file-input' : 'open-folder-input');
      if (picker) picker.click();
    }, 320);
  }
  platformShortcutLabels(); window.addEventListener('vex-language-changed',function () { platformShortcutLabels(); syncFilesToolbar(); syncStructureVisibility(); });
})();
