/* Vex 原型使用与应用一致的八套配色和独立三态模式。 */
(function () {
  var palettes = [
    ['dotnet','.NET 紫','#512BD4'], ['aurora','AI 极光','#8968FF'],
    ['ember','AI 赤陶','#BD674D'], ['default','默认蓝','#0064FA'],
    ['aquatic','水生','#008D9E'], ['desert','沙漠','#B57532'],
    ['dusk','黄昏','#7A5CC4'], ['night-sky','夜空','#2B6CB0']
  ];
  var modes = [['system','跟随系统'],['light','浅色'],['dark','深色']];
  var storeKey = 'vex-design-appearance', system = matchMedia('(prefers-color-scheme: dark)');
  var state = { mode:'system', palette:'dotnet' };
  try { Object.assign(state, JSON.parse(localStorage.getItem(storeKey) || '{}')); } catch (_) {}
  var query = new URLSearchParams(location.search), theme = query.get('theme');
  if (modes.some(item => item[0] === theme)) state.mode = theme;
  else if (palettes.some(item => item[0] === theme)) state.palette = theme;
  if (query.has('palette')) state.palette = query.get('palette');
  function apply() {
    if (!modes.some(item => item[0] === state.mode)) state.mode = 'system';
    if (!palettes.some(item => item[0] === state.palette)) state.palette = 'dotnet';
    document.documentElement.dataset.themeMode = state.mode;
    document.documentElement.dataset.theme = state.mode === 'system' ? (system.matches ? 'dark' : 'light') : state.mode;
    document.documentElement.dataset.palette = state.palette;
    try { localStorage.setItem(storeKey, JSON.stringify(state)); } catch (_) {}
    document.querySelectorAll('#vex-theme-panel button').forEach(button => {
      var selected = button.dataset.mode ? button.dataset.mode === state.mode : button.dataset.palette === state.palette;
      button.classList.toggle('on', selected); button.setAttribute('aria-pressed', String(selected));
    });
    var fab = document.getElementById('vex-theme-fab');
    if (fab) fab.style.background = palettes.find(item => item[0] === state.palette)[2];
  }
  window.VexTheme = {
    setMode: function(mode) { state.mode = mode; apply(); },
    setPalette: function(palette) { state.palette = palette; apply(); }
  };
  apply();
  system.addEventListener('change', function() { if (state.mode === 'system') apply(); });
  window.addEventListener('storage', function(event) {
    if (event.key !== storeKey) return;
    try { Object.assign(state, JSON.parse(event.newValue || '{}')); apply(); } catch (_) {}
  });
  window.addEventListener('keydown', function(event) {
    var index = Number(event.key) - 1;
    if (event.altKey && !event.ctrlKey && index >= 0 && index < palettes.length) window.VexTheme.setPalette(palettes[index][0]);
  });
  document.addEventListener('DOMContentLoaded', function() {
    var style = document.createElement('style');
    style.textContent = '#vex-theme-fab{position:fixed;right:22px;bottom:22px;width:42px;height:42px;border-radius:50%;border:2px solid #fff8;z-index:999;cursor:pointer}' +
      '#vex-theme-panel{position:fixed;right:22px;bottom:74px;z-index:999;padding:10px;min-width:230px;background:var(--panel);border:1px solid var(--border);border-radius:10px;box-shadow:var(--shadow-pop)}' +
      '#vex-theme-panel[hidden]{display:none}.vtp-modes{display:flex;gap:4px;margin-bottom:8px}.vtp-item{display:flex;align-items:center;gap:8px;width:100%;text-align:left;border:0;border-radius:6px;padding:7px;background:transparent;color:var(--fg);cursor:pointer}.vtp-item:hover{background:var(--hover)}.vtp-item.on{background:var(--accent-soft);color:var(--accent)}.vtp-item i{width:13px;height:13px;border-radius:50%}.vtp-modes button{white-space:nowrap}';
    document.head.appendChild(style);
    var panel = document.createElement('div'); panel.id = 'vex-theme-panel'; panel.hidden = true;
    panel.innerHTML = '<div class="vtp-modes">' + modes.map(item => '<button class="vtp-item" data-mode="'+item[0]+'">'+item[1]+'</button>').join('')+'</div>' +
      palettes.map(item => '<button class="vtp-item" data-palette="'+item[0]+'"><i style="background:'+item[2]+'"></i>'+item[1]+'</button>').join('');
    var fab = document.createElement('button'); fab.id = 'vex-theme-fab'; fab.setAttribute('aria-label','切换主题');
    document.body.append(panel,fab);
    panel.addEventListener('click', function(event) {
      var button = event.target.closest('button'); if (!button) return;
      if (button.dataset.mode) window.VexTheme.setMode(button.dataset.mode);
      if (button.dataset.palette) window.VexTheme.setPalette(button.dataset.palette);
    });
    fab.onclick = function() { panel.hidden = !panel.hidden; };
    document.addEventListener('click', function(event) { if (!panel.contains(event.target) && event.target !== fab) panel.hidden = true; });
    apply();
  });
})();
