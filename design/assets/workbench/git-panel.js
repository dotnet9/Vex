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
  var DEFAULT_BRANCH = 'main';   // 原型固定默认分支（桌面版取 origin/HEAD）
  var defaults = {
    repo: false, repoKey: 'vex-notes', openRepos: ['vex-notes', 'CodeWF'], repoData: {},
    branch: 'main', branches: seedBranches('main', '昨天 20:37'), recentBranches: [], remote: null, lastSync: null,
    changes: [], history: [], ahead: 0, behind: 0, pulledOnce: false, nextAutoAt: 0
  };
  var state = JSON.parse(JSON.stringify(defaults));
  try {
    var saved = JSON.parse(VexStorage.getItem(STORE_KEY) || 'null');
    if (saved && typeof saved === 'object' && typeof saved.repo === 'boolean') state = Object.assign(state, saved);
    state.branches = normalizeBranches(state.branches);
    if (!Array.isArray(state.recentBranches)) state.recentBranches = [];
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
  function seedBranches(defaultBranchName, defaultUpdated) {
    // 演示数据：默认分支 + 若干本地分支，用于呈现 GHD 的三组列表（默认 / 最近 / 其他）。
    return [
      { name: defaultBranchName || DEFAULT_BRANCH, updated: defaultUpdated || '昨天 20:37', ahead: 0, behind: 0 },
      { name: 'draft/流式渲染', updated: '昨天 21:04', ahead: 0, behind: 0 },
      { name: 'archive/旧稿', updated: '3 天前', ahead: 0, behind: 0 },
      { name: 'release/2.1', updated: '上周', ahead: 0, behind: 0 }
    ];
  }
  function normalizeBranches(list) {
    // 兼容旧存档（字符串数组 / 缺字段）；保证默认分支与当前分支都在列表里。
    var out = [];
    (Array.isArray(list) ? list : []).forEach(function (item) {
      var name = typeof item === 'string' ? item : item && item.name;
      if (name && !out.some(function (b) { return b.name === name; })) {
        out.push(typeof item === 'string' ? { name: name, updated: '昨天', ahead: 0, behind: 0 } : item);
      }
    });
    if (!out.some(function (b) { return b.name === DEFAULT_BRANCH; })) out.unshift({ name: DEFAULT_BRANCH, updated: '昨天', ahead: 0, behind: 0 });
    if (!out.some(function (b) { return b.name === state.branch; })) out.push({ name: state.branch, updated: '昨天', ahead: 0, behind: 0 });
    return out;
  }
  function initRepo(silent) {
    state.repo = true;
    if (state.openRepos.indexOf(state.repoKey) < 0) state.openRepos.push(state.repoKey);
    state.branches = normalizeBranches(state.branches);
    state.changes = seedChanges(); state.history = seedHistory();
    persist(); render();
    if (!silent) VexProto.toast(t('已在当前文件夹初始化本地仓库'));
  }

  /* ---------- 工具 ---------- */
  function now() { var d = new Date(); return ('0' + d.getHours()).slice(-2) + ':' + ('0' + d.getMinutes()).slice(-2); }
  function checkedChanges() { return state.changes.filter(function (c) { return c.checked; }); }
  function isSsh(url) { return /^git@|^ssh:\/\//i.test(url || ''); }
  function repoName() { return state.repoKey; }
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
    renderPanelBranch();
    if (!state.repo) { renderEmpty(); return; }
    var html = '<div class="git-topbar">'
      + '<button class="git-repo" data-git="repo" aria-haspopup="dialog" title="' + esc(t('已打开的仓库')) + '">' + icon('git-branch') + '<span class="git-topname">' + esc(repoName()) + '</span>' + icon('chevron-down') + '</button>'
      + '<button class="git-branch" data-git="branch" aria-haspopup="dialog" aria-expanded="' + String(!branchPop.hidden) + '" title="' + esc(state.branch) + '">' + icon('git-branch') + '<span class="git-branch-lines"><span class="git-chip-name">' + esc(state.branch) + '</span><small>' + esc(t('当前分支')) + '</small></span>' + icon('chevron-down') + '</button>'
      + syncButtonHtml(state)
      + '<span class="git-top-spacer"></span>'
      + (state.remote
        ? '<span class="git-chip' + (isSsh(state.remote) ? ' warn' : '') + '" title="' + esc(isSsh(state.remote) ? t('SSH 远程：推送与拉取需要系统 Git 环境；改用 HTTPS 地址则无需安装。') : state.remote) + '">origin · ' + (isSsh(state.remote) ? 'SSH' : 'HTTPS') + '</span>'
        : '')
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
    var committedBranch = currentBranch();
    if (committedBranch) committedBranch.updated = now();
    selectedChange = null; tab = 'changes';
    persist(); render();
    VexProto.toast((isAuto ? t('自动提交完成') : t('已提交')) + ' · ' + subject);
  }
  function randomHash() { return Math.random().toString(16).slice(2, 9); }

  /* ---------- 同步按钮（GitHub Desktop 式）：随状态换动作，永不禁用；副文案=上次拉取时间 ---------- */
  function syncButtonHtml(state) {
    if (!state.remote) {
      // 无远程：发布仓库（打开远程地址配置），语义对齐 GHD 的 Publish repository。
      return '<button class="git-sync primary" data-git="remote-dialog">' + icon('export')
        + '<span class="git-sync-main">' + esc(t('发布仓库…')) + '</span>'
        + '<small class="git-sync-sub">' + esc(t('未配置远程，仅本地管理')) + '</small></button>';
    }
    var badge = (state.ahead ? '<span class="git-sync-badge up">↑' + state.ahead + '</span>' : '')
      + (state.behind ? '<span class="git-sync-badge down">↓' + state.behind + '</span>' : '');
    var act = state.ahead ? 'push' : (state.behind ? 'pull' : 'fetch');
    var label = state.ahead ? t('推送 origin') : (state.behind ? t('拉取 origin') : t('获取 origin'));
    var sub = state.lastSync ? t('上次拉取') + ' ' + state.lastSync : t('从未拉取');
    return '<button class="git-sync primary" data-git="' + act + '">' + (state.ahead ? icon('export') : icon('download'))
      + '<span class="git-sync-main">' + esc(label) + '</span>' + badge
      + '<small class="git-sync-sub">' + esc(sub) + '</small></button>';
  }
  function fetchOrigin() {
    if (!state.remote) return;
    state.lastSync = now(); persist(); render();
    VexProto.toast(t('已获取远端状态'));
  }
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
        case 'fetch': fetchOrigin(); break;
        case 'remote-dialog': openRemoteDialog(); break;
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

  /* ---------- 标题栏「Git」菜单（仅 Git 模式显示；条目对照 GHD Repository/Branch 菜单） ---------- */
  var gitAppMenu = document.createElement('div');
  gitAppMenu.className = 'file-context-menu tb-branch-menu'; gitAppMenu.hidden = true; gitAppMenu.setAttribute('role', 'menu');
  document.body.appendChild(gitAppMenu);
  function renderGitAppMenu() {
    var items;
    if (!state.repo) {
      items = '<button type="button" role="menuitem" data-git-menu="init">' + esc(t('初始化本地仓库')) + '</button>'
        + '<button type="button" role="menuitem" data-git-menu="remote-dialog">' + esc(t('添加远程…')) + '</button>';
    } else {
      items = '<button type="button" role="menuitem" data-git-menu="pull">' + esc(t('拉取')) + '</button>'
        + '<button type="button" role="menuitem" data-git-menu="push">' + esc(t('推送')) + '</button>'
        + '<button type="button" role="menuitem" data-git-menu="fetch">' + esc(t('获取')) + '</button>'
        + '<button type="button" role="menuitem" data-git-menu="sep" class="menu-sep" tabindex="-1"></button>'
        + '<button type="button" role="menuitem" data-git-menu="new-branch">' + esc(t('新建分支…')) + '</button>'
        + '<button type="button" role="menuitem" data-git-menu="merge">' + esc(t('合并分支…')) + '</button>'
        + '<button type="button" role="menuitem" data-git-menu="create-tag">' + esc(t('打 tag…')) + '</button>'
        + '<button type="button" role="menuitem" data-git-menu="push-tag">' + esc(t('推送 tag')) + '</button>'
        + '<button type="button" role="menuitem" data-git-menu="sep" class="menu-sep" tabindex="-1"></button>'
        + '<button type="button" role="menuitem" data-git-menu="remote-dialog">' + esc(t('远程设置…')) + '</button>';
    }
    gitAppMenu.innerHTML = items;
  }
  function closeGitAppMenu() { gitAppMenu.hidden = true; }
  function ensureGitMenuButton() {
    // MENUS 渲染出的 Git 按钮（label='Git'）挂上 id；没有（旧缓存）才补建。
    var nav = document.querySelector('.tb-menu');
    if (!nav) return;
    var btn = document.getElementById('tb-git-menu');
    if (btn && btn.isConnected) return;
    btn = Array.prototype.find.call(nav.querySelectorAll('.tb-menuitem'), function (b) { return b.textContent === 'Git'; });
    if (!btn) {
      btn = document.createElement('button');
      btn.type = 'button'; btn.className = 'tb-menuitem';
      btn.textContent = 'Git';
      var help = Array.prototype.find.call(nav.querySelectorAll('.tb-menuitem'), function (b) { return /^帮助/.test(b.textContent); });
      nav.insertBefore(btn, help && help.isConnected ? help : null);
    }
    btn.id = 'tb-git-menu';
    btn.setAttribute('aria-haspopup', 'true');
  }
  document.addEventListener('click', function (event) {
    var btn = event.target.closest('#tb-git-menu');
    if (!btn) return;
    event.stopPropagation();
    if (!gitAppMenu.hidden) { closeGitAppMenu(); return; }
    renderGitAppMenu();
    gitAppMenu.hidden = false;
    var rect = btn.getBoundingClientRect();
    gitAppMenu.style.left = rect.left + 'px';
    gitAppMenu.style.top = (rect.bottom + 2) + 'px';
    gitAppMenu.querySelector('button:not(.menu-sep)').focus({ preventScroll: true });
  });
  gitAppMenu.addEventListener('click', function (event) {
    var item = event.target.closest('[data-git-menu]');
    if (!item || item.classList.contains('menu-sep')) return;
    closeGitAppMenu();
    var act = item.dataset.gitMenu;
    if (act === 'init') initRepo(false);
    else if (act === 'remote-dialog') openRemoteDialog();
    else if (act === 'pull') pull();
    else if (act === 'push') push();
    else if (act === 'fetch') fetchOrigin();
    else if (act === 'new-branch') openBranchDialog('create', '');
    else if (act === 'merge') openMergeDialog();
    else if (act === 'create-tag') openTagDialog(false);
    else if (act === 'push-tag') openTagDialog(true);
  });
  document.addEventListener('pointerdown', function (event) {
    if (!gitAppMenu.hidden && !gitAppMenu.contains(event.target) && !event.target.closest('#tb-git-menu')) closeGitAppMenu();
  });
  document.addEventListener('keydown', function (event) {
    if (event.key === 'Escape' && !gitAppMenu.hidden) { event.preventDefault(); closeGitAppMenu(); }
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

  /* ---------- 分支下拉（标题栏常驻；工作区是仓库时，文件/Git 两种模式都显示）----------
   * 逐项对照 desktop/desktop（development 分支源码）：
   *   toolbar/branch-dropdown.tsx               按钮本体：分支图标 + 分支名，tooltip=分支名
   *   branches/branch-list.tsx                   筛选框 →「New branch」按钮 → 分组列表
   *   branches/group-branches.ts                 分组：默认分支 → 最近分支（去默认）→ 其他分支
   *   branches/branch-list-item.tsx              行：✓（当前分支）或分支图标 + 名称 + 最后提交时间
   *   branches/no-branches.tsx                   空态：找不到分支，给「Create new branch」
   *   branches/branches-container.tsx            底部：选择要合并到 当前分支 的分支
   *   branches/branch-list-item-context-menu.tsx 行右键：重命名… / 复制分支名 / 删除…
   *   create-branch-dialog.tsx / merge-choose-branch-dialog.tsx / delete-branch-dialog.tsx 弹层
   * Vex 保留项（GHD 无对应，发布流程依赖）：「打 tag… / 推送 tag」一行。 */
  var OCTICON = {
    check: 'M13.78 4.22a.75.75 0 0 1 0 1.06l-7.25 7.25a.75.75 0 0 1-1.06 0L2.22 9.28a.751.751 0 0 1 .018-1.042.751.751 0 0 1 1.042-.018L6 10.94l6.72-6.72a.75.75 0 0 1 1.06 0Z',
    branch: 'M9.5 3.25a2.25 2.25 0 1 1 3 2.122V6A2.5 2.5 0 0 1 10 8.5H6a1 1 0 0 0-1 1v1.128a2.251 2.251 0 1 1-1.5 0V5.372a2.25 2.25 0 1 1 1.5 0v1.836A2.493 2.493 0 0 1 6 7h4a1 1 0 0 0 1-1v-.628A2.25 2.25 0 0 1 9.5 3.25Zm-6 0a.75.75 0 1 0 1.5 0 .75.75 0 0 0-1.5 0Zm8.25-.75a.75.75 0 1 0 0 1.5.75.75 0 0 0 0-1.5ZM4.25 12a.75.75 0 1 0 0 1.5.75.75 0 0 0 0-1.5Z',
    merge: 'M5.45 5.154A4.25 4.25 0 0 0 9.25 7.5h1.378a2.251 2.251 0 1 1 0 1.5H9.25A5.734 5.734 0 0 1 5 7.123v3.505a2.25 2.25 0 1 1-1.5 0V5.372a2.25 2.25 0 1 1 1.95-.218ZM4.25 13.5a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5Zm8.5-4.5a.75.75 0 1 0 0-1.5.75.75 0 0 0 0 1.5ZM5 3.25a.75.75 0 1 0 0 .005V3.25Z'
  };
  function octIcon(name) { return '<svg class="ico" viewBox="0 0 16 16" aria-hidden="true"><path fill="currentColor" d="' + OCTICON[name] + '"/></svg>'; }
  function fmt(text, value) { return String(text).split('{0}').join(value); }

  var repoPop = document.createElement('div');
  repoPop.className = 'tb-repo-pop'; repoPop.hidden = true; repoPop.setAttribute('role', 'listbox');
  document.body.appendChild(repoPop);
  function repoSubset() {
    return {
      branch: state.branch, branches: state.branches, recentBranches: state.recentBranches,
      remote: state.remote, lastSync: state.lastSync, ahead: state.ahead, behind: state.behind,
      pulledOnce: state.pulledOnce, changes: state.changes, history: state.history
    };
  }
  function seedRepoState(key) {
    if (key === 'CodeWF') {
      return { branch: 'develop', branches: null, recentBranches: [], remote: 'https://github.com/dotnet9/CodeWF.git', lastSync: null, ahead: 0, behind: 0, pulledOnce: false };
    }
    return { branch: 'main', branches: null, recentBranches: [], remote: null, lastSync: null, ahead: 0, behind: 0, pulledOnce: false };
  }
  function switchRepo(key) {
    if (key === state.repoKey) return;
    state.repoData[state.repoKey] = repoSubset();
    var saved = state.repoData[key];
    state.repoKey = key;
    if (saved) {
      Object.assign(state, saved);
      delete state.repoData[key];
      state.branches = normalizeBranches(state.branches);
      if (!Array.isArray(state.recentBranches)) state.recentBranches = [];
    } else {
      var seed = seedRepoState(key);
      Object.assign(state, seed);
      state.branches = normalizeBranches(null);
      state.changes = seedChanges();
      state.history = seedHistory();
    }
    state.nextAutoAt = 0;
    tab = 'changes'; selectedChange = null; selectedCommit = null; selectedCommitFile = null;
    persist(); render();
    VexProto.toast(t('已切换到仓库') + ' ' + key);
  }
  function renderRepoPop() {
    var html = state.openRepos.map(function (key) {
      var isCurrent = key === state.repoKey;
      return '<div class="tb-branch-row' + (isCurrent ? ' current' : '') + '" role="option" aria-selected="' + String(isCurrent) + '" tabindex="-1" data-repo-row="' + esc(key) + '">'
        + '<span class="tb-branch-check">' + octIcon(isCurrent ? 'check' : 'branch') + '</span>'
        + '<span class="tb-branch-row-name">' + esc(key) + '</span></div>';
    }).join('');
    repoPop.innerHTML = '<div class="tb-branch-group">' + esc(t('已打开的仓库')) + '</div>' + html
      + '<div class="tb-branch-extra"><button type="button" data-repo-act="remote">' + esc(state.remote ? t('远程设置…') : t('添加远程…')) + '</button></div>';
  }
  function openRepoPop(anchor) {
    renderRepoPop();
    repoPop.hidden = false;
    var rect = anchor.getBoundingClientRect();
    var width = repoPop.offsetWidth || 300;
    repoPop.style.left = Math.max(8, Math.min(rect.left, innerWidth - width - 8)) + 'px';
    repoPop.style.top = (rect.bottom + 4) + 'px';
  }
  function closeRepoPop() { repoPop.hidden = true; }
  repoPop.addEventListener('click', function (event) {
    var row = event.target.closest('[data-repo-row]');
    if (row) { closeRepoPop(); switchRepo(row.dataset.repoRow); return; }
    var act = event.target.closest('[data-repo-act]');
    if (act && act.dataset.repoAct === 'remote') { closeRepoPop(); openRemoteDialog(); }
  });
  var branchPop = document.createElement('div');
  branchPop.className = 'tb-branch-pop'; branchPop.hidden = true; branchPop.setAttribute('role', 'dialog');
  document.body.appendChild(branchPop);
  var branchMenu = document.createElement('div');   // 行右键菜单（与文件右键菜单同款样式）
  branchMenu.className = 'file-context-menu tb-branch-menu'; branchMenu.hidden = true; branchMenu.setAttribute('role', 'menu');
  document.body.appendChild(branchMenu);
  var branchFilterText = '', branchKbIndex = 0;
  var branchDialogMode = 'create', renameTarget = null, deleteTarget = null;

  function findBranch(name) {
    for (var i = 0; i < state.branches.length; i++) if (state.branches[i].name === name) return state.branches[i];
    return null;
  }
  function currentBranch() { return findBranch(state.branch); }
  function closeBranchMenu() { branchMenu.hidden = true; }
  function groupLabel(group) {
    return group === 'default' ? t('默认分支') : group === 'recent' ? t('最近分支') : t('其他分支');
  }
  function groupedBranches() {
    // 照 GHD group-branches：默认分支组只有默认分支；最近分支组去掉默认；其余全部归「其他」。
    var recentNames = {}, groups = [];
    (state.recentBranches || []).forEach(function (name) { recentNames[name] = true; });
    var def = findBranch(DEFAULT_BRANCH);
    if (def) groups.push({ group: 'default', branch: def });
    (state.recentBranches || []).forEach(function (name) {
      var branch = findBranch(name);
      if (branch && name !== DEFAULT_BRANCH) groups.push({ group: 'recent', branch: branch });
    });
    state.branches.forEach(function (branch) {
      if (branch.name !== DEFAULT_BRANCH && !recentNames[branch.name]) groups.push({ group: 'other', branch: branch });
    });
    return groups;
  }
  function visibleBranchRows() {
    var keyword = branchFilterText.trim().toLowerCase();
    return groupedBranches().filter(function (item) { return !keyword || item.branch.name.toLowerCase().indexOf(keyword) >= 0; });
  }
  function renderPanelBranch() {
    var top = pane.querySelector('.git-topbar');
    if (!top) return;
    var open = !branchPop.hidden;
    var btn = top.querySelector('[data-git="branch"]');
    if (!btn) return;
    btn.classList.toggle('active', open);
    btn.setAttribute('aria-expanded', String(open));
    btn.querySelector('.git-chip-name').textContent = state.branch;
  }
  function renderBranchList() {
    var list = branchPop.querySelector('.tb-branch-list');
    if (!list) return;
    var rows = visibleBranchRows();
    if (branchKbIndex >= rows.length) branchKbIndex = Math.max(0, rows.length - 1);
    var html = '', lastGroup = null;
    rows.forEach(function (item, index) {
      if (item.group !== lastGroup) { lastGroup = item.group; html += '<div class="tb-branch-group">' + esc(groupLabel(item.group)) + '</div>'; }
      var branch = item.branch, isCurrent = branch.name === state.branch;
      html += '<div class="tb-branch-row' + (isCurrent ? ' current' : '') + (index === branchKbIndex ? ' kb' : '') + '" role="option" aria-selected="' + String(isCurrent) + '" tabindex="-1" data-branch-row="' + esc(branch.name) + '">'
        + '<span class="tb-branch-check">' + octIcon(isCurrent ? 'check' : 'branch') + '</span>'
        + '<span class="tb-branch-row-name">' + esc(branch.name) + '</span>'
        + '<span class="tb-branch-row-date">' + esc(branch.updated || '') + '</span></div>';
    });
    if (!html) {
      html = '<div class="tb-branch-empty"><div class="tb-branch-empty-title">' + esc(t('抱歉，找不到这个分支')) + '</div>'
        + '<div class="tb-branch-empty-sub">' + esc(t('要新建一个分支吗？')) + '</div>'
        + '<button type="button" class="btn sm" data-branch-act="new">' + esc(t('新建分支')) + '</button></div>';
    }
    list.innerHTML = html;
  }
  function renderBranchPop() {
    branchPop.innerHTML = '<div class="tb-branch-filter">' + icon('search')
      + '<input id="tb-branch-filter" type="text" autocomplete="off" spellcheck="false" placeholder="' + esc(t('筛选')) + '" aria-label="' + esc(t('筛选')) + '"></div>'
      + '<button type="button" class="tb-branch-new" data-branch-act="new">' + octIcon('branch') + '<span>' + esc(t('新建分支')) + '</span></button>'
      + '<div class="tb-branch-list" role="listbox" aria-label="' + esc(t('分支')) + '"></div>'
      + '<button type="button" class="tb-branch-merge" data-branch-act="merge" title="' + esc(fmt(t('选择要合并到 {0} 的分支'), state.branch)) + '">'
      + octIcon('merge') + '<span>' + esc(t('选择要合并到')) + ' <b>' + esc(state.branch) + '</b> ' + esc(t('的分支')) + '</span></button>';
    document.getElementById('tb-branch-filter').value = branchFilterText;
    renderBranchList();
  }
  function positionBranchPop(anchor) {
    var rect = anchor.getBoundingClientRect();
    var width = branchPop.offsetWidth || 320;
    branchPop.style.left = Math.max(8, Math.min(rect.left, innerWidth - width - 8)) + 'px';
    branchPop.style.top = (rect.bottom + 4) + 'px';
  }
  function openBranchPop(anchor) {
    branchFilterText = ''; branchKbIndex = 0;
    renderBranchPop();
    branchPop.hidden = false;
    branchPopSize = innerWidth + 'x' + innerHeight;
    positionBranchPop(anchor);
    renderPanelBranch();
    var input = document.getElementById('tb-branch-filter');
    if (input) input.focus({ preventScroll: true });
  }
  function closeBranchPop() {
    if (branchPop.hidden) return;
    branchPop.hidden = true;
    closeBranchMenu();
    renderPanelBranch();
  }

  /* ---------- 分支操作（原型模拟；语义照 GHD：创建后切换过去、检出目标进最近分支） ---------- */
  function validBranchName(name) {
    return /^[A-Za-z0-9_][A-Za-z0-9_\/.-]*$/.test(name) && name.indexOf('..') < 0 && name.indexOf('//') < 0
      && name.indexOf('@{') < 0 && !/[\/.]$/.test(name) && !/\.lock$/.test(name);
  }
  function switchBranch(name, silent) {
    if (name === state.branch || !findBranch(name)) return;
    var previous = currentBranch();
    if (previous) { previous.ahead = state.ahead; previous.behind = state.behind; }
    state.branch = name;
    state.recentBranches = [name].concat((state.recentBranches || []).filter(function (n) { return n !== name; })).slice(0, 5);
    var next = findBranch(name);
    state.ahead = next.ahead || 0;
    state.behind = next.behind || 0;
    persist(); render();
    if (!silent) VexProto.toast(t('已切换到分支') + ' ' + name);
  }
  function createBranch(name) {
    state.branches.push({ name: name, updated: now(), ahead: 0, behind: 0 });
    switchBranch(name, true);
    VexProto.toast(t('已创建并切换到分支') + ' ' + name);
  }
  function renameBranch(oldName, newName) {
    var branch = findBranch(oldName);
    if (!branch) return;
    branch.name = newName;
    if (state.branch === oldName) state.branch = newName;
    state.recentBranches = (state.recentBranches || []).map(function (n) { return n === oldName ? newName : n; });
    persist(); render();
    VexProto.toast(t('已将分支重命名为') + ' ' + newName);
  }
  function deleteBranch(name) {
    if (!findBranch(name)) return;
    // 当前检出的分支不能直接删：先切回默认分支（git 同样拒绝删除已检出分支）。
    if (name === state.branch) switchBranch(DEFAULT_BRANCH, true);
    state.branches = state.branches.filter(function (b) { return b.name !== name; });
    state.recentBranches = (state.recentBranches || []).filter(function (n) { return n !== name; });
    persist(); render();
    VexProto.toast(t('已删除分支') + ' ' + name);
  }

  /* ---------- 弹层：新建/重命名（create-branch-dialog）、合并（merge-choose-branch-dialog）、删除确认（delete-branch-dialog） ---------- */
  function openBranchDialog(mode, initialName, target) {
    branchDialogMode = mode; renameTarget = target || null;
    var renaming = mode === 'rename';
    document.getElementById('git-branch-title').textContent = renaming ? t('重命名分支') : t('新建分支');
    document.getElementById('git-branch-ok').textContent = renaming ? t('重命名分支') : t('创建分支');
    document.getElementById('git-branch-name').value = initialName || '';
    document.getElementById('git-branch-error').classList.add('is-hidden');
    document.getElementById('git-branch-base-row').style.display = renaming ? 'none' : '';
    if (!renaming) {
      var note = document.getElementById('git-branch-base-note'), options = document.getElementById('git-branch-base-options');
      if (state.branch === DEFAULT_BRANCH) {
        note.innerHTML = esc(fmt(t('新分支将基于你当前检出的分支（{0}）。'), state.branch)) + ' ' + esc(fmt(t('{0} 是仓库的默认分支。'), state.branch));
        options.innerHTML = '';
      } else {
        note.textContent = '';
        options.innerHTML = '<div class="tb-branch-base-label">' + esc(t('基于…')) + '</div>'
          + '<label class="setting-label"><input type="radio" name="git-branch-base" value="current" checked><span><b>' + esc(state.branch) + '</b> — ' + esc(t('你当前检出的分支。要在这个分支已有工作的基础上继续时选它。')) + '</span></label>'
          + '<label class="setting-label"><input type="radio" name="git-branch-base" value="default"><span><b>' + esc(DEFAULT_BRANCH) + '</b> — ' + esc(t('仓库的默认分支。想从不依赖当前分支的起点开始时选它。')) + '</span></label>';
      }
    }
    VexProto.handleAction('dlg-git-branch');
  }
  document.getElementById('git-branch-ok').addEventListener('click', function () {
    var name = document.getElementById('git-branch-name').value.trim();
    var error = document.getElementById('git-branch-error');
    if (!validBranchName(name)) { error.textContent = t('分支名称不合法：不能以 - 或 . 开头，不能含空格、..、~ ^ : ? * [ \\，不能以 / 或 . 结尾'); error.classList.remove('is-hidden'); return; }
    if (name !== renameTarget && findBranch(name)) { error.textContent = fmt(t('已存在名为 {0} 的分支。'), name); error.classList.remove('is-hidden'); return; }
    document.getElementById('layer-git-branch').hidden = true;
    if (branchDialogMode === 'create') createBranch(name);
    else if (name !== renameTarget) renameBranch(renameTarget, name);
  });
  document.getElementById('git-branch-name').addEventListener('input', function () {
    document.getElementById('git-branch-error').classList.add('is-hidden');
  });

  function updateMergeNote() {
    var source = document.getElementById('git-merge-source').value;
    var note = document.getElementById('git-merge-note');
    var ok = document.getElementById('git-merge-ok');
    if (!source) { note.textContent = t('没有其他可合并的分支。'); ok.disabled = true; return; }
    ok.disabled = false;
    note.innerHTML = esc(t('将把来自')) + ' <b>' + esc(source) + '</b> ' + esc(t('的提交合并到')) + ' <b>' + esc(state.branch) + '</b>' + esc(t('。'));
  }
  function openMergeDialog() {
    var others = state.branches.filter(function (b) { return b.name !== state.branch; });
    document.getElementById('git-merge-source').innerHTML = others.map(function (b) {
      return '<option value="' + esc(b.name) + '">' + esc(b.name) + '</option>';
    }).join('');
    document.getElementById('git-merge-title').innerHTML = esc(t('合并到')) + ' <b>' + esc(state.branch) + '</b>';
    document.getElementById('git-merge-ok').textContent = t('合并到') + ' ' + state.branch;
    updateMergeNote();
    VexProto.handleAction('dlg-git-merge');
  }
  document.getElementById('git-merge-source').addEventListener('change', updateMergeNote);
  document.getElementById('git-merge-ok').addEventListener('click', function () {
    var source = document.getElementById('git-merge-source').value, target = state.branch;
    if (!source) return;
    state.history.unshift({ hash: randomHash(), subject: 'merge: ' + source + ' → ' + target, author: 'Dotnet9(dotnet9.com)', time: now(), files: [] });
    if (state.remote) state.ahead += 1;
    var branch = currentBranch();
    if (branch) branch.updated = now();
    tab = 'history'; selectedChange = null;
    document.getElementById('layer-git-merge').hidden = true;
    persist(); render();
    VexProto.toast(t('已将 {0} 合并到 {1}').split('{0}').join(source).split('{1}').join(target));
  });
  document.getElementById('git-branch-delete-ok').addEventListener('click', function () {
    document.getElementById('layer-git-branch-delete').hidden = true;
    if (deleteTarget) deleteBranch(deleteTarget);
    deleteTarget = null;
  });

  /* ---------- 事件 ---------- */
  pane.addEventListener('click', function (event) {
    var chip = event.target.closest('[data-git="branch"]');
    if (chip) { if (branchPop.hidden) openBranchPop(chip); else closeBranchPop(); }
    var repoChip = event.target.closest('[data-git="repo"]');
    if (repoChip) { if (repoPop.hidden) openRepoPop(repoChip); else closeRepoPop(); }
  });
  branchPop.addEventListener('input', function (event) {
    if (event.target.id !== 'tb-branch-filter') return;
    branchFilterText = event.target.value; branchKbIndex = 0;
    renderBranchList();
  });
  branchPop.addEventListener('keydown', function (event) {
    if (event.target.id !== 'tb-branch-filter') return;
    var rows = visibleBranchRows();
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {       // 方向键在可见行间移动
      event.preventDefault();
      if (rows.length) { branchKbIndex = (branchKbIndex + (event.key === 'ArrowDown' ? 1 : rows.length - 1)) % rows.length; renderBranchList(); }
    } else if (event.key === 'Enter') {                              // 有结果=切换；无结果=按筛选文字新建（GHD 同）
      event.preventDefault();
      if (rows.length) { switchBranch(rows[branchKbIndex].branch.name); closeBranchPop(); }
      else if (branchFilterText.trim()) { closeBranchPop(); openBranchDialog('create', branchFilterText.trim()); }
    }
  });
  branchPop.addEventListener('click', function (event) {
    var act = event.target.closest('[data-branch-act]');
    if (act) {
      var kind = act.dataset.branchAct;
      closeBranchPop();
      if (kind === 'new') openBranchDialog('create', branchFilterText.trim());
      else if (kind === 'merge') openMergeDialog();
      return;
    }
    var row = event.target.closest('[data-branch-row]');
    if (row) { switchBranch(row.dataset.branchRow); closeBranchPop(); }
  });
  branchPop.addEventListener('contextmenu', function (event) {       // 行右键：重命名… / 复制分支名 / 删除…
    var row = event.target.closest('[data-branch-row]');
    if (!row) return;
    event.preventDefault();
    branchMenu.innerHTML = '<button type="button" role="menuitem" data-branch-act="rename">' + esc(t('重命名…')) + '</button>'
      + '<button type="button" role="menuitem" data-branch-act="copy">' + esc(t('复制分支名')) + '</button>'
      + '<button type="button" role="menuitem" data-branch-act="delete">' + esc(t('删除…')) + '</button>';
    branchMenu.dataset.branch = row.dataset.branchRow;
    branchMenu.hidden = false;
    var size = branchMenu.getBoundingClientRect();
    branchMenu.style.left = Math.max(8, Math.min(event.clientX, innerWidth - size.width - 8)) + 'px';
    branchMenu.style.top = Math.max(8, Math.min(event.clientY, innerHeight - size.height - 8)) + 'px';
  });
  branchMenu.addEventListener('click', function (event) {
    var act = event.target.closest('[data-branch-act]');
    if (!act) return;
    var name = branchMenu.dataset.branch, kind = act.dataset.branchAct;
    closeBranchMenu();
    if (kind === 'rename') { closeBranchPop(); openBranchDialog('rename', name, name); }
    else if (kind === 'copy') { try { navigator.clipboard.writeText(name); } catch (e) { /* 非安全上下文忽略 */ } VexProto.toast(t('已复制分支名') + '：' + name); }
    else if (kind === 'delete') {
      deleteTarget = name;
      document.getElementById('git-branch-delete-msg').innerHTML = esc(t('删除分支')) + ' <b>' + esc(name) + '</b>' + esc(t('？'));
      closeBranchPop();
      VexProto.handleAction('dlg-git-branch-delete');
    }
  });
  document.addEventListener('pointerdown', function (event) {
    if (!branchMenu.hidden && !branchMenu.contains(event.target) && !event.target.closest('[data-branch-row]')) closeBranchMenu();
    if (!branchPop.hidden && !branchPop.contains(event.target) && !branchMenu.contains(event.target)
      && !event.target.closest('[data-git="branch"]') && !event.target.closest('[data-git="repo"]') && !repoPop.contains(event.target)) closeBranchPop();
    if (!repoPop.hidden && !repoPop.contains(event.target) && !event.target.closest('[data-git="repo"]')) closeRepoPop();
  });
  document.addEventListener('keydown', function (event) {
    if (event.key !== 'Escape') return;
    if (!branchMenu.hidden) { event.preventDefault(); closeBranchMenu(); }
    else if (!branchPop.hidden) { event.preventDefault(); closeBranchPop(); }
    else if (!repoPop.hidden) { event.preventDefault(); closeRepoPop(); }
  });
  var branchPopSize = '';
  // 窗口尺寸真变了才收起（刷新后/截图时的伪 resize 事件不收）。
  window.addEventListener('resize', function () {
    if (!branchPop.hidden && branchPopSize !== innerWidth + 'x' + innerHeight) closeBranchPop();
    if (!repoPop.hidden) closeRepoPop();
  });

  function openTagDialog(pushAfter) {
    document.getElementById('git-tag-name').value = '';
    document.getElementById('git-tag-push').checked = pushAfter;
    document.getElementById('git-tag-title').textContent = pushAfter ? t('打 tag 并推送') : t('打 tag');
    document.getElementById('git-tag-error').classList.add('is-hidden');
    VexProto.handleAction('dlg-git-tag');
  }
  document.getElementById('git-tag-ok').addEventListener('click', function () {
    var name = document.getElementById('git-tag-name').value.trim();
    var push = document.getElementById('git-tag-push').checked;
    var error = document.getElementById('git-tag-error');
    if (!/^[A-Za-z0-9._-]+$/.test(name)) { error.classList.remove('is-hidden'); return; }
    if (!state.history.length) { error.textContent = t('还没有提交，无法打 tag'); error.classList.remove('is-hidden'); return; }
    var layer = document.getElementById('layer-git-tag');
    if (layer) layer.hidden = true;
    (push ? VexProto.toast(t('tag 已创建并推送') + '：' + name) : VexProto.toast(t('tag 已创建') + '：' + name));
  });
  document.getElementById('git-tag-name').addEventListener('input', function () {
    document.getElementById('git-tag-error').classList.add('is-hidden');
  });

  /* ---------- 右键菜单「打开所在目录」（与文件列表右键菜单同一套样式；先选中右键行再弹出） ---------- */
  var gitContextMenu = document.createElement('div');
  gitContextMenu.className = 'file-context-menu'; gitContextMenu.hidden = true; gitContextMenu.setAttribute('role', 'menu');
  document.body.appendChild(gitContextMenu);
  function closeGitContextMenu() { gitContextMenu.hidden = true; }
  pane.addEventListener('contextmenu', function (event) {
    var changeRow = event.target.closest('.change-item[data-git-file]');
    var commitFileRow = event.target.closest('[data-git-commit-file]');
    if (!changeRow && !commitFileRow) return;
    event.preventDefault(); event.stopPropagation();

    // 历史提交右键：tag 操作（GHD commit-list 右键「Create Tag…」同位）。
    var commitRow = event.target.closest('[data-git-commit]');
    if (commitRow) {
      if (selectedCommit !== commitRow.dataset.gitCommit) { selectedCommit = commitRow.dataset.gitCommit; render(); }
      gitContextMenu.innerHTML = '<button type="button" role="menuitem" data-git-tag-act="create">' + esc(t('打 tag…')) + '</button>'
        + '<button type="button" role="menuitem" data-git-tag-act="push">' + esc(t('推送 tag')) + '</button>';
      gitContextMenu.hidden = false;
      var tagWidth = gitContextMenu.offsetWidth || 168, tagHeight = gitContextMenu.offsetHeight || 36;
      gitContextMenu.style.left = Math.max(8, Math.min(event.clientX, innerWidth - tagWidth - 8)) + 'px';
      gitContextMenu.style.top = Math.max(8, Math.min(event.clientY, innerHeight - tagHeight - 8)) + 'px';
      gitContextMenu.querySelector('button').focus({ preventScroll: true });
      return;
    }

    var path;
    if (changeRow) {
      var index = Number(changeRow.dataset.gitFile);
      var change = state.changes[index];
      if (!change) return;
      if (selectedChange !== change.path) { selectedChange = change.path; render(); }
      path = change.path;
    } else if (commitFileRow) {
      var commit = state.history.find(function (c) { return c.hash === selectedCommit; });
      var file = commit && commit.files[Number(commitFileRow.dataset.gitCommitFile)];
      if (!file) return;
      if (selectedCommitFile !== file.path) { selectedCommitFile = file.path; render(); }
      path = file.path;
    }
    gitContextMenu.innerHTML = '<button type="button" role="menuitem" data-git-context="reveal" data-git-reveal-path="' + esc(path) + '">' + esc(t('打开所在目录')) + '</button>';
    gitContextMenu.hidden = false;
    var width = gitContextMenu.offsetWidth || 168, height = gitContextMenu.offsetHeight || 36;
    gitContextMenu.style.left = Math.max(8, Math.min(event.clientX, innerWidth - width - 8)) + 'px';
    gitContextMenu.style.top = Math.max(8, Math.min(event.clientY, innerHeight - height - 8)) + 'px';
    gitContextMenu.querySelector('button').focus({ preventScroll: true });
  });
  gitContextMenu.addEventListener('click', function (event) {
    var tagAct = event.target.closest('[data-git-tag-act]');
    if (tagAct) { closeGitContextMenu(); openTagDialog(tagAct.dataset.gitTagAct === 'push'); return; }
    var action = event.target.closest('[data-git-context="reveal"]'); if (!action) return;
    closeGitContextMenu();
    /* 演示态：桌面端经 Toolkit FileHelper 打开资源管理器并选中（跨平台）。 */
    VexProto.toastKey('ToastFileLocation', action.dataset.gitRevealPath);
  });
  document.addEventListener('pointerdown', function (event) { if (!gitContextMenu.contains(event.target)) closeGitContextMenu(); });
  document.addEventListener('keydown', function (event) { if (event.key === 'Escape' && !gitContextMenu.hidden) { event.preventDefault(); closeGitContextMenu(); } });

  /* ---------- 文件 / Git 模式切换 ---------- */
  function setMode(mode) {
    var git = mode === 'git';
    document.body.classList.toggle('git-mode', git);
    var filesBtn = document.getElementById('mode-files'), gitBtn = document.getElementById('mode-git');
    filesBtn.classList.toggle('on', !git); filesBtn.setAttribute('aria-pressed', String(!git));
    gitBtn.classList.toggle('on', git); gitBtn.setAttribute('aria-pressed', String(git));
    pane.hidden = !git;
    renderPanelBranch();
    if (git) render();
  }

  /* ---------- 动作接入 ---------- */
  var previousHandle = window.VexProto && VexProto.handleAction;
  var GIT_MENU_ACTIONS = {
    'git-pull': function () { pull(); },
    'git-push': function () { push(); },
    'git-fetch': function () { fetchOrigin(); },
    'git-init': function () { initRepo(false); },
    'git-remote': function () { openRemoteDialog(); },
    'git-new-branch': function () { openBranchDialog('create', ''); },
    'git-merge': function () { openMergeDialog(); },
    'git-create-tag': function () { openTagDialog(false); },
    'git-push-tag': function () { openTagDialog(true); }
  };
  VexProto.handleAction = function (action, item) {
    if (GIT_MENU_ACTIONS[action]) { GIT_MENU_ACTIONS[action](); return true; }
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

  /* ---------- 大纲折叠钮移位：外观/语言面板右侧、窗口按钮左侧（DOMContentLoaded 时 panel 已建） ---------- */
  function moveOutlineButton() {
    var captionBtns = document.getElementById('caption-btns');
    var outlineBtn = document.getElementById('outline-toggle');
    var panel = document.getElementById('vex-theme-panel');
    if (!captionBtns || !outlineBtn) return;
    if (panel) panel.after(outlineBtn);
    else captionBtns.before(outlineBtn);
  }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', moveOutlineButton);
  else moveOutlineButton();

  /* ---------- 偏好联动（自动提交默认关） ---------- */
  window.addEventListener('vex-preference-changed', function (event) {
    if (event.detail && /^gitAutoCommit/.test(event.detail.key)) { state.nextAutoAt = 0; persist(); syncAutoTimer(); }
  });
  window.addEventListener('vex-language-changed', function () { ensureGitMenuButton(); renderPanelBranch(); if (!pane.hidden) render(); });
  ensureGitMenuButton();   // 标题栏 Git 菜单（仅 Git 模式显示，CSS 控制）
  renderPanelBranch();
  window.VexGitPanel = { setMode: setMode, showPane: function () { setMode('git'); }, get state() { return state; } };
})();
