/* Shared document typography; compact spacing is independent. */
(function () {
  'use strict';
  var items = VexTypes;
  var state = { key: 'Simple', compact: false };
  try {
    var saved = JSON.parse(VexStorage.getItem('vex-design-typography') || '{}');
    if (items.some(function (item) { return item.key === VexTypeKey(saved.key); })) state.key = VexTypeKey(saved.key);
    if (typeof saved.compact === 'boolean') state.compact = saved.compact;
  } catch (_) {}
  var requested = new URLSearchParams(location.search).get('typography');requested=VexTypeKey(requested);
  if (items.some(function (item) { return item.key === requested; })) state.key = requested;
  var panel = document.createElement('section'); panel.id = 'typography-panel'; panel.className = 'typography-popover'; panel.hidden = true;
  panel.setAttribute('aria-labelledby', 'typography-title'); panel.setAttribute('role', 'dialog');
  panel.innerHTML = '<div class="popover-title"><strong id="typography-title">正文排版</strong><button class="icon-button" id="typography-close" aria-label="关闭排版"><svg class="ico" aria-hidden="true"><use href="#i-x"/></svg></button></div><p class="typography-note">每张卡片都是独立效果预览，点击应用。方向键浏览，Enter 确认。</p><div class="choice-filter" role="group" aria-label="排版范围"><button type="button" data-type-scope="recommended" aria-pressed="true">推荐</button><button type="button" data-type-scope="all" aria-pressed="false">全部 27 套</button></div><div class="typography-grid" role="group" aria-label="排版效果预览">' + items.map(function (item) {
    return '<button type="button" class="typography-card" data-typography-key="' + item.key + '" aria-label="' + item.label + '，' + item.note + '，应用此正文排版"><span class="typography-card-heading"><strong class="typography-card-name">' + item.label + '</strong><span class="typography-card-selected" aria-hidden="true">✓ 当前</span></span><span class="typography-card-purpose">' + item.note + '</span>' + VexTypeCardPreview(item.key) + '</button>';
  }).join('') + '</div><label class="typography-compact">紧凑布局<input type="checkbox" id="typography-compact"></label>';
  document.querySelector('.window-body').appendChild(panel);
  var scope = 'recommended';
  function filter() {
    panel.querySelectorAll('[data-typography-key]').forEach(function(button){
      var item=items.find(function(item){return item.key===button.dataset.typographyKey;});
      button.hidden=scope==='recommended'&&!item.recommended&&item.key!==state.key;
    });
    panel.querySelectorAll('[data-type-scope]').forEach(function(button){button.setAttribute('aria-pressed',String(button.dataset.typeScope===scope));});
  }
  panel.querySelector('.choice-filter').onclick=function(event){var button=event.target.closest('[data-type-scope]');if(!button)return;scope=button.dataset.typeScope;filter();panel.querySelector('.typography-grid').scrollTop=0;};
  function apply() {
    var theme = items.find(function (item) { return item.key === state.key; }), root = document.documentElement;
    root.dataset.typography = theme.key; root.dataset.typographyVariant = theme.variant; root.dataset.compact = String(state.compact);
    root.style.setProperty('--typography-accent', 'var(--accent)');
    document.querySelectorAll('[data-typography-label]').forEach(function (label) { label.textContent = theme.label; });
    panel.querySelectorAll('[data-typography-key]').forEach(function (button) {
      button.setAttribute('aria-pressed', String(button.dataset.typographyKey === state.key));
    });
    document.getElementById('typography-compact').checked = state.compact;
    filter();
    try { VexStorage.setItem('vex-design-typography', JSON.stringify(state)); } catch (_) {}
  }
  panel.querySelector('.typography-grid').onclick = function (event) {
    var button = event.target.closest('[data-typography-key]'); if (button) choose(button.dataset.typographyKey);
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
  function close(restoreFocus) { var wasOpen = !panel.hidden; panel.hidden = true; document.querySelectorAll('[data-action="typography"]').forEach(function (button) { button.setAttribute('aria-expanded', 'false'); }); if (restoreFocus && wasOpen && trigger) trigger.focus({preventScroll:true}); }
  document.getElementById('typography-close').onclick = function () { close(true); };
  document.getElementById('typography-compact').onchange = function () { state.compact = this.checked; apply(); };
  function choose(key) { key=VexTypeKey(key);if (items.some(function (item) { return item.key === key; })) { state.key = key; apply(); } }
  document.addEventListener('click', function (event) {
    if (!panel.contains(event.target) && !event.target.closest('[data-action="typography"]') && !event.target.closest('.menu-flyout')) close();
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
  apply();
})();
