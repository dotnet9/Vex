/* 配色派生规则 · 共享模块（UMD：Node 生成器与浏览器自定义配色编辑共用）。
 * 公式与常数的唯一事实源是 CodeWF.Avalonia docs/配色与排版令牌规则.md 附录 A；
 * 本模块只实现公式引擎、"收敛步骤"与"原型变量映射层"，禁止在任何一侧另抄公式或常数。 */
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

  function alphaOf(formula) {
    var match = formula.match(/@([\d.]+)%$/);
    return match ? Number(match[1]) / 100 : 0.64;
  }

  function mixSpec(formula) {
    var match = formula.match(/^mix\(([a-z]+)->([a-z]+),\s*([\d.]+)%\)$/i);
    return match ? { from: match[1], to: match[2], weight: Number(match[3]) / 100 } : null;
  }

  /* 渐变全插值区间可读性（§3 行动渐变端点约束）：只检查采样点，避免只校验端点。 */
  function gradientReadable(text, start, end) {
    for (var t = 0; t <= 1.0001; t += 0.05) {
      if (contrast(text, mix(start, end, t)) < 4.5) return false;
    }
    return true;
  }

  /* mix 派生向起点收敛：权重每次减少 0.5%，直到文字在目标色（或整段渐变）上达到 4.5:1。 */
  function convergeMix(formula, base, text, sampleGradient) {
    var spec = mixSpec(formula);
    if (!spec) return resolveFormula(formula, base, function (key) { return base[key] || base.paper; });
    var from = colorOf(spec.from, base), to = colorOf(spec.to, base);
    var weight = spec.weight, value = mix(from, to, weight);
    function passes(candidate) {
      return sampleGradient ? gradientReadable(text, from, candidate) : contrast(text, candidate) >= 4.5;
    }
    while (!passes(value) && weight > 0.005) {
      weight -= 0.005;
      value = mix(from, to, weight);
    }
    return value;
  }

  /* muted：ink@64% 起，对 bg/surface/raised/hover 任一工作面不足 4.5 时按 1% 步进加深（规则 §3）。 */
  function mutedFor(base, constants) {
    var alpha = alphaOf(constants.muted);
    var muted = alphaOver(base.ink, alpha, base.paper);
    var bg = mix(base.paper, base.surface, 0.30);
    var raised = alphaOver(base.ink, alphaOf(constants.raised), base.paper);
    var hover = alphaOver(base.ink, alphaOf(constants.hover), base.surface);
    while (alpha < 0.90 && (contrast(muted, bg) < 4.5 || contrast(muted, base.surface) < 4.5 || contrast(muted, raised) < 4.5 || contrast(muted, hover) < 4.5)) {
      alpha += 0.01;
      muted = alphaOver(base.ink, alpha, base.paper);
    }
    return muted;
  }

  /* 强调软底（§3）：accent 作为文字落在 accentSoft 上不足 4.5 时，浅色态降低透明度、深色态提高（同状态软底约束）。 */
  function accentSoftFor(base, alpha, dark) {
    var a = alpha;
    var soft = alphaOver(base.accent, a, base.paper);
    while (contrast(base.accent, soft) < 4.5 && a > (dark ? 0.40 : 0.03)) {
      a += dark ? 0.01 : -0.01;
      soft = alphaOver(base.accent, a, base.paper);
    }
    return soft;
  }

  /* 状态软底（§3）：文字与软底不足 4.5 时，浅色态降低透明度、深色态提高透明度。 */
  function statusSoftFor(base, status, alpha, dark) {
    var a = alpha;
    var soft = alphaOver(status, a, base.paper);
    while (contrast(status, soft) < 4.5 && a > (dark ? 0.40 : 0.03)) {
      a += dark ? 0.01 : -0.01;
      soft = alphaOver(status, a, base.paper);
    }
    return soft;
  }

  /* 状态色可读性（§3/§5 状态四键）：作为文字在 paper 与 surface 上不足 4.5 时，
     浅色态向 ink、深色态向白按 1% 步进收敛；statusSoft 以收敛值派生。 */
  function statusColor(base, status, dark) {
    if (contrast(status, base.paper) >= 4.5 && contrast(status, base.surface) >= 4.5) return status;
    var target = dark ? '#FFFFFF' : base.ink;
    for (var step = 1; step <= 100; step++) {
      var candidate = mix(status, target, step / 100);
      if (contrast(candidate, base.paper) >= 4.5 && contrast(candidate, base.surface) >= 4.5) return candidate;
    }
    return target;
  }

  /* 把任意目标色向可读方向移动（Markdown 主题文字色校正用）：返回首个满足全部背景的插值。 */
  function readable(color, backgrounds, minimum) {
    minimum = minimum || 4.5;
    if (backgrounds.every(function (bg) { return contrast(color, bg) >= minimum; })) return color;
    var endpoint = luminance(backgrounds[0]) > 0.4 ? '#111820' : '#FFFFFF';
    for (var i = 1; i <= 100; i++) {
      var next = mix(color, endpoint, i / 100);
      if (backgrounds.every(function (bg) { return contrast(next, bg) >= minimum; })) return next;
    }
    return endpoint;
  }

  /* 原型变量映射层：规则语义键 → 原型 CSS 变量词汇表（§4 消费契约的原型投影）。 */
  function deriveVars(base, mode, constants) {
    var dark = mode === 'dark';
    var resolved = resolveConstants(constants, base);
    var on = onAccent(base.accent);
    /* §3 的四处收敛：muted 步进加深、primaryHover/gradientEnd 向 accent 收敛、强调软底与状态软底按明暗反向调整。 */
    resolved.muted = mutedFor(base, constants);
    resolved.accentSoft = accentSoftFor(base, alphaOf(constants.accentSoft), dark);
    resolved.primaryHover = convergeMix(constants.primaryHover, base, on, false);
    resolved.gradientEnd = convergeMix(constants.gradientEnd, base, on, true);
    var softAlpha = alphaOf(constants.statusSoft);
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
      var status = statusColor(base, base[key], dark);
      var soft = statusSoftFor(base, status, softAlpha, dark);
      vars[key] = status;
      vars[key + '-soft'] = soft;
      vars[key + '-hover-bg'] = soft;
      vars[key + '-hover-fg'] = status;
      vars[key + '-pressed-bg'] = statusSoftFor(base, status, softAlpha + 0.04, dark);
      vars[key + '-pressed-fg'] = status;
    });
    vars['status-error'] = vars.danger; vars['status-error-bg'] = vars['danger-soft'];
    vars['status-success'] = vars.success; vars['status-warning'] = vars.warning;
    return vars;
  }

  /* §5 校验 + §3 收敛结果复核：返回问题清单（空数组 = 通过）。 */
  function validate(base, mode, statusDefaults, derivation) {
    var issues = [];
    var dark = mode === 'dark';
    var defaults = (statusDefaults && statusDefaults[mode]) || {};
    var merged = {};
    STATUS_KEYS.forEach(function (key) { merged[key] = base[key] || defaults[key]; });
    merged.paper = base.paper; merged.surface = base.surface; merged.ink = base.ink; merged.accent = base.accent;
    function need(pair, minimum, label) {
      var ratio = contrast(pair[0], pair[1]);
      if (ratio < minimum) issues.push(label + ' 对比度 ' + ratio.toFixed(2) + ':1，需 ≥ ' + minimum + ':1');
    }
    need([merged.ink, merged.paper], 10, '正文 ink/paper');
    need([merged.accent, merged.paper], 4.5, 'accent 作为文字/paper');
    need([merged.accent, merged.surface], 4.5, 'accent 作为文字/surface');
    var on = onAccent(merged.accent);
    need([on, merged.accent], 4.5, 'onAccent/accent');
    STATUS_KEYS.forEach(function (key) {
      if (!merged[key]) return;
      var status = statusColor(merged, merged[key], dark);
      need([status, merged.paper], 4.5, key + '/paper');
      need([status, merged.surface], 4.5, key + '/surface');
    });
    if (derivation) {
      /* 收敛后的派生值复核：规则 §3 的收敛约束 + 软底可读性。 */
      var muted = mutedFor(merged, derivation);
      need([muted, merged.paper], 4.5, '次要文字 muted/paper');
      need([muted, mix(merged.paper, merged.surface, 0.30)], 4.5, '次要文字 muted/bg');
      need([muted, merged.surface], 4.5, '次要文字 muted/surface');
      need([muted, alphaOver(merged.ink, alphaOf(derivation.raised), merged.paper)], 4.5, '次要文字 muted/raised');
      need([muted, alphaOver(merged.ink, alphaOf(derivation.hover), merged.surface)], 4.5, '次要文字 muted/hover');
      need([merged.accent, accentSoftFor(merged, alphaOf(derivation.accentSoft), dark)], 4.5, '强调文字 accent/accentSoft');
      var hover = convergeMix(derivation.primaryHover, merged, on, false);
      need([on, hover], 4.5, 'onAccent/primaryHover');
      var pressed = resolveFormula(derivation.primaryPressed, merged, function (key) { return merged[key] || merged.paper; });
      need([on, pressed], 4.5, 'onAccent/primaryPressed');
      var end = convergeMix(derivation.gradientEnd, merged, on, true);
      var worst = 4.5;
      for (var t = 0; t <= 1.0001; t += 0.05) worst = Math.min(worst, contrast(on, mix(merged.accent, end, t)));
      if (worst < 4.5) issues.push('onAccent/行动渐变 最低 ' + worst.toFixed(2) + ':1，需 ≥ 4.5:1');
      var softAlpha = alphaOf(derivation.statusSoft);
      STATUS_KEYS.forEach(function (key) {
        if (!merged[key]) return;
        var status = statusColor(merged, merged[key], dark);
        need([status, statusSoftFor(merged, status, softAlpha, dark)], 4.5, key + '/soft');
      });
    } else {
      var mutedFallback = alphaOver(merged.ink, 0.64, merged.paper);
      need([mutedFallback, merged.paper], 4.5, '次要文字 muted/paper');
    }
    var paperLum = luminance(merged.paper);
    if (dark ? paperLum > 0.12 : paperLum < 0.90) issues.push('Paper 相对亮度 ' + paperLum.toFixed(3) + '，超出' + (dark ? '深色态 ≤ 0.12' : '浅色态 ≥ 0.90') + ' 区间');
    return issues;
  }

  return { rgb: rgb, hex: hex, mix: mix, alphaOver: alphaOver, luminance: luminance, contrast: contrast,
    onAccent: onAccent, resolveFormula: resolveFormula, readable: readable, statusColor: statusColor, deriveVars: deriveVars, validate: validate };
});
