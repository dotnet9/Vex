/* One Mermaid configuration for reading and editable previews. Measure text
 * with the same local font stack that is used to paint the finished SVG. */
(() => {
  'use strict';
  const font = '"Segoe UI", "Microsoft YaHei UI", "PingFang SC", sans-serif';
  const tokens = {'#d1e2f3':'--diagram-node','#253647':'--diagram-text','#456789':'--diagram-accent','#e2d3c4':'--diagram-secondary','#f3e4d5':'--diagram-paper','#647586':'--diagram-line','#b4c5d6':'--diagram-border'};
  const series = ['#7466e0','#d9ad53','#70a98b','#c583b5','#62a5c2','#d58585','#7e9ecb','#a9b568'];
  const soft = ['#eeeafd','#fff4d6','#e5f2e5','#f5e4f1','#e0f0f7','#fae6e3','#e7eef9','#eff2d9'];
  // 实色底上的可读前景：紫/青/蓝/红取白，黄/绿/粉/橄榄取深（对齐参考图的 mindmap/timeline）。
  const onSeries = ['#ffffff','#253647','#253647','#253647','#ffffff','#ffffff','#ffffff','#253647'];
  const palette = {};
  ['#4e79a7','#f28e2c','#e15759','#76b7b2','#59a14f','#edc949','#af7aa1','#ff9da7','#9c755f','#bab0ab'].forEach((color,index) => { tokens[color]='--diagram-series-'+(index%series.length); });
  series.forEach((color,index) => {
    tokens[color]='--diagram-series-'+index; tokens[soft[index]]='--diagram-soft-'+index;
    palette['pie'+(index+1)]=color;palette['git'+index]=color;palette['cScale'+index]=color;
    palette['cScaleLabel'+index]=onSeries[index];palette['cScaleInv'+index]=color;
  });
  let initialized = false;
  function initialize() {
    if (initialized) return;
    const c4 = {useMaxWidth:false,diagramMarginX:24,c4ShapeMargin:32,c4ShapePadding:18,width:216,messageFontSize:14,messageFontFamily:font};
    for (const name of ['person','external_person','system','external_system','system_db','external_system_db','system_queue','external_system_queue','boundary','container','external_container','container_db','external_container_db','container_queue','external_container_queue','component','external_component','component_db','external_component_db','component_queue','external_component_queue']) c4[name+'FontFamily'] = font;
    mermaid.initialize({startOnLoad:false,securityLevel:'strict',theme:'base',suppressErrorRendering:true,fontFamily:font,fontSize:14,
      gantt:{useMaxWidth:false,useWidth:960,barHeight:30,barGap:10,topPadding:64,leftPadding:100,rightPadding:48,fontSize:14,sectionFontSize:14,axisFormat:'%m/%d'},
      c4,
      xyChart:{useMaxWidth:false,width:760,height:480,titleFontSize:22,titlePadding:20,xAxis:{labelFontSize:14,labelPadding:10,titleFontSize:16,titlePadding:14},yAxis:{labelFontSize:14,labelPadding:10,titleFontSize:16,titlePadding:14}},
      themeVariables:{...palette,darkMode:false,fontFamily:font,fontSize:'14px',primaryColor:'#d1e2f3',primaryTextColor:'#253647',primaryBorderColor:'#456789',secondaryColor:'#e2d3c4',tertiaryColor:'#f3e4d5',lineColor:'#647586',textColor:'#253647',mainBkg:'#d1e2f3',nodeBorder:'#456789',clusterBkg:'#f3e4d5',clusterBorder:'#b4c5d6',edgeLabelBackground:'#f3e4d5',actorBkg:'#d1e2f3',actorBorder:'#456789',actorTextColor:'#253647',actorLineColor:'#647586',signalColor:'#253647',signalTextColor:'#253647',labelBoxBkgColor:'#f3e4d5',labelTextColor:'#253647',
        xyChart:{backgroundColor:'#f3e4d5',titleColor:'#253647',xAxisTitleColor:'#253647',xAxisLabelColor:'#253647',xAxisTickColor:'#647586',xAxisLineColor:'#647586',yAxisTitleColor:'#253647',yAxisLabelColor:'#253647',yAxisTickColor:'#647586',yAxisLineColor:'#647586',plotColorPalette:'#eeeafd,#7e9ecb'}
      }});
    initialized = true;
  }
  function themed(svg) {
    return svg.replace(/#[\da-f]{6}\b/gi, color => tokens[color.toLowerCase()] ? 'var('+tokens[color.toLowerCase()]+')' : color)
      .replace(/rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)(?:\s*,\s*([\d.]+))?\s*\)/g,(color,r,g,b,alpha)=>{
        const token=tokens['#'+[r,g,b].map(x=>Number(x).toString(16).padStart(2,'0')).join('')];
        return token ? alpha && Number(alpha)<1 ? 'color-mix(in srgb, var('+token+') '+Number(alpha)*100+'%, transparent)' : 'var('+token+')' : color;
      });
  }
  const cache = new Map(), jobs = new Map(), ready = new Set();
  let cacheSerial = 0, draining = false;
  // Reading and editing share layouts, but every mounted SVG owns its IDs.
  async function render(id, source) {
    let entry = cache.get(source);
    if (!entry) {
      initialize();
      entry = mermaid.render('vex-cached-' + (++cacheSerial), source).then(result => {
        const svg = themed(result.svg), ids = [...svg.matchAll(/\bid="([^"]+)"/g)].map(match => match[1]);
        const escaped = [...new Set(ids)].sort((a,b) => b.length-a.length).map(value => value.replace(/[.*+?^${}()|[\]\\]/g,'\\$&'));
        return {svg, ids, references:new RegExp('(#|id=["\'])(' + escaped.join('|') + ')(?![\\w-])','g')};
      });
      cache.set(source,entry);
      entry.catch(() => { if (cache.get(source) === entry) cache.delete(source); });
      while (cache.size > 48) cache.delete(cache.keys().next().value);
    } else { cache.delete(source); cache.set(source,entry); }
    const result = await entry, ids = new Map(result.ids.map((value,index) => [value,id+'-'+index]));
    return result.svg.replace(result.references,(_,prefix,value) => prefix+ids.get(value));
  }
  const observer = new IntersectionObserver(entries => {
    for (const entry of entries) {
      if (entry.isIntersecting) ready.add(entry.target); else ready.delete(entry.target);
    }
    drain();
  }, {rootMargin:'160px 0px'});
  function prune() {
    for (const host of jobs.keys()) if (!host.isConnected) { observer.unobserve(host); jobs.delete(host); ready.delete(host); }
  }
  function schedule(host, callback) {
    prune(); jobs.set(host,callback); observer.observe(host);
    // Replacing an already observed job (for example editing) needs no new IO event.
    if (ready.has(host)) drain();
  }
  async function drain() {
    if (draining) return;
    draining = true;
    try {
      while (ready.size) {
        // Give input, navigation and paint a turn between CPU-heavy layouts.
        await new Promise(resolve => setTimeout(resolve,0));
        prune();
        const hosts = [...ready].filter(host => jobs.has(host) && host.getClientRects().length);
        hosts.sort((a,b) => Math.abs(a.getBoundingClientRect().top)-Math.abs(b.getBoundingClientRect().top));
        const host = hosts[0];
        if (!host) break;
        const callback = jobs.get(host); jobs.delete(host); ready.delete(host); observer.unobserve(host);
        await callback();
      }
    } finally { draining = false; }
  }
  function mount(host, source, label) {
    const svg=host.querySelector('svg');
    if (!svg) return;
    svg.setAttribute('role','img');svg.setAttribute('aria-label',label || VexMermaid.label(source));
    // 阅读视图：把 SVG 包进预览容器，加"mermaid"徽标（右上角）与可切换的源码块（点图表看源码）。
    if (!host.classList.contains('diagram-preview') && !host.querySelector('.diagram-preview')) {
      const preview=document.createElement('div');preview.className='diagram-preview';
      svg.replaceWith(preview);preview.append(svg);
      const sourceView=document.createElement('pre');
      sourceView.className='diagram-source-view';sourceView.hidden=true;
      sourceView.setAttribute('aria-label','Mermaid 源码');
      const code=document.createElement('code');code.textContent=source;sourceView.append(code);
      host.prepend(sourceView);
      const badge=document.createElement('button');
      badge.className='diagram-badge';badge.type='button';
      badge.textContent='mermaid';
      badge.setAttribute('aria-expanded','false');
      badge.setAttribute('aria-label','查看图表源码');
      badge.title='查看 / 隐藏源码';
      preview.append(badge);
      const toggle=()=>{
        const show=sourceView.hidden;
        sourceView.hidden=!show;
        host.classList.toggle('show-source',show);
        badge.setAttribute('aria-expanded',String(show));
      };
      badge.addEventListener('click',event=>{event.stopPropagation();toggle();});
      host.addEventListener('click',event=>{if(event.target.closest('.diagram-badge'))return;toggle();});
      host.addEventListener('keydown',event=>{
        if(event.target===host&&(event.key==='Enter'||event.key===' ')){event.preventDefault();toggle();}
      });
    }
    if (/^\s*mindmap/m.test(source)) {
      // 参考样式：mindmap 分支线为 4px 粗彩线（section 色由 cScale 提供）。
      for (const edge of svg.querySelectorAll('[class*="section-edge"], .edge path')) edge.style.strokeWidth = '4';
    }
    if (/^\s*sankey/m.test(source)) {
      // Multiplication turns translucent ribbons almost black on dark paper.
      for (const element of svg.querySelectorAll('[style*="mix-blend-mode"]')) element.style.mixBlendMode='normal';
    }
    if (/^\s*packet/m.test(source)) {
      host.dataset.diagramType='packet';
      for (const text of svg.querySelectorAll('text')) text.style.fill='var(--diagram-text)';
    }
    if (/^\s*gantt\b/m.test(source)) for (const text of svg.querySelectorAll('.tick text')) text.style.fontSize='13px';
    // C4's hard-coded textLength compresses the stereotype glyphs. Keep their
    // original centre, but let the chosen font use its natural letter widths.
    if (/^\s*C4/m.test(source)) {
      for (const text of svg.querySelectorAll('text[textLength]')) {
        text.setAttribute('x',Number(text.getAttribute('x'))+Number(text.getAttribute('textLength'))/2);
        text.setAttribute('text-anchor','middle');text.removeAttribute('textLength');text.removeAttribute('lengthAdjust');
        text.setAttribute('font-style','normal');text.setAttribute('font-size','14');
        text.setAttribute('font-family',font);
      }
      for (const text of svg.querySelectorAll('text')) if (text.getAttribute('fill')==='#444444') text.setAttribute('fill','var(--fg)');
      for (const shape of svg.querySelectorAll('[fill="#999999"]')) shape.setAttribute('fill','#586572');
      for (const edge of svg.querySelectorAll('[stroke="#444444"], [stroke="#444"]')) edge.setAttribute('stroke','var(--muted)');
      for (const arrow of svg.querySelectorAll('marker [fill="#444444"], marker [fill="#444"]')) arrow.setAttribute('fill','var(--muted)');
    }
    const box=svg.viewBox.baseVal;
    if (box.width>0 && box.height>0) {
      svg.style.width=box.width+'px';svg.style.height='auto';svg.style.maxWidth='100%';
      svg.setAttribute('width',box.width);svg.setAttribute('height',box.height);
    }
    host.tabIndex=0;
    host.setAttribute('aria-label',label || VexMermaid.label(source));
  }
  window.VexDiagramRendering={render,mount,schedule};
})();
