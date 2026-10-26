/* A detached light page keeps copied articles independent of the app theme. */
(function () {
  'use strict';
  var platforms=[{key:'wechat',label:'微信公众号'},{key:'zhihu',label:'知乎'},{key:'juejin',label:'稀土掘金'}];
  var properties=['color','background-color','font-family','font-size','font-weight','font-style','line-height','text-align','text-indent','text-decoration','white-space','overflow-wrap','padding','margin','border-top','border-right','border-bottom','border-left','border-radius','border-collapse','list-style-type','max-width'];
  function article(options) {
    return new Promise(function(resolve,reject){
      var frame=document.createElement('iframe'),timer;
      frame.setAttribute('sandbox','allow-same-origin');frame.setAttribute('aria-hidden','true');frame.tabIndex=-1;
      frame.style.cssText='position:fixed;left:-10000px;top:0;width:760px;height:600px;border:0;pointer-events:none';
      function clean(){clearTimeout(timer);frame.remove();}
      frame.onload=function(){
        try {
          var doc=frame.contentDocument, root=doc.body, source=[root].concat(Array.from(root.querySelectorAll('*'))), copy=document.createElement('section');
          copy.innerHTML=root.innerHTML;
          var target=[copy].concat(Array.from(copy.querySelectorAll('*')));
          source.forEach(function(node,index){
            var computed=frame.contentWindow.getComputedStyle(node),styled=target[index];
            Array.from(styled.attributes).forEach(function(attribute){if(!['href','src','alt','title','colspan','rowspan','start','type'].includes(attribute.name))styled.removeAttribute(attribute.name);});
            properties.forEach(function(property){styled.style.setProperty(property,computed.getPropertyValue(property));});
            if(styled.tagName==='IMG'){styled.style.maxWidth='100%';styled.style.height='auto';}
          });
          copy.style.margin='0';copy.style.maxWidth='100%';
          resolve({html:copy.outerHTML,plain:root.innerText});clean();
        }catch(error){clean();reject(error);}
      };
      var safe=document.createElement('template');safe.innerHTML=options.html;
      safe.content.querySelectorAll('script,style,iframe,object,embed,link,button,textarea,input,.code-tools,.mermaid-tools').forEach(function(node){node.remove();});
      safe.content.querySelectorAll('*').forEach(function(node){Array.from(node.attributes).forEach(function(attribute){if(/^on/i.test(attribute.name)||['href','src'].includes(attribute.name)&&/^\s*(?:javascript|vbscript):/i.test(attribute.value))node.removeAttribute(attribute.name);});});
      var base='body{margin:0;padding:0;font:16px/1.9 system-ui;color:#30343b;background:#fff}img{max-width:100%}pre{white-space:pre-wrap;background:#f5f6f7;padding:16px}code{font-family:Consolas,monospace}table{width:100%;border-collapse:collapse}th,td{border:1px solid #d9dfe3;padding:8px 12px}a{color:#24766b}';
      var css=options.platform==='wechat'?VexTypeExport(options.typography,options.compact):VexTypeExport('Basic',false);
      frame.srcdoc='<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src \'none\'; style-src \'unsafe-inline\'; img-src data:"><style>'+base+css+'</style></head><body>'+safe.innerHTML+'</body></html>';
      timer=setTimeout(function(){clean();reject(new Error('Copy preparation timed out'));},5000);document.body.appendChild(frame);
    });
  }
  function fallback(data) {
    var host=document.createElement('div');host.innerHTML=data.html;host.contentEditable='true';host.style.cssText='position:fixed;left:-10000px;top:0';document.body.appendChild(host);
    var selection=getSelection(),ranges=Array.from({length:selection.rangeCount},function(_,index){return selection.getRangeAt(index).cloneRange();}),focus=document.activeElement,range=document.createRange();range.selectNodeContents(host);selection.removeAllRanges();selection.addRange(range);
    function onCopy(event){event.preventDefault();event.clipboardData.setData('text/html',data.html);event.clipboardData.setData('text/plain',data.plain);}
    document.addEventListener('copy',onCopy);
    try{if(!document.execCommand('copy'))throw new Error('Clipboard not available');}
    finally{document.removeEventListener('copy',onCopy);host.remove();selection.removeAllRanges();ranges.forEach(function(saved){selection.addRange(saved);});focus?.focus({preventScroll:true});}
  }
  async function copy(options) {
    var platform=platforms.find(function(item){return item.key===options.platform;});if(!platform)return false;
    try {
      if(platform.key==='juejin')await navigator.clipboard.writeText(options.text);
      else if(navigator.clipboard?.write&&window.ClipboardItem){
        var result=article(options);
        await navigator.clipboard.write([new ClipboardItem({'text/html':result.then(function(data){return new Blob([data.html],{type:'text/html'});}),'text/plain':result.then(function(data){return new Blob([data.plain],{type:'text/plain'});})})]);
      }else fallback(await article(options));
      options.toast(platform.key==='juejin'?'Markdown 已复制，可粘贴到稀土掘金':'富文本已复制，可粘贴到'+platform.label);return true;
    }catch(error){options.toast('未能访问剪贴板，请允许复制后重试');return false;}
  }
  window.VexSocialCopy={platforms:platforms,copy:copy,article:article};
})();
