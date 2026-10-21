/* 外观集中在折叠面板中，保留三态、十六套配色和语言入口。 */
(function () {
  /* 推荐配色置顶（角标标识），其余按原顺序跟随。 */
  var palettes=[].concat(VexPalettes.filter(function(p){return p.recommended;}),VexPalettes.filter(function(p){return !p.recommended;})).map(function(p){return [p.key,p.label,p.light];});
  var modes=[['light','ThemeLight','☀'],['dark','ThemeDark','☾'],['system','ThemeSystem','◐']];
  var storeKey='vex-design-appearance',system=matchMedia('(prefers-color-scheme: dark)'),state={mode:'light',palette:'weft'};
  try{Object.assign(state,JSON.parse(VexStorage.getItem(storeKey)||'{}'));}catch(_){}
  var query=new URLSearchParams(location.search),theme=query.get('theme');
  if(modes.some(item=>item[0]===theme))state.mode=theme;else if(palettes.some(item=>item[0]===theme))state.palette=theme;
  if(query.has('palette'))state.palette=query.get('palette');state.palette=VexPaletteKey(state.palette);
  function word(key){return window.VexL?VexL.get(key):key;}
  var themeFrame = 0;
  function apply(syncLanguage){
    if(!modes.some(item=>item[0]===state.mode))state.mode='system';
    if(!palettes.some(item=>item[0]===state.palette))state.palette='weft';
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
    if(changed && root.dataset.theme){
      themeFrame=requestAnimationFrame(function(){
        themeFrame=requestAnimationFrame(function(){
          root.classList.remove('theme-switching');
          themeFrame=0;
        });
      });
    }
    try{VexStorage.setItem(storeKey,JSON.stringify(state));}catch(_){}
    document.querySelectorAll('#vex-theme-panel [data-mode],#vex-theme-panel [data-palette]').forEach(button=>{
      var active=button.dataset.mode?button.dataset.mode===state.mode:button.dataset.palette===state.palette;
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
  window.VexTheme={state:state,setMode(value){state.mode=value;apply(false);},setPalette(value){state.palette=VexPaletteKey(value);apply(false);}};apply(false);
  system.addEventListener('change',()=>{if(state.mode==='system')apply(false);});
  window.addEventListener('storage',event=>{if(event.key===storeKey)try{Object.assign(state,JSON.parse(event.newValue||'{}'));apply(true);}catch(_){}});
  window.addEventListener('vex-language-changed',()=>apply(true));
  window.addEventListener('keydown',event=>{var index=Number(event.key)-1;if(event.altKey&&!event.ctrlKey&&index>=0&&index<palettes.length)VexTheme.setPalette(palettes[index][0]);if(event.key==='Escape'){var menu=document.getElementById('vex-language-menu');if(menu)menu.open=false;}});
  document.addEventListener('DOMContentLoaded',()=>{
    var windowPanel=document.createElement('section');windowPanel.id='prototype-window-state';windowPanel.hidden=true;windowPanel.innerHTML='<p></p><button type="button" id="restore-window"></button>';document.body.append(windowPanel);
    windowPanel.querySelector('button').onclick=()=>{document.querySelector('.app-window').hidden=false;windowPanel.hidden=true;document.querySelector('#caption-btns button').focus();};
    document.getElementById('caption-btns').addEventListener('click',event=>{var button=event.target.closest('[data-action]');if(!button)return;event.stopImmediatePropagation();var app=document.querySelector('.app-window'),action=button.dataset.action;if((action==='close'||action==='exit-app')&&window.VexProto){VexProto.handleAction('exit-app');return;}if(action==='maximize'){app.classList.toggle('expanded');return;}var words=({'zh-CN':['窗口已最小化','窗口已关闭','还原窗口','重新打开'],'zh-Hant':['視窗已最小化','視窗已關閉','還原視窗','重新開啟'],'en-US':['Window minimized','Window closed','Restore window','Reopen'],'ja-JP':['ウィンドウを最小化しました','ウィンドウを閉じました','ウィンドウを復元','もう一度開く']})[VexL.culture];app.hidden=true;windowPanel.dataset.state=action;windowPanel.querySelector('p').textContent=words[action==='close'?1:0];windowPanel.querySelector('button').textContent=words[action==='close'?3:2];windowPanel.hidden=false;windowPanel.querySelector('button').focus();},true);
    var panel=document.createElement('div');panel.id='vex-theme-panel';panel.className='studio-title-controls';
    panel.innerHTML='<button type="button" class="icon-button find-launch" data-action="find" title="查找当前正文 · Ctrl+F" aria-label="查找当前正文"><svg class="ico" aria-hidden="true"><use href="#i-search"/></svg></button><details id="vex-appearance-menu"><summary title="外观：主题与配色"><svg class="ico" aria-hidden="true"><use href="#i-palette"/></svg><span>外观</span></summary><div class="appearance-choices"><fieldset class="studio-modes"><legend>明暗模式</legend><div class="theme-mode-options">'+modes.map(item=>'<label><input type="radio" name="vex-theme-mode" value="'+item[0]+'" data-mode="'+item[0]+'" data-resource="'+item[1]+'"><span>'+word(item[1])+'</span></label>').join('')+'</div></fieldset><strong>界面配色</strong><div class="studio-palettes" role="group" aria-label="Palette">'+palettes.map(item=>{var info=VexPalettes.find(p=>p.key===item[0]);return '<button type="button" data-palette="'+item[0]+'" aria-label="'+item[1]+'">'+(info.recommended?'<em class="palette-badge">推荐</em>':'')+'<i style="background:'+item[2]+'"></i><span class="palette-label"><b>'+item[1]+'</b><small>'+info.note+'</small></span></button>';}).join('')+'</div></div></details><details id="vex-language-menu" class="studio-language"><summary>文<span aria-hidden="true">A</span></summary><div role="menu">'+Object.entries(VEX_DATA.languages).map(([code])=>'<button type="button" role="menuitemradio" data-language="'+code+'">'+({"zh-CN":"简体中文","zh-Hant":"繁體中文","en-US":"English","ja-JP":"日本語"}[code]||code)+'</button>').join('')+'</div></details>';
    document.getElementById('caption-btns').before(panel);
    panel.addEventListener('change',event=>{var input=event.target;if(input.matches('input[name="vex-theme-mode"]'))VexTheme.setMode(input.value);});
    panel.addEventListener('click',event=>{var button=event.target.closest('button');if(!button)return;if(button.dataset.palette)VexTheme.setPalette(button.dataset.palette);if(button.dataset.language){VexL.set(button.dataset.language);document.getElementById('vex-language-menu').open=false;}});
    document.addEventListener('click',event=>{if(!event.target.closest('#vex-language-menu'))document.getElementById('vex-language-menu').open=false;if(!event.target.closest('#vex-appearance-menu'))document.getElementById('vex-appearance-menu').open=false;});
    document.addEventListener('keydown',event=>{if(event.key==='Escape'){var menu=document.getElementById('vex-appearance-menu');if(menu.open){menu.open=false;menu.querySelector('summary').focus();}}});apply(true);
  });
})();
