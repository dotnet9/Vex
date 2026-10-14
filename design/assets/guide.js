/* 聚光引导：操作真实控件，练习内容与进入前的文档隔离。 */
(function () {
  'use strict';
  var $ = function (selector) { return document.querySelector(selector); };
  var storageKey = 'vex-design-guide', session = null, stepIndex = 0, open = false, completed = false;
  var frame = null, tween = null, settledTimer = null, rectangles = [], previousFocus = null, tasks = new Set(), pendingAnimation = false;
  var layer = document.createElement('div'); layer.className = 'tour-layer'; layer.id = 'vex-guide'; layer.hidden = true;
  layer.innerHTML = '<svg class="tour-mask" aria-hidden="true"><path class="tour-mask-path"></path><path class="tour-connector"></path></svg><div class="tour-rings"></div><section class="tour-card" role="dialog" aria-modal="false" aria-labelledby="tour-title" aria-describedby="tour-description"></section>';
  document.body.appendChild(layer);
  var card = $('.tour-card'), mask = $('.tour-mask-path'), rings = $('.tour-rings'), connector = $('.tour-connector');
  var viewsVisual = '<div class="tour-views"><span>编辑</span><i>↔</i><span>阅读</span><i>↔</i><span>导图</span></div>';
  var codeVisual = '<div class="tour-code-demo"><code>## 一个值得写下来的想法</code><strong>一个值得写下来的想法</strong></div>';
  var colorVisual = '<div class="tour-color-demo">' + '<span><i></i><b></b><b></b><b></b></span>'.repeat(3) + '</div>';
  var treeVisual = '<svg class="tour-tree-demo" viewBox="0 0 320 94" aria-hidden="true"><path d="M107 47H135Q146 47 146 26V23H188M146 47V69Q146 72 155 72H188"/><rect class="tree-root" x="30" y="33" width="77" height="28" rx="6"/><text class="tree-root-label" x="43" y="51">你的 Markdown</text><rect x="188" y="11" width="102" height="25" rx="5"/><text x="201" y="27">标题 · 结构主干</text><rect x="188" y="60" width="102" height="25" rx="5" stroke-dasharray="3 3"/><text x="201" y="76">段落 · 内容摘要</text></svg>';
  var shortcutsVisual = '<div class="tour-shortcuts"><span><kbd>Ctrl+S</kbd>保存</span><span><kbd>Ctrl+P</kbd>快速打开</span><span><kbd>Ctrl+F</kbd>查找</span><span><kbd>F8</kbd>专注模式</span></div>';
  var steps = [
    {
      name: '视角', title: '一份内容，三个视角', visual: viewsVisual,
      description: '编辑用来写，阅读用来看，导图用来梳理结构。切换视角时，内容始终是同一份 Markdown。',
      tip: ['先认识工作台', '左侧放文档和大纲，正文居中；顶部切换视角，底部查看状态。'],
      task: '试着点击上方“阅读”或“导图”，看看同一份内容。',
      targets: function () { return ['.view-switch']; }, allow: '.view-switch button', placement: 'bottom',
      prepare: function () { VexProto.handleAction('view-edit'); }
    },
    {
      name: '写作', title: '写的时候，就能看见效果', visual: codeVisual,
      description: '点击聚光中的段落直接编辑。当前块显示输入与实时效果，其余内容保持排版；离开这一块，正文就恢复排版。',
      tip: ['编辑有两种方式', '“即时渲染”适合专注写作；“源码对照”适合检查 Markdown。它们都属于编辑视图。'],
      task: '点击高亮段落，输入几个字或 **加粗**，看看实时效果。',
      targets: function () { return ['#instant-content > .instant-block:nth-child(2)']; },
      allow: '#instant-content > .instant-block:nth-child(2), #instant-content > .instant-block:nth-child(2) *', placement: 'left',
      prepare: function () { VexProto.handleAction('edit-instant'); $('.instant-scroll').scrollTop = 0; }
    },
    {
      name: '排版', title: '让正文有自己的气质', visual: colorVisual,
      description: '选择一种 Markdown 排版，标题、段落和引用会一起变化。即时编辑、阅读和 HTML 导出共用这套排版。',
      tip: ['正文排版 ≠ 界面配色', '排版决定文章的样子；“视图 → 界面配色”决定工作台的颜色和浅深模式。'],
      task: '点击右侧任意排版卡片，观察正文标题的变化。',
      targets: function () { return ['#typography-panel', '#markdown-preview h2']; },
      allow: '#typography-panel button, #typography-panel input', placement: 'left',
      extras: [['typography', '打开排版主题']],
      prepare: function () { VexProto.handleAction('view-read'); $('.reading-scroll').scrollTop = 0; $('#typography-panel').hidden = false; }
    },
    {
      name: '导图', title: '看见结构，也看见内容', visual: treeVisual,
      description: '标题组成主干，段落补充摘要。默认最多显示 30 字，可以调整；点击节点查看全文，截断只影响显示。',
      tip: ['三种常用操作', '点击分支折叠；拖动画布平移；Ctrl + 滚轮缩放。节点详情还可以定位到源码。'],
      task: '把摘要改成 12 字，或点击高亮节点查看全文。',
      targets: function () { return [!$('#map-settings').hidden ? '#map-settings' : !$('#node-inspector').hidden ? '#node-inspector' : '#map-settings-button', '.map-node[data-kind="paragraph"]']; },
      allow: '#map-settings input, #map-settings button, #node-inspector button, #map-settings-button, .map-node[data-kind="paragraph"] button', placement: 'left',
      extras: [['summary', '摘要设置'], ['fulltext', '查看节点全文']],
      prepare: function () { VexProto.handleAction('view-mindmap'); VexProto.handleAction('map-settings'); requestAnimationFrame(function () { VexProto.handleAction('map-fit'); }); }
    },
    {
      name: '日常', title: '写完之后，记得保存', visual: shortcutsVisual,
      description: '“文件”集中放保存、导出和打印。底部的修改状态会告诉你内容是否已保存；Markdown 排版可以随 HTML 一起导出。',
      tip: ['记住几个入口就够了', 'Ctrl+P 快速打开，Ctrl+F 查找，F8 专注写作。以后从“帮助 → 新手引导”随时重新体验。'],
      task: '可以保存演练文档的副本，或展开“导出”看看格式。',
      targets: function () { return $('.menu-flyout').style.display !== 'none' ? ['.menu-item[data-menu-label="保存"]', '.menu-item[data-menu-label="导出"]', '.menu-item[data-menu-action="export-html"]', '#save-status'] : ['.save-button', '#save-status']; },
      allow: '.tb-menuitem[data-menu="文件(F)"], .save-button, .menu-item[data-menu-action="save"], .menu-item[data-menu-label="导出"], .menu-item[data-menu-action="export-html"]', placement: 'right',
      extras: [['exports', '查看导出格式']],
      prepare: function () { VexProto.handleAction('view-read'); $('.tb-menuitem[data-menu="文件(F)"]').click(); }
    }
  ];
  function reduced() { return document.documentElement.dataset.motion === 'false' || matchMedia('(prefers-reduced-motion: reduce)').matches; }
  function storeResult(value) { try { localStorage.setItem(storageKey, value); } catch (_) {} $('#guide-launch').hidden = true; }
  try { $('#guide-launch').hidden = !!localStorage.getItem(storageKey); } catch (_) {}
  function hideExtras() { ['#typography-panel', '#map-settings', '#node-inspector', '#vex-theme-panel'].forEach(function (selector) { var node = $(selector); if (node) node.hidden = true; }); VexProto.closeMenu(); }
  function renderCard() {
    var step = steps[stepIndex];
    card.classList.remove('tour-completed'); card.setAttribute('aria-modal', 'false');
    card.innerHTML = '<div class="tour-meta"><span class="tour-wordmark">V</span><span>快速上手</span><span class="tour-count">' + (stepIndex + 1) + ' / 5</span><button class="tour-close" data-tour="close" aria-label="退出引导">×</button></div>' +
      '<div class="tour-content"><div class="tour-visual">' + step.visual + '</div><h2 id="tour-title">' + step.title + '</h2><p class="tour-description" id="tour-description">' + step.description + '</p><div class="tour-tip"><strong>' + step.tip[0] + '</strong>' + step.tip[1] + '</div><div class="tour-feedback" aria-live="polite">' + step.task + '</div>' +
      (step.extras ? '<div class="tour-extras">' + step.extras.map(function (extra) { return '<button class="tour-extra" data-tour="' + extra[0] + '">' + extra[1] + '</button>'; }).join('') + '</div>' : '') + '</div>' +
      '<nav class="tour-progress" aria-label="引导步骤">' + steps.map(function (item, index) { return '<button class="' + (index < stepIndex ? 'passed' : '') + '" data-tour-step="' + index + '" aria-label="第 ' + (index + 1) + ' 步：' + item.name + '"' + (index === stepIndex ? ' aria-current="step"' : '') + '>' + item.name + '</button>'; }).join('') + '</nav>' +
      '<div class="tour-actions"><button class="tour-skip" data-tour="close">跳过引导</button><button class="tour-previous" data-tour="previous"' + (stepIndex === 0 ? ' hidden' : '') + '>上一步</button><button class="tour-next" data-tour="next">' + (stepIndex === 4 ? '完成体验' : '下一步') + '<span aria-hidden="true">→</span></button></div><div class="tour-footnote">← → 切换步骤 · Esc 退出 · 结束后回到你的文档</div>';
    card.classList.remove('changing'); void card.offsetWidth; card.classList.add('changing'); updateFeedback();
  }
  function updateFeedback() {
    if (!open || completed) return;
    var feedback = $('.tour-feedback'); if (!feedback) return;
    var messages = ['已切换视角。内容会一起变化，继续体验写作。', '已看到实时效果。Ctrl+Enter 可以完成当前块。', '排版已更新。选中的样式也会用于正文预览。', '摘要与全文是两回事；改变长度不会改写内容。', '已体验保存 / 导出。日常操作都能从这里找到。'];
    feedback.classList.toggle('done', tasks.has(stepIndex));
    var text = tasks.has(stepIndex) ? messages[stepIndex] : steps[stepIndex].task;
    if (feedback.textContent !== text) feedback.textContent = text;
    var typeButton = card.querySelector('[data-tour="typography"]');
    if (typeButton) typeButton.hidden = !$('#typography-panel').hidden;
  }
  function go(index) {
    if (!open || completed) return;
    stepIndex = Math.max(0, Math.min(4, index)); layer.dataset.step = stepIndex + 1;
    $('.app-window').dataset.guideStep = stepIndex + 1;
    hideExtras(); steps[stepIndex].prepare(); renderCard(); schedule(true);
    $('.tour-next').focus({ preventScroll: true });
    clearTimeout(settledTimer); settledTimer = setTimeout(function () { schedule(); }, 380);
  }
  function start() {
    if (open) return;
    previousFocus = document.activeElement; tasks.clear(); completed = false; rectangles = [];
    session = VexProto.beginGuideSession(); open = true; layer.hidden = false; go(0);
  }
  function restore() {
    if (!session) return;
    var snapshot = session, wasOpen = open; session = null; open = false;
    try { VexProto.endGuideSession(snapshot); } finally { open = wasOpen; }
  }
  function close() {
    if (!open) return;
    open = false; clearTimeout(settledTimer); cancelAnimationFrame(frame); cancelAnimationFrame(tween);
    layer.hidden = true; delete $('.app-window').dataset.guideStep; hideExtras(); restore(); storeResult(completed ? 'completed' : 'skipped');
    var target = previousFocus && previousFocus.isConnected && previousFocus.getBoundingClientRect().width && !previousFocus.closest('[hidden]') ? previousFocus : $('.tb-menuitem[data-menu="帮助(H)"]');
    if (target) target.focus({ preventScroll: true });
  }
  function finish() {
    completed = true; delete $('.app-window').dataset.guideStep; hideExtras(); restore(); storeResult('completed');
    card.classList.add('tour-completed'); card.setAttribute('aria-modal', 'true');
    card.innerHTML = '<div class="tour-meta"><span class="tour-wordmark">V</span><span>准备就绪</span><span class="tour-count">5 / 5</span><button class="tour-close" data-tour="close" aria-label="关闭引导">×</button></div><div class="tour-content"><div class="tour-visual"><div class="tour-success"><b>✓</b></div></div><h2 id="tour-title">现在，写下你的想法</h2><p class="tour-description" id="tour-description">你已经认识了写作、阅读、排版和导图。原来的文档与设置已经恢复，接下来可以继续写，或打开一篇自己的 Markdown。</p><div class="tour-tip"><strong>需要时，随时回来</strong>帮助 → 新手引导。无需记住每个菜单，先把注意力留给内容。</div></div><div class="tour-actions"><button class="tour-previous" data-tour="open">打开 Markdown…</button><button class="tour-next" data-tour="close">回到文档<span aria-hidden="true">→</span></button></div>';
    schedule(true); $('.tour-next').focus({ preventScroll: true });
  }
  card.addEventListener('click', function (event) {
    event.stopPropagation(); var jump = event.target.closest('[data-tour-step]');
    if (jump) { go(Number(jump.dataset.tourStep)); return; }
    var button = event.target.closest('[data-tour]'); if (!button) return;
    switch (button.dataset.tour) {
      case 'close': close(); break;
      case 'previous': go(stepIndex - 1); break;
      case 'next': if (stepIndex === 4) finish(); else go(stepIndex + 1); break;
      case 'summary': $('#node-inspector').hidden = true; if ($('#map-settings').hidden) VexProto.handleAction('map-settings'); schedule(); break;
      case 'fulltext': var node = $('.map-node[data-kind="paragraph"] .map-node-button'); if (node) { node.click(); tasks.add(3); updateFeedback(); schedule(); } break;
      case 'typography': $('#typography-panel').hidden = false; schedule(); break;
      case 'exports':
        if ($('.menu-flyout').style.display === 'none') $('.tb-menuitem[data-menu="文件(F)"]').click();
        var item = $('.menu-item[data-menu-label="导出"]');
        if (item && item._sub.style.display !== 'block') item.click();
        schedule(); break;
      case 'open': close(); VexProto.handleAction('open'); break;
    }
  });
  function allowed(target) {
    if (card.contains(target)) return true;
    if (completed) return false;
    var matched = target.closest(steps[stepIndex].allow);
    if (matched && matched.dataset.menuLabel === '导出' && target.closest('.menu-item') !== matched) return false;
    return !!matched;
  }
  ['pointerdown', 'click'].forEach(function (eventName) {
    document.addEventListener(eventName, function (event) {
      if (!open || allowed(event.target)) return;
      event.preventDefault(); event.stopImmediatePropagation();
    }, true);
  });
  document.addEventListener('click', function (event) {
    if (!open || completed || card.contains(event.target)) return;
    var target = event.target;
    if ((stepIndex === 0 && target.closest('.view-switch')) || (stepIndex === 2 && target.closest('[data-typography-key]')) || (stepIndex === 3 && target.closest('.map-node-button')) || (stepIndex === 4 && target.closest('[data-menu-action="save"], [data-menu-action="export-html"]'))) {
      tasks.add(stepIndex); requestAnimationFrame(updateFeedback);
    }
    schedule();
  }, true);
  document.addEventListener('input', function (event) {
    if (!open || completed) return;
    if ((stepIndex === 1 && event.target.matches('.instant-input')) || (stepIndex === 3 && event.target.id === 'summary-length' && event.target.validity.valid)) tasks.add(stepIndex);
    updateFeedback(); schedule();
  }, true);
  function focusables() {
    var selector = 'button:not([disabled]), input:not([disabled]), textarea:not([disabled]), [tabindex="0"]';
    return Array.from(document.querySelectorAll(selector)).filter(function (node) {
      return allowed(node) && !node.closest('[hidden]') && node.getBoundingClientRect().width > 0 && getComputedStyle(node).visibility !== 'hidden';
    });
  }
  document.addEventListener('keydown', function (event) {
    if (!open || event.isComposing) return;
    var editing = event.target.closest('input, textarea');
    if (event.key === 'Escape') { event.preventDefault(); event.stopImmediatePropagation(); close(); return; }
    if (event.key === 'Tab') {
      event.preventDefault(); event.stopImmediatePropagation(); var items = focusables(), index = items.indexOf(document.activeElement);
      if (items.length) items[(index + (event.shiftKey ? items.length - 1 : 1)) % items.length].focus({ preventScroll: true }); return;
    }
    if (!editing && !event.ctrlKey && !event.metaKey && ['ArrowLeft', 'ArrowRight'].includes(event.key)) {
      event.preventDefault(); event.stopImmediatePropagation(); if (!completed) go(stepIndex + (event.key === 'ArrowRight' ? 1 : -1)); return;
    }
    if ((event.ctrlKey || event.metaKey) && ['s', 'o', 'n', 'w', 'p', 'f', 'h'].includes(event.key.toLowerCase())) {
      if (!completed && stepIndex === 4 && event.key.toLowerCase() === 's') { tasks.add(4); updateFeedback(); return; }
      event.preventDefault(); event.stopImmediatePropagation();
    }
    if (['F5', 'F8', 'F11'].includes(event.key) || (event.altKey && event.key === 'Enter')) { event.preventDefault(); event.stopImmediatePropagation(); }
  }, true);
  document.addEventListener('focusin', function (event) {
    if (open && !allowed(event.target)) { var next = $('.tour-next'); if (next) next.focus({ preventScroll: true }); }
  });
  function targetRect(selector) {
    var node = $(selector); if (!node || node.closest('[hidden]')) return null;
    var rect = node.getBoundingClientRect(), x = rect.left, y = rect.top, right = rect.right, bottom = rect.bottom;
    if (node.matches('#markdown-preview h2')) {
      var textRange = document.createRange(); textRange.selectNodeContents(node); var textRect = textRange.getBoundingClientRect();
      x = Math.max(x, textRect.left - 12); right = Math.min(right, textRect.right + 12);
    }
    var viewport = node.closest('.instant-scroll, .reading-scroll, #map-viewport');
    if (viewport) { var bounds = viewport.getBoundingClientRect(); x = Math.max(x, bounds.left); y = Math.max(y, bounds.top); right = Math.min(right, bounds.right); bottom = Math.min(bottom, bounds.bottom); }
    if (right <= x || bottom <= y) return null;
    x = Math.max(6, x - 8); y = Math.max(6, y - 8); right = Math.min(innerWidth - 6, right + 8); bottom = Math.min(innerHeight - 6, bottom + 8);
    return { x: x, y: y, width: right - x, height: bottom - y };
  }
  function roundedPath(rect) {
    var x = rect.x, y = rect.y, w = rect.width, h = rect.height, r = Math.min(11, w / 2, h / 2);
    return 'M' + (x + r) + ' ' + y + 'H' + (x + w - r) + 'Q' + (x + w) + ' ' + y + ' ' + (x + w) + ' ' + (y + r) + 'V' + (y + h - r) + 'Q' + (x + w) + ' ' + (y + h) + ' ' + (x + w - r) + ' ' + (y + h) + 'H' + (x + r) + 'Q' + x + ' ' + (y + h) + ' ' + x + ' ' + (y + h - r) + 'V' + (y + r) + 'Q' + x + ' ' + y + ' ' + (x + r) + ' ' + y + 'Z';
  }
  function draw(rects) {
    mask.setAttribute('d', 'M0 0H' + innerWidth + 'V' + innerHeight + 'H0Z' + rects.map(roundedPath).join(''));
    while (rings.children.length > rects.length) rings.lastChild.remove();
    rects.forEach(function (rect, index) {
      var ring = rings.children[index]; if (!ring) { ring = document.createElement('div'); ring.className = 'tour-focus'; rings.appendChild(ring); }
      Object.assign(ring.style, { left: rect.x + 'px', top: rect.y + 'px', width: rect.width + 'px', height: rect.height + 'px' });
    });
  }
  function overlap(a, b) { return Math.max(0, Math.min(a.x + a.width, b.x + b.width) - Math.max(a.x, b.x)) * Math.max(0, Math.min(a.y + a.height, b.y + b.height) - Math.max(a.y, b.y)); }
  function position(rects) {
    var width = card.getBoundingClientRect().width, height = card.getBoundingClientRect().height, inset = 14;
    if (!rects.length) { card.style.left = Math.max(inset, (innerWidth - width) / 2) + 'px'; card.style.top = Math.max(inset, (innerHeight - height) / 2) + 'px'; connector.setAttribute('d', ''); return; }
    var rect = rects[0], candidates = {
      left: { x: rect.x - width - 24, y: rect.y + rect.height / 2 - height / 2 },
      right: { x: rect.x + rect.width + 24, y: rect.y + rect.height / 2 - height / 2 },
      bottom: { x: rect.x + rect.width / 2 - width / 2, y: rect.y + rect.height + 24 },
      top: { x: rect.x + rect.width / 2 - width / 2, y: rect.y - height - 24 },
      edgeLeft: { x: inset, y: rect.y + rect.height / 2 - height / 2 },
      edgeRight: { x: innerWidth - width - inset, y: rect.y + rect.height / 2 - height / 2 }
    };
    var preferred = completed ? 'bottom' : steps[stepIndex].placement;
    var order = [preferred].concat(Object.keys(candidates).filter(function (name) { return name !== preferred; }));
    var places = order.map(function (name, index) {
      var raw = candidates[name], place = { x: Math.max(inset, Math.min(innerWidth - width - inset, raw.x)), y: Math.max(inset, Math.min(innerHeight - height - inset, raw.y)), width: width, height: height };
      place.score = rects.reduce(function (score, target) { return score + overlap(place, target); }, 0) * 10 + index * 50;
      return place;
    }).sort(function (a, b) { return a.score - b.score; });
    var best = places[0]; card.style.left = best.x + 'px'; card.style.top = best.y + 'px';
    var left = best.x + width / 2 < rect.x + rect.width / 2;
    var startX = left ? best.x + width : best.x, startY = Math.max(best.y + 36, Math.min(best.y + height - 30, rect.y + rect.height / 2));
    var endX = left ? rect.x : rect.x + rect.width, endY = Math.max(rect.y + 10, Math.min(rect.y + rect.height - 10, startY));
    var middle = (startX + endX) / 2;
    connector.setAttribute('d', overlap(best, rect) ? '' : 'M' + startX + ' ' + startY + 'C' + middle + ' ' + startY + ' ' + middle + ' ' + endY + ' ' + endX + ' ' + endY);
  }
  function layout(animate) {
    if (!open) return;
    if (!completed && stepIndex === 3) VexProto.arrangeGuideMap();
    var next = completed ? [] : steps[stepIndex].targets().map(targetRect).filter(Boolean);
    position(next); cancelAnimationFrame(tween);
    if (!animate || reduced() || !rectangles.length) { rectangles = next; draw(next); return; }
    var from = rectangles, startTime = performance.now();
    function tick(now) {
      if (!open) return;
      var progress = Math.min(1, (now - startTime) / 340), ease = 1 - Math.pow(1 - progress, 3);
      rectangles = next.map(function (target, index) { var old = from[index] || target, rect = {}; ['x', 'y', 'width', 'height'].forEach(function (key) { rect[key] = old[key] + (target[key] - old[key]) * ease; }); return rect; });
      draw(rectangles); if (progress < 1) tween = requestAnimationFrame(tick);
    }
    tween = requestAnimationFrame(tick);
  }
  function schedule(animate) {
    if (!open) return;
    pendingAnimation = pendingAnimation || !!animate; cancelAnimationFrame(frame);
    frame = requestAnimationFrame(function () { var requested = pendingAnimation; pendingAnimation = false; updateFeedback(); layout(requested); });
  }
  new MutationObserver(function () { schedule(); }).observe($('.app-window'), { subtree: true, childList: true, attributes: true, attributeFilter: ['hidden', 'style', 'data-active'] });
  new ResizeObserver(function () { schedule(); }).observe($('.app-window'));
  window.addEventListener('resize', function () { schedule(); });
  document.addEventListener('scroll', function () { schedule(); }, true);
  window.VexGuide = { start: start, close: close, get active() { return open; } };
  if (new URLSearchParams(location.search).get('tour') === '1') requestAnimationFrame(start);
})();
