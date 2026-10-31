// Sync the approved design contract into native library resources. No browser or third-party dependencies.
// 支持 --check：不落盘，重新生成并与库内现有文件比对，漂移即退出码 1（供 CI 门禁）。
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const root = path.resolve(__dirname, '..');
/* 库路径解析顺序：--library <path> > 位置参数 > 环境变量 CODEWF_AVALONIA_ROOT。
 * 不再内置盘符默认值（历史默认指向不存在的 D: 盘）；缺失或不存在时按用法退出。 */
const argv = process.argv.slice(2);
const typographyOnly = argv.includes('--typography-only');
const checkOnly = argv.includes('--check');
let libraryArg = null;
for (let i = 0; i < argv.length; i++) {
  if (argv[i] === '--library') { libraryArg = argv[++i]; continue; }
  if (argv[i].startsWith('--') || argv[i] === libraryArg) continue;
  if (libraryArg === null) libraryArg = argv[i];
}
const libraryRoot = libraryArg || process.env.CODEWF_AVALONIA_ROOT;
if (!libraryRoot) {
  console.error('用法: node scripts/sync_native_design.cjs [--typography-only] [--check] [--library <CodeWF.Avalonia 根目录>]');
  console.error('也可用环境变量 CODEWF_AVALONIA_ROOT 指定库根目录。');
  process.exit(2);
}
const library = path.resolve(libraryRoot);
if (!fs.existsSync(library)) {
  console.error(`库根目录不存在：${library}`);
  process.exit(2);
}
const context = { document: {createElement:()=>({}), head:{append(){}}} };
context.window = context;
vm.createContext(context);
for (const name of ['prototype/palettes.js','generated/palette-rules-data.js','prototype/palette-rules.js','prototype/typography-catalog.js','prototype/desktop-resources.js','prototype/design-system.js'])
  vm.runInContext(fs.readFileSync(path.join(root,'design/assets',name),'utf8'),context);
const palettes = context.VexPalettes;
const types = context.VexTypes;
/* 配色派生统一走共享模块（与生成器/浏览器编辑同源），不再解析 palettes.css 文本。 */
const derive = (key, mode) => context.VexPaletteRules.deriveVars(
  Object.assign({}, context.VexPaletteRulesData.palettes[key][mode], context.VexPaletteRulesData.statusDefaults[mode]),
  mode, context.VexPaletteRulesData.derivation[mode]);
