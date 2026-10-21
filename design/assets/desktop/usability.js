/* 用户评审：明确状态、辅助面板与多视图控制。只操作原型内存。 */
(function () {
  'use strict';
  var W = window.VexWorkbench, $ = function (id) { return document.getElementById(id); };
  var status = null, details = document.createElement('section');
  details.id = 'document-save-details'; details.className = 'save-details'; details.hidden = true;
  details.setAttribute('role', 'dialog'); details.setAttribute('aria-label', '文档与保存状态');
  details.innerHTML = '<div class="usability-panel-heading"><strong>文档与保存状态</strong><button type="button" class="icon-button" aria-label="关闭保存详情">×</button></div><dl><dt>当前文件</dt><dd id="save-file-name"></dd><dt>来源位置</dt><dd id="save-source-location"></dd><dt>副本位置</dt><dd>浏览器下载目录，具体位置由浏览器设置决定</dd><dt>最近一次下载</dt><dd id="save-last-export"></dd><dt>恢复草稿</dt><dd id="save-draft-state"></dd></dl><p>本原型通过下载保存 Markdown 副本，不写回来源文件。下载发起后，请在浏览器下载记录中确认完成。</p><button class="btn primary" type="button" id="save-details-retry">保存 Markdown 副本</button>';
  document.querySelector('.window-body').appendChild(details);
  function updateSaveDetails() {
    var doc = W.documents[W.state.doc];
    $('save-file-name').textContent = doc.name;
    $('save-source-location').textContent = doc.isNew ? '新建文档，尚未保存到磁盘' : doc.path || (doc.readOnly ? '内置只读文档 / ' : '演示文档 / ') + doc.name;
    $('save-last-export').textContent = doc.saveError ? '下载失败，当前内容仍在编辑器中。可重试。' : doc.lastExport ? doc.lastExport + ' · 已发起，完成情况请查看浏览器' : '尚未下载副本';
    $('save-draft-state').textContent = doc.recovered ? '已恢复草稿；恢复不等于已保存，请另存副本' : '当前没有载入恢复草稿';
    $('save-details-retry').textContent = doc.saveError ? '重试下载副本' : '保存 Markdown 副本';
    $('save-details-retry').disabled = !!doc.readOnly || W.state.emptyWorkspace;
  }
  function openSaveDetails() { if (details.hidden) { updateSaveDetails(); details.hidden = false; details.querySelector('button').focus(); } else { details.hidden = true; } }
  function closeSaveDetails() { details.hidden = true; }
  window.addEventListener('vex-open-save-details', openSaveDetails);
  details.querySelector('.icon-button').onclick = function () { closeSaveDetails(true); };
  $('save-details-retry').onclick = function () { VexProto.handleAction('save'); updateSaveDetails(); };
  document.addEventListener('pointerdown', function (event) { if (!details.contains(event.target)) closeSaveDetails(); });
  document.addEventListener('keydown', function (event) { if (event.key === 'Escape' && !details.hidden) closeSaveDetails(true); });
  window.addEventListener('vex-save-state-changed', updateSaveDetails);

  function updateFollow() {
    var following = W.state.followViews;
    $('view-follow-toggle').textContent = following ? '联动开启' : '自由浏览';
    $('view-follow-toggle').setAttribute('aria-pressed', String(following));
    $('view-follow-toggle').title = following ? '暂停视图联动，各栏独立浏览' : '恢复视图联动，并回到编辑位置';
    $('view-follow-driver').textContent = following ? '跟随' + (W.state.followDriver || '编辑位置') : '各栏独立浏览';
  }
  $('view-follow-toggle').onclick = function () { W.setFollow(!W.state.followViews); };
  $('return-to-editing').onclick = function () { VexWorkspace.openPanel('editor'); W.returnToEditing(); };
  window.addEventListener('vex-follow-changed', updateFollow); updateFollow();
  function focusPanel(target) {
    var active = target.closest('.view-panel'); if (!active) return;
    document.querySelectorAll('.view-panel').forEach(function (panel) { panel.classList.toggle('active-view', panel === active); });
  }
  document.addEventListener('focusin', function (event) { focusPanel(event.target); });
  document.addEventListener('pointerdown', function (event) { focusPanel(event.target); });

  var welcome = document.createElement('section'); welcome.id = 'workspace-welcome'; welcome.className = 'workspace-welcome';
  welcome.innerHTML = '<div><span class="welcome-kicker">从一篇文章开始</span><h1>写下第一个想法</h1><p>新建一篇空白文档，或打开本地 Markdown。<br>标题会自动形成大纲与导图。</p><div><button class="btn primary" data-action="new">新建文档</button><button class="btn" data-action="open">打开文件…</button><button class="btn" data-action="open-folder">打开文件夹…</button></div><small>编辑内容保留在当前页面；保存副本会下载 Markdown 文件。</small></div>';
  $('view-surface').before(welcome);
  var blankNote = document.createElement('div'); blankNote.className = 'blank-document-note'; blankNote.textContent = '空白文档 · 点击正文开始写作，输入 # 添加标题。';
  document.querySelector('.content-area').before(blankNote);
  var mapEmpty = document.createElement('div'); mapEmpty.className = 'map-empty-hint';
  mapEmpty.innerHTML = '<strong>正文还没有章节标题</strong><span>在正文中输入 # 标题，章节会出现在这里。</span><button type="button">回到正文</button>';
  $('map-viewport').appendChild(mapEmpty); mapEmpty.querySelector('button').onclick = function () { VexWorkspace.setLayout('instant'); W.returnToEditing(); };
  function updateEmptyState() {
    var empty = W.state.emptyWorkspace;
    document.querySelector('.workspace-main').classList.toggle('workspace-empty', empty); welcome.hidden = !empty;
    blankNote.hidden = empty || !!W.text.trim();
    mapEmpty.hidden = empty || W.state.parsed.blocks.some(function (block) { return block.kind === 'heading'; });
    var saveButton = document.querySelector('.save-button'); if (saveButton) saveButton.disabled = empty || !!W.documents[W.state.doc].readOnly;
    $('document-name').textContent = empty ? '未打开文档' : W.name;
  }
  window.addEventListener('vex-content-changed', updateEmptyState); window.addEventListener('vex-document-loaded', updateEmptyState); updateEmptyState();

  function updateSelectionCount() {
    var input = $('markdown-input'), selected = '', count = 0;
    if (document.activeElement === input && input.selectionStart !== input.selectionEnd) {
      selected = input.value.slice(input.selectionStart, input.selectionEnd); count = VexWriting.metrics(selected).count;
    } else {
      var selection = window.getSelection();
      if (selection && !selection.isCollapsed && [selection.anchorNode, selection.focusNode].every(function (node) { var element = node && (node.nodeType === 1 ? node : node.parentElement); return element && element.closest('#instant-content, #markdown-preview'); })) {
        selected = selection.toString(); count = VexWriting.measureText(selected).count;
      }
    }
    $('sb-wordcount').textContent = (selected ? '选中 ' + count.toLocaleString() + ' · ' : '') + '字数 ' + VexWriting.metrics(W.text).count.toLocaleString();
  }
  document.addEventListener('selectionchange', updateSelectionCount); $('markdown-input').addEventListener('select', updateSelectionCount);
  window.addEventListener('vex-content-changed', updateSelectionCount);
  updateSaveDetails(); updateSelectionCount();
})();
