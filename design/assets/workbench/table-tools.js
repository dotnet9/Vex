/* 阅读视图表格工具（ZCode 式）：复制 Markdown / 下载 CSV / 预览表格。
 * 通过 MutationObserver 增强渲染管线产出的 .md-table-scroll，不侵入渲染代码；
 * 排版卡迷你预览（.type-swatch）与预览浮层内不再叠加工具栏。 */
(function () {
  'use strict';
  var counter = 0;

  function tableData(scroll) {
    var table = scroll.querySelector('table');
    if (!table) return null;
    var rows = [];
    table.querySelectorAll('tr').forEach(function (tr) {
      var cells = [];
      tr.querySelectorAll('th,td').forEach(function (cell) { cells.push(cell.textContent.trim()); });
      rows.push(cells);
    });
    return rows.length ? rows : null;
  }

  function toMarkdown(rows) {
    var width = Math.max.apply(null, rows.map(function (row) { return row.length; }));
    function pad(row) {
      var out = row.slice();
      while (out.length < width) out.push('');
      return out.map(function (cell) { return String(cell).replace(/\|/g, '\\|'); });
    }
    var out = ['| ' + pad(rows[0]).join(' | ') + ' |', '|' + rows[0].map(function () { return ' --- |'; }).join('')];
    rows.slice(1).forEach(function (row) { out.push('| ' + pad(row).join(' | ') + ' |'); });
    return out.join('\n');
  }

  function toCsv(rows) {
    return '\uFEFF' + rows.map(function (row) {
      return row.map(function (cell) {
        cell = String(cell).replace(/"/g, '""');
        return /[",\n]/.test(cell) ? '"' + cell + '"' : cell;
      }).join(',');
    }).join('\r\n');
  }

  function copyText(text, done) {
    function fallback() {
      var area = document.createElement('textarea');
      area.value = text; area.style.cssText = 'position:fixed;opacity:0';
      document.body.append(area); area.select();
      try { document.execCommand('copy'); } catch (_) {}
      area.remove(); done();
    }
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).then(done, fallback);
    else fallback();
  }

  function flash(button, message) {
    var original = button.textContent;
    button.textContent = message; button.disabled = true;
    setTimeout(function () { button.textContent = original; button.disabled = false; }, 1200);
  }

  function documentName() {
    var title = document.querySelector('.document-title');
    return (title && title.textContent ? title.textContent.trim() : 'vex') || 'vex';
  }

  function downloadCsv(rows, index) {
    var blob = new Blob([toCsv(rows)], { type: 'text/csv;charset=utf-8' });
    var link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = documentName() + '-表格' + (index > 1 ? '-' + index : '') + '.csv';
    document.body.append(link); link.click(); link.remove();
    setTimeout(function () { URL.revokeObjectURL(link.href); }, 1000);
  }

  function openPreview(scroll) {
    var stale = document.getElementById('vex-table-preview');
    if (stale) stale.remove();
    var overlay = document.createElement('div');
    overlay.id = 'vex-table-preview';
    var label = documentName() + ' · 表格预览';
    overlay.innerHTML = '<div class="table-preview-card" role="dialog" aria-modal="true" aria-label="' + label + '">'
      + '<div class="table-preview-head"><strong>' + label + '</strong>'
      + '<button type="button" class="table-preview-close">关闭</button></div>'
      + '<div class="table-preview-body md-body"></div></div>';
    var clone = scroll.cloneNode(true);
    clone.removeAttribute('tabindex');
    overlay.querySelector('.table-preview-body').append(clone);
    document.body.append(overlay);
    overlay.addEventListener('click', function (event) {
      if (event.target === overlay || event.target.closest('.table-preview-close')) overlay.remove();
    });
    document.addEventListener('keydown', function esc(event) {
      if (event.key === 'Escape') { overlay.remove(); document.removeEventListener('keydown', esc); }
    });
    overlay.querySelector('.table-preview-close').focus();
  }

  function enhance(scroll) {
    if (!scroll || scroll.closest('#vex-table-preview') || scroll.closest('.type-swatch')) return;
    var previous = scroll.previousElementSibling;
    if (previous && previous.classList && previous.classList.contains('md-table-toolbar')) return;
    var rows = tableData(scroll);
    if (!rows) return;
    var index = ++counter;
    var block = document.createElement('div');
    block.className = 'md-table-block';
    var bar = document.createElement('div');
    bar.className = 'md-table-toolbar';
    bar.setAttribute('role', 'group');
    bar.setAttribute('aria-label', '表格操作');
    function action(text, handler) {
      var button = document.createElement('button');
      button.type = 'button'; button.textContent = text;
      button.onclick = handler;
      return button;
    }
    var copyButton = action('复制 Markdown', function () { copyText(toMarkdown(rows), function () { flash(copyButton, '已复制'); }); });
    bar.append(copyButton);
    bar.append(action('下载 CSV', function () { downloadCsv(rows, index); flash(bar.children[1], '已生成'); }));
    bar.append(action('预览表格', function () { openPreview(scroll); }));
    scroll.before(block);
    block.append(bar, scroll);
  }

  function scan(root) { (root || document).querySelectorAll('.md-table-scroll').forEach(enhance); }

  var observer = new MutationObserver(function (mutations) {
    mutations.forEach(function (mutation) {
      mutation.addedNodes.forEach(function (node) {
        if (node.nodeType !== 1) return;
        if (node.classList && node.classList.contains('md-table-scroll')) enhance(node);
        else if (node.querySelectorAll) node.querySelectorAll('.md-table-scroll').forEach(enhance);
      });
    });
  });
  function start() { scan(); observer.observe(document.body, { childList: true, subtree: true }); }
  if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
  else start();
})();
