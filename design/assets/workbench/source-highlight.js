/* 源码视图上色层：镜像 textarea 的文本，只插入 span、不增删字符，与输入框逐字对齐。 */
(() => {
  'use strict';
  const span = (kind, text) => text ? '<span class="sh-' + kind + '">' + VexEsc(text) + '</span>' : '';
  const wrap = (kind, html) => '<span class="sh-' + kind + '">' + html + '</span>';
  const TOKEN = /(<span[^>\n]*>|<\/span\s*>|\*\*[^*\n]+\*\*|__[^_\n]+__|~~[^~\n]+~~|==[^=\n]+==|`[^`\n]+`|\*[^*\n]+\*|_[^_\n]+_|!?\[[^\]\n]*\](?:\([^)\n]*\))?|\[\^[^\]\n]*\])/g;
  const PAIRS = [['strong', '**', '**'], ['strong', '__', '__'], ['strike', '~~', '~~'], ['mark', '==', '=='], ['code', '`', '`'], ['em', '*', '*'], ['em', '_', '_']];
  const LINK = /^(!?)\[([^\]\n]*)\](?:\(([^)\n]*)\))?$/;

  function link(token) {
    const parts = LINK.exec(token);
    if (!parts) return VexEsc(token);
    const html = span('marker', parts[1] + '[') + span('link', parts[2]) + span('marker', ']');
    return token.endsWith(')') ? html + span('marker', '(') + span('url', parts[3] || '') + span('marker', ')') : html;
  }

  // 行内标记：先试行内 span 标签与链接/脚注，再试成对符；未识别的字符原样输出。
  function inline(text) {
    let html = '', cursor = 0, match;
    TOKEN.lastIndex = 0;
    while ((match = TOKEN.exec(text))) {
      html += VexEsc(text.slice(cursor, match.index));
      const token = match[0];
      if (/^<span[^>]*>$|^<\/span\s*>$/i.test(token)) html += span('marker', token);
      else if (/^!?\[/.test(token)) html += link(token);
      else {
        const pair = PAIRS.find(item => token.startsWith(item[1]) && token.endsWith(item[2]) && token.length > item[1].length + item[2].length);
        html += pair ? span('marker', pair[1]) + span(pair[0], token.slice(pair[1].length, token.length - pair[2].length)) + span('marker', pair[2]) : VexEsc(token);
      }
      cursor = match.index + token.length;
    }
    return html + VexEsc(text.slice(cursor));
  }

  function line(text, state) {
    if (state.fence) {
      if (new RegExp('^\\s{0,3}' + state.fence + '{3,}\\s*$').test(text)) { state.fence = null; return span('marker', text); }
      return span('code', text);
    }
    const fence = /^\s{0,3}(`{3,}|~{3,})(.*)$/.exec(text);
    if (fence) { state.fence = fence[1][0]; return span('marker', text.slice(0, fence[1].length)) + span('lang', fence[2]); }
    if (/^\s{0,3}\$\$/.test(text)) { state.math = !state.math; return span('marker', text); }
    if (state.math) return span('math', text);
    if (/^\s{0,3}([-*_])\s*(?:\1\s*){2,}$/.test(text)) return span('marker', text);
    const heading = /^(\s{0,3})(#{1,6})(.*)$/.exec(text);
    if (heading) return heading[1] + span('marker', heading[2]) + wrap('heading', inline(heading[3]));
    const quote = /^(\s*>+\s?)(.*)$/.exec(text);
    if (quote) return span('marker', quote[1]) + wrap('quote', inline(quote[2]));
    const list = /^(\s*)([-+*]|\d{1,9}[.)])(\s+)(\[[ xX]\]\s*)?(.*)$/.exec(text);
    if (list) return list[1] + span('marker', list[2]) + list[3] + (list[4] ? span('marker', list[4]) : '') + inline(list[5]);
    if (/^\s*\|/.test(text) && /^[\s|:-]+$/.test(text)) return span('marker', text);
    if (text.includes('|')) return text.split(/(\|)/g).map(part => part === '|' ? span('marker', part) : inline(part)).join('');
    return inline(text);
  }

  function render(source) {
    const state = { fence: null, math: false };
    return source.split('\n').map(text => line(text, state)).join('\n');
  }

  function attach(host, source) {
    if (!host || !source) return { update: function () {} };
    /* 当前行：滚动容器里的绝对定位色条（用规则生成的 --currentline-bg 令牌），
       行号同步点亮；横向滚动跟随 textarea 偏移，镜像层保持逐字对齐不受影响。 */
    var currentBar = null;
    function syncCurrent() {
      var scroller = host.parentElement;
      if (!source.value.length || !source.getClientRects().length) {
        if (currentBar) currentBar.hidden = true;
        return;
      }
      if (!currentBar) {
        currentBar = document.createElement('div');
        currentBar.className = 'source-currentline';
        currentBar.setAttribute('aria-hidden', 'true');
        scroller.insertBefore(currentBar, scroller.firstChild);
      }
      var line = source.value.slice(0, source.selectionStart).split('\n').length - 1;
      var style = getComputedStyle(source);
      var row = parseFloat(style.lineHeight) || parseFloat(style.fontSize) * 1.75;
      currentBar.hidden = false;
      currentBar.style.top = (parseFloat(style.paddingTop) + line * row) + 'px';
      currentBar.style.height = row + 'px';
      currentBar.style.transform = 'translateX(' + (-source.scrollLeft) + 'px)';
      var numbers = scroller.querySelector('.line-numbers');
      if (numbers && numbers.children.length) {
        var previous = numbers.querySelector('.current');
        if (previous) previous.classList.remove('current');
        var marker = numbers.children[Math.min(line, numbers.children.length - 1)];
        if (marker) marker.classList.add('current');
      }
    }
    function update() {
      try {
        host.innerHTML = render(source.value);
        host.parentElement.classList.add('source-highlighted');
      } catch (_) {
        // 上色失败就退回纯文本：正文可见性优先于高亮。
        host.innerHTML = '';
        host.parentElement.classList.remove('source-highlighted');
      }
      syncCurrent();
    }
    source.addEventListener('input', update);
    // 长行时输入框可能横向滚动，镜像层跟随同一偏移，避免文字与颜色错位。
    source.addEventListener('scroll', function () { host.scrollLeft = source.scrollLeft; syncCurrent(); });
    // 光标移动不只发生在输入：方向键/点击（keyup、click）与程序化选区（selectionchange）都要刷新当前行。
    ['keyup', 'click'].forEach(function (type) { source.addEventListener(type, syncCurrent); });
    document.addEventListener('selectionchange', function () { if (document.activeElement === source) syncCurrent(); });
    window.addEventListener('vex-content-changed', update);
    update();
    return { update: update };
  }

  window.VexSourceHighlight = { render: render, attach: attach };
})();
