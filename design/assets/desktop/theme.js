/* 外观集中在折叠面板中，保留三态、八套配色、界面密度三档和语言入口。 */
(function () {
  /* 推荐配色置顶（角标标识），其余按原顺序跟随。 */
  var palettes=[].concat(VexPalettes.filter(function(p){return p.recommended;}),VexPalettes.filter(function(p){return !p.recommended;})).map(function(p){return [p.key,p.label,Array.isArray(p.swatch)?p.swatch:[p.light]];});
  function swatchDots(colors){return '<span class="swatch-dots">'+colors.map(function(color){return '<i style="background:'+color+'"></i>';}).join('')+'</span>';}
  var modes=[['light','ThemeLight','☀'],['dark','ThemeDark','☾'],['system','ThemeSystem','◐']];
  var densities=[['compact','紧凑'],['normal','正常'],['relaxed','宽松']];
  var mdsizes=[['compact','紧凑'],['normal','正常'],['relaxed','宽松']];
  var storeKey='vex-design-appearance',system=matchMedia('(prefers-color-scheme: dark)'),state={mode:'light',palette:'indigo',density:'normal',mdsize:'normal'};
  try{Object.assign(state,JSON.parse(VexStorage.getItem(storeKey)||'{}'));}catch(_){}
  state.palette=VexPaletteKey(state.palette);
  if(!densities.some(item=>item[0]===state.density))state.density='normal';
  if(!mdsizes.some(item=>item[0]===state.mdsize))state.mdsize='normal';
  var query=new URLSearchParams(location.search),theme=query.get('theme');
  if(modes.some(item=>item[0]===theme))state.mode=theme;else if(palettes.some(item=>item[0]===theme))state.palette=theme;
  if(query.has('palette'))state.palette=query.get('palette');state.palette=VexPaletteKey(state.palette);
  function word(key){return window.VexL?VexL.get(key):key;}
  var themeFrame = 0;
  function apply(syncLanguage){
    if(!modes.some(item=>item[0]===state.mode))state.mode='system';
    if(!palettes.some(item=>item[0]===state.palette))state.palette='indigo';
    var root=document.documentElement;
    var nextTheme=state.mode==='system'?(system.matches?'dark':'light'):state.mode;
    var changed=root.dataset.theme!==nextTheme||root.dataset.palette!==state.palette;
    // Prevent every themed control from animating independently during a palette swap.
    // One frame applies the tokens together; ordinary hover/focus transitions remain enabled.
    if(changed && root.dataset.theme){
      root.classList.add('theme-switching');
      if(themeFrame)cancelAnimationFrame(themeFrame);
    }
    root.dataset.themeMode=state.mode;
    root.dataset.theme=nextTheme;
    root.dataset.palette=state.palette;
    root.dataset.density=state.density;
    root.dataset.mdsize=state.mdsize;
    /* 正文字号档（§11）：提供基准 BodySize，预览与源码编辑区同档（--vex-writing-size）。 */
    root.style.setProperty('--vex-writing-size',{compact:'13px',normal:'15px',relaxed:'17px'}[state.mdsize]||'15px');
    if(changed && root.dataset.theme){
      themeFrame=requestAnimationFrame(function(){
        themeFrame=requestAnimationFrame(function(){
          root.classList.remove('theme-switching');
          themeFrame=0;
        });
      });
    }
    try{VexStorage.setItem(storeKey,JSON.stringify(state));}catch(_){}
    document.querySelectorAll('#vex-theme-panel [data-mode],#vex-theme-panel [data-palette],#vex-theme-panel [data-density],#vex-theme-panel [data-mdsize]').forEach(button=>{
      var active=button.dataset.mode?button.dataset.mode===state.mode:button.dataset.palette?button.dataset.palette===state.palette:button.dataset.density?button.dataset.density===state.density:button.dataset.mdsize===state.mdsize;
      var palette=button.dataset.palette&&VexPalettes.find(p=>p.key===button.dataset.palette);
      button.title=palette?palette[window.VexL?.culture==='en-US'?'en':'label']:word(button.dataset.resource);
      button.setAttribute('aria-label',button.title);
      if(button.matches('input[type="radio"]')){
        button.checked=active;
        button.nextElementSibling.textContent=word(button.dataset.resource);
      }else{
        button.classList.toggle('on',active);button.setAttribute('aria-pressed',String(active));
      }
    });
    // Language controls do not need to be walked on every palette/mode switch.
    if(syncLanguage){
      document.querySelectorAll('#vex-language-menu [data-language]').forEach(button=>{
        var active=button.dataset.language===window.VexL?.culture;
        button.setAttribute('aria-checked',String(active));button.classList.toggle('on',active);
      });
      var language=document.querySelector('#vex-language-menu>summary');
      if(language){language.title=word('MenuLanguage');language.setAttribute('aria-label',language.title);}
    }
  }
  /* 自定义配色（规则 §7 编写层 4 色）：持久化于 VexStorage，启动时恢复派生结果并注册进配色目录。 */
  var CUSTOM='user-custom',customStore='vex-custom-palette';
  function customFull(base,mode){return Object.assign({},base,VexPaletteRulesData.statusDefaults[mode]);}
  function customVars(base,mode){return VexPaletteRules.deriveVars(customFull(base,mode),mode,VexPaletteRulesData.derivation[mode]);}
  function customCss(base,mode){
    var vars=customVars(base,mode);
    return ':root[data-theme="'+mode+'"][data-palette="'+CUSTOM+'"]{'+Object.entries(vars).map(function(pair){return '--'+pair[0]+':'+pair[1];}).join(';')+'}';
  }
  function customSwatch(base){
    var defs=VexPaletteRulesData.statusDefaults.light;
    return [base.paper,base.surface,base.ink,base.accent,defs.danger,defs.success,defs.warning,defs.info];
  }
  function registerCustom(saved){
    var varsLight=customVars(saved.light,'light'),varsDark=customVars(saved.dark,'dark');
    var entry={key:CUSTOM,label:saved.name||'自定义',en:'Custom',note:saved.note||'我的配色 · 实时派生',recommended:false,
      light:saved.light.accent,dark:saved.dark.accent,swatch:customSwatch(saved.light),
      bg:varsLight.bg,paper:saved.light.paper,panel:saved.light.surface,fg:saved.light.ink,muted:varsLight.muted,
      darkBg:varsDark.bg,darkPaper:saved.dark.paper,darkPanel:saved.dark.surface,darkFg:saved.dark.ink,darkMuted:varsDark.muted};
    var known=VexPalettes.some(function(p){return p.key===CUSTOM;});
    if(!known){
      VexPalettes.push(entry);palettes.push([entry.key,entry.label,entry.swatch]);
    }else{
      VexPalettes=VexPalettes.map(function(p){return p.key===CUSTOM?entry:p;});
      palettes=palettes.map(function(item){return item[0]===CUSTOM?[entry.key,entry.label,entry.swatch]:item;});
    }
    renderPaletteGrid();
    var style=document.getElementById('custom-palette-style');
    if(!style){style=document.createElement('style');style.id='custom-palette-style';document.head.append(style);}
    style.textContent=customCss(saved.light,'light')+'\n'+customCss(saved.dark,'dark');
  }
  function readCustom(){try{return JSON.parse(VexStorage.getItem(customStore)||'null');}catch(_){return null;}}
  /* 界面配色网格渲染：自定义卡带 ✎/× 工具；无自定义时末尾显示"+"新增块。（IIFE 顶层，启动恢复时也可调用） */
  function renderPaletteGrid(){
    var grid=document.querySelector('#vex-appearance-menu .studio-palettes');
    if(!grid)return;
    grid.innerHTML=palettes.map(function(item){
      var info=VexPalettes.find(function(p){return p.key===item[0];});
      var card='<button type="button" data-palette="'+item[0]+'" aria-label="'+item[1]+'">'+(info.recommended?'<em class="palette-badge">推荐</em>':'')+swatchDots(item[2])+'<span class="palette-label"><b>'+item[1]+'</b><small>'+info.note+'</small></span></button>';
      if(item[0]===CUSTOM)card='<div class="palette-wrap">'+card+'<span class="palette-card-tools"><button type="button" data-edit-palette title="编辑此配色" aria-label="编辑自定义配色">✎</button><button type="button" data-delete-palette title="删除此配色" aria-label="删除自定义配色">×</button></span></div>';
      return card;
    }).join('')+(readCustom()?'':'<button type="button" class="palette-add" id="add-custom-palette" aria-label="新增自定义配色" title="新增自定义配色"><span>＋</span><small>新增配色</small></button>');
    var current=grid.querySelector('[data-palette="'+state.palette+'"]');
    if(current)current.setAttribute('aria-pressed','true');
  }
  function restoreCustom(){
    if(!window.VexPaletteRules||!window.VexPaletteRulesData)return;
    var saved=readCustom();
    if(saved&&saved.light&&saved.dark)registerCustom(saved);
  }
  window.VexTheme={
    state:state,
    setMode(value){state.mode=value;apply(false);},
    setPalette(value){state.palette=VexPaletteKey(value);apply(false);},
    /* 两个独立档位：界面密度只作用于界面控件；渲染密度（setMdSize）只作用于 Markdown 渲染。 */
    setDensity(value){if(densities.some(item=>item[0]===value)){state.density=value;apply(false);if(window.VexPreferences&&VexPreferences.values.uiDensity!==value)VexPreferences.set('uiDensity',value);}},
    setMdSize(value){if(mdsizes.some(item=>item[0]===value)){state.mdsize=value;apply(false);if(window.VexPreferences&&VexPreferences.values.mdSize!==value)VexPreferences.set('mdSize',value);}},
    /* 供偏好面板复用的自定义配色 API（增加/修改 = upsert，清除 = 删除）。 */
    applyCustomPalette(saved){if(!saved||!saved.light||!saved.dark)return false;VexStorage.setItem(customStore,JSON.stringify(saved));registerCustom(saved);VexTheme.setPalette(CUSTOM);return true;},
    clearCustomPalette(){VexStorage.removeItem(customStore);var style=document.getElementById('custom-palette-style');if(style)style.remove();var index=VexPalettes.findIndex(function(p){return p.key===CUSTOM;});if(index>=0){VexPalettes.splice(index,1);palettes=palettes.filter(function(item){return item[0]!==CUSTOM;});}renderPaletteGrid();if(state.palette===CUSTOM)VexTheme.setPalette(VexPaletteRulesData.default);},
    readCustomPalette:readCustom
  };
  restoreCustom();apply(false);
  system.addEventListener('change',()=>{if(state.mode==='system')apply(false);});
  window.addEventListener('storage',event=>{if(event.key===storeKey)try{Object.assign(state,JSON.parse(event.newValue||'{}'));apply(true);}catch(_){}});
  window.addEventListener('vex-language-changed',()=>apply(true));
  window.addEventListener('keydown',event=>{var index=Number(event.key)-1;if(event.altKey&&!event.ctrlKey&&index>=0&&index<palettes.length)VexTheme.setPalette(palettes[index][0]);if(event.key==='Escape'){var menu=document.getElementById('vex-language-menu');if(menu&&menu.open){event.preventDefault();menu.open=false;}}},true);
  document.addEventListener('DOMContentLoaded',()=>{
    var windowPanel=document.createElement('section');windowPanel.id='prototype-window-state';windowPanel.hidden=true;windowPanel.innerHTML='<p></p><button type="button" id="restore-window"></button>';document.body.append(windowPanel);
    windowPanel.querySelector('button').onclick=()=>{document.querySelector('.app-window').hidden=false;windowPanel.hidden=true;document.querySelector('#caption-btns button').focus();};
    document.getElementById('caption-btns').addEventListener('click',event=>{var button=event.target.closest('[data-action]');if(!button)return;event.stopImmediatePropagation();var app=document.querySelector('.app-window'),action=button.dataset.action;if((action==='close'||action==='exit-app')&&window.VexProto){VexProto.handleAction('exit-app');return;}if(action==='maximize'){app.classList.toggle('expanded');return;}var words=({'zh-CN':['窗口已最小化','窗口已关闭','还原窗口','重新打开'],'zh-Hant':['視窗已最小化','視窗已關閉','還原視窗','重新開啟'],'en-US':['Window minimized','Window closed','Restore window','Reopen'],'ja-JP':['ウィンドウを最小化しました','ウィンドウを閉じました','ウィンドウを復元','もう一度開く']})[VexL.culture];app.hidden=true;windowPanel.dataset.state=action;windowPanel.querySelector('p').textContent=words[action==='close'?1:0];windowPanel.querySelector('button').textContent=words[action==='close'?3:2];windowPanel.hidden=false;windowPanel.querySelector('button').focus();},true);
    var panel=document.createElement('div');panel.id='vex-theme-panel';panel.className='studio-title-controls';
    /* 语言菜单名片化：语言缩写徽标 + 自称，选中项徽标反色。 */
    var langChips={'zh-CN':'中','zh-Hant':'繁','en-US':'EN','ja-JP':'日'};
    panel.innerHTML='<button type="button" class="icon-button find-launch" data-action="find" title="查找当前正文 · Ctrl+F" aria-label="查找当前正文"><svg class="ico" aria-hidden="true"><use href="#i-search"/></svg></button><details id="vex-appearance-menu"><summary title="外观：主题与配色"><svg class="ico" aria-hidden="true"><use href="#i-palette"/></svg><span>外观</span></summary><div class="appearance-choices"><strong>界面密度</strong><div class="density-options" role="group" aria-label="Density">'+densities.map(function(item){return '<button type="button" data-density="'+item[0]+'">'+item[1]+'</button>';}).join('')+'</div><strong>明暗模式</strong><div class="density-options" role="group" aria-label="Theme mode">'+modes.map(function(item){return '<button type="button" data-mode="'+item[0]+'">'+word(item[1])+'</button>';}).join('')+'</div><strong>界面配色</strong><div class="studio-palettes" role="group" aria-label="Palette"></div></div></details><details id="vex-language-menu" class="studio-language"><summary>文<span aria-hidden="true">A</span></summary><div role="menu">'+Object.entries(VEX_DATA.languages).map(([code])=>'<button type="button" role="menuitemradio" data-language="'+code+'"><i class="lang-chip" aria-hidden="true">'+(langChips[code]||code.slice(0,2))+'</i><span>'+({"zh-CN":"简体中文","zh-Hant":"繁體中文","en-US":"English","ja-JP":"日本語"}[code]||code)+'</span></button>').join('')+'</div></details>';
    document.getElementById('caption-btns').before(panel);
    /* 界面配色网格渲染见 IIFE 顶层 renderPaletteGrid()。 */
    window.VexThemeRenderPaletteGrid=renderPaletteGrid;
    panel.addEventListener('click',event=>{
      var button=event.target.closest('button');if(!button)return;
      if(button.dataset.mode){VexTheme.setMode(button.dataset.mode);return;}
      if(button.id==='add-custom-palette'){openPaletteDialog(null);return;}
      if(button.hasAttribute('data-edit-palette')){openPaletteDialog(readCustom());return;}
      if(button.hasAttribute('data-delete-palette')){VexTheme.clearCustomPalette();if(window.VexPreferences)VexPreferences.set('palette',VexPaletteRulesData.default);return;}
      if(button.dataset.palette)VexTheme.setPalette(button.dataset.palette);
      if(button.dataset.density)VexTheme.setDensity(button.dataset.density);
      if(button.dataset.mdsize)VexTheme.setMdSize(button.dataset.mdsize);
      if(button.dataset.language){VexL.set(button.dataset.language);document.getElementById('vex-language-menu').open=false;}
    });
    /* 新增/编辑配色对话框（双明暗 8 色，规则 §7）：独立于弹出层，保存即派生校验并应用。 */
    var paletteDialog=document.createElement('section');paletteDialog.id='palette-dialog';paletteDialog.hidden=true;
    paletteDialog.innerHTML='<div class="palette-dialog-card" role="dialog" aria-modal="true" aria-labelledby="palette-dialog-title"><strong id="palette-dialog-title">新增配色</strong>'
      +'<label class="pd-field">名称<input type="text" data-pf="name" maxlength="12" placeholder="如：暖纸"></label>'
      +'<label class="pd-field">描述<input type="text" data-pf="note" maxlength="20" placeholder="可选，显示在配色卡片上"></label>'
      +'<div class="pd-group"><strong>浅色态</strong><div class="pd-colors">'+[['paper','纸面'],['surface','面板'],['ink','墨色'],['accent','强调']].map(function(pair){return '<label>'+pair[1]+'<input type="color" data-pf="light-'+pair[0]+'"></label>';}).join('')+'</div></div>'
      +'<div class="pd-group"><strong>深色态</strong><div class="pd-colors">'+[['paper','纸面'],['surface','面板'],['ink','墨色'],['accent','强调']].map(function(pair){return '<label>'+pair[1]+'<input type="color" data-pf="dark-'+pair[0]+'"></label>';}).join('')+'</div></div>'
      +'<p class="pd-report" id="palette-dialog-report">4 色 × 双明暗编写（规则 §7），其余语义键自动派生。</p>'
      +'<div class="pd-actions"><button type="button" class="btn" id="pd-cancel">取消</button><button type="button" class="btn primary" id="pd-apply">保存并应用</button></div></div>';
    document.body.append(paletteDialog);
    function pdField(name){return paletteDialog.querySelector('[data-pf="'+name+'"]');}
    function openPaletteDialog(saved){
      var defaults=VexPaletteRulesData.palettes[VexPaletteRulesData.default];
      var light=(saved&&saved.light)||defaults.light,dark=(saved&&saved.dark)||defaults.dark;
      pdField('name').value=(saved&&saved.name)||'';
      pdField('note').value=(saved&&saved.note)||'';
      ['paper','surface','ink','accent'].forEach(function(key){pdField('light-'+key).value=light[key];pdField('dark-'+key).value=dark[key];});
      paletteDialog.querySelector('#palette-dialog-title').textContent=saved?'编辑配色':'新增配色';
      paletteDialog.querySelector('#palette-dialog-report').textContent='4 色 × 双明暗编写（规则 §7），其余语义键自动派生。';
      paletteDialog.hidden=false;pdField('light-accent').focus();
    }
    function closePaletteDialog(){paletteDialog.hidden=true;}
    paletteDialog.querySelector('#pd-cancel').onclick=closePaletteDialog;
    paletteDialog.addEventListener('keydown',function(event){if(event.key==='Escape'){event.stopPropagation();closePaletteDialog();}});
    paletteDialog.querySelector('#pd-apply').onclick=function(){
      var pick=function(mode){var base={};['paper','surface','ink','accent'].forEach(function(key){base[key]=pdField(mode+'-'+key).value.toUpperCase();});return base;};
      var saved={name:pdField('name').value.trim()||'自定义',note:pdField('note').value.trim(),light:pick('light'),dark:pick('dark')};
      var issues=VexPaletteRules.validate(customFull(saved.light,'light'),'light',VexPaletteRulesData.statusDefaults,VexPaletteRulesData.derivation.light)
        .concat(VexPaletteRules.validate(customFull(saved.dark,'dark'),'dark',VexPaletteRulesData.statusDefaults,VexPaletteRulesData.derivation.dark));
      var report=paletteDialog.querySelector('#palette-dialog-report');
      if(!window.VexTheme.applyCustomPalette(saved)){report.textContent='⚠ 保存失败，请重试';return;}
      if(window.VexPreferences)VexPreferences.set('palette',CUSTOM);
      report.textContent=issues.length?('⚠ '+issues.join('；')):'✓ 对比度校验通过（规则 §5）';
      if(!issues.length)closePaletteDialog();
    };
    renderPaletteGrid();
    document.addEventListener('click',event=>{if(!event.target.closest('#vex-language-menu'))document.getElementById('vex-language-menu').open=false;if(!event.target.closest('#vex-appearance-menu')&&!event.target.closest('#palette-dialog'))document.getElementById('vex-appearance-menu').open=false;});
    document.addEventListener('keydown',event=>{if(event.key==='Escape'){var menu=document.getElementById('vex-appearance-menu');if(menu&&menu.open){event.preventDefault();menu.open=false;menu.querySelector('summary').focus();}}},true);apply(true);
  });
})();
