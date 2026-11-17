/* 命令面板（F1）+ Mermaid 图鉴选择器 + 界面微状态（视图徽标 / MCP 服务徽标）。
   菜单收敛后的全命令检索入口：所有退役菜单命令（编辑/插入/段落/格式/工具）在此保留。
   依赖：VexProto.handleAction（workbench.js，须在本文件之前加载）、VexMermaid.items（mermaid-catalog.js）。 */
(function () {
  'use strict';
  var VexProto = window.VexProto;
  /* 本地化取值：zh-CN 原文直出，其余语言查 label 表（与 localization.js 同源）。 */
  var L = function (text) { return window.VexL && VexL.label ? VexL.label(text) : text; };

  /* ---------- 命令表：group 为分类徽标，label 为检索主词 ---------- */
  var COMMANDS = [];
  function C(group, label, action, gesture, extra) {
    COMMANDS.push(Object.assign({ group: group, label: label, action: action, gesture: gesture || '' }, extra || {}));
  }
  C('文件', '新建', 'new', 'Ctrl+N'); C('文件', '从模板新建', 'new-from-template'); C('文件', '新建窗口', 'new-window', 'Ctrl+Shift+N');
  C('文件', '打开', 'open', 'Ctrl+O'); C('文件', '打开文件夹', 'open-folder'); C('文件', '打开演示文档', 'open-samples'); C('文件', '快速打开', 'quick-open', 'Ctrl+P');
  C('文件', '保存', 'save', 'Ctrl+S'); C('文件', '另存为', 'save-as', 'Ctrl+Shift+S'); C('文件', '另存为模板', 'save-as-template'); C('文件', '重命名', 'dlg-rename');
  C('文件', '导出 HTML', 'export-html'); C('文件', '导出 PDF', 'export-pdf'); C('文件', '打印预览', 'print', 'Ctrl+Shift+P');
  C('文件', '关闭文档', 'close', 'Ctrl+W'); C('文件', '从工作区移除', 'dlg-delete');
  C('编辑', '撤销', 'undo', 'Ctrl+Z'); C('编辑', '重做', 'redo', 'Ctrl+Y');
  C('编辑', '剪切', 'cut', 'Ctrl+X'); C('编辑', '复制', 'copy', 'Ctrl+C'); C('编辑', '粘贴', 'paste', 'Ctrl+V');
  C('编辑', '全选', 'select-all', 'Ctrl+A'); C('编辑', '复制为纯文本', 'copy-plain', 'Ctrl+Shift+C');
  C('编辑', '查找', 'find', 'Ctrl+F'); C('编辑', '替换', 'replace', 'Ctrl+H');
  C('插入', '段落', 'insert-paragraph');
  ['1', '2', '3', '4', '5', '6'].forEach(function (level) { C('插入', '标题 ' + level, 'insert-heading' + level, 'Ctrl+' + level); });
  C('插入', '无序列表', 'insert-list'); C('插入', '有序列表', 'insert-ordered'); C('插入', '任务列表', 'insert-task'); C('插入', '引用', 'insert-quote');
  C('插入', '表格', 'insert-table'); C('插入', '代码块', 'insert-code'); C('插入', '公式块', 'insert-math');
  C('插入', '水平分割线', 'insert-rule'); C('插入', '脚注', 'insert-footnote'); C('插入', '链接', 'insert-link', 'Ctrl+K'); C('插入', '图片', 'insert-image');
  C('插入', 'Mermaid 图鉴（全部类型）', 'insert-mermaid-picker');
  (window.VexMermaid ? VexMermaid.items : []).forEach(function (item) {
    C('插入 · 图表', 'Mermaid ' + item.label, 'insert-mermaid-' + item.key);
  });
  C('格式', '加粗', 'insert-bold', 'Ctrl+B'); C('格式', '斜体', 'insert-italic', 'Ctrl+I');
  C('格式', '删除线', 'insert-strike', 'Ctrl+Shift+5'); C('格式', '高亮', 'insert-highlight', 'Ctrl+Shift+H');
  C('格式', '上标', 'insert-superscript', 'Ctrl+Shift+='); C('格式', '下标', 'insert-subscript', 'Ctrl+Shift+-');
  C('格式', '行内代码', 'insert-inline-code', 'Ctrl+Shift+`'); C('格式', '清除格式', 'clear-format');
  C('视图', '切换：显示行号', 'toggle-lines'); C('视图', '切换：显示词数', 'toggle-word-count');
  C('视图', '切换：显示文档水印', 'toggle-watermark'); C('视图', '切换：界面动效', 'toggle-motion');
  C('视图', '切换：自动配对输入', 'toggle-pairs'); C('视图', '切换：打字机模式', 'toggle-typewriter');
  C('视图', '切换：专注模式', 'toggle-focus', 'F8'); C('视图', '切换：全屏', 'toggle-fullscreen', 'F11');
  C('视图', '切换：窗口置顶', 'toggle-topmost');
  C('视图', '演示模式', 'presentation', 'F6');
  C('视图', '转到编辑视图', 'view-edit'); C('视图', '转到阅读视图', 'view-read'); C('视图', '转到导图视图', 'view-mindmap');
  C('视图', '即时编辑模式', 'edit-instant'); C('视图', '源码编辑模式', 'edit-source');
  C('帮助', '命令面板', 'command-palette', 'F1'); C('帮助', '新手引导', 'guide');
  C('帮助', '更新日志', 'dlg-doc', null, { arg: '更新日志' }); C('帮助', '鸣谢', 'dlg-doc', null, { arg: '鸣谢' });
  C('帮助', '检查更新', 'dlg-update'); C('帮助', '关于 Vex', 'dlg-about');
  C('设置', '偏好设置', 'preferences'); C('设置', 'MCP 服务配置', 'preferences-mcp'); C('设置', 'MCP 调用审计', 'mcp-audit');

  function dispatch(item) {
    if (item.toast) { VexProto.toast(item.toast); return; }
    if (typeof VexProto.handleAction === 'function') VexProto.handleAction(item.action, item);
  }

  /* ---------- 命令面板浮层 ---------- */
  var backdrop = null, listEl = null, input = null, matches = [], cursor = 0;
  function buildPalette() {
    backdrop = document.createElement('div');
    backdrop.className = 'cp-backdrop';
    backdrop.hidden = true;
    var panel = document.createElement('div');
    panel.className = 'cp-panel';
    panel.setAttribute('role', 'dialog');
    panel.setAttribute('aria-modal', 'true');
    panel.setAttribute('aria-label', L('命令面板'));
    input = document.createElement('input');
    input.className = 'cp-input';
    input.type = 'text';
    input.placeholder = L('搜索命令（全部命令，含图表 / 布局 / 开关）…');
    input.setAttribute('aria-label', L('搜索命令'));
    listEl = document.createElement('div');
    listEl.className = 'cp-list';
    listEl.setAttribute('role', 'listbox');
    var foot = document.createElement('div');
    foot.className = 'cp-foot';
    foot.innerHTML = '<span>' + L('↑↓ 选择') + '</span><span>' + L('Enter 执行') + '</span><span>' + L('Esc 关闭') + '</span>';
    panel.appendChild(input); panel.appendChild(listEl); panel.appendChild(foot);
    backdrop.appendChild(panel);
    document.body.appendChild(backdrop);
    backdrop.addEventListener('pointerdown', function (event) { if (event.target === backdrop) close(); });
    input.addEventListener('input', function () { render(); });
    input.addEventListener('keydown', function (event) {
      if (event.isComposing) return;
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
        event.preventDefault();
        if (!matches.length) return;
        cursor = (cursor + (event.key === 'ArrowDown' ? 1 : matches.length - 1)) % matches.length;
        paint(); return;
      }
      if (event.key === 'Home' || event.key === 'End') {
        event.preventDefault(); if (!matches.length) return;
        cursor = event.key === 'Home' ? 0 : matches.length - 1; paint(); return;
      }
      if (event.key === 'Enter') { event.preventDefault(); var item = matches[cursor]; if (item) { close(); dispatch(item); } return; }
      if (event.key === 'Escape') { event.preventDefault(); close(); }
    });
  }
  function needle() { return input.value.trim().toLowerCase(); }
  function render() {
    var q = needle();
    matches = COMMANDS.filter(function (item) {
      if (!q) return true;
      return (item.label + ' ' + item.group).toLowerCase().indexOf(q) >= 0;
    });
    cursor = 0;
    listEl.innerHTML = '';
    if (!matches.length) {
      var empty = document.createElement('div');
      empty.className = 'cp-empty'; empty.textContent = L('没有匹配的命令');
      listEl.appendChild(empty); return;
    }
    matches.slice(0, 40).forEach(function (item, index) {
      var row = document.createElement('div');
      row.className = 'cp-item' + (index === cursor ? ' active' : '');
      row.setAttribute('role', 'option');
      row.tabIndex = -1;
      row.innerHTML = '<span class="cp-label"></span><span class="cp-group"></span>' + (item.gesture ? '<span class="cp-gesture">' + item.gesture + '</span>' : '');
      row.querySelector('.cp-label').textContent = item.label;
      row.querySelector('.cp-group').textContent = item.group;
      row.addEventListener('mouseenter', function () { cursor = index; paint(); });
      row.addEventListener('click', function () { close(); dispatch(item); });
      listEl.appendChild(row);
    });
    if (matches.length > 40) {
      var more = document.createElement('div');
      more.className = 'cp-empty'; more.textContent = L('还有') + ' ' + (matches.length - 40) + ' ' + L('条，继续输入缩小范围');
      listEl.appendChild(more);
    }
  }
  function paint() {
    var rows = listEl.querySelectorAll('.cp-item');
    rows.forEach(function (row, index) { row.classList.toggle('active', index === cursor); });
    var active = rows[cursor];
    if (active) active.scrollIntoView({ block: 'nearest' });
  }
  function open() {
    if (!backdrop) buildPalette();
    backdrop.hidden = false;
    input.value = ''; render();
    requestAnimationFrame(function () { input.focus({ preventScroll: true }); });
  }
  function close() { if (backdrop) { backdrop.hidden = true; input.blur(); } }
  function toggle() { if (!backdrop || backdrop.hidden) open(); else close(); }

  document.addEventListener('keydown', function (event) {
    if (event.key === 'F1' && !event.isComposing && !event.defaultPrevented) {
      event.preventDefault(); event.stopPropagation();
      toggle();
    }
    if (event.key === 'Escape' && backdrop && !backdrop.hidden && document.activeElement !== input) close();
  }, true);

  /* ---------- Mermaid 图鉴选择器：24 类型分组网格 ---------- */
  var picker = null, thumbSerial = 0;
  function buildPicker() {
    picker = document.createElement('div');
    picker.className = 'cp-backdrop dg-backdrop';
    picker.hidden = true;
    var panel = document.createElement('div');
    panel.className = 'dg-panel';
    panel.setAttribute('role', 'dialog');
    panel.setAttribute('aria-modal', 'true');
    panel.setAttribute('aria-label', 'Mermaid 图鉴');
    var head = document.createElement('div');
    head.className = 'dg-head';
    head.innerHTML = '<b>' + L('Mermaid 图鉴') + '</b><small>' + L('24 类图表 · 单击插入') + '</small>';
    var close = document.createElement('button');
    close.type = 'button'; close.className = 'icon-button dg-close';
    close.setAttribute('aria-label', L('关闭图鉴')); close.textContent = '×';
    close.addEventListener('click', function () { closePicker(); });
    head.appendChild(close);
    var grid = document.createElement('div');
    grid.className = 'dg-grid';
    /* 分组 tab（对话框头下方）：选组看大预览，默认第一组。 */
    var tabs = document.createElement('div');
    tabs.className = 'dg-tabs';
    var groups = [];
    (window.VexMermaid ? VexMermaid.items : []).forEach(function (item) { if (groups.indexOf(item.group) < 0) groups.push(item.group); });
    groups.forEach(function (group, index) {
      var tab = document.createElement('button');
      tab.type = 'button'; tab.className = 'dg-tab' + (index === 0 ? ' on' : ''); tab.textContent = group;
      tab.setAttribute('aria-pressed', String(index === 0));
      tab.addEventListener('click', function () {
        tabs.querySelectorAll('.dg-tab').forEach(function (t) { t.classList.remove('on'); t.setAttribute('aria-pressed', 'false'); });
        tab.classList.add('on'); tab.setAttribute('aria-pressed', 'true');
        grid.querySelectorAll('.dg-card').forEach(function (card) { card.hidden = card.dataset.group !== group; });
      });
      tabs.appendChild(tab);
    });
    (window.VexMermaid ? VexMermaid.items : []).forEach(function (item) {
      var card = document.createElement('button');
      card.type = 'button'; card.className = 'dg-card'; card.dataset.group = item.group;
      if (item.group !== groups[0]) card.hidden = true;
      card.innerHTML = '<span class="dg-thumb" aria-hidden="true"></span><span class="dg-label"></span>';
      card.querySelector('.dg-label').textContent = item.label;
      card.title = item.label + '（' + item.group + '）';
      card.addEventListener('click', function () {
        closePicker();
        if (typeof VexProto.handleAction === 'function') VexProto.handleAction('insert-mermaid-' + item.key, { label: item.label, action: 'insert-mermaid-' + item.key });
      });
      grid.appendChild(card);
      // 缩略图：真实渲染各类型的迷你图。render 要纯 mermaid 源码（item.source），
      // snippet 是带 ```mermaid 围栏的插入串；render(id, source) 返回已完成 id 前缀隔离的
      // svg 字符串，唯一 id 由这里的 serial 保证，避免同页 24 张图 id 撞车。
      var thumbHost = card.querySelector('.dg-thumb');
      if (window.VexDiagramRendering && item.source) {
        var serial = ++thumbSerial;
        VexDiagramRendering.render('dg-thumb-' + serial, item.source).then(function (svg) {
          if (!thumbHost.isConnected) return;
          thumbHost.innerHTML = svg;
          thumbHost.classList.add('ready');
        }).catch(function () { thumbHost.classList.add('failed'); });
      }
    });
    panel.appendChild(head); panel.appendChild(tabs); panel.appendChild(grid);
    picker.appendChild(panel);
    document.body.appendChild(picker);
    picker.addEventListener('pointerdown', function (event) { if (event.target === picker) closePicker(); });
    picker.addEventListener('keydown', function (event) { if (event.key === 'Escape') { event.preventDefault(); closePicker(); } });
  }
  function openPicker() {
    if (!picker) buildPicker();
    picker.hidden = false;
    var close = picker.querySelector('.dg-close');
    if (close) close.focus({ preventScroll: true });
  }
  function closePicker() { if (picker) picker.hidden = true; }

  /* ---------- 动作拦截：命令面板 / 图鉴入口（workbench.js 之后加载，handleAction 已就绪） ---------- */
  var previousHandle = window.VexProto && VexProto.handleAction;
  if (previousHandle) {
    VexProto.handleAction = function (action, item) {
      if (action === 'command-palette') { toggle(); return true; }
      if (action === 'insert-mermaid-picker') { openPicker(); return true; }
      return previousHandle(action, item);
    };
  }

  /* ---------- 视图徽标：字数旁常驻当前视图（数据源 = workspace[data-view] + 源码面板可见性） ---------- */
  function initViewChip() {
    var tools = document.querySelector('.document-tools');
    var workspace = document.querySelector('.workspace');
    var sourcePane = document.getElementById('source-pane');
    if (!tools || !workspace) return;
    var chip = document.createElement('span');
    chip.className = 'sb-layout'; chip.id = 'sb-layout';
    chip.title = '当前视图';
    tools.insertBefore(chip, tools.firstChild);
    function refresh() {
      var view = workspace.dataset.view || 'edit';
      var label = view === 'read' ? '阅读' : view === 'mindmap' ? '导图'
        : (sourcePane && !sourcePane.hidden ? '源码' : '即时');
      chip.textContent = label;
      chip.dataset.view = view;
    }
    refresh();
    new MutationObserver(refresh).observe(workspace, { attributes: true, attributeFilter: ['data-view'] });
    if (sourcePane) new MutationObserver(refresh).observe(sourcePane, { attributes: true, attributeFilter: ['hidden'] });
  }

  /* ---------- MCP 服务徽标：设置图标上的运行态小点 ---------- */
  function initMcpBadge() {
    var settings = document.querySelector('.rail-item[data-action="preferences"]');
    if (!settings) return;
    var badge = document.createElement('i');
    badge.className = 'mcp-badge'; badge.hidden = true;
    badge.title = 'MCP 服务运行中';
    settings.appendChild(badge);
    document.addEventListener('change', function (event) {
      var field = event.target.closest && event.target.closest('[data-mcp-field="mcpEnabled"]');
      if (field) badge.hidden = !field.checked;
    });
  }

  function boot() { initViewChip(); initMcpBadge(); initBandHeading(); }
  /* 写作工具带的段落结构下拉：档位值即动作名（insert-paragraph / insert-headingN）。 */
  function initBandHeading() {
    var select = document.getElementById('band-heading');
    if (!select) return;
    for (var oi = 0; oi < select.options.length; oi++) select.options[oi].text = L(select.options[oi].text);
    select.addEventListener('change', function () {
      if (!select.value) return;
      if (typeof VexProto.handleAction === 'function') VexProto.handleAction(select.value, { label: select.options[select.selectedIndex].text, action: select.value });
      select.value = 'insert-paragraph';
    });
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
  else boot();

  window.VexCommandPalette = { open: open, close: close, toggle: toggle, openDiagramPicker: openPicker };
})();
