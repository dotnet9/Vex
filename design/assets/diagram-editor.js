/* Keeps diagram source, preview and asynchronous render ownership in one block. */
(() => {
  'use strict';
  const sessions = new Set();
  let serial = 0;
  const reduced = () => matchMedia('(prefers-reduced-motion:reduce)').matches || document.documentElement.dataset.motion === 'false';
  function mount(element, block, options) {
    const original = block.raw, fence = VexCodeFence.parse(original);
    if (!fence) return;
    const node = element.querySelector('.md-diagram') || element;
    node.className = 'diagram-editor'; node.contentEditable = 'false'; node.tabIndex = 0;
    node.setAttribute('role','group'); node.setAttribute('aria-label','Mermaid 图表'); node.setAttribute('aria-expanded','false');
    node.innerHTML = '<div class="diagram-source-shell" inert><div><textarea class="diagram-source" spellcheck="false" aria-label="Mermaid 源码"></textarea></div></div><div class="diagram-preview" role="img" aria-label="图表预览"></div><p class="diagram-error" role="status" hidden></p>';
    const textarea = node.querySelector('textarea'), shell = node.querySelector('.diagram-source-shell'), preview = node.querySelector('.diagram-preview'), error = node.querySelector('.diagram-error');
    textarea.value = fence.code;
    let expanded = false, closing = false, revision = 0, timer, closeTimer, raw = original, composing = false;
    const session = {element:node,busy:() => expanded || closing}; sessions.add(session);
    function resize() { textarea.style.height = 'auto'; textarea.style.height = Math.max(128,Math.min(360,textarea.scrollHeight)) + 'px'; }
    async function render() {
      clearTimeout(timer); const version = ++revision;
      preview.dataset.loading = 'true';
      try {
        const svg = await VexDiagramRendering.render('vex-edit-diagram-' + (++serial),textarea.value);
        if (version !== revision || !node.isConnected) return;
        preview.innerHTML = svg; VexDiagramRendering.mount(preview,textarea.value); preview.dataset.stale = 'false'; error.hidden = true;
      } catch (failure) {
        if (version !== revision || !node.isConnected) return;
        const line = /(?:line|行)\s*(\d+)/i.exec(String(failure));
        error.textContent = '图表语法有误' + (line ? ' · 第 ' + line[1] + ' 行' : '') + '。源码已保留。'; error.hidden = false;
        preview.dataset.stale = 'true';
        if (!preview.querySelector('svg')) preview.textContent = '无法生成预览';
        preview.setAttribute('aria-label','图表预览未更新');
      } finally { if (version === revision) preview.dataset.loading = 'false'; }
    }
    session.render = render;
    function open() {
      if (expanded) return;
      clearTimeout(closeTimer); expanded = true; closing = false;
      shell.inert = false; node.classList.add('is-expanded'); node.setAttribute('aria-expanded','true');
      options.begin?.(); resize(); textarea.focus({preventScroll:true});
    }
    function close() {
      if (!expanded) return;
      expanded = false; closing = true;
      node.classList.remove('is-expanded'); node.setAttribute('aria-expanded','false'); shell.inert = true;
      clearTimeout(closeTimer);
      closeTimer = setTimeout(() => { closing = false; options.finish?.(); },reduced() ? 0 : 230);
    }
    session.close = close;
    node.addEventListener('click',event => { event.stopPropagation(); if (!expanded) open(); });
    node.addEventListener('keydown',event => {
      if (event.isComposing) return;
      if (event.target === node && (event.key === 'Enter' || event.key === ' ')) {event.preventDefault();open();}
      if (event.target === textarea && event.key === 'Tab') {event.preventDefault();textarea.setRangeText('  ',textarea.selectionStart,textarea.selectionEnd,'end');textarea.dispatchEvent(new Event('input',{bubbles:true}));}
      if (event.key === 'Escape' || ((event.ctrlKey || event.metaKey) && event.key === 'Enter')) {event.preventDefault();event.stopPropagation();textarea.blur();close();node.focus({preventScroll:true});}
    });
    function sync() {
      raw = fence.write(textarea.value,'mermaid'); options.change(raw);
      resize(); clearTimeout(timer); timer = setTimeout(render,240);
    }
    textarea.addEventListener('compositionstart',() => {composing = true;});
    textarea.addEventListener('compositionend',() => {composing = false;sync();});
    textarea.addEventListener('input',() => {if (!composing) sync();});
    node.addEventListener('focusout',event => {if (!node.contains(event.relatedTarget)) close();});
    VexDiagramRendering.schedule(preview,render);
  }
  document.addEventListener('pointerdown',event => {
    for (const session of sessions) { if (!session.element.isConnected) {sessions.delete(session);continue;} if (!session.element.contains(event.target)) session.close(); }
  },true);
  window.VexDiagramEditing = {mount,busy(host) {return [...sessions].some(s => host.contains(s.element) && s.busy());}};
})();
