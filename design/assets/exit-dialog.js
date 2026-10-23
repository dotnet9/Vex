/* 单文档关闭守卫：切换文档必经保存确认，同一时刻至多一篇未保存（desktop/tablet/phone 共用）。 */
(function () {
  'use strict';
  function esc(text) {return String(text).replace(/[&<>"']/g,function(c){return {'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c];});}
  var fileIcon='<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M14 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9zM14 3v6h6M8 13h8M8 17h5"/></svg>';
  function create(adapter) {
    var host=document.createElement('div');host.className='exit-dialog-host';host.hidden=true;adapter.parent.appendChild(host);
    var release, documents=[], busy=false;
    function cancel() {if(busy)return;host.hidden=true;release?.();release=null;}
    function open() {
      if(!host.hidden)return;
      adapter.beforeOpen?.();documents=adapter.documents();
      if(!documents.length){adapter.close();return;}
      var doc=documents[0];
      host.innerHTML='<section class="exit-dialog" role="dialog" aria-modal="true" aria-labelledby="exit-dialog-title"><header class="exit-dialog-titlebar"><span class="exit-dialog-ico" aria-hidden="true">!</span><span id="exit-dialog-title">有未保存的更改</span><button class="vex-icon-button" data-exit="cancel" aria-label="继续编辑"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 6 12 12M18 6 6 18"/></svg></button></header><div class="exit-dialog-body"><h2>'+esc(doc.name)+'</h2><p id="exit-dialog-description">退出后，尚未保存的修改将会丢失。</p></div><footer class="exit-dialog-actions"><button class="exit-continue" data-exit="cancel">继续编辑</button><button class="exit-discard danger-outline" data-exit="discard">放弃修改并退出</button></footer></section>';
      host.hidden=false;release=VexOverlay.lock(host);
      requestAnimationFrame(function(){host.querySelector('[data-exit=save]').focus({preventScroll:true});});
    }
    host.onclick=async function(event){
      if(busy)return;
      var row=event.target.closest('[data-exit-document]');if(row){cancel();adapter.edit?.(documents[Number(row.dataset.exitDocument)]);return;}
      var button=event.target.closest('[data-exit]');if(!button||busy)return;
      var action=button.dataset.exit;if(action==='cancel'){cancel();return;}
      busy=true;host.querySelectorAll('button').forEach(function(button){button.disabled=true;});
      try {
        if(action==='save')await adapter.save(documents);
        if(action==='draft')await adapter.keepDrafts(documents);
        if(action==='discard')await adapter.discard?.(documents);
        busy=false;cancel();adapter.close();
      } catch(error) {
        busy=false;host.querySelectorAll('button').forEach(function(button){button.disabled=false;});
        var message=host.querySelector('.exit-dialog-error');message.hidden=false;message.textContent=action==='draft'?'草稿未能保留，窗口没有关闭。请先保存文档。':'保存未完成，窗口没有关闭。请重试或返回编辑。';
      }
    };
    document.addEventListener('keydown',function(event){if(event.key==='Escape'&&!host.hidden){event.preventDefault();event.stopImmediatePropagation();cancel();}},true);
    host.addEventListener('keydown',function(event){
      event.stopPropagation();
      if(event.ctrlKey||event.metaKey)event.preventDefault();
      if(event.key!=='Tab')return;
      var controls=Array.from(host.querySelectorAll('button:not(:disabled)')),first=controls[0],last=controls.at(-1);
      if(!controls.length){event.preventDefault();return;}
      if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}
      else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}
    });
    return {open:open,cancel:cancel,get visible(){return !host.hidden;}};
  }
  window.VexExitDialog={create:create};
})();
