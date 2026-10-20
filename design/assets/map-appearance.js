/* Structure and visual treatment are independent of Markdown typography. */
(function () {
  'use strict';
  var styles = [
    {key:'quiet',label:'清雅'}, {key:'colorful',label:'缤纷'}, {key:'outline',label:'线框'},
    {key:'capsule',label:'胶囊'}, {key:'underline',label:'脉络'}, {key:'mono',label:'黑白'}
  ];
  var colors = ['var(--accent)','var(--branch-secondary, var(--accent))','var(--branch-tertiary, var(--accent))','var(--muted)'];
  function miniature(layout, style) {
    var tree = {children:[{children:[{},{}]},{children:[{},{}]}]};
    var graph = VexMapLayout.layout(tree, {mode:layout, measure:function (node) {node.width=52;node.height=22;}});
    return '<svg class="map-miniature" data-map-style="'+style+'" viewBox="-12 -12 '+(graph.width+24)+' '+(graph.height+24)+'" aria-hidden="true">'+graph.links.map(function (link,index) {
      return '<path class="map-preview-link" style="--map-branch:'+colors[index%colors.length]+'" d="'+link.path+'"/>';
    }).join('')+graph.nodes.map(function (node,index) {
      return '<rect class="map-preview-node '+(node===tree?'map-preview-root':'')+'" style="--map-branch:'+colors[index%colors.length]+'" x="'+node.x+'" y="'+node.y+'" width="'+node.width+'" height="'+node.height+'" rx="4"/>';
    }).join('')+'</svg>';
  }
  function create(adapter) {
    var style = 'quiet', trigger, tab = 'structure';
    var isolated = new URLSearchParams(location.search).get('preview') === '1';
    try { var stored = isolated ? null : localStorage.getItem('vex-prototype-map-style'); if (styles.some(function (item) {return item.key===stored;})) style=stored; } catch (_) {}
    var panel = document.createElement('section'); panel.id='map-appearance-panel'; panel.className='map-appearance-popover'; panel.hidden=true;
    panel.setAttribute('role','dialog');panel.setAttribute('aria-label','导图样式');
    panel.innerHTML='<header class="appearance-heading"><strong>导图样式</strong><button class="vex-icon-button" data-map-close aria-label="关闭导图样式"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18"/></svg></button></header><div class="appearance-tabs" role="tablist" aria-label="导图外观"><button role="tab" id="map-tab-structure" data-map-tab="structure" aria-controls="map-structure-choices">结构</button><button role="tab" id="map-tab-style" data-map-tab="style" aria-controls="map-style-choices">风格</button></div><div class="map-choice-grid" id="map-structure-choices" role="tabpanel" aria-labelledby="map-tab-structure"></div><div class="map-choice-grid" id="map-style-choices" role="tabpanel" aria-labelledby="map-tab-style" hidden></div>';
    adapter.parent.appendChild(panel);
    function apply() {
      adapter.scene.dataset.mapStyle=style;
      document.querySelectorAll('[data-map-style-label]').forEach(function (label) {label.textContent=styles.find(function (item) {return item.key===style;}).label;});
      panel.querySelectorAll('[data-map-layout]').forEach(function (button) {button.setAttribute('aria-pressed',String(button.dataset.mapLayout===adapter.getLayout()));});
      panel.querySelectorAll('[data-map-style-choice]').forEach(function (button) {button.setAttribute('aria-pressed',String(button.dataset.mapStyleChoice===style));});
    }
    function render() {
      panel.querySelector('#map-structure-choices').innerHTML=VexMapLayout.items.map(function (item) {return '<button class="map-choice" data-map-layout="'+item.key+'">'+miniature(item.key,style)+'<strong>'+item.label+'</strong></button>';}).join('');
      panel.querySelector('#map-style-choices').innerHTML=styles.map(function (item) {return '<button class="map-choice" data-map-style-choice="'+item.key+'">'+miniature(adapter.getLayout(),item.key)+'<strong>'+item.label+'</strong></button>';}).join('');
      apply();
    }
    function selectTab(key) {
      tab=key; panel.querySelectorAll('[data-map-tab]').forEach(function (button) {var active=button.dataset.mapTab===tab;button.setAttribute('aria-selected',String(active));button.tabIndex=active?0:-1;});
      panel.querySelector('#map-structure-choices').hidden=tab!=='structure';panel.querySelector('#map-style-choices').hidden=tab!=='style';
    }
    function close() {panel.hidden=true;if(trigger)trigger.setAttribute('aria-expanded','false');}
    function choose(key) {
      if(!styles.some(function(item){return item.key===key;}))return;
      style=key;if(!isolated&&!adapter.isTemporary?.())try{localStorage.setItem('vex-prototype-map-style',style);}catch(_){}render();
    }
    function position() {
      if(panel.hidden)return;
      var host=adapter.parent.getBoundingClientRect(), rect=trigger.getBoundingClientRect();
      var phone=document.body.classList.contains('device-phone');
      var below=host.bottom-rect.bottom-20,above=rect.top-host.top-20,flip=below<320&&above>below;
      panel.style.left=phone?'8px':Math.max(8,Math.min(host.width-panel.offsetWidth-8,rect.right-host.left-panel.offsetWidth))+'px';panel.style.right='auto';
      if(phone)panel.style.width=(host.width-16)+'px';
      panel.style.maxHeight=Math.max(180,phone?host.height-52:flip?above:below)+'px';
      panel.style.top=phone?'auto':(flip?Math.max(8,rect.top-host.top-panel.offsetHeight-8):Math.max(8,rect.bottom-host.top+8))+'px';
    }
    function toggle(button) {
      if(!panel.hidden){close();return;}
      adapter.beforeOpen?.();trigger=button;render();selectTab(tab);panel.hidden=false;trigger.setAttribute('aria-expanded','true');position();
    }
    panel.onclick=function (event) {
      var button=event.target.closest('button');if(!button)return;
      if(button.hasAttribute('data-map-close')){close();trigger?.focus({preventScroll:true});return;}
      if(button.dataset.mapTab){selectTab(button.dataset.mapTab);return;}
      if(button.dataset.mapLayout){adapter.setLayout(button.dataset.mapLayout);render();}
      if(button.dataset.mapStyleChoice)choose(button.dataset.mapStyleChoice);
    };
    panel.querySelector('.appearance-tabs').onkeydown=function (event) {
      if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key))return;event.preventDefault();selectTab(event.key==='Home'?'structure':event.key==='End'?'style':tab==='style'?'structure':'style');panel.querySelector('[data-map-tab="'+tab+'"]').focus();
    };
    document.addEventListener('pointerdown',function (event) {if(!panel.contains(event.target)&&!event.target.closest('[data-action="map-appearance"], .tour-card'))close();});
    document.addEventListener('keydown',function (event) {if(event.key==='Escape'&&!panel.hidden){close();trigger?.focus({preventScroll:true});}});
    window.addEventListener('resize',position);apply();selectTab(tab);
    return {toggle:toggle,close:close,choose:choose,sync:apply,get style(){return style;}};
  }
  window.VexMapAppearance={styles:styles,miniature:miniature,create:create};
})();
