/* 选区浮动条 + 行内样式面板：在即时编辑与源码选区上方弹出（参考 Notion 式 bubble toolbar），
   动作全部复用元素菜单的捕获/映射/回写机制（capture + handleAction），不另起文本管线。
   颜色面板取 Semi Design 色板（Semi.Avalonia ColorPicker 的 SemiColorLightPalette，MIT）：
   文字颜色用深色档（4-7），背景颜色用浅色档（0-3）。 */
(() => {
  'use strict';
  /* 9 个常用色相 × 10 档明暗，取自 Semi 色板（浅色模式）。 */
  const SEMI_HUES = [
    ['红', ['FEF2ED', 'FEDDD2', 'FDB7A5', 'FB9078', 'FA664C', 'F93920', 'D52515', 'B2140C', '8E0805', '6A0103']],
    ['橙', ['FFF8EA', 'FEEECC', 'FED998', 'FDC165', 'FDA633', 'FC8800', 'D26700', 'A84A00', '7E3100', '541D00']],
    ['琥珀', ['FEFBEB', 'FCF5CE', 'F9E89E', 'F6D86F', 'F3C641', 'F0B114', 'C88A0F', 'A0660A', '784606', '502B03']],
    ['黄', ['FFFDEA', 'FEFBCB', 'FDF398', 'FCE865', 'FBDA32', 'FAC800', 'D0AA00', 'A78B00', '7D6A00', '534800']],
    ['绿', ['ECF7EC', 'D0F0D1', 'A4E0A7', '7DD182', '5AC262', '3BB346', '30953B', '25772F', '1B5924', '113C18']],
    ['青', ['E5F7F8', 'C2EFF0', '8ADDE2', '58CBD3', '2CB8C5', '05A4B6', '038698', '016979', '004D5B', '00323D']],
    ['蓝', ['EAF5FF', 'CBE7FE', '98CDFD', '65B2FC', '3295FB', '0064FA', '0062D6', '004FB3', '003D8F', '002C6B']],
    ['紫', ['F3EDF9', 'E2D1F4', 'C4A7E9', 'A67FDD', '885BD2', '6A3AC7', '572FB3', '46259E', '361C8A', '281475']],
    ['灰', ['F9F9F9', 'E6E8EA', 'C6CACD', 'A7ABB0', '888D92', '6B7075', '555B61', '41464C', '2E3238', '1C1F23']],
  ];
  const FG_SHADES = [4, 5, 6, 7];   /* 中深→深：文字色保证纸面可读 */
  const BG_SHADES = [0, 1, 2, 3];   /* 极浅→浅：背景色垫底不抢字 */
  const SIZES = [12, 14, 16, 18, 22, 28];
  const BUTTONS = [
    { icon: 'bold', title: '加粗', action: 'wrap-bold' },
    { icon: 'italic', title: '斜体', action: 'wrap-italic' },
    { label: 'S', strike: true, title: '删除线', action: 'wrap-strikethrough' },
    { icon: 'inline-code', title: '行内代码', action: 'wrap-code' },
    { icon: 'link', title: '链接', action: 'insert-link' },
    { label: 'H1', title: '一级标题', action: 'convert-heading1' },
    { label: 'H2', title: '二级标题', action: 'convert-heading2' },
    { icon: 'list', title: '列表', action: 'convert-list' },
  ];
  const svg = name => `<svg class="vex-selection-icon" aria-hidden="true"><use href="#i-${name}"/></svg>`;
  const symbol = item => item.icon ? svg(item.icon) : `<span class="vex-selection-glyph${item.strike ? ' strike' : ''}">${item.label}</span>`;

  function mount(options) {
    const source = options.source, menu = options.menu;
    let layer = null, bar = null, panel = null, panelOwner = null, panelLayer = null, lastPoint = null, raf = 0;

    const app = () => document.querySelector('.app-window');
    /* 可用选区：即时编辑块内的 DOM 选区，或源码输入框的非空选区。 */
    function context() {
      const selection = getSelection();
      if (selection.rangeCount && !selection.isCollapsed) {
        const start = selection.getRangeAt(0).startContainer;
        const node = start.nodeType === Node.ELEMENT_NODE ? start : start.parentElement;
        const block = node?.closest?.('.instant-block');
        if (block?.getClientRects().length) return { target: block, dom: true };
      }
      if (document.activeElement === source && source.getClientRects().length && source.selectionStart !== source.selectionEnd)
        return { target: source, dom: false };
      return null;
    }
    function closePanel() { panel?.remove(); panel = null; panelOwner = null; if (panelLayer && panelLayer !== layer) { panelLayer.remove(); panelLayer = null; } }
    function hide() { closePanel(); if (layer) layer.hidden = true; }

    function run(action) {
      const ok = apply(action);
      closePanel();
      if (ok) hide();
    }
    /* 供浮动条与快捷工具条共用：对当前选区执行一个元素菜单动作。 */
    function apply(action) {
      const current = context();
      if (!current) { options.toast?.('先选中要调整的文字'); return false; }
      menu.capture(current.target);
      return menu.handleAction('element-' + action, bar);
    }

    function placePanel(anchor) {
      const host = panelLayer || layer, frame = host.getBoundingClientRect(), bounds = app()?.getBoundingClientRect() || frame;
      const rect = anchor.getBoundingClientRect();
      const minX = Math.max(6, bounds.left - frame.left), maxX = Math.min(frame.width - 6, bounds.right - frame.left);
      const minY = Math.max(6, bounds.top - frame.top), maxY = Math.min(frame.height - 6, bounds.bottom - frame.top);
      let left = rect.left - frame.left + rect.width / 2 - panel.offsetWidth / 2;
      left = Math.min(Math.max(left, minX), Math.max(minX, maxX - panel.offsetWidth));
      let top = rect.bottom - frame.top + 6;
      if (top + panel.offsetHeight > maxY) top = rect.top - frame.top - panel.offsetHeight - 6;
      top = Math.min(Math.max(top, minY), Math.max(minY, maxY - panel.offsetHeight));
      panel.style.left = Math.round(left) + 'px';
      panel.style.top = Math.round(top) + 'px';
    }
    function openPanel(kind, anchor) {
      if (panel && panelOwner === anchor) { closePanel(); return; }
      closePanel();
      panelOwner = anchor;
      panel = buildPanel(kind);
      let host;
      if (layer && !layer.hidden) host = layer;
      else {
        if (!panelLayer || panelLayer === layer) {
          panelLayer = document.createElement('div');
          panelLayer.className = 'vex-selection-layer';
          panelLayer.hidden = false;
          VexOverlay.mount(panelLayer);
        }
        host = panelLayer;
      }
      host.append(panel);
      placePanel(anchor);
    }
    function buildPanel(kind) {
      const wrap = document.createElement('div');
      wrap.className = 'vex-span-panel';
      const title = document.createElement('div');
      title.className = 'vex-span-title';
      title.textContent = kind === 'fg' ? '文字颜色' : kind === 'bg' ? '背景颜色' : '字号';
      wrap.append(title);
      if (kind === 'size') {
        const grid = document.createElement('div');
        grid.className = 'vex-span-grid sizes';
        SIZES.forEach(size => {
          const chip = document.createElement('button');
          chip.type = 'button'; chip.className = 'vex-span-size';
          chip.textContent = String(size); chip.title = size + ' px';
          chip.onclick = () => run('span-font-size:' + size + 'px');
          grid.append(chip);
        });
        wrap.append(grid);
      } else {
        const shades = kind === 'fg' ? FG_SHADES : BG_SHADES;
        const grid = document.createElement('div');
        grid.className = 'vex-span-grid';
        SEMI_HUES.forEach(([name, hexes]) => shades.forEach(shade => {
          const hex = '#' + hexes[shade];
          const swatch = document.createElement('button');
          swatch.type = 'button'; swatch.className = 'vex-span-swatch';
          swatch.style.background = hex;
          swatch.title = name + ' ' + hex; swatch.setAttribute('aria-label', name + ' ' + hex);
          swatch.onclick = () => run((kind === 'fg' ? 'span-color:' : 'span-background-color:') + hex);
          grid.append(swatch);
        }));
        wrap.append(grid);
      }
      const clear = document.createElement('button');
      clear.type = 'button'; clear.className = 'vex-span-clear';
      clear.textContent = '清除全部样式'; clear.title = '剥掉选区的行内样式标签';
      clear.onclick = () => run('span-clear');
      wrap.append(clear);
      /* 面板挂在层上（不在条内），必须自行拦掉 mousedown 默认行为，否则按下色块的瞬间浏览器清空选区。 */
      wrap.addEventListener('mousedown', event => event.preventDefault());
      return wrap;
    }

    function place(x, y) {
      const frame = layer.getBoundingClientRect(), bounds = app()?.getBoundingClientRect() || frame;
      const minX = Math.max(6, bounds.left - frame.left), maxX = Math.min(frame.width - 6, bounds.right - frame.left);
      const minY = Math.max(6, bounds.top - frame.top), maxY = Math.min(frame.height - 6, bounds.bottom - frame.top);
      const width = bar.offsetWidth || 200, height = bar.offsetHeight || 40;
      bar.style.left = Math.round(Math.min(Math.max(x - frame.left - width / 2, minX), Math.max(minX, maxX - width))) + 'px';
      bar.style.top = Math.round(Math.min(Math.max(y - frame.top - height - 10, minY), Math.max(minY, maxY - height))) + 'px';
    }
    function show(current) {
      if (!layer) { layer = document.createElement('div'); layer.className = 'vex-selection-layer'; layer.append(buildBar()); VexOverlay.mount(layer); }
      closePanel();
      layer.hidden = false;
      const selection = getSelection();
      let x = lastPoint?.x ?? (document.documentElement.clientWidth / 2), y = lastPoint?.y ?? 220;
      if (current.dom && selection.rangeCount) {
        const rect = selection.getRangeAt(0).getBoundingClientRect();
        if (rect.width || rect.height) { x = rect.left + rect.width / 2; y = rect.top; }
      }
      place(x, y);
    }
    function sync() {
      const current = context();
      if (!current) { hide(); return; }
      show(current);
    }
    function schedule() { if (raf) return; raf = requestAnimationFrame(() => { raf = 0; sync(); }); }

    function buildBar() {
      bar = document.createElement('div');
      bar.className = 'vex-selection-bar';
      bar.setAttribute('role', 'toolbar');
      bar.setAttribute('aria-label', '选区格式');
      BUTTONS.forEach(item => {
        const button = document.createElement('button');
        button.type = 'button'; button.title = item.title; button.setAttribute('aria-label', item.title);
        button.innerHTML = symbol(item);
        button.onclick = () => run(item.action);
        bar.append(button);
      });
      const divider = () => { const rule = document.createElement('i'); rule.className = 'vex-selection-rule'; return rule; };
      bar.append(divider());
      [['size', '字号'], ['fg', '文字颜色'], ['bg', '背景颜色']].forEach(([kind, label], index) => {
        const button = document.createElement('button');
        button.type = 'button'; button.title = label; button.setAttribute('aria-label', label);
        button.setAttribute('aria-haspopup', 'menu');
        button.innerHTML = `<span class="vex-selection-glyph${kind === 'fg' ? ' fg' : kind === 'bg' ? ' bg' : ''}">A</span>${svg('chevron-down')}`;
        button.onclick = () => openPanel(kind, button);
        bar.append(button);
        if (index === 0) bar.append(divider());
      });
      bar.append(divider());
      const clear = document.createElement('button');
      clear.type = 'button'; clear.title = '清除样式'; clear.setAttribute('aria-label', '清除样式');
      clear.innerHTML = svg('x');
      clear.onclick = () => run('span-clear');
      bar.append(clear);
      /* mousedown 不放走默认行为：点击按钮时保住文本选区，连续叠加样式不用重新选。 */
      bar.addEventListener('mousedown', event => event.preventDefault());
      return bar;
    }
    /* 快捷工具条入口：字号 / 文字颜色 / 背景颜色 三个带面板的按钮，动作与浮动条共用。 */
    function attachToolbar(toolbar) {
      [['span-size-button', 'size'], ['span-fg-button', 'fg'], ['span-bg-button', 'bg']].forEach(([id, kind]) => {
        const button = toolbar?.querySelector('#' + id);
        if (!button) return;
        button.addEventListener('mousedown', event => event.preventDefault());
        button.addEventListener('click', () => { panel && panelOwner === button ? closePanel() : openPanel(kind, button); });
      });
    }

    document.addEventListener('pointerup', event => { lastPoint = { x: event.clientX, y: event.clientY }; }, true);
    document.addEventListener('selectionchange', schedule);
    document.addEventListener('pointerdown', event => {
      const target = event.target;
      if (target.closest?.('.vex-selection-layer') || target.closest?.('.vex-span-panel')) return;
      if (panelOwner?.contains?.(target)) return; /* 面板锚点按钮的按下交给它的 click 做开关 */
      if (panel) closePanel();
      hide();
    }, true);
    document.addEventListener('keydown', event => { if (event.key === 'Escape') hide(); });
    document.addEventListener('scroll', () => hide(), true);
    window.addEventListener('resize', hide);
    ['vex-view-changed', 'vex-document-loaded', 'vex-source-toggled'].forEach(name => window.addEventListener(name, hide));
    return { hide: hide, sync: sync, apply: apply, attachToolbar: attachToolbar, closePanel: closePanel };
  }
  window.VexSelectionMenu = { mount: mount };
})();
