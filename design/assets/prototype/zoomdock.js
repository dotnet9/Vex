/* 缩放坞的百分比气泡：百分比不再常驻，只有手按在拖动条上（拖动或方向键调值）时才浮在拖动块上方，松手即隐。
 * 宿主建好坞后调一次 VexZoomDock.attach(坞元素)接上显隐，此后在原本写百分比的那一处调 VexZoomDock.update(坞元素, 文案)回写文案与位置；
 * 坞的几何与配色仍由 prototype.css 的 .vex-zoomdock 配方决定。 */
(function () {
  'use strict';
  /* Chrome 原生 range 的拖动块宽（含边框），用于把当前值换算成块心横距；气泡按 left 贴这块中心。 */
  var THUMB_W = 15;
  var LINGER = 600;
  var timers = new WeakMap();
  function place(range, value) {
    var min = Number(range.min), max = Number(range.max);
    var ratio = max > min ? (Number(range.value) - min) / (max - min) : 0;
    value.style.left = Math.round(THUMB_W / 2 + ratio * Math.max(0, range.clientWidth - THUMB_W)) + 'px';
  }
  /* ms 省略即常驻（拖动中），给了 ms 就是到点再收（键盘调值、松手、失焦）。 */
  function reveal(value, ms) {
    var pending = timers.get(value);
    if (pending) clearTimeout(pending);
    value.classList.add('is-live');
    if (ms) timers.set(value, setTimeout(function () { value.classList.remove('is-live'); }, ms));
  }
  function dismiss(value, ms) {
    var pending = timers.get(value);
    if (pending) clearTimeout(pending);
    timers.set(value, setTimeout(function () { value.classList.remove('is-live'); }, ms));
  }
  function attach(dock) {
    var range = dock.querySelector('.zoom-range'), value = dock.querySelector('.zoom-value');
    if (!range || !value || range.dataset.dockReady === '1') return;
    range.dataset.dockReady = '1';
    /* 按下先补一次定位：坞可能在隐藏时建的，那时量不到拖动条宽度。 */
    range.addEventListener('pointerdown', function () { place(range, value); reveal(value); });
    ['pointerup', 'pointercancel'].forEach(function (name) { range.addEventListener(name, function () { dismiss(value, 120); }); });
    range.addEventListener('keydown', function () { place(range, value); reveal(value, LINGER); });
    range.addEventListener('blur', function () { dismiss(value, 0); });
  }
  window.VexZoomDock = {
    attach: attach,
    update: function (dock, text) {
      var range = dock.querySelector('.zoom-range'), value = dock.querySelector('.zoom-value');
      if (!range || !value) return;
      value.textContent = text;
      place(range, value);
    }
  };
})();
