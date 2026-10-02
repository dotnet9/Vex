/* ============================================================
   Vex 界面原型 · 主题切换器
   - 在 <head> 同步加载：立即设置 data-theme，避免闪烁
   - DOMContentLoaded 后自动注入右下角悬浮切换面板
   - localStorage 持久化，多页面 / 多标签页同步
   - 快捷键：Alt + 1..6 切换主题
   ============================================================ */
(function () {
  var THEMES = [
    { key: 'light',     name: '浅色 · 极地蓝', dots: ['#FFFFFF', '#1677FF', '#FDFDFD'] },
    { key: 'dark',      name: '深色 · 石墨',   dots: ['#151B26', '#60A5FA', '#111827'] },
    { key: 'aquatic',   name: '水生 · 湖心青', dots: ['#FFFFFF', '#0EA5B7', '#F0FAFD'] },
    { key: 'desert',    name: '沙漠 · 暖阳橙', dots: ['#FFFFFF', '#C77B2A', '#FCF7F0'] },
    { key: 'dusk',      name: '黄昏 · 薄暮紫', dots: ['#FFFFFF', '#8A63D2', '#F5F7FB'] },
    { key: 'night-sky', name: '夜空 · 深海蓝', dots: ['#172033', '#7AA2FF', '#0D1320'] }
  ];
  var STORE_KEY = 'vex-design-theme';

  function current() {
    // URL 参数 ?theme=xxx 优先（便于分享与无头截图），其次 localStorage
    var fromUrl = new URLSearchParams(location.search).get('theme');
    if (fromUrl && THEMES.some(function (x) { return x.key === fromUrl; })) return fromUrl;
    var t = null;
    try { t = localStorage.getItem(STORE_KEY); } catch (e) { /* file:// 下个别浏览器禁用 */ }
    if (!t || !THEMES.some(function (x) { return x.key === t; })) t = 'light';
    return t;
  }

  function apply(key) {
    document.documentElement.setAttribute('data-theme', key);
    try { localStorage.setItem(STORE_KEY, key); } catch (e) { /* ignore */ }
    var panel = document.getElementById('vex-theme-panel');
    if (panel) {
      var rows = panel.querySelectorAll('.vtp-item');
      rows.forEach(function (r) {
        r.classList.toggle('on', r.getAttribute('data-key') === key);
      });
    }
    var fab = document.getElementById('vex-theme-fab');
    if (fab) {
      var theme = THEMES.filter(function (x) { return x.key === key; })[0];
      if (theme) fab.style.background = 'conic-gradient(from 210deg, ' + theme.dots[1] + ', ' + theme.dots[0] + ', ' + theme.dots[1] + ')';
    }
  }

  // 尽早执行，避免主题闪烁
  apply(current());

  window.addEventListener('storage', function (e) {
    if (e.key === STORE_KEY && e.newValue) apply(e.newValue);
  });

  window.addEventListener('keydown', function (e) {
    if (e.altKey && !e.ctrlKey && !e.metaKey) {
      var idx = parseInt(e.key, 10) - 1;
      if (idx >= 0 && idx < THEMES.length) {
        apply(THEMES[idx].key);
      }
    }
  });

  function inject() {
    if (document.getElementById('vex-theme-fab')) return;

    var style = document.createElement('style');
    style.textContent = [
      '#vex-theme-fab{position:fixed;right:22px;bottom:22px;z-index:999;width:42px;height:42px;border-radius:50%;',
      'border:2px solid rgba(255,255,255,.65);box-shadow:0 4px 14px rgba(0,0,0,.28);cursor:pointer;transition:transform .15s ease, box-shadow .15s ease;}',
      '#vex-theme-fab:hover{transform:scale(1.08);box-shadow:0 6px 20px rgba(0,0,0,.34);}',
      '#vex-theme-panel{position:fixed;right:22px;bottom:74px;z-index:999;min-width:218px;background:var(--panel);',
      'border:1px solid var(--border);border-radius:12px;box-shadow:var(--shadow-pop);padding:8px;display:none;}',
      '#vex-theme-panel.open{display:block;}',
      '.vtp-title{font-size:11px;color:var(--muted);padding:2px 10px 7px;letter-spacing:.4px;display:flex;justify-content:space-between;align-items:center;}',
      '.vtp-title .vtp-hint{font-size:10px;opacity:.8;}',
      '.vtp-item{display:flex;align-items:center;gap:9px;padding:7px 10px;border-radius:8px;cursor:pointer;font-size:12.5px;color:var(--fg);}',
      '.vtp-item:hover{background:var(--hover);}',
      '.vtp-item.on{background:var(--accent-soft);color:var(--accent);font-weight:600;}',
      '.vtp-dots{display:inline-flex;flex:none;}',
      '.vtp-dots i{width:13px;height:13px;border-radius:50%;border:1.5px solid var(--panel);margin-left:-4px;box-shadow:0 0 0 1px rgba(0,0,0,.10);}',
      '.vtp-dots i:first-child{margin-left:0;}',
      '.vtp-item .vtp-check{margin-left:auto;font-size:12px;font-weight:700;display:none;}',
      '.vtp-item.on .vtp-check{display:inline;}'
    ].join('');
    document.head.appendChild(style);

    var panel = document.createElement('div');
    panel.id = 'vex-theme-panel';
    var rows = THEMES.map(function (t, i) {
      return '<div class="vtp-item" data-key="' + t.key + '">' +
        '<span class="vtp-dots">' + t.dots.map(function (c) { return '<i style="background:' + c + '"></i>'; }).join('') + '</span>' +
        '<span>' + t.name + '</span>' +
        '<span class="vtp-check">✓</span>' +
        '<span class="kbd" style="margin-left:auto">' + (i + 1) + '</span>' +
        '</div>';
    }).join('');
    panel.innerHTML = '<div class="vtp-title"><span>主题色 · 与 App 同步</span><span class="vtp-hint">Alt+1..6</span></div>' + rows;

    var fab = document.createElement('button');
    fab.id = 'vex-theme-fab';
    fab.title = '切换主题色';
    fab.setAttribute('aria-label', '切换主题色');

    document.body.appendChild(panel);
    document.body.appendChild(fab);

    panel.querySelectorAll('.vtp-item').forEach(function (row) {
      row.addEventListener('click', function () {
        apply(row.getAttribute('data-key'));
      });
    });

    fab.addEventListener('click', function () {
      panel.classList.toggle('open');
    });

    document.addEventListener('click', function (e) {
      if (!panel.contains(e.target) && e.target !== fab) panel.classList.remove('open');
    });

    apply(current());
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', inject);
  } else {
    inject();
  }
})();
