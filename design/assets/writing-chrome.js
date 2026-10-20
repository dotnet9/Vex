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
  function renderFiles({container, docs, current, folder, mode, descending, query, onOpen, icon}) {
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
    docs.map((doc, index) => ({doc, index})).sort((a,b) => (descending ? -1 : 1) * a.doc.name.localeCompare(b.doc.name, 'zh-CN'))
      .filter(({doc}) => doc.name.toLowerCase().includes(query.toLowerCase().trim())).forEach(({doc, index}) => {
        let parent = root, path = '';
        if (mode === 'tree') (doc.path || doc.name).split('/').slice(0,-1).forEach(part => {
          path += '/' + part;
          if (!folders.has(path)) folders.set(path, directory(parent, part, path));
          parent = folders.get(path);
        });
        const button = document.createElement('button');
        button.className = 'file-row file-item' + (doc === current ? ' selected active' : '');
        button.dataset.fileName = doc.name; button.dataset.fileIndex = index; button.dataset.filePath = doc.path || doc.name; button.dataset.contextKind = 'file'; button.title = doc.path || doc.name;
        button.setAttribute('aria-current', doc === current ? 'page' : 'false');
        if (mode === 'tree') button.innerHTML = icon('file');
        const label = document.createElement('span'); label.className = 'file-name'; label.textContent = doc.name; button.append(label);
        if (mode === 'list') {
          const excerpt = document.createElement('span'); excerpt.className = 'file-excerpt';
          excerpt.textContent = metrics(doc.text).excerpt; button.append(excerpt);
        }
        button.onclick = () => onOpen(doc,index); parent.append(button);
      });
    if (!container.querySelector('.file-row')) {
      const empty = document.createElement('p'); empty.className = 'empty-state'; empty.textContent = '没有匹配的文档'; container.append(empty);
    }
  }
  window.VexWriting = {metrics, measureText, renderFiles};
})();
