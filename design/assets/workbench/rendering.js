/* 本地 KaTeX / mhchem / Mermaid：离线演示及源码修改后的重新渲染。 */
(function () {
  'use strict';
  function VexEsc(value) { return String(value).replace(/[&<>"']/g, function (char) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]; }); }
  function safeURL(value, image) {
    if (/^https?:\/\//i.test(value) || (!image && /^(mailto:|#)/i.test(value))) return value;
    if (image && /^(?:\.\/)?assets\/[\w./-]+$/i.test(value) && !value.split('/').includes('..')) return value;
    if (value === '../design/Third-Party-Licenses.html') return value;
    return '';
  }
  function math(source, display) {
    var raw = (display ? '$$\n' : '$') + source + (display ? '\n$$' : '$');
    if (window.VexPreferences && (!VexPreferences.values.math || (!VexPreferences.values.chemistry && /\\ce\{|\\pu\{/.test(source)))) return '<code>' + VexEsc(raw) + '</code>';
    try {
      return '<span class="md-math" contenteditable="false" data-markdown="' + VexEsc(raw) + '">' + katex.renderToString(source, { displayMode: display, throwOnError: true, trust: false, output: 'htmlAndMathml' }) + '</span>';
    } catch (_) {
      return '<code class="md-render-error" data-markdown="' + VexEsc(raw) + '">' + VexEsc(source) + '</code>';
    }
  }
  /* 行内 <span style>：只放行 color / background-color / font-size，其余属性丢弃（白名单外不渲染）。
     开/闭标签分别占位、闭标签弹最近的栈——嵌套 span 按真实层级输出，多余的闭标签忽略、未闭合的开标签在结尾补齐。 */
  function spanStyle(raw) {
    var out = [];
    String(raw || '').split(';').forEach(function (decl) {
      var at = decl.indexOf(':'); if (at < 0) return;
      var prop = decl.slice(0, at).trim().toLowerCase(), value = decl.slice(at + 1).trim();
      if (prop === 'font-size') { if (/^\d{1,3}(?:\.\d+)?px$/.test(value)) out.push(prop + ':' + value); return; }
      if ((prop === 'color' || prop === 'background-color') && /^(#[0-9a-fA-F]{3,8}|rgb\(\s*\d{1,3}\s*(?:,\s*\d{1,3}\s*){2}\)|[a-zA-Z]{3,24})$/.test(value)) out.push(prop + ':' + value);
    });
    return out.join(';');
  }
  function inline(value) {
    var tokens = [];
    var stack = [];
    var source = String(value).replace(/<span\s+style="([^"]*)"\s*>|<\/span\s*>/gi, function (raw, style) {
      if (style === undefined) { /* 闭标签：弹最近一个未闭合的开标签 */
        if (!stack.length) return '';
        stack.pop();
        tokens.push('</span>');
      } else {
        var css = spanStyle(style);
        if (!css) return ''; /* 白名单外：标签整个丢弃，内容照常排版 */
        stack.push(true);
        tokens.push('<span class="md-styled" style="' + VexEsc(css) + '">');
      }
      return '\u0001' + (tokens.length - 1) + '\u0002';
    }).replace(/\[\^([^\]\n]+)\]/g, function (raw, label) {
      tokens.push('<a class="md-footnote-ref" id="vex-footnote-ref-' + encodeURIComponent(label) + '" href="#vex-footnote-' + encodeURIComponent(label) + '"><sup>[' + VexEsc(label) + ']</sup></a>');
      return '\u0001' + (tokens.length - 1) + '\u0002';
    }).replace(/`([^`]+)`|\$([^$\n]+)\$|(!?)\[([^\]]*)\]\(([^\s)]+)\)/g, function (raw, code, formula, image, label, url) {
      var html;
      if (code !== undefined) html = '<code>' + VexEsc(code) + '</code>';
      else if (formula !== undefined) html = math(formula, false);
      else {
        var safe = safeURL(url, !!image);
        html = !safe ? VexEsc(raw) : image ? '<img src="' + VexEsc(safe) + '" alt="' + VexEsc(label) + '">' : '<a href="' + VexEsc(safe) + '" target="_blank" rel="noopener noreferrer">' + VexEsc(label) + '</a>';
      }
      tokens.push(html); return '\u0001' + (tokens.length - 1) + '\u0002';
    });
    var html = VexEsc(source).replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>').replace(/\*([^*]+)\*/g, '<em>$1</em>').replace(/~~([^~]+)~~/g, '<del>$1</del>');
    if (window.VexPreferences?.values.highlight !== false) html = html.replace(/==([^=]+)==/g,'<mark>$1</mark>');
    if (window.VexPreferences?.values.subscript !== false) html = html.replace(/\^([^\s^]+)\^/g,'<sup>$1</sup>').replace(/(?<!~)~([^\s~]+)~(?!~)/g,'<sub>$1</sub>');
    var restored = html.replace(/\u0001(\d+)\u0002/g, function (_, index) { return tokens[index]; });
    /* 未闭合的 span 一路包到段落末尾：补齐闭合标签，保持结构合法。 */
    return stack.length ? restored + '</span>'.repeat(stack.length) : restored;
  }
  var serial = 0;
  function diagrams() {
    document.querySelectorAll('.md-diagram:not([data-rendered])').forEach(function (element) {
      element.dataset.rendered = 'queued';
      VexDiagramRendering.schedule(element, async function () {
        if (!element.isConnected) return;
        element.dataset.rendered = 'pending';
        try {
          var svgText = await VexDiagramRendering.render('vex-diagram-' + (++serial), element.dataset.source);
          if (!element.isConnected) return;
          element.innerHTML = svgText; element.dataset.rendered = 'true';
          VexDiagramRendering.mount(element, element.dataset.source, element.dataset.label);
        } catch (_) {
          if (element.isConnected) { element.dataset.rendered = 'error'; element.innerHTML = '<p class="md-render-error">图表语法有误，请切换源码检查。</p><pre><code>' + VexEsc(element.dataset.source) + '</code></pre>'; }
        }
      });
    });
  }
  function special(block) { return block.kind === 'math' || (block.kind === 'code' && block.language.toLowerCase() === 'mermaid'); }
  function renderSpecial(block) {
    var attr = ' data-line="' + block.line + '"';
    if (block.kind === 'math') return '<div class="md-math-block"' + attr + '>' + math(block.text, true) + '</div>';
    if (window.VexPreferences?.values.mermaid === false) return '<pre' + attr + '><code>' + VexEsc(block.raw || block.text) + '</code></pre>';
    return '<div class="md-diagram"' + attr + ' data-source="' + VexEsc(block.text) + '" data-label="' + VexEsc(VexMermaid.label(block.text)) + '"><pre><code>' + VexEsc(block.text) + '</code></pre></div>';
  }
  window.VexRendering = { inline: inline, special: special, renderSpecial: renderSpecial, diagrams: diagrams };
})();
