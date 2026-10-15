/* 单块编辑原型：正在输入的块保留 Markdown，其他块即时显示排版。 */
(function () {
  'use strict';
  window.createVexInstantEditor = function (options) {
    var host = document.getElementById('instant-content'), active = null;
    function range(block) {
      var lines = options.input.value.split('\n');
      var start = lines.slice(0, block.line - 1).join('\n').length + (block.line > 1 ? 1 : 0);
      return { start: start, end: start + lines.slice(block.line - 1, block.endLine).join('\n').length };
    }
    function render() {
      if (active) {
        active.preview.innerHTML = options.blocks().filter(function (block) {
          var span = range(block);
          return span.start >= active.start && span.start < active.start + active.editor.value.length;
        }).map(options.renderBlock).join('') || '<p class="empty-state">输入内容，预览会同步更新。</p>';
        return;
      }
      host.innerHTML = '';
      var blocks = options.blocks();
      blocks.forEach(function (block, index) {
        var element = document.createElement('div'), span = range(block);
        element.className = 'instant-block'; element.dataset.blockIndex = index; element.dataset.start = span.start;
        element.tabIndex = 0; element.setAttribute('role', 'button');
        element.setAttribute('aria-label', '编辑第 ' + block.line + ' 行：' + block.text.slice(0, 20));
        element.innerHTML = options.renderBlock(block); host.appendChild(element);
      });
      if (!blocks.length) {
        var empty = document.createElement('button'); empty.className = 'instant-empty'; empty.textContent = '点击这里，开始写作…';
        empty.onclick = function () { beginEmpty(); }; host.appendChild(empty);
      }
    }
    function grow() {
      if (active) {
        active.editor.style.height = 'auto'; active.editor.style.height = Math.max(48, active.editor.scrollHeight) + 'px';
        if (options.typewriter()) active.element.scrollIntoView({ block: 'center' });
      }
    }
    function sync() {
      if (!active) return;
      options.input.value = active.before + active.editor.value + active.after;
      options.input.setSelectionRange(active.start + active.editor.selectionStart, active.start + active.editor.selectionEnd);
      options.input.dispatchEvent(new Event('input')); grow(); options.cursor();
    }
    function finish(cancel) {
      if (!active) return;
      if (!cancel && active.original !== active.editor.value) options.remember(active.before + active.original + active.after);
      if (cancel) { active.editor.value = active.original; sync(); }
      else sync();
      var start = active.start;
      active = null; options.refresh(); render();
      document.getElementById('instant-hint').textContent = '点击内容编辑，输入时同步预览，离开后恢复排版';
      return start;
    }
    function attach(element, span, original, index) {
      var editor = document.createElement('textarea'); editor.className = 'instant-input'; editor.spellcheck = false;
      editor.setAttribute('aria-label', '编辑当前 Markdown 块'); editor.value = original;
      var preview = document.createElement('div'); preview.className = 'instant-live-preview'; preview.setAttribute('aria-label', '当前块实时预览');
      active = { element: element, editor: editor, preview: preview, start: span.start, end: span.end, index: index, original: original, before: options.input.value.slice(0, span.start), after: options.input.value.slice(span.end) };
      element.removeAttribute('role'); element.removeAttribute('tabindex'); element.classList.add('is-editing'); element.innerHTML = ''; element.appendChild(editor);
      element.appendChild(preview); render();
      editor.oninput = sync;
      editor.onblur = function () { finish(); };
      editor.onkeyup = function () { if (active) { options.input.setSelectionRange(active.start + editor.selectionStart, active.start + editor.selectionEnd); options.cursor(); } };
      editor.onkeydown = function (event) {
        if (event.isComposing) return;
        if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); finish(true); }
        else if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') { event.preventDefault(); finish(); }
        else {
          var pairs = { '(': ')', '[': ']', '{': '}', '"': '"', "'": "'", '`': '`' };
          if (options.autoPair() && !event.ctrlKey && !event.metaKey && pairs[event.key]) {
            event.preventDefault(); var start = editor.selectionStart, selected = editor.value.slice(start, editor.selectionEnd);
            editor.setRangeText(event.key + selected + pairs[event.key], start, editor.selectionEnd, 'end');
            editor.setSelectionRange(start + 1, start + 1 + selected.length); sync();
          }
        }
      };
      document.getElementById('instant-hint').textContent = '正在编辑当前块 · Ctrl+Enter 完成 · Esc 取消';
      editor.focus({ preventScroll: true }); editor.setSelectionRange(editor.value.length, editor.value.length); grow();
    }
    function begin(index) {
      if (active) finish();
      var block = options.blocks()[index], element = host.querySelector('[data-block-index="' + index + '"]');
      if (block && element) attach(element, range(block), block.raw, index);
    }
    function beginEmpty() {
      host.innerHTML = '<div class="instant-block is-editing"></div>';
      attach(host.firstChild, { start: 0, end: options.input.value.length }, options.input.value, 0);
    }
    host.addEventListener('pointerdown', function (event) {
      if (event.target.closest('textarea, .instant-empty') || event.button !== 0) return;
      var block = event.target.closest('.instant-block'); if (!block) return;
      event.preventDefault();
      var index = Number(block.dataset.blockIndex), offset = Number(block.dataset.start);
      if (active) {
        if (active.element === block) return;
        if (offset >= active.end) offset += active.editor.value.length - active.original.length;
        finish();
        index = options.blocks().findIndex(function (candidate) { var span = range(candidate); return offset >= span.start && offset <= span.end; });
      }
      begin(index);
    });
    host.addEventListener('keydown', function (event) {
      var block = event.target.closest('.instant-block');
      if (block && event.target === block && (event.key === 'Enter' || event.key === ' ')) { event.preventDefault(); begin(Number(block.dataset.blockIndex)); }
    });
    return {
      render: render, finish: finish,
      editing: function () { return !!active; },
      focusAt: function (line) { finish(); var index = options.blocks().findIndex(function (block) { return line >= block.line && line <= block.endLine; }); if (index < 0) index = Math.max(0, options.blocks().length - 1); if (options.blocks().length) { begin(index); active.element.scrollIntoView({ block: 'nearest' }); } else beginEmpty(); },
      insert: function (text) { if (!active) return false; active.editor.focus(); if (!document.execCommand('insertText', false, text)) active.editor.setRangeText(text, active.editor.selectionStart, active.editor.selectionEnd, 'end'); sync(); return true; }
    };
  };
})();
