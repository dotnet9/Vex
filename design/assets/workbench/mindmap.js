/* 原型解析器：保留段落边界与源码行号。正式接入应使用 Markdown AST。 */
(function () {
  'use strict';
  var segmenter = typeof Intl.Segmenter === 'function' ? new Intl.Segmenter('zh', { granularity: 'grapheme' }) : null;
  function characters(text) {
    return segmenter ? Array.from(segmenter.segment(text), function (part) { return part.segment; }) : Array.from(text);
  }
  function plain(text) {
    return text.replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1').replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
      .replace(/<[^>]*>/g, '').replace(/[*_~`]/g, '').replace(/\\([\\*_[\]`#])/g, '$1').replace(/\s+/g, ' ').trim();
  }
  function clip(text, limit) {
    var parts = characters(text);
    return parts.length > limit ? parts.slice(0, limit).join('') + '…' : text;
  }
  function parse(markdown, fileTitle) {
    var lines = markdown.replace(/\r\n?/g, '\n').split('\n'), serial = 0, blocks = [];
    function block(kind, text, start, end, extra) {
      blocks.push(Object.assign({ kind: kind, text: text, line: start + 1, endLine: end + 1, raw: lines.slice(start, end + 1).join('\n') }, extra));
    }
    function heading(line) { return /^ {0,3}(#{1,6})\s+(.+?)(?:\s+#+\s*)?$/.exec(line); }
    function list(line) { return /^(\s*)(?:[-+*]|\d+[.)])\s+(?:\[([ xX])\]\s+)?(.+)$/.exec(line); }
    function fence(line) { return /^ {0,3}(`{3,}|~{3,})(.*)$/.exec(line); }
    function structural(index) {
      var line = lines[index] || '';
      return heading(line) || list(line) || fence(line) || line.trim() === '[TOC]' || /^\s*\[\^[^\]]+\]:/.test(line) || /^\s*\$\$\s*$/.test(line) || /^\s*>/.test(line) || /^\s*(?:---+|\*\*\*+|___+)\s*$/.test(line)
        || (index + 1 < lines.length && /^ {0,3}(?:=+|-+)\s*$/.test(lines[index + 1]));
    }
    for (var i = 0; i < lines.length;) {
      var start = i, line = lines[i], match;
      if (!line.trim()) { i++; continue; }
      if ((match = fence(line))) {
        var marker = match[1], language = match[2].trim();
        i++;
        while (i < lines.length && !(new RegExp('^ {0,3}' + marker[0] + '{' + marker.length + ',}\\s*$')).test(lines[i])) i++;
        var content = lines.slice(start + 1, i).join('\n');
        if (i < lines.length) i++;
        block('code', content, start, i - 1, { language: language || 'text', label: '代码块' + (language ? ' · ' + language : '') });
      } else if (/^\s*\$\$\s*$/.test(line)) {
        i++; while (i < lines.length && !/^\s*\$\$\s*$/.test(lines[i])) i++;
        var formula = lines.slice(start + 1, i).join('\n');
        if (i < lines.length) i++;
        block('math', formula, start, i - 1, { label: '公式' });
      } else if ((match = heading(line))) {
        block('heading', plain(match[2]), i, i, { level: match[1].length }); i++;
      } else if (i + 1 < lines.length && /^ {0,3}(?:=+|-+)\s*$/.test(lines[i + 1])) {
        block('heading', plain(line), i, i + 1, { level: lines[i + 1].trim()[0] === '=' ? 1 : 2 }); i += 2;
      } else if (/^\s*(?:---+|\*\*\*+|___+)\s*$/.test(line)) {
        block('rule', '', i, i); i++;
      } else if ((match = list(line))) {
        var text = match[3], indent = match[1].replace(/\t/g, '    ').length;
        i++;
        while (i < lines.length && lines[i].trim() && !structural(i) && /^\s+/.test(lines[i])) text += ' ' + lines[i++].trim();
        block('list', plain(text), start, i - 1, { indent: indent, checked: match[2] === undefined ? null : match[2].toLowerCase() === 'x' });
      } else if (/^\s*>/.test(line)) {
        i++;
        while (i < lines.length && /^\s*>/.test(lines[i])) i++;
        block('quote', plain(lines.slice(start, i).map(function (item) { return item.replace(/^\s*>\s?/, ''); }).join(' ')), start, i - 1);
      } else if (i + 1 < lines.length && line.includes('|') && /^\s*\|?\s*:?-{3,}/.test(lines[i + 1])) {
        i += 2;
        while (i < lines.length && lines[i].includes('|') && lines[i].trim()) i++;
        block('table', plain(line.replace(/\|/g, ' · ')), start, i - 1, { label: '表格 · ' + (i - start - 2) + ' 行' });
      } else {
        i++;
        while (i < lines.length && lines[i].trim() && !structural(i)) i++;
        block('paragraph', plain(lines.slice(start, i).join(' ')), start, i - 1);
      }
    }
    function node(item) { return Object.assign({ id: 'node-' + serial++, children: [] }, item); }
    var topHeadings = blocks.filter(function (item) { return item.kind === 'heading' && item.level === 1; });
    var useFirst = blocks.length && blocks[0].kind === 'heading' && blocks[0].level === 1 && topHeadings.length === 1;
    var root = node(useFirst ? blocks[0] : { kind: 'heading', level: 0, text: fileTitle || '未命名文档', line: 1, endLine: lines.length, raw: markdown });
    root.isRoot = true;
    var stack = [root], listStack = [];
    blocks.slice(useFirst ? 1 : 0).forEach(function (item) {
      var child = node(item);
      if (item.kind === 'heading') {
        while (stack.length > 1 && stack[stack.length - 1].level >= item.level) stack.pop();
        stack[stack.length - 1].children.push(child); stack.push(child); listStack = [];
      } else if (item.kind === 'list') {
        while (listStack.length && listStack[listStack.length - 1].indent >= item.indent) listStack.pop();
        (listStack.length ? listStack[listStack.length - 1] : stack[stack.length - 1]).children.push(child);
        listStack.push(child);
      } else {
        stack[stack.length - 1].children.push(child); listStack = [];
      }
    });
    return { root: root, blocks: blocks, lines: lines };
  }
  function flatten(root) {
    var nodes = [];
    function visit(node, depth, branch, key) {
      node.depth = depth; node.branch = branch; node.key = key; nodes.push(node);
      var occurrences = new Map();
      node.children.forEach(function (child, index) {
        var identity = child.kind + ':' + child.text, occurrence = occurrences.get(identity) || 0;
        occurrences.set(identity, occurrence + 1);
        visit(child, depth + 1, depth === 0 ? index : branch, key + '/' + identity + ':' + occurrence);
      });
    }
    visit(root, 0, 0, 'document'); return nodes;
  }
  window.VexMindMap = { parse: parse, plain: plain, clip: clip, characters: characters, flatten: flatten };
})();
