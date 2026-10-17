/* ============================================================
   Vex 界面原型 · 交互层（单页版）
   主窗体为 Vex.html，视图可拆出副窗口；菜单与状态栏的界面
   在窗体内以浮层呼出：
   - VexProto.toast(msg)          操作反馈（对应 App 状态栏提示）
   - VexProto.applyTheme(key)     程序化切主题
   - VexProto.attachMenuBar(el)   标题菜单栏（数据取自 ShellTitleMenuView.axaml）
   - VexProto.handleAction        页面注入：处理菜单呼出动作（act 名）
   ============================================================ */
(function () {
  var VexProto = {
    applyTheme: function (key) {
      window.VexTheme.setMode(key);
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
    { label:'跟随系统', theme:'system' }, { label:'浅色', theme:'light' }, { label:'深色', theme:'dark' }, { sep:true },
    { label:'.NET 紫', palette:'dotnet' }, { label:'AI 极光', palette:'aurora' }, { label:'AI 赤陶', palette:'ember' },
    { label:'默认蓝', palette:'default' }, { label:'水生', palette:'aquatic' }, { label:'沙漠', palette:'desert' },
    { label:'黄昏', palette:'dusk' }, { label:'夜空', palette:'night-sky' }
  ];

  var MENUS = [
    { key: 'file', label: '文件(F)', items: [
      { label: '新建文档', gesture: 'Ctrl+N', action: 'new' },
      { label: '新建窗口', toast: '新建窗口（桌面版操作演示）' },
      { sep: true },
      { label: '打开文档…', gesture: 'Ctrl+O', action: 'open' },
      { label: '打开文件夹…', toast: '选择文件夹并载入文档列表（桌面版操作演示）' },
      { label: '快速打开…', gesture: 'Ctrl+P', action: 'quick-open' },
      { label: '最近打开', sub: [
        { label: '主题设计笔记.md', action: 'open-sample', arg: 0 },
        { label: '每周复盘.md', action: 'open-sample', arg: 1 },
        { label: '读书摘录.md', action: 'open-sample', arg: 2 },
        { sep: true }, { label: '清空最近记录', toast: '最近记录已清空（演示）' }
      ]},
      { label: '选择编码重新打开', sub: ['UTF-8', 'UTF-8 BOM', 'GB18030', 'Big5'].map(function (label) {
        return { label: label, toast: '按 ' + label + ' 重新读取（桌面版操作演示）' };
      })},
      { sep: true },
      { label: '保存', gesture: 'Ctrl+S', action: 'save' },
      { label: '另存为…', gesture: 'Ctrl+Shift+S', action: 'save' },
      { label: '导出', sub: [
        { label: 'HTML', action: 'export-html' }, { label: 'PDF', act: 'dlg-error' },
        { label: 'PNG 长图', toast: 'PNG 长图导出（桌面版操作演示）' },
        { label: 'Word（.docx）', toast: 'Word 导出（桌面版操作演示）' }
      ]},
      { label: '打印预览', gesture: 'Ctrl+Shift+P', action: 'print' },
      { sep: true },
      { label: '文档属性', gesture: 'Alt+Enter', act: 'dlg-properties' },
      { label: '打开文件位置', toast: '在系统文件管理器中定位（桌面版操作演示）' },
      { label: '删除文档…', danger: true, act: 'dlg-delete' },
      { label: '关闭文档', gesture: 'Ctrl+W', action: 'close' }
    ]},
    { key: 'edit', label: '编辑(E)', items: [
      { label: '撤销', gesture: 'Ctrl+Z', action: 'undo' }, { label: '重做', gesture: 'Ctrl+Y', action: 'redo' },
      { sep: true },
      { label: '剪切', gesture: 'Ctrl+X', action: 'cut' }, { label: '复制', gesture: 'Ctrl+C', action: 'copy' },
      { label: '粘贴', gesture: 'Ctrl+V', action: 'paste' }, { label: '全选', gesture: 'Ctrl+A', action: 'select-all' },
      { label: '复制为纯文本', gesture: 'Ctrl+Shift+C', action: 'copy-plain' },
      { sep: true },
      { label: '查找…', gesture: 'Ctrl+F', action: 'find' }, { label: '替换…', gesture: 'Ctrl+H', action: 'replace' }
    ]},
    { key: 'insert', label: '插入(I)', items: [
      { label: '段落', action: 'insert-paragraph' },
      { label: '标题 1', action: 'insert-heading1' }, { label: '标题 2', action: 'insert-heading2' }, { label: '标题 3', action: 'insert-heading3' }, { label: '标题 4', action: 'insert-heading4' }, { label: '标题 5', action: 'insert-heading5' }, { label: '标题 6', action: 'insert-heading6' },
      { label: '无序列表', action: 'insert-list' }, { label: '有序列表', action: 'insert-ordered' }, { label: '任务列表', action: 'insert-task' },
      { label: '引用', action: 'insert-quote' },
      { sep: true },
      { label: '表格', action: 'insert-table' }, { label: '代码块', action: 'insert-code' },
      { label: '公式块', action: 'insert-math' }, { label: '水平分割线', action: 'insert-rule' },
      { label: '链接', action: 'insert-link' }, { label: '图片', action: 'insert-image' },
      { sep: true },
      { label: '加粗', action: 'insert-bold' }, { label: '斜体', action: 'insert-italic' },
      { label: '行内代码', action: 'insert-inline-code' }, { label: '清除格式', action: 'clear-format' }
    ]},
    { key: 'view', label: '视图(V)', items: [
      { label: '演示文档', gesture: 'F6', action: 'presentation' }, { sep: true },
      { label: '编辑视图', check: true, action: 'view-edit' }, { label: '阅读视图', check: true, action: 'view-read' },
      { label: '思维导图', check: true, action: 'view-mindmap' },
      { label: '编辑方式', sub: [
        { label: '即时渲染', check: true, action: 'edit-instant' }, { label: '源码', check: true, action: 'edit-source' }
      ]},
      { sep: true },
      { label: '文档导航', sub: [
        { label: '侧边栏', check: true, action: 'toggle-sidebar' }, { label: '文档列表', action: 'pane-files' }, { label: '文档大纲', action: 'pane-outline' }
      ]},
      { label: '编辑显示', sub: [
        { label: '显示行号', check: true, action: 'toggle-lines' },
        { label: '自动配对输入', check: true, action: 'toggle-pairs' }, { label: '显示状态栏', check: true, action: 'toggle-status' }
      ]},
      { label: '写作辅助', sub: [
        { label: '专注模式', gesture: 'F8', check: true, action: 'toggle-focus' }, { label: '打字机模式', check: true, action: 'toggle-typewriter' },
        { label: '界面动效', check: true, action: 'toggle-motion' }
      ]},
      { sep: true },
      { label: '窗口', sub: [
        { label: '全屏', gesture: 'F11', check: true, action: 'toggle-fullscreen' },
        { label: '保持窗口在最前', check: true, action: 'toggle-topmost' }, { label: '最大化 / 还原', action: 'maximize' }
      ]}
    ]},
    { key: 'tools', label: '工具(T)', items: [
      { label: '导图显示设置', action: 'map-settings' }, { label: '字数统计', act: 'dlg-statistics' },
      { label: '刷新内容', gesture: 'F5', action: 'refresh' }, { sep: true },
      { label: '复制到公众号', toast: '公众号富文本复制（桌面版操作演示）' },
      { label: '复制到知乎', toast: '知乎富文本复制（桌面版操作演示）' },
      { label: '复制到稀土掘金', toast: '掘金富文本复制（桌面版操作演示）' },
      { sep: true }, { label: 'MCP 设置', act: 'dlg-mcp' }, { label: 'MCP 审计', toast: '查看调用记录与操作结果（桌面版操作演示）' }
    ]},
    { key: 'help', label: '帮助(H)', items: [
      { label: '新手引导', action: 'guide' }, { label: '更新日志', act: 'dlg-doc', arg: '更新日志' },
      { label: '鸣谢', act: 'dlg-doc', arg: '鸣谢' }, { sep: true },
      { label: '官方网站', toast: '官方网站：codewf.com' }, { label: '反馈', toast: '反馈入口：github.com/dotnet9/Vex/issues' },
      { label: '关于 Vex', act: 'dlg-about' }
    ]}
  ];
  VexProto.menuData = MENUS;

  /* 把一个 .tb-menu 导航容器变成可交互菜单 */
  var openMenuKey = null;
  var menuHost = null;
  function closeOpenMenu() {
    if (menuHost) menuHost.style.display = 'none';
    openMenuKey = null;
    document.querySelectorAll('.tb-menuitem.open').forEach(function (m) { m.classList.remove('open'); });
    document.querySelectorAll('.tb-menuitem').forEach(function (m) { m.setAttribute('aria-expanded', 'false'); });
  }
  VexProto.closeMenu = closeOpenMenu;

  function buildItem(it, toastAll) {
    if (it.sep) { var s = document.createElement('div'); s.className = 'menu-sep'; return s; }
    if (it.check && it.action && VexProto.isActionChecked) it.on = VexProto.isActionChecked(it.action, it);
    if (it.action === 'theme') it.on = document.documentElement.dataset.themeMode === it.arg;
    if (it.action === 'palette') it.on = document.documentElement.dataset.palette === it.arg;
    var el = document.createElement('div');
    el.dataset.menuLabel = it.label;
    if (it.action || it.act) el.dataset.menuAction = it.action || it.act;
    el.tabIndex = 0;
    el.setAttribute('role', it.check ? 'menuitemcheckbox' : 'menuitem');
    if (it.check) el.setAttribute('aria-checked', String(!!it.on));
    el.addEventListener('keydown', function (event) {
      if (event.target !== el) return;
      if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); event.stopPropagation(); el.click(); }
      if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); event.stopPropagation(); var siblings = Array.from(el.parentElement.children).filter(function (item) { return item.classList.contains('menu-item'); }); siblings[(siblings.indexOf(el) + (event.key === 'ArrowDown' ? 1 : siblings.length - 1)) % siblings.length].focus(); }
      if (event.key === 'ArrowRight' && el._sub) { event.preventDefault(); event.stopPropagation(); el._placeSub(); el._sub.querySelector('.menu-item').focus(); }
      if (event.key === 'ArrowLeft' && el.parentElement.classList.contains('submenu')) { event.preventDefault(); event.stopPropagation(); el.parentElement.style.display = 'none'; el.parentElement.parentElement.focus(); }
    });
    el.className = 'menu-item' + (it.danger ? ' danger' : '') + (it.on ? ' checked' : '') + (it.sub ? ' has-sub' : '');
    var indicator = document.createElement('span'); indicator.className = 'menu-state'; indicator.setAttribute('aria-hidden', 'true'); indicator.textContent = it.on ? '✓' : ''; el.appendChild(indicator);
    var label = document.createElement('span'); label.className = 'menu-label'; label.textContent = window.VexL ? VexL.label(it.label) : it.label; el.appendChild(label);
    if (it.gesture) {
      var g = document.createElement('span');
      g.className = 'mi-gesture';
      g.textContent = it.gesture;
      el.appendChild(g);
    } else if (it.sub) {
      var a = document.createElement('span');
      a.className = 'mi-arrow';
      a.textContent = '›';
      el.appendChild(a);
    }
    if (it.sub) {
      var sub = document.createElement('div');
      sub.className = 'submenu flyout'; sub.setAttribute('role', 'menu');
      it.sub.forEach(function (s) { sub.appendChild(buildItem(s)); });
      el.appendChild(sub);
      function placeSub() {
        el.parentNode.querySelectorAll(':scope > .menu-item > .submenu').forEach(function (s) {
          if (s !== sub) s.style.display = 'none';
        });
        var r = el.getBoundingClientRect();
        sub.style.display = 'block';
        sub.style.position = 'fixed';
        sub.style.left = Math.max(8, Math.min(r.right - 4, window.innerWidth - sub.offsetWidth - 8)) + 'px';
        sub.style.top = Math.max(8, Math.min(r.top - 6, window.innerHeight - sub.offsetHeight - 8)) + 'px';
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
      if (it.action === 'theme' || it.action === 'palette') {
        if (it.action === 'palette') window.VexTheme.setPalette(it.arg);
        else VexProto.applyTheme(it.arg);
        closeOpenMenu();
        VexProto.toast('主题已切换为「' + it.label + '」');
        return;
      }
      if (it.toast) { closeOpenMenu(); VexProto.toast(it.toast); return; }
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
      nav.querySelectorAll('.tb-menuitem').forEach(function (button) { button.setAttribute('aria-expanded', 'false'); });
      item.setAttribute('aria-expanded', 'true');
      box.setAttribute('role', 'menu');
      host.style.display = 'block';
      host.style.left = Math.max(0, Math.min(item.offsetLeft, bar.offsetWidth - 268)) + 'px';
      host.style.top = (nav.offsetTop + nav.offsetHeight + 2) + 'px';
      openMenuKey = data.key;
    }

    nav.querySelectorAll('.tb-menuitem').forEach(function (item, index) {
      var key = MENUS[index].key;
      item.setAttribute('data-menu', MENUS[index].label);
      item.addEventListener('click', function (e) {
        e.stopPropagation();
        if (openMenuKey === key) { closeOpenMenu(); return; }
        closeOpenMenu();
        item.classList.add('open');
        openFor(item);
      });
      item.addEventListener('keydown', function (event) { if (event.key === 'ArrowDown') { event.preventDefault(); openFor(item); host.querySelector('.menu-item').focus(); } });
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
