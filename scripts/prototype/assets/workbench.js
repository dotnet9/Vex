(function () {
  'use strict';
  var $ = function (id) { return document.getElementById(id); }, MM = window.VexMindMap;
  var sample = [
    '# 主题设计笔记', '',
    '让写作、阅读与结构梳理共享同一份 Markdown，切换视角时保留正在关注的内容。', '',
    '## 设计原则', '',
    '### 保持沉浸感', '',
    '把注意力留给内容。写作时保留必要工具，阅读和梳理结构时让文档占据更多空间。', '',
    '### 降低视觉噪音', '',
    '用层级、留白和少量强调色表达关系，避免让边框、按钮和状态提示争夺注意力。', '',
    '## 主题色体系', '',
    '主题需要同时照顾浅色与深色。强调色用于选中、定位和主要操作，正文保持稳定对比度。', '',
    '- 浅色与深色独立选择',
    '- 八套配色共用语义令牌', '',
    '## 排版与字距', '',
    '正文以舒适阅读为先，标题层级清晰。导图中的段落默认截取三十个字，点击后可查看完整内容。', '',
    '## 导出一致性', '',
    '> 同一份内容，在编辑器、阅读视图与导出文件中，都应该保留清晰的结构。', '',
    '| 输出格式 | 使用场景 |', '| --- | --- |', '| HTML | 网页发布 |', '| PDF | 阅读与分享 |', '',
    '```markdown', '# 这里是代码，不生成标题节点', '```'
  ].join('\n');
  var docs = [
    { name: '主题设计笔记.md', text: sample },
    { name: '每周复盘.md', text: '# 每周复盘\n\n## 本周进展\n\n完成写作工作台的第一版原型，把结构梳理融入日常 Markdown 写作。\n\n- [x] 确认三个主要视图\n- [x] 验证导图摘要长度\n- [ ] 收集实际使用反馈\n\n## 遇到的问题\n\n文档变长时，单看大纲很难理解章节的具体内容，需要段落摘要辅助判断。\n\n## 下周计划\n\n先验证原型，再接入原生导图控件，避免在交互未确定时投入大量实现工作。' },
    { name: '读书摘录.md', text: '# 读书摘录\n\n## 写作就是思考\n\n写下来，才能看清一个想法里缺失的环节。结构帮助我们识别重复、跳跃与尚未回答的问题。\n\n> 好的工具应该让想法更清楚。\n\n## 从笔记到文章\n\n### 收集素材\n\n先保留原始材料与出处，避免过早修改。\n\n### 整理结构\n\n把相近的观点放在一起，先确定层级，再补充论据。' },
    { name: '快速开始.md', text: '# 快速开始\n\n## 编辑\n\n默认使用即时渲染，点击正文编辑当前块，当前块显示源码，其他块保持排版，离开后恢复渲染。也可以切换源码，一边编辑 Markdown，一边查看正文。两种方式与导图共享内容。\n\n## 阅读\n\n切换到阅读视图，专注于排版后的完整内容。\n\n## 导图\n\n点击顶部导图查看结构，在摘要设置中调整长度，也可以隐藏内容只看标题。\n\n- 点击节点查看全文\n- 点击分支右侧按钮折叠\n- 拖动画布平移\n- Ctrl 加滚轮缩放\n\n## 打开自己的文档\n\n从文件菜单打开 Markdown 文件。保存按钮会下载副本，原文件不会被覆盖。' },
    { name: '导出指南.md', text: '# 导出指南\n\n## Markdown\n\n保存会下载当前文档的副本。\n\n## HTML\n\n从文件菜单导出 HTML，得到包含正文内容的独立页面。\n\n## PDF 与 Word\n\n这两个入口保留操作演示，正式版本使用文档导出服务。' }
  ];
  var settings = { length: 30, paragraphs: true, lists: true, blocks: false };
  try {
    var saved = JSON.parse(localStorage.getItem('vex-design-mindmap') || '{}');
    if (Number.isInteger(saved.length) && saved.length >= 1 && saved.length <= 120) settings.length = saved.length;
    ['paragraphs', 'lists', 'blocks'].forEach(function (key) { if (typeof saved[key] === 'boolean') settings[key] = saved[key]; });
  } catch (_) {}
  var editMode = 'instant';
  try { if (localStorage.getItem('vex-design-edit-method') === 'source') editMode = 'source'; } catch (_) {}
  var requestedEdit = new URLSearchParams(location.search).get('edit');
  if (['instant', 'source'].includes(requestedEdit)) editMode = requestedEdit;
  var state = { view: 'edit', editMode: editMode, autoPair: true, focus: false, typewriter: false, motion: true, topmost: false, language: 'zh-CN', undo: [], redo: [], sidebar: true, source: false, doc: 0, selected: null, collapsed: new Set(), scale: 1, presentation: false, presentationScale: 1.5, x: 0, y: 0, dirty: false, parsed: null, nodes: [], pending: null };
  // 原型只持久化隔离的演示宽度，不接触宿主应用设置。
  var sidebar = document.querySelector('.sidebar'), splitter = $('sidebar-splitter'), sidebarWidth = 226;
  try { var storedWidth = Number(localStorage.getItem('vex-design-sidebar-width')); if (storedWidth >= 200 && storedWidth <= 560) sidebarWidth = storedWidth; } catch (_) {}
  function resizeSidebar(width, persist) {
    var available = document.querySelector('.workspace').clientWidth;
    var maximum = Math.min(560, Math.max(200, available - 365));
    sidebarWidth = Math.max(200, Math.min(maximum, width));
    sidebar.style.setProperty('--sidebar-width', sidebarWidth + 'px');
    splitter.setAttribute('aria-valuenow', Math.round(sidebarWidth));
    if (persist) try { localStorage.setItem('vex-design-sidebar-width', String(sidebarWidth)); } catch (_) {}
  }
  resizeSidebar(sidebarWidth, false);
  var sidebarDrag;
  splitter.addEventListener('pointerdown', function (event) {
    if (event.button !== 0) return; event.preventDefault();
    sidebarDrag = { x: event.clientX, width: sidebar.getBoundingClientRect().width };
    splitter.setPointerCapture(event.pointerId); document.body.classList.add('sidebar-resizing');
  });
  splitter.addEventListener('pointermove', function (event) { if (sidebarDrag) resizeSidebar(sidebarDrag.width + event.clientX - sidebarDrag.x, false); });
  ['pointerup', 'pointercancel', 'lostpointercapture'].forEach(function (name) {
    splitter.addEventListener(name, function () { if (!sidebarDrag) return; sidebarDrag = null; document.body.classList.remove('sidebar-resizing'); resizeSidebar(sidebarWidth, true); });
  });
  splitter.addEventListener('keydown', function (event) {
    if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
    event.preventDefault(); resizeSidebar(sidebarWidth + (event.key === 'ArrowRight' ? 10 : -10), true);
  });
  window.addEventListener('resize', function () { resizeSidebar(sidebarWidth, false); });
  var canvas = $('map-viewport'), scene = $('map-scene'), input = $('markdown-input'), mapElements = new Map();
  var labels = { heading: '标题', paragraph: '段落', quote: '引用', list: '列表项', code: '代码块', table: '表格' };
  var branchColors = ['var(--accent)', 'var(--success)', 'var(--warning)', 'var(--accent)', 'var(--muted)'];
  var textMeasure = document.createElement('canvas').getContext('2d');
  var instant = createVexInstantEditor({
    input: input, blocks: function () { return state.parsed ? state.parsed.blocks : []; },
    renderBlock: function (block) {
      var element = $('markdown-preview').querySelector('[data-line="' + block.line + '"]');
      return element ? (block.kind === 'list' ? '<ul>' + element.outerHTML + '</ul>' : element.outerHTML) : '<p></p>';
    },
    refresh: refreshNow, cursor: updateCursor, remember: remember,
    autoPair: function () { return state.autoPair; }, typewriter: function () { return state.typewriter; }
  });
  function remember(text) {
    if (state.undo[state.undo.length - 1] !== text) state.undo.push(text);
    if (state.undo.length > 100) state.undo.shift(); state.redo = [];
  }
  function escape(text) { return text.replace(/[&<>"']/g, function (char) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]; }); }
  function icon(name) { return '<svg class="ico" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; }
  function closeLayers() { document.querySelectorAll('.win-layer').forEach(function (layer) { layer.hidden = true; }); }
  function openLayer(name) {
    document.querySelectorAll('.win-layer').forEach(function (layer) { layer.hidden = layer.id !== 'layer-' + name; });
  }
  var pendingSwitch = null;
  function confirmSwitch(callback) {
    if (!state.dirty) { callback(); return; }
    pendingSwitch = callback;
    document.querySelector('#layer-unsaved .dlg-msg').textContent = '「' + docs[state.doc].name + '」有尚未下载保存的修改。切换前要保存副本吗？';
    openLayer('unsaved');
  }
  function loadDoc(index) {
    instant.finish(); state.undo = []; state.redo = [];
    clearTimeout(state.pending);
    state.doc = index; state.selected = null; state.collapsed.clear(); state.dirty = false; state.mapVisited = false;
    input.value = docs[index].text; $('document-name').textContent = docs[index].name; $('node-inspector').hidden = true;
    input.setSelectionRange(0, 0); document.querySelector('.source-scroll').scrollTop = 0;
    document.querySelector('.reading-scroll').scrollTop = 0;
    renderFiles(); refresh(); requestAnimationFrame(function () { fit(true); });
  }
  function renderFiles() {
    $('file-list').innerHTML = '';
    docs.forEach(function (doc, index) {
      var button = document.createElement('button'); button.className = 'file-row' + (index === state.doc ? ' selected' : '');
      button.innerHTML = icon('file') + '<span>' + escape(doc.name) + '</span>';
      button.title = doc.name; button.setAttribute('aria-current', index === state.doc ? 'page' : 'false');
      button.onclick = function () { if (index !== state.doc) confirmSwitch(function () { loadDoc(index); }); };
      $('file-list').appendChild(button);
    });
    filterFiles();
  }
  function filterFiles() {
    var keyword = $('file-filter').value.trim().toLowerCase(), count = 0;
    Array.from($('file-list').children).forEach(function (row) { row.hidden = !row.textContent.toLowerCase().includes(keyword); if (!row.hidden) count++; });
    $('file-empty').hidden = count !== 0;
  }
  $('file-filter').oninput = filterFiles;
  function inline(text) {
    return escape(text).replace(/`([^`]+)`/g, '<code>$1</code>').replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
      .replace(/\*([^*]+)\*/g, '<em>$1</em>').replace(/\[([^\]]+)\]\((https?:\/\/[^\s)]+)\)/g, '<a href="$2" target="_blank" rel="noopener noreferrer">$1</a>');
  }
  function renderPreview() {
    var html = '', listOpen = false;
    function closeList() { if (listOpen) { html += '</ul>'; listOpen = false; } }
    state.parsed.blocks.forEach(function (block) {
      var attr = ' data-line="' + block.line + '"';
      if (block.kind === 'list') {
        if (!listOpen) { html += '<ul>'; listOpen = true; }
        html += '<li' + attr + '>' + (block.checked === null ? '' : block.checked ? '☑ ' : '☐ ') + inline(block.text) + '</li>'; return;
      }
      closeList();
      if (block.kind === 'heading') html += '<h' + block.level + attr + '>' + escape(block.text) + '</h' + block.level + '>';
      else if (block.kind === 'code') {
        var numbers = block.text.replace(/\n$/, '').split('\n').map(function (_, index) { return index + 1; }).join('\n');
        html += '<section class="md-code-block"' + attr + '><div class="md-code-header"><span>' + escape(block.language || 'text')
          + '</span><button class="md-code-copy" type="button" aria-label="复制代码">复制</button></div>'
          + '<div class="md-code-scroll"><span class="md-code-lines" aria-hidden="true">' + numbers + '</span>'
          + '<pre><code>' + escape(block.text) + '</code></pre></div></section>';
      }
      else if (block.kind === 'quote') html += '<blockquote' + attr + '>' + inline(block.text) + '</blockquote>';
      else if (block.kind === 'table') {
        var rows = block.raw.split('\n').filter(function (_, index) { return index !== 1; });
        html += '<table' + attr + '>' + rows.map(function (row, index) {
          var tag = index === 0 ? 'th' : 'td';
          return '<tr>' + row.replace(/^\s*\||\|\s*$/g, '').split('|').map(function (cell) { return '<' + tag + '>' + inline(cell.trim()) + '</' + tag + '>'; }).join('') + '</tr>';
        }).join('') + '</table>';
      } else html += '<p' + attr + '>' + inline(block.raw.replace(/\n/g, ' ')) + '</p>';
    });
    closeList(); $('markdown-preview').innerHTML = html || '<p class="empty-state">输入 Markdown，开始写作。</p>';
  }
  function renderOutline() {
    $('outline-body').innerHTML = '';
    var headings = state.nodes.filter(function (node) { return node.kind === 'heading' && node.level > 0; });
    $('outline-count').textContent = headings.length;
    headings.forEach(function (node) {
      var button = document.createElement('button'); button.className = 'outline-item';
      button.style.paddingLeft = (8 + Math.max(0, node.level - 1) * 12) + 'px';
      button.innerHTML = '<small>H' + node.level + '</small>' + escape(node.text);
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
        $('outline-body').querySelectorAll('button').forEach(function (item) { item.classList.toggle('on', item === button); });
      };
      $('outline-body').appendChild(button);
    });
    if (!headings.length) $('outline-body').innerHTML = '<div class="empty-state">添加标题后，会在这里显示大纲。</div>';
  }
  function refresh() {
    if (state.parsed && state.lastText === input.value && state.lastDoc === state.doc) { updateMeta(); updateCursor(); return; }
    var collapsedKeys = new Set(state.nodes.filter(function (node) { return state.collapsed.has(node.id); }).map(function (node) { return node.key; }));
    state.parsed = MM.parse(input.value, docs[state.doc].name.replace(/\.[^.]+$/, ''));
    state.nodes = MM.flatten(state.parsed.root);
    state.collapsed = new Set(state.nodes.filter(function (node) { return collapsedKeys.has(node.key); }).map(function (node) { return node.id; }));
    state.lastText = input.value; state.lastDoc = state.doc;
    // 源码变化后关闭旧节点详情，防止重新编号后显示到其他段落。
    state.selected = null; $('node-inspector').hidden = true;
    $('line-numbers').innerHTML = state.parsed.lines.map(function (_, index) { return '<div>' + (index + 1) + '</div>'; }).join('');
    input.style.height = (state.parsed.lines.length * 23 + 82) + 'px';
    updateMeta(); renderPreview(); instant.render(); renderOutline(); renderMap(); updateCursor();
  }
  function updateMeta() {
    $('dirty-dot').hidden = !state.dirty;
    $('save-text').textContent = state.dirty ? '未保存修改' : '未修改';
    $('save-status').querySelector('.ico').style.color = state.dirty ? 'var(--warning)' : 'var(--success)';
    $('sb-wordcount').textContent = contentCount().toLocaleString() + ' 字符';
  }
  function refreshNow() { clearTimeout(state.pending); refresh(); }
  input.addEventListener('input', function () {
    state.dirty = input.value !== docs[state.doc].text;
    $('dirty-dot').hidden = !state.dirty; $('save-text').textContent = state.dirty ? '未保存修改' : '未修改';
    clearTimeout(state.pending); state.pending = setTimeout(refresh, 180);
  });
  input.addEventListener('beforeinput', function () { remember(input.value); });
  function updateCursor() {
    var before = input.value.slice(0, input.selectionStart).split('\n');
    $('cursor-position').textContent = '行 ' + before.length + '，列 ' + (MM.characters(before[before.length - 1]).length + 1);
    if (state.typewriter && document.activeElement === input) {
      var scroll = document.querySelector('.source-scroll'); scroll.scrollTop = Math.max(0, (before.length - 1) * 23 - scroll.clientHeight / 2);
    }
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
  function reducedMotion() { return !state.motion || matchMedia('(prefers-reduced-motion: reduce)').matches; }
  function applyPresentation() {
    var root = document.querySelector('.app-window');
    root.classList.toggle('presentation-mode', state.presentation);
    root.style.setProperty('--presentation-scale', state.presentationScale);
    $('presentation-tools').hidden = !state.presentation;
    if (state.presentation) setView('read');
  }
  function startPresentation() {
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
    document.querySelector('.view-switch').dataset.active = view;
    document.querySelectorAll('[data-view]').forEach(function (button) {
      if (button.tagName === 'BUTTON') { button.setAttribute('aria-selected', String(button.dataset.view === view)); button.tabIndex = button.dataset.view === view ? 0 : -1; }
    });
    $('edit-options').hidden = view !== 'edit';
    $('instant-pane').hidden = view !== 'edit' || state.editMode !== 'instant';
    $('source-pane').hidden = !(view === 'edit' && state.editMode === 'source') && !(view === 'mindmap' && state.source);
    $('reading-pane').hidden = view !== 'read';
    $('map-pane').hidden = view !== 'mindmap';
    $('reading-title').textContent = view === 'read' ? '阅读' : '正文预览';
    $('edit-instant').setAttribute('aria-pressed', String(state.editMode === 'instant'));
    $('edit-source').setAttribute('aria-pressed', String(state.editMode === 'source'));
    $('edit-method-description').textContent = state.editMode === 'instant' ? '点击内容即可编辑' : '编辑 Markdown 源码';
    document.querySelector('.workspace').classList.toggle('source-only', view === 'edit' && state.editMode === 'source');
    document.querySelectorAll('.content-area > section').forEach(function (pane) { pane.classList.remove('pane-enter'); void pane.offsetWidth; if (!pane.hidden) pane.classList.add('pane-enter'); });
    $('status-view').textContent = { edit: state.editMode === 'instant' ? '编辑 · 即时渲染' : '编辑 · 源码', read: '阅读视图', mindmap: '思维导图' }[view];
    $('cursor-position').hidden = view !== 'edit' && !state.source;
    $('map-settings').hidden = true; $('map-settings-button').setAttribute('aria-expanded', 'false');
    if (view === 'mindmap' && !state.mapVisited) { state.mapVisited = true; requestAnimationFrame(function () { fit(true); }); }
  }
  function setEditMode(mode) {
    instant.finish(); state.editMode = mode;
    try { localStorage.setItem('vex-design-edit-method', mode); } catch (_) {}
    setView('edit');
  }
  document.querySelectorAll('.view-switch button').forEach(function (button) { button.onclick = function () { setView(button.dataset.view); }; });
  document.querySelector('.view-switch').addEventListener('keydown', function (event) {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault(); var views = ['edit', 'read', 'mindmap'], index = views.indexOf(state.view);
    index = event.key === 'Home' ? 0 : event.key === 'End' ? 2 : (index + (event.key === 'ArrowRight' ? 1 : 2)) % 3;
    setView(views[index]); $('view-' + views[index]).focus();
  });
  document.querySelectorAll('.side-tab').forEach(function (tab) {
    tab.onclick = function () {
      document.querySelectorAll('.side-tab').forEach(function (item) { item.classList.toggle('on', item === tab); });
      document.querySelectorAll('[data-pane-body]').forEach(function (pane) { pane.hidden = pane.dataset.paneBody !== tab.dataset.pane; });
    };
  });
  function visibleChildren(node) {
    return node.children.filter(function (child) {
      return child.kind === 'heading' || (['paragraph', 'quote'].includes(child.kind) && settings.paragraphs)
        || (child.kind === 'list' && settings.lists) || (['code', 'table'].includes(child.kind) && settings.blocks);
    });
  }
  function nodeText(node) { return node.kind === 'heading' ? node.text : MM.clip(node.text || node.label || '空内容', settings.length); }
  function height(node) {
    var content = nodeText(node), fontSize = node.kind === 'heading' ? 13 : 12, width = node.width - (node.kind === 'heading' && !node.isRoot ? 28 : 26);
    textMeasure.font = (node.kind === 'heading' ? '600 ' : '') + fontSize + 'px ' + getComputedStyle(document.body).fontFamily;
    var rows = 1, used = 0;
    MM.characters(content).forEach(function (char) { var charWidth = textMeasure.measureText(char).width; if (used + charWidth > width) { rows++; used = 0; } used += charWidth; });
    return (node.kind === 'heading' ? 40 : 22) + rows * 20;
  }
  function renderMap() {
    if (!state.parsed) return;
    var rendered = [], cursor = 54, gap = 14;
    function layout(node, depth) {
      node.width = node.isRoot ? 214 : 224; node.height = height(node); node.x = depth * 272 + 32;
      var children = state.collapsed.has(node.id) ? [] : visibleChildren(node);
      if (!children.length) { node.y = cursor; cursor += node.height + gap; }
      else {
        children.forEach(function (child) { layout(child, depth + 1); });
        node.y = (children[0].y + children[0].height / 2 + children[children.length - 1].y + children[children.length - 1].height / 2) / 2 - node.height / 2;
      }
      node.visibleChildren = children; rendered.push(node);
    }
    layout(state.parsed.root, 0);
    var visible = new Set(rendered.map(function (node) { return node.id; }));
    mapElements.forEach(function (element, id) { if (!visible.has(id)) { element.remove(); mapElements.delete(id); } });
    var links = '';
    rendered.forEach(function (node, index) {
      var element = mapElements.get(node.id), isNew = !element;
      if (isNew) { element = document.createElement('div'); mapElements.set(node.id, element); $('map-nodes').appendChild(element); }
      element.className = 'map-node' + (node.isRoot ? ' root' : node.kind === 'heading' ? '' : ' content') + (state.selected === node.id ? ' selected' : '') + (isNew ? ' enter' : '');
      element.dataset.kind = node.kind; element.dataset.line = node.line; element.style.left = node.x + 'px'; element.style.top = node.y + 'px';
      element.style.width = node.width + 'px'; element.style.minHeight = node.height + 'px';
      element.style.setProperty('--branch-color', branchColors[node.branch % branchColors.length]);
      element.style.setProperty('--delay', Math.min(index * 14, 180) + 'ms');
      var kind = node.isRoot ? '文档' : node.kind === 'heading' ? 'H' + node.level : node.label || labels[node.kind];
      element.innerHTML = '<button class="map-node-button" title="' + escape(node.text || kind) + '"><span class="map-node-type">' + escape(kind) + '</span><span class="map-node-text">' + escape(nodeText(node)) + '</span></button>';
      element.querySelector('button').onclick = function () { selectNode(node); };
      var children = visibleChildren(node);
      if (children.length) {
        var collapse = document.createElement('button'); collapse.className = 'map-collapse'; collapse.textContent = state.collapsed.has(node.id) ? '+' : '−';
        collapse.setAttribute('aria-label', (state.collapsed.has(node.id) ? '展开' : '折叠') + '：' + node.text); collapse.setAttribute('aria-expanded', String(!state.collapsed.has(node.id)));
        collapse.onclick = function () { if (state.collapsed.has(node.id)) state.collapsed.delete(node.id); else state.collapsed.add(node.id); $('node-inspector').hidden = true; renderMap(); };
        element.appendChild(collapse);
      }
      node.visibleChildren.forEach(function (child) {
        var x1 = node.x + node.width, y1 = node.y + node.height / 2, x2 = child.x, y2 = child.y + child.height / 2, middle = (x1 + x2) / 2;
        links += '<path style="--branch-color:' + branchColors[child.branch % branchColors.length] + '" d="M' + x1 + ',' + y1 + ' C' + middle + ',' + y1 + ' ' + middle + ',' + y2 + ' ' + x2 + ',' + y2 + '"/>';
      });
    });
    $('map-links').innerHTML = links;
    state.bounds = { width: Math.max.apply(null, rendered.map(function (node) { return node.x + node.width; })) + 32, height: cursor + 20 };
    scene.style.width = state.bounds.width + 'px'; scene.style.height = state.bounds.height + 'px';
    $('map-links').setAttribute('width', state.bounds.width); $('map-links').setAttribute('height', state.bounds.height);
    $('map-count').textContent = state.nodes.filter(function (node) { return node.kind === 'heading'; }).length + ' 个标题 · ' + rendered.length + ' 个可见节点';
    $('collapse-all').innerHTML = icon('list') + (state.collapsed.size ? '展开全部' : '收起分支');
    transform();
  }
  function transform(animate) {
    scene.classList.toggle('animating', !!animate);
    scene.style.transform = 'translate(' + state.x + 'px,' + state.y + 'px) scale(' + state.scale + ')';
    $('zoom-value').textContent = Math.round(state.scale * 100) + '%';
  }
  function fit(readable) {
    if (state.guideRestoring || state.view !== 'mindmap' || !state.bounds || !canvas.clientWidth) return;
    state.scale = Math.min(1, Math.max(readable === true ? .75 : .18, Math.min((canvas.clientWidth - 64) / state.bounds.width, (canvas.clientHeight - 84) / state.bounds.height)));
    state.x = (canvas.clientWidth - state.bounds.width * state.scale) / 2;
    state.y = (canvas.clientHeight - state.bounds.height * state.scale) / 2;
    if (readable === true) state.x = Math.max(24, state.x);
    transform(true);
  }
  function zoom(factor, anchor) {
    anchor = anchor || { x: canvas.clientWidth / 2, y: canvas.clientHeight / 2 };
    var previous = state.scale; state.scale = Math.min(2, Math.max(.18, state.scale * factor));
    state.x = anchor.x - (anchor.x - state.x) * state.scale / previous; state.y = anchor.y - (anchor.y - state.y) * state.scale / previous;
    transform(!anchor.pointer);
  }
  var drag = null;
  canvas.addEventListener('pointerdown', function (event) {
    if (event.target.closest('button') || event.button !== 0) return;
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
    if (state.view !== 'mindmap' || !canvas.clientWidth) return;
    if (state.guideRestoring) { lastCanvasSize = { width: canvas.clientWidth, height: canvas.clientHeight }; return; }
    if (lastCanvasSize.width !== canvas.clientWidth || lastCanvasSize.height !== canvas.clientHeight) {
      lastCanvasSize = { width: canvas.clientWidth, height: canvas.clientHeight }; fit(true);
    }
  }).observe(canvas);
  function selectNode(node) {
    state.selected = node.id;
    mapElements.forEach(function (element, id) { element.classList.toggle('selected', id === node.id); });
    $('inspector-kind').textContent = node.isRoot ? '文档' : labels[node.kind];
    $('inspector-title').textContent = node.kind === 'heading' ? node.text : '完整' + labels[node.kind];
    $('inspector-content').textContent = node.kind === 'heading' ? (node.children.filter(function (child) { return child.kind !== 'heading'; }).map(function (child) { return child.text; }).join('\n\n') || '此标题下没有直接内容，展开分支查看子章节。') : node.text;
    $('inspector-line').textContent = '第 ' + node.line + (node.endLine > node.line ? '–' + node.endLine : '') + ' 行';
    $('node-inspector').hidden = false; $('map-settings').hidden = true; $('map-settings-button').setAttribute('aria-expanded', 'false');
    if (state.source) highlightSource(node, false);
  }
  function highlightSource(node, focus) {
    var start = state.parsed.lines.slice(0, node.line - 1).join('\n').length + (node.line > 1 ? 1 : 0);
    var length = state.parsed.lines.slice(node.line - 1, node.endLine).join('\n').length;
    input.setSelectionRange(start, start + length);
    document.querySelector('.source-scroll').scrollTo({ top: Math.max(0, (node.line - 1) * 23 - 90), behavior: reducedMotion() ? 'auto' : 'smooth' });
    if (focus) input.focus({ preventScroll: true }); updateCursor();
  }
  function locate(node) {
    if (state.view === 'mindmap') { state.source = true; applySource(); highlightSource(node, true); }
    else if (state.editMode === 'instant') { setView('edit'); instant.focusAt(node.line); }
    else { setView('edit'); highlightSource(node, true); }
  }
  function applySource() {
    $('source-pane').hidden = !state.source; $('map-source-toggle').classList.toggle('on', state.source);
    $('map-source-toggle').setAttribute('aria-pressed', String(state.source)); $('cursor-position').hidden = !state.source;
    requestAnimationFrame(function () { fit(true); });
  }
  function syncSettings() {
    $('summary-length').value = settings.length; $('show-paragraphs').checked = settings.paragraphs;
    $('show-lists').checked = settings.lists; $('show-blocks').checked = settings.blocks;
    $('summary-label').textContent = !settings.paragraphs && !settings.lists && !settings.blocks ? '仅标题' : '摘要 ' + settings.length + ' 字';
    $('summary-example').textContent = MM.clip('把注意力留给内容。写作时保留必要工具，阅读和梳理结构时让文档占据更多空间。', settings.length);
    try { localStorage.setItem('vex-design-mindmap', JSON.stringify(settings)); } catch (_) {}
    renderMap(); requestAnimationFrame(function () { fit(true); });
  }
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
    refreshNow(); download(input.value, docs[state.doc].name); docs[state.doc].text = input.value; state.dirty = false; refresh(); $('save-text').textContent = '副本已下载';
    VexProto.toast('已下载 Markdown 副本');
  }
  function insert(text, activate) {
    if (activate !== false) setView('edit');
    if (state.view === 'edit' && state.editMode === 'instant') {
      if (instant.insert(text)) return;
      remember(input.value);
      input.setRangeText(text, input.selectionStart, input.selectionEnd, 'end');
      input.dispatchEvent(new Event('input')); refreshNow();
      instant.focusAt(input.value.slice(0, input.selectionEnd).split('\n').length); return;
    }
    input.focus();
    if (!document.execCommand('insertText', false, text)) input.setRangeText(text, input.selectionStart, input.selectionEnd, 'end');
    input.dispatchEvent(new Event('input'));
  }
  function contentCount() { return MM.characters(state.parsed.blocks.map(function (block) { return block.text; }).join('')).length; }
  function findNext() {
    var term = $('find-input').value;
    if (!term) return;
    setEditMode('source'); var index = input.value.indexOf(term, input.selectionEnd); if (index < 0) index = input.value.indexOf(term);
    if (index < 0) { $('find-count').textContent = '没有匹配内容'; return; }
    input.setSelectionRange(index, index + term.length); input.focus();
    var line = input.value.slice(0, index).split('\n').length;
    document.querySelector('.source-scroll').scrollTop = Math.max(0, (line - 1) * 23 - 90); updateCursor();
  }
  function updateFind() {
    var term = $('find-input').value; $('find-count').textContent = term ? input.value.split(term).length - 1 + ' 处匹配' : '输入查找内容';
  }
  $('find-input').oninput = updateFind; $('find-input').onkeydown = function (event) { if (event.key === 'Enter') findNext(); };
  $('open-file-input').onchange = async function () {
    var file = this.files[0]; if (!file) return;
    try {
      var text = await file.text();
      confirmSwitch(function () { docs.push({ name: file.name, text: text }); loadDoc(docs.length - 1); VexProto.toast('已打开 ' + file.name); });
    } catch (_) { VexProto.toast('无法读取文件，请重新选择'); }
    this.value = '';
  };
  $('open-folder-input').onchange = async function () {
    var files = Array.from(this.files || []).filter(function (file) { return /\.(md|markdown|mdown|txt)$/i.test(file.name); });
    if (!files.length) { VexProto.toast('文件夹中没有可打开的 Markdown 文件'); this.value = ''; return; }
    try {
      var loaded = await Promise.all(files.map(async function (file) { return { name: file.name, text: await file.text() }; }));
      var folder = (files[0].webkitRelativePath || '').split('/')[0] || 'Markdown';
      confirmSwitch(function () {
        docs = loaded; state.doc = 0; state.dirty = false; $('workspace-folder').textContent = folder;
        loadDoc(0); VexProto.toast('已打开文件夹 · ' + loaded.length + ' 个 Markdown 文件');
      });
    } catch (_) { VexProto.toast('无法读取文件夹中的 Markdown 文件'); }
    this.value = '';
  };
  var snippets = { paragraph: '\n\n新段落\n\n', heading: '\n## 新章节\n\n', list: '\n- 列表项\n', ordered: '\n1. 列表项\n', task: '\n- [ ] 待办事项\n', quote: '\n> 引用内容\n', code: '\n```\n代码内容\n```\n', math: '\n$$\nx^2 + y^2 = z^2\n$$\n', rule: '\n\n---\n\n', 'inline-code': '`代码`', table: '\n| 列一 | 列二 |\n| --- | --- |\n| 内容 | 内容 |\n', link: '[链接文字](https://example.com)', bold: '**文字**', italic: '*文字*', image: '![图片描述](image.png)' };
  [1,2,3,4,5,6].forEach(function (level) { snippets['heading' + level] = '\n' + '#'.repeat(level) + ' 新章节\n\n'; });
  VexProto.handleAction = function (action, item) {
    if (action.startsWith('insert-')) { insert(snippets[action.slice(7)] || ''); return true; }
    if (action.startsWith('view-')) { setView(action.slice(5)); return true; }
    if (action === 'edit-instant' || action === 'edit-source') { setEditMode(action.slice(5)); return true; }
    if (action.startsWith('dlg-')) {
      if (action === 'dlg-statistics') {
        refreshNow();
        var stats = document.querySelectorAll('#layer-statistics .stat-card .num');
        if (stats[0]) stats[0].textContent = contentCount().toLocaleString();
        if (stats[1]) stats[1].textContent = MM.characters(input.value).length.toLocaleString();
        document.querySelectorAll('#layer-statistics .stat-card .lbl')[0].textContent = '正文字符';
        document.querySelectorAll('#layer-statistics .stat-card .lbl')[1].textContent = '源码字符';
        var metrics = [['行数', state.parsed.lines.length], ['段落', state.parsed.blocks.filter(function (block) { return block.kind === 'paragraph'; }).length], ['标题', state.parsed.blocks.filter(function (block) { return block.kind === 'heading'; }).length], ['阅读时间', '约 ' + Math.max(1, Math.ceil(contentCount() / 350)) + ' 分钟'], ['编码', 'UTF-8'], ['状态', state.dirty ? '已修改' : '未修改']];
        document.querySelector('#layer-statistics .stat-rows').innerHTML = metrics.map(function (metric) { return '<div class="stat-row"><span class="k">' + metric[0] + '</span><span class="v">' + metric[1] + '</span></div>'; }).join('');
      }
      if (action === 'dlg-properties') {
        refreshNow();
        document.querySelector('#layer-properties .prop-name').textContent = docs[state.doc].name;
        var properties = [['状态', state.dirty ? '已修改 · 尚未保存' : '未修改'], ['格式', 'Markdown'], ['编码', 'UTF-8'], ['大小', new Blob([input.value]).size.toLocaleString() + ' 字节']];
        document.querySelector('#layer-properties .prop-rows').innerHTML = properties.map(function (property) { return '<div class="prop-row"><span class="k">' + property[0] + '</span><span class="v">' + property[1] + '</span></div>'; }).join('');
      }
      if (action === 'dlg-delete') {
        document.querySelector('#layer-delete .dlg-title').textContent = '从工作区移除「' + docs[state.doc].name + '」？';
        document.querySelector('#layer-delete .dlg-msg').textContent = '原型仅移除工作区中的文档，原文件不会改动。';
        document.querySelector('#layer-delete .dlg-path').textContent = docs[state.doc].name;
      }
      if (action === 'dlg-doc') {
        var thanks = item && item.arg === '鸣谢'; $('doc-tb-title').textContent = thanks ? '鸣谢' : '更新日志'; $('doc-head-title').textContent = $('doc-tb-title').textContent;
        $('doc-content-changelog').hidden = thanks; $('doc-content-thanks').hidden = !thanks; openLayer('doc');
      } else openLayer(action.slice(4));
      return true;
    }
    switch (action) {
      case 'new': confirmSwitch(function () { docs.push({ name: '未命名文档.md', text: '# 未命名文档\n\n' }); loadDoc(docs.length - 1); setView('edit'); if (state.editMode === 'instant') instant.focusAt(1); else input.focus(); }); break;
      case 'open': $('open-file-input').click(); break;
      case 'open-folder': $('open-folder-input').click(); break;
      case 'open-sample': confirmSwitch(function () { loadDoc(Math.min(item.arg, docs.length - 1)); }); break;
      case 'quick-open': if (!state.sidebar) VexProto.handleAction('toggle-sidebar'); VexProto.handleAction('pane-files'); $('file-filter').focus(); break;
      case 'save': save(); break;
      case 'save-all': save(); VexProto.toast('当前修改已保存，其余文档没有待保存的修改'); break;
      case 'export-html': instant.finish(); refreshNow(); download('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><title>' + escape(docs[state.doc].name) + '</title><style>body{max-width:780px;margin:50px auto;font:16px/1.9 system-ui;padding:0 24px;color:#242933;background:#fff}pre{padding:16px;background:#f4f5f7;overflow:auto}table{border-collapse:collapse}td,th{border:1px solid #ddd;padding:6px 12px}blockquote{border-left:3px solid #888;padding-left:16px;color:#666}' + VexTypography.exportStyles() + '</style><body>' + $('markdown-preview').innerHTML + '</body></html>', docs[state.doc].name.replace(/\.[^.]+$/, '') + '.html', 'text/html;charset=utf-8'); break;
      case 'toggle-sidebar': state.sidebar = !state.sidebar; document.querySelector('.sidebar').classList.toggle('closed', !state.sidebar); $('sidebar-toggle').classList.toggle('on', state.sidebar); $('sidebar-toggle').setAttribute('aria-pressed', String(state.sidebar)); break;
      case 'pane-outline': case 'pane-files': if (!state.sidebar) VexProto.handleAction('toggle-sidebar'); document.querySelector('.side-tab[data-pane="' + action.slice(5) + '"]').click(); break;
      case 'map-source': state.source = !state.source; applySource(); break;
      case 'map-settings': var opened = !$('map-settings').hidden; if (state.view !== 'mindmap') setView('mindmap'); $('map-settings').hidden = opened; $('map-settings-button').setAttribute('aria-expanded', String(!opened)); $('node-inspector').hidden = true; break;
      case 'close-settings': $('map-settings').hidden = true; $('map-settings-button').setAttribute('aria-expanded', 'false'); break;
      case 'reset-settings': settings = { length: 30, paragraphs: true, lists: true, blocks: false }; syncSettings(); break;
      case 'map-collapse': if (state.collapsed.size) state.collapsed.clear(); else state.nodes.filter(function (node) { return !node.isRoot && visibleChildren(node).length; }).forEach(function (node) { state.collapsed.add(node.id); }); $('node-inspector').hidden = true; renderMap(); fit(); break;
      case 'map-fit': fit(); break;
      case 'zoom-in': zoom(1.2); break;
      case 'zoom-out': zoom(1 / 1.2); break;
      case 'close-inspector': state.selected = null; $('node-inspector').hidden = true; renderMap(); break;
      case 'locate-source': var node = state.nodes.find(function (node) { return node.id === state.selected; }); if (node) locate(node); break;
      case 'find': case 'replace': $('findbar').hidden = false; $('replace-input').hidden = action !== 'replace'; $('replace-all').hidden = action !== 'replace'; $('find-input').focus(); updateFind(); break;
      case 'close-find': $('findbar').hidden = true; break;
      case 'find-next': findNext(); break;
      case 'replace-all': var term = $('find-input').value; if (term) { input.value = input.value.split(term).join($('replace-input').value); input.dispatchEvent(new Event('input')); updateFind(); setView('edit'); } break;
      case 'toggle-lines': document.querySelector('.source-scroll').classList.toggle('no-lines'); break;
      case 'toggle-pairs': state.autoPair = !state.autoPair; break;
      case 'toggle-focus': state.focus = !state.focus; document.querySelector('.app-window').classList.toggle('focus-mode', state.focus); break;
      case 'toggle-typewriter': state.typewriter = !state.typewriter; updateCursor(); break;
      case 'toggle-motion': state.motion = !state.motion; document.documentElement.dataset.motion = String(state.motion); break;
      case 'presentation': startPresentation(); break;
      case 'presentation-larger': state.presentationScale = Math.min(2.5, +(state.presentationScale + .1).toFixed(1)); applyPresentation(); break;
      case 'presentation-smaller': state.presentationScale = Math.max(1, +(state.presentationScale - .1).toFixed(1)); applyPresentation(); break;
      case 'presentation-exit': stopPresentation(); break;
      case 'format-more': VexProto.toast('更多格式请从插入菜单选择'); break;
      case 'typography': VexTypography.toggle(); break;
      case 'typography-select': VexTypography.choose(item.arg); break;
      case 'toggle-compact': VexTypography.toggleCompact(); break;
      case 'language': state.language = item.arg; VexL.set(item.arg); break;
      case 'toggle-status': document.querySelector('.statusbar').hidden = !document.querySelector('.statusbar').hidden; break;
      case 'maximize': document.querySelector('.app-window').classList.toggle('expanded'); break;
      case 'toggle-fullscreen': document.querySelector('.app-window').classList.toggle('expanded'); break;
      case 'toggle-topmost': state.topmost = !state.topmost; VexProto.toast(state.topmost ? '窗口置顶（桌面版操作演示）' : '取消置顶（桌面版操作演示）'); break;
      case 'minimize': VexProto.toast('最小化由桌面窗口提供（原型演示）'); break;
      case 'close': confirmSwitch(function () { VexProto.toast('文档已关闭（原型演示）'); }); break;
      case 'theme': document.querySelector('#vex-theme-panel button').focus(); break;
      case 'refresh': refreshNow(); break;
      case 'undo': case 'redo':
        instant.finish();
        var from = action === 'undo' ? state.undo : state.redo, to = action === 'undo' ? state.redo : state.undo;
        if (from.length) { to.push(input.value); input.value = from.pop(); input.dispatchEvent(new Event('input')); refreshNow(); }
        setView('edit'); break;
      case 'copy': case 'copy-plain': case 'cut':
        var selected = input.value.slice(input.selectionStart, input.selectionEnd), copied = selected || input.value;
        if (action === 'copy-plain') copied = MM.plain(copied);
        navigator.clipboard.writeText(copied).then(function () { if (action === 'cut' && selected) insert('', false); VexProto.toast('已复制文本'); }).catch(function () { VexProto.toast('请在编辑区使用系统复制快捷键'); }); break;
      case 'paste': navigator.clipboard.readText().then(function (text) { insert(text); }).catch(function () { VexProto.toast('请在编辑区使用 Ctrl+V 粘贴'); }); break;
      case 'select-all': setEditMode('source'); input.focus(); input.select(); break;
      case 'clear-format': var selection = input.value.slice(input.selectionStart, input.selectionEnd); if (selection) insert(MM.plain(selection)); else VexProto.toast('先选择需要清除格式的文字'); break;
      case 'guide': if (window.VexGuide) VexGuide.start(); break;
      case 'print': VexProto.toast('桌面版提供打印预览（原型演示）'); break;
      default: return false;
    }
    return true;
  };
  VexProto.isSidebarVisible = function () { return state.sidebar; };
  // 引导演练使用临时文档；结束时还原进入前的内容、视图和显示偏好。
  VexProto.beginGuideSession = function () {
    instant.finish(); refreshNow(); closeLayers(); VexProto.closeMenu();
    var snapshot = {
      doc: state.doc, text: input.value, dirty: state.dirty, view: state.view, editMode: state.editMode,
      sidebar: state.sidebar, focus: state.focus, source: state.source,
      sideTab: document.querySelector('.side-tab.on').dataset.pane,
      statusHidden: document.querySelector('.statusbar').hidden, findHidden: $('findbar').hidden,
      typography: Object.assign({}, VexTypography.state), settings: Object.assign({}, settings),
      undo: state.undo.slice(), redo: state.redo.slice(), selection: [input.selectionStart, input.selectionEnd],
      scrolls: ['.instant-scroll', '.reading-scroll', '.source-scroll'].map(function (selector) { return document.querySelector(selector).scrollTop; }),
      collapsed: new Set(state.collapsed), selected: state.selected, map: { x: state.x, y: state.y, scale: state.scale, visited: state.mapVisited },
      editPreference: localStorage.getItem('vex-design-edit-method')
    };
    state.focus = false; document.querySelector('.app-window').classList.remove('focus-mode');
    document.querySelector('.statusbar').hidden = false; $('findbar').hidden = true;
    if (!state.sidebar) VexProto.handleAction('toggle-sidebar');
    VexProto.handleAction('pane-files'); state.source = false; state.editMode = 'instant';
    settings = { length: 30, paragraphs: true, lists: true, blocks: false };
    snapshot.demo = { name: '一分钟上手 · 演练.md', text: '# 让一个想法，长成一篇文章\n\n我想把零散的想法写下来，再用结构把它们连成一条清晰的线索。先写内容，之后慢慢整理。\n\n## 写下想法\n\n点击这段文字试着修改，也可以用 **加粗** 标记重点。\n\n## 看清结构\n\n标题成为导图的主干，段落提供摘要。显示文字可以变短，完整内容一直保留。\n\n## 准备分享\n\n选择舒服的排版，确认保存状态，再把文章导出给读者。' };
    docs.push(snapshot.demo); loadDoc(docs.length - 1); syncSettings(); setView('edit');
    return snapshot;
  };
  VexProto.endGuideSession = function (snapshot) {
    state.guideRestoring = true; document.querySelector('.app-window').classList.add('guide-restoring');
    instant.finish(); VexProto.closeMenu(); closeLayers();
    var index = docs.indexOf(snapshot.demo); if (index >= 0) docs.splice(index, 1);
    loadDoc(snapshot.doc); input.value = snapshot.text; state.dirty = snapshot.dirty;
    state.editMode = snapshot.editMode; state.source = snapshot.source;
    state.focus = snapshot.focus; document.querySelector('.app-window').classList.toggle('focus-mode', snapshot.focus);
    if (state.sidebar !== snapshot.sidebar) VexProto.handleAction('toggle-sidebar');
    document.querySelector('.side-tab[data-pane="' + snapshot.sideTab + '"]').click();
    document.querySelector('.statusbar').hidden = snapshot.statusHidden; $('findbar').hidden = snapshot.findHidden;
    settings = snapshot.settings; syncSettings();
    VexTypography.state.compact = snapshot.typography.compact; VexTypography.choose(snapshot.typography.key);
    try { if (snapshot.editPreference === null) localStorage.removeItem('vex-design-edit-method'); else localStorage.setItem('vex-design-edit-method', snapshot.editPreference); } catch (_) {}
    refreshNow(); state.collapsed = snapshot.collapsed; state.undo = snapshot.undo; state.redo = snapshot.redo;
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
      'language': !!item && state.language === item.arg
    };
    if (Object.prototype.hasOwnProperty.call(checks, action)) return checks[action];
    return action.startsWith('view-') ? state.view === action.slice(5) : action === 'toggle-sidebar' ? state.sidebar
      : action === 'toggle-lines' ? !document.querySelector('.source-scroll').classList.contains('no-lines')
      : action === 'toggle-status' ? !document.querySelector('.statusbar').hidden : false;
  };
  document.addEventListener('click', function (event) {
    var copyCode = event.target.closest('.md-code-copy');
    if (copyCode) {
      event.stopPropagation();
      var code = copyCode.closest('.md-code-block').querySelector('code').textContent;
      if (navigator.clipboard) navigator.clipboard.writeText(code).then(function () { VexProto.toast('代码已复制'); }, function () { VexProto.toast('无法访问剪贴板，请选择代码后复制'); });
      else VexProto.toast('无法访问剪贴板，请选择代码后复制');
      return;
    }
    var button = event.target.closest('[data-action]'); if (button) { event.stopPropagation(); VexProto.handleAction(button.dataset.action); }
    else if (!$('map-settings').contains(event.target)) { $('map-settings').hidden = true; $('map-settings-button').setAttribute('aria-expanded', 'false'); }
  });
  document.querySelectorAll('.win-layer').forEach(function (layer) {
    layer.onclick = function (event) { if (event.target === layer) { closeLayers(); pendingSwitch = null; } };
    layer.querySelectorAll('[data-close]').forEach(function (button) {
      button.onclick = function () {
        if (layer.id === 'layer-delete' && button.classList.contains('danger')) {
          closeLayers(); docs.splice(state.doc, 1);
          if (!docs.length) docs.push({ name: '未命名文档.md', text: '' });
          state.lastDoc = -1; state.lastText = null; state.dirty = false; loadDoc(Math.min(state.doc, docs.length - 1)); return;
        }
        if (layer.id === 'layer-unsaved' && pendingSwitch) {
          var next = pendingSwitch; pendingSwitch = null;
          if (button.textContent.trim() === '取消') { closeLayers(); return; }
          if (button.textContent.trim() === '保存') save();
          closeLayers(); next(); return;
        }
        closeLayers(); if (button.dataset.close) VexProto.toast(button.dataset.close);
      };
    });
  });
  // 保留独立窗口的可点击演示。
  if ($('err-toggle')) $('err-toggle').onclick = function () { $('err-detail').style.display = $('err-detail').style.display === 'none' ? '' : 'none'; };
  ['mcp-copy-token', 'mcp-copy-endpoint'].forEach(function (id) { if ($(id)) $(id).onclick = function () { navigator.clipboard.writeText(id.endsWith('token') ? $('mcp-token').value : $('mcp-endpoint') ? $('mcp-endpoint').textContent : 'http://127.0.0.1:17891/mcp/').catch(function () { VexProto.toast('请手动复制'); }); }; });
  if ($('mcp-switch')) $('mcp-switch').onclick = function () { this.classList.toggle('on'); $('mcp-form').style.opacity = this.classList.contains('on') ? '' : '.45'; $('mcp-status-text').textContent = this.classList.contains('on') ? '服务运行中（演示）' : '服务已停止（演示）'; };
  if ($('mcp-save')) $('mcp-save').onclick = function () { closeLayers(); VexProto.toast('MCP 设置已保存（演示）'); };
  if ($('mcp-gen')) $('mcp-gen').onclick = function () { $('mcp-token').value = 'vx_' + crypto.randomUUID().replace(/-/g, ''); };
  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') { if (state.presentation) { stopPresentation(); return; } closeLayers(); pendingSwitch = null; $('findbar').hidden = true; $('node-inspector').hidden = true; $('map-settings').hidden = true; $('map-settings-button').setAttribute('aria-expanded', 'false'); }
    if ((event.ctrlKey || event.metaKey) && ['s', 'f', 'h', 'o', 'n', 'w', 'p'].includes(event.key.toLowerCase())) {
      event.preventDefault(); VexProto.handleAction(event.key.toLowerCase() === 'p' ? (event.shiftKey ? 'print' : 'quick-open') : { s: 'save', f: 'find', h: 'replace', o: 'open', n: 'new', w: 'close' }[event.key.toLowerCase()]);
    }
    if (event.key === 'F5') { event.preventDefault(); refreshNow(); }
    if (event.key === 'F8') { event.preventDefault(); VexProto.handleAction('toggle-focus'); }
    if (event.key === 'F11') { event.preventDefault(); VexProto.handleAction('toggle-fullscreen'); }
    if (event.altKey && event.key === 'Enter') { event.preventDefault(); VexProto.handleAction('dlg-properties'); }
    if (!event.ctrlKey && !event.metaKey && event.key === '/' && !event.target.closest('input, textarea')) { event.preventDefault(); if (!state.sidebar) VexProto.handleAction('toggle-sidebar'); VexProto.handleAction('pane-files'); $('file-filter').focus(); }
  });
  VexProto.attachMenuBar(document.querySelector('.tb-menu'));
  syncSettings(); loadDoc(0); setView(new URLSearchParams(location.search).get('view') || 'edit');
})();
