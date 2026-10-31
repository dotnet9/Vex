/* 文档大纲：停靠与覆盖显示共用一套开关和焦点恢复。 */
(function () {
  'use strict';
  var W = window.VexWorkbench, $ = function (id) { return document.getElementById(id); };
  var sidebar = $('structure-sidebar'), inspector = $('node-inspector');

  inspector.setAttribute('aria-label', VexL.t('章节内容'));
  inspector.querySelector('[data-action="close-inspector"]').remove();
  function sync() {
    var open = !sidebar.classList.contains('collapsed'), overlay = document.querySelector('.workspace').classList.contains('auxiliary-overlay');
    sidebar.setAttribute('role', overlay ? 'dialog' : 'complementary');
    $('outline-toggle').setAttribute('aria-pressed', String(open));
    $('outline-toggle').title = $('outline-toggle').ariaLabel = VexL.t(open ? '收起文档大纲' : '展开文档大纲');
  }
  function show() {
    W.showStructure(true);
    $('outline-filter').focus({preventScroll:true});
  }
  function close(restore) {
    if (restore || sidebar.contains(document.activeElement)) $('outline-toggle').focus({preventScroll:true});
    W.showStructure(false);
  }
  function handleAction(action) {
    if (action === 'toggle-structure') {
      if (!sidebar.classList.contains('collapsed')) close(true);
      else show();
      return true;
    }
    if (action === 'pane-outline') { show(); return true; }
    return false;
  }

  window.addEventListener('vex-language-changed', sync);
  document.addEventListener('pointerdown', function (event) {
    if (!sidebar.classList.contains('collapsed') && document.querySelector('.workspace').classList.contains('auxiliary-overlay') && event.target.closest('#view-surface, .sidebar')) close(false);
  });
  document.addEventListener('keydown', function (event) {
    if (event.key !== 'Escape' || sidebar.classList.contains('collapsed') || event.defaultPrevented) return;
    var popup = Array.from(document.querySelectorAll('[role="dialog"], .overlay, .vex-overlay, .typography-popover, .settings-popover, .chapter-delete-dialog, .file-context-menu, .menu-flyout, .vex-element-menu, .save-details, details[open]#vex-appearance-menu')).some(function (element) { return !sidebar.contains(element) && element.getClientRects().length > 0; });
    if (event.target.closest('.chapter-title-input') || popup) return;
    event.preventDefault(); event.stopPropagation(); close(true);
  }, true);
  var beginGuide = VexProto.beginGuideSession, endGuide = VexProto.endGuideSession;
  VexProto.beginGuideSession = function () { var snapshot = beginGuide(); snapshot.outlineOpen = !sidebar.classList.contains('collapsed'); return snapshot; };
  VexProto.endGuideSession = function (snapshot) { endGuide(snapshot); if (snapshot) W.showStructure(snapshot.outlineOpen); };
  window.VexAuxiliary = {close:close, sync:sync, handleAction:handleAction};
  sync();
})();

/* 阅读视图右下角缩放坞：与演示正文同一枚 .vex-zoomdock，量程对齐原生 ContentZoom（50–200，还原 100）。 */
(function () {
  'use strict';
  var pane = document.getElementById('reading-pane');
  if (!pane) return;
  var dock = document.createElement('footer');
  dock.className = 'vex-zoomdock reading-zoom';
  dock.innerHTML = '<div><span class="zoom-tools"><input type="range" class="zoom-range" min="50" max="200" step="5" data-l10n-label="内容缩放" aria-label="' + VexL.t('内容缩放') + '"><span class="zoom-value" aria-hidden="true"></span></span><button type="button" class="zoom-reset" data-l10n-tip="还原字号" data-l10n-label="还原字号"><svg class="ico" aria-hidden="true"><use href="#i-fit"/></svg></button></div>';
  pane.appendChild(dock);
  VexZoomDock.attach(dock);
  var range = dock.querySelector('.zoom-range'), reset = dock.querySelector('.zoom-reset');
  function sync() { var zoom = Number(VexPreferences.values.zoom) || 100; range.value = zoom; VexZoomDock.update(dock, Math.round(zoom) + '%'); }
  range.addEventListener('input', function () { VexPreferences.set('zoom', Number(range.value)); });
  reset.onclick = function () { VexPreferences.set('zoom', 100); };
  var tip = VexL.t('还原字号');
  reset.title = tip;
  reset.setAttribute('aria-label', tip);
  window.addEventListener('vex-preference-changed', function (event) { if (event.detail.key === 'zoom') sync(); });
  /* 缩放坞收进整窗底栏右端：正文与导图共用一个槽位，随布局渲染切换可见性，不再悬浮压住内容。 */
  var slot = document.createElement('div'); slot.className = 'statusbar-zoom';
  var bar = document.querySelector('.window-body>.document-statusbar');
  bar.insertBefore(slot, bar.querySelector('.document-word-count'));
  var mapDock = document.querySelector('.map-controls.vex-zoomdock');
  slot.appendChild(dock); if (mapDock) slot.appendChild(mapDock);
  function syncSlot() {
    var mapOn = !document.getElementById('map-pane').hidden;
    var readOn = !document.getElementById('reading-pane').hidden;
    if (mapDock) mapDock.hidden = !mapOn;
    dock.hidden = mapOn || !readOn;
  }
  document.addEventListener('vex-layout-rendered', syncSlot);
  syncSlot();
  sync();
})();