const q = value => JSON.stringify(value);
/* 生成结果统一收集，最后按模式落地（写入）或比对（--check）。 */
const outputs = [];
/* 模板带 BOM，库侧 axaml 一律无 BOM：不剥掉就会每次都报「首行不同」的假漂移。 */
const write = (file,text) => outputs.push({file: path.resolve(file), text: text.replace(/^﻿/,'').replace(/\r\n/g,'\n')});
if (!typographyOnly) {
const names = {graphite:'Graphite',indigo:'Indigo',teal:'Teal',pine:'Pine',sky:'Sky',amber:'Amber',crimson:'Crimson',violet:'Violet',terracotta:'Terracotta'};
const tokens = path.join(library,'src/Controls/CodeWF.Avalonia.Controls/Themes/Studio/Tokens');
const template = fs.readFileSync(path.join(__dirname,'native-design/StudioPalette.template.axaml'),'utf8');
for (const p of palettes) for (const mode of ['light','dark']) {
  const dark=mode==='dark', t=derive(p.key,mode);
  /* WCAG 对比度：与库侧 StudioPaletteEngine.Luminance/Contrast 同式（线性化阈值 0.04045、权重 .2126/.7152/.0722），
   * 两边任一处改公式，StudioTokenParityTests 的逐键比色就会红。 */
  const channel=v=>{v/=255;return v<=0.04045?v/12.92:Math.pow((v+0.055)/1.055,2.4);};
  const lum=h=>{const p=h.replace('#','');return [0,2,4].map(j=>channel(parseInt(p.substr(j,2),16))).reduce((s,v,i)=>s+v*[0.2126,0.7152,0.0722][i],0);};
  const vs=(a,b)=>{const x=lum(a),y=lum(b);return (Math.max(x,y)+0.05)/(Math.min(x,y)+0.05);};
  /* 一组底色共用的文字色：白与黑取「最差那枚对比更高者」，与引擎 OnColors 同样用 >= 让平局归白。 */
  const onColors=(...hexes)=>{const worst=ink=>Math.min(...hexes.map(h=>vs(ink,h)));return worst('#FFFFFF')>=worst('#000000')?'#FFFFFF':'#000000';};
  /* 画布三键必须取 deriveVars 里的 §5.1 去色温值（原型 palettes.css 烧的就是它），不能取 p.darkPaper/p.darkPanel
   * 这类编写层原始值：那样九套暗色各带各的色温，大面积画布整体不像原型，而 --check 全绿也发现不了，
   * 因为漂移就烧在取数行里。派生表里 --paper/--surface 同值（都= 去温后的 paper），--panel = 去温后的 surface。
   * 分段控件两键与浮起面同样直接取派生表：它们原本是这里自算的三个 mix，与库引擎各写一遍。 */
  const paper=t.paper, surface=t.panel, fgColor=t.fg;
  const colors={StudioBackgroundColor:t.bg,StudioSurfaceColor:surface,
    StudioPaperColor:paper,StudioRaisedColor:t.hover,StudioBorderColor:t.border,
    StudioSegmentedTrackColor:t['segmented-track'],StudioSegmentedThumbColor:t['segmented-thumb'],StudioElevatedColor:t.elevated,
    StudioTextColor:fgColor,StudioMutedColor:t.muted,StudioAccentColor:t.accent,
    StudioSecondaryColor:t['accent-decoration'],StudioPrimaryColor:t.primary,StudioGradientStartColor:t.primary,
    StudioGradientEndColor:t['gradient-end'],StudioTextGradientEndColor:t['gradient-end']};
  let text=template.replace(/<Color x:Key="([^"]+)">[^<]+<\/Color>/g,(all,key)=>`<Color x:Key="${key}">${colors[key]||'#24766B'}</Color>`);
  /* 状态软底与实色全部直接取原型派生表的键，不在这里重抄 §3 公式：palette-rules.js 的模块头写明
   * 「公式与常数的唯一事实源是规则附录 A，禁止在任何一侧另抄」。曾因本处自有一套 mix 收敛，
   * 与库侧引擎各说各话——同一控件在内置配色与用户配色下两种深浅，而 StudioTokenParityTests 看不见。
   * 键位：-soft/-hover-bg 是 hover 档、-pressed-bg 深一档；状态字色 *-hover-fg/-pressed-fg 由 §3
   * 的状态色族不动点解出，保证文字对自家两档软底同时 ≥4.5（曾因缺映射导致暗色徽章浅底配浅字 1.0:1）。 */
  const brushes={StudioOnPrimary:t['on-primary'],StudioPrimaryHover:t['primary-hover'],StudioPrimaryPressed:t['primary-pressed'],
    SemiColorSuccess:t['status-success'],SemiColorWarning:t['status-warning'],SemiColorDanger:t['status-error'],
    SemiColorDangerActive:t['danger-pressed-fg'],
    SemiColorDangerLight:t['status-error-bg'],SemiColorDangerLightActive:t['danger-pressed-bg'],
    SemiColorSuccessLight:t['success-soft'],SemiColorWarningLight:t['warning-soft'],
    StatusCardTopTextForeground:t['fg'],
  /* 徽章左侧胶囊有两张脸：默认/Debug 落在强调色上（--on-primary），Info/Warn/Error/Fatal 落在状态实色上。
   * 一个前景键喂不满两套底色——鼠尾草的 accent 要黑字（白字仅 3.56:1），浅底 success 要白字（黑字 4.17:1）。 */
  StatusBadgeStatusTextForeground:onColors(t['status-success'],t['status-warning'],t['status-error']),
    PgSelectedText:t['on-primary'],StatusBadgeLeftTextForeground:t['on-primary']};
  text=text.replace(/<SolidColorBrush x:Key="([^"]+)">[^<]+<\/SolidColorBrush>/g,(all,key)=>brushes[key]?`<SolidColorBrush x:Key="${key}">${brushes[key]}</SolidColorBrush>`:all);
  /* 操作面渐变的第二停点走装饰副色（--gradient-end），已在 colors 里按键派发，
   * 这里不再整段改写模板中的 GradientStop——那会把它压回 primary，库里的「独立渐变」设计随之消失。 */
  const extra={StudioHover:t.hover,StudioPressed:t.pressed,StudioSelected:t['accent-soft'],StudioControlBorder:t['control-border'],StudioFocusRing:t['focus-ring'],StudioDisabled:t['fg-disabled']};
  const extraText=Object.entries(extra).map(([key,value])=>`    <Color x:Key="${key}Color">${value}</Color>\n    <SolidColorBrush x:Key="${key}" Color="{StaticResource ${key}Color}"/>`).join('\n');
  text=text.replace('    <!-- 颜色令牌',`    <Color x:Key="StudioPaperColor">${colors.StudioPaperColor}</Color>\n${extraText}\n    <!-- 颜色令牌`);
  write(path.join(tokens,names[p.key]+(dark?'Dark':'Light')+'.axaml'),text);
}
let index='<ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:themes="using:CodeWF.Avalonia.Controls.Themes">\n<ResourceDictionary.ThemeDictionaries>\n';
/* Default/Light/Dark 指向 indigo（库默认配色），与库生成器 generate-studio-cs.cjs 的 _index 约定一致；
 * 本脚本不产出 Default*.axaml，指向不存在的文件会让库编译失败（AVLN2000）。 */
