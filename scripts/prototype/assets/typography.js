/* 名称与键来自 ShellAppearanceViewModel；CSS 是原型的排版效果。 */
(function () {
  'use strict';
  var items = [
    ['Basic', 'Basic', '#475467', '#B8C3D3', 'basic'],
    ['Simple', '简', '#475467', '#B8C3D3', 'simple'],
    ['OrangeHeart', '橙心', '#B85421', '#F3AD7C', 'stripe'],
    ['InkBlack', '墨黑', '#242933', '#D7DCE4', 'serif'],
    ['ColorfulPurple', '姹紫', '#7650B3', '#BFA2EA', 'underline'],
    ['TenderGreen', '嫩青', '#317F76', '#80CFC0', 'stripe'],
    ['Verdant', '绿意', '#3A7D3F', '#97D39A', 'underline'],
    ['RedScarlet', '红绯', '#B84557', '#ECA0AC', 'stripe'],
    ['BlueGlow', '蓝莹', '#336FB4', '#8BB9EC', 'underline'],
    ['TechnologyBlue', '科技蓝', '#2369AC', '#85B8ED', 'technical'],
    ['LanQing', '兰青', '#267D88', '#87CAD2', 'underline'],
    ['Yamabuki', '山吹', '#98691C', '#E1BF72', 'stripe'],
    ['FrontendPeak', '前端之峰', '#297785', '#88C6D1', 'technical'],
    ['GeekBlack', '极客黑', '#3D495C', '#C0CBDC', 'technical'],
    ['RosePurple', '蔷薇紫', '#99507C', '#D5A0C0', 'serif'],
    ['CuteGreen', '萌绿', '#41805B', '#9ACDB0', 'stripe'],
    ['FullStackBlue', '全栈蓝', '#2A64A3', '#8EB6E5', 'technical'],
    ['WeChatFormat', '微信公众号', '#327664', '#95C5B5', 'editorial']
  ].map(function (item) { return { key: item[0], label: item[1], light: item[2], dark: item[3], variant: item[4] }; });
  var state = { key: 'Simple', compact: false };
  try {
    var saved = JSON.parse(localStorage.getItem('vex-design-typography') || '{}');
    if (items.some(function (item) { return item.key === saved.key; })) state.key = saved.key;
    if (typeof saved.compact === 'boolean') state.compact = saved.compact;
  } catch (_) {}
  var requested = new URLSearchParams(location.search).get('typography');
  if (items.some(function (item) { return item.key === requested; })) state.key = requested;
  var panel = document.createElement('section'); panel.id = 'typography-panel'; panel.className = 'typography-popover'; panel.hidden = true;
  panel.setAttribute('aria-label', 'Markdown 排版主题');
  panel.innerHTML = '<div class="popover-title">Markdown 排版<button class="icon-button" id="typography-close" aria-label="关闭排版主题">×</button></div><p class="typography-note">用于即时编辑、阅读与正文预览</p><div class="typography-grid">' + items.map(function (item) {
    return '<button class="typography-card" data-typography-key="' + item.key + '" style="--swatch:' + item.light + '"><span class="type-swatch" aria-hidden="true"></span><span>' + item.label + '</span></button>';
  }).join('') + '</div><label class="typography-compact">紧凑布局<input type="checkbox" id="typography-compact"></label>';
  document.querySelector('.window-body').appendChild(panel);
  function apply() {
    var theme = items.find(function (item) { return item.key === state.key; }), root = document.documentElement;
    root.dataset.typography = theme.key; root.dataset.typographyVariant = theme.variant; root.dataset.compact = String(state.compact);
    root.style.setProperty('--typography-accent', root.dataset.theme === 'dark' ? theme.dark : theme.light);
    document.querySelectorAll('[data-typography-label]').forEach(function (label) { label.textContent = theme.label; });
    panel.querySelectorAll('[data-typography-key]').forEach(function (button) {
      button.setAttribute('aria-pressed', String(button.dataset.typographyKey === state.key));
    });
    document.getElementById('typography-compact').checked = state.compact;
    try { localStorage.setItem('vex-design-typography', JSON.stringify(state)); } catch (_) {}
  }
  panel.querySelector('.typography-grid').onclick = function (event) {
    var button = event.target.closest('[data-typography-key]'); if (button) choose(button.dataset.typographyKey);
  };
  document.getElementById('typography-close').onclick = function () { panel.hidden = true; };
  document.getElementById('typography-compact').onchange = function () { state.compact = this.checked; apply(); };
  function choose(key) { if (items.some(function (item) { return item.key === key; })) { state.key = key; apply(); } }
  document.addEventListener('click', function (event) {
    if (!panel.contains(event.target) && !event.target.closest('[data-action="typography"]') && !event.target.closest('.menu-flyout')) panel.hidden = true;
  });
  document.addEventListener('keydown', function (event) { if (event.key === 'Escape') panel.hidden = true; });
  new MutationObserver(apply).observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
  window.VexTypography = {
    items: items, state: state, apply: apply, choose: choose,
    toggleCompact: function () { state.compact = !state.compact; apply(); },
    toggle: function () { panel.hidden = !panel.hidden; },
    exportStyles: function () {
      var theme = items.find(function (item) { return item.key === state.key; });
      var accent = theme.light, css = 'body{font-size:' + (state.compact ? '14' : '16') + 'px;line-height:' + (state.compact ? '1.65' : '1.95') + '}h1{font-size:2em;padding-bottom:14px;border-bottom:1px solid #e1e5ea}h2{margin:26px 0 12px}h3{margin:18px 0 10px}h2,h3{color:' + accent + '}blockquote{border-left-color:' + accent + ';background:color-mix(in srgb,' + accent + ' 6%,transparent)}';
      var variants = {
        simple: 'h2,h3{color:#242933}',
        basic: 'h2{border-bottom:1px solid #e1e5ea;padding-bottom:8px}',
        stripe: 'h2{border-left:3px solid ' + accent + ';padding:3px 0 3px 12px}',
        underline: 'h2{border-bottom:2px solid color-mix(in srgb,' + accent + ' 45%,transparent);padding-bottom:8px}',
        technical: 'h2{background:color-mix(in srgb,' + accent + ' 8%,transparent);border-left:3px solid ' + accent + ';padding:7px 12px;border-radius:0 4px 4px 0}',
        serif: 'body{font-family:"Noto Serif SC","Songti SC",SimSun,serif}',
        editorial: 'h1{text-align:center;border-bottom:none}h2{text-align:center;padding:7px 0;border-top:1px solid #e1e5ea;border-bottom:1px solid #e1e5ea}'
      };
      return css + variants[theme.variant] + (state.compact ? 'h2{margin:18px 0 7px}h3{margin:12px 0 6px}' : '');
    }
  };
  apply();
})();
