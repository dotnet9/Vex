/* One set of document rules drives reading, thumbnails and HTML export. */
(function () {
  'use strict';
  var serif = '"Noto Serif SC","Songti SC",SimSun,serif';
  window.VexTypes = [
    { key:'Simple', label:'简约', en:'Minimal', variant:'simple', light:'#343B43', dark:'#DFE3E8',
      rules:'BODY{--type-size:16px;--type-leading:1.9}BODY h1{font-weight:650}BODY h2,BODY h3{color:var(--fg)}' },
    { key:'Basic', label:'文档', en:'Document', variant:'document', light:'#376C9A', dark:'#97BDE0',
      rules:'BODY{--type-size:15px;--type-leading:1.85}BODY h1{border-bottom:1px solid var(--border)}BODY h2{border-bottom:1px solid var(--border);padding-bottom:.45em}BODY h3{color:var(--fg)}' },
    { key:'InkBlack', label:'书卷', en:'Book', variant:'book', light:'#70604F', dark:'#D3BC9A',
      rules:'BODY{--type-size:17px;--type-leading:2;font-family:'+serif+'}BODY h1{font-size:2.15em;font-weight:600}BODY h2{font-size:1.45em;font-weight:600;color:var(--fg);margin-top:1.6em}BODY h3{color:var(--fg);font-weight:600}BODY p{text-align:justify;text-indent:2em}BODY blockquote{border-left:1px solid var(--type-decoration);background:transparent;padding:0 1.2em}' },
    { key:'TechnologyBlue', label:'技术', en:'Technical', variant:'technical', light:'#376C9A', dark:'#97BDE0',
      rules:'BODY{--type-size:15px;--type-leading:1.85}BODY h1{font-size:1.9em;border-bottom:1px solid var(--border)}BODY h2{font-size:1.3em;background:color-mix(in srgb,var(--typography-accent) 9%,transparent);border-left:3px solid var(--type-decoration);padding:.45em .7em;border-radius:0 4px 4px 0}BODY h3{font-size:1.12em}BODY pre{border:1px solid var(--border);border-radius:6px}BODY code{font-variant-ligatures:none}' },
    { key:'WeChatFormat', label:'刊物', en:'Editorial', variant:'editorial', light:'#24766B', dark:'#8AC8B3',
      rules:'BODY{--type-size:16px;--type-leading:2}BODY h1{text-align:center;font-size:2em;margin-bottom:.7em}BODY h2{text-align:center;font-size:1.25em;border-top:1px solid var(--type-decoration);border-bottom:1px solid var(--type-decoration);padding:.35em 0}BODY h3{text-align:center;font-size:1.1em;color:var(--fg)}BODY h4{text-align:center}BODY p{text-align:justify}BODY blockquote{border-left:0;text-align:center;padding:.8em 1.3em}' },
    { key:'SoftSage', label:'鼠尾草', en:'Soft Sage', variant:'sage', light:'#789889', dark:'#8FB3A3',
      rules:'BODY{--type-size:15px;--type-leading:1.85}BODY h1{text-align:center;font-size:1.9em;font-weight:700;border-bottom:1.5px solid color-mix(in srgb,var(--typography-accent) 35%,transparent);padding-bottom:.5em}BODY h2{text-align:center;font-size:1.35em;font-weight:650;background:color-mix(in srgb,var(--typography-accent) 10%,transparent);border-radius:6px;padding:.5em 0}BODY h3{font-size:1.12em;color:var(--fg);font-weight:650}BODY blockquote{border-left:3px solid var(--typography-accent);background:color-mix(in srgb,var(--typography-accent) 8%,transparent);border-radius:0 8px 8px 0;color:var(--fg);padding:.7em 1.1em}BODY th{background:color-mix(in srgb,var(--typography-accent) 9%,transparent)}BODY td{border-color:color-mix(in srgb,var(--typography-accent) 14%,transparent)}' }
  ];
  window.VexTypeKey = function (key) { return key; };
  var base = [
    'BODY{font-family:var(--font,system-ui),sans-serif;font-size:calc(var(--type-size,16px) * var(--vex-writing-scale,1));line-height:var(--type-leading,1.9);letter-spacing:0;color:var(--fg);margin-left:auto;margin-right:auto}',
    'BODY h1{font-size:2em;line-height:1.45;letter-spacing:0;margin:0 0 .55em;padding:0;border:0;background:none;border-radius:0;color:var(--fg);font-weight:700;text-align:left}',
    'BODY h2{font-size:1.4em;line-height:1.6;letter-spacing:0;margin:1.4em 0 .45em;padding:0;border:0;background:none;border-radius:0;color:var(--typography-accent);font-weight:650;text-align:left}',
    'BODY h3{font-size:1.15em;line-height:1.65;letter-spacing:0;margin:1.15em 0 .35em;padding:0;border:0;background:none;color:var(--typography-accent);font-weight:600;text-align:left}',
    'BODY h4{font-size:1em;line-height:1.7;letter-spacing:0;margin:1em 0 .3em;color:var(--fg);font-weight:600;text-align:left}',
    'BODY p{margin:0 0 .85em;text-indent:0}',
    'BODY .instant-block:has(+.instant-block>:is(h1,h2,h3,h4))>:last-child{margin-bottom:0}',
    'BODY blockquote{margin:1.1em 0;border:0;border-left:3px solid var(--type-decoration);padding:.6em 1em;background:color-mix(in srgb,var(--typography-accent) 6%,transparent);color:var(--muted)}BODY blockquote p{margin:0;text-indent:0}'
  ].join('');
  window.VexTypeRules = function (key, compact) {
    var item = VexTypes.find(function (t) { return t.key === key; }) || VexTypes[0];
    return base + item.rules + (compact ? 'BODY{font-size:calc(var(--type-size,16px) * var(--vex-writing-scale,1) * .93);line-height:1.65}BODY h2{margin-top:1.3em;margin-bottom:.45em}BODY h3{margin-top:1em;margin-bottom:.35em}' : '');
  };
  window.VexTypeExport = function (key, compact) {
    var item = VexTypes.find(function (t) { return t.key === key; }) || VexTypes[0];
    return VexTypeRules(key, compact).replaceAll('BODY', 'body') + 'body{--document-bg:#fff;--fg:#30343b;--muted:#606770;--border:#dde1e5;--typography-accent:'+item.light+'}';
  };
})();
