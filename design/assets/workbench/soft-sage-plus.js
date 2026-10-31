/* Vex · Soft Sage interaction polish. Additive prototype enhancements only. */
(function () {
  'use strict';
  function init() {
    const root=document.documentElement, input=document.getElementById('markdown-input');
    const preview=document.getElementById('markdown-preview'), sourceScroll=document.querySelector('.source-scroll');
    const readingScroll=document.querySelector('.reading-scroll'), status=document.querySelector('.document-statusbar');
    if(!input||!preview||!status)return;

    const tools=document.createElement('div');
    tools.className='sage-status-tools';
    tools.innerHTML='<span class="sage-reading-progress" aria-label="阅读进度"><i></i></span><span class="sage-stats" aria-live="polite"></span><button type="button" class="sage-focus-toggle" aria-pressed="false" title="专注模式（Ctrl+Shift+F）">专注写作</button>';
    status.append(tools);
    const stats=tools.querySelector('.sage-stats'), progress=tools.querySelector('.sage-reading-progress i');
    const countWords=text=>{const c=(text.match(/[\u3400-\u9fff]/g)||[]).length;const e=(text.match(/[A-Za-z0-9_]+(?:['’-][A-Za-z0-9_]+)*/g)||[]).length;return c+e;};
    function updateStats(){
      const value=input.value||'', words=countWords(value), chars=value.replace(/\s/g,'').length;
      const mins=Math.max(1,Math.ceil(words/400));
      stats.textContent='字数 '+words.toLocaleString('zh-CN')+' · '+chars.toLocaleString('zh-CN')+' 字符 · 约 '+mins+' 分钟';
    }
    input.addEventListener('input',updateStats); updateStats();

    const focusButton=tools.querySelector('button');
    function setFocus(on){
      root.classList.toggle('sage-focus-mode',on);
      focusButton.setAttribute('aria-pressed',String(on));
      focusButton.textContent=on?'退出专注':'专注写作';
      focusButton.title=on?'退出专注模式（Esc）':'专注模式（Ctrl+Shift+F）';
    }
    focusButton.addEventListener('click',()=>setFocus(!root.classList.contains('sage-focus-mode')));
    document.addEventListener('keydown',event=>{
      if((event.ctrlKey||event.metaKey)&&event.shiftKey&&event.key.toLowerCase()==='f'){
        event.preventDefault();setFocus(!root.classList.contains('sage-focus-mode'));
      } else if(event.key==='Escape'&&root.classList.contains('sage-focus-mode')) setFocus(false);
    });

    // Source and rendered preview follow the same relative document position.
    let syncing=false;
    function sync(from,to){
      if(syncing||!from||!to)return; syncing=true;
      const a=Math.max(1,from.scrollHeight-from.clientHeight), b=Math.max(0,to.scrollHeight-to.clientHeight);
      to.scrollTop=(from.scrollTop/a)*b;
      requestAnimationFrame(()=>{syncing=false;});
    }
    sourceScroll?.addEventListener('scroll',()=>sync(sourceScroll,readingScroll),{passive:true});
    readingScroll?.addEventListener('scroll',()=>sync(readingScroll,sourceScroll),{passive:true});

    function updateProgress(){
      if(!readingScroll)return;
      const max=readingScroll.scrollHeight-readingScroll.clientHeight;
      progress.style.width=(max>0?Math.min(100,readingScroll.scrollTop/max*100):0)+'%';
    }
    readingScroll?.addEventListener('scroll',updateProgress,{passive:true});
    new ResizeObserver(updateProgress).observe(readingScroll||preview);

    // Image lightbox: click an image in the rendered article to inspect it.
    const lightbox=document.createElement('div');
    lightbox.className='sage-lightbox';lightbox.hidden=true;
    lightbox.innerHTML='<button type="button" aria-label="关闭图片预览" title="关闭（Esc）">×</button><img alt="">';
    document.body.append(lightbox);
    function closeImage(){lightbox.hidden=true;lightbox.querySelector('img').removeAttribute('src');}
    lightbox.addEventListener('click',closeImage);
    preview.addEventListener('click',event=>{
      const img=event.target.closest('img');if(!img)return;
      lightbox.querySelector('img').src=img.currentSrc||img.src;
      lightbox.querySelector('img').alt=img.alt||'';
      lightbox.hidden=false;
    });
    // Add a copy affordance to rendered code blocks without changing source content.
    const observer=new MutationObserver(()=>{
      preview.querySelectorAll('pre').forEach(pre=>{
        if(pre.closest('.md-code-block')?.querySelector('.code-tools')||pre.parentElement.querySelector('.sage-copy-code'))return;
        const button=document.createElement('button');
        button.type='button';button.className='sage-copy-code';button.textContent='复制代码';
        button.addEventListener('click',async()=>{
          try{await navigator.clipboard.writeText(pre.innerText);button.textContent='已复制';}
          catch(_){button.textContent='复制失败';}
          setTimeout(()=>{button.textContent='复制代码';},1300);
        });
        pre.parentElement.insertBefore(button,pre);
      });
      updateProgress();
    });
    observer.observe(preview,{childList:true,subtree:true});
    updateProgress();
  }
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init,{once:true});else init();
})();