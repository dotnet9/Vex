/* Prototype design contract: shared roles, distinct values, accessible surfaces.
 * 配色令牌已改为生成器产出（scripts/palette-rules.cjs ← 规则文档附录 A），
 * 本文件只负责正文排版的对比度校正与目录精选，不再运行时派生界面配色。 */
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
  /* 排版文字的可达背景集合：取自生成配色数据的基础面（paper/panel/bg 及近似 hover）。 */
  const surfaces={light:[],dark:[]};
  for(const p of VexPalettes){
    for(const mode of ['light','dark']){
      const dark=mode==='dark';
      const paper=dark?p.darkPaper:p.paper,panel=dark?p.darkPanel:p.panel,bg=dark?p.darkBg:p.bg;
      const decoration=dark?p.dark:p.light;
      surfaces[mode].push(bg,paper,panel,mix(panel,decoration,.07),mix(panel,decoration,.12));
    }
  }
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
  /* 精选 9 套高对比度排版：简约 / 文档 / 书卷 / 专栏 / 刊物 / 学术 / 橙心 / 极客黑 / 萌绿，3×3 网格。 */
  const keptTypes = ['Simple','Basic','InkBlack','Column','WeChatFormat','Academic','OrangeHeart','GeekBlack','CuteGreen'];
  const originalRules=window.VexTypeRules;
  const typeTokens={};
  /* 用户自定义排版（数组，键 user-<id>）：用各自编写层双明暗值注册令牌，对比度校正照跑。 */
  let customTypes=[];
  try{
    const raw=JSON.parse(VexStorage.getItem('vex-custom-typographics')||'[]');
    if(Array.isArray(raw))customTypes=raw.filter(c=>c&&c.id&&c.light&&c.dark)
      .map(c=>({key:'user-'+c.id,label:c.name||'自定义',note:'我的排版 · 实时派生',light:c.light,dark:c.dark}));
  }catch(_){}
  for(const t of VexTypes.concat(customTypes)){
    t.note=typeNotes[t.key]||t.note||t.label;if(t.key.indexOf('user-')!==0)t.recommended=recommendedTypes.includes(t.key);typeTokens[t.key]={};
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
  window.VexDesign={contrast,mix,readable,typeTokens};
})();
