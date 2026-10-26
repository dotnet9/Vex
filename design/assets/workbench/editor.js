/* Prototype-only Markdown renderer and editable blocks. No external dependencies. */
(() => {
  'use strict';
  const esc = s => String(s).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const safeURL = (value, image = false) => /^(https?:|mailto:|#)/i.test(value) || value === '../design/Third-Party-Licenses.html' || (image && (/^data:image\/(png|jpeg|gif|webp);base64,/i.test(value) || /^assets\/[\w./-]+$/i.test(value) && !value.split('/').includes('..'))) ? value : '#';
  function inline(value) {
    if (window.VexRendering) return VexRendering.inline(value);
    const tokens = [];
    const token = html => { tokens.push(html); return `\u0001${tokens.length - 1}\u0002`; };
    let s = esc(value);
    s = s.replace(/`([^`]+)`/g, (_, code) => token(`<code>${code}</code>`));
    s = s.replace(/!\[([^\]]*)\]\(([^\s)]+)\)/g, (_, alt, url) => token(`<img src="${esc(safeURL(url.replace(/&amp;/g,'&'), true))}" alt="${alt}" loading="lazy">`));
    s = s.replace(/\[([^\]]+)\]\(([^\s)]+)\)/g, (_, label, url) => token(`<a href="${esc(safeURL(url.replace(/&amp;/g,'&')))}" target="_blank" rel="noopener noreferrer">${label}</a>`));
    s = s.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>').replace(/__(.+?)__/g, '<strong>$1</strong>').replace(/\*([^*\n]+)\*/g, '<em>$1</em>').replace(/~~(.+?)~~/g, '<del>$1</del>');
    return s.replace(/\u0001(\d+)\u0002/g, (_, n) => tokens[n]);
  }
  function listInfo(line) {
    const match = /^(\s*)([-+*]|\d+[.)])\s+(?:\[([ xX])\]\s+)?(.*)$/.exec(line);
    if (!match) return null;
    return { indent: match[1].replace(/\t/g, '    ').length, marker: match[2], checked: match[3], text: match[4] };
  }
  function renderList(lines, start) {
    const first = listInfo(lines[start]), base = first.indent;
    let index = start, items = [], lastItem = null;
    const ordered = /^\d/.test(first.marker), tag = ordered ? 'ol' : 'ul';
    while (index < lines.length) {
      const info = listInfo(lines[index]);
      if (!info || info.indent < base) break;
      if (info.indent > base) {
        if (!lastItem) break;
        const nested = renderList(lines, index);
        lastItem.html += nested.html;
        index = nested.end;
        continue;
      }
      if ((/^\d/.test(info.marker)) !== ordered) break;
      lastItem = { html: `<li>${info.checked === undefined
        ? inline(info.text)
        : `<input type="checkbox" data-task-line="${index}" aria-label="${esc(info.text)}" ${info.checked.toLowerCase() !== ' ' ? 'checked' : ''} contenteditable="false">${inline(info.text)}`}</li>` };
      items.push(lastItem);
      index++;
    }
    return { html: `<${tag}>${items.map(item => item.html).join('')}</${tag}>`, end: index, ordered };
  }
  function tocEntries(lines) {
    const entries = [], fences = { marker: null, length: 0 };
    lines.forEach((line, index) => {
      const fence = /^ {0,3}(`{3,}|~{3,})/.exec(line);
      if (fence) {
        if (!fences.marker) { fences.marker = fence[1][0]; fences.length = fence[1].length; }
        else if (fence[1][0] === fences.marker && fence[1].length >= fences.length) fences.marker = null;
        return;
      }
      if (fences.marker) return;
      const heading = /^ {0,3}(#{1,6})\s+(.+?)\s*#*\s*$/.exec(line);
      if (heading) entries.push({ level: heading[1].length, title: heading[2], id: `heading-${index}` });
    });
    return entries;
  }
  function parse(text) {
    const lines = text.split('\n'), offsets = []; let position = 0;
    lines.forEach(line => { offsets.push(position); position += line.length + 1; });
    const blocks = [], headings = [];
    const add = (type, start, end, html, extra = {}) => {
      const block = { type, start: offsets[start], end: end < lines.length ? offsets[end] - 1 : text.length, line: start + 1, endLine: end, raw: lines.slice(start, end).join('\n'), html, ...extra };
      blocks.push(block); if (type === 'heading') headings.push(block);
    };
    const special = l => /^(#{1,6}\s|```|~~~|>\s?|\s*[-*+]\s|\s*\d+[.)]\s|\s*(?:---+|\*\*\*+)\s*$)/.test(l) || l.trim() === '[TOC]' || /^\s*\[\^[^\]]+\]:/.test(l);
    for (let i = 0; i < lines.length;) {
      const start = i, line = lines[i]; let match;
      if (i === 0 && line.trim() === '---') {
        const end = lines.findIndex((l, n) => n > 0 && /^(---|\.\.\.)\s*$/.test(l));
        if (end > 0) { i = end + 1; continue; }
      }
      if (!line.trim()) { i++; continue; }
      if (line.trim() === '[TOC]') {
        const entries = tocEntries(lines);
        const html = `<nav class="md-toc" aria-label="目录"><strong>目录</strong>${entries.map(entry => `<a href="#${entry.id}" style="padding-left:${(entry.level - 1) * 14 + 8}px">${inline(entry.title)}</a>`).join('')}</nav>`;
        add('toc', i, ++i, html);
        continue;
      }
      if (line.trim() === '$$') {
        i++; while (i < lines.length && lines[i].trim() !== '$$') i++;
        const formula = lines.slice(start + 1,i).join('\n'); if (i < lines.length) i++;
        add('math',start,i,VexRendering.renderSpecial({kind:'math',text:formula,line:start + 1})); continue;
      }
      if ((match = /^(#{1,6})\s+(.+?)\s*#*\s*$/.exec(line))) {
        const level = match[1].length, title = match[2];
        add('heading', i, ++i, `<h${level}>${inline(title)}</h${level}>`, { level, title, id: `heading-${start}` }); continue;
      }
      if (i + 1 < lines.length && /^ {0,3}(=+|-+)\s*$/.test(lines[i + 1]) && !/^\s{4}|^\s*[>*+-]\s/.test(line)) {
        const level = lines[i + 1].trim().startsWith('=') ? 1 : 2, title = line.trim();
        i += 2; add('heading', start, i, `<h${level}>${inline(title)}</h${level}>`, { level, title, id: `heading-${start}` }); continue;
      }
      if ((match = /^ {0,3}(`{3,}|~{3,})(.*)/.exec(line))) {
        const marker = match[1], language = match[2].trim(); i++;
        const closing = new RegExp('^ {0,3}' + marker[0] + '{' + marker.length + ',}\\s*$');
        while (i < lines.length && !closing.test(lines[i])) i++;
        const code = lines.slice(start + 1, i).join('\n'); if (i < lines.length) i++;
        add('code', start, i, language.toLowerCase() === 'mermaid' && window.VexRendering ? VexRendering.renderSpecial({kind:'code',language,text:code,line:start+1}) : `<pre data-language="${esc(language)}"><code>${esc(code)}</code></pre>`, { language }); continue;
      }
      if (/^\s*(---+|\*\*\*+)\s*$/.test(line)) { add('hr', i, ++i, '<hr>'); continue; }
      if (i + 1 < lines.length && line.includes('|') && /^\s*\|?\s*:?-{3,}/.test(lines[i + 1])) {
        i += 2; while (i < lines.length && lines[i].trim() && lines[i].includes('|')) i++;
        const value=VexDocumentOps.table(lines.slice(start,i).join('\n'));
        if(value){const row=(cells,tag)=>'<tr>'+cells.map((cell,index)=>`<${tag} data-align="${value.align[index]||'left'}" style="text-align:${value.align[index]||'left'}">${inline(cell.replace(/\\\|/g,'|'))}</${tag}>`).join('')+'</tr>';add('table',start,i,`<div class="md-table-scroll" tabindex="0" aria-label="表格"><table><thead>${row(value.rows[0],'th')}</thead><tbody>${value.rows.slice(1).map(cells=>row(cells,'td')).join('')}</tbody></table></div>`);continue;}
        i=start;
      }
      const callout = /^ {0,3}>\s*\[!(INFO|WARNING)\]\s*$/i.exec(line);
      if (callout) {
        i++;
        const body = [];
        while (i < lines.length && /^ {0,3}>/.test(lines[i])) { body.push(lines[i].replace(/^ {0,3}>\s?/, '')); i++; }
        add('callout', start, i, `<aside class="md-callout ${callout[1].toLowerCase()}"><strong>${callout[1].toUpperCase() === 'WARNING' ? '警告' : '信息'}</strong><div>${inline(body.join('\n'))}</div></aside>`);
        continue;
      }
      if (/^ {0,3}\[\^([^\]\r\n]+)\]:/.test(line)) {
        const footnote = /^ {0,3}\[\^([^\]\r\n]+)\]:\s*(.*)$/.exec(line), label = footnote[1], body = footnote[2];
        add('footnote', i, ++i, `<div class="md-footnote" id="vex-footnote-${encodeURIComponent(label)}"><sup>[${esc(label)}]</sup> ${inline(body)} <a href="#vex-footnote-ref-${encodeURIComponent(label)}" aria-label="返回正文">↩</a></div>`, { label });
        continue;
      }
      if (/^>/.test(line)) {
        while (i < lines.length && /^>/.test(lines[i])) i++;
        add('quote', start, i, `<blockquote><p>${lines.slice(start,i).map(x => inline(x.replace(/^>\s?/,''))).join('<br>')}</p></blockquote>`); continue;
      }
      if (listInfo(line)) {
        const list = renderList(lines, i);
        add('list', start, list.end, list.html, { ordered: list.ordered });
        i = list.end;
        continue;
      }
      i++;
      while (i < lines.length && lines[i].trim() && !special(lines[i]) && !(i + 1 < lines.length && lines[i].includes('|') && /^\s*\|?\s*:?-{3,}/.test(lines[i + 1]))) i++;
      add('paragraph', start, i, `<p>${lines.slice(start, i).map(inline).join(window.VexPreferences?.values.softBreak === false ? ' ' : '<br>')}</p>`);
    }
    return { blocks, headings };
  }
  function render(text, editable = false) {
    const data = parse(text);
    return data.blocks.map((b, n) => `<div class="md-block" data-block="${n}" data-line="${b.line}" ${b.id ? `id="${editable ? 'edit-' : 'read-'}${b.id}"` : ''} ${editable && !['hr', 'code','math'].includes(b.type) ? 'contenteditable="true" spellcheck="false"' : ''}>${editable && b.type === 'code' && b.language.toLowerCase() !== 'mermaid' ? b.html.replace('<pre ', '<pre contenteditable="plaintext-only" spellcheck="false" role="textbox" aria-label="代码正文" ') : editable ? b.html : b.html.replace(/<input /g,'<input disabled ')}</div>`).join('') || (editable ? '<div class="md-block" data-block="0" contenteditable="true"><p><br></p></div>' : '<p class="muted">空白的一页，等待你的想法。</p>');
  }
  function listMarkdown(node, depth) {
    const ordered = node.tagName === 'OL', indent = '  '.repeat(depth);
    return [...node.children].map((item, index) => {
      const box = [...item.children].find(child => child.tagName === 'INPUT');
      const nested = [...item.children].filter(child => child.tagName === 'UL' || child.tagName === 'OL');
      const content = [...item.childNodes].filter(child => child.nodeType !== Node.ELEMENT_NODE || !/^(UL|OL|INPUT)$/.test(child.tagName)).map(toMarkdown).join('').trim();
      const prefix = ordered ? `${index + 1}. ` : '- ';
      return indent + prefix + (box ? `[${box.checked ? 'x' : ' '}] ` : '') + content + '\n' + nested.map(child => listMarkdown(child, depth + 1)).join('');
    }).join('');
  }
  function toMarkdown(node) {
    if (node.nodeType === Node.TEXT_NODE) return node.nodeValue.replace(/\u00a0/g,' ');
    if (node.nodeType !== Node.ELEMENT_NODE) return '';
    if (node.classList.contains('md-code-footer')) return '';
    if (node.dataset.markdown) return node.dataset.markdown;
    const tag = node.tagName.toLowerCase(), inner = () => [...node.childNodes].map(toMarkdown).join('');
    if (/^h[1-6]$/.test(tag)) return '#'.repeat(Number(tag[1])) + ' ' + inner() + '\n\n';
    if (tag === 'br') return '\n';
    if (tag === 'strong' || tag === 'b') return '**' + inner() + '**';
    if (tag === 'em' || tag === 'i') return '*' + inner() + '*';
    if (tag === 'del' || tag === 's') return '~~' + inner() + '~~';
    if (tag === 'a') return `[${inner()}](${safeURL(node.getAttribute('href') || '')})`;
    if (tag === 'img') return `![${node.alt || ''}](${safeURL(node.getAttribute('src') || '', true)})`;
    if (tag === 'pre') return '```' + (node.dataset.language || '') + '\n' + node.textContent.replace(/\n$/,'') + '\n```\n\n';
    if (tag === 'code') return '`' + node.textContent + '`';
    if (tag === 'blockquote') return inner().trim().split('\n').map(x => '> ' + x).join('\n') + '\n\n';
    if (tag === 'ul' || tag === 'ol') return listMarkdown(node, 0) + '\n';
    if (tag === 'li') return listMarkdown(node.parentElement, 0);
    if (tag === 'input') return '';
    if (tag === 'table') {
      return VexDocumentOps.fromDOM(node,toMarkdown) + '\n\n';
    }
    if (tag === 'p' || tag === 'div') return inner() + '\n\n';
    return inner();
  }
  function mount(host, options) {
    let active = null, composing = false;
    const clearActive = () => {
      if (!active) return;
      active.element.classList.remove('is-editing');
      active = null;
    };
    const repaint = () => {
      if (window.VexDiagramEditing?.busy(host)) return;
      active = null; const text = options.getText(); host.innerHTML = render(text, true);
      parse(text).blocks.forEach((block,index) => {
        const el = host.querySelector(`[data-block="${index}"]`);
        if (block.type !== 'code') return;
        if (block.language.toLowerCase() === 'mermaid' && window.VexPreferences?.values.mermaid !== false) {
          const before = text.slice(0,block.start), after = text.slice(block.end);
          VexDiagramEditing.mount(el,block,{begin:clearActive,change:raw => options.onChange(before + raw + after,true),finish:repaint});
        } else el.append(VexCodeFence.footer(block.language || ''));
      });
    };
    const begin = el => {
      if (active?.element === el) return;
      clearActive();
      const block = parse(options.getText()).blocks[Number(el.dataset.block)] || { start: 0, end: 0, raw: '' };
      if (el.querySelector('.diagram-editor') || block.type === 'math') return;
      active = { element: el, before: options.getText().slice(0, block.start), after: options.getText().slice(block.end), raw: block.raw, changed: false };
      el.classList.add('is-editing');
      if (block.type === 'code') {
        active.fence = VexCodeFence.parse(block.raw);
        el.querySelector('.md-code-language').value = active.fence?.language || block.language || '';
      }
    };
    const sync = () => {
      if (!active || composing) return;
      const raw = active.fence
        ? active.fence.write(active.element.querySelector('pre').innerText, active.element.querySelector('.md-code-language').value)
        : toMarkdown(active.element).trimEnd();
      active.changed = true;
      options.onChange(active.before + raw + active.after, true);
    };
    host.addEventListener('focusin', event => { const el = event.target.closest('.md-block'); if (el) begin(el); });
    host.addEventListener('compositionstart', () => { composing = true; });
    host.addEventListener('compositionend', () => { composing = false; sync(); });
    host.addEventListener('input', () => { if (!composing) sync(); });
    host.addEventListener('focusout', () => setTimeout(() => { if (active && !active.element.contains(document.activeElement)) { if (active.changed) repaint(); else clearActive(); } }, 0));
    host.addEventListener('paste', event => { if (event.target.matches('input')) return; event.preventDefault(); document.execCommand('insertText', false, event.clipboardData.getData('text/plain')); });
    host.addEventListener('change', event => {
      if (!event.target.matches('[data-task-line]')) return;
      const lines = options.getText().split('\n'), n = Number(event.target.dataset.taskLine);
      lines[n] = lines[n].replace(/\[[ xX]\]/, event.target.checked ? '[x]' : '[ ]');
      active = null; options.onChange(lines.join('\n')); repaint();
    });
    host.addEventListener('keydown', event => {
      if (composing || event.isComposing) return;
      if (active && event.target.matches('.md-code-language') && event.key === 'Enter') { event.preventDefault(); active.element.querySelector('pre').focus(); return; }
      if (event.key === 'Escape' && active) { const original = active.before + active.raw + active.after; active = null; options.onChange(original); repaint(); }
      if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') { event.preventDefault(); sync(); document.activeElement.blur(); }
    });
    repaint();
    return { repaint, flush: () => { if (active?.changed) sync(); clearActive(); }, format(command) { document.execCommand(command, false); sync(); }, get active() { return active; } };
  }
  window.TexEditor = { esc, inline, parse, render, mount, toMarkdown, safeURL };
})();
