/* 标题栏统一提供三态、八色点和可扩展语言菜单。 */
(function () {
  var palettes=[['dotnet','ThemeDotNet','#512BD4'],['aurora','ThemeAurora','linear-gradient(135deg, #8968FF, #26D4CC)'],['ember','ThemeEmber','#BD674D'],['default','ThemeDefault','#0064FA'],['aquatic','ThemeAquatic','#008D9E'],['desert','ThemeDesert','#B57532'],['dusk','ThemeDusk','#7A5CC4'],['night-sky','ThemeNightSky','#2B6CB0']];
  var modes=[['light','ThemeLight','☀'],['dark','ThemeDark','☾'],['system','ThemeSystem','◐']];
  var storeKey='vex-design-appearance',system=matchMedia('(prefers-color-scheme: dark)'),state={mode:'system',palette:'dotnet'};
  try{Object.assign(state,JSON.parse(localStorage.getItem(storeKey)||'{}'));}catch(_){}
  var query=new URLSearchParams(location.search),theme=query.get('theme');
  if(modes.some(item=>item[0]===theme))state.mode=theme;else if(palettes.some(item=>item[0]===theme))state.palette=theme;
  if(query.has('palette'))state.palette=query.get('palette');
  function word(key){return window.VexL?VexL.get(key):key;}
  function apply(){
    if(!modes.some(item=>item[0]===state.mode))state.mode='system';if(!palettes.some(item=>item[0]===state.palette))state.palette='dotnet';
    document.documentElement.dataset.themeMode=state.mode;document.documentElement.dataset.theme=state.mode==='system'?(system.matches?'dark':'light'):state.mode;document.documentElement.dataset.palette=state.palette;
    try{localStorage.setItem(storeKey,JSON.stringify(state));}catch(_){}
    var toggle=document.getElementById('vex-mode-toggle');if(toggle){var selectedMode=modes.find(item=>item[0]===state.mode);toggle.dataset.mode=state.mode;toggle.dataset.resource=selectedMode[1];toggle.textContent=selectedMode[2];}
    document.querySelectorAll('#vex-theme-panel [data-mode],#vex-theme-panel [data-palette]').forEach(button=>{var active=button.dataset.mode?button.dataset.mode===state.mode:button.dataset.palette===state.palette;button.classList.toggle('on',active);button.setAttribute('aria-pressed',String(active));button.title=word(button.dataset.resource);button.setAttribute('aria-label',button.title);});
    document.querySelectorAll('#vex-language-menu [data-language]').forEach(button=>{var active=button.dataset.language===window.VexL?.culture;button.setAttribute('aria-checked',String(active));button.classList.toggle('on',active);});
    var language=document.querySelector('#vex-language-menu>summary');if(language){language.title=word('MenuLanguage');language.setAttribute('aria-label',language.title);}
  }
  window.VexTheme={setMode(value){state.mode=value;apply();},setPalette(value){state.palette=value;apply();}};apply();
  system.addEventListener('change',()=>{if(state.mode==='system')apply();});
  window.addEventListener('storage',event=>{if(event.key===storeKey)try{Object.assign(state,JSON.parse(event.newValue||'{}'));apply();}catch(_){}});
  window.addEventListener('vex-language-changed',apply);
  window.addEventListener('keydown',event=>{var index=Number(event.key)-1;if(event.altKey&&!event.ctrlKey&&index>=0&&index<palettes.length)VexTheme.setPalette(palettes[index][0]);if(event.key==='Escape'){var menu=document.getElementById('vex-language-menu');if(menu)menu.open=false;}});
  document.addEventListener('DOMContentLoaded',()=>{
    var windowPanel=document.createElement('section');windowPanel.id='prototype-window-state';windowPanel.hidden=true;windowPanel.innerHTML='<p></p><button type="button" id="restore-window"></button>';document.body.append(windowPanel);
    windowPanel.querySelector('button').onclick=()=>{document.querySelector('.app-window').hidden=false;windowPanel.hidden=true;document.querySelector('#caption-btns button').focus();};
    document.getElementById('caption-btns').addEventListener('click',event=>{var button=event.target.closest('[data-action]');if(!button)return;event.stopImmediatePropagation();var app=document.querySelector('.app-window'),action=button.dataset.action;if(action==='maximize'){app.classList.toggle('expanded');return;}var words=({'zh-CN':['窗口已最小化','窗口已关闭','还原窗口','重新打开'],'zh-Hant':['視窗已最小化','視窗已關閉','還原視窗','重新開啟'],'en-US':['Window minimized','Window closed','Restore window','Reopen'],'ja-JP':['ウィンドウを最小化しました','ウィンドウを閉じました','ウィンドウを復元','もう一度開く']})[VexL.culture];app.hidden=true;windowPanel.dataset.state=action;windowPanel.querySelector('p').textContent=words[action==='close'?1:0];windowPanel.querySelector('button').textContent=words[action==='close'?3:2];windowPanel.hidden=false;windowPanel.querySelector('button').focus();},true);
    var panel=document.createElement('div');panel.id='vex-theme-panel';panel.className='studio-title-controls';
    panel.innerHTML='<div class="studio-modes" role="group" aria-label="Theme mode">'+'<button type="button" id="vex-mode-toggle" data-mode="system" data-resource="ThemeSystem">◐</button>'+'</div><div class="studio-palettes" role="group" aria-label="Palette">'+palettes.map(item=>'<button type="button" data-palette="'+item[0]+'" data-resource="'+item[1]+'"><i style="background:'+item[2]+'"></i></button>').join('')+'</div><details id="vex-language-menu" class="studio-language"><summary>文<span aria-hidden="true">A</span></summary><div role="menu">'+Object.entries(VEX_DATA.languages).map(([code])=>'<button type="button" role="menuitemradio" data-language="'+code+'">'+({"zh-CN":"简体中文","zh-Hant":"繁體中文","en-US":"English","ja-JP":"日本語"}[code]||code)+'</button>').join('')+'</div></details>';
    document.getElementById('caption-btns').before(panel);
    panel.addEventListener('click',event=>{var button=event.target.closest('button');if(!button)return;if(button.dataset.mode)VexTheme.setMode(({system:'light',light:'dark',dark:'system'})[state.mode]);if(button.dataset.palette)VexTheme.setPalette(button.dataset.palette);if(button.dataset.language){VexL.set(button.dataset.language);document.getElementById('vex-language-menu').open=false;}});
    document.addEventListener('click',event=>{if(!event.target.closest('#vex-language-menu'))document.getElementById('vex-language-menu').open=false;});apply();
  });
})();
