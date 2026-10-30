/* Source-preserving operations used by the prototype's contextual controls. */
(() => {
  'use strict';
  function cells(line) {
    const result = []; let value = '', ticks = 0;
    for (let i = 0; i < line.length; i++) {
      const char = line[i];
      if (char === '\\' && i + 1 < line.length) { value += char + line[++i]; continue; }
      if (char === '`') {
        let count = 1; while (line[i + count] === '`') count++;
        if (!ticks) ticks = count; else if (ticks === count) ticks = 0;
        value += '`'.repeat(count); i += count - 1; continue;
      }
      if (char === '|' && !ticks) { result.push(value.trim()); value = ''; } else value += char;
    }
    result.push(value.trim());
    if (line.trim().startsWith('|')) result.shift();
    if (line.trim().endsWith('|') && result.at(-1) === '') result.pop();
    return result;
  }
  function table(raw) {
    const lines = raw.trim().split('\n');
    if (lines.length < 2) return null;
    const head = cells(lines[0]), separators = cells(lines[1]);
    if (!head.length || separators.length !== head.length || separators.some(cell => !/^:?-{3,}:?$/.test(cell))) return null;
    return { rows: [head, ...lines.slice(2).map(cells)].map(row => head.map((_, i) => row[i] || '')),
      align: separators.map(cell => cell.startsWith(':') ? cell.endsWith(':') ? 'center' : 'left' : cell.endsWith(':') ? 'right' : '') };
  }
  function serialize(value) {
    const separators = value.align.map(align => ({left:':---',center:':---:',right:'---:'}[align] || '---'));
    const rows = [value.rows[0], separators, ...value.rows.slice(1)];
    const widths = separators.map((_, i) => Math.max(3, ...rows.map(row => (row[i] || '').length)));
    return rows.map(row => '| ' + widths.map((width, i) => (row[i] || '').padEnd(width)).join(' | ') + ' |').join('\n');
  }
  function canTable(raw, action, row, col) {
    const value = table(raw); if (!value) return false;
    if (!Number.isInteger(row) || !Number.isInteger(col) || row < 0 || row >= value.rows.length || col < 0 || col >= value.align.length) return false;
    if (['row-above','row-delete'].includes(action)) return row > 0;
    if (action === 'row-up') return row > 1;
    if (action === 'row-down') return row > 0 && row < value.rows.length - 1;
    if (action === 'col-left') return col > 0;
    if (action === 'col-right') return col < value.align.length - 1;
    if (action === 'col-delete') return value.align.length > 1;
    return true;
  }
  function changeTable(raw, action, row = 0, col = 0) {
    if (!canTable(raw, action, row, col)) return null;
    const value = table(raw), empty = () => value.align.map(() => '');
    if (action === 'row-above' || action === 'row-below') { row += action === 'row-below' ? 1 : 0; value.rows.splice(row, 0, empty()); }
    if (action === 'row-delete') { value.rows.splice(row, 1); row = Math.min(row, value.rows.length - 1); }
    if (action === 'row-up' || action === 'row-down') { const next = row + (action === 'row-up' ? -1 : 1); [value.rows[row], value.rows[next]] = [value.rows[next], value.rows[row]]; row = next; }
    if (action === 'col-before' || action === 'col-after') { col += action === 'col-after' ? 1 : 0; value.rows.forEach(row => row.splice(col, 0, '')); value.align.splice(col, 0, ''); }
    if (action === 'col-delete') { value.rows.forEach(row => row.splice(col, 1)); value.align.splice(col, 1); col = Math.min(col, value.align.length - 1); }
    if (action === 'col-left' || action === 'col-right') { const next = col + (action === 'col-left' ? -1 : 1); [...value.rows, value.align].forEach(row => { [row[col], row[next]] = [row[next], row[col]]; }); col = next; }
    if (action.startsWith('align-')) value.align[col] = action.slice(6);
    return { raw: serialize(value), row, col };
  }
  function wrap(text, start, end, marker) {
    if (start === end) return { text, start, end };
    if (text.slice(start - marker.length, start) === marker && text.slice(end, end + marker.length) === marker) {
      return { text: text.slice(0, start - marker.length) + text.slice(start, end) + text.slice(end + marker.length), start: start - marker.length, end: end - marker.length };
    }
    return { text: text.slice(0, start) + marker + text.slice(start, end) + marker + text.slice(end), start: start + marker.length, end: end + marker.length };
  }
  /* <span style> 行内样式（字号/前景色/背景色）：选区被同款 span 包住时改写或解包，否则新包一层。 */
  const SPAN_OPEN = /<span\s+style="([^"]*)"\s*>/;
  function parseSpanStyle(raw) {
    const style = {};
    String(raw || '').split(';').forEach(decl => {
      const at = decl.indexOf(':'); if (at < 0) return;
      const prop = decl.slice(0, at).trim().toLowerCase(), value = decl.slice(at + 1).trim();
      if (['color', 'background-color', 'font-size'].includes(prop) && value) style[prop] = value;
    });
    return style;
  }
  function serializeSpanStyle(style) {
    return ['color', 'font-size', 'background-color'].filter(prop => style[prop]).map(prop => prop + ':' + style[prop]).join(';');
  }
  function wrapSpan(text, start, end, prop, value, toggle = true) {
    if (start === end) return { text, start, end };
    /* 只看紧贴选区左侧的开标签：取窗口里最近一个 '<span'，否则更早的 span 会干扰合并判定。 */
    const window = text.slice(Math.max(0, start - 200), start);
    const at = window.lastIndexOf('<span');
    const open = at >= 0 ? SPAN_OPEN.exec(window.slice(at)) : null;
    if (open && open.index === 0 && text.slice(end, end + 7) === '</span>') {
      const absolute = Math.max(0, start - 200) + at, style = parseSpanStyle(open[1]), inner = text.slice(start, end);
      const current = style[prop];
      if (!current || current.toLowerCase() !== value.toLowerCase()) {
        style[prop] = value;
        const openTag = '<span style="' + serializeSpanStyle(style) + '">';
        return { text: text.slice(0, absolute) + openTag + inner + '</span>' + text.slice(end + 7), start: absolute + openTag.length, end: absolute + openTag.length + inner.length };
      }
      if (!toggle) return { text, start, end }; /* 选色器设同值：无操作，保住选区 */
      delete style[prop];
      if (!Object.keys(style).length) return { text: text.slice(0, absolute) + inner + text.slice(end + 7), start: absolute, end: absolute + inner.length };
      const openTag = '<span style="' + serializeSpanStyle(style) + '">';
      return { text: text.slice(0, absolute) + openTag + inner + '</span>' + text.slice(end + 7), start: absolute + openTag.length, end: absolute + openTag.length + inner.length };
    }
    const openTag = '<span style="' + prop + ':' + value + '">';
    return { text: text.slice(0, start) + openTag + text.slice(start, end) + '</span>' + text.slice(end), start: start + openTag.length, end: end + openTag.length };
  }
  function clearSpanStyle(text, start, end) {
    const cuts = []; let match;
    const tag = /<span\s+style="[^"]*"\s*>|<\/span\s*>/g;
    while ((match = tag.exec(text))) if (match.index < end && match.index + match[0].length > start) cuts.push([match.index, match.index + match[0].length]);
    if (!cuts.length) return { text, start, end };
    const map = position => {
      let delta = 0;
      for (const [from, to] of cuts) {
        if (position >= to) delta += to - from;
        else if (position > from) return from - delta;
        else break;
      }
      return position - delta;
    };
    let out = '', cursor = 0;
    for (const [from, to] of cuts) { out += text.slice(cursor, from); cursor = to; }
    out += text.slice(cursor);
    return { text: out, start: Math.max(0, map(start)), end: Math.max(0, map(end)) };
  }
  function convert(raw, kind) {
    const lines = raw.split('\n');
    if (lines.length === 2 && /^\s*(=+|-+)\s*$/.test(lines[1])) lines.pop();
    const content = lines.map(line => line.replace(/^\s*(?:#{1,6}\s+|>\s?|(?:[-+*]|\d+[.)])\s+(?:\[[ xX]\]\s*)?)/, ''));
    if (kind.startsWith('heading')) return '#'.repeat(Number(kind.slice(7))) + ' ' + content.join(' ').trim();
    if (kind === 'paragraph') return content.join('\n');
    return content.map((line, i) => ({quote:'> ',list:'- ',ordered:(i + 1) + '. ',task:'- [ ] '}[kind] || '') + line).join('\n');
  }
  function inlineTokens(raw) {
    return [...raw.matchAll(/(!?)\[([^\]\n]*)\]\(([^\s)]+)\)/g)].map(match => ({kind:match[1] ? 'image' : 'link',label:match[2],url:match[3],start:match.index,end:match.index + match[0].length}));
  }
  function fromDOM(node, inline) {
    return serialize({rows:[...node.rows].map(row=>[...row.cells].map(cell=>inline(cell).trim().replace(/\n/g,' ').replace(/(?<!\\)\|/g,'\\|'))),align:[...node.rows[0].cells].map(cell=>cell.dataset.align||cell.style.textAlign||'')});
  }
  window.VexDocumentOps = {cells,table,serialize,canTable,changeTable,wrap,wrapSpan,clearSpanStyle,convert,inlineTokens,fromDOM};
})();
