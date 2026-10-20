/* Read-only Markdown presentation shared by desktop, tablet and phone prototypes. */
(function () {
  'use strict';
  function buildDeck(text, title) {
    var parsed = TexEditor.parse(text), pages = [], start = 0, heading = null;
    function append(end) {
      if (end <= start) return;
      pages.push({ title: heading?.title || title, level: heading?.level || 1,
        html: parsed.blocks.slice(start, end).map(function (b) { return b.html; }).join('') });
    }
    parsed.blocks.forEach(function (block, i) {
      if (block.type === 'heading') { append(i); start = i; heading = block; }
      else if (block.type === 'hr') { append(i); start = i + 1; }
    });
    append(parsed.blocks.length);
    if (!pages.length) pages.push({ title: title, level: 1, html: '' });
    return pages;
  }

  var active = null;
  function open(options) {
    if (active) return;
    options.finish?.();
    var pages = buildDeck(options.text(), options.title()), page = 0, scale = VexOverlay.width() <= 699 ? 1.1 : 1.5, mapMode = !!options.mapMode?.();
    var mapX = 0, mapY = 0, mapScale = 1, pan = null;
    var mapNodes = pages.map(function (slide, index) { return { index: index, title: slide.title, level: slide.level, children: [] }; });
    var parents = [mapNodes[0]];
    for (var n = 1; n < mapNodes.length; n++) {
      while (parents.length > 1 && parents[parents.length - 1].level >= mapNodes[n].level) parents.pop();
      mapNodes[n].parent = parents[parents.length - 1]; mapNodes[n].parent.children.push(mapNodes[n]); parents.push(mapNodes[n]);
    }
    var measure = document.createElement('canvas').getContext('2d');
    measure.font = '16px ' + getComputedStyle(document.body).fontFamily;
    var geometry = VexMapLayout.layout(mapNodes[0], { mode: options.mapLayout?.(), measure: function (node) {
      node.width = 176; var rows = 1, used = 0;
      Array.from(node.title).forEach(function (char) { var width = measure.measureText(char).width; if (used + width > 146) { rows++; used = 0; } used += width; });
      node.height = Math.max(66,Math.ceil(rows * 21.6 + 18));
    }});
    var narrow = VexOverlay.width() <= 699, outline = !narrow;
    var root = document.createElement('section'), releaseOverlay;
    var priorFullscreen = document.fullscreenElement, requestedFullscreen = false;
    root.className = 'vex-deck'; root.tabIndex = -1; root.setAttribute('role', 'dialog');
    root.setAttribute('aria-modal', 'true');
    root.innerHTML = '<header><button type="button" data-deck="outline">☷</button><strong></strong><button type="button" data-deck="markdown"></button><button type="button" data-deck="map"></button><button type="button" data-deck="exit">×</button></header>' +
      '<div class="deck-stage"><nav></nav><div class="deck-scroll"><article class="md-body markdown-body"></article></div><div class="deck-map"><div class="deck-map-scene"></div></div></div>' +
      '<footer><small></small><div><span class="deck-tools"><button type="button" data-deck="smaller">A−</button><span class="deck-zoom"></span><button type="button" data-deck="larger">A+</button><button type="button" data-deck="fit">⊡</button></span><span class="deck-spacer"></span>' +
      '<span class="deck-pages"><button type="button" data-deck="previous">←</button><output aria-live="polite"></output><button type="button" data-deck="next">→</button></span></div></footer><progress max="1"></progress>';
    var nav = root.querySelector('nav'), body = root.querySelector('article'), scroll = root.querySelector('.deck-scroll');
    var map = root.querySelector('.deck-map'), scene = root.querySelector('.deck-map-scene');
    var mapWidth = geometry.width, mapHeight = geometry.height;
    scene.dataset.layout = VexMapLayout.valid(options.mapLayout?.()) ? options.mapLayout() : 'right';
    scene.innerHTML = '<svg width="' + mapWidth + '" height="' + mapHeight + '" aria-hidden="true">' + geometry.links.map(function (link) {
      return '<path d="' + link.path + '"/>';
    }).join('') + '</svg>' + mapNodes.map(function (node) {
      return '<button type="button" class="deck-map-node" data-map-page="' + node.index + '" style="left:' + node.x + 'px;top:' + node.y + 'px;height:' + node.height + 'px">' + TexEditor.esc(node.title) + '</button>';
    }).join('');
    function transformMap() { scene.style.transform = 'translate(' + mapX + 'px,' + mapY + 'px) scale(' + mapScale + ')'; root.classList.toggle('deck-map-overview', mapNodes.length > 300 && mapScale < .2); root.querySelector('.deck-zoom').textContent = Math.round((mapMode ? mapScale : scale) * 100) + '%'; }
    function focusMap() { var node = mapNodes[page]; mapScale = 1; mapX = map.clientWidth / 2 - node.cx; mapY = map.clientHeight / 2 - node.cy; transformMap(); }
    function fitScale() { return Math.max(.000001, Math.min(1, (map.clientWidth - 48) / mapWidth, (map.clientHeight - 48) / mapHeight)); }
    function zoomMap(factor) { var next = Math.max(Math.min(.15, fitScale()), Math.min(3, mapScale * factor)), ratio = next / mapScale; mapX = map.clientWidth / 2 + (mapX - map.clientWidth / 2) * ratio; mapY = map.clientHeight / 2 + (mapY - map.clientHeight / 2) * ratio; mapScale = next; transformMap(); }
    function labels() {
      var language = options.language?.() || 'zh-CN';
      return VexPresentationLabels[language] || VexPresentationLabels['en-US'];
    }
    function render(reset) {
      var l = labels();
      root.setAttribute('aria-label', l.presentationOutline);
      root.querySelector('header strong').textContent = options.title();
      root.querySelector('footer small').textContent = l.presentationKeys;
      root.querySelector('output').textContent = (page + 1) + ' / ' + pages.length;
      root.querySelector('.deck-zoom').textContent = Math.round((mapMode ? mapScale : scale) * 100) + '%';
      root.querySelector('[data-deck="markdown"]').textContent = l.presentationMarkdown;
      root.querySelector('[data-deck="map"]').textContent = l.mindMap;
      root.querySelector('[data-deck="markdown"]').setAttribute('aria-pressed', String(!mapMode));
      root.querySelector('[data-deck="map"]').setAttribute('aria-pressed', String(mapMode));
      root.querySelector('[data-deck="fit"]').hidden = !mapMode;
      root.querySelector('[data-deck="fit"]').title = l.mindMap + ' · ⊡';
      root.querySelector('[data-deck="fit"]').setAttribute('aria-label', l.mindMap + ' · ⊡');
      root.querySelector('[data-deck="fit"]').innerHTML = narrow ? controlIcon('fit') : '⊡';
      root.querySelector('progress').value = (page + 1) / pages.length;
      root.classList.toggle('deck-outline', outline);
      root.classList.toggle('deck-map-mode', mapMode);
      root.dataset.narrow = String(narrow);
      scroll.inert = narrow && outline;
      map.inert = narrow && outline;
      root.style.setProperty('--deck-scale', scale);
      ['outline', 'previous', 'next', 'smaller', 'larger', 'exit'].forEach(function (name) {
        var b = root.querySelector('[data-deck="' + name + '"]');
        var key = { outline: 'presentationOutline', previous: 'presentationPrevious', next: 'presentationNext',
          smaller: 'presentationSmaller', larger: 'presentationLarger', exit: 'exitPresentation' }[name];
        b.title = l[key]; b.setAttribute('aria-label', l[key]);
        b.disabled = name === 'previous' && page === 0 || name === 'next' && page === pages.length - 1 || !mapMode && (name === 'smaller' && scale <= 1 || name === 'larger' && scale >= 2.5);
      });
      root.querySelector('[data-deck="outline"]').setAttribute('aria-expanded', String(outline));
      nav.replaceChildren();
      pages.forEach(function (slide, index) {
        var button = document.createElement('button'); button.type = 'button';
        button.textContent = (index + 1) + '  ' + slide.title; button.dataset.page = index;
        button.style.paddingLeft = (12 + (slide.level - 1) * 10) + 'px';
        button.setAttribute('aria-current', index === page ? 'page' : 'false');
        button.onclick = function () { page = index; if (narrow) outline = false; render(true); root.focus(); };
        nav.appendChild(button);
      });
      nav.querySelector('[aria-current="page"]')?.scrollIntoView({ block: 'nearest' });
      if (reset) { body.innerHTML = pages[page].html; scroll.scrollTop = 0; VexRendering.diagrams(); }
      scene.querySelectorAll('[data-map-page]').forEach(function (node) { node.classList.toggle('selected', +node.dataset.mapPage === page); });
      root.querySelector('[data-deck="smaller"]').innerHTML = mapMode && narrow ? controlIcon('min') : mapMode ? '−' : 'A−';
      root.querySelector('[data-deck="larger"]').innerHTML = mapMode && narrow ? controlIcon('plus') : mapMode ? '+' : 'A+';
      root.querySelector('[data-deck="previous"]').innerHTML = narrow ? controlIcon('arrow') : '←';
      root.querySelector('[data-deck="next"]').innerHTML = narrow ? controlIcon('arrow') : '→';
      if (mapMode && reset) focusMap();
    }
    function controlIcon(name) { return '<svg class="deck-control-icon" aria-hidden="true"><use href="#i-' + name + '"/></svg>'; }
    function go(value) { var next = Math.max(0, Math.min(pages.length - 1, value)); if (next !== page) { page = next; render(true); } }
    function close() {
      if (!active) return;
      active = null;
      document.removeEventListener('keydown', keys, true);
      document.removeEventListener('fullscreenchange', fullscreenChanged);
      window.removeEventListener('resize', resized);
      var shouldExitFullscreen = requestedFullscreen && document.fullscreenElement === root && !priorFullscreen;
      if (shouldExitFullscreen) document.exitFullscreen().catch(function () {});
      root.remove();
      releaseOverlay();
    }
    function fullscreenChanged() { if (requestedFullscreen && !document.fullscreenElement) close(); }
    function resized() { var next = VexOverlay.width() <= 699; if (next !== narrow) outline = !next; narrow = next; render(false); }
    function keys(event) {
      if (event.isComposing) return;
      var handled = true;
      switch (event.key) {
        case 'Escape': case 'F5': close(); break;
        case 'ArrowRight': case 'PageDown': go(page + 1); break;
        case 'ArrowLeft': case 'PageUp': go(page - 1); break;
        case ' ': go(page + (event.shiftKey ? -1 : 1)); break;
        case 'Home': go(0); break;
        case 'End': go(pages.length - 1); break;
        case 'Tab': {
          var buttons = Array.from(root.querySelectorAll('button:not(:disabled)')).filter(function (b) { return !b.closest('[inert]') && b.getClientRects().length; });
          var index = buttons.indexOf(document.activeElement);
          buttons[(index + (event.shiftKey ? buttons.length - 1 : 1)) % buttons.length]?.focus(); break;
        }
        default: handled = false;
      }
      if (handled) event.preventDefault();
      // Keep writing shortcuts out of the hidden editor; preserve copy and scrolling.
      event.stopImmediatePropagation();
    }
    root.addEventListener('click', function (event) {
      var mapPage = event.target.closest('[data-map-page]')?.dataset.mapPage;
      if (mapPage != null) { go(+mapPage); return; }
      var action = event.target.closest('[data-deck]')?.dataset.deck;
      if (action === 'exit') close();
      if (action === 'next') go(page + 1);
      if (action === 'previous') go(page - 1);
      if (action === 'outline') { outline = !outline; render(false); }
      if (action === 'map' || action === 'markdown') { mapMode = action === 'map'; render(true); }
      if (action === 'fit') { mapScale = fitScale(); mapX = (map.clientWidth - mapWidth * mapScale) / 2; mapY = (map.clientHeight - mapHeight * mapScale) / 2; transformMap(); }
      if (action === 'larger' || action === 'smaller') {
        if (mapMode) zoomMap(action === 'larger' ? 1.15 : 1 / 1.15);
        else { scale = Math.round(Math.max(1, Math.min(2.5, scale + (action === 'larger' ? .1 : -.1))) * 10) / 10; render(false); }
      }
    });
    map.addEventListener('dblclick', function (event) { var index = event.target.closest('[data-map-page]')?.dataset.mapPage; if (index != null) { page = +index; mapMode = false; render(true); } });
    map.addEventListener('wheel', function (event) { event.preventDefault(); zoomMap(event.deltaY > 0 ? 1 / 1.15 : 1.15); }, { passive: false });
    map.addEventListener('pointerdown', function (event) { if (event.target.closest('button')) return; pan = { x: event.clientX, y: event.clientY, mapX: mapX, mapY: mapY }; map.setPointerCapture(event.pointerId); });
    map.addEventListener('pointermove', function (event) { if (pan) { mapX = pan.mapX + event.clientX - pan.x; mapY = pan.mapY + event.clientY - pan.y; transformMap(); } });
    ['pointerup','pointercancel'].forEach(function (name) { map.addEventListener(name, function () { pan = null; }); });
    var scope = VexOverlay.mount(root);
    releaseOverlay = VexOverlay.lock(root);
    active = { close: close }; render(true); root.focus();
    document.addEventListener('keydown', keys, true);
    document.addEventListener('fullscreenchange', fullscreenChanged);
    window.addEventListener('resize', resized);
    if (scope === document.body && !priorFullscreen && root.requestFullscreen && !narrow) {
      root.requestFullscreen().then(function () {
        requestedFullscreen = true;
        if (!active && document.fullscreenElement === root) document.exitFullscreen().catch(function () {});
      }).catch(function () {});
    }
  }
  window.VexPresentation = { open: open, buildDeck: buildDeck, get active() { return !!active; } };
})();
