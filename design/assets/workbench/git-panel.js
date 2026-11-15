/* Git 管理界面（原型）：GitHub Desktop 式全客户区左右布局。
 * rail 切换 文件/Git 两个模式：Git 模式整体替换编辑工作区；文件模式的侧栏折叠按钮在标题栏。
 * 状态机：无仓库 → 初始化 → 变更/提交（Changes/History 双 tab + 文件级 diff）→ 添加远程（推送/拉取）。
 * Diff 显示双模式：Unified 单栏 / Split 左右对照（右上齿轮弹层切换，含隐藏空白变更），带行号与增删统计。
 * 正式实现走 Vex.Git（LibGit2Sharp 嵌入式，迁移自 Vox.Git）；此处全部为演示状态，localStorage 持久化。 */
(function () {
  'use strict';
  var esc = function (value) { return String(value).replace(/[&<>"']/g, function (c) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]; }); };
  var t = function (text) { return window.VexL ? VexL.t(text) : text; };
  var pane = document.getElementById('git-workspace');
  if (!pane) return;

  /* ---------- 演示状态 ---------- */
  var STORE_KEY = 'vex-design-git-state';
  var defaults = {
    repo: false, branch: 'main', remote: null, lastSync: null,
    changes: [], history: [], ahead: 0, behind: 0, pulledOnce: false, nextAutoAt: 0
  };
  var state = JSON.parse(JSON.stringify(defaults));
  try {
    var saved = JSON.parse(VexStorage.getItem(STORE_KEY) || 'null');
    if (saved && typeof saved === 'object' && typeof saved.repo === 'boolean') state = Object.assign(state, saved);
  } catch (_) {}
  function persist() { try { VexStorage.setItem(STORE_KEY, JSON.stringify(state)); } catch (_) {} }
  /* 会话态：tab、选中项与 Diff 显示偏好不持久化 */
  var tab = 'changes', selectedChange = null, selectedCommit = null, selectedCommitFile = null;
  var diffMode = 'unified', hideWhitespace = false, diffOptionsOpen = false;

  var query = new URLSearchParams(location.search);
  if (!state.repo && (query.get('scenario') === 'git' || query.get('git') === '1')) initRepo(true);

  /* ---------- 模拟数据 ---------- */
  function seedChanges() {
    return [
      { path: '笔记/架构设计.md', status: 'M', checked: true,
        diff: [[' ', '## 架构总览'], [' ', ''], ['-', '编辑器采用单文档模型，视图共享状态。'], ['+', '编辑器采用单文档模型，四视图共享同一份文档状态。'], ['+', '视图间通过 IWorkspaceDocumentState 同步光标与选区。'], [' ', '切换布局时保留当前章节位置与撤销栈。']] },
      { path: '笔记/模板中心调研.md', status: 'M', checked: true,
        diff: [[' ', '### 模板格式'], ['-', '模板携带 YAML 元数据（title/kind/standard）。'], ['+', '模板 = 纯 Markdown 文件，分组对应子文件夹，无元数据。'], ['+', '唯一标识 = 分组 + 模板名，同名覆盖仅限我的模板。'], ['+', '   '], [' ', '另存为模板不展示预览，内容取自当前文档。']] },
      { path: 'assets/截图.png', status: 'A', checked: true,
        diff: [['+', '（二进制文件，桌面版展示图片对比）']] },
      { path: '草稿/待整理.md', status: '??', checked: false,
        diff: [['+', '# 待整理'], ['+', '- 阅读视图的断行规则'], ['+', '- 导图摘要长度默认值'], ['+', '- 模板分组的文件夹语义']] }
    ];
  }
  function seedHistory() {
    return [
      { hash: 'c41a9f2', subject: 'docs: 整理阅读视图的使用说明', author: 'Dotnet9(dotnet9.com)', time: '昨天 21:04', files: [{ path: '笔记/阅读视图.md', status: 'M', diff: [[' ', '## 阅读跟随'], ['-', '阅读跟随抢占正文焦点。'], ['+', '阅读跟随只更新状态，不抢占正文焦点。']] }] },
      { hash: '8e02b7d', subject: 'init: 初始化本地仓库', author: 'Dotnet9(dotnet9.com)', time: '昨天 20:37', tag: 'v1.0', files: [{ path: '笔记/写作路线.md', status: 'A', diff: [['+', '# 写作路线'], ['+', '- 即时编辑为主，源码为辅。'], ['+', '- 每周整理一次草稿目录。']] }] }
    ];
  }
  function initRepo(silent) {
    state.repo = true; state.changes = seedChanges(); state.history = seedHistory();
    persist(); render();
    if (!silent) VexProto.toast(t('已在当前文件夹初始化本地仓库'));
  }

  /* ---------- 工具 ---------- */
  function now() { var d = new Date(); return ('0' + d.getHours()).slice(-2) + ':' + ('0' + d.getMinutes()).slice(-2); }
  function checkedChanges() { return state.changes.filter(function (c) { return c.checked; }); }
  function isSsh(url) { return /^git@|^ssh:\/\//i.test(url || ''); }
  function repoName() {
    if (state.remote) { var m = /\/([^\/]+?)(?:\.git)?$/.exec(state.remote.replace(/\/$/, '')); if (m) return m[1]; }
    return 'vex-notes';
  }
  function icon(name) { return '<svg class="ico" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; }
  function statusDot(status) {
    var cls = { 'M': 'm', 'A': 'a', 'D': 'd', '??': 'u' }[status] || 'u';
    var label = { 'M': '修改', 'A': '新增', 'D': '删除', '??': '未跟踪' }[status] || status;
    return '<span class="git-status ' + cls + '" title="' + esc(label) + '"></span>';
  }
  function diffStats(diff) {
    var add = 0, del = 0;
    diff.forEach(function (r) { if (r[0] === '+') add++; else if (r[0] === '-') del++; });
    return { add: add, del: del };
  }

  /* ---------- Diff 渲染（Unified / Split 双模式 + 隐藏空白变更 + 行号） ---------- */
  function effectiveDiff(diff) {
    return hideWhitespace
      ? diff.filter(function (r) { return r[0] === ' ' || String(r[1]).trim(); })
      : diff;
  }
  function diffHtml(file) {
    var rows = effectiveDiff(file.diff);
    var stats = diffStats(rows);
    var hunk = '@@ -1,' + (rows.length - stats.add) + ' +' + 1 + ',' + (rows.length - stats.del) + ' @@';
    var body = diffMode === 'split' ? splitRowsHtml(rows) : unifiedRowsHtml(rows);
    return '<div class="git-hunk-head">' + esc(hunk) + '</div><div class="git-diff ' + diffMode + '">' + body + '</div>';
  }
  function unifiedRowsHtml(rows) {
    var left = 1, right = 1, html = '';
    rows.forEach(function (row) {
      var lno = '', rno = '';
      if (row[0] === '+') rno = right++;
      else if (row[0] === '-') lno = left++;
      else { lno = left++; rno = right++; }
      var cls = row[0] === '+' ? 'add' : row[0] === '-' ? 'del' : 'ctx';
      html += '<div class="git-diff-row ' + cls + '">'
        + '<span class="lno">' + lno + '</span><span class="lno">' + rno + '</span>'
        + '<span class="sign">' + (row[0] === ' ' ? '' : row[0]) + '</span>'
        + '<span class="text">' + esc(row[1]) + '</span></div>';
    });
    return html;
  }
  /* Unified 行序列 → Split 左右配对：−块与其后的 + 块按行配对，余者单侧留空。 */
  function splitPairs(diff) {
    var pairs = [], i = 0;
    while (i < diff.length) {
      if (diff[i][0] === '-') {
        var dels = [], adds = [];
        while (i < diff.length && diff[i][0] === '-') dels.push(diff[i++]);
        while (i < diff.length && diff[i][0] === '+') adds.push(diff[i++]);
        var n = Math.max(dels.length, adds.length);
        for (var k = 0; k < n; k++) pairs.push({ left: dels[k] || null, right: adds[k] || null });
      } else if (diff[i][0] === '+') {
        pairs.push({ left: null, right: diff[i++] });
      } else {
        var ctx = diff[i++];
        pairs.push({ left: ctx, right: ctx });
      }
    }
    return pairs;
  }
  function splitRowsHtml(rows) {
    var pairs = splitPairs(rows);
    var left = 1, right = 1, html = '';
    pairs.forEach(function (pair) {
      var lCls = pair.left ? (pair.left[0] === '-' ? 'del' : 'ctx') : 'empty';
      var rCls = pair.right ? (pair.right[0] === '+' ? 'add' : 'ctx') : 'empty';
      var lno = pair.left ? left++ : '';
      var rno = pair.right ? right++ : '';
      html += '<div class="git-split-row">'
        + '<div class="git-diff-row ' + lCls + '"><span class="lno">' + lno + '</span><span class="text">' + (pair.left ? esc(pair.left[1]) : '') + '</span></div>'
        + '<div class="git-diff-row ' + rCls + '"><span class="lno">' + rno + '</span><span class="text">' + (pair.right ? esc(pair.right[1]) : '') + '</span></div>'
        + '</div>';
    });
    return html;
  }
  /* 文件头快捷栏：状态点 + 路径 + 增删统计 + 齿轮（Diff Options）+ 文件状态标志（与列表一致） */
  function fileHead(file) {
    var stats = diffStats(effectiveDiff(file.diff));
    return '<div class="git-file-head">' + statusDot(file.status)
      + '<span class="git-file-path">' + esc(file.path) + '</span>'
      + '<span class="git-file-stats"><span class="stat-add">+' + stats.add + '</span><span class="stat-del">−' + stats.del + '</span></span>'
      + '<button class="icon-button git-diff-options-btn" data-git="diff-options" aria-expanded="' + String(diffOptionsOpen) + '" title="' + esc(t('差异显示选项')) + '" aria-label="' + esc(t('差异显示选项')) + '">' + icon('gear') + '<svg class="ico vex-expand-icon" aria-hidden="true"><use href="#i-chevron-down"/></svg></button>'
      + '<span class="git-head-status" title="' + esc(t('文件状态') + '：' + ({ 'M': '修改', 'A': '新增', 'D': '删除', '??': '未跟踪' }[file.status] || file.status)) + '">' + statusDot(file.status) + '</span>'
      + '</div>'
      + (diffOptionsOpen ? diffOptionsHtml() : '');
  }
  function diffOptionsHtml() {
    return '<div class="git-popover" role="dialog" aria-label="' + esc(t('差异显示选项')) + '">'
      + '<div class="git-pop-title">' + esc(t('Diff 显示')) + '</div>'
      + '<label class="git-pop-row"><input type="radio" name="git-diff-mode" value="unified"' + (diffMode === 'unified' ? ' checked' : '') + '> Unified</label>'
      + '<label class="git-pop-row"><input type="radio" name="git-diff-mode" value="split"' + (diffMode === 'split' ? ' checked' : '') + '> Split</label>'
      + '<div class="git-pop-title">' + esc(t('空白')) + '</div>'
      + '<label class="git-pop-row"><input type="checkbox" id="git-hide-ws"' + (hideWhitespace ? ' checked' : '') + '> ' + esc(t('隐藏空白变更')) + '</label>'
      + '<p class="git-pop-note">' + esc(t('隐藏空白变更后，逐行操作将不可用。')) + '</p>'
      + '</div>';
  }

  /* ---------- 渲染 ---------- */
  function render() {
    if (!state.repo) { renderEmpty(); return; }
    var html = '<div class="git-topbar">'
      + '<button class="git-repo" data-git="remote-dialog" title="' + esc(state.remote ? t('修改远程地址') : t('添加远程…')) + '">' + icon('git-branch') + '<span class="git-topname">' + esc(repoName()) + '</span>' + icon('chevron-down') + '</button>'
      + '<button class="git-branch" data-git="branch">' + icon('git-branch') + '<span class="git-topname">' + esc(state.branch) + '</span>' + icon('chevron-down') + '</button>'
      + '<span class="git-fetch"><button class="btn sm" data-git="pull"' + (state.remote ? '' : ' disabled') + '>' + icon('download') + ' ' + esc(t('拉取')) + '</button><small>' + esc(t('上次同步') + ' ' + (state.lastSync || '—')) + '</small></span>'
      + '<span class="git-top-spacer"></span>'
      + (state.remote
        ? '<span class="git-chip' + (isSsh(state.remote) ? ' warn' : '') + '" title="' + esc(isSsh(state.remote) ? t('SSH 远程：推送与拉取需要系统 Git 环境；改用 HTTPS 地址则无需安装。') : state.remote) + '">origin · ' + (isSsh(state.remote) ? 'SSH' : 'HTTPS') + '</span>'
        : '<button class="btn sm" data-git="remote-dialog">' + esc(t('添加远程…')) + '</button>')
      + (state.ahead || state.behind ? '<span class="git-chip">' + (state.ahead ? '↑' + state.ahead + ' ' : '') + (state.behind ? '↓' + state.behind : '') + '</span>' : '')
      + '</div>';
    html += '<div class="git-main"><div class="git-left">';
    var changeCount = state.changes.length;
    html += '<div class="git-tabs" role="tablist">'
      + '<button class="git-tab' + (tab === 'changes' ? ' on' : '') + '" data-git-tab="changes" role="tab" aria-selected="' + String(tab === 'changes') + '">' + esc(t('变更')) + (changeCount ? ' <span class="git-badge">' + changeCount + '</span>' : '') + '</button>'
      + '<button class="git-tab' + (tab === 'history' ? ' on' : '') + '" data-git-tab="history" role="tab" aria-selected="' + String(tab === 'history') + '">' + esc(t('历史')) + ' <span class="git-badge">' + state.history.length + '</span></button>'
      + '</div>';
    if (tab === 'changes') {
      html += '<div class="git-filter">' + icon('search') + '<input id="git-change-filter" placeholder="' + esc(t('筛选变更文件…')) + '" aria-label="' + esc(t('筛选变更文件')) + '"></div>';
      if (changeCount) {
        html += '<label class="git-row git-all-row"><input type="checkbox" id="git-stage-all"' + (state.changes.every(function (c) { return c.checked; }) ? ' checked' : '') + ' aria-label="' + esc(t('全选')) + '"><span>' + esc(t('{0} 个变更文件').replace('{0}', changeCount)) + '</span></label>';
        html += '<div class="git-list">';
        state.changes.forEach(function (change, index) {
          var slash = change.path.lastIndexOf('/');
          var dir = slash >= 0 ? change.path.slice(0, slash + 1) : '';
          var name = slash >= 0 ? change.path.slice(slash + 1) : change.path;
          html += '<div class="git-row change-item' + (selectedChange === change.path ? ' selected' : '') + '" data-git-file="' + index + '" role="option" aria-selected="' + String(selectedChange === change.path) + '" tabindex="0">'
            + '<input type="checkbox" data-git-index="' + index + '"' + (change.checked ? ' checked' : '') + ' aria-label="' + esc(change.path) + '">'
            + statusDot(change.status)
            + '<span class="git-path"><span class="dir">' + esc(dir) + '</span>' + esc(name) + '</span></div>';
        });
        html += '</div>';
      } else {
        html += '<div class="git-blank">' + icon('check') + '<p>' + esc(t('工作区干净，所有变更都已提交')) + '</p></div>';
      }
      html += '<div class="git-commit-box">'
        + '<input class="input git-summary" id="git-commit-summary" placeholder="' + esc(t('Summary（必填）')) + '" aria-label="' + esc(t('提交摘要')) + '">'
        + '<textarea class="git-description" id="git-commit-description" placeholder="' + esc(t('Description（可选，详细说明）')) + '" aria-label="' + esc(t('提交描述')) + '"></textarea>'
        + '<button class="btn primary git-commit-btn" data-git="commit"' + (checkedChanges().length ? '' : ' disabled') + '>' + esc(t('提交 {0} 个文件到 {1}').replace('{0}', checkedChanges().length).replace('{1}', state.branch)) + '</button>'
        + '<div class="git-push-row"><button class="btn sm" data-git="push"' + (state.remote && state.ahead ? '' : ' disabled') + '>' + icon('export') + ' ' + esc(t('推送')) + '</button>'
        + '<span class="git-hint">' + esc(state.remote ? (state.ahead ? t('{0} 个本地提交待推送').replace('{0}', state.ahead) : t('与远程同步')) : t('未配置远程，仅本地管理')) + '</span></div>'
        + '</div>';
    } else {
      html += '<div class="git-filter git-filter-dim">' + icon('git-branch') + '<input id="git-compare" placeholder="' + esc(t('选择分支以对比…')) + '" aria-label="' + esc(t('选择分支以对比')) + '" disabled title="' + esc(t('分支对比在桌面版中提供')) + '"></div>';
      html += '<div class="git-list">';
      if (state.history.length) {
        state.history.forEach(function (commit) {
          html += '<div class="git-row commit-item' + (selectedCommit === commit.hash ? ' selected' : '') + '" data-git-commit="' + esc(commit.hash) + '" role="option" aria-selected="' + String(selectedCommit === commit.hash) + '" tabindex="0">'
            + '<div class="commit-main"><div class="commit-subject">' + esc(commit.subject) + (commit.tag ? ' <span class="git-tag">' + esc(commit.tag) + '</span>' : '') + '</div>'
            + '<div class="commit-meta">' + icon('git-branch') + ' ' + esc(commit.author || '') + ' · ' + esc(commit.time) + '</div></div></div>';
        });
      } else {
        html += '<div class="git-blank"><p>' + esc(t('还没有提交')) + '</p></div>';
      }
      html += '</div>';
    }
    html += '</div>';
    /* 右区：Changes = 文件头快捷栏+diff；History = 详情头 + [文件列表 | diff] 两栏 */
    html += '<div class="git-right">';
    if (tab === 'changes') {
      var file = selectedChange != null ? state.changes.find(function (c) { return c.path === selectedChange; }) : null;
      if (file) {
        html += fileHead(file) + diffHtml(file);
      } else {
        html += '<div class="git-blank big">' + icon('columns') + '<p>' + esc(t('在左侧选择一个文件，查看与上次提交的差异')) + '</p></div>';
      }
    } else {
      var commit = selectedCommit ? state.history.find(function (c) { return c.hash === selectedCommit; }) : null;
      if (commit) {
        var totals = commit.files.reduce(function (acc, f) { var s = diffStats(effectiveDiff(f.diff)); acc.add += s.add; acc.del += s.del; return acc; }, { add: 0, del: 0 });
        html += '<div class="git-commit-head"><div class="commit-subject">' + esc(commit.subject) + (commit.tag ? ' <span class="git-tag">' + esc(commit.tag) + '</span>' : '') + '</div>'
          + '<div class="commit-meta">' + icon('git-branch') + ' ' + esc(commit.author || '') + ' · ' + esc(commit.time) + ' · <span class="hash">' + esc(commit.hash) + '</span>'
          + '<span class="git-file-stats"><span class="stat-add">+' + totals.add + '</span><span class="stat-del">−' + totals.del + '</span></span></div></div>';
        html += '<div class="git-right-cols"><div class="git-commit-files">';
        html += '<div class="git-file-head slim"><span>' + esc(t('{0} 个文件变更').replace('{0}', commit.files.length)) + '</span></div>';
        commit.files.forEach(function (file, index) {
          var selectedFile = selectedCommitFile === file.path;
          html += '<div class="git-row change-item' + (selectedFile ? ' selected' : '') + '" data-git-commit-file="' + index + '" role="option" aria-selected="' + String(selectedFile) + '" tabindex="0">'
            + statusDot(file.status) + '<span class="git-path">' + esc(file.path) + '</span></div>';
        });
        html += '</div><div class="git-diff-col">';
        var active = selectedCommitFile != null ? commit.files.find(function (f) { return f.path === selectedCommitFile; }) : null;
        if (active) html += fileHead(active) + diffHtml(active);
        else html += '<div class="git-blank big">' + icon('columns') + '<p>' + esc(t('选择一个文件查看差异')) + '</p></div>';
        html += '</div></div>';
      } else {
        html += '<div class="git-blank big">' + icon('sort-clock') + '<p>' + esc(t('在左侧选择一次提交，查看其文件变化')) + '</p></div>';
      }
    }
    html += '</div></div>';
    pane.innerHTML = html;
    syncAutoTimer();
    if (window.VexL) VexL.sync(pane);
  }

  function renderEmpty() {
    pane.innerHTML = '<div class="git-topbar"><span class="git-repo static">' + icon('git-branch') + '<span class="git-topname">' + esc(t('未初始化仓库')) + '</span></span></div>'
      + '<div class="git-empty-hero"><span class="git-empty-ico">' + icon('git-branch') + '</span>'
      + '<strong>' + esc(t('此文件夹还不是 Git 仓库')) + '</strong>'
      + '<p>' + esc(t('初始化后可对工作区做本地版本管理：提交、比较与回退；配置远程后还能推送与拉取。全程在本机完成，不依赖安装 Git。')) + '</p>'
      + '<div class="git-empty-actions"><button class="btn primary" data-git="init">' + esc(t('初始化本地仓库')) + '</button>'
      + '<button class="btn" data-git="remote-dialog">' + esc(t('添加远程…')) + '</button></div></div>';
    if (window.VexL) VexL.sync(pane);
  }

  /* ---------- 自动提交（偏好开关，默认关；界面不显示状态） ---------- */
  function autoOn() { return !!(window.VexPreferences && VexPreferences.values.gitAutoCommit) && state.repo; }
  var autoTimer = null;
  function syncAutoTimer() {
    if (autoTimer) { clearInterval(autoTimer); autoTimer = null; }
    if (!autoOn()) return;
    if (!state.nextAutoAt) state.nextAutoAt = Date.now() + (VexPreferences.values.gitAutoCommitInterval || 30) * 60000;
    autoTimer = setInterval(function () {
      if (!autoOn()) { clearInterval(autoTimer); autoTimer = null; return; }
      if (Date.now() >= state.nextAutoAt) {
        state.nextAutoAt = Date.now() + (VexPreferences.values.gitAutoCommitInterval || 30) * 60000;
        if (state.changes.length) {
          var template = (window.VexPreferences && VexPreferences.values.gitAutoCommitMessage) || 'auto: {date}';
          commitAll(state.changes.slice(), template.replace('{date}', now()), true);
        }
      }
    }, 1000);
  }

  /* ---------- 模拟动作 ---------- */
  function commitAll(files, message, isAuto) {
    var committed = files || checkedChanges();
    if (!committed.length) return;
    var subject = (message || '').trim() || t('更新工作区文档');
    state.history.unshift({
      hash: randomHash(), subject: subject, author: 'Dotnet9(dotnet9.com)', time: now(),
      files: committed.map(function (c) { return { path: c.path, status: c.status, diff: c.diff }; })
    });
    var committedPaths = {};
    committed.forEach(function (c) { committedPaths[c.path] = true; });
    state.changes = state.changes.filter(function (c) { return !committedPaths[c.path]; });
    if (state.remote) state.ahead += 1;
    selectedChange = null; tab = 'changes';
    persist(); render();
    VexProto.toast((isAuto ? t('自动提交完成') : t('已提交')) + ' · ' + subject);
  }
  function randomHash() { return Math.random().toString(16).slice(2, 9); }
  function push() {
    if (!state.remote) return;
    if (!state.ahead) { VexProto.toast(t('没有待推送的提交')); return; }
    var button = pane.querySelector('[data-git="push"]'); if (button) { button.disabled = true; }
    setTimeout(function () {
      state.ahead = 0; state.behind = 0; state.lastSync = now(); persist(); render();
      VexProto.toast(t('已推送到 origin/') + state.branch);
    }, 700);
  }
  function pull() {
    if (!state.remote) return;
    if (!state.pulledOnce) {
      state.pulledOnce = true; state.behind = 1; persist(); render();
      VexProto.toast(t('远程有 1 个新提交，再次拉取即可合入'));
      return;
    }
    if (!state.behind) { state.lastSync = now(); persist(); VexProto.toast(t('已是最新，无需拉取')); render(); return; }
    state.history.push({ hash: randomHash(), subject: 'docs: 更新部署说明（来自远程）', author: 'Dotnet9(dotnet9.com)', time: t('刚刚'), files: [{ path: '笔记/部署说明.md', status: 'M', diff: [[' ', '## 部署'], ['-', '部署端口 8080。'], ['+', '部署端口 443（HTTPS）。']] }] });
    state.behind = 0; state.lastSync = now(); persist(); render();
    VexProto.toast(t('已合入远程的 1 个提交'));
  }

  /* ---------- 事件（pane 元素只绑一次） ---------- */
  pane.addEventListener('click', function (event) {
    var target = event.target.closest('[data-git]');
    if (target) {
      switch (target.dataset.git) {
        case 'init': initRepo(false); break;
        case 'commit': commitAll(null, (document.getElementById('git-commit-summary') || {}).value, false); break;
        case 'push': push(); break;
        case 'pull': pull(); break;
        case 'remote-dialog': openRemoteDialog(); break;
        case 'branch': VexProto.toast(t('分支管理在桌面版中提供')); break;
        case 'diff-options': diffOptionsOpen = !diffOptionsOpen; render(); return;
      }
      return;
    }
    if (event.target.closest('.git-popover')) return; /* 弹层内点击不重渲染 */
    var tabBtn = event.target.closest('[data-git-tab]');
    if (tabBtn) { tab = tabBtn.dataset.gitTab; diffOptionsOpen = false; render(); return; }
    var item = event.target.closest('[data-git-file]');
    if (item) {
      if (event.target.closest('input[type=checkbox]')) return;
      var index = Number(item.dataset.gitFile);
      selectedChange = state.changes[index] ? state.changes[index].path : null;
      render(); return;
    }
    var commitItem = event.target.closest('[data-git-commit]');
    if (commitItem) {
      selectedCommit = commitItem.dataset.gitCommit;
      selectedCommitFile = null;
      var commit = state.history.find(function (c) { return c.hash === selectedCommit; });
      if (commit && commit.files.length) selectedCommitFile = commit.files[0].path;
      render(); return;
    }
    var commitFile = event.target.closest('[data-git-commit-file]');
    if (commitFile) {
      var commit2 = state.history.find(function (c) { return c.hash === selectedCommit; });
      if (commit2 && commit2.files[Number(commitFile.dataset.gitCommitFile)]) selectedCommitFile = commit2.files[Number(commitFile.dataset.gitCommitFile)].path;
      render(); return;
    }
    if (diffOptionsOpen) { diffOptionsOpen = false; render(); }
  });
  /* 点击面板外关闭 Diff Options 弹层 */
  document.addEventListener('pointerdown', function (event) {
    if (!diffOptionsOpen || pane.hidden) return;
    if (event.target.closest('.git-popover') || event.target.closest('[data-git="diff-options"]')) return;
    diffOptionsOpen = false;
    var pop = pane.querySelector('.git-popover');
    if (pop) {
      pop.remove();
      var gear = pane.querySelector('[data-git="diff-options"]');
      if (gear) gear.setAttribute('aria-expanded', 'false');
    }
  });
  pane.addEventListener('change', function (event) {
    if (event.target.name === 'git-diff-mode') { diffMode = event.target.value; render(); return; }
    if (event.target.id === 'git-hide-ws') { hideWhitespace = event.target.checked; render(); return; }
    if (event.target.id === 'git-stage-all') {
      var all = event.target.checked;
      state.changes.forEach(function (c) { c.checked = all; });
      persist(); render(); return;
    }
    var index = event.target.dataset && event.target.dataset.gitIndex;
    if (index === undefined) return;
    state.changes[Number(index)].checked = event.target.checked;
    persist();
    var button = pane.querySelector('[data-git="commit"]');
    if (button) {
      button.disabled = !checkedChanges().length || !((document.getElementById('git-commit-summary') || {}).value || '').trim();
      button.textContent = t('提交 {0} 个文件到 {1}').replace('{0}', checkedChanges().length).replace('{1}', state.branch);
    }
  });
  pane.addEventListener('input', function (event) {
    if (event.target.id === 'git-change-filter') {
      var keyword = event.target.value.trim().toLowerCase();
      pane.querySelectorAll('.change-item[data-git-file]').forEach(function (row) {
        var change = state.changes[Number(row.dataset.gitFile)];
        row.hidden = !!keyword && change.path.toLowerCase().indexOf(keyword) < 0;
      });
      return;
    }
    if (event.target.id === 'git-commit-summary') {
      var button = pane.querySelector('[data-git="commit"]');
      if (button) button.disabled = !checkedChanges().length || !event.target.value.trim();
    }
  });

  /* ---------- 添加远程弹层 ---------- */
  function openRemoteDialog() {
    var input = document.getElementById('git-remote-url');
    input.value = state.remote || '';
    document.getElementById('git-remote-error').classList.add('is-hidden');
    VexProto.handleAction('dlg-git-remote');
  }
  function saveRemote() {
    var input = document.getElementById('git-remote-url');
    var url = input.value.trim();
    var valid = /^https?:\/\/\S+$/i.test(url) || /^git@\S+:\S+/.test(url) || /^ssh:\/\/\S+$/i.test(url);
    if (!valid) { document.getElementById('git-remote-error').classList.remove('is-hidden'); input.focus(); return; }
    state.remote = url; state.ahead = 0; state.behind = 0; state.pulledOnce = false; state.lastSync = null;
    persist();
    var layer = document.getElementById('layer-git-remote');
    if (layer) layer.hidden = true;
    render();
    VexProto.toast(t('远程已保存') + '：' + (isSsh(url) ? 'SSH' : 'HTTPS'));
  }
  document.getElementById('git-remote-ok').addEventListener('click', saveRemote);
  document.getElementById('git-remote-url').addEventListener('input', function () { document.getElementById('git-remote-error').classList.add('is-hidden'); });

  /* ---------- 文件 / Git 模式切换 ---------- */
  function setMode(mode) {
    var git = mode === 'git';
    document.body.classList.toggle('git-mode', git);
    var filesBtn = document.getElementById('mode-files'), gitBtn = document.getElementById('mode-git');
    filesBtn.classList.toggle('on', !git); filesBtn.setAttribute('aria-pressed', String(!git));
    gitBtn.classList.toggle('on', git); gitBtn.setAttribute('aria-pressed', String(git));
    pane.hidden = !git;
    if (git) render();
  }

  /* ---------- 动作接入 ---------- */
  var previousHandle = window.VexProto && VexProto.handleAction;
  VexProto.handleAction = function (action, item) {
    if (action === 'mode-files') { setMode('files'); return true; }
    if (action === 'mode-git') { setMode('git'); return true; }
    /* Git 模式下正文操作先回到文件模式（快速打开、查找都发生在编辑工作区）。 */
    if ((action === 'pane-files' || action === 'find') && document.body.classList.contains('git-mode')) setMode('files');
    var result = previousHandle.call(this, action, item);
    /* 标题栏折叠按钮：跟随 workbench 状态同步图标（图标箭头 = 点击后的动作方向）与按压态。 */
    if (action === 'toggle-sidebar') {
      var sidebar = document.querySelector('.sidebar');
      var open = sidebar && !sidebar.classList.contains('closed');
      var btn = document.getElementById('sidebar-toggle');
      if (btn) {
        btn.setAttribute('aria-pressed', String(open));
        var use = btn.querySelector('use');
        if (use) use.setAttribute('href', open ? '#i-rail-collapse' : '#i-rail-expand');
      }
    }
    if (action === 'toggle-structure') {
      var structure = document.getElementById('structure-sidebar');
      var expanded = structure && !structure.classList.contains('collapsed');
      var outlineBtn = document.getElementById('outline-toggle');
      if (outlineBtn) {
        outlineBtn.setAttribute('aria-pressed', String(expanded));
        var outlineUse = outlineBtn.querySelector('use');
        if (outlineUse) outlineUse.setAttribute('href', expanded ? '#i-rail-expand' : '#i-rail-collapse');
      }
    }
    return result;
  };

  /* ---------- 大纲折叠按钮移位：外观/语言面板（studio-title-controls）右侧 ---------- */
  function moveOutlineButton() {
    var captionBtns = document.getElementById('caption-btns');
    var outlineBtn = document.getElementById('outline-toggle');
    if (captionBtns && outlineBtn) captionBtns.before(outlineBtn);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', moveOutlineButton);
  else moveOutlineButton();
  /* 启动时同步两个折叠按钮图标（侧栏/大纲默认展开 → 收起态图标） */
  (function () {
    var sidebar = document.querySelector('.sidebar');
    var open = sidebar && !sidebar.classList.contains('closed');
    var btn = document.getElementById('sidebar-toggle');
    if (btn) {
      btn.setAttribute('aria-pressed', String(!!open));
      var use = btn.querySelector('use');
      if (use) use.setAttribute('href', open ? '#i-rail-collapse' : '#i-rail-expand');
    }
    var structure = document.getElementById('structure-sidebar');
    var expanded = structure && !structure.classList.contains('collapsed');
    var outlineBtn = document.getElementById('outline-toggle');
    if (outlineBtn) {
      outlineBtn.setAttribute('aria-pressed', String(!!expanded));
      var outlineUse = outlineBtn.querySelector('use');
      if (outlineUse) outlineUse.setAttribute('href', expanded ? '#i-rail-expand' : '#i-rail-collapse');
    }
  })();

  /* ---------- 偏好联动（自动提交默认关） ---------- */
  window.addEventListener('vex-preference-changed', function (event) {
    if (event.detail && /^gitAutoCommit/.test(event.detail.key)) { state.nextAutoAt = 0; persist(); syncAutoTimer(); }
  });
  window.addEventListener('vex-language-changed', function () { if (!pane.hidden) render(); });
  window.VexGitPanel = { setMode: setMode, showPane: function () { setMode('git'); }, get state() { return state; } };
})();
