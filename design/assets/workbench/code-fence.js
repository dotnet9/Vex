/* Shared source-preserving code editing for the desktop and touch prototypes. */
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
  function footer(language) {
    const row = document.createElement('div');
    row.className = 'md-code-footer'; row.contentEditable = 'false';
    const input = document.createElement('input');
    input.className = 'md-code-language'; input.type = 'text'; input.value = language;
    input.placeholder = 'text'; input.setAttribute('aria-label', '代码语言'); input.spellcheck = false;
    input.autocomplete = 'off'; input.autocapitalize = 'off';
    input.addEventListener('input', () => { input.value = input.value.replace(/[\s`]/g, ''); });
    row.append(input); return row;
  }
  window.VexCodeFence = { parse, footer };
})();
