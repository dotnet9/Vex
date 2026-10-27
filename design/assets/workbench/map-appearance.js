/* Structure and visual treatment are independent of Markdown typography. */
(function () {
  'use strict';
  // 每张卡带一行说明（Vox 改版口径）：六项里缤纷/黑白换的是颜色，线框/胶囊/脉络换的是节点形态，
  // 光看名称猜不到效果，故第二排叫「风格」而非「配色」。
  var styles = [
    {key:'quiet',label:'清雅',desc:'单色 · 跟随强调色'}, {key:'colorful',label:'缤纷',desc:'分支各一色'},
    {key:'outline',label:'线框',desc:'根节点只勾轮廓'}, {key:'capsule',label:'胶囊',desc:'圆角胶囊底'},
    {key:'underline',label:'脉络',desc:'子节点留底线'}, {key:'mono',label:'黑白',desc:'打印 · 截图'}
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
    var style = 'quiet', trigger, nudgeTimer;
    var isolated = new URLSearchParams(location.search).get('preview') === '1';
    try { var stored = isolated ? null : localStorage.getItem('vex-prototype-map-style'); if (styles.some(function (item) {return item.key===stored;})) style=stored; } catch (_) {}
    var panel = document.createElement('section'); panel.id='map-appearance-panel'; panel.className='map-appearance-popover'; panel.hidden=true;
    panel.setAttribute('role','dialog');panel.setAttribute('aria-label','导图样式');
    // 页签已删：结构与风格上下平铺，靠 ①② 步骤标签分组——选完结构顺手往下看风格，不必先点开页签。
    panel.innerHTML='<header class="appearance-heading"><strong>导图样式</strong><button class="vex-icon-button" data-map-close aria-label="关闭导图样式"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18"/></svg></button></header><div class="map-appearance-scroll"><div class="map-step-label"><i>1</i>结构<span class="hint">先选一种布局</span></div><div class="map-choice-grid" id="map-structure-choices" role="group" aria-label="导图结构"></div><div class="map-step-label" data-map-next-step><i>2</i>风格<span class="hint">线稿随上方所选结构实时重绘</span></div><div class="map-choice-grid" id="map-style-choices" role="group" aria-label="导图风格"></div></div>';
    adapter.parent.appendChild(panel);
    function apply() {
      adapter.scene.dataset.mapStyle=style;
      document.querySelectorAll('[data-map-style-label]').forEach(function (label) {label.textContent=styles.find(function (item) {return item.key===style;}).label;});
      panel.querySelectorAll('[data-map-layout]').forEach(function (button) {button.setAttribute('aria-pressed',String(button.dataset.mapLayout===adapter.getLayout()));});
      panel.querySelectorAll('[data-map-style-choice]').forEach(function (button) {button.setAttribute('aria-pressed',String(button.dataset.mapStyleChoice===style));});
    }
    function render() {
      panel.querySelector('#map-structure-choices').innerHTML=VexMapLayout.items.map(function (item) {return '<button class="map-choice" data-map-layout="'+item.key+'">'+miniature(item.key,style)+'<strong>'+item.label+'</strong></button>';}).join('');
      panel.querySelector('#map-style-choices').innerHTML=styles.map(function (item) {return '<button class="map-choice" data-map-style-choice="'+item.key+'">'+miniature(adapter.getLayout(),item.key)+'<strong>'+item.label+'</strong><span class="map-choice-purpose">'+item.desc+'</span></button>';}).join('');
      apply();
    }
    // 步骤联想：选完结构把「② 配色」点亮 900ms，视线跟着走，不用人再去找第二排。
    function nudgeNextStep() {
      var label = panel.querySelector('[data-map-next-step]'); if (!label) return;
      label.classList.add('nudge'); clearTimeout(nudgeTimer);
      nudgeTimer = setTimeout(function () { label.classList.remove('nudge'); }, 900);
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
      adapter.beforeOpen?.();trigger=button;render();panel.hidden=false;trigger.setAttribute('aria-expanded','true');position();
    }
    panel.onclick=function (event) {
      var button=event.target.closest('button');if(!button)return;
      if(button.hasAttribute('data-map-close')){close();trigger?.focus({preventScroll:true});return;}
      if(button.dataset.mapLayout){adapter.setLayout(button.dataset.mapLayout);render();nudgeNextStep();}
      if(button.dataset.mapStyleChoice)choose(button.dataset.mapStyleChoice);
    };
    document.addEventListener('pointerdown',function (event) {if(!panel.contains(event.target)&&!event.target.closest('[data-action="map-appearance"], .tour-card'))close();});
    document.addEventListener('keydown',function (event) {if(event.key==='Escape'&&!panel.hidden){event.preventDefault();close();trigger?.focus({preventScroll:true});}},true);
    window.addEventListener('resize',position);apply();
    return {toggle:toggle,close:close,choose:choose,sync:apply,get style(){return style;}};
  }
  window.VexMapAppearance={styles:styles,miniature:miniature,create:create};
})();
