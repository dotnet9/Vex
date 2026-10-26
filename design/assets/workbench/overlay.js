/* Keep shared surfaces inside the adaptive device, without disabling the surface itself. */
(() => {
  'use strict';
  const scope = () => document.getElementById('app') || document.body;
  function mount(host) {
    const parent = scope();
    host.classList.toggle('vex-contained-overlay',parent !== document.body);
    parent.append(host);
    return parent;
  }
  function lock(host) {
    const focus = document.activeElement;
    const siblings = [...host.parentElement.children].filter(el => el !== host).map(el => [el,el.inert]);
    siblings.forEach(([el]) => el.inert = true);
    let released = false;
    return () => {
      if (released) return;
      released = true;
      siblings.forEach(([el,inert]) => el.inert = inert);
      if (focus?.isConnected && !focus.closest('[inert]')) focus.focus({preventScroll:true});
    };
  }
  window.VexOverlay = {scope,mount,lock,width:() => scope().clientWidth};
})();
