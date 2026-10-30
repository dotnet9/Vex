/* Shared source-preserving code editing for the desktop and touch prototypes.
   代码块框架参考 StreamMark：头部左上角语言标签、右上角工具键，正文带轻量语法高亮。 */
(() => {
  'use strict';
  function parse(raw) {
    const open = /^( {0,3})(`{3,}|~{3,})([^\r\n]*)(\r\n|\n|\r|$)/.exec(raw);
    if (!open) return null;
    const content = raw.slice(open[0].length);
    const close = new RegExp('^ {0,3}' + open[2][0] + '{' + open[2].length + ',}[ \\t]*(?:\\r\\n|\\n|\\r)?$', 'm').exec(content);
    const body = content.slice(0, close ? close.index : content.length);
    const ending = /(?:\r\n|\n|\r)$/.exec(body)?.[0] || '';
    const language = /\S+/.exec(open[3]);
    return {
      language: language?.[0] || '', code: body.slice(0, body.length - ending.length),
      write(code, name) {
        name = name.replace(/[\s`]/g, '');
        if (code === this.code && name === this.language) return raw;
        const newline = open[4] || '\n';
        code = code.replace(/\r\n|\n|\r/g, newline);
        let size = open[2].length;
        const fenceLines = new RegExp('^ {0,3}(' + open[2][0] + '{3,})[ \\t]*$', 'gm');
        for (const match of code.replace(/\r\n|\r/g, '\n').matchAll(fenceLines)) size = Math.max(size, match[1].length + 1);
        const marker = open[2][0].repeat(size);
        const info = name === this.language ? open[3] : language ? open[3].slice(0, language.index) + name + open[3].slice(language.index + language[0].length) : open[3] + name;
        const closing = close ? content.slice(close.index).replace(/`{3,}|~{3,}/, marker) : '';
        return open[1] + marker + info + newline + code + (ending || (closing && code ? newline : '')) + closing;
      }
    };
  }
  const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
  /* 轻量高亮：注释/字符串/数字/关键字/类型五类，覆盖演示常用语言；其余语言原样输出。 */
  const LANG_KINDS = lang => {
    const name = String(lang || '').toLowerCase();
    if (/^(js|jsx|javascript|ts|tsx|typescript)$/.test(name)) return 'js';
    if (/^(python|py|python3)$/.test(name)) return 'py';
    if (/^(cs|csharp|c#)$/.test(name)) return 'cs';
    if (/^(cpp|c\+\+|cxx|c|java|go|golang|rust|rs|kotlin|kt|swift|scala|php)$/.test(name)) return 'clike';
    return null;
  };
  const KEYWORDS = {
    js: 'const|let|var|function|return|if|else|for|while|do|class|extends|new|await|async|import|from|export|default|try|catch|finally|throw|typeof|instanceof|this|super|null|undefined|true|false|of|in|switch|case|break|continue',
    py: 'def|return|if|elif|else|for|while|in|import|from|as|class|try|except|finally|with|lambda|yield|pass|raise|and|or|not|None|True|False|self|async|await|global|nonlocal|assert|del|is|break|continue',
    cs: 'public|private|protected|internal|static|async|await|class|struct|record|interface|enum|namespace|using|return|if|else|for|foreach|while|do|switch|case|var|new|override|virtual|abstract|sealed|readonly|get|set|try|catch|finally|throw|break|continue|lock|params|in|out|ref|null|true|false|this|base|yield|when|and|or|not',
    clike: 'int|float|double|char|bool|void|long|short|unsigned|signed|const|constexpr|static|struct|class|public|private|protected|namespace|using|template|typename|virtual|override|return|if|else|for|while|do|switch|case|break|continue|new|delete|nullptr|true|false|auto|enum|include|func|go|package|defer|chan|fn|pub|impl|trait|enum|match|unsafe|final|abstract|extends|implements',
  };
  function highlight(code, language) {
    const kind = LANG_KINDS(language);
    if (!kind) return esc(code);
    /* 注释：Python 用 #，其余用 // 与块注释（Python 的 // 是整除符，不算注释）。 */
    const comment = kind === 'py' ? '#[^\\n]*' : '\\/\\/[^\\n]*|\\/\\*[\\s\\S]*?\\*\\/';
    const pattern = new RegExp(
      '(' + comment + ')'
      + '|("(?:[^"\\\\\\n]|\\\\.)*"|\'(?:[^\'\\\\\\n]|\\\\.)*\'|`(?:[^`\\\\]|\\\\.)*`)'
      + '|(\\b0[xX][0-9a-fA-F]+\\b|\\b\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?[fFlLdDuU]?\\b)'
      + '|([A-Za-z_]\\w*)',
      'g');
    let html = '', cursor = 0, match;
    while ((match = pattern.exec(code))) {
      html += esc(code.slice(cursor, match.index));
      const raw = match[0], commentToken = match[1], str = match[2], num = match[3], word = match[4];
      if (commentToken) html += '<span class="ch-com">' + esc(raw) + '</span>';
      else if (str) html += '<span class="ch-str">' + esc(raw) + '</span>';
      else if (num) html += '<span class="ch-num">' + esc(raw) + '</span>';
      else if (word) {
        if (new RegExp('^(?:' + KEYWORDS[kind] + ')$').test(word)) html += '<span class="ch-kw">' + esc(raw) + '</span>';
        else if (/^[A-Z]/.test(word)) html += '<span class="ch-type">' + esc(raw) + '</span>';
        else html += esc(raw);
      }
      cursor = match.index + raw.length;
    }
    return html + esc(code.slice(cursor));
  }
  /* 头部框架：左侧语言标签（保留输入框以支持即时改语言），右侧下载/换行/复制三键。 */
  function header(language) {
    const row = document.createElement('div');
    row.className = 'md-code-header'; row.contentEditable = 'false';
    const input = document.createElement('input');
    input.className = 'md-code-language'; input.type = 'text'; input.value = language;
    input.placeholder = 'text'; input.setAttribute('aria-label', '代码语言'); input.spellcheck = false;
    input.autocomplete = 'off'; input.autocapitalize = 'off';
    input.addEventListener('input', () => { input.value = input.value.replace(/[\s`]/g, ''); });
    row.append(input);
    const tools = document.createElement('div');
    tools.className = 'md-code-tools'; tools.setAttribute('role', 'group'); tools.setAttribute('aria-label', '代码工具');
    const icon = path => '<svg class="md-code-tool-icon" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round">' + path + '</svg>';
    const button = (title, path, action) => {
      const b = document.createElement('button');
      b.type = 'button'; b.className = 'md-code-tool'; b.title = title; b.setAttribute('aria-label', title);
      b.innerHTML = icon(path);
      b.addEventListener('mousedown', event => event.preventDefault());
      b.addEventListener('click', () => {
        const pre = row.parentElement?.querySelector('pre');
        if (!pre) return;
        action(pre, input);
      });
      return b;
    };
    tools.append(
      button('下载代码', '<path d="M12 3v12"/><path d="M7 10l5 5 5-5"/><path d="M5 21h14"/>', (pre, input) => {
        const name = (input.value || 'code') + fileExt(input.value);
        const blob = new Blob([pre.textContent], { type: 'text/plain;charset=utf-8' });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob); link.download = name;
        document.body.append(link); link.click(); link.remove();
        setTimeout(() => URL.revokeObjectURL(link.href), 2000);
      }),
      button('切换换行', '<path d="M4 6h16"/><path d="M4 12h12a3 3 0 0 1 0 6h-4"/><path d="M12 15l-3 3 3 3"/>', pre => {
        const block = pre.closest('.md-block,.md-code-block');
        const on = pre.style.whiteSpace !== 'pre-wrap';
        block?.classList.toggle('code-wrap', on);
        pre.style.whiteSpace = on ? 'pre-wrap' : '';
      }),
      button('复制代码', '<rect x="9" y="9" width="12" height="12" rx="2"/><path d="M5 15V5a2 2 0 0 1 2-2h10"/>', async pre => {
        try { await navigator.clipboard.writeText(pre.textContent); } catch (_) { /* 剪贴板被拒时静默 */ }
      }),
    );
    row.append(tools);
    return row;
  }
  function fileExt(language) {
    const name = String(language || '').toLowerCase();
    if (name === 'json') return '.json';
    if (LANG_KINDS(name) === 'py') return '.py';
    if (LANG_KINDS(name) === 'cs') return '.cs';
    if (/^(cpp|c\+\+|cxx)$/.test(name)) return '.cpp';
    if (/^(js|javascript|ts|typescript|jsx|tsx)$/.test(name)) return name.startsWith('ts') ? '.ts' : '.js';
    if (/^(go|golang)$/.test(name)) return '.go';
    if (/^(rust|rs)$/.test(name)) return '.rs';
    if (/^(java|kotlin|kt|swift|scala|php|c)$/.test(name)) return '.' + name;
    return '.txt';
  }
  window.VexCodeFence = { parse, header, footer: header, highlight };
})();
