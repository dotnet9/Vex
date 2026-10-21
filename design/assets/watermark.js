/* Watermarks never become editable document content or Markdown source. */
(() => {
  'use strict';
  const ns = 'http://www.w3.org/2000/svg';
  let values = {};
  let activeNow = false;
  function settings(input = values) {
    return {text:Array.from(String(input.watermarkText ?? '维刻 Vex')).slice(0,60).join(''),
      opacity:Math.max(0,Math.min(50,Number(input.watermarkOpacity) || 0)) / 100,
      fontSize:Math.max(12,Math.min(36,Number(input.watermarkFontSize) || 21)),
      weight:Number(input.watermarkWeight) || 400,
      color:String(input.watermarkColor || '#24766B')};
  }
  /* 深色态前景自动提亮（向白混 45%），保证用户自选前景色在两种明暗下都可读。 */
  function lighten(hex) {
    const n = hex.replace('#','').match(/../g)?.map(x => parseInt(x,16)) ?? [36,118,107];
    return '#' + n.map(v => Math.round(v + (255 - v) * .45).toString(16).padStart(2,'0')).join('');
  }
  function svg(input, color = '#24766b', full = false) {
    const config = settings(input), root = document.createElementNS(ns,'svg');
    root.setAttribute('xmlns',ns); root.setAttribute('width',full ? '100%' : '280'); root.setAttribute('height',full ? '100%' : '180');
    const text = document.createElementNS(ns,'text');
    for (const [key,value] of Object.entries({x:140,y:90,fill:color,'fill-opacity':config.opacity,'text-anchor':'middle','dominant-baseline':'middle','font-family':'system-ui, sans-serif','font-size':config.fontSize,'font-weight':config.weight,transform:'rotate(-25 140 90)'})) text.setAttribute(key,String(value));
    text.textContent = config.text;
    if (Array.from(config.text).length > 14) { text.setAttribute('textLength',String(Math.round(config.fontSize * 11.2))); text.setAttribute('lengthAdjust','spacingAndGlyphs'); }
    if (full) {
      const defs = document.createElementNS(ns,'defs'), pattern = document.createElementNS(ns,'pattern'), fill = document.createElementNS(ns,'rect');
      pattern.id = 'vex-watermark-pattern'; pattern.setAttribute('width','280'); pattern.setAttribute('height','180'); pattern.setAttribute('patternUnits','userSpaceOnUse'); pattern.append(text); defs.append(pattern);
      fill.setAttribute('width','100%'); fill.setAttribute('height','100%'); fill.setAttribute('fill','url(#vex-watermark-pattern)'); root.append(defs,fill);
    } else root.append(text);
    return new XMLSerializer().serializeToString(root);
  }
  function image(input, color) { return 'url("data:image/svg+xml,' + encodeURIComponent(svg(input,color)) + '")'; }
  function apply(input) {
    values = input;
    const style = document.documentElement.style;
    const base = settings().color, light = base, dark = lighten(base);
    style.setProperty('--vex-watermark-light',image(values,light)); style.setProperty('--vex-watermark-dark',image(values,dark));
    /* 开启即全场景显示：阅读 / 即时 / 演示（.vex-deck article）+ 导出。 */
    const enabled = !!values.watermarkPreview && !!settings().text && !new URLSearchParams(location.search).has('document');
    activeNow = enabled;
    document.querySelectorAll('#instant-content, #markdown-preview, #reading-content, .vex-deck article').forEach(el => el.classList.toggle('vex-watermarked',enabled));
    document.querySelectorAll('[data-vex-export-watermark]').forEach(el => { el.innerHTML = exportMark(); });
  }
  function exportMark(input = values) {
    return input.watermarkPreview && settings(input).text ? '<div class="vex-export-watermark" aria-hidden="true">' + svg(input,settings(input).color,true) + '</div>' : '';
  }
  const exportStyles = '.vex-export-watermark{position:fixed;inset:0;z-index:10;pointer-events:none;user-select:none;overflow:hidden}.vex-export-watermark>svg{display:block;width:100%;height:100%}';
  function preview(title, html) {
    let host = document.querySelector('.vex-export-host');
    if (!host) { host = document.createElement('div'); host.className = 'vex-export-host'; VexOverlay.mount(host); }
    host.dataset.small = String(VexOverlay.width() <= 650);
    const releaseOverlay = VexOverlay.lock(host);
    host.innerHTML = '<section class="vex-export-dialog" role="dialog" aria-modal="true" aria-labelledby="vex-export-title"><header><h2 id="vex-export-title"></h2><button class="vex-icon-button" aria-label="关闭导出预览"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor"><path d="m6 6 12 12M18 6 6 18"/></svg></button></header><label class="vex-pref-row"><span>导出时包含水印</span><input type="checkbox" role="switch" data-export-watermark></label><div class="vex-export-scroll"><article class="vex-export-page md-body markdown-body"><div data-vex-export-content></div><div data-vex-export-watermark></div></article></div><footer>PDF 排版预览 · 原型不生成实际 PDF 文件</footer></section>';
    host.querySelector('h2').textContent = title;
    host.querySelector('[data-vex-export-content]').innerHTML = html;
    host.querySelector('[data-vex-export-watermark]').innerHTML = exportMark();
    const toggle = host.querySelector('[data-export-watermark]'); toggle.checked = !!values.watermarkPreview;
    toggle.onchange = () => window.VexPreferences.set('watermarkPreview',toggle.checked);
    function close() { host.remove(); document.removeEventListener('keydown',keydown,true); releaseOverlay(); }
    function keydown(event) {
      if (event.key === 'Escape') {event.preventDefault();event.stopImmediatePropagation();close();}
      if (event.key === 'Tab') { event.preventDefault(); event.stopImmediatePropagation(); (document.activeElement === toggle ? host.querySelector('button') : toggle).focus(); }
    }
    document.addEventListener('keydown',keydown,true);
    host.querySelector('button').onclick = close; host.onclick = event => {if (event.target === host) close();};
    host.querySelector('button').focus();
    window.VexRendering?.diagrams();
  }
  window.VexWatermark = {apply,svg,image,exportMark,exportStyles,preview,active: () => activeNow};
})();
