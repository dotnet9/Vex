/* 章节操作只改相应 Markdown 区间；整节移动携带正文与全部子章节。 */
(function () {
  'use strict';
  var W = window.VexWorkbench, MM = window.VexMindMap;
  var $ = function (id) { return document.getElementById(id); };
  var renamed = null, dragging = null, drop = null, selectedLine = null;
  function icon(name) { return '<svg class="ico" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; }
  function escape(value) { return String(value).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); }
  function selected() { return W.state.nodes.find(function (node) { return node.id === W.state.selected; }); }
  function heading() { var node = selected(); return node && node.kind === 'heading' ? node : W.state.parsed.root; }
  function records() {
    var offset = 0, starts = W.text.split('\n').map(function (line) { var start = offset; offset += line.length + 1; return start; });
    return W.state.parsed.blocks.filter(function (block) { return block.kind === 'heading'; }).map(function (block) {
      var next = starts[block.endLine];
      return { line: block.line, level: block.level, text: block.text, start: starts[block.line - 1], headerEnd: next === undefined ? W.text.length : next, raw: block.raw };
    });
  }
  function record(node) {
    if (!node || node.level === 0) return null;
    var list = records(), index = list.findIndex(function (item) { return item.line === node.line; });
    if (index < 0) return null;
    var item = list[index], next = list.slice(index + 1).find(function (candidate) { return candidate.level <= item.level; });
    item.end = next ? next.start : W.text.length;
    item.bodyEnd = list[index + 1] ? list[index + 1].start : W.text.length;
    item.descendants = list.filter(function (candidate) { return candidate.start > item.start && candidate.start < item.end; });
    return item;
  }
  function parentOf(node) {
    function find(parent) { if (parent.children.some(function (child) { return child.id === node.id; })) return parent; for (var child of parent.children) { var result = find(child); if (result) return result; } return null; }
    return find(W.state.parsed.root);
  }
  function siblings(node) { var parent = parentOf(node); return parent ? parent.children.filter(function (item) { return item.kind === 'heading'; }) : []; }
  function lineAt(text, offset) { return text.slice(0, offset).split('\n').length; }
  var feedback = document.createElement('div'); feedback.className = 'chapter-feedback'; feedback.hidden = true;
  feedback.innerHTML = '<span role="status"></span><button type="button">撤销这次修改</button>';
  document.querySelector('.map-toolbar').after(feedback);
  var feedbackText, feedbackBefore;
  feedback.querySelector('button').onclick = function () {
    if (W.text !== feedbackText || W.state.undo[W.state.undo.length - 1] !== feedbackBefore) { feedback.hidden = true; VexProto.toast('正文已有后续修改，请使用 Ctrl+Z 依次撤销。'); return; }
    W.history('undo'); feedback.hidden = true; VexProto.toast('已撤销章节修改，正文与导图一起恢复');
  };
  var dropPreview = document.createElement('div'); dropPreview.className = 'chapter-drop-preview'; dropPreview.hidden = true; dropPreview.setAttribute('role', 'status'); $('map-viewport').appendChild(dropPreview);
  function change(text, line, message, editTitle) {
    var before = W.text;
    W.applyText(text, { line: line }); selectedLine = line;
    if (W.text === before) return;
    var node = W.state.nodes.find(function (candidate) { return candidate.kind === 'heading' && candidate.line === line; });
    if (node) {
      W.selectNode(node);
      if (editTitle) requestAnimationFrame(function () { beginRename(node); });
    }
    feedbackText = W.text; feedbackBefore = before;
    feedback.querySelector('span').textContent = message || '章节标题已同步修改正文'; feedback.hidden = false;
  }
  function rename(node, title) {
    title = title.trim().replace(/[\r\n]+/g, ' ');
    if (!title) { VexProto.toast('给章节起一个标题，再继续。'); return; }
    var item = record(node); if (!item) return;
    var header = '#'.repeat(item.level) + ' ' + title;
    var after = W.text.slice(item.headerEnd), replacement = header + (item.headerEnd < W.text.length || /\n$/.test(W.text.slice(item.start, item.headerEnd)) ? '\n' : '');
    change(W.text.slice(0, item.start) + replacement + after, item.line, null);
  }
  function beginRename(node) {
    if (!node || node.kind !== 'heading' || node.level === 0) return;
    var element = document.querySelector('.map-node[data-node-id="' + node.id + '"] .map-node-text'); if (!element || renamed) return;
    var input = document.createElement('input'); input.className = 'chapter-rename'; input.value = node.text; input.setAttribute('aria-label', '章节标题');
    element.replaceChildren(input); renamed = input; var canceled = false;
    input.addEventListener('pointerdown', function (event) { event.stopPropagation(); });
    input.addEventListener('click', function (event) { event.stopPropagation(); });
    input.addEventListener('dblclick', function (event) { event.stopPropagation(); });
    function finish() { if (renamed !== input) return; renamed = null; if (!canceled && input.value.trim() && input.value !== node.text) rename(node, input.value); else { W.renderMap(); if (!input.value.trim() && !canceled) VexProto.toast('章节标题不能为空，已保留原来的标题。'); } $('map-viewport').focus({ preventScroll: true }); }
    input.addEventListener('keydown', function (event) {
      if (event.isComposing) return;
      if (event.key === 'Enter' || event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); canceled = event.key === 'Escape'; finish(); }
    });
    input.onblur = finish; input.focus({ preventScroll: true }); input.select();
  }
  function add(node, child) {
    W.finish(); var item = record(node), level, at;
    if (!item) { level = 1; at = W.text.length; }
    else { level = item.level + (child ? 1 : 0); at = item.end; }
    if (level > 6) { VexProto.toast('Markdown 最多支持六级标题。'); return; }
    var before = W.text.slice(0, at), after = W.text.slice(at);
    var separator = before ? /\n\n$/.test(before) ? '' : /\n$/.test(before) ? '\n' : '\n\n' : '';
    var prefix = before + separator, title = '#'.repeat(level) + ' 新章节\n\n';
    change(prefix + title + after, lineAt(prefix, prefix.length), child ? '已新增子章节' : '已新增同级章节', true);
  }
  function shifted(item, delta) {
    var list = [item].concat(item.descendants);
    if (list.some(function (candidate) { return candidate.level + delta < 1 || candidate.level + delta > 6; })) return null;
    var text = W.text.slice(item.start, item.end);
    list.slice().reverse().forEach(function (candidate) {
      var start = candidate.start - item.start, end = candidate.headerEnd - item.start;
      var old = text.slice(start, end), newline = /\n$/.test(old) ? '\n' : '';
      text = text.slice(0, start) + '#'.repeat(candidate.level + delta) + ' ' + candidate.raw.replace(/^ {0,3}#{1,6}\s+/, '').split('\n')[0].replace(/\s+#+\s*$/, '') + newline + text.slice(end);
    });
    return text;
  }
  function moveTo(node, target, position, message) {
    W.finish(); var item = record(node), destination = record(target);
    if (!item || node.isRoot || !destination || node.id === target.id || destination.start >= item.start && destination.start < item.end) return;
    var newLevel = destination.level + (position === 'child' ? 1 : 0), delta = newLevel - item.level;
    var moved = shifted(item, delta); if (moved === null) { VexProto.toast('这次移动会超过六级标题，请先调整子章节层级。'); return; }
    var at = position === 'before' ? destination.start : destination.end;
    var remaining = W.text.slice(0, item.start) + W.text.slice(item.end);
    if (at > item.start) at -= item.end - item.start;
    var prefix = remaining.slice(0, at), suffix = remaining.slice(at);
    if (prefix && !/\n\n$/.test(prefix)) prefix += /\n$/.test(prefix) ? '\n' : '\n\n';
    if (suffix && !/\n\n$/.test(moved)) moved += /\n$/.test(moved) ? '\n' : '\n\n';
    change(prefix + moved + suffix, lineAt(prefix, prefix.length), message || '章节及其正文、子章节已一起移动');
  }
  function moveSibling(node, direction) {
    var list = siblings(node), index = list.findIndex(function (candidate) { return candidate.id === node.id; }), other = list[index + direction];
    if (other) moveTo(node, other, direction < 0 ? 'before' : 'after');
  }
  function promote(node) {
    var parent = parentOf(node); if (!parent || parent.level === 0 || node.isRoot) return;
    moveTo(node, parent, 'after', '已提升一级，正文与子章节保持完整');
  }
  function demote(node) {
    var list = siblings(node), index = list.findIndex(function (candidate) { return candidate.id === node.id; });
    if (index > 0) moveTo(node, list[index - 1], 'child', '已归入上一章节，正文与子章节保持完整');
  }
  var deleteDialog = document.createElement('div'); deleteDialog.className = 'chapter-delete-dialog'; deleteDialog.id = 'chapter-delete-dialog'; deleteDialog.hidden = true;
  deleteDialog.innerHTML = '<section role="alertdialog" aria-modal="true" aria-labelledby="chapter-delete-title"><h2 id="chapter-delete-title">删除章节？</h2><p id="chapter-delete-description"></p><label><input type="checkbox" id="chapter-delete-keep">只移除标题，保留正文与子章节</label><div class="dlg-actions"><button class="btn" id="chapter-delete-cancel">取消</button><button class="btn danger" id="chapter-delete-confirm">删除章节</button></div></section>';
  document.body.appendChild(deleteDialog); var deleting = null, previousFocus;
  function requestDelete(node) {
    var item = record(node); if (!item || node.isRoot) return;
    deleting = node; previousFocus = document.activeElement;
    $('chapter-delete-title').textContent = '删除「' + node.text + '」？';
    $('chapter-delete-description').textContent = '这将移除本节正文' + (item.descendants.length ? '及 ' + item.descendants.length + ' 个子章节' : '') + '。可以使用撤销恢复。';
    $('chapter-delete-keep').checked = false; deleteDialog.hidden = false; $('chapter-delete-cancel').focus();
  }
  function cancelDelete() { deleteDialog.hidden = true; deleting = null; previousFocus?.focus({ preventScroll: true }); }
  $('chapter-delete-cancel').onclick = cancelDelete;
  deleteDialog.addEventListener('click', function (event) { if (event.target === deleteDialog) cancelDelete(); });
  $('chapter-delete-confirm').onclick = function () {
    var item = record(deleting); if (!item) return cancelDelete();
    var parent = parentOf(deleting), text = W.text.slice(0, item.start) + W.text.slice($('chapter-delete-keep').checked ? item.headerEnd : item.end);
    deleteDialog.hidden = true; deleting = null; change(text, parent?.line || 1, '已删除章节 · Ctrl+Z 撤销'); $('map-viewport').focus({ preventScroll: true });
  };
  var toolbar = document.createElement('div'); toolbar.className = 'chapter-toolbar';
  toolbar.innerHTML = '<button class="chapter-primary" id="chapter-add-child" title="新增子章节 · Tab">' + icon('plus') + '子章节</button><button id="chapter-add-sibling" title="新增同级章节 · Enter">' + icon('plus') + '同级</button><button id="chapter-headings-only" aria-pressed="false" title="只显示章节标题">仅标题</button>';
  document.querySelector('.map-toolbar').prepend(toolbar);
  $('chapter-add-child').onclick = function () { add(heading(), true); };
  $('chapter-add-sibling').onclick = function () { var node = heading(); add(node, !!node.isRoot); };
  var savedVisibility = null;
  $('chapter-headings-only').onclick = function () {
    var only = !W.settings.paragraphs && !W.settings.lists && !W.settings.blocks;
    if (!only) savedVisibility = { paragraphs: W.settings.paragraphs, lists: W.settings.lists, blocks: W.settings.blocks };
    [['show-paragraphs', 'paragraphs'], ['show-lists', 'lists'], ['show-blocks', 'blocks']].forEach(function (pair) { var input = $(pair[0]); input.checked = only ? (savedVisibility || { paragraphs: true, lists: true, blocks: false })[pair[1]] : false; input.dispatchEvent(new Event('change')); });
    updateHeadingToggle();
  };
  function updateHeadingToggle() { $('chapter-headings-only').setAttribute('aria-pressed', String(!W.settings.paragraphs && !W.settings.lists && !W.settings.blocks)); }
  document.querySelector('.map-hint').textContent = '导图与正文同步 · 拖动会移动整节正文及子章节';
  document.querySelector('.map-hint').title = '章节移动会同时移动正文和子章节；拖动画布空白处平移，Ctrl + 滚轮缩放。';
  function showInspector(node) {
    selectedLine = node.line;
    if (node.kind !== 'heading' || node.level === 0) return;
    var item = record(node); if (!item) return;
    var list = siblings(node), index = list.findIndex(function (candidate) { return candidate.id === node.id; }), parent = parentOf(node);
    $('inspector-kind').textContent = '章节 · H' + node.level; $('inspector-title').textContent = '';
    var body = W.text.slice(item.headerEnd, item.bodyEnd).replace(/^\n/, '').replace(/\n+$/, '');
    $('inspector-content').innerHTML = '<div class="chapter-inspector"><label for="chapter-title-input">章节标题</label><div class="chapter-title-row"><span class="chapter-level">H' + node.level + '</span><input class="chapter-title-input" id="chapter-title-input" value="' + escape(node.text) + '"></div>'
      + '<div class="chapter-actions">'
      + '<button data-chapter-command="sibling"' + (node.isRoot ? ' disabled' : '') + '>＋ 同级</button><button data-chapter-command="child"' + (node.level >= 6 ? ' disabled' : '') + '>＋ 子级</button><button data-chapter-command="rename">修改标题</button>'
      + '<button data-chapter-command="promote"' + (!parent || parent.level === 0 || node.isRoot ? ' disabled' : '') + '>← 提升</button><button data-chapter-command="demote"' + (index <= 0 || list[index - 1]?.level >= 6 ? ' disabled' : '') + '>→ 降级</button><button data-chapter-command="up"' + (index <= 0 ? ' disabled' : '') + '>↑ 上移</button>'
      + '<button data-chapter-command="down"' + (index < 0 || index >= list.length - 1 ? ' disabled' : '') + '>↓ 下移</button><button class="chapter-delete-button" data-chapter-command="delete"' + (node.isRoot ? ' disabled' : '') + '>删除章节</button></div>'
      + '<label for="chapter-body-input">本节正文</label><textarea class="chapter-body-input" id="chapter-body-input" placeholder="先写下这一节要表达的内容…">' + escape(body) + '</textarea><p class="chapter-body-note">这里的修改会同步到文章。支持 Markdown，子章节单独保留；离开输入框即同步，可撤销。</p></div>';
    var title = $('chapter-title-input');
    title.onchange = function () { rename(node, title.value); };
    title.onkeydown = function (event) { if (event.isComposing) return; if (event.key === 'Enter') { event.preventDefault(); title.blur(); } if (event.key === 'Escape') { title.value = node.text; title.blur(); event.stopPropagation(); } };
    $('chapter-body-input').onchange = function () {
      var latest = record(node); if (!latest) return;
      var bodyText = this.value.trim(), replacement = bodyText ? '\n' + bodyText + '\n\n' : '\n';
      change(W.text.slice(0, latest.headerEnd) + replacement + W.text.slice(latest.bodyEnd), latest.line, '本节正文已同步');
    };
    document.querySelectorAll('[data-chapter-command]').forEach(function (button) {
      button.onclick = function () { execute(button.dataset.chapterCommand, node); };
    });
  }
  function execute(command, node) {
    if (command === 'sibling') add(node, false);
    if (command === 'child') add(node, true);
    if (command === 'rename') beginRename(node);
    if (command === 'promote') promote(node);
    if (command === 'demote') demote(node);
    if (command === 'up') moveSibling(node, -1);
    if (command === 'down') moveSibling(node, 1);
    if (command === 'delete') requestDelete(node);
  }
  window.addEventListener('vex-node-selected', function (event) { showInspector(event.detail); });
  function attachNodes() {
    updateHeadingToggle();
    document.querySelectorAll('.map-node[data-kind="heading"]').forEach(function (element) {
      var node = W.state.nodes.find(function (candidate) { return candidate.id === element.dataset.nodeId; }); if (!node) return;
      element.draggable = !node.isRoot;
      var button = element.querySelector('.map-node-button'); button.setAttribute('aria-label', node.text + '，双击修改章节标题');
      button.ondblclick = function (event) { event.preventDefault(); event.stopPropagation(); W.selectNode(node); beginRename(node); };
      element.ondragstart = function (event) { if (node.isRoot || event.target.closest('input')) { event.preventDefault(); return; } dragging = node; event.stopPropagation(); event.dataTransfer.setData('application/x-vex-chapter', String(node.line)); event.dataTransfer.effectAllowed = 'move'; };
      element.ondragover = function (event) {
        drop = null; clearDropClasses(); dropPreview.hidden = true;
        if (!dragging || dragging.id === node.id) return;
        var source = record(dragging), target = record(node); if (!source || !target || target.start >= source.start && target.start < source.end) return;
        event.preventDefault(); event.stopPropagation(); clearDropClasses();
        var rect = element.getBoundingClientRect(), ratio = (event.clientY - rect.top) / rect.height;
        var position = node.isRoot ? 'child' : ratio < .25 ? 'before' : ratio > .75 ? 'after' : 'child';
        var level = target.level + (position === 'child' ? 1 : 0);
        if ([source].concat(source.descendants).some(function (item) { return item.level + level - source.level > 6; })) return;
        element.classList.add('chapter-drag-' + position); drop = { node: node, position: position };
        dropPreview.textContent = (position === 'child' ? '归入「' + node.text + '」成为子章节' : '移到「' + node.text + '」' + (position === 'before' ? '之前' : '之后')) + ' · H' + level + ' · 包含正文和 ' + source.descendants.length + ' 个子章节';
        dropPreview.hidden = false;
      };
      element.ondragleave = function (event) { if (!element.contains(event.relatedTarget)) { drop = null; clearDropClasses(); dropPreview.hidden = true; } };
      element.ondrop = function (event) { if (!dragging || !drop) return; event.preventDefault(); event.stopPropagation(); var source = dragging, target = drop; endDrag(); moveTo(source, target.node, target.position); };
      element.ondragend = endDrag;
    });
  }
  function clearDropClasses() { document.querySelectorAll('.chapter-drag-before,.chapter-drag-after,.chapter-drag-child').forEach(function (element) { element.classList.remove('chapter-drag-before', 'chapter-drag-after', 'chapter-drag-child'); }); }
  function endDrag() { dragging = null; drop = null; clearDropClasses(); dropPreview.hidden = true; }
  window.addEventListener('vex-document-loaded', function () { feedback.hidden = true; endDrag(); });
  window.addEventListener('vex-content-changed', function () { if (W.text !== feedbackText) feedback.hidden = true; });
  window.addEventListener('vex-map-rendered', attachNodes);
  $('map-viewport').addEventListener('keydown', function (event) {
    if (event.isComposing || event.target.closest('input,textarea') || event.ctrlKey || event.metaKey || event.altKey) return;
    var node = selected(); if (!node || node.kind !== 'heading') return;
    var command = { Enter: 'sibling', Tab: 'child', F2: 'rename', Delete: 'delete' }[event.key];
    if (!command) return;
    event.preventDefault(); event.stopPropagation();
    if (event.key === 'Enter' && node.isRoot) command = 'child';
    execute(command, node);
  });
  deleteDialog.addEventListener('keydown', function (event) {
    if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); cancelDelete(); }
    if (event.key === 'Tab') { var items = Array.from(deleteDialog.querySelectorAll('input,button')), at = items.indexOf(document.activeElement); event.preventDefault(); items[(at + (event.shiftKey ? items.length - 1 : 1)) % items.length].focus(); }
  });
  attachNodes();
  window.VexChapters = { rename: rename, add: add, move: moveTo, promote: promote, demote: demote, record: record };
})();
