/* Reviewable failure/recovery flows. External changes here are explicit simulations. */
(function () {
  'use strict';
  const W=VexWorkbench,host=document.querySelector('.window-body');
  const notice=document.createElement('section');notice.className='workflow-notice';notice.hidden=true;notice.setAttribute('aria-label','文档操作提示');
  document.querySelector('.titlebar').after(notice);
  const dialog=document.createElement('dialog');dialog.className='workflow-dialog';dialog.setAttribute('aria-labelledby','workflow-title');host.append(dialog);
  let drafts=[],draftError=false,origin=null;
  try{const stored=VexStorage.getItem('vex-design-exit-drafts');if(stored){drafts=JSON.parse(stored);if(!Array.isArray(drafts)||!drafts.every(x=>typeof x.name==='string'&&typeof x.text==='string'))throw Error('Invalid draft');}}catch(_){drafts=[];draftError=true;}
  function update(){
    const doc=W.documents[W.state.doc];
    let title='',description='',actions='',kind='info';
    if(doc.saveError||doc.exportError){kind='error';title=doc.saveError?'Markdown 副本下载失败 · 内容已保留':'HTML 导出失败 · 内容与排版已保留';description='可以继续编辑，或重新发起下载。';actions=doc.saveError?'<button class="btn primary" data-workflow="retry-save">重试保存副本</button>':'<button class="btn primary" data-workflow="retry-export">重试导出 HTML</button>';}
    else if(doc.externalText!==undefined){title='检测到外部版本 · 请先比较';description='当前编辑内容保持原样，选择后再处理外部版本。';actions='<button class="btn primary" data-workflow="compare">比较版本…</button>';}
    else if(drafts.length){title='发现 '+drafts.length+' 篇恢复草稿';description='恢复为独立文档，确认内容后再保存副本。';actions='<button class="btn primary" data-workflow="recover">查看草稿…</button><button class="btn" data-workflow="later">稍后处理</button>';}
    else if(draftError){kind='error';title='恢复草稿暂时无法读取';description='草稿数据仍保留，请保留浏览器数据后重试。';}
    const markup='<p><strong>'+VexEsc(title)+'</strong><small>'+VexEsc(description)+'</small></p><div class="workflow-actions">'+actions+'</div>';
    notice.hidden=!title;notice.dataset.kind=kind;notice.setAttribute('role',kind==='error'?'alert':'status');
    if(notice.innerHTML!==markup)notice.innerHTML=markup;
  }
  function capture(){return {focus:document.activeElement,selection:[document.querySelector('#markdown-input').selectionStart,document.querySelector('#markdown-input').selectionEnd],scrolls:[...document.querySelectorAll('.source-scroll,.instant-scroll,.reading-scroll')].map(el=>({el,top:el.scrollTop}))};}
  function close(){dialog.close();if(!origin)return;const old=origin;origin=null;requestAnimationFrame(()=>{old.focus?.isConnected&&old.focus.focus({preventScroll:true});document.querySelector('#markdown-input').setSelectionRange(...old.selection);old.scrolls.forEach(x=>x.el.scrollTop=x.top);});}
  function compare(){
    W.finish();const doc=W.documents[W.state.doc];if(doc.externalText===undefined)return;
    const current=W.text.split('\n'),external=doc.externalText.split('\n');let changed=0;
    while(changed<Math.max(current.length,external.length)&&current[changed]===external[changed])changed++;
    const start=Math.max(0,changed-2),same=W.text===doc.externalText;
    function excerpt(lines){return (start?'<span class="version-context">… 前 '+start+' 行相同</span>\n':'')+VexEsc(lines.slice(start,changed).join('\n'))+'\n<mark>'+VexEsc(lines.slice(changed).join('\n')||'（此版本没有后续内容）')+'</mark>';}
    origin=capture();dialog.innerHTML='<h2 id="workflow-title">比较「'+VexEsc(doc.name)+'」的两个版本</h2><p>原型演练：模拟来自其他程序的修改。载入外部版本后，可用撤销找回当前版本。</p><p>'+(same?'两个版本的内容相同。':'从第 '+(changed+1)+' 行出现差异，先展示附近内容。')+'</p><div class="version-comparison"><section><strong>当前编辑内容</strong><pre tabindex="0">'+excerpt(current)+'</pre></section><section><strong>外部版本</strong><pre tabindex="0">'+excerpt(external)+'</pre></section></div><details><summary>查看两个完整版本</summary><div class="version-comparison"><section><strong>当前编辑内容</strong><pre tabindex="0">'+VexEsc(W.text)+'</pre></section><section><strong>外部版本</strong><pre tabindex="0">'+VexEsc(doc.externalText)+'</pre></section></div></details><p id="workflow-error" role="alert" hidden></p><div class="workflow-actions"><button class="btn" data-workflow="cancel" autofocus>取消</button><button class="btn" data-workflow="keep-current">保留当前版本</button><button class="btn" data-workflow="copy-current">下载当前副本</button><button class="btn primary" data-workflow="load-external">载入外部版本</button></div>';
    dialog.showModal();
  }
  function recover(){
    origin=capture();dialog.innerHTML='<h2 id="workflow-title">恢复草稿</h2><p>以下内容将作为独立的“恢复.md”文档打开，不会覆盖已打开的文章。</p>'+drafts.map(d=>'<details><summary>'+VexEsc(d.name)+'</summary><pre>'+VexEsc(d.text)+'</pre></details>').join('')+'<div class="workflow-actions"><button class="btn" data-workflow="cancel" autofocus>暂不恢复</button><button class="btn primary" data-workflow="restore-drafts">恢复为独立文档</button></div>';dialog.showModal();
  }
  function action(event){
    const button=event.target.closest('[data-workflow]');if(!button)return;
    event.stopPropagation();const doc=W.documents[W.state.doc];
    switch(button.dataset.workflow){
      case 'retry-save':VexProto.handleAction('save');break;
      case 'retry-export':VexProto.handleAction('export-html');break;
      case 'compare':compare();break;
      case 'recover':recover();break;
      case 'cancel':close();break;
      case 'later':drafts=[];break;
      case 'keep-current':delete doc.externalText;close();VexProto.toast('已保留当前版本，外部版本未写入正文');break;
      case 'copy-current':VexProto.handleAction('save');{const error=dialog.querySelector('#workflow-error');error.hidden=!doc.saveError;error.textContent='副本下载失败，当前与外部版本均已保留。请再次下载，或取消后继续编辑。';}if(!doc.saveError)VexProto.toast('已发起当前副本下载，仍可继续比较外部版本');break;
      case 'load-external':{const text=doc.externalText;delete doc.externalText;close();W.loadExternalVersion(text);VexProto.toast('已载入外部版本，可用撤销恢复刚才的编辑');break;}
      case 'restore-drafts':{const recovered=drafts;drafts=[];close();W.recoverDrafts(recovered);break;}
    }
    update();
  }
  notice.addEventListener('click',action);dialog.addEventListener('click',action);
  dialog.addEventListener('cancel',event=>{event.preventDefault();close();});
  dialog.addEventListener('keydown',event=>{event.stopPropagation();if((event.ctrlKey||event.metaKey)&&['s','o','n','w'].includes(event.key.toLowerCase()))event.preventDefault();});
  window.addEventListener('vex-workflow-result',update);window.addEventListener('vex-save-state-changed',update);window.addEventListener('vex-document-loaded',update);
  window.VexWorkflows={simulateExternal(text){W.finish();W.documents[W.state.doc].externalText=text;update();},showDrafts(items){drafts=items;update();},compare};
  const scenario=new URLSearchParams(location.search).get('scenario');
  if(scenario==='external-change')VexWorkflows.simulateExternal(W.text+'\n\n## 来自外部的补充\n\n这段内容来自另一个程序，等待比较后载入。\n');
  if(scenario==='draft')drafts=[{name:'昨日写作手记.md',text:'# 昨日写作手记\n\n这是一段尚未保存的思路。\n\n## 接下来\n\n把观察整理成文章，再保存副本。\n'}];
  update();
})();
