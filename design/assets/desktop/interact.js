/* ============================================================
   Vex 界面原型 · 交互层（单页版）
   主窗体为 Vex.html，采用单窗口固定视图组合；菜单与状态栏的界面
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
    { label:'跟随系统', action:'theme', arg:'system', check:true }, { label:'浅色', action:'theme', arg:'light', check:true }, { label:'深色', action:'theme', arg:'dark', check:true }, { sep:true }
  ].concat(VexPalettes.map(function(p){return {label:p.label,action:'palette',arg:p.key,check:true};}));

  var MENUS = [
    { key: 'file', label: '文件(F)', items: [
      { label: '新建', gesture: 'Ctrl+N', action: 'new' },
      { label: '新建窗口', gesture: 'Ctrl+Shift+N', action: 'new-window' },
      { sep: true },
      { label: '打开…', gesture: 'Ctrl+O', action: 'open' },
      { label: '打开文件夹…', action: 'open-folder' },
      { label: '演示文档', action: 'open-samples' },
      { label: '快速打开…', gesture: 'Ctrl+P', action: 'quick-open' },
      { label: '最近打开', sub: [
        { label: '排版与写作.md', action: 'open-sample', arg: 0 },
        { label: '图文与链接.md', action: 'open-sample', arg: 1 },
        { label: '数学与化学.md', action: 'open-sample', arg: 2 },
        { sep: true }, { label: '清空最近记录', toast: '最近记录已清空（演示）' }
      ]},
      { sep: true },
      { label: '保存', gesture: 'Ctrl+S', action: 'save' },
      { label: '另存为…', gesture: 'Ctrl+Shift+S', action: 'save' },
      { sep: true },
      { label: '导出', sub: [
        { label: '导出 HTML', action: 'export-html' }, { label: '导出 PDF', action: 'export-pdf' },
        { label: '导出 PNG 长图', toast: 'PNG 长图导出（桌面版操作演示）' },
        { label: '导出 Word（.docx）', toast: 'Word 导出（桌面版操作演示）' }
      ]},
      { label: '导入', sub: [
        { label: '从 HTML 导入…', toast: 'HTML 导入（桌面版操作演示）' },
        { label: '从 PDF 导入…', toast: 'PDF 导入（桌面版操作演示）' },
        { label: '从 Word 导入…', toast: 'Word 导入（桌面版操作演示）' }
      ]},
      { label: '打印预览', gesture: 'Ctrl+Shift+P', action: 'print' },
      { sep: true },
      { label: '文档属性', gesture: 'Alt+Enter', act: 'dlg-properties' },
      { label: '打开文件位置', toast: '在系统文件管理器中定位（桌面版操作演示）' },
      { label: '选择编码重新打开', sub: ['UTF-8', 'UTF-8 BOM', 'GB18030', 'Big5'].map(function (label) {
        return { label: label, toast: '按 ' + label + ' 重新读取（桌面版操作演示）' };
      })},
      { sep: true }, { label: '删除文档…', danger: true, act: 'dlg-delete' },
      { label: '关闭文档', gesture: 'Ctrl+W', action: 'close' }
    ]},
    { key: 'edit', label: '编辑(E)', items: [
      { label: '撤销', gesture: 'Ctrl+Z', action: 'undo' }, { label: '重做', gesture: 'Ctrl+Y', action: 'redo' },
      { sep: true },
      { label: '剪切', gesture: 'Ctrl+X', action: 'cut' }, { label: '复制', gesture: 'Ctrl+C', action: 'copy' },
      { label: '粘贴', gesture: 'Ctrl+V', action: 'paste' }, { label: '全选', gesture: 'Ctrl+A', action: 'select-all' },
      { label: '复制为纯文本', gesture: 'Ctrl+Shift+C', action: 'copy-plain' },
      { sep: true },
      { label: '查找…', gesture: 'Ctrl+F', action: 'find' }, { label: '替换…', gesture: 'Ctrl+H', action: 'replace' },
      { sep: true }, { label: '自动配对输入', check: true, action: 'toggle-pairs' }
    ]},
    { key: 'insert', label: '插入(I)', items: [
      { label: '段落', action: 'insert-paragraph' },
      { label: '标题 1', action: 'insert-heading1' }, { label: '标题 2', action: 'insert-heading2' }, { label: '标题 3', action: 'insert-heading3' }, { label: '标题 4', action: 'insert-heading4' }, { label: '标题 5', action: 'insert-heading5' }, { label: '标题 6', action: 'insert-heading6' },
      { label: '无序列表', action: 'insert-list' }, { label: '有序列表', action: 'insert-ordered' }, { label: '任务列表', action: 'insert-task' },
      { label: '引用', action: 'insert-quote' },
      { sep: true },
      { label: '表格', action: 'insert-table', sub:VexElementMenu.tableItems() }, { label: '代码块', action: 'insert-code' },
      { label: '公式块', action: 'insert-math' }, { label: '水平分割线', action: 'insert-rule' },
      { label: '链接', action: 'insert-link' }, { label: '图片', action: 'insert-image' },
      { sep: true },
      { label: '加粗', action: 'insert-bold' }, { label: '斜体', action: 'insert-italic' },
      { label: '行内代码', action: 'insert-inline-code' }, { label: '清除格式', action: 'clear-format' }
    ]},
    { key: 'view', label: '视图(V)', items: [
      { label: '编辑视图', check: true, action: 'view-edit' }, { label: '阅读视图', check: true, action: 'view-read' },
      { label: '思维导图', check: true, action: 'view-mindmap' },
      { sep: true },
      { label: '即时渲染', check: true, action: 'edit-instant' }, { label: '源码', check: true, action: 'edit-source' },
      { label: '侧边栏', check: true, action: 'toggle-sidebar' }, { label: '文档列表', action: 'pane-files' }, { label: '文档大纲', action: 'pane-outline' },
      { label: '显示行号', check: true, action: 'toggle-lines' }, { label: '显示词数', check: true, action: 'toggle-word-count' },
      { label: '显示文档水印', check: true, action: 'toggle-watermark' }, { label: '界面动效', check: true, action: 'toggle-motion' },
      { sep: true },
      { label: '演示模式', gesture: 'F5', action: 'presentation' },
      { label: '专注模式', gesture: 'F8', check: true, action: 'toggle-focus' }, { label: '打字机模式', check: true, action: 'toggle-typewriter' },
      { sep: true },
      { label: '全屏', gesture: 'F11', check: true, action: 'toggle-fullscreen' },
      { label: '保持窗口在最前', check: true, action: 'toggle-topmost' }, { label: '最大化 / 还原', action: 'maximize' }
    ]},
    { key: 'tools', label: '工具(T)', items: [
      { label: '字数统计', act: 'dlg-statistics' },
      { label: '刷新内容', action: 'refresh' }, { sep: true },
      { label: '复制到微信公众号', action: 'copy-wechat' },
      { label: '复制到知乎', action: 'copy-zhihu' },
      { label: '复制到稀土掘金', action: 'copy-juejin' },
      { sep: true }, { label: 'MCP 设置', act: 'dlg-mcp' }, { label: 'MCP 审计', toast: '查看调用记录与操作结果（桌面版操作演示）' }
    ]},
    { key: 'help', label: '帮助(H)', items: [
      { label: '新手引导', action: 'guide' }, { label: '更新日志', act: 'dlg-doc', arg: '更新日志' },
      { label: '鸣谢', act: 'dlg-doc', arg: '鸣谢' }, { sep: true },
      { label: '官方网站', toast: '官方网站：codewf.com' }, { label: '反馈', toast: '反馈入口：github.com/dotnet9/Vex/issues' },
      { label: '检查更新', act: 'dlg-update' }, { label: '关于 Vex', act: 'dlg-about' }
    ]}
  ];
  var originalInsert = MENUS[2].items;
  var paragraphItems = originalInsert.filter(function (item) { return item.action && !['insert-link','insert-image','insert-bold','insert-italic','insert-inline-code','clear-format'].includes(item.action); });
  var paragraphItem = function (action) { return paragraphItems.find(function (item) { return item.action === action; }); };
  MENUS[2] = { key:'paragraph', label:'段落(P)', items:[
    paragraphItem('insert-paragraph'),
    paragraphItem('insert-heading1'), paragraphItem('insert-heading2'), paragraphItem('insert-heading3'),
    paragraphItem('insert-heading4'), paragraphItem('insert-heading5'), paragraphItem('insert-heading6'), {sep:true},
    paragraphItem('insert-list'), paragraphItem('insert-ordered'), paragraphItem('insert-task'), paragraphItem('insert-quote'), {sep:true},
    paragraphItem('insert-table'), paragraphItem('insert-code'), paragraphItem('insert-math'),
    {label:'Mermaid 图表',sub:VexMermaid.items.map(function (item) {return {label:item.label,action:'insert-mermaid-'+item.key};})},
    {sep:true}, paragraphItem('insert-rule'), {label:'脚注',action:'insert-footnote'}
  ]};
  MENUS.splice(3,0,{key:'format',label:'格式(O)',items:[{label:'加粗',gesture:'Ctrl+B',action:'insert-bold'},{label:'斜体',gesture:'Ctrl+I',action:'insert-italic'},{label:'删除线',action:'insert-strike'},{label:'高亮',action:'insert-highlight'},{label:'上标',action:'insert-superscript'},{label:'下标',action:'insert-subscript'},{label:'行内代码',action:'insert-inline-code'},{sep:true},{label:'链接',action:'insert-link'},{label:'图片',action:'insert-image'},{sep:true},{label:'清除格式',action:'clear-format'}]});
  var toolsMenu = MENUS.find(function (menu) { return menu.key === 'tools'; });
  toolsMenu.items.unshift({label:'偏好设置…',gesture:'Ctrl+,',action:'preferences'},{sep:true});
  toolsMenu.items = toolsMenu.items.map(function (item) { return item.label === 'MCP 设置' ? {label:'MCP 设置',action:'preferences-mcp'} : item.label === 'MCP 审计' ? {label:'MCP 审计',action:'mcp-audit'} : item; });
  if (new URLSearchParams(location.search).get('platform') === 'macos') MENUS.unshift({key:'app',label:'Vex',items:[{label:'关于 Vex',act:'dlg-about'},{label:'偏好设置…',gesture:'⌘,',action:'preferences'},{sep:true},{label:'退出 Vex',gesture:'⌘Q',action:'exit-app'}]});
  VexProto.menuData = MENUS;
  var menuNav = document.querySelector('.tb-menu');
  menuNav.innerHTML = MENUS.map(function (menu) { return '<button class="tb-menuitem" type="button" aria-haspopup="true" aria-expanded="false">' + menu.label + '</button>'; }).join('');

  /* 把一个 .tb-menu 导航容器变成可交互菜单 */
  var openMenuKey = null;
  var menuHost = null;
  var menuTrigger = null;
  var hoverSwitch = false;
  var switchMenu = null;
  var submenus = [];
  function hideSubmenu(sub) {
    clearTimeout(sub._openTimer); clearTimeout(sub._hideTimer);
    var visible = !sub.hidden;
    sub.hidden = true; sub._owner.setAttribute('aria-expanded','false');
    submenus.filter(function (item) { return item._owner.closest('.submenu') === sub; }).forEach(hideSubmenu);
    if (visible) window.dispatchEvent(new CustomEvent('vex-menu-positioned'));
  }
  function clearSubmenus() { submenus.forEach(function (sub) { clearTimeout(sub._openTimer); clearTimeout(sub._hideTimer); sub.remove(); }); submenus = []; }
  function closeOpenMenu(restoreFocus) {
    var trigger = menuTrigger;
    clearSubmenus();
    if (menuHost) menuHost.hidden = true;
    openMenuKey = null;
    menuTrigger = null;
    document.querySelectorAll('.tb-menuitem.open').forEach(function (m) { m.classList.remove('open'); });
    document.querySelectorAll('.tb-menuitem').forEach(function (m) { m.setAttribute('aria-expanded', 'false'); });
    if (restoreFocus === true && trigger?.isConnected) trigger.focus({preventScroll:true});
    window.dispatchEvent(new CustomEvent('vex-menu-positioned'));
  }
  VexProto.closeMenu = closeOpenMenu;

  function fitMenu(menu, availableHeight) {
    // Keep every command reachable even when compact groups exceed the window.
    menu.style.maxHeight = ''; menu.style.overflowY = '';
    menu.classList.remove('menu-compact');
    if (menu.offsetHeight > availableHeight) menu.classList.add('menu-compact');
    if (menu.offsetHeight > availableHeight) {
      menu.style.maxHeight = Math.max(64, availableHeight) + 'px';
      menu.style.overflowY = 'auto';
    }
  }

  function menuBounds() {
    var app = document.querySelector('.app-window').getBoundingClientRect();
    return {left:Math.max(8,app.left + 6),right:Math.min(innerWidth - 8,app.right - 6),top:Math.max(8,app.top + 6),bottom:Math.min(innerHeight - 8,app.bottom - 6)};
  }

  function focusItem(menu, last) {
    var items = menu.querySelectorAll('.menu-item:not([aria-disabled=true])');
    var target=last ? items[items.length - 1] : items[0];
    target?.focus({preventScroll:true});target?.scrollIntoView({block:'nearest'});
  }

  function buildItem(it, toastAll, depth) {
    depth = depth || 0;
    if (it.sep) { var s = document.createElement('div'); s.className = 'menu-sep'; s.setAttribute('role','separator'); return s; }
    if (it.check && it.action && VexProto.isActionChecked) it.on = VexProto.isActionChecked(it.action, it);
    if (it.action === 'theme') it.on = document.documentElement.dataset.themeMode === it.arg;
    if (it.action === 'palette') it.on = document.documentElement.dataset.palette === it.arg;
    var el = document.createElement('div');
    el.dataset.menuLabel = it.label;
    if (it.action || it.act) el.dataset.menuAction = it.action || it.act;
    el.tabIndex = -1;
    var disabled = typeof it.disabled === 'function' ? it.disabled() : !!it.disabled;
    if (disabled) { el.setAttribute('aria-disabled','true'); el.tabIndex = -1; }
    el.setAttribute('role', it.check ? 'menuitemcheckbox' : 'menuitem');
    if (it.check) el.setAttribute('aria-checked', String(!!it.on));
    el.addEventListener('keydown', function (event) {
      if (event.target !== el) return;
      if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); event.stopPropagation(); el.click(); }
      if (['ArrowDown','ArrowUp','Home','End'].includes(event.key)) {
        event.preventDefault(); event.stopPropagation();
        var menu = el.closest('[role=menu]'), siblings = Array.from(menu.querySelectorAll('.menu-item:not([aria-disabled=true])'));
        var index = event.key === 'Home' ? 0 : event.key === 'End' ? siblings.length - 1 : (siblings.indexOf(el) + (event.key === 'ArrowDown' ? 1 : siblings.length - 1)) % siblings.length;
        submenus.filter(function (sub) {return sub._owner.closest('[role=menu]') === menu;}).forEach(hideSubmenu);
        siblings[index]?.focus({preventScroll:true});siblings[index]?.scrollIntoView({block:'nearest'});
      }
      if (event.key === 'ArrowRight') { event.preventDefault(); event.stopPropagation(); if (el._sub) { el._placeSub(); focusItem(el._sub); } else if (!el.closest('.submenu')) switchMenu(1); }
      if (event.key === 'ArrowLeft' || event.key === 'Escape') {
        event.preventDefault(); event.stopPropagation();
        var parent = el.closest('.submenu');
        if (el._sub && !el._sub.hidden && event.key === 'Escape') { hideSubmenu(el._sub); return; }
        if (parent) { hideSubmenu(parent); parent._owner.focus({preventScroll:true}); }
        else if (event.key === 'ArrowLeft') switchMenu(-1);
        else closeOpenMenu(true);
      }
    });
    el.className = 'menu-item' + (it.danger ? ' danger' : '') + (it.on ? ' checked' : '') + (it.sub ? ' has-sub' : '');
    var indicator = document.createElement('span'); indicator.className = 'menu-state'; indicator.setAttribute('aria-hidden', 'true'); indicator.textContent = it.on ? '✓' : ''; el.appendChild(indicator);
    var label = document.createElement('span'); label.className = 'menu-label'; label.textContent = window.VexL ? VexL.label(it.label) : it.label; el.appendChild(label);
    if (it.gesture) {
      var g = document.createElement('span');
      g.className = 'mi-gesture';
      g.textContent = new URLSearchParams(location.search).get('platform') === 'macos' ? it.gesture.replace(/Ctrl\+/g,'⌘').replace(/Shift\+/g,'⇧').replace(/Alt\+/g,'⌥') : it.gesture;
      el.appendChild(g);
    } else if (it.sub) {
      var a = document.createElement('span');
      a.className = 'mi-arrow';
      a.setAttribute('aria-hidden','true');
      a.innerHTML = '<svg class="ico vex-expand-icon" aria-hidden="true"><use href="#i-chevron"/></svg>';
      el.appendChild(a);
    }
    if (it.sub) {
      var sub = document.createElement('div');
      sub.className = 'submenu flyout'; sub.setAttribute('role', 'menu'); sub.setAttribute('aria-label', window.VexL ? VexL.label(it.label) : it.label);
      sub._owner = el; sub._depth = depth; sub.dataset.parentLabel = it.label;
      sub.id = 'vex-submenu-' + submenus.length; submenus.push(sub);
      sub.hidden = true; document.body.appendChild(sub);
      el.setAttribute('aria-haspopup','menu'); el.setAttribute('aria-expanded','false'); el.setAttribute('aria-controls',sub.id);
      it.sub.forEach(function (s) { sub.appendChild(buildItem(s,toastAll,depth+1)); });
      function placeSub() {
        clearTimeout(sub._openTimer); clearTimeout(sub._hideTimer);
        submenus.filter(function (s) { return s._depth >= depth && s !== sub; }).forEach(hideSubmenu);
        var r = el.getBoundingClientRect();
        var bounds = menuBounds();
        sub.hidden = false;
        fitMenu(sub, bounds.bottom - bounds.top);
        var parent = el.closest('[role=menu]').getBoundingClientRect();
        var right = parent.right + 4, left = parent.left - sub.offsetWidth - 4;
        var flip = right + sub.offsetWidth > bounds.right;
        sub.dataset.side = flip ? 'left' : 'right';
        sub.style.left = Math.max(bounds.left, Math.min(flip ? left : right, bounds.right - sub.offsetWidth)) + 'px';
        sub.style.top = Math.max(bounds.top, Math.min(r.top - 6, bounds.bottom - sub.offsetHeight)) + 'px';
        el.setAttribute('aria-expanded','true');
        window.dispatchEvent(new CustomEvent('vex-menu-positioned'));
      }
      function branchHovered(menu) { return menu.matches(':hover') || submenus.some(function (child) { return child._owner.closest('.submenu') === menu && !child.hidden && branchHovered(child); }); }
      function scheduleHide() { clearTimeout(sub._hideTimer); sub._hideTimer = setTimeout(function () { if (!window.VexGuide?.active && !branchHovered(sub) && !el.matches(':hover')) hideSubmenu(sub); },220); }
      el.addEventListener('mouseenter', function () { if(!window.VexGuide?.active||VexGuide.allows(el)) { clearTimeout(sub._openTimer); sub._openTimer = setTimeout(placeSub,120); } });
      el.addEventListener('mouseleave', function () { clearTimeout(sub._openTimer); scheduleHide(); });
      sub.addEventListener('mouseenter', function () { clearTimeout(sub._hideTimer); });
      sub.addEventListener('mouseleave',scheduleHide);
      el._placeSub = placeSub;
      el._sub = sub;
    }
    el.addEventListener('click', function (e) {
      e.stopPropagation();
      if (disabled) return;
      if (it.sub) {
        el._placeSub();
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
        closeOpenMenu();
        var handled = VexProto.handleAction(it.action, it);
        if (handled !== false) return;
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
    host.hidden = true;
    nav.setAttribute('role','menubar');
    menuHost = host;
    var bar = nav.parentNode;
    bar.style.position = 'relative';
    bar.appendChild(host);
    var buttons = Array.from(nav.querySelectorAll('.tb-menuitem'));
    function activate(item) {
      buttons.forEach(function (button) {button.tabIndex = button === item ? 0 : -1;button.classList.toggle('open',button === item);});
    }
    switchMenu = function (direction) {
      var next = buttons[(buttons.indexOf(menuTrigger) + direction + buttons.length) % buttons.length];
      openFor(next); focusItem(host);
    };

    function openFor(item, fromHover) {
      var key = item.getAttribute('data-menu');
      var data = MENUS.filter(function (m) { return m.label === key || m.key === key; })[0];
      if (!data) return;
      clearSubmenus(); host.innerHTML = '';
      window.VexTypography?.close(); window.VexWorkspace?.closeAppearance();
      var languageMenu = document.getElementById('vex-language-menu'); if (languageMenu) languageMenu.open = false;
      var box = document.createElement('div');
      box.className = 'flyout';
      data.items.forEach(function (it) { box.appendChild(buildItem(it, data.toastAll)); });
      host.appendChild(box);
      nav.querySelectorAll('.tb-menuitem').forEach(function (button) { button.setAttribute('aria-expanded', 'false'); });
      item.setAttribute('aria-expanded', 'true');
      box.setAttribute('role', 'menu');
      box.id = 'vex-menu-' + data.key; box.setAttribute('aria-labelledby',item.id); item.setAttribute('aria-controls',box.id);
      host.hidden = false;
      host.style.left = item.getBoundingClientRect().left - bar.getBoundingClientRect().left + 'px';
      host.style.top = (nav.offsetTop + nav.offsetHeight + 2) + 'px';
      var app = menuBounds();
      fitMenu(box, app.bottom - host.getBoundingClientRect().top);
      var bounds = host.getBoundingClientRect();
      host.style.left = Math.max(app.left, Math.min(bounds.left, app.right - bounds.width)) - bar.getBoundingClientRect().left + 'px';
      openMenuKey = data.key;
      menuTrigger = item; hoverSwitch = !!fromHover; activate(item);
      window.dispatchEvent(new CustomEvent('vex-menu-positioned'));
    }

    nav.querySelectorAll('.tb-menuitem').forEach(function (item, index) {
      var key = MENUS[index].key;
      item.id = 'vex-menu-trigger-' + key;
      item.tabIndex = index === 0 ? 0 : -1;
      item.setAttribute('role','menuitem'); item.setAttribute('aria-haspopup','menu');
      item.setAttribute('data-menu', MENUS[index].label);
      item.addEventListener('click', function (e) {
        e.stopPropagation();
        if (openMenuKey === key && !hoverSwitch) { closeOpenMenu(); return; }
        closeOpenMenu();
        item.classList.add('open');
        openFor(item);
      });
      item.addEventListener('keydown', function (event) {
        if (event.key === 'ArrowDown' || event.key === 'ArrowUp') { event.preventDefault(); event.stopPropagation(); openFor(item); focusItem(host,event.key === 'ArrowUp'); }
        if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); event.stopPropagation(); var next = buttons[(index + (event.key === 'ArrowRight' ? 1 : buttons.length - 1)) % buttons.length]; if (openMenuKey) openFor(next); else buttons.forEach(function (button) {button.tabIndex = button === next ? 0 : -1;}); next.focus(); }
      });
      item.addEventListener('mouseenter', function () {
        if(window.VexGuide?.active&&!VexGuide.allows(item))return;
        if (openMenuKey && openMenuKey !== key) {
          document.querySelectorAll('.tb-menuitem.open').forEach(function (m) { m.classList.remove('open'); });
          item.classList.add('open');
          openFor(item, true);
        }
      });
    });

    document.addEventListener('pointerdown', function (event) { if (!event.target.closest('.tb-menu,.menu-flyout,.submenu,.tour-card')) closeOpenMenu(); });
    document.addEventListener('click', function (event) { if (!event.target.closest('.tb-menu,.menu-flyout,.submenu,.tour-card')) closeOpenMenu(); });
    document.addEventListener('keydown', function (event) {
      if (event.key === 'Escape' && openMenuKey) { event.preventDefault(); closeOpenMenu(true); }
      if (event.key === 'Tab' && openMenuKey) closeOpenMenu(true);
      if (!event.altKey || event.ctrlKey || event.metaKey || event.isComposing) return;
      var item = buttons.find(function (button) {return button.dataset.menu.match(/\((.)\)/)?.[1].toLowerCase() === event.key.toLowerCase();});
      if (item && (!window.VexGuide?.active || VexGuide.allows(item))) { event.preventDefault(); openFor(item); focusItem(host); }
    });
    window.addEventListener('resize',function () {closeOpenMenu();});
    window.addEventListener('blur',function () {closeOpenMenu();});
  };
})();
