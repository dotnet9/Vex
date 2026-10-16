/* 六步演练使用真实控件；退出时恢复当前文档、布局与偏好。 */
(function () {
  'use strict';
  var $ = function (selector) { return document.querySelector(selector); };
  var open = false, preparing = false, completed = false, session = null, stepIndex = 0, previousFocus;
  var frame, settledTimer, feedback = [], stepText = '';
  var storageKey = 'vex-design-guide';
  var layer = document.createElement('div'); layer.id = 'vex-guide'; layer.className = 'tour-layer'; layer.hidden = true;
  layer.innerHTML = '<svg class="tour-mask" aria-hidden="true"><defs><mask id="tour-cutouts" maskUnits="userSpaceOnUse"><rect width="100%" height="100%" fill="white"></rect><g class="tour-cutouts" fill="black"></g></mask></defs><rect class="tour-scrim" width="100%" height="100%" mask="url(#tour-cutouts)"></rect></svg><div class="tour-rings"></div><section class="tour-card" role="dialog" aria-modal="false" aria-labelledby="tour-title" aria-describedby="tour-description"></section>';
  document.body.appendChild(layer);
  var card = $('.tour-card'), cutouts = $('.tour-cutouts'), rings = $('.tour-rings');
  function visible(selector) { var node = $(selector); return !!node && !node.closest('[hidden]') && node.getBoundingClientRect().width > 0; }
  var steps = [
    {
      name: '写作', title: '直接写作', description: '点击正文直接写，也可切换源码编辑 Markdown。', tip: '这里是演练文档，结束后会回到原文。',
      targets: function () { return ['.edit-methods', visible('#instant-pane') ? '#instant-content > .instant-block:nth-child(2)' : '#markdown-input']; },
      allow: '.edit-methods button, #instant-content .instant-block, #markdown-input',
      prepare: function () { VexWorkspace.setLayout('instant'); $('.instant-scroll').scrollTop = 0; }
    },
    {
      name: '导航', title: '文件与大纲', description: '文件列表切换当前文档，大纲帮你快速定位章节。', tip: '点一个大纲章节，看看正文定位到哪里。',
      targets: function () { return ['.side-tabs', '#view-surface'].concat($('.side-tab.on').dataset.pane === 'outline' ? ['#outline-body'] : ['.sidebar-search', '.file-row.selected']); },
      allow: '.side-tabs button, #outline-body button, #file-filter, .file-row.selected',
      prepare: function () { VexWorkspace.setLayout('instant'); VexProto.handleAction('pane-outline'); }
    },
    {
      name: '章节', title: '先梳理，再展开', description: '双击修改标题；拖动章节时，正文和子章节一起移动。', tip: '也可以从节点详情新增同级或子章节。',
      targets: function () { return ['@chapters'].concat(visible('#node-inspector') ? ['#node-inspector'] : []).concat(visible('#chapter-delete-dialog') ? ['#chapter-delete-dialog > section'] : []); },
      allow: '#map-viewport, .chapter-toolbar, #node-inspector, #chapter-delete-dialog',
      prepare: function () {
        VexWorkspace.setLayout('map');
        if ($('#chapter-headings-only').getAttribute('aria-pressed') !== 'true') $('#chapter-headings-only').click();
        requestAnimationFrame(function () { requestAnimationFrame(function () { if (open && stepIndex === 2) fitChapters(); }); });
      }
    },
    {
      name: '布局', title: '边写边看全局', description: '试选视图组合，源码、正文和导图始终同步。', tip: '多屏时，可从布局面板将视图分到独立窗口。',
      targets: function () { return ['#workspace-layout', '.view-switch', '#view-surface'].concat(visible('#workspace-layout-menu') ? ['#workspace-layout-menu'] : []); },
      allow: '#workspace-layout, .view-switch button, #workspace-layout-menu .layout-preset, #reset-workspace, #workspace-layout-menu .workspace-menu-heading button',
      prepare: function () { VexWorkspace.setLayout('instant-map'); fitComparison(); }
    },
    {
      name: '排版', title: '让文章有自己的样子', description: '点击排版试选正文样式，也可以切换紧凑布局。', tip: '正文排版与界面配色分别设置。',
      targets: function () { return ['#edit-options [data-action="typography"]', '#view-surface'].concat(visible('#typography-panel') ? ['#typography-panel'] : []); },
      allow: '#edit-options [data-action="typography"], #typography-panel',
      prepare: function () { VexWorkspace.setLayout('instant'); $('.instant-scroll').scrollTop = 0; }
    },
    {
      name: '分享', title: '保存，也方便分享', description: '保存文档，或将排好版的内容导出、复制到发布平台。', tip: '文件 → 导出；工具 → 复制到公众号等平台。',
      targets: function () {
        return ['.save-button', '.tb-menuitem[data-menu="文件(F)"]', '.tb-menuitem[data-menu="工具(T)"]'].concat(visible('.menu-flyout') ? ['.menu-flyout'] : []).concat(visible('.menu-item[data-menu-label="导出"] .submenu') ? ['.menu-item[data-menu-label="导出"] .submenu'] : []);
      },
      allow: '.save-button, .tb-menuitem[data-menu="文件(F)"], .tb-menuitem[data-menu="工具(T)"], .menu-item[data-menu-action="save"], .menu-item[data-menu-label="导出"], .menu-item[data-menu-action="export-html"], .menu-item[data-menu-label^="复制到"]',
      prepare: function () { VexWorkspace.setLayout('read'); $('.reading-scroll').scrollTop = 0; }
    }
  ];
  try { $('#guide-launch').hidden = !!localStorage.getItem(storageKey); } catch (_) {}
  function hideExtras() {
    if (visible('#chapter-delete-dialog')) $('#chapter-delete-cancel').click();
    ['#typography-panel', '#map-settings', '#node-inspector', '#vex-theme-panel', '#workspace-layout-menu'].forEach(function (selector) { var node = $(selector); if (node) node.hidden = true; });
    $('#workspace-layout').setAttribute('aria-expanded', 'false'); VexProto.closeMenu();
  }
  function mark(index, message) {
    if (!open || preparing || index !== stepIndex) return;
    feedback[index] = message;
    var text = $('.tour-feedback'); if (text) { text.textContent = message; text.classList.add('done'); }
    schedule();
  }
  function renderCard() {
    var step = steps[stepIndex];
    card.innerHTML = '<div class="tour-meta"><span>快速上手</span><span class="tour-count">' + (stepIndex + 1) + ' / ' + steps.length + '</span><button class="tour-close" data-tour="close" aria-label="退出引导">×</button></div>' +
      '<h2 id="tour-title">' + step.title + '</h2><p class="tour-description" id="tour-description">' + step.description + '</p>' +
      '<p class="tour-feedback' + (feedback[stepIndex] ? ' done' : '') + '" aria-live="polite">' + (feedback[stepIndex] || step.tip) + '</p>' +
      (stepIndex === 5 ? '<details class="tour-more"><summary>还有 AI 协作</summary><p>从“工具 → MCP 设置”连接 AI 客户端，协助整理当前文档。</p></details>' : '') +
      '<nav class="tour-progress" aria-label="引导步骤">' + steps.map(function (item, index) { return '<button data-tour-step="' + index + '" aria-label="第 ' + (index + 1) + ' 步：' + item.name + '"' + (index === stepIndex ? ' aria-current="step"' : '') + (index < stepIndex ? ' class="passed"' : '') + '></button>'; }).join('') + '</nav>' +
      '<div class="tour-actions"><button class="tour-skip" data-tour="close">跳过</button><button class="tour-previous" data-tour="previous"' + (stepIndex === 0 ? ' hidden' : '') + '>上一步</button><button class="tour-next" data-tour="next">' + (stepIndex === steps.length - 1 ? '完成' : '下一步') + '<span aria-hidden="true">→</span></button></div>';
  }
  function go(index) {
    if (!open) return;
    preparing = true; hideExtras(); stepIndex = Math.max(0, Math.min(steps.length - 1, index));
    layer.dataset.step = stepIndex + 1; $('.app-window').dataset.guideStep = stepIndex + 1;
    steps[stepIndex].prepare(); stepText = VexWorkbench.text; renderCard(); preparing = false; schedule();
    $('.tour-next').focus({ preventScroll: true });
    clearTimeout(settledTimer); settledTimer = setTimeout(schedule, 260);
  }
  function start() {
    if (open) return;
    previousFocus = document.activeElement; feedback = []; completed = false;
    session = VexProto.beginGuideSession(); open = true; layer.hidden = false; go(0);
  }
  function close() {
    if (!open) return;
    preparing = true; hideExtras(); open = false; preparing = false;
    clearTimeout(settledTimer); cancelAnimationFrame(frame); layer.hidden = true; delete $('.app-window').dataset.guideStep;
    var snapshot = session; session = null; if (snapshot) VexProto.endGuideSession(snapshot);
    try { localStorage.setItem(storageKey, completed ? 'completed' : 'skipped'); } catch (_) {}
    $('#guide-launch').hidden = true;
    var target = previousFocus && previousFocus.isConnected && previousFocus.getBoundingClientRect().width && !previousFocus.closest('[hidden]') ? previousFocus : $('.tb-menuitem[data-menu="帮助(H)"]');
    if (target) target.focus({ preventScroll: true });
  }
  card.addEventListener('click', function (event) {
    event.stopPropagation(); var jump = event.target.closest('[data-tour-step]');
    if (jump) { go(Number(jump.dataset.tourStep)); return; }
    var button = event.target.closest('[data-tour]'); if (!button) return;
    if (button.dataset.tour === 'close') close();
    if (button.dataset.tour === 'previous') go(stepIndex - 1);
    if (button.dataset.tour === 'next') { if (stepIndex === steps.length - 1) { completed = true; close(); } else go(stepIndex + 1); }
  });
  function allowed(target) { return card.contains(target) || !!target.closest(steps[stepIndex].allow); }
  ['pointerdown', 'click', 'dblclick', 'contextmenu'].forEach(function (eventName) {
    document.addEventListener(eventName, function (event) {
      if (!open || preparing || allowed(event.target)) return;
      event.preventDefault(); event.stopImmediatePropagation();
    }, true);
  });
  document.addEventListener('click', function (event) {
    if (!open || preparing || card.contains(event.target)) return;
    var target = event.target;
    if (stepIndex === 1 && target.closest('.outline-item')) mark(1, '已定位到该章节，可以继续浏览大纲。');
    if (stepIndex === 3 && target.closest('.layout-preset, #reset-workspace, .view-switch button')) { fitComparison(); requestAnimationFrame(function () { mark(3, '视图已同步，可继续试选其他组合。'); }); }
    if (stepIndex === 4 && target.closest('[data-typography-key]')) requestAnimationFrame(function () { mark(4, '正文排版已应用，可继续比较。'); });
    if (stepIndex === 5 && target.closest('.save-button, [data-menu-action="save"], [data-menu-action="export-html"]')) mark(5, '演练内容已下载为副本。');
    schedule();
  }, true);
  document.addEventListener('input', function () { schedule(); }, true);
  document.addEventListener('change', function (event) { if (stepIndex === 4 && event.target.id === 'typography-compact') mark(4, '正文间距已更新。'); }, true);
  window.addEventListener('vex-content-changed', function () {
    if (!open || preparing || VexWorkbench.text === stepText) return;
    if (stepIndex === 0) mark(0, '文字已更新，可以继续写作。');
    if (stepIndex === 2) mark(2, '章节已更新，正文同步修改。');
  });
  function focusables() {
    return Array.from(document.querySelectorAll('button:not([disabled]), input:not([disabled]), textarea:not([disabled]), [contenteditable="true"], [tabindex="0"], .tour-more summary')).filter(function (node) {
      return allowed(node) && !node.closest('[hidden]') && node.getBoundingClientRect().width > 0 && getComputedStyle(node).visibility !== 'hidden';
    });
  }
  document.addEventListener('keydown', function (event) {
    if (!open || event.isComposing) return;
    var editing = event.target.closest('input, textarea, [contenteditable="true"]');
    if (event.key === 'Escape') {
      if (stepIndex === 3 && visible('#workspace-layout-menu')) { $('#workspace-layout-menu').hidden = true; $('#workspace-layout').setAttribute('aria-expanded', 'false'); schedule(); }
      else if (stepIndex === 4 && visible('#typography-panel')) { $('#typography-panel').hidden = true; schedule(); }
      else { close(); }
      event.preventDefault(); event.stopImmediatePropagation(); return;
    }
    if (event.key === 'Tab') {
      event.preventDefault(); event.stopImmediatePropagation(); var items = focusables(), index = items.indexOf(document.activeElement);
      if (items.length) items[(index + (event.shiftKey ? items.length - 1 : 1)) % items.length].focus({ preventScroll: true }); return;
    }
    if (!editing && !event.target.closest('.menu-flyout, .view-switch, .tour-more') && !event.ctrlKey && !event.metaKey && ['ArrowLeft', 'ArrowRight'].includes(event.key)) { event.preventDefault(); event.stopImmediatePropagation(); go(stepIndex + (event.key === 'ArrowRight' ? 1 : -1)); return; }
    if ((event.ctrlKey || event.metaKey) && ['s','o','n','w','p','f','h'].includes(event.key.toLowerCase())) {
      if (stepIndex === 5 && event.key.toLowerCase() === 's') { mark(5, '演练内容已下载为副本。'); return; }
      event.preventDefault(); event.stopImmediatePropagation();
    }
    if (['F5','F8','F11'].includes(event.key) || (event.altKey && event.key === 'Enter')) { event.preventDefault(); event.stopImmediatePropagation(); }
  }, true);
  document.addEventListener('focusin', function (event) { if (open && !preparing && !allowed(event.target)) $('.tour-next')?.focus({ preventScroll: true }); });
  function appBounds() { return $('.app-window').getBoundingClientRect(); }
  function targetRect(selector) {
    if (selector === '@chapters') {
      var items = Array.from(document.querySelectorAll('.map-node')).map(function (node) { return node.getBoundingClientRect(); });
      if (!items.length) return null;
      return clipRect({ left: Math.min.apply(null, items.map(function (r) { return r.left; })), top: Math.min.apply(null, items.map(function (r) { return r.top; })), right: Math.max.apply(null, items.map(function (r) { return r.right; })), bottom: Math.max.apply(null, items.map(function (r) { return r.bottom; })) }, $('#map-viewport').getBoundingClientRect(), 'area');
    }
    if (!visible(selector)) return null;
    var node = $(selector), viewport = node.closest('.instant-scroll, .reading-scroll, #map-viewport');
    var kind = node.matches('.instant-block, #markdown-input') ? 'text' : node.id === 'view-surface' ? 'preview' : 'control';
    return clipRect(node.getBoundingClientRect(), viewport ? viewport.getBoundingClientRect() : appBounds(), kind);
  }
  function clipRect(rect, bounds, kind) {
    var app = appBounds(), pad = kind === 'text' ? 4 : 6;
    var x = Math.max(app.left + 4, bounds.left, rect.left - pad), y = Math.max(app.top + 4, bounds.top, rect.top - pad);
    var right = Math.min(app.right - 4, bounds.right, rect.right + pad), bottom = Math.min(app.bottom - 4, bounds.bottom, rect.bottom + pad);
    return right > x && bottom > y ? { x:x, y:y, width:right-x, height:bottom-y, kind:kind } : null;
  }
  function draw(rects) {
    cutouts.replaceChildren(); rings.replaceChildren();
    rects.forEach(function (rect) {
      var hole = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
      Object.entries({x:rect.x,y:rect.y,width:rect.width,height:rect.height,rx:rect.kind === 'text' ? 2 : 7}).forEach(function (pair) { hole.setAttribute(pair[0],pair[1]); }); cutouts.appendChild(hole);
      if (rect.kind === 'preview') return;
      var ring = document.createElement('div'); ring.className = 'tour-focus'; ring.dataset.kind = rect.kind;
      Object.assign(ring.style, {left:rect.x+'px',top:rect.y+'px',width:rect.width+'px',height:rect.height+'px'}); rings.appendChild(ring);
    });
  }
  function position(rects) {
    var bounds = appBounds(), size = card.getBoundingClientRect(), margin = 18, left = bounds.left + margin, top = bounds.top + 70;
    card.style.maxHeight = Math.max(180,bounds.height-2*margin)+'px';
    if (stepIndex === 1) { top = bounds.bottom - size.height - margin; }
    else if (stepIndex === 2) { top = $('#map-viewport').getBoundingClientRect().top + 28; }
    else if (stepIndex === 3 || stepIndex === 4) { top = bounds.bottom - size.height - margin; }
    else if (stepIndex === 5) { left = bounds.right - size.width - margin; top = $('.workbar').getBoundingClientRect().bottom + 20; }
    else {
      var rect = rects.find(function (r) { return r.kind === 'text'; }) || rects[0];
      if (rect) { left = rect.x + 12; top = rect.y + rect.height + 18; if (top + size.height > bounds.bottom-margin) top = rect.y-size.height-18; }
    }
    left = Math.max(bounds.left+margin,Math.min(bounds.right-size.width-margin,left)); top = Math.max(bounds.top+margin,Math.min(bounds.bottom-size.height-margin,top));
    card.style.left = left+'px'; card.style.top = top+'px';
  }
  function fitChapters() {
    if (!open || stepIndex !== 2 || !VexWorkbench.state.bounds) return;
    position([]);
    var viewport = $('#map-viewport').getBoundingClientRect(), guide = card.getBoundingClientRect(), bounds = VexWorkbench.state.bounds;
    var left = Math.max(viewport.left,guide.right+24), width = Math.max(200,viewport.right-left-16), height = viewport.height-36;
    var state = VexWorkbench.state; state.scale = Math.min(1,width/bounds.width,height/bounds.height);
    state.x = left-viewport.left+(width-bounds.width*state.scale)/2; state.y = (viewport.height-bounds.height*state.scale)/2;
    VexWorkbench.renderMap(); schedule();
  }
  function fitComparison() {
    requestAnimationFrame(function () { requestAnimationFrame(function () {
      if (open && stepIndex === 3 && visible('#map-pane')) { VexWorkbench.fit(false); schedule(); }
    }); });
  }
  function layout() { if (!open) return; var rects = steps[stepIndex].targets().map(targetRect).filter(Boolean); position(rects); draw(rects); }
  function schedule() { if (!open) return; cancelAnimationFrame(frame); frame = requestAnimationFrame(layout); }
  new MutationObserver(schedule).observe($('.app-window'), {subtree:true,childList:true,attributes:true,attributeFilter:['hidden','style','data-active']});
  new ResizeObserver(schedule).observe($('.app-window'));
  card.addEventListener('toggle', schedule, true);
  window.addEventListener('resize', function () { schedule(); if (stepIndex === 2) requestAnimationFrame(fitChapters); });
  document.addEventListener('scroll', schedule, true);
  window.VexGuide = {start:start,close:close,get active() {return open;}};
  if (new URLSearchParams(location.search).get('tour') === '1') requestAnimationFrame(start);
})();
