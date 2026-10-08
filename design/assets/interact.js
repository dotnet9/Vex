/* ============================================================
   Vex 界面原型 · 交互层（单页版）
   页面只有一个主窗体（index.html），所有界面通过标题菜单 / 状态栏
   在窗体内以浮层呼出：
   - VexProto.toast(msg)          操作反馈（对应 App 状态栏提示）
   - VexProto.applyTheme(key)     程序化切主题
   - VexProto.attachMenuBar(el)   标题菜单栏（数据取自 ShellTitleMenuView.axaml）
   - VexProto.handleAction        页面注入：处理菜单呼出动作（act 名）
   ============================================================ */
(function () {
  var VexProto = {
    applyTheme: function (key) {
      if (typeof apply === 'function') apply(key);
    }
  };
  window.VexProto = VexProto;

  /* ---------- Toast ---------- */
  var toastWrap = null;
  VexProto.toast = function (msg) {
    if (!toastWrap) {
      toastWrap = document.createElement('div');
      toastWrap.className = 'proto-toast-wrap';
      document.body.appendChild(toastWrap);
    }
    var t = document.createElement('div');
    t.className = 'proto-toast';
    t.textContent = msg;
    toastWrap.appendChild(t);
    requestAnimationFrame(function () { t.classList.add('show'); });
    setTimeout(function () {
      t.classList.remove('show');
      setTimeout(function () { t.remove(); }, 250);
    }, 1900);
  };

  /* ---------- 标题菜单（ShellTitleMenuView.axaml 的原型化） ---------- */
  var THEME_ITEMS = [
    { label: '跟随系统', theme: 'light' },
    { label: '浅色 · 极地蓝', theme: 'light' },
    { label: '深色 · 石墨', theme: 'dark' },
    { sep: true },
    { label: '水生 · 湖心青', theme: 'aquatic', checked: true },
    { label: '沙漠 · 暖阳橙', theme: 'desert' },
    { label: '黄昏 · 薄暮紫', theme: 'dusk' },
    { label: '夜空 · 深海蓝', theme: 'night-sky' }
  ];
  var TYPE_ITEMS = ['Basic', '简', '橙心', '墨黑', '姹紫', '嫩青', '绿意', '红绯', '蓝莹', '科技蓝', '兰青', '山吹', '前端之峰', '极客黑', '蔷薇紫', '萌绿', '全栈蓝', '微信公众号'];

  var MENUS = [
    { key: 'file', label: '文件(F)', items: [
      { label: '新建', gesture: 'Ctrl+N', toast: '已新建空白文档（演示）' },
      { label: '新建窗口', toast: '已打开新窗口（演示）' },
      { sep: true },
      { label: '打开...', gesture: 'Ctrl+O', toast: '弹出文件选择框（演示）' },
      { label: '打开文件夹...', toast: '已打开文件夹工作区（演示）' },
      { label: '快速打开...', gesture: 'Ctrl+K', toast: '快速打开面板（演示）' },
      { label: '打开最近文件', icon: 'i-clock', sub: [
        { label: '主题设计笔记.md', toast: '打开前会先经过「未保存更改」确认', act: 'dlg-unsaved' },
        { label: '每周复盘.md', toast: '已打开最近文件（演示）' },
        { label: '读书摘录.md', toast: '已打开最近文件（演示）' },
        { sep: true },
        { label: '清空最近文件', toast: '已清空最近文件（演示）' }
      ]},
      { label: '选择编码重新打开', icon: 'i-info', sub: [
        { label: 'UTF-8' }, { label: 'UTF-8 BOM' }, { label: 'GB18030' }, { label: 'Big5' }
      ], toastAll: '已按所选编码重新打开（演示）' },
      { sep: true },
      { label: '复制到公众号', toast: '富 HTML 已复制，可直接粘贴到公众号编辑器（演示）' },
      { label: '复制到知乎', toast: '富 HTML 已复制，可直接粘贴到知乎编辑器（演示）' },
      { label: '复制到稀土掘金', toast: '富 HTML 已复制，可直接粘贴到掘金编辑器（演示）' },
      { sep: true },
      { label: '保存', icon: 'i-save', gesture: 'Ctrl+S', toast: '已保存 · 状态栏恢复「已保存」（演示）' },
      { label: '另存为...', gesture: 'Ctrl+Shift+S', toast: '弹出另存为对话框（演示）' },
      { label: '保存全部打开的文件...', toast: '已保存全部文档（演示）' },
      { sep: true },
      { label: '属性...', icon: 'i-gear', gesture: 'Alt+Enter', act: 'dlg-properties' },
      { label: '打开文件位置...', icon: 'i-ext', toast: '已在资源管理器中定位文件（演示）' },
      { label: '删除...', icon: 'i-trash', danger: true, act: 'dlg-delete' },
      { sep: true },
      { label: '导出', icon: 'i-export', sub: [
        { label: 'HTML', toast: '已导出 HTML（演示）' },
        { label: 'PDF', toast: '导出失败会弹出「错误提示」，试试看', act: 'dlg-error' },
        { label: 'PNG', toast: '已导出 PNG 长图（演示）' },
        { label: 'Word（.docx）', toast: '已导出 Word（演示）' }
      ]},
      { label: '打印...', icon: 'i-print', gesture: 'Ctrl+P', toast: '已唤起打印预览（演示）' },
      { sep: true },
      { label: '关闭', gesture: 'Ctrl+W', toast: '关闭前会先经过「未保存更改」确认', act: 'dlg-unsaved' }
    ]},
    { key: 'edit', label: '编辑(E)', items: [
      { label: '撤销', gesture: 'Ctrl+Z', toast: '已撤销（演示）' },
      { label: '重做', gesture: 'Ctrl+Y', toast: '已重做（演示）' },
      { sep: true },
      { label: '剪切', gesture: 'Ctrl+X', toast: '已剪切（演示）' },
      { label: '复制', gesture: 'Ctrl+C', toast: '已复制（演示）' },
      { label: '粘贴', gesture: 'Ctrl+V', toast: '网页粘贴会优先转换为 Markdown（演示）' },
      { label: '全选', gesture: 'Ctrl+A', toast: '已全选（演示）' },
      { sep: true },
      { label: '查找...', icon: 'i-search', gesture: 'Ctrl+F', act: 'find' },
      { label: '替换...', icon: 'i-search', gesture: 'Ctrl+H', act: 'replace' }
    ]},
    { key: 'para', label: '段落(P)', items: [
      { label: '段落' }, { label: '标题 1' }, { label: '标题 2' }, { label: '标题 3' },
      { label: '标题 4' }, { label: '标题 5' }, { label: '标题 6' },
      { sep: true },
      { label: '表格' }, { label: '代码块' }, { label: '公式块' }, { label: '引用' },
      { label: '有序列表' }, { label: '无序列表' }, { label: '任务列表' }, { label: '水平分割线' }
    ], toastAll: '已在光标处插入「%s」（演示）' },
    { key: 'format', label: '格式(O)', items: [
      { label: '加粗' }, { label: '斜体' }, { label: '代码' },
      { sep: true },
      { label: '链接' }, { label: '图像' }, { label: '清除格式' }
    ], toastAll: '已对选区应用「%s」（演示）' },
    { key: 'view', label: '视图(V)', items: [
      { label: '刷新预览', gesture: 'F5', toast: '预览已刷新（演示）' },
      { sep: true },
      { label: '侧边栏', check: true, on: true, action: 'toggle-sidebar' },
      { label: '大纲', icon: 'i-list', act: 'pane-outline' },
      { label: '文档列表', icon: 'i-folder', act: 'pane-files' },
      { label: '源代码模式', check: true, action: 'toggle-source' },
      { label: '预览模式', check: true, on: true, action: 'toggle-preview' },
      { label: '显示行号', check: true, on: true, toast: '行号显示已切换（演示）' },
      { label: '显示状态栏', check: true, on: true, toast: '状态栏显示已切换（演示）' },
      { label: '字数统计窗口', act: 'dlg-statistics' },
      { sep: true },
      { label: '切换全屏', gesture: 'F11', toast: '已切换全屏（演示）' },
      { label: '保持窗口在最前端', check: true, toast: '窗口置顶已切换（演示）' }
    ]},
    { key: 'help', label: '帮助(H)', items: [
      { label: '主题色', sub: THEME_ITEMS.map(function (t) {
        return t.sep ? { sep: true } : { label: t.label, check: true, on: !!t.checked, action: 'theme', arg: t.theme };
      })},
      { label: '排版', sub: TYPE_ITEMS.map(function (n, i) {
        return { label: n, check: true, on: i === 0, toast: '排版主题已切换为「' + n + '」（演示）' };
      }).concat([{ sep: true }, { label: '紧凑布局', check: true, toast: '紧凑布局已切换（演示）' }]) },
      { label: '国际化(L)', sub: [
        { label: '简体中文', check: true, on: true, toast: '界面语言已是简体中文' },
        { label: '繁體中文', toast: '界面语言已切换为繁體中文（演示）' },
        { label: 'English', toast: 'Language switched to English (demo)' },
        { label: '日本語', toast: 'インターフェースを日本語に切り替えました（デモ）' }
      ]},
      { sep: true },
      { label: 'MCP 设置', act: 'dlg-mcp' },
      { label: '更新日志', act: 'dlg-doc', arg: '更新日志' },
      { label: '新手引导', toast: '新手引导蒙层开始（演示）' },
      { label: '鸣谢', act: 'dlg-doc', arg: '鸣谢' },
      { sep: true },
      { label: '官方网站', toast: '将在浏览器打开 codewf.com（演示）' },
      { label: '反馈', toast: '将在浏览器打开 GitHub Issues（演示）' },
      { label: '关于', act: 'dlg-about' }
    ]}
  ];

  /* 把一个 .tb-menu 导航容器变成可交互菜单 */
  var openMenuKey = null;
  var menuHost = null;
  function closeOpenMenu() {
    if (menuHost) menuHost.style.display = 'none';
    openMenuKey = null;
    document.querySelectorAll('.tb-menuitem.open').forEach(function (m) { m.classList.remove('open'); });
  }

  function buildItem(it, toastAll) {
    if (it.sep) { var s = document.createElement('div'); s.className = 'menu-sep'; return s; }
    if (it.action === 'toggle-sidebar' && VexProto.isSidebarVisible) it.on = VexProto.isSidebarVisible();
    var el = document.createElement('div');
    el.className = 'menu-item' + (it.danger ? ' danger' : '') + (it.on ? ' checked' : '') + (it.sub ? ' has-sub' : '');
    if (it.icon && document.getElementById(it.icon)) {
      var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
      svg.setAttribute('class', 'ico mi-ico');
      var use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
      use.setAttribute('href', '#' + it.icon);
      svg.appendChild(use);
      el.appendChild(svg);
    }
    el.appendChild(document.createTextNode(it.label));
    if (it.gesture) {
      var g = document.createElement('span');
      g.className = 'mi-gesture kbd';
      g.textContent = it.gesture;
      el.appendChild(g);
    } else if (it.sub) {
      var a = document.createElement('span');
      a.className = 'mi-arrow';
      a.textContent = '▸';
      el.appendChild(a);
    }
    if (it.sub) {
      var sub = document.createElement('div');
      sub.className = 'submenu flyout';
      it.sub.forEach(function (s) { sub.appendChild(buildItem(s)); });
      el.appendChild(sub);
      function placeSub() {
        el.parentNode.querySelectorAll(':scope > .menu-item > .submenu').forEach(function (s) {
          if (s !== sub) s.style.display = 'none';
        });
        var r = el.getBoundingClientRect();
        sub.style.display = 'block';
        sub.style.position = 'fixed';
        sub.style.left = Math.min(r.right - 4, window.innerWidth - 226) + 'px';
        sub.style.top = Math.min(r.top - 6, window.innerHeight - 180) + 'px';
      }
      el.addEventListener('mouseenter', placeSub);
      el.addEventListener('mouseleave', function () {
        setTimeout(function () {
          if (!sub.matches(':hover') && !el.matches(':hover')) sub.style.display = 'none';
        }, 120);
      });
      sub.addEventListener('mouseenter', function () { sub.style.display = 'block'; });
      el._placeSub = placeSub;
      el._sub = sub;
    }
    el.addEventListener('click', function (e) {
      e.stopPropagation();
      if (it.sub) {
        if (el._sub.style.display === 'block') el._sub.style.display = 'none';
        else el._placeSub();
        return;
      }
      if (it.action === 'theme') {
        VexProto.applyTheme(it.arg);
        closeOpenMenu();
        VexProto.toast('主题已切换为「' + it.label + '」');
        return;
      }
      if (it.action && typeof VexProto.handleAction === 'function') {
        var handled = VexProto.handleAction(it.action, it);
        if (handled !== false) { closeOpenMenu(); return; }
      }
      closeOpenMenu();
      if (it.act && typeof VexProto.handleAction === 'function') {
        VexProto.handleAction(it.act, it);
        return;
      }
      var msg = it.toast || (toastAll ? toastAll.replace('%s', it.label) : null);
      if (msg) VexProto.toast(msg);
    });
    return el;
  }

  VexProto.attachMenuBar = function (nav) {
    var host = document.createElement('div');
    host.className = 'menu-flyout';
    host.style.display = 'none';
    menuHost = host;
    var bar = nav.parentNode;
    bar.style.position = 'relative';
    bar.appendChild(host);

    function openFor(item) {
      var key = item.getAttribute('data-menu');
      var data = MENUS.filter(function (m) { return m.label === key || m.key === key; })[0];
      if (!data) return;
      host.innerHTML = '';
      var box = document.createElement('div');
      box.className = 'flyout';
      box.style.minWidth = '248px';
      data.items.forEach(function (it) { box.appendChild(buildItem(it, data.toastAll)); });
      host.appendChild(box);
      host.style.display = 'block';
      host.style.left = Math.max(0, Math.min(item.offsetLeft, bar.offsetWidth - 268)) + 'px';
      host.style.top = (nav.offsetTop + nav.offsetHeight + 2) + 'px';
      openMenuKey = data.key;
    }

    nav.querySelectorAll('.tb-menuitem').forEach(function (item) {
      var key = (item.textContent || '').trim();
      item.setAttribute('data-menu', key);
      item.addEventListener('click', function (e) {
        e.stopPropagation();
        if (openMenuKey === key) { closeOpenMenu(); return; }
        closeOpenMenu();
        item.classList.add('open');
        openFor(item);
      });
      item.addEventListener('mouseenter', function () {
        if (openMenuKey && openMenuKey !== key) {
          document.querySelectorAll('.tb-menuitem.open').forEach(function (m) { m.classList.remove('open'); });
          item.classList.add('open');
          openFor(item);
        }
      });
    });

    document.addEventListener('click', closeOpenMenu);
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeOpenMenu(); });
  };
})();
