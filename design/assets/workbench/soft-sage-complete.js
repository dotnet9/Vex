/* Vex · Soft Sage Complete interactions: workspace presets and reading themes. */
(function(){
  'use strict';
  function init(){
    var root=document.documentElement,status=document.querySelector('.document-statusbar');
    if(!status||document.querySelector('.sage-layout-tools'))return;
    var host=document.querySelector('.sage-status-tools')||status;
    var layouts=document.createElement('div');layouts.className='sage-layout-tools';layouts.setAttribute('role','group');layouts.setAttribute('aria-label','工作区布局');
    layouts.innerHTML='<span class="sage-tool-label">布局</span><button type="button" data-sage-layout="write" aria-pressed="true">写作</button><button type="button" data-sage-layout="read" aria-pressed="false">阅读</button><button type="button" data-sage-layout="map" aria-pressed="false">导图</button><button type="button" data-sage-layout="focus" aria-pressed="false">专注</button>';
    var themes=document.createElement('div');themes.className='sage-reading-themes';themes.setAttribute('role','group');themes.setAttribute('aria-label','阅读主题');
    themes.innerHTML='<span class="sage-tool-label">阅读</span><button type="button" data-sage-theme="paper" aria-pressed="true">纸感</button><button type="button" data-sage-theme="minimal" aria-pressed="false">极简</button><button type="button" data-sage-theme="eye" aria-pressed="false">护眼</button>';
    host.append(layouts,themes);
    function currentView(){var active=document.querySelector('.view-switch button[aria-selected="true"]');return active?active.dataset.view:'edit';}
    function setView(view){var button=document.querySelector('.view-switch button[data-view="'+view+'"]');if(button)button.click();}
    function update(){
      var view=currentView(),focus=root.classList.contains('sage-focus-mode');
      layouts.querySelectorAll('button').forEach(function(button){
        var key=button.dataset.sageLayout,active=key==='write'?view==='edit'&&!focus:key==='read'?view==='read':key==='map'?view==='mindmap':focus;
        button.setAttribute('aria-pressed',String(active));
      });
      themes.querySelectorAll('button').forEach(function(button){button.setAttribute('aria-pressed',String((root.dataset.readingTheme||'paper')===button.dataset.sageTheme));});
    }
    layouts.addEventListener('click',function(event){
      var button=event.target.closest('button[data-sage-layout]');if(!button)return;
      var key=button.dataset.sageLayout;
      if(key==='write'){root.classList.remove('sage-focus-mode');setView('edit');}
      if(key==='read'){root.classList.remove('sage-focus-mode');setView('read');}
      if(key==='map'){root.classList.remove('sage-focus-mode');setView('mindmap');}
      if(key==='focus'){setView('edit');root.classList.add('sage-focus-mode');}
      update();
    });
    themes.addEventListener('click',function(event){
      var button=event.target.closest('button[data-sage-theme]');if(!button)return;
      root.dataset.readingTheme=button.dataset.sageTheme;update();
    });
    var focusObserver=new MutationObserver(update);focusObserver.observe(root,{attributes:true,attributeFilter:['class','data-reading-theme']});
    document.addEventListener('click',function(event){if(event.target.closest('.view-switch'))requestAnimationFrame(update);});
    document.addEventListener('keydown',function(event){if(event.key==='Escape')requestAnimationFrame(update);});
    update();
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init,{once:true});else init();
})();