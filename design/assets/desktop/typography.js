/* Shared document typography; compact spacing is independent. */
(function () {
  'use strict';
  var items = VexTypes;
  var state = { key: 'Simple', compact: false, scope: 'recommended' };
  /* 用户自定义排版（规则 §8/§13）：支持多个，存储为数组；卡上编辑/删除，"+"块新增，均走对话框。 */
  var customStore = 'vex-custom-typographics';
  var customs = [];
  function readCustoms() {
    try { var raw = JSON.parse(VexStorage.getItem(customStore) || '[]'); if (Array.isArray(raw)) customs = raw.filter(function (c) { return c && c.id && c.light && c.dark; }); } catch (_) {}
  }
  function saveCustoms() { try { VexStorage.setItem(customStore, JSON.stringify(customs)); } catch (_) {} }
  function themeOf(c) {
    var deco = 'light-dark(' + c.deco + ',' + c.decoDark + ')';
    return {
      key: 'user-' + c.id, label: c.name || '自定义', en: 'Custom', variant: 'custom', custom: c,
      light: c.light, dark: c.dark,
      rules: 'BODY{--type-decoration:' + deco + ';--type-solid:' + deco + ';--type-on-accent:#fff;--type-size:16px;--type-leading:' + c.leading + '}'
        + (c.font === 'serif' ? 'BODY{font-family:var(--serif,"Noto Serif SC",serif)}' : c.font === 'mono' ? 'BODY{font-family:var(--mono,"Cascadia Code",monospace)}' : '')
        + 'BODY h1{font-weight:650}BODY h2{color:var(--fg)}'
        + ({ none: 'BODY h2{border:0;padding:0;background:none}',
             underline: 'BODY h2{border-bottom:2px solid var(--type-decoration);padding-bottom:.35em;background:none}',
             tint: 'BODY h2{background:color-mix(in srgb,var(--type-decoration) 10%,transparent);padding:.4em .7em;border-radius:4px}',
             leftBar: 'BODY h2{border-left:4px solid var(--type-decoration);padding:.3em .7em;background:none}' }[c.h2] || '')
    };
  }
  function syncCustomThemes() {
    items = items.filter(function (item) { return item.key.indexOf('user-') !== 0; });
    customs.forEach(function (c) { items = items.concat([themeOf(c)]); });
  }
  function injectCustomStyles() {
    var style = document.getElementById('custom-typography-style');
    if (!customs.length) { if (style) style.remove(); return; }
    if (!style) { style = document.createElement('style'); style.id = 'custom-typography-style'; document.head.append(style); }
    style.textContent = customs.map(function (c) {
      var theme = themeOf(c);
      var scope = ':root[data-typography="' + theme.key + '"]';
      return ['.content-area .md-body', '.markdown-body'].map(function (selector) {
        return theme.rules.replaceAll('BODY', scope + ' ' + selector);
      }).join('\n')
      + theme.rules.replaceAll('BODY', '.type-swatch[data-preview-type="' + theme.key + '"] .type-document');
    }).join('\n');
  }

  var state = state; /* 占位保持下方 saved 恢复逻辑可读 */
  try {
    var saved = JSON.parse(VexStorage.getItem('vex-design-typography') || '{}');
    if (typeof saved.compact === 'boolean') state.compact = saved.compact;
    if (saved.scope === 'all' || saved.scope === 'recommended') state.scope = saved.scope;
    if (typeof saved.key === 'string') state.key = saved.key; /* 合法性在 sync 后校验 */
  } catch (_) {}
  var requested = new URLSearchParams(location.search).get('typography');
  if (requested) state.key = requested;
  readCustoms(); syncCustomThemes();
  /* 恢复的 key 不存在（内置或自定义已删）则回退默认。 */
  if (!items.some(function (item) { return item.key === state.key; })) state.key = items[0].key;

  var panel = document.createElement('section'); panel.id = 'typography-panel'; panel.className = 'typography-popover'; panel.hidden = true;
  panel.setAttribute('aria-labelledby', 'typography-title'); panel.setAttribute('role', 'dialog');
  function cardHtml(item) {
    if (item.custom) {
      return '<div class="typography-card" data-custom-card="' + item.key + '"><button type="button" class="typography-card-apply" data-typography-key="' + item.key + '" aria-label="' + item.label + '，应用此正文排版"><span class="typography-card-heading"><strong class="typography-card-name">' + item.label + '</strong><span class="typography-card-selected" aria-hidden="true">✓ 当前</span></span>' + VexTypeCardPreview(item.key) + '</button><span class="typography-card-tools"><button type="button" data-edit-type="' + item.key + '" title="编辑此排版" aria-label="编辑' + item.label + '">✎</button><button type="button" data-delete-type="' + item.key + '" title="删除此排版" aria-label="删除' + item.label + '">×</button></span></div>';
    }
    return '<button type="button" class="typography-card" data-typography-key="' + item.key + '" aria-label="' + item.label + '，应用此正文排版"><span class="typography-card-heading"><strong class="typography-card-name">' + item.label + '</strong><span class="typography-card-selected" aria-hidden="true">✓ 当前</span></span>' + VexTypeCardPreview(item.key) + '</button>';
  }
  function renderGrid() {
    panel.querySelector('.typography-grid').innerHTML = items.map(cardHtml).join('')
      + '<button type="button" class="typography-add" id="add-custom-type" aria-label="新增自定义排版" title="新增自定义排版"><span>＋</span><small>新增排版</small></button>';
    applyScope();
  }
  /* 推荐 / 全部（规则 §9）：当前方案与自定义排版始终可见，避免选中项被筛选隐藏。 */
  function applyScope() {
    panel.querySelectorAll('.typography-grid [data-typography-key]').forEach(function (button) {
      var item = items.find(function (candidate) { return candidate.key === button.dataset.typographyKey; });
      var hidden = state.scope === 'recommended' && item && !item.custom && !item.recommended && item.key !== state.key;
      if (hidden) button.setAttribute('hidden', ''); else button.removeAttribute('hidden');
    });
    panel.querySelectorAll('[data-tp-scope]').forEach(function (button) {
      button.setAttribute('aria-pressed', String(button.dataset.tpScope === state.scope));
    });
  }
  panel.innerHTML = '<div class="popover-title"><strong id="typography-title">正文排版</strong><button class="icon-button" id="typography-close" aria-label="关闭排版"><svg class="ico" aria-hidden="true"><use href="#i-x"/></svg></button></div><div class="typography-density"><strong>渲染密度</strong><div class="density-options" role="group" aria-label="Markdown 渲染密度">' + [['compact','紧凑'],['normal','正常'],['relaxed','宽松']].map(function (item) { return '<button type="button" data-tp-mdsize="' + item[0] + '" title="作用于 Markdown 渲染（基准字号 13/15/17）">' + item[1] + '</button>'; }).join('') + '</div></div><div class="typography-scope"><strong>方案范围</strong><div class="density-options" role="group" aria-label="排版范围"><button type="button" data-tp-scope="recommended">推荐</button><button type="button" data-tp-scope="all">全部 ' + items.filter(function (item) { return item.key.indexOf('user-') !== 0; }).length + ' 套</button></div></div><div class="typography-grid" role="group" aria-label="排版效果预览"></div>';
  renderGrid();
  document.querySelector('.window-body').appendChild(panel);

  /* 新增/编辑对话框（独立于弹出层）。 */
  var dialog = document.createElement('section'); dialog.id = 'typography-dialog'; dialog.hidden = true;
  dialog.innerHTML = '<div class="typography-dialog-card" role="dialog" aria-modal="true" aria-labelledby="typography-dialog-title"><strong id="typography-dialog-title">新增排版</strong>'
    + '<label class="td-row">名称<input type="text" data-tf="name" maxlength="12" placeholder="如：暖纸"></label>'
    + '<div class="td-row td-colors">'
    + [['light', '强调·浅'], ['dark', '强调·深'], ['deco', '装饰·浅'], ['decoDark', '装饰·深']].map(function (pair) { return '<label>' + pair[1] + '<input type="color" data-tf="' + pair[0] + '"></label>'; }).join('')
    + '</div>'
    + '<div class="td-row"><label>字体<select data-tf="font"><option value="sans">无衬线</option><option value="serif">衬线</option><option value="mono">等宽</option></select></label>'
    + '<label>行距<select data-tf="leading"><option value="1.7">紧凑 1.7</option><option value="1.9">正常 1.9</option><option value="2.1">舒展 2.1</option></select></label>'
    + '<label>H2 装饰<select data-tf="h2"><option value="leftBar">左竖线</option><option value="underline">下划线</option><option value="tint">色块</option><option value="none">无</option></select></label></div>'
    + '<div class="td-actions"><button type="button" class="btn" id="td-cancel">取消</button><button type="button" class="btn primary" id="td-apply">保存并应用</button></div></div>';
  document.body.append(dialog);
  var editingId = null;
  function field(name) { return dialog.querySelector('[data-tf="' + name + '"]'); }
  function openDialog(theme) {
    editingId = theme ? theme.custom.id : null;
    dialog.querySelector('#typography-dialog-title').textContent = theme ? '编辑排版' : '新增排版';
    var c = theme ? theme.custom : { name: '', light: '#2C7A7B', dark: '#7FD1CC', deco: '#B7791F', decoDark: '#F6C667', font: 'sans', leading: '1.9', h2: 'leftBar' };
    field('name').value = c.name || '';
    field('light').value = c.light; field('dark').value = c.dark;
    field('deco').value = c.deco; field('decoDark').value = c.decoDark;
    field('font').value = c.font; field('leading').value = String(c.leading); field('h2').value = c.h2;
    dialog.hidden = false;
    field('name').focus();
  }
  function closeDialog() { dialog.hidden = true; editingId = null; }
  dialog.querySelector('#td-cancel').onclick = closeDialog;
  dialog.querySelector('#td-apply').onclick = function () {
    var record = {
      id: editingId || ('t' + Date.now().toString(36)),
      name: field('name').value.trim() || '自定义',
      light: field('light').value.toUpperCase(), dark: field('dark').value.toUpperCase(),
      deco: field('deco').value.toUpperCase(), decoDark: field('decoDark').value.toUpperCase(),
      font: field('font').value, leading: field('leading').value, h2: field('h2').value
    };
    if (editingId) customs = customs.map(function (c) { return c.id === editingId ? record : c; });
    else customs = customs.concat([record]);
    saveCustoms(); syncCustomThemes(); injectCustomStyles(); renderGrid();
    state.key = 'user-' + record.id; apply(); closeDialog();
  };
  dialog.addEventListener('keydown', function (event) { if (event.key === 'Escape') { event.stopPropagation(); closeDialog(); } });

  function apply() {
    var theme = items.find(function (item) { return item.key === state.key; }), root = document.documentElement;
    if (!theme) { state.key = items[0].key; theme = items[0]; }
    root.dataset.typography = theme.key; root.dataset.typographyVariant = theme.variant; root.dataset.compact = String(state.compact);
    /* 自定义主题用编写层的双明暗强调色；内置主题走界面 accent（design-system.js 会以 typeTokens 覆写）。 */
    root.style.setProperty('--typography-accent', theme.custom ? 'light-dark(' + theme.custom.light + ',' + theme.custom.dark + ')' : 'var(--accent)');
    document.querySelectorAll('[data-typography-label]').forEach(function (label) { label.textContent = theme.label; });
    panel.querySelectorAll('[data-typography-key]').forEach(function (button) {
      button.setAttribute('aria-pressed', String(button.dataset.typographyKey === state.key));
    });
    panel.querySelectorAll('[data-custom-card]').forEach(function (card) {
      card.classList.toggle('current', card.dataset.customCard === state.key);
    });
    /* 渲染密度行：作用于 Markdown 渲染（正文字号档，§11），与外观菜单的界面密度独立。 */
    var currentMdSize = window.VexTheme ? VexTheme.state.mdsize : 'normal';
    panel.querySelectorAll('[data-tp-mdsize]').forEach(function (button) {
      button.classList.toggle('on', button.dataset.tpMdsize === currentMdSize);
    });
    applyScope();
    try { VexStorage.setItem('vex-design-typography', JSON.stringify(state)); } catch (_) {}
    /* 偏好启动回放会重放 typography（workbench configure→apply），这里同步写入避免覆盖（与自定义配色同模式）。 */
    if (window.VexPreferences && VexPreferences.values.typography !== state.key) VexPreferences.set('typography', state.key);
  }
  panel.querySelector('.typography-density').onclick = function (event) {
    var size = event.target.closest('[data-tp-mdsize]');
    if (size) { if (window.VexTheme) VexTheme.setMdSize(size.dataset.tpMdsize); apply(); }
  };
  panel.querySelector('.typography-scope').onclick = function (event) {
    var scope = event.target.closest('[data-tp-scope]');
    if (scope) { state.scope = scope.dataset.tpScope; apply(); }
  };
  panel.querySelector('.typography-grid').onclick = function (event) {
    var edit = event.target.closest('[data-edit-type]');
    if (edit) { var t = items.find(function (item) { return item.key === edit.dataset.editType; }); if (t) openDialog(t); return; }
    var del = event.target.closest('[data-delete-type]');
    if (del) {
      var key = del.dataset.deleteType;
      customs = customs.filter(function (c) { return 'user-' + c.id !== key; });
      saveCustoms(); syncCustomThemes(); injectCustomStyles(); renderGrid();
      if (state.key === key) state.key = 'Simple';
      apply(); return;
    }
    if (event.target.closest('#add-custom-type')) { openDialog(null); return; }
    var button = event.target.closest('[data-typography-key]');
    if (button) choose(button.dataset.typographyKey);
  };
  panel.querySelector('.typography-grid').addEventListener('keydown', function (event) {
    if (event.altKey || event.ctrlKey || event.metaKey || event.isComposing) return;
    var button = event.target.closest('[data-typography-key]'); if (!button) return;
    var buttons = Array.from(panel.querySelectorAll('[data-typography-key]:not([hidden])')), index = buttons.indexOf(button);
    var columns = getComputedStyle(button.parentElement).gridTemplateColumns.split(' ').length;
    var next = {ArrowLeft:index-1, ArrowRight:index+1, ArrowUp:index-columns, ArrowDown:index+columns, Home:0, End:buttons.length-1}[event.key];
    if (next === undefined) return;
    event.preventDefault(); event.stopPropagation();
    var target = buttons[Math.max(0,Math.min(buttons.length-1,next))];
    target.focus({preventScroll:true}); target.scrollIntoView({block:'nearest',inline:'nearest'});
  });
  var trigger;
  function position() {
    if (panel.hidden || !trigger) return;
    var host = panel.parentElement.getBoundingClientRect(), rect = trigger.getBoundingClientRect();
    var below = Math.min(host.bottom,innerHeight)-rect.bottom-12, above = rect.top-Math.max(host.top,0)-12, flip = below < 320 && above > below;
    panel.style.maxHeight = Math.max(140,Math.min(680,flip ? above : below)) + 'px';
    panel.style.left = Math.max(8,Math.min(host.width-panel.offsetWidth-8,rect.right-host.left-panel.offsetWidth)) + 'px'; panel.style.right = 'auto';
    panel.style.top = (flip ? Math.max(8,rect.top-host.top-panel.offsetHeight-8) : Math.max(8,rect.bottom-host.top+8)) + 'px';
  }
  window.addEventListener('resize',position);
  function close(restoreFocus) { var wasOpen = !panel.hidden; panel.hidden = true; if (!dialog.hidden) closeDialog(); document.querySelectorAll('[data-action="typography"]').forEach(function (button) { button.setAttribute('aria-expanded', 'false'); }); if (restoreFocus && wasOpen && trigger) trigger.focus({preventScroll:true}); }
  document.getElementById('typography-close').onclick = function () { close(true); };
  function choose(key) { key=VexTypeKey(key);if (items.some(function (item) { return item.key === key; })) { state.key = key; apply(); } }
  document.addEventListener('click', function (event) {
    if (!panel.contains(event.target) && !event.target.closest('#typography-dialog') && !event.target.closest('[data-action="typography"]') && !event.target.closest('.menu-flyout')) close();
  });
  document.addEventListener('keydown', function (event) { if (event.key === 'Escape') close(true); });
  document.addEventListener('vex-layout-rendered', function () { if (!document.querySelector('[data-action="typography"]:not([hidden])')) close(); });
  window.VexTypography = {
    items: items, state: state, apply: apply, choose: choose,
    toggleCompact: function () { state.compact = !state.compact; apply(); },
    close: close,
    toggle: function (anchor) {
      if (!panel.hidden) { close(); return; }
      panel.hidden = false;
      trigger = anchor || document.querySelector('[data-action="typography"]');
      position();
      document.querySelectorAll('[data-action="typography"]').forEach(function (button) { button.setAttribute('aria-expanded', 'true'); });
    },
    exportStyles: function () { return VexTypeExport(state.key,state.compact); }
  };
  injectCustomStyles();
  apply();
})();
