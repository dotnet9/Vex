/* Contextual controls share Markdown and history with the existing editors. */
(() => {
  'use strict';
  const ops = window.VexDocumentOps, esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const icon = name => `<svg class="vex-element-icon${name==='chevron'?' vex-expand-icon':''}" aria-hidden="true"><use href="#i-${name}"/></svg>`;
  const tableCommands = [
    ['上方插入行','row-above'],['下方插入行','row-below'],[],['左侧插入列','col-before'],['右侧插入列','col-after'],[],
    ['上移该行','row-up'],['下移该行','row-down'],['左移该列','col-left'],['右移该列','col-right'],[],
    ['列左对齐','align-left'],['列居中对齐','align-center'],['列右对齐','align-right'],[],
    ['删除行','row-delete'],['删除列','col-delete'],[],['复制表格','copy'],['格式化表格源码','format'],[],['删除表格','delete']
  ];
  let current;
  function tableItems() {
    const items=tableCommands.filter(([label])=>label).map(([label,action])=>({label,action:'element-table-'+action,disabled:()=>!current?.canTable(action),danger:action==='delete'}));
    const group=(label,actions)=>({label,sub:actions.map(action=>items.find(item=>item.action==='element-table-'+action))});
    return [{label:'插入表格…',action:'element-insert-table',disabled:()=>!current?.editable()},{sep:true},
      group('行操作',['row-above','row-below','row-up','row-down','row-delete']),
      group('列操作',['col-before','col-after','col-left','col-right','col-delete']),
      group('列对齐',['align-left','align-center','align-right']),{sep:true},
      ...items.filter(item=>['element-table-copy','element-table-format','element-table-delete'].includes(item.action))];
  }
  function mount(options) {
    const source = options.source, roots = options.roots, scope = VexOverlay.scope();
    let snapshot = null, lastTarget = null, pointerType = 'mouse', menuLayer = null, dialogLayer = null, unlock = null, dialogUnlock = null, menuFocus = null;
    const text = () => options.getText(), identity = () => options.getIdentity(), editable = () => !(snapshot?.readonly ?? options.readOnly());
    const rootFor = node => roots.find(item => item.element === node || item.element.contains(node));
    const blockSelector = '.instant-block,.md-block,[data-line]';
    function blocks() {
      return options.blocks().map(block => {
        const start = block.start ?? text().split('\n').slice(0,block.line-1).join('\n').length + (block.line > 1 ? 1 : 0);
        return {...block,type:block.type || block.kind,start,end:block.end ?? start+block.raw.length};
      });
    }
    function blockAt(node) {
      const element = node?.nodeType === Node.ELEMENT_NODE ? node : node?.parentElement;
      const wrapper = element?.closest('.instant-block,.md-block') || element?.closest(blockSelector), all = blocks();
      if (!wrapper) return {block:all.find(b => source.selectionStart >= b.start && source.selectionStart <= b.end) || all.at(-1),wrapper:null};
      const line = Number(wrapper.dataset.line) || all[Number(wrapper.dataset.blockIndex ?? wrapper.dataset.block)]?.line;
      return {block:all.find(b => b.line === line) || all.find(b => line >= b.line && line <= b.endLine),wrapper};
    }
    function serialized(wrapper, block) {
      if(block.type==='code'){const fence=VexCodeFence.parse(block.raw);if(fence)return fence.write((wrapper.querySelector('pre')||wrapper).textContent.replace(/\n$/,''),fence.language);}
      if(block.type==='list'&&/^\s*[-+*]\s+\[[ xX]\]/.test(block.raw)&&!wrapper.querySelector('input')){
        const item=wrapper.tagName==='LI'?wrapper:wrapper.querySelector('li');
        if(item){const walker=document.createTreeWalker(item,NodeFilter.SHOW_TEXT);let node;while((node=walker.nextNode())){if(!node.nodeValue.replace(/\uE110VEX(?:START|END)\uE111/g,''))continue;node.nodeValue=node.nodeValue.replace(/^((?:\uE110VEX(?:START|END)\uE111)*)(?:☑|☐) ?/,'$1');break;}const checkbox=document.createElement('input');checkbox.type='checkbox';checkbox.checked=/^\s*[-+*]\s+\[[xX]\]/.test(block.raw);item.prepend(checkbox);}
      }
      if(wrapper.tagName==='LI'){const parent=document.createElement(/^\s*\d+[.)]/.test(block.raw)?'ol':'ul');parent.append(wrapper);return TexEditor.toMarkdown(parent).trimEnd();}
      let raw = TexEditor.toMarkdown(wrapper).trimEnd();
      if (block.type === 'list' && wrapper.classList.contains('instant-block')) {
        raw = raw.replace(/^\s*(?:- |1\. )?[☑☐] /, /^\s*[-+*]\s+\[[xX]\]/.test(block.raw) ? '- [x] ' : '- [ ] ');
        if (/^\s*\d+[.)]/.test(block.raw)) raw = raw.replace(/^1\./,block.raw.match(/^\s*\d+[.)]/)[0]);
      }
      return raw;
    }
    function selectionMap(range) {
      const first = blockAt(range.startContainer), last = blockAt(range.endContainer);
      if (!first.wrapper || !last.wrapper || !first.block || !last.block || [first,last].some(item => item.block.type==='math'||item.block.language?.toLowerCase()==='mermaid'||item.wrapper.querySelector('.diagram-editor,.md-math,.md-special'))) return null;
      const markers = ['\uE110VEXSTART\uE111','\uE110VEXEND\uE111'];
      const affected = first.wrapper === last.wrapper ? [first] : [first,last];
      let working = text(), spans = [];
      for (const item of affected) {
        const clone = item.wrapper.cloneNode(true), targets = [];
        const locate = (node,offset) => { const path=[]; while(node!==item.wrapper){if(!node?.parentNode)return null;path.unshift([...node.parentNode.childNodes].indexOf(node));node=node.parentNode;} let copy=clone;for(const n of path)copy=copy.childNodes[n];return {node:copy,offset}; };
        if (item.wrapper === first.wrapper) targets.push({...locate(range.startContainer,range.startOffset),marker:markers[0]});
        if (item.wrapper === last.wrapper) targets.push({...locate(range.endContainer,range.endOffset),marker:markers[1]});
        // End first keeps the start offset valid when both endpoints share a text node.
        for (const point of targets.reverse()) {if(!point.node)return null;const at=document.createRange();at.setStart(point.node,point.offset);at.collapse(true);at.insertNode(document.createTextNode(point.marker));}
        const raw=serialized(clone,item.block);spans.push({start:item.block.start,end:item.block.end,raw});
      }
      for (const span of spans.sort((a,b)=>b.start-a.start)) working=working.slice(0,span.start)+span.raw+working.slice(span.end);
      const start=working.indexOf(markers[0]), end=working.indexOf(markers[1]);if(start<0||end<start)return null;
      return {working:working.replace(markers[0],'').replace(markers[1],''),start,end:end-markers[0].length};
    }
    function capture(target) {
      const original=text(), selection=getSelection(), node=target?.nodeType === Node.ELEMENT_NODE?target:target?.parentElement;
      const sourceActive=source.getClientRects().length>0&&(target===source || (!target && document.activeElement===source)), selected=selection?.rangeCount?selection.getRangeAt(0):null;
      const usable=node=>node?.isConnected&&rootFor(node)?.element.getClientRects().length>0;
      const focusTarget=sourceActive?source:usable(node)?node:selected&&usable(selected.startContainer)?selected.startContainer:usable(lastTarget)?lastTarget:null;
      const located=sourceActive?blockAt(null):blockAt(focusTarget), block=located.block || {type:'paragraph',raw:'',start:0,end:0,line:1,endLine:1};
      const root=sourceActive?null:rootFor(focusTarget), readonly=!!options.readOnly() || !!root?.readOnly;
      let start=sourceActive?source.selectionStart:block.start,end=sourceActive?source.selectionEnd:block.start,working=original,plain='';
      if (!sourceActive && selected && usable(selected.startContainer) && usable(selected.endContainer) && (!node || selected.intersectsNode(node))) {
        const mapped=selectionMap(selected);if(mapped){({start,end,working}=mapped);plain=selected.toString();}
      }
      if(sourceActive)plain=original.slice(start,end);
      const focusElement=focusTarget?.nodeType===Node.ELEMENT_NODE?focusTarget:focusTarget?.parentElement;
      const cell=focusElement?.closest('td,th'), listItem=focusElement?.closest('li');
      let row=cell?.parentElement.rowIndex ?? 0,col=cell?.cellIndex ?? 0,line=block.line;
      if (listItem && located.wrapper) line+=Math.max(0,[...located.wrapper.querySelectorAll('li')].indexOf(listItem));
      if(sourceActive){const before=original.slice(block.start,start).split('\n');line=block.line+before.length-1;if(block.type==='table'){row=Math.max(0,before.length-1-(before.length>1?1:0));col=Math.max(0,ops.cells(before.at(-1)).length-1);col=Math.min(col,(ops.table(block.raw)?.align.length||1)-1);}}
      const media=focusElement?.closest('a,img'), tokens=ops.inlineTokens(block.raw);
      let token=media?tokens.filter(t=>t.kind===(media.tagName==='IMG'?'image':'link')).find(t=>t.url===media.getAttribute(media.tagName==='IMG'?'src':'href')):tokens.find(t=>start>=block.start+t.start&&start<=block.start+t.end);
      if(media&&token&&located.wrapper){const matches=[...located.wrapper.querySelectorAll(media.tagName==='IMG'?'img':'a')].filter(el=>el.getAttribute(media.tagName==='IMG'?'src':'href')===token.url);token=tokens.filter(t=>t.kind===token.kind&&t.url===token.url)[matches.indexOf(media)]||token;}
      return {original,working,start,end,plain,block,row,col,line,token,readonly,id:identity(),sourceActive};
    }
    function save(target) {snapshot=capture(target);return snapshot;}
    function valid(context=snapshot) {if(!context || context.id!==identity() || context.original!==text()){options.toast('文档已变化，请重新打开操作菜单');return false;}return true;}
    function apply(value,position,context=snapshot) {
      if(!valid(context)||context.readonly)return;
      options.finish();options.applyText(value);
      if(position!==undefined)source.setSelectionRange(Math.min(position,value.length),Math.min(position,value.length));
      lastTarget=null;
      options.focus(context.block.line,context.sourceActive);
    }
    const replaceBlock=(raw,context=snapshot)=>apply(context.original.slice(0,context.block.start)+raw+context.original.slice(context.block.end),context.block.start,context);
    function canTable(action) {
      if(!snapshot)return false;
      if(snapshot.block.type!=='table')return false;
      if(action==='copy')return true;
      if(snapshot.readonly)return false;
      return ['delete','format'].includes(action)||ops.canTable(snapshot.block.raw,action,snapshot.row,snapshot.col);
    }
    function closeMenu(restore=true) {if(!menuLayer)return;menuLayer.remove();menuLayer=null;const release=unlock;unlock=null;if(release)release();if(restore&&menuFocus?.isConnected)menuFocus.focus({preventScroll:true});}
    function closeDialog() {if(!dialogLayer)return;dialogLayer.remove();dialogLayer=null;dialogUnlock?.();dialogUnlock=null;}
    const choice=(label,action,extra={})=>({label,action,...extra});
    function tableChoices(){
      const convert=item=>item.sep?{}:item.sub?{label:item.label,children:()=>item.sub.map(convert)}:choice(item.label,item.action.slice(8),{disabled:item.disabled(),danger:item.danger});
      return tableItems().map(convert);
    }
    function paragraphChoices(){return [choice('正文','convert-paragraph'),...[1,2,3,4,5,6].map(n=>choice(n+' 级标题','convert-heading'+n)),{},choice('引用','convert-quote'),choice('无序列表','convert-list'),choice('有序列表','convert-ordered'),choice('任务列表','convert-task')].map(item=>({...item,disabled:!editable()||['table','code','math','hr','rule'].includes(snapshot.block.type)}));}
    function insertChoices(){return [choice('链接…','insert-link'),choice('图片…','insert-image'),choice('表格…','insert-table'),choice('代码块…','insert-code'),choice('公式块…','insert-math'),{label:'Mermaid 图表',children:()=>Array.from(new Set(VexMermaid.items.map(item=>item.group))).map(group=>({label:group,children:()=>VexMermaid.items.filter(item=>item.group===group).map(item=>choice(item.label,'mermaid-'+item.key))}))},{},choice('在上方插入段落','insert-above'),choice('在下方插入段落','insert-below')].map(item=>({...item,disabled:!editable()}));}
    function rootChoices() {
      const context=snapshot, type=context.block.type, media=context.token?.kind, choices=[];
      if(media){choices.push(choice(media==='image'?'预览图片':'打开链接',media==='image'?'preview-image':'open-link'),choice(media==='image'?'复制图片地址':'复制链接地址','copy-url'));if(!context.readonly)choices.push(choice(media==='image'?'替换图片 / 修改描述…':'编辑链接…','edit-media'),choice(media==='image'?'删除图片':'取消链接','unlink',{danger:media==='image'}));choices.push({});}
      if(type==='table')choices.push({label:'表格',children:tableChoices},{});
      if(type==='code'||type==='math'){
        const diagram=type==='code'&&context.block.language?.toLowerCase()==='mermaid';
        choices.push(choice(type==='math'?'预览公式':diagram?'预览图表':'复制代码',type==='math'||diagram?'preview-block':'copy-code'));
        if(type==='math'||diagram)choices.push(choice('复制源码','copy-code'));
        if(!context.readonly){if(diagram)choices.push(choice('展开源码与预览','expand-diagram'));choices.push(choice(type==='math'?'编辑公式…':diagram?'编辑图表源码…':'编辑代码 / 语言…','edit-block'),choice('删除'+(type==='math'?'公式块':diagram?'图表':'代码块'),'delete-block',{danger:true}));}
        choices.push({});
      }
      choices.push({tools:'clipboard'},{label:'复制 / 粘贴为',children:()=>[choice('复制为 Markdown','copy-markdown',{disabled:context.start===context.end}),choice('复制为 HTML 代码','copy-html',{disabled:context.start===context.end}),choice('复制为纯文本','copy-plain',{disabled:context.start===context.end}),{},choice('粘贴为纯文本','paste-plain',{disabled:context.readonly})]});
      if(!context.readonly){choices.push({tools:'format'},{label:'段落类型',children:paragraphChoices,disabled:['table','code','math','hr','rule'].includes(type)});if(type==='list'){choices.push(choice('增加缩进','indent'),choice('减少缩进','outdent',{disabled:!/^\s+/.test(context.original.split('\n')[context.line-1]||'')}));if(/^\s*[-+*]\s+\[[ xX]\]/.test(context.original.split('\n')[context.line-1]||''))choices.push(choice('切换任务状态','toggle-task'));}choices.push({label:'插入',children:insertChoices});if(!['code','math','table'].includes(type))choices.push({},choice('删除当前'+({heading:'标题',list:'列表项'}[type]||'段落'),'delete-block',{danger:true}));}
      return choices;
    }
    function tools(kind) {
      const editing=!snapshot.readonly, selected=snapshot.start!==snapshot.end, textBlock=!['table','code','math','hr','rule'].includes(snapshot.block.type);
      const items=kind==='clipboard'?[['剪切','cut','scissors',!editing||!selected],['复制','copy-plain','copy',!selected],['粘贴','paste','clipboard',!editing],['删除选中文字','delete-selection','trash',!editing||!selected]]:[['加粗','wrap-bold','<b>B</b>',!selected||!textBlock],['斜体','wrap-italic','<i>I</i>',!selected||!textBlock],['行内代码','wrap-code','inline-code',!selected||!textBlock],['链接','insert-link','link',!textBlock]];
      const group=document.createElement('div');group.className='vex-element-tools';group.setAttribute('role','group');group.setAttribute('aria-label',kind==='clipboard'?'剪贴板':'文字格式');
      items.forEach(([label,action,symbol,disabled])=>{const button=document.createElement('button');button.type='button';button.title=label;button.setAttribute('aria-label',label);button.setAttribute('role','menuitem');button.disabled=disabled;button.innerHTML=symbol.startsWith('<')?symbol:icon(symbol);button.onclick=()=>run(action);group.append(button);});return group;
    }
    function open(target,anchor,tableOnly=false) {
      if(target||!snapshot)save(target);
      options.beforeOpen();closeMenu(false);closeDialog();
      const sheet=options.touch() || VexOverlay.width()<601;
      const focus=document.activeElement;menuFocus=focus;
      menuLayer=document.createElement('div');menuLayer.className='vex-element-layer'+(sheet?' vex-element-sheet':'');menuLayer.dataset.motion=String(VexPreferences.values.motion);
      VexOverlay.mount(menuLayer);if(sheet)unlock=VexOverlay.lock(menuLayer);
      const stack=[], menu=document.createElement('section');menu.className='vex-element-menu';menu.setAttribute('role',sheet?'dialog':'menu');menu.setAttribute('aria-label',tableOnly?'表格操作':'当前元素操作');if(sheet)menu.setAttribute('aria-modal','true');menuLayer.append(menu);
      const content=document.createElement('div');content.className='vex-element-content';content.setAttribute('role','menu');
      const branches=[];
      function menuBounds(){
        const layer=menuLayer.getBoundingClientRect(),app=document.querySelector('.app-window')?.getBoundingClientRect()||layer;
        return {left:layer.left,top:layer.top,minX:Math.max(8,app.left-layer.left+6),maxX:Math.min(layer.width-8,app.right-layer.left-6),minY:Math.max(8,app.top-layer.top+6),maxY:Math.min(layer.height-8,app.bottom-layer.top-6)};
      }
      function fitMenu(element){if(sheet)return;element.classList.remove('vex-element-compact');const bounds=menuBounds();if(element.offsetHeight>bounds.maxY-bounds.minY)element.classList.add('vex-element-compact');}
      function clearBranches(depth=0){for(let i=branches.length-1;i>=depth;i--){const branch=branches.pop();clearTimeout(branch.timer);branch.owner.setAttribute('aria-expanded','false');branch.element.remove();}}
      function flyout(title,items,owner,keyboard=false){
        const parent=owner.closest('.vex-element-menu'),depth=Number(parent.dataset.depth||0);
        if(branches[depth]?.owner===owner){if(keyboard)branches[depth].element.querySelector('button:not(:disabled)')?.focus();return;}
        clearBranches(depth);const element=document.createElement('section');element.className='vex-element-menu';element.dataset.depth=depth+1;element.setAttribute('role','menu');element.setAttribute('aria-label',title);menuLayer.append(element);
        const branch={element,owner};branches.push(branch);owner.setAttribute('aria-expanded','true');
        for(const item of items){if(!item.label){const rule=document.createElement('div');rule.className='vex-element-rule';rule.setAttribute('role','separator');element.append(rule);continue;}
          const button=document.createElement('button');button.type='button';button.className='vex-element-row'+(item.danger?' danger':'');button.disabled=!!item.disabled;button.setAttribute('role','menuitem');button.innerHTML=`<span>${esc(item.label)}</span>${item.children?icon('chevron'):''}`;
          if(item.children){button.setAttribute('aria-haspopup','menu');button.setAttribute('aria-expanded','false');}
          button.onclick=()=>{if(item.children)flyout(item.label,item.children(),button,true);else run(item.action);};
          button.onmouseenter=()=>{if(item.children&&!button.disabled)flyout(item.label,item.children(),button);else clearBranches(depth+1);};element.append(button);
        }
        fitMenu(element);
        const bounds=menuBounds(),r=parent.getBoundingClientRect(),row=owner.getBoundingClientRect(),right=r.right-bounds.left+4,left=r.left-bounds.left-element.offsetWidth-4;
        element.style.left=Math.max(bounds.minX,Math.min(right+element.offsetWidth<=bounds.maxX?right:left,bounds.maxX-element.offsetWidth))+'px';
        element.style.top=Math.max(bounds.minY,Math.min(row.top-bounds.top-6,bounds.maxY-element.offsetHeight))+'px';
        const delay=()=>{clearTimeout(branch.timer);branch.timer=setTimeout(()=>{if(!owner.matches(':hover')&&!branches.slice(depth).some(item=>item.element.matches(':hover')))clearBranches(depth);},180);};
        owner.onmouseleave=delay;element.onmouseleave=delay;element.onmouseenter=()=>clearTimeout(branch.timer);element.addEventListener('scroll',()=>clearBranches(depth+1));
        if(keyboard)element.querySelector('button:not(:disabled)')?.focus({preventScroll:true});
      }
      function page(title,items,back=false){
        content.setAttribute('aria-label',title);menu.replaceChildren();if(sheet||back){const header=document.createElement('header');header.innerHTML=`${back?`<button class="vex-element-icon-button" aria-label="返回">${icon('arrow-left')}</button>`:''}<h2>${esc(title)}</h2><button class="vex-element-icon-button" aria-label="关闭元素操作">${icon('x')}</button>`;header.querySelector('[aria-label="关闭元素操作"]').onclick=()=>closeMenu();if(back)header.querySelector('[aria-label="返回"]').onclick=()=>{const previous=stack.pop();page(previous.title,previous.items,stack.length>0);};menu.append(header);}
        content.replaceChildren();menu.append(content);
        if(tableOnly||title==='表格') {const caption=document.createElement('div');caption.className='vex-element-caption';caption.textContent=snapshot.block.type==='table'?`第 ${snapshot.row+1} 行 · 第 ${snapshot.col+1} 列`:'未选中表格';content.append(caption);}
        for(const item of items){if(item.tools){content.append(tools(item.tools));continue;}if(!item.label){const rule=document.createElement('div');rule.className='vex-element-rule';rule.setAttribute('role','separator');content.append(rule);continue;}
          const button=document.createElement('button');button.type='button';button.className='vex-element-row'+(item.danger?' danger':'');button.disabled=!!item.disabled;button.setAttribute('role','menuitem');button.innerHTML=`<span>${esc(item.label)}</span>${item.children?icon('chevron'):''}`;
          if(item.children)button.setAttribute('aria-haspopup','menu');
          button.onclick=()=>{if(item.children){if(!sheet){flyout(item.label,item.children(),button,true);return;}stack.push({title,items});page(item.label,item.children(),true);}else run(item.action);};
          if(!sheet)button.onmouseenter=()=>{if(item.children&&!button.disabled)flyout(item.label,item.children(),button);else clearBranches();};content.append(button);
        }
        if(sheet&&VexPreferences.values.motion&&!matchMedia('(prefers-reduced-motion:reduce)').matches)content.animate([{opacity:.4,transform:'translateX(8px)'},{opacity:1,transform:'translateX(0)'}],{duration:180,easing:'ease-out'});
        if(!sheet){fitMenu(menu);place();}menu.querySelector('button:not(:disabled)')?.focus({preventScroll:true});
      }
      function place(){const bounds=menuBounds(),r=anchor?.getBoundingClientRect?.();const x=r?r.right+4:anchor?.x??bounds.left+16,y=r?r.top:anchor?.y??bounds.top+16;menu.style.left=Math.max(bounds.minX,Math.min(x-bounds.left,bounds.maxX-menu.offsetWidth))+'px';menu.style.top=Math.max(bounds.minY,Math.min(y-bounds.top,bounds.maxY-menu.offsetHeight))+'px';}
      menuLayer.addEventListener('pointerdown',event=>{if(event.target===menuLayer){event.preventDefault();closeMenu();if(focus?.isConnected)focus.focus({preventScroll:true});}});
      menuLayer.addEventListener('click',event=>event.stopPropagation());
      menuLayer.addEventListener('keydown',event=>{
        if(event.key==='Escape'){event.preventDefault();event.stopPropagation();if(branches.length){const depth=branches.length-1,owner=branches[depth].owner;clearBranches(depth);owner.focus();}else if(stack.length){const previous=stack.pop();page(previous.title,previous.items,stack.length>0);}else closeMenu();return;}
        const activeMenu=document.activeElement.closest('.vex-element-menu')||menu,buttons=[...activeMenu.querySelectorAll('button:not(:disabled)')],index=buttons.indexOf(document.activeElement);let next;
        if(event.key==='ArrowDown'||event.key==='ArrowUp')next=(index+(event.key==='ArrowDown'?1:buttons.length-1))%buttons.length;
        if(event.key==='Home')next=0;if(event.key==='End')next=buttons.length-1;
        if(event.key==='ArrowRight'&&document.activeElement.hasAttribute('aria-haspopup')){event.preventDefault();document.activeElement.click();return;}
        if(event.key==='ArrowLeft'&&!sheet&&activeMenu!==menu){event.preventDefault();const depth=Number(activeMenu.dataset.depth)-1,owner=branches[depth].owner;clearBranches(depth);owner.focus();return;}
        if(event.key==='ArrowLeft'&&stack.length){event.preventDefault();const previous=stack.pop();page(previous.title,previous.items,stack.length>0);return;}
        if(event.key==='Tab'){event.preventDefault();next=(index+(event.shiftKey?buttons.length-1:1))%buttons.length;}
        if(next!==undefined){event.preventDefault();buttons[next]?.focus();}
      });
      page(tableOnly?'表格':'当前元素操作',tableOnly?tableChoices():rootChoices());
    }
    function dialog(title,body,saveAction,label='保存') {
      closeMenu(false);closeDialog();dialogLayer=document.createElement('div');dialogLayer.className='vex-element-layer vex-element-dialog-layer';dialogLayer.dataset.motion=String(VexPreferences.values.motion);VexOverlay.mount(dialogLayer);dialogUnlock=VexOverlay.lock(dialogLayer);
      dialogLayer.innerHTML=`<section class="vex-element-dialog" role="dialog" aria-modal="true" aria-labelledby="vex-element-dialog-title"><header><h2 id="vex-element-dialog-title">${esc(title)}</h2><button class="vex-element-icon-button" aria-label="关闭">${icon('x')}</button></header><div class="vex-element-dialog-body">${body}<p class="vex-element-error" role="alert" hidden></p></div><footer><button data-dismiss>${saveAction?'取消':'关闭'}</button>${saveAction?`<button class="primary" data-submit>${esc(label)}</button>`:''}</footer></section>`;
      const host=dialogLayer;
      host.querySelector('header button').onclick=closeDialog;host.querySelector('[data-dismiss]').onclick=closeDialog;
      const error=message=>{const el=host.querySelector('[role=alert]');el.hidden=false;el.textContent=message;};
      if(saveAction)host.querySelector('[data-submit]').onclick=()=>saveAction(host,error);
      host.addEventListener('pointerdown',event=>{if(event.target===host)closeDialog();});host.addEventListener('click',event=>event.stopPropagation());
      host.addEventListener('keydown',event=>{event.stopPropagation();if(event.key==='Escape'){event.preventDefault();closeDialog();}if(event.key==='Tab'){const list=[...host.querySelectorAll('button,input,textarea,select,a[href]')].filter(el=>!el.disabled),index=list.indexOf(document.activeElement);if(event.shiftKey&&index===0||!event.shiftKey&&index===list.length-1){event.preventDefault();list[event.shiftKey?list.length-1:0].focus();}}});
      host.querySelector('input,textarea,[data-dismiss]')?.focus({preventScroll:true});return host;
    }
    const field=(label,name,value='',type='text')=>`<label for="vex-element-${name}">${esc(label)}</label><input id="vex-element-${name}" name="${name}" type="${type}" value="${esc(value)}">`;
    const area=(label,value)=>`<label for="vex-element-content">${esc(label)}</label><textarea id="vex-element-content" name="content" spellcheck="false">${esc(value)}</textarea>`;
    function insert(raw,context=snapshot){const at=context.end;apply(context.working.slice(0,context.start)+raw+context.working.slice(at),context.start+raw.length,context);}
    async function copy(value,context=snapshot){try{if(!navigator.clipboard?.writeText)throw Error();await navigator.clipboard.writeText(value);options.toast('已复制');return valid(context);}catch{options.toast('浏览器未允许访问剪贴板，请使用系统复制');return false;}}
    async function run(action) {
      const context=snapshot;if(!valid(context))return;closeMenu(false);
      const isCopy=action.startsWith('copy')||action==='table-copy', canRead=isCopy||['open-link','preview-image','preview-block'].includes(action);
      if(context.readonly&&!canRead)return;
      if(action.startsWith('table-')){const kind=action.slice(6);if(!canTable(kind))return;if(kind==='copy'){await copy(context.block.raw,context);return;}if(kind==='delete'){replaceBlock('');return;}const changed=ops.changeTable(context.block.raw,kind,context.row,context.col);if(changed)replaceBlock(changed.raw);return;}
      if(action.startsWith('copy')||action==='cut'){
        let value=context.working.slice(context.start,context.end);
        if(action==='copy-url')value=context.token?.url||'';
        if(action==='copy-code')value=VexCodeFence.parse(context.block.raw)?.code ?? context.block.raw.replace(/^\$\$\n?|\n?\$\$$/g,'');
        if(action==='copy-html')value=TexEditor.render(value);
        if(action==='copy-plain'||action==='cut')value=context.plain||value;
        if(!value)return;
        if(await copy(value,context)&&action==='cut')insert('',context);return;
      }
      if(action==='paste'||action==='paste-plain'){
        try{if(!navigator.clipboard?.readText)throw Error();const value=await navigator.clipboard.readText();if(!valid(context))return;const raw=action==='paste-plain'?value.replace(/[\\`*_{}\[\]<>#|~]/g,'\\$&'):value;insert(raw,context);}catch{options.toast('浏览器未允许读取剪贴板，请使用系统粘贴');}return;
      }
      if(action==='delete-selection'){if(context.start!==context.end)insert('',context);return;}
      if(action==='delete-block'){replaceBlock('');return;}
      if(action.startsWith('wrap-')){const marker={bold:'**',italic:'*',code:'`'}[action.slice(5)];if(context.start===context.end)return;const result=ops.wrap(context.working,context.start,context.end,marker);apply(result.text,result.end,context);return;}
      if(action.startsWith('convert-')){const kind=action.slice(8);if(context.block.type==='list'){const lines=context.original.split('\n');lines[context.line-1]=ops.convert(lines[context.line-1],kind);apply(lines.join('\n'),undefined,context);}else replaceBlock(ops.convert(context.block.raw,kind));return;}
      if(['indent','outdent','toggle-task'].includes(action)){const lines=context.original.split('\n'),index=context.line-1;lines[index]=action==='indent'?'  '+lines[index]:action==='outdent'?lines[index].replace(/^ {1,2}|^\t/,''):lines[index].replace(/\[([ xX])\]/,(_,check)=>check===' '?'[x]':'[ ]');apply(lines.join('\n'),undefined,context);return;}
      if(action==='open-link'){const url=TexEditor.safeURL(context.token.url);if(url==='#'&&!context.token.url.startsWith('#')){options.toast('此链接地址不可打开');return;}if(url.startsWith('#')){let hash;try{hash=decodeURIComponent(url.slice(1));}catch{hash=url.slice(1);}const heading=TexEditor.parse(context.original).headings.find(item=>item.id===hash||item.title.toLowerCase().replace(/\s+/g,'-')===hash.toLowerCase()),target=document.getElementById(hash)||(heading&&options.findBlock(heading.line));if(target)target.scrollIntoView({block:'center',behavior:VexPreferences.values.motion?'smooth':'auto'});else options.toast('未找到此文档锚点');return;}window.open(url,'_blank','noopener,noreferrer');return;}
      if(action==='unlink'){const token=context.token;replaceBlock(context.block.raw.slice(0,token.start)+(token.kind==='image'?'':token.label)+context.block.raw.slice(token.end));return;}
      if(action==='preview-image'){dialog('图片预览',`<div class="vex-element-preview"><img src="${esc(TexEditor.safeURL(context.token.url,true))}" alt="${esc(context.token.label)}"></div>`);return;}
      if(action==='insert-link'||action==='insert-image'||action==='edit-media'){
        const existing=action==='edit-media'?context.token:null,image=existing?existing.kind==='image':action==='insert-image';
        const host=dialog(image?'图片':'链接',field(image?'图片地址':'链接地址','url',existing?.url||'https://')+field(image?'替代文字':'显示文字','label',existing?.label||context.plain||'')+(image?'<label for="vex-element-file">本地图片</label><input id="vex-element-file" type="file" accept="image/png,image/jpeg,image/gif,image/webp">':''),(host,error)=>{
          const url=host.querySelector('[name=url]').value.trim(),label=host.querySelector('[name=label]').value.trim();if(!url||/\s|[()]/.test(url)||TexEditor.safeURL(url,image)==='#'&&url!=='#'){error('请输入有效地址，图片也可使用 assets/ 下的路径');return;}if(/[\[\]\n]/.test(label)){error('显示文字不能包含方括号或换行');return;}
          const raw=(image?'!':'')+'['+label+']('+url+')';closeDialog();if(existing)replaceBlock(context.block.raw.slice(0,existing.start)+raw+context.block.raw.slice(existing.end),context);else insert(raw,context);
        });
        if(image)host.querySelector('[type=file]').onchange=event=>{const file=event.target.files[0];if(!file)return;const error=host.querySelector('[role=alert]');if(!/^image\/(png|jpeg|gif|webp)$/.test(file.type)||file.size>1024*1024){error.hidden=false;error.textContent='请选择 1 MB 以内的 PNG、JPEG、GIF 或 WebP 图片';return;}const reader=new FileReader();reader.onload=()=>{if(!host.isConnected)return;host.querySelector('[name=url]').value=reader.result;if(!host.querySelector('[name=label]').value)host.querySelector('[name=label]').value=file.name.replace(/[\[\]]/g,'');error.hidden=true;};reader.readAsDataURL(file);};return;
      }
      if(action==='insert-table'){
        dialog('插入表格',field('列数','columns','3','number')+field('内容行数','rows','3','number'),(host,error)=>{const columns=Number(host.querySelector('[name=columns]').value),rows=Number(host.querySelector('[name=rows]').value);if(!Number.isInteger(columns)||!Number.isInteger(rows)||columns<1||columns>20||rows<1||rows>100){error('列数为 1–20，内容行数为 1–100');return;}const raw=ops.serialize({rows:[Array.from({length:columns},(_,i)=>'列 '+(i+1)),...Array.from({length:rows},()=>Array(columns).fill(''))],align:Array(columns).fill('')});closeDialog();insert('\n\n'+raw+'\n\n',context);},'插入');return;
      }
      if(action==='insert-above'||action==='insert-below'){const at=action==='insert-above'?context.block.start:context.block.end;apply(context.original.slice(0,at)+'\n\n新段落\n\n'+context.original.slice(at),at+2,context);return;}
      if(action.startsWith('mermaid-')){insert(VexMermaid.snippet(action.slice(8)),context);return;}
      if(action==='expand-diagram'){options.finish();const block=options.findBlock(context.block.line);const editor=block?.querySelector('.diagram-editor');if(editor){editor.click();}else options.editSource(context.block.line);return;}
      const fence=VexCodeFence.parse(context.block.raw),math=context.block.type==='math'||action==='insert-math',code=fence?.code ?? (math?context.block.raw.replace(/^\$\$\n?|\n?\$\$$/g,''):'');
      if(action==='preview-block'){const host=dialog(math?'公式预览':'图表预览',`<div class="vex-element-preview markdown-body md-body">${TexEditor.render(context.block.raw)}</div>`);VexRendering.diagrams(host);return;}
      if(['edit-block','insert-code','insert-math'].includes(action)){
        const inserting=action!=='edit-block', language=fence?.language||'';
        dialog(math?'公式源码':'代码源码',(!math?field('语言','language',language):'')+area('源码',inserting?'':code),(host,error)=>{const value=host.querySelector('[name=content]').value,lang=math?'':host.querySelector('[name=language]').value.trim();if(/[\n`~]/.test(lang)){error('语言名称不能包含换行或围栏符号');return;}const raw=math?'$$\n'+value+'\n$$':(fence||VexCodeFence.parse('```\n\n```')).write(value,lang);closeDialog();if(inserting)insert('\n\n'+raw+'\n\n',context);else replaceBlock(raw,context);});
      }
    }
    document.addEventListener('pointerdown',event=>{
      pointerType=event.pointerType||'mouse';
      if(rootFor(event.target)||event.target===source)lastTarget=event.target;
      if(event.target.closest('.tb-menu,.menubar,.mobile-more,[data-element-open],[data-action="insert-table"],[data-format="table"]'))save(document.activeElement===source?source:lastTarget?.isConnected?lastTarget:null);
    },true);
    roots.forEach(root=>root.element.addEventListener('contextmenu',event=>{if(pointerType==='touch')return;event.preventDefault();event.stopPropagation();open(event.target,{x:event.clientX,y:event.clientY});}));
    source.addEventListener('contextmenu',event=>{if(pointerType==='touch')return;event.preventDefault();event.stopPropagation();open(source,{x:event.clientX,y:event.clientY});});
    document.addEventListener('keydown',event=>{if(menuLayer||dialogLayer)return;if((event.key==='ContextMenu'||event.shiftKey&&event.key==='F10')&&(rootFor(document.activeElement)||document.activeElement===source)){event.preventDefault();open(document.activeElement,document.activeElement);}},true);
    options.entries.forEach(entry=>{const button=document.createElement('button');button.className='vex-element-entry';button.type='button';button.title='当前元素操作';button.setAttribute('aria-label','当前元素操作');button.dataset.elementOpen='';button.innerHTML=icon('more');entry.append(button);button.onclick=event=>{event.stopPropagation();open(null,button);};});
    window.addEventListener('resize',()=>{closeMenu();closeDialog();});
    const api={editable,canTable,tableItems,openCurrent:(anchor,tableOnly=false)=>open(null,anchor,tableOnly),handleAction(action,anchor){if(!action.startsWith('element-'))return false;if(!snapshot)save(null);if(action==='element-table')open(null,anchor,true);else run(action.slice(8));return true;},capture:save};
    current=api;return api;
  }
  window.VexElementMenu={mount,tableItems};
})();
