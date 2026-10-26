/* 在原有排版 DOM 中编辑：聚焦只出现光标，不替换输入框、不改变块尺寸。 */
(function () {
  'use strict';
  window.createVexInstantEditor = function (options) {
    var host = document.getElementById('instant-content'), active = null, composing = false;
    function range(block) {
      var lines = options.input.value.split('\n');
      var start = lines.slice(0, block.line - 1).join('\n').length + (block.line > 1 ? 1 : 0);
      return { start: start, end: start + lines.slice(block.line - 1, block.endLine).join('\n').length };
    }
    function text(node) { return (node.textContent || '').replace(/\u00a0/g, ' '); }
    function inline(node) {
      if (node.nodeType === Node.TEXT_NODE) return node.nodeValue.replace(/\u00a0/g, ' ');
      if (node.nodeType !== Node.ELEMENT_NODE) return '';
      if (node.dataset.markdown) return node.dataset.markdown;
      if (node.contentEditable === 'false') return '';
      var value = Array.from(node.childNodes).map(inline).join(''), tag = node.tagName;
      if (tag === 'BR') return '\n';
      if (tag === 'STRONG' || tag === 'B') return '**' + value + '**';
      if (tag === 'EM' || tag === 'I') return '*' + value + '*';
      if (tag === 'DEL' || tag === 'S') return '~~' + value + '~~';
      if (tag === 'CODE') return '`' + text(node) + '`';
      if (tag === 'A') return '[' + value + '](' + node.getAttribute('href') + ')';
      if (tag === 'IMG') return '![' + (node.getAttribute('alt') || '') + '](' + node.getAttribute('src') + ')';
      return value;
    }
    function serializeNode(node) {
      if (node.nodeType !== Node.ELEMENT_NODE) return inline(node);
      var tag = node.tagName, value = inline(node);
      if (/^H[1-6]$/.test(tag)) return '#'.repeat(Number(tag[1])) + ' ' + value + '\n\n';
      if (tag === 'P' || tag === 'DIV') return Array.from(node.childNodes).map(serializeNode).join('') + '\n\n';
      if (tag === 'BLOCKQUOTE') return value.split('\n').map(function (line) { return '> ' + line; }).join('\n') + '\n\n';
      if (tag === 'UL' || tag === 'OL') return Array.from(node.children).map(function (item, index) { return (tag === 'OL' ? (index + 1) + '. ' : '- ') + inline(item).replace(/^[☑☐] /, ''); }).join('\n') + '\n\n';
      if (tag === 'TABLE') {
        return VexDocumentOps.fromDOM(node,inline) + '\n\n';
      }
      if (node.classList.contains('md-code-block')) return '```' + (active.block.language || '') + '\n' + text(node.querySelector('code')).replace(/\n$/, '') + '\n```\n\n';
      return value;
    }
    function markdown() {
      if (active.fence) {
        var code = active.element.querySelector('pre');
        return active.fence.write(code.innerText, active.element.querySelector('.md-code-language').value);
      }
      var element = active.element, hasStructure = element.querySelector('h1,h2,h3,h4,h5,h6,p,ul,ol,blockquote,table,.md-code-block,div');
      var value = Array.from(element.childNodes).map(serializeNode).join('').replace(/\n+$/, '');
      if (active.block.kind !== 'code') value = value.replace(/\n{3,}/g, '\n\n');
      if (!hasStructure && active.block.kind === 'heading') value = '#'.repeat(active.block.level) + ' ' + value;
      if (active.block.kind === 'list') {
        var prefix = active.original.match(/^\s*(?:[-+*]|\d+\.)\s+(?:\[[ xX]\]\s*)?/);
        value = (prefix ? prefix[0] : '- ') + value.replace(/^\s*(?:[-+*]|\d+\.)\s+/, '').replace(/^[☑☐] /, '');
      }
      if (!hasStructure && active.block.kind === 'quote') value = value.split('\n').map(function (line) { return '> ' + line; }).join('\n');
      return value;
    }
    function caretOffset() {
      var selection = getSelection(); if (!active || !selection.rangeCount || !active.element.contains(selection.anchorNode)) return 0;
      var before = selection.getRangeAt(0).cloneRange(); before.selectNodeContents(active.element); before.setEnd(selection.anchorNode, selection.anchorOffset);
      return before.toString().length;
    }
    function setCaret(offset) {
      if (!active) return;
      var walker = document.createTreeWalker(active.element, NodeFilter.SHOW_TEXT), node;
      while ((node = walker.nextNode())) {
        if (node.parentElement.closest('[contenteditable="false"]')) continue;
        if (offset <= node.length) { var selection = getSelection(), caret = document.createRange(); caret.setStart(node, offset); caret.collapse(true); selection.removeAllRanges(); selection.addRange(caret); return; }
        offset -= node.length;
      }
      var end = document.createRange(); end.selectNodeContents(active.element); end.collapse(false); getSelection().removeAllRanges(); getSelection().addRange(end);
    }
    function normalizeInline() {
      if (!active || composing || active.block.kind === 'code' || active.block.kind === 'table') return;
      var offset = caretOffset(), delta = 0, walker = document.createTreeWalker(active.element, NodeFilter.SHOW_TEXT), nodes = [], node;
      while ((node = walker.nextNode())) nodes.push(node);
      var traversed = 0;
      nodes.forEach(function (item) {
        var value = item.nodeValue, start = traversed; traversed += value.length;
        if (item.parentElement.closest('code,a,[contenteditable="false"]')) return;
        var pattern = /\*\*([^*\n]+)\*\*|(?<!\*)\*([^*\n]+)\*(?!\*)|`([^`\n]+)`/g, match, at = 0, fragment = document.createDocumentFragment(), changed = false;
        while ((match = pattern.exec(value))) {
          changed = true; fragment.append(document.createTextNode(value.slice(at, match.index)));
          var formatted = document.createElement(match[1] !== undefined ? 'strong' : match[2] !== undefined ? 'em' : 'code'); formatted.textContent = match[1] ?? match[2] ?? match[3]; fragment.append(formatted);
          if (start + match.index + match[0].length <= offset) delta += match[0].length - formatted.textContent.length;
          at = match.index + match[0].length;
        }
        if (changed) { fragment.append(document.createTextNode(value.slice(at))); item.replaceWith(fragment); }
      });
      if (delta) setCaret(offset - delta);
    }
    function sync() {
      if (!active) return;
      active.changed = true; active.value = markdown();
      options.input.value = active.before + active.value + active.after;
      var offset = Math.min(active.value.length, caretOffset());
      options.input.setSelectionRange(active.start + offset, active.start + offset);
      options.input.dispatchEvent(new Event('input')); options.cursor();
    }
    function render() {
      if (active || window.VexDiagramEditing?.busy(host)) return;
      host.replaceChildren();
      options.blocks().forEach(function (block, index) {
        var element = document.createElement('div'), span = range(block);
        element.className = 'instant-block'; element.dataset.blockIndex = index; element.dataset.start = span.start;
        element.contentEditable = 'true'; element.spellcheck = false; element.setAttribute('role', 'textbox'); element.setAttribute('aria-multiline', 'true');
        element.setAttribute('aria-label', '编辑第 ' + block.line + ' 行：' + block.text.slice(0, 20));
        element.innerHTML = options.renderBlock(block);
        if (VexRendering.special(block) || block.kind === 'rule') {
          element.contentEditable = 'false';
          if (block.kind === 'code' && block.language.toLowerCase() === 'mermaid' && window.VexPreferences?.values.mermaid !== false && !options.input.readOnly) {
            var diagramBefore = options.input.value.slice(0, span.start), diagramAfter = options.input.value.slice(span.end);
            VexDiagramEditing.mount(element, block, {
              begin: function () { if (active) finish(); options.remember(options.input.value); },
              change: function (raw) { options.input.value = diagramBefore + raw + diagramAfter; options.input.dispatchEvent(new Event('input')); },
              finish: function () { options.refresh(); render(); }
            });
          } else if (block.kind !== 'rule') {
            element.tabIndex = 0; element.setAttribute('role', 'figure'); element.setAttribute('aria-label', block.kind === 'math' ? '数学或化学公式' : 'Markdown 图表');
            element.ondblclick = function () { VexWorkspace.locateEditor(block.line); };
            element.onkeydown = function (event) { if (event.key === 'Enter') { event.preventDefault(); VexWorkspace.locateEditor(block.line); } };
          } else element.removeAttribute('role');
        } else if (block.kind === 'code') {
          element.contentEditable = 'false';
          var pre = element.querySelector('pre'); pre.contentEditable = 'plaintext-only'; pre.tabIndex = 0;
          pre.setAttribute('role', 'textbox'); pre.setAttribute('aria-label', '代码正文'); pre.setAttribute('aria-multiline', 'true');
          element.append(VexCodeFence.footer(block.language || ''));
        }
        if (options.input.readOnly) { element.contentEditable = 'false'; element.querySelectorAll('[contenteditable]').forEach(function (node) { node.contentEditable = 'false'; }); }
        host.appendChild(element);
      });
      if (!options.blocks().length) {
        var empty = document.createElement('div'); empty.className = 'instant-block instant-empty'; empty.contentEditable = 'true'; empty.dataset.blockIndex = '0'; empty.dataset.start = '0';
        empty.setAttribute('role', 'textbox'); empty.setAttribute('aria-label', '开始写作'); empty.dataset.placeholder = '开始写作…'; host.appendChild(empty);
      }
    }
    function begin(element) {
      if (options.input.readOnly || element.querySelector('.diagram-editor')) return;
      if (active?.element === element) return;
      if (active) finish();
      var block = options.blocks()[Number(element.dataset.blockIndex)] || { kind: 'paragraph', raw: '', line: 1, endLine: 1 }, span = range(block);
      active = { element: element, block: block, start: span.start, end: span.end, original: block.raw, value: block.raw, before: options.input.value.slice(0, span.start), after: options.input.value.slice(span.end), changed: false };
      if (block.kind === 'code') {
        active.fence = VexCodeFence.parse(block.raw);
        element.querySelector('.md-code-language').value = active.fence?.language || block.language || '';
      }
      element.classList.add('instant-input', 'is-editing');
    }
    function finish(cancel) {
      if (!active) return;
      var previous = active, start = previous.start;
      if (previous.changed) {
        if (cancel) { options.input.value = previous.before + previous.original + previous.after; options.input.dispatchEvent(new Event('input')); }
        else if (previous.original !== previous.value) options.remember(previous.before + previous.original + previous.after);
      }
      active = null; previous.element.classList.remove('instant-input', 'is-editing');
      if (previous.element.contains(document.activeElement)) document.activeElement.blur();
      if (previous.changed) { options.refresh(); render(); }
      return start;
    }
    host.addEventListener('pointerdown', function (event) {
      if (event.button !== 0 || event.target.closest('button,a,.md-code-footer')) return;
      var element = event.target.closest('.instant-block'); if (!element || !active || element === active.element) return;
      if (!active.changed) return;
      var offset = Number(element.dataset.start); if (offset >= active.end) offset += active.value.length - active.original.length;
      event.preventDefault(); finish();
      var index = options.blocks().findIndex(function (block) { var span = range(block); return offset >= span.start && offset <= span.end; });
      var next = host.querySelector('[data-block-index="' + index + '"]');
      if (next) { (next.querySelector('pre[contenteditable]') || next).focus({ preventScroll: true }); var caret = document.caretRangeFromPoint(event.clientX, event.clientY); if (caret && next.contains(caret.startContainer)) { getSelection().removeAllRanges(); getSelection().addRange(caret); } }
    });
    host.addEventListener('focusin', function (event) { var element = event.target.closest('.instant-block'); if (element && !VexRendering.special(options.blocks()[Number(element.dataset.blockIndex)] || {}) && !event.target.closest('.md-code-footer')) begin(element); });
    host.addEventListener('focusout', function (event) { if (active && !active.element.contains(event.relatedTarget)) finish(); });
    host.addEventListener('input', function (event) { if (active) { if (!event.isComposing) normalizeInline(); sync(); } });
    host.addEventListener('compositionstart', function () { composing = true; });
    host.addEventListener('compositionend', function () { composing = false; normalizeInline(); sync(); });
    host.addEventListener('paste', function (event) { if (!active || event.target.matches('input')) return; event.preventDefault(); document.execCommand('insertText', false, event.clipboardData.getData('text/plain')); normalizeInline(); sync(); });
    host.addEventListener('keydown', function (event) {
      if (!active || event.isComposing || composing) return;
      if (event.target.matches('.md-code-language') && event.key === 'Enter') { event.preventDefault(); active.element.querySelector('pre').focus(); return; }
      var selection = getSelection(), anchor = selection.anchorNode?.parentElement, heading = anchor?.closest('h1,h2,h3,h4,h5,h6');
      if (event.key === 'Enter' && !event.shiftKey && !event.ctrlKey && !event.metaKey && heading && active.element.contains(heading)) {
        event.preventDefault();
        var tail = selection.getRangeAt(0).cloneRange(); tail.deleteContents(); tail.setEnd(heading, heading.childNodes.length);
        var paragraph = document.createElement('p'); paragraph.append(tail.extractContents()); if (!paragraph.textContent) paragraph.append(document.createElement('br'));
        heading.after(paragraph); var caret = document.createRange(); caret.selectNodeContents(paragraph); caret.collapse(true); selection.removeAllRanges(); selection.addRange(caret); sync();
      }
      else if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); finish(true); }
      else if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') { event.preventDefault(); finish(); }
      else if ((event.ctrlKey || event.metaKey) && ['b', 'i'].includes(event.key.toLowerCase())) { event.preventDefault(); event.stopPropagation(); document.execCommand(event.key.toLowerCase() === 'b' ? 'bold' : 'italic'); sync(); }
    });
    return {
      render: render, finish: finish, editing: function () { return !!active; },
      focusAt: function (line) {
        finish(); var index = options.blocks().findIndex(function (block) { return line >= block.line && line <= block.endLine; }); if (index < 0) index = Math.max(0, options.blocks().length - 1);
        var element = host.querySelector('[data-block-index="' + index + '"]'); if (element) { (element.querySelector('pre[contenteditable]') || element).focus({ preventScroll: true }); begin(element); setCaret(text(element).length); if (options.typewriter()) options.cursor(); else element.scrollIntoView({ block: 'nearest' }); }
      },
      insert: function (value) { if (!active) return false; (active.element.querySelector('pre[contenteditable]') || active.element).focus({ preventScroll: true }); document.execCommand('insertText', false, value); normalizeInline(); sync(); return true; },
      format: function (kind) {
        if (!active || !['bold', 'italic', 'heading1', 'heading2', 'heading3', 'heading4', 'heading5', 'heading6', 'quote', 'list', 'ordered'].includes(kind)) return false;
        active.element.focus({ preventScroll: true });
        if (kind === 'bold' || kind === 'italic') document.execCommand(kind);
        else if (kind.startsWith('heading')) document.execCommand('formatBlock', false, 'h' + kind.slice(-1));
        else if (kind === 'quote') document.execCommand('formatBlock', false, 'blockquote');
        else document.execCommand(kind === 'list' ? 'insertUnorderedList' : 'insertOrderedList');
        sync(); return true;
      }
    };
  };
})();