index+='<ResourceInclude x:Key="Default" Source="IndigoLight.axaml"/>\n<ResourceInclude x:Key="Light" Source="IndigoLight.axaml"/>\n<ResourceInclude x:Key="Dark" Source="IndigoDark.axaml"/>\n';
for(const p of palettes) for(const mode of ['Light','Dark']) if(names[p.key]!=='Default') index+=`<ResourceInclude x:Key="{x:Static themes:StudioTheme.${names[p.key]+mode}}" Source="${names[p.key]+mode}.axaml"/>\n`;
index+='</ResourceDictionary.ThemeDictionaries>\n';
for(const p of palettes) index+=`<SolidColorBrush x:Key="StudioSwatch${names[p.key]}">${p.light}</SolidColorBrush>\n`;
write(path.join(tokens,'_index.axaml'),index+'</ResourceDictionary>\n');
/* 库侧配色目录（StudioPalettes.g.cs）由库自身 scripts/generate/generate-studio-cs.cjs
 * 从规则附录 A 生成，这里不再二次写出，避免两处生成器互相覆盖。 */
}

function declarations(css,selector) {
  const result={};
  for(const [,selectors,body] of css.matchAll(/([^{}]+)\{([^{}]*)\}/g))
    if(selectors.split(',').map(x=>x.trim()).includes(selector))
      for(const entry of body.split(';')) {const i=entry.indexOf(':');if(i>0){const key=entry.slice(0,i).trim(),value=entry.slice(i+1).trim();result[key]=value;if(key==='border')for(const edge of ['left','top','right','bottom'])result['border-'+edge]=value;if(key==='margin'||key==='padding')for(const edge of ['left','top','right','bottom'])delete result[key+'-'+edge];}}
  return result;
}
const n = (text,fallback=0) => Number.parseFloat(text)||fallback;
const px=(value,size)=>n(value)*(value?.endsWith('em')?size:1);
function thickness(value,size){const values=(value||'0').split(/\s+/).map(v=>px(v,size));const [a,b=a,c=a,d=b]=values;return `${d},${a},${b},${c}`;}
function spacing(rule,size){const values=thickness(rule.margin,size).split(',').map(Number);for(const [edge,i] of Object.entries({left:0,top:1,right:2,bottom:3}))if(rule['margin-'+edge])values[i]=px(rule['margin-'+edge],size);return values.join(',');}
function borders(rule,size){const base=px((rule.border||'0').split(' ')[0],size);return ['left','top','right','bottom'].map(edge=>rule['border-'+edge]===undefined?base:px(rule['border-'+edge].split(' ')[0],size)).join(',');}
function heading(t,level,size){
  const h=declarations(context.VexTypeRules(t.key,false),'BODY h'+level),fontSize=px(h['font-size']||'1em',size);
  const border={top:0,right:0,bottom:0,left:0};
  for(const edge of Object.keys(border)) border[edge]=px((h['border-'+edge]||'').split(' ')[0],fontSize);
  const padding=thickness(h.padding,fontSize).split(',').map(Number);if(h['padding-bottom'])padding[3]=px(h['padding-bottom'],fontSize);if(h['padding-top'])padding[1]=px(h['padding-top'],fontSize);if(h['padding-left'])padding[0]=px(h['padding-left'],fontSize);
  const background=h.background||'',solid=background==='var(--type-solid)'||background==='var(--type-decoration)';
  return `new(${fontSize}, ${n(h['font-weight'],level===1?700:level===2?650:600)}, ${q(h['text-align']==='center'?'Center':'Left')}, ${q(borders(h,fontSize))}, ${q(padding.join(','))}, ${background==='none'||!background?'0':solid?'1':'.1'}, ${q(h.color||'var(--fg)')}, ${px(h['border-radius']||'0',fontSize)}, ${q(h['font-family']?.includes('mono')?'mono':'system')}, ${fontSize*n(h['line-height'],1.7)}, ${q(spacing(h,fontSize))})`;
}
function quote(t,size){const r=declarations(context.VexTypeRules(t.key,false),'BODY blockquote');return `new(${q(borders(r,size))}, ${q(thickness(r.padding,size))}, ${q(spacing(r,size))}, ${px(r['border-radius']||'0',size)}, ${r.background==='transparent'?0:.06}, ${q(r['text-align']==='center'?'Center':'Left')}, ${q(r.color||'var(--muted)')}, ${n(r['font-size'],1)}, ${r['font-style']==='italic'})`;}
let catalog=`// Generated by Vex/scripts/sync_native_design.cjs; do not hand edit.\n#nullable enable\nnamespace CodeWF.Avalonia.Markdown.Themes;\npublic static class ArticleTypographyCatalog\n{\n    public static IReadOnlyList<ArticleTypography> All { get; } = [\n`;
for(const t of types){const body=declarations(context.VexTypeRules(t.key,false),'BODY'),size=n(t.rules.match(/--type-size:([\d.]+)/)?.[1],16),leading=n(t.rules.match(/--type-leading:([\d.]+)/)?.[1],1.9);
  catalog+=`        new(${q(t.key)}, ${q(t.label)}, ${q(t.en)}, ${q(t.note)}, ${t.recommended}, ${size}, ${leading}, ${q(context.VexDesign.typeTokens[t.key].light.text)}, ${q(context.VexDesign.typeTokens[t.key].dark.text)}, ${q(t.light)}, ${q(t.dark)}, ${q(body['font-family']?.includes('Serif')?'serif':body['font-family']?.includes('mono')?'mono':'system')}, [${[1,2,3,4,5,6].map(l=>heading(t,l,size)).join(', ')}], ${q(context.VexTypeExport(t.key,false))}, ${q(context.VexTypeExport(t.key,true))}, ${quote(t,size)}, ${q(declarations(context.VexTypeRules(t.key,false),'BODY p')['text-align']==='justify'?'Justify':'Left')}),\n`;
}
catalog+='    ];\n    public static ArticleTypography? Find(string? key) => All.FirstOrDefault(item => item.Key == key);\n}\n';
write(path.join(library,'src/Markdown/CodeWF.Avalonia.Markdown.Lite/Themes/ArticleTypographyCatalog.g.cs'),catalog);
if (checkOnly) {
  const drifted = [];
  for (const {file,text} of outputs) {
    const existing = fs.existsSync(file) ? fs.readFileSync(file,'utf8').replace(/\r\n/g,'\n') : null;
    if (existing !== text) drifted.push(path.relative(library,file));
  }
  if (drifted.length) {
    console.error(`库资源与原型契约不同步（${drifted.length} 个文件漂移）：`);
    for (const file of drifted) console.error('  ' + file);
    console.error('请运行: node scripts/sync_native_design.cjs --library <CodeWF.Avalonia 根目录>');
    process.exit(1);
  }
  console.log(`Check passed: ${outputs.length} generated library files match the prototype contract.`);
} else {
  for (const {file,text} of outputs) {
    fs.mkdirSync(path.dirname(file),{recursive:true});
    /* 跟随目标文件现有换行风格：库内 axaml/cs 是 CRLF，统一写 LF 会让 18 个令牌文件
     * 每次同步都产生纯换行漂移（同 Vex.Setup.iss 病灶）。新文件默认 LF。 */
    const existing = fs.existsSync(file) ? fs.readFileSync(file,'utf8') : '';
    fs.writeFileSync(file, existing.includes('\r\n') ? text.replace(/\n/g,'\r\n') : text);
  }
  console.log(`Synced ${types.length} independent article typographies to ${library}` + (typographyOnly ? ' (typography only)' : `; palette tokens for ${palettes.length} palettes × 2 modes`));
}
