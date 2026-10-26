/* 分段控件行为：为 .vex-segmented 注入指示块，选中项滑动定位，方向键在段间漫游。
 * role=tablist 方向键即切换（与 WAI-ARIA 手动/自动激活约定一致），role=group 只移动焦点，交给回车激活。 */
(function () {
  'use strict';
  var frame = 0;
  var ro = window.ResizeObserver ? new ResizeObserver(function () { schedule(); }) : null;
  function segments(el) {
    return Array.prototype.filter.call(el.children, function (node) {
      return node.tagName === 'BUTTON' && !node.classList.contains('vex-segmented-thumb');
    });
  }
  function activeOf(list) {
    for (var i = 0; i < list.length; i++) {
      var b = list[i];
      if (b.getAttribute('aria-pressed') === 'true' || b.getAttribute('aria-selected') === 'true' || b.classList.contains('on')) return b;
    }
    return null;
  }
  function sync(el) {
    var thumb = el.querySelector(':scope > .vex-segmented-thumb');
    if (!thumb) return;
    var list = segments(el), active = activeOf(list);
    if (!active || !active.offsetWidth) { thumb.style.width = '0px'; return; }
    thumb.style.width = active.offsetWidth + 'px';
    thumb.style.setProperty('--seg-x', active.offsetLeft + 'px');
    for (var i = 0; i < list.length; i++) list[i].tabIndex = list[i] === active ? 0 : -1;
  }
  function upgrade(el) {
    if (el.dataset.segReady === '1') return;
    el.dataset.segReady = '1';
    var thumb = document.createElement('span');
    thumb.className = 'vex-segmented-thumb';
    thumb.setAttribute('aria-hidden', 'true');
    el.insertBefore(thumb, el.firstChild);
    el.addEventListener('keydown', function (event) {
      var list = segments(el), from = list.indexOf(document.activeElement);
      if (from < 0) return;
      var forward = event.key === 'ArrowRight' || event.key === 'ArrowDown';
      var back = event.key === 'ArrowLeft' || event.key === 'ArrowUp';
      if (!forward && !back && event.key !== 'Home' && event.key !== 'End') return;
      event.preventDefault();
      var next = event.key === 'Home' ? 0 : event.key === 'End' ? list.length - 1 : (from + (forward ? 1 : -1) + list.length) % list.length;
      var target = list[next];
      target.tabIndex = 0;
      target.focus();
      if (el.getAttribute('role') === 'tablist') target.click();
    });
    if (ro) { ro.observe(el); segments(el).forEach(function (b) { ro.observe(b); }); }
    sync(el);
  }
  function scan() {
    document.querySelectorAll('.vex-segmented').forEach(upgrade);
    document.querySelectorAll('.vex-segmented[data-seg-ready]').forEach(sync);
  }
  function schedule() {
    if (frame) cancelAnimationFrame(frame);
    frame = requestAnimationFrame(function () { frame = 0; scan(); });
  }
  function watch() {
    new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['aria-pressed', 'aria-selected', 'class'] });
    window.addEventListener('resize', schedule);
    scan();
  }
  if (document.body) watch(); else document.addEventListener('DOMContentLoaded', watch);
  window.VexSegmented = { sync: schedule, scan: scan };
})();
