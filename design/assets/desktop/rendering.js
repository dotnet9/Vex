/* 本地 KaTeX / mhchem / Mermaid：离线演示及源码修改后的重新渲染。 */
(function () {
  'use strict';
  function escape(value) { return String(value).replace(/[&<>"']/g, function (char) { return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[char]; }); }
  function safeURL(value, image) {
    if (/^https?:\/\//i.test(value) || (!image && /^(mailto:|#)/i.test(value))) return value;
    if (image && /^(?:\.\/)?assets\/[\w./-]+$/i.test(value) && !value.split('/').includes('..')) return value;
    if (value === '../design/Third-Party-Licenses.html') return value;
    return '';
  }
  function math(source, display) {
    var raw = (display ? '$$\n' : '$') + source + (display ? '\n$$' : '$');
    if (window.VexPreferences && (!VexPreferences.values.math || (!VexPreferences.values.chemistry && /\\ce\{|\\pu\{/.test(source)))) return '<code>' + escape(raw) + '</code>';
    try {
      return '<span class="md-math" contenteditable="false" data-markdown="' + escape(raw) + '">' + katex.renderToString(source, { displayMode: display, throwOnError: true, trust: false, output: 'htmlAndMathml' }) + '</span>';
    } catch (_) {
      return '<code class="md-render-error" data-markdown="' + escape(raw) + '">' + escape(source) + '</code>';
    }
  }
  function inline(value) {
    var tokens = [];
    var source = String(value).replace(/\[\^([^\]\n]+)\]/g, function (raw, label) {
      tokens.push('<a class="md-footnote-ref" id="vex-footnote-ref-' + encodeURIComponent(label) + '" href="#vex-footnote-' + encodeURIComponent(label) + '"><sup>[' + escape(label) + ']</sup></a>');
      return '\u0001' + (tokens.length - 1) + '\u0002';
    }).replace(/`([^`]+)`|\$([^$\n]+)\$|(!?)\[([^\]]*)\]\(([^\s)]+)\)/g, function (raw, code, formula, image, label, url) {
      var html;
      if (code !== undefined) html = '<code>' + escape(code) + '</code>';
      else if (formula !== undefined) html = math(formula, false);
      else {
        var safe = safeURL(url, !!image);
        html = !safe ? escape(raw) : image ? '<img src="' + escape(safe) + '" alt="' + escape(label) + '">' : '<a href="' + escape(safe) + '" target="_blank" rel="noopener noreferrer">' + escape(label) + '</a>';
      }
      tokens.push(html); return '\u0001' + (tokens.length - 1) + '\u0002';
    });
    var html = escape(source).replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>').replace(/\*([^*]+)\*/g, '<em>$1</em>').replace(/~~([^~]+)~~/g, '<del>$1</del>');
    if (window.VexPreferences?.values.highlight !== false) html = html.replace(/==([^=]+)==/g,'<mark>$1</mark>');
    if (window.VexPreferences?.values.subscript !== false) html = html.replace(/\^([^\s^]+)\^/g,'<sup>$1</sup>').replace(/(?<!~)~([^\s~]+)~(?!~)/g,'<sub>$1</sub>');
    return html
      .replace(/\u0001(\d+)\u0002/g, function (_, index) { return tokens[index]; });
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
          if (element.isConnected) { element.dataset.rendered = 'error'; element.innerHTML = '<p class="md-render-error">图表语法有误，请切换源码检查。</p><pre><code>' + escape(element.dataset.source) + '</code></pre>'; }
        }
      });
    });
  }
  function special(block) { return block.kind === 'math' || (block.kind === 'code' && block.language.toLowerCase() === 'mermaid'); }
  function renderSpecial(block) {
    var attr = ' data-line="' + block.line + '"';
    if (block.kind === 'math') return '<div class="md-math-block"' + attr + '>' + math(block.text, true) + '</div>';
    if (window.VexPreferences?.values.mermaid === false) return '<pre' + attr + '><code>' + escape(block.raw || block.text) + '</code></pre>';
    return '<div class="md-diagram"' + attr + ' data-source="' + escape(block.text) + '" data-label="' + escape(VexMermaid.label(block.text)) + '"><pre><code>' + escape(block.text) + '</code></pre></div>';
  }
  window.VexRendering = { inline: inline, special: special, renderSpecial: renderSpecial, diagrams: diagrams };
})();
