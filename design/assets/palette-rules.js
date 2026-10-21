/* 配色派生规则 · 共享模块（UMD：Node 生成器与浏览器自定义配色编辑共用）。
 * 公式与常数的唯一事实源是 CodeWF.Avalonia docs/配色与排版令牌规则.md 附录 A；
 * 本模块只实现公式引擎与"原型变量映射层"，禁止在任何一侧另抄公式或常数。 */
(function (root, factory) {
  if (typeof module === 'object' && module.exports) module.exports = factory();
  else root.VexPaletteRules = factory();
})(typeof self !== 'undefined' ? self : this, function () {
  'use strict';

  function rgb(color) {
    return color.replace('#', '').match(/../g).map(function (part) { return parseInt(part, 16); });
  }
  function hex(values) {
    return '#' + values.map(function (v) { return Math.round(Math.min(255, Math.max(0, v))).toString(16).padStart(2, '0'); }).join('');
  }
  function mix(a, b, weight) {
    const from = rgb(a), to = rgb(b);
    return hex(from.map(function (v, i) { return v * (1 - weight) + to[i] * weight; }));
  }
  /* alphaOver(fg, α, base)：把透明度烘焙到已知底色上，输出不透明色（规则 §3 烘焙原则）。 */
  function alphaOver(color, alpha, base) { return mix(base, color, alpha); }
  function luminance(color) {
    return rgb(color).map(function (v) { v /= 255; return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); })
      .reduce(function (sum, v, i) { return sum + v * [0.2126, 0.7152, 0.0722][i]; }, 0);
  }
  function contrast(a, b) {
    const la = luminance(a), lb = luminance(b);
    return (Math.max(la, lb) + 0.05) / (Math.min(la, lb) + 0.05);
  }
  /* onAccent 自动判定（§3）：白字与黑字取对比度更高者（深色态 ink 为亮色，不可作 accent 上文字）。 */
  function onAccent(accent) {
    return contrast('#FFFFFF', accent) > contrast('#000000', accent) ? '#FFFFFF' : '#000000';
  }

  var KEYWORDS = { white: '#FFFFFF', black: '#000000' };
  function colorOf(key, base) { return KEYWORDS[key] || base[key]; }

  /* 解析附录公式串："ink@64%"、"accent@12%"、"black@40%"、"mix(accent->paper,8%)"。 */
  function resolveFormula(formula, base, backdrop) {
    var alpha = formula.match(/^([a-z]+)@([\d.]+)%$/i);
    if (alpha) {
      var key = alpha[1], value = Number(alpha[2]) / 100;
      if (key === 'black') return 'rgba(0,0,0,' + value + ')'; /* scrim 保持半透明（遮罩必须透出下层） */
      return alphaOver(colorOf(key, base), value, backdrop(key, base));
    }
    var mixed = formula.match(/^mix\(([a-z]+)->([a-z]+),\s*([\d.]+)%\)$/i);
    if (mixed) return mix(colorOf(mixed[1], base), colorOf(mixed[2], base), Number(mixed[3]) / 100);
    return colorOf(formula, base);
  }

  /* 派生键的回底规则：中性派生落在 paper，hover 落在 surface（§3 表格的"→ 底色"列）。 */
  function backdrop(key) {
    return key === 'hover' ? 'surface' : 'paper';
  }

  /* 把一套常数（derivation[mode]）解析成具体色值；statusSoft 是四状态共用的模板公式，由 deriveVars 单独解析。 */
  function resolveConstants(constants, base) {
    var resolved = {};
    Object.keys(constants).forEach(function (key) {
      if (key === 'statusSoft') return;
      resolved[key] = resolveFormula(constants[key], base, function (backdropKey) { return base[backdrop(backdropKey)]; });
    });
    return resolved;
  }

  var STATUS_KEYS = ['danger', 'success', 'warning', 'info'];

  /* 原型变量映射层：规则语义键 → 原型 CSS 变量词汇表（§4 消费契约的原型投影）。 */
  function deriveVars(base, mode, constants) {
    var dark = mode === 'dark';
    var resolved = resolveConstants(constants, base);
    var on = onAccent(base.accent, base.ink);
    var softAlpha = Number(String(constants.statusSoft).match(/@([\d.]+)%/)[1]) / 100;
    var vars = {
      stage: alphaOver(base.ink, dark ? 0.10 : 0.08, base.paper),
      bg: mix(base.paper, base.surface, 0.30),
      panel: base.surface, sidebar: base.surface, splitter: base.surface,
      'statusbar-bg': base.surface, 'badge-bg': base.surface,
      surface: base.paper, paper: base.paper, field: base.paper, 'editor-bg': base.paper,
      fg: base.ink, value: base.ink, 'editor-fg': base.ink,
      muted: resolved.muted, subtle: resolved.muted, 'md-marker': resolved.muted, 'md-quote': resolved.muted,
      'fg-disabled': resolved.disabledText,
      border: resolved.border, line: resolved.border, 'badge-border': resolved.border, 'currentline-border': resolved.border,
      'control-border': resolved.controlBorder,
      hover: resolved.hover, raised: resolved.raised, 'currentline-bg': resolved.raised,
      pressed: resolved.selected, selected: resolved.selected, 'accent-soft': resolved.accentSoft,
      selection: resolved.selection,
      'code-bg': base.surface, code: base.surface,
      accent: base.accent, 'accent-decoration': base.accent,
      'accent-fg': on, 'on-accent': on, 'accent-hover': resolved.primaryHover,
      primary: base.accent, 'on-primary': on,
      'primary-hover': resolved.primaryHover, 'primary-pressed': resolved.primaryPressed,
      'focus-ring': base.accent, 'md-code-fg': base.accent,
      'tooltip-bg': base.ink, 'tooltip-fg': base.paper,
      scrim: resolved.scrim,
      'gradient-end': resolved.gradientEnd,
      'title-grad': 'linear-gradient(135deg,' + base.accent + ',' + resolved.gradientEnd + ')'
    };
    STATUS_KEYS.forEach(function (key) {
      if (!base[key]) return;
      var soft = alphaOver(base[key], softAlpha, base.paper);
      vars[key] = base[key];
      vars[key + '-soft'] = soft;
      vars[key + '-hover-bg'] = soft;
      vars[key + '-hover-fg'] = base[key];
      vars[key + '-pressed-bg'] = alphaOver(base[key], softAlpha + 0.04, base.paper);
      vars[key + '-pressed-fg'] = base[key];
    });
    vars['status-error'] = vars.danger; vars['status-error-bg'] = vars['danger-soft'];
    vars['status-success'] = vars.success; vars['status-warning'] = vars.warning;
    return vars;
  }

  /* §5 校验：返回问题清单（空数组 = 通过）。 */
  function validate(base, mode, statusDefaults) {
    var issues = [];
    var dark = mode === 'dark';
    var merged = {};
    STATUS_KEYS.forEach(function (key) { merged[key] = base[key] || (statusDefaults && statusDefaults[mode] && statusDefaults[mode][key]); });
    merged.paper = base.paper; merged.ink = base.ink; merged.accent = base.accent;
    var muted = alphaOver(base.ink, 0.64, base.paper);
    function need(pair, minimum, label) {
      var ratio = contrast(pair[0], pair[1]);
      if (ratio < minimum) issues.push(label + ' 对比度 ' + ratio.toFixed(2) + ':1，需 ≥ ' + minimum + ':1');
    }
    need([merged.ink, merged.paper], 10, '正文 ink/paper');
    need([muted, merged.paper], 4.5, '次要文字 muted/paper');
    need([merged.accent, merged.paper], 4.5, 'accent 作为文字/paper');
    need([merged.accent, base.surface], 4.5, 'accent 作为文字/surface');
    var on = onAccent(merged.accent, merged.ink);
    need([on, merged.accent], 4.5, 'onAccent/accent');
    STATUS_KEYS.forEach(function (key) { if (merged[key]) need([merged[key], merged.paper], 4.5, key + '/paper'); });
    var paperLum = luminance(merged.paper);
    if (dark ? paperLum > 0.12 : paperLum < 0.90) issues.push('Paper 相对亮度 ' + paperLum.toFixed(3) + '，超出' + (dark ? '深色态 ≤ 0.12' : '浅色态 ≥ 0.90') + ' 区间');
    return issues;
  }

  return { rgb: rgb, hex: hex, mix: mix, alphaOver: alphaOver, luminance: luminance, contrast: contrast,
    onAccent: onAccent, resolveFormula: resolveFormula, deriveVars: deriveVars, validate: validate };
});
