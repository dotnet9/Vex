/* 评审参数不进入产品菜单。 */
(function () {
  const query=new URLSearchParams(location.search);
  document.body.dataset.platform=query.get('platform')||'windows';
  const language={zh:'zh-CN',en:'en-US'}[query.get('lang')]||query.get('lang');
  if(language)VexL.set(language);
  if(query.get('scenario')==='guide')setTimeout(()=>VexGuide.start({automatic:true}),250);
  if(query.get('scenario')==='exit-unsaved')setTimeout(()=>VexProto.previewUnsavedExit(),100);

})();
