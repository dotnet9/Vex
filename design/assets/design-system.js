/* Prototype design contract: shared roles, distinct values, accessible surfaces. */
(function () {
  'use strict';
  const rgb = hex => hex.replace('#','').match(/../g).map(x=>parseInt(x,16));
  const hex = values => '#'+values.map(x=>Math.round(x).toString(16).padStart(2,'0')).join('');
  const mix = (a,b,weight) => hex(rgb(a).map((v,i)=>v*(1-weight)+rgb(b)[i]*weight));
  const luminance = color => rgb(color).map(v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4;}).reduce((sum,v,i)=>sum+v*[.2126,.7152,.0722][i],0);
  const contrast = (a,b) => (Math.max(luminance(a),luminance(b))+.05)/(Math.min(luminance(a),luminance(b))+.05);
  // Move only as far as necessary along the original hue's path to a light/dark endpoint.
  function readable(color, backgrounds, minimum=4.65) {
    if(backgrounds.every(bg=>contrast(color,bg)>=minimum))return color;
    const endpoint = luminance(backgrounds[0])>.4?'#111820':'#FFFFFF';
    for(let i=1;i<=100;i++){const next=mix(color,endpoint,i/100);if(backgrounds.every(bg=>contrast(next,bg)>=minimum))return next;}
    return endpoint;
  }
  const onSolid = color => contrast('#FFFFFF',color)>=contrast('#17211C',color)?'#FFFFFF':'#17211C';
  const paletteNotes = {
    weft:'纸白墨青 · 日常写作',graphite:'中性灰阶 · 专注内容',ocean:'沉稳海蓝 · 技术工作',sand:'暖纸沙金 · 长时阅读',
    rose:'柔和蔷薇 · 随笔记录',iris:'冷调鸢尾 · 创意整理',olive:'低饱和橄榄 · 自然舒缓',ember:'温暖陶朱 · 灵感手记',
    default:'晴空亮蓝 · 清晰明快',dotnet:'浓郁紫罗兰 · 开发工作','studio-ember':'高饱和赤陶 · 鲜明活力','night-sky':'深空蓝紫 · 理性思考',aurora:'极光紫调 · 创意表达',dusk:'暮色暖紫 · 安静阅读',
    aquatic:'清透水青 · 轻快记录',desert:'大地暖棕 · 温润沉静'
  };
  /* 精简为 6 套高对比度配色：品牌墨青 / 石墨 / 海蓝 / 沙金 / .NET 紫 / 星夜。 */
  const keptPalettes = ['weft','graphite','ocean','sand','dotnet','night-sky'];
  const recommendedPalettes = ['weft','graphite','ocean','sand'];
  const recommendedTypes = ['Simple','InkBlack','TechnologyBlue','Column','Academic','WeChatFormat'];
  const typeNotes = {
    Simple:'日常写作 · 简洁留白',Basic:'工作文档 · 清晰层级',InkBlack:'长文阅读 · 书卷宋体',TechnologyBlue:'技术文档 · 条理分明',
    Column:'观点专栏 · 强调结构',WeChatFormat:'刊物文章 · 居中标题',Academic:'研究论文 · 稳重宋体',Journal:'散文手札 · 舒缓叙述',Handbook:'操作手册 · 分节易查',
    Foundation:'通用文档 · 细线分节',Clear:'轻量笔记 · 无饰标题',OrangeHeart:'生活随笔 · 暖橙边线',InkNight:'长文阅读 · 墨色双线',
    ColorfulPurple:'创意专栏 · 紫调浅底',TenderGreen:'日常记录 · 柔绿轻盈',Verdant:'知识整理 · 翠绿侧线',RedScarlet:'观点文章 · 朱红下划线',
    BlueGlow:'项目笔记 · 冷蓝浅底',TechAzure:'技术报告 · 湛蓝侧线',LanQing:'文化随笔 · 蓝青细线',Yamabuki:'生活专栏 · 山吹暖底',
    FrontendPeak:'开发教程 · 蓝色实底标题',GeekBlack:'代码手册 · 等宽标题',RosePurple:'创作笔记 · 玫紫双线',CuteGreen:'轻松科普 · 绿调圆角',
    FullStackBlue:'工程文档 · 蓝色分节',WeChatPaper:'图文专栏 · 对称标题'
  };
  const css=[], surfaces={light:[],dark:[]};
  const paletteTokens={};
  for(const p of VexPalettes){
    p.note=paletteNotes[p.key]||('独立色温 · '+p.label);p.recommended=recommendedPalettes.includes(p.key);
    paletteTokens[p.key]={};
    for(const mode of ['light','dark']){
      const dark=mode==='dark',bg=dark?p.darkBg:p.bg,paper=dark?p.darkPaper:p.paper,panel=dark?p.darkPanel:p.panel;
      const decoration=dark?p.dark:p.light,fg=dark?p.darkFg:p.fg;
      const hover=mix(panel,decoration,.07),pressed=mix(panel,decoration,.12),selected=mix(bg,decoration,.1);
      const backgrounds=[bg,paper,panel,hover,pressed,selected];surfaces[mode].push(...backgrounds);
      const accent=readable(decoration,backgrounds),muted=readable(dark?p.darkMuted:p.muted,backgrounds);
      const solid=decoration,on=onSolid(solid),solidHover=readable(mix(solid,on==='#FFFFFF'?'#000000':'#FFFFFF',.08),[on]);
      const solidPressed=readable(mix(solid,on==='#FFFFFF'?'#000000':'#FFFFFF',.16),[on]);
      const error=readable(dark?'#FFABA1':'#B53632',backgrounds);
      const tokens={accent,'accent-decoration':decoration,'accent-fg':onSolid(accent),'on-accent':onSolid(accent),
        'accent-soft':selected,hover,pressed,selection:mix(bg,decoration,.22),muted,subtle:muted,'fg-disabled':muted,'md-marker':muted,'md-quote':muted,'md-code-fg':accent,
        primary:solid,'on-primary':on,'primary-hover':solidHover,'primary-pressed':solidPressed,'accent-hover':readable(mix(accent,dark?'#FFFFFF':'#000000',.08),backgrounds),
        'control-border':readable(mix(panel,fg,.34),backgrounds,3.05),'focus-ring':accent,'status-error':error,'status-error-bg':mix(bg,error,.06),
        'status-success':readable(dark?'#9BD4B0':'#316C48',backgrounds),'status-warning':readable(dark?'#E5C48D':'#825B19',backgrounds),
        danger:error,'danger-soft':mix(bg,error,.06),'danger-hover-bg':mix(bg,error,.08),'danger-hover-fg':error,'danger-pressed-bg':mix(bg,error,.1),'danger-pressed-fg':error};
      tokens.success=tokens['status-success'];tokens.warning=tokens['status-warning'];
      paletteTokens[p.key][mode]=tokens;
      css.push(':root[data-theme="'+mode+'"][data-palette="'+p.key+'"]{'+Object.entries(tokens).map(([key,value])=>'--'+key+':'+value).join(';')+'}');
    }
  }
  window.VexPalettes = VexPalettes.filter(p => keptPalettes.includes(p.key));
  /* 精选 9 套高对比度排版：简约 / 文档 / 书卷 / 专栏 / 刊物 / 学术 / 橙心 / 极客黑 / 萌绿，3×3 网格。 */
  const keptTypes = ['Simple','Basic','InkBlack','Column','WeChatFormat','Academic','OrangeHeart','GeekBlack','CuteGreen'];
  const style=document.createElement('style');style.id='design-system-tokens';style.textContent=css.join('\n');document.head.append(style);
  const originalRules=window.VexTypeRules;
  const typeTokens={};
  for(const t of VexTypes){
    t.note=typeNotes[t.key]||t.label;t.recommended=recommendedTypes.includes(t.key);typeTokens[t.key]={};
    for(const mode of ['light','dark']){
      const decoration=t[mode],backgrounds=surfaces[mode];
      // Include tinted titles and quotes, not just bare paper.
      const text=readable(decoration,[...backgrounds,...backgrounds.map(bg=>mix(bg,decoration,.12))]);
      typeTokens[t.key][mode]={text,decoration,solid:decoration,on:onSolid(decoration)};
    }
  }
  window.VexTypes = VexTypes.filter(t => keptTypes.includes(t.key));
  function typeProperties(t,exporting){
    const a=typeTokens[t.key].light,b=typeTokens[t.key].dark;
    const value=key=>exporting?a[key]:'light-dark('+a[key]+','+b[key]+')';
    return '--typography-accent:'+value('text')+';--type-decoration:'+value('decoration')+';--type-solid:'+value('solid')+';--type-on-accent:'+value('on')+';--type-tint:color-mix(in srgb,var(--type-decoration) 10%,transparent)';
  }
  function rules(key,compact,exporting){
    const t=VexTypes.find(t=>t.key===key)||VexTypes[0];
    const solidHeading=['Handbook','FrontendPeak'].includes(t.key)?'BODY h2{background:var(--type-solid);color:var(--type-on-accent)}':'';
    return originalRules(t.key,compact)+'BODY{'+typeProperties(t,exporting)+';--type-size:'+Math.max(16,Number(t.rules.match(/--type-size:(\d+)/)?.[1]||16))+'px;'+(compact?'font-size:max(16px,calc(var(--writing-size,var(--type-size,16px)) * .93))':'')+'}'+
      'BODY a{color:var(--typography-accent);text-decoration:underline;text-underline-offset:.18em}BODY blockquote{color:var(--fg);background:color-mix(in srgb,var(--type-decoration) 6%,transparent)}'+solidHeading;
  }
  window.VexTypeRules=(key,compact)=>rules(key,compact,false);
  window.VexTypeExport=(key,compact)=>rules(key,compact,true).replaceAll('BODY','body')+
    '.md-table-scroll{max-width:100%;overflow-x:auto}table{table-layout:auto;width:max-content;max-width:100%}th,td{width:auto;overflow-wrap:anywhere}body{color-scheme:light;--document-bg:#fff;--fg:#30343b;--muted:#606770;--border:#dde1e5}';
  window.VexDesign={contrast,mix,readable,paletteTokens,typeTokens};
})();
