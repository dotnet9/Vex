/* Shared document metrics and file navigation for both prototype shells. */
(() => {
  'use strict';
  const cjk = /[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}\p{Script=Hangul}]/gu;
  const words = /[\p{L}\p{N}][\p{L}\p{M}\p{N}]*(?:['\u2019_-][\p{L}\p{N}][\p{L}\p{M}\p{N}]*)*/gu;
  const cache = new Map();
  function text(source) {
    const el = document.createElement('div');
    el.innerHTML = TexEditor.render(source);
    el.querySelectorAll('img').forEach(img => img.replaceWith(document.createTextNode(img.alt)));
    el.querySelectorAll('p,li,th,td,h1,h2,h3,h4,h5,h6,pre,br').forEach(node => node.append(' '));
    return el.textContent.replace(/\s+/g, ' ').trim();
  }
  function measureText(plain) {
    const cjkCount = (plain.match(cjk) || []).length, wordCount = (plain.replace(cjk, ' ').match(words) || []).length;
    return {count: cjkCount + wordCount, cjkCount, wordCount, characters: Array.from(plain.replace(/\s/gu, '')).length};
  }
  function metrics(source) {
    if (cache.has(source)) return cache.get(source);
    const plain = text(source), result = Object.assign(measureText(plain), {excerpt: Array.from(plain).slice(0, 120).join('')});
    if (cache.size >= 40) cache.delete(cache.keys().next().value);
    cache.set(source, result); return result;
  }
  function renderFiles({container, docs, current, folder, mode, descending, query, sort, onOpen, icon, sortComparers, onSwitchTree}) {
    const collapsed = new Set([...container.querySelectorAll('details:not([open])')].map(el => el.dataset.path));
    container.replaceChildren(); container.classList.toggle('file-excerpts', mode === 'list');
    const folders = new Map();
    function directory(parent, name, path) {
      const group = document.createElement('details'); group.className = 'file-directory'; group.dataset.path = path;
      group.open = !!query || !collapsed.has(path);
      const summary = document.createElement('summary'); summary.dataset.contextKind = 'folder'; summary.dataset.folderPath = path; summary.innerHTML = icon('folder');
      const label = document.createElement('span'); label.textContent = name; summary.append(label);
      const children = document.createElement('div'); children.className = 'file-children';
      group.append(summary, children); parent.append(group); return children;
    }
    const root = mode === 'tree' ? directory(container, folder, '') : container;
    folders.set('', root);
    docs.map((doc, index) => ({doc, index})).sort(sort === 'asc' ? (a,b) => (a.doc.name < b.doc.name ? -1 : a.doc.name > b.doc.name ? 1 : 0)
        : sort === 'desc' ? (a,b) => (a.doc.name < b.doc.name ? 1 : a.doc.name > b.doc.name ? -1 : 0)
        : (a,b) => a.index - b.index)
      .sort(sort ? sortComparers[sort] : undefined)
      .filter(({doc}) => !(mode === 'list' && (doc.path || doc.name).includes('/')) && doc.name.toLowerCase().includes(query.toLowerCase().trim()));
    if (mode === 'list' && visible.length > 200) {
      /* 文件过多：列表性能不佳，提示切换树形视图（链接点击即切）。 */
      container.innerHTML = '<div class="file-list-overflow"><div class="overflow-warn">⚠</div>当前文件夹文件过多，请切换至<a href="#" data-switch-tree>树形视图</a>以获得更佳的使用体验。</div>';
      const link = container.querySelector('[data-switch-tree]');
      if (link && onSwitchTree) link.onclick = function (event) { event.preventDefault(); onSwitchTree(); };
      return;
    }
    visible.forEach(({doc, index}) => {
        let parent = root, path = '';
        if (mode === 'tree') (doc.treePath || doc.path || doc.name).split('/').slice(0,-1).forEach(part => {
          path += '/' + part;
          if (!folders.has(path)) folders.set(path, directory(parent, part, path));
          parent = folders.get(path);
        });
        const button = document.createElement('button');
        button.className = 'file-row file-item' + (doc === current ? ' selected active' : '');
        button.dataset.fileName = doc.name; button.dataset.fileIndex = index; button.dataset.filePath = doc.path || doc.name; button.dataset.contextKind = 'file'; button.title = doc.path || doc.name;
        button.setAttribute('aria-current', doc === current ? 'page' : 'false');
        /* 列表行与树形行同一观感语言：图标 + 名称单行；列表只显当前目录的直接子文件（不递归、无摘要）。 */
        if (mode === 'tree') button.innerHTML = icon('file');
        const label = document.createElement('span'); label.className = 'file-name'; label.textContent = doc.name;
        if (mode === 'list') button.innerHTML = icon('file');
        button.append(label);
        button.onclick = () => onOpen(doc,index); parent.append(button);
      });
    if (!container.querySelector('.file-row')) {
      const empty = document.createElement('p'); empty.className = 'empty-state'; empty.textContent = '没有匹配的文档'; container.append(empty);
    }
  }
  window.VexWriting = {metrics, measureText, renderFiles};
})();
