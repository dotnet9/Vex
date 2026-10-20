(function () {
  'use strict';
  function scrollTarget(scroll, caret) {
    var viewport = scroll.getBoundingClientRect(), scale = viewport.height / scroll.offsetHeight || 1;
    var middle = viewport.top + (scroll.clientTop + scroll.clientHeight / 2) * scale;
    return Math.max(0, Math.min(scroll.scrollHeight - scroll.clientHeight,
      scroll.scrollTop + (caret.top + caret.height / 2 - middle) / scale));
  }
  function create(options) {
    var source = options.source, host = options.instant;
    var sourceScroll = source.closest('.source-scroll'), instantScroll = host.closest('.instant-scroll');
    var frame, animation, dragging = false, lastEditor = null;
    function sourceHeight() {
      var style = getComputedStyle(source);
      return source.value.split('\n').length * parseFloat(style.lineHeight)
        + parseFloat(style.paddingTop) + parseFloat(style.paddingBottom)
        + parseFloat(style.borderTopWidth) + parseFloat(style.borderBottomWidth);
    }
    function layout() {
      [sourceScroll, instantScroll].forEach(function (scroll) {
        scroll.classList.toggle('typewriter-on', options.enabled());
        var zoom = scroll === instantScroll ? parseFloat(getComputedStyle(host).zoom) || 1 : 1;
        scroll.style.setProperty('--typewriter-space', scroll.clientHeight / (2 * zoom) + 'px');
      });
      source.style.height = sourceHeight() + 'px';
    }
    function stop() { cancelAnimationFrame(animation); }
    function editableCaret() {
      var selection = getSelection();
      if (!selection.rangeCount || !selection.isCollapsed || !host.contains(selection.focusNode)) return;
      var caret = selection.getRangeAt(0).cloneRange(); caret.collapse(false);
      var rect = caret.getBoundingClientRect();
      if (rect.height) return rect;
      // Empty lines have no range box; use their own line, never the whole paragraph.
      var node = selection.focusNode, offset = selection.focusOffset;
      if (node.nodeType === Node.TEXT_NODE && node.length) {
        caret.setStart(node, offset ? offset - 1 : 0);
        caret.setEnd(node, offset || 1);
        return caret.getBoundingClientRect();
      }
      var element = node.nodeType === Node.ELEMENT_NODE ? node : node.parentElement;
      var line = element.childNodes[offset];
      if (line?.nodeType === Node.ELEMENT_NODE) element = line;
      rect = element.getBoundingClientRect();
      var height = parseFloat(getComputedStyle(element).lineHeight) || rect.height;
      return { top: rect.top, height: Math.min(rect.height, height) };
    }
    function caretTarget(force) {
      var focused = document.activeElement;
      if (focused === source || (force && lastEditor === source)) {
        if (source.selectionStart !== source.selectionEnd || !sourceScroll.clientHeight) return;
        var style = getComputedStyle(source), rect = source.getBoundingClientRect();
        var lineHeight = parseFloat(style.lineHeight), scale = rect.height / source.offsetHeight || 1;
        var line = source.value.slice(0, source.selectionStart).split('\n').length - 1;
        return { scroll: sourceScroll, rect: { top: rect.top + (source.clientTop + parseFloat(style.paddingTop) + line * lineHeight - source.scrollTop) * scale, height: lineHeight * scale } };
      }
      if ((!host.contains(focused) && !(force && lastEditor === host)) || !instantScroll.clientHeight) return;
      if (focused.closest('input,button,select') || source.readOnly) return;
      var caret = editableCaret();
      if (caret) return { scroll: instantScroll, rect: caret };
    }
    function center(force) {
      if (!options.enabled() || dragging) return;
      var target = caretTarget(force); if (!target) return;
      var scroll = target.scroll, top = scrollTarget(scroll, target.rect), start = scroll.scrollTop;
      stop();
      if (Math.abs(top - start) < .5) return;
      if (options.reducedMotion()) { scroll.scrollTop = top; return; }
      var began;
      function step(now) {
        if (!options.enabled() || dragging) return;
        if (began === undefined) began = now;
        var progress = Math.min(1, (now - began) / 140);
        scroll.scrollTop = start + (top - start) * (1 - Math.pow(1 - progress, 3));
        if (progress < 1) animation = requestAnimationFrame(step);
      }
      animation = requestAnimationFrame(step);
    }
    function request(force) {
      cancelAnimationFrame(frame);
      frame = requestAnimationFrame(function () { center(force === true); });
    }
    function refresh() {
      stop(); layout();
      if (options.enabled()) request(true);
    }
    function isEditor(target) { return target === source || host.contains(target); }
    document.addEventListener('focusin', function (event) {
      if (!isEditor(event.target)) return;
      lastEditor = event.target === source ? source : host; request();
    });
    document.addEventListener('selectionchange', function () { request(); });
    document.addEventListener('pointerdown', function (event) {
      if (isEditor(event.target)) { dragging = true; stop(); }
    });
    ['pointerup', 'pointercancel'].forEach(function (name) {
      document.addEventListener(name, function () { if (dragging) { dragging = false; request(); } });
    });
    [source, host].forEach(function (editor) {
      ['input', 'keyup', 'compositionend'].forEach(function (name) { editor.addEventListener(name, function () { request(); }); });
    });
    [sourceScroll, instantScroll].forEach(function (scroll) { scroll.addEventListener('wheel', stop, { passive: true }); });
    new ResizeObserver(refresh).observe(sourceScroll);
    new ResizeObserver(refresh).observe(instantScroll);
    window.addEventListener('vex-preference-changed', refresh);
    window.addEventListener('vex-view-changed', refresh);
    return { refresh: refresh, request: request, sourceHeight: sourceHeight };
  }
  window.VexTypewriter = { create: create, scrollTarget: scrollTarget };
})();
