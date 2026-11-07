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
    if (!source.trim()) return {count: 0, cjkCount: 0, wordCount: 0, characters: 0, excerpt: ''};
    if (cache.has(source)) return cache.get(source);
    const plain = text(source), result = Object.assign(measureText(plain), {excerpt: Array.from(plain).slice(0, 120).join('')});
    if (cache.size >= 40) cache.delete(cache.keys().next().value);
    cache.set(source, result); return result;
  }
  function renderFiles({container, docs, current, folder, roots, mode, descending, query, sort, onOpen, onFoldRoot, onRootMenu, onMoveDoc, icon, sortComparers, onSwitchTree}) {
    const escaped = value => String(value).replace(/[&<>"']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[ch]));
    const queryTrim = (query || '').toLowerCase().trim();
    const collapsed = new Set([...container.querySelectorAll('details:not([open])')].map(el => el.dataset.path));
    container.replaceChildren();
    const folders = new Map();
    function directory(parent, name, path) {
      const group = document.createElement('details'); group.className = 'file-directory'; group.dataset.path = path;
      group.open = !!queryTrim || !collapsed.has(path);
      const summary = document.createElement('summary'); summary.dataset.contextKind = 'folder'; summary.dataset.folderPath = path; summary.innerHTML = icon('folder');
      const label = document.createElement('span'); label.textContent = name; summary.append(label);
      const children = document.createElement('div'); children.className = 'file-children';
      group.append(summary, children); parent.append(group); return children;
    }
    /* 比较器按文档字段比（natural-desc / name-asc 等），这里裹一层 {doc,index} 以便回填原序；
       'group' 在 sortComparers 里映射为 null（只按文件夹分组、不比字段），必须挡掉：
       Array.prototype.sort(null) 会直接抛错。同值一律退回原序，切换排序不会随机换位。 */
    const comparerFor = sortKey => sortKey ? sortComparers[sortKey] : null;
    function renderRowsInto(parent, items, mode, rootName, multiRoot, queryActive, onMoveDoc) {
      items.forEach(({doc, index}) => {
        let host = parent, path = '';
        if (mode === 'tree') (doc.treePath || doc.path || doc.name).split('/').slice(0,-1).forEach(part => {
          path += '/' + part;
          const key = (rootName || '') + path;
          if (!folders.has(key)) folders.set(key, directory(parent, part, path));
          host = folders.get(key);
        });
        const button = document.createElement('button');
        button.className = 'file-row file-item' + (doc === current ? ' selected active' : '');
        button.dataset.fileName = doc.name; button.dataset.fileIndex = index; button.dataset.filePath = doc.path || doc.name; button.dataset.contextKind = 'file'; button.title = doc.path || doc.name;
        button.setAttribute('aria-current', doc === current ? 'page' : 'false');
        if (onMoveDoc) {
          button.draggable = true;
          button.ondragstart = event => { event.dataTransfer.setData('text/plain', String(index)); event.dataTransfer.effectAllowed = 'move'; };
        }
        /* 列表行与树形行同一观感语言：图标 + 名称单行，子目录文件行尾加目录标签。 */
        const fileType = /^readme\.md$/i.test(doc.name) ? 'readme' : /\.(md|markdown|mdown)$/i.test(doc.name) ? 'markdown' : null;
        button.innerHTML = fileType
          ? '<img class="ico file-type-icon" src="assets/generated/file-icons/' + fileType + '.png" alt="" aria-hidden="true">'
          : icon('file');
        const label = document.createElement('span'); label.className = 'file-name'; label.textContent = doc.name;
        button.append(label);
        if (mode === 'list') {
          /* 跨根查找时结果行带所属根前缀，多根下同名文件一眼可分。 */
          if (queryActive && multiRoot && rootName) {
            const rootTag = document.createElement('small'); rootTag.className = 'file-root-tag'; rootTag.textContent = rootName; button.append(rootTag);
          }
          const rel = doc.treePath || doc.path || doc.name;
          const dir = rel.includes('/') ? rel.slice(0, rel.lastIndexOf('/')) : '';
          if (dir) { const tag = document.createElement('small'); tag.className = 'file-folder-tag'; tag.textContent = dir; button.append(tag); }
        }
        button.onclick = () => onOpen(doc,index); host.append(button);
      });
    }
    if (roots && roots.length) {
      /* 多根工作区：每根一个可折叠组头（名称 + 计数 + 操作菜单），
         查找时忽略折叠并隐藏空根，结果行带根名前缀。 */
      const multiRoot = roots.length > 1;
      let shown = 0;
      roots.forEach(root => {
        const items = docs.map((doc, index) => ({doc, index}))
          .filter(({doc}) => doc.root === root.key && doc.name.toLowerCase().includes(queryTrim));
        if (queryTrim && !items.length) return;
        const comparer = comparerFor(root.sort || sort);
        if (comparer) items.sort((a, b) => comparer(a.doc, b.doc) || a.index - b.index);
        const section = document.createElement('div'); section.className = 'file-root' + (root.folded && !queryTrim ? ' folded' : ''); section.dataset.root = root.key;
        const head = document.createElement('div'); head.className = 'file-root-head';
        head.innerHTML = '<button type="button" class="file-root-fold" aria-label="' + (root.folded ? '展开' : '折叠') + '文件夹" aria-expanded="' + (!root.folded) + '">'
          + '<svg class="ico" aria-hidden="true"><use href="#i-chevron-down"/></svg></button>'
          + '<span class="file-root-name">' + escaped(root.name) + '</span>'
          + '<span class="file-root-count">' + items.length + '</span>'
          + '<button type="button" class="file-root-more" aria-haspopup="menu" aria-expanded="false" aria-label="' + escaped(root.name) + ' 文件夹操作"><svg class="ico" aria-hidden="true"><use href="#i-more-v"/></svg></button>';
        section.append(head);
        const children = document.createElement('div'); children.className = 'file-root-children';
        section.append(children);
        renderRowsInto(children, items, mode, root.name, multiRoot, !!queryTrim, onMoveDoc);
        if (root.folded && !queryTrim) {
          head.querySelector('.file-root-fold').setAttribute('aria-expanded', 'false');
        }
        head.querySelector('.file-root-fold').onclick = event => { event.stopPropagation(); onFoldRoot(root.key, !root.folded); };
        head.querySelector('.file-root-more').onclick = event => { event.stopPropagation(); onRootMenu(event.currentTarget, root.key); };
        if (onMoveDoc) {
          /* 侧栏拖拽移动：文件行可拖到目标根（投放即改归属，不触碰磁盘）。 */
          section.ondragover = event => { event.preventDefault(); event.dataTransfer.dropEffect = 'move'; section.classList.add('drop-target'); };
          section.ondragleave = event => { if (!section.contains(event.relatedTarget)) section.classList.remove('drop-target'); };
          section.ondrop = event => {
            event.preventDefault(); section.classList.remove('drop-target');
            const index = Number(event.dataTransfer.getData('text/plain'));
            if (!Number.isNaN(index)) onMoveDoc(index, root.key);
          };
        }
        container.append(section);
        shown += items.length; // 折叠根的计数也参与「整页无结果」判断
      });
      if (!shown) {
        const empty = document.createElement('p'); empty.className = 'empty-state'; empty.textContent = '没有匹配的文档'; container.append(empty);
      }
      return;
    }
    const root = mode === 'tree' ? directory(container, folder, '') : container;
    folders.set('', root);
    /* 列表 = 打开目录的全部文件（含子目录，扁平不递归分组），与原生 FlatDocumentFileNodes 同口径；
       子目录文件在行尾挂一枚目录标签消歧（同名文件靠它区分）。内置演示的 path 是取数路径
       （../docs/…），层级一律以 treePath 优先判断。 */
    const visible = docs.map((doc, index) => ({doc, index}))
      .filter(({doc}) => doc.name.toLowerCase().includes(queryTrim));
    const comparer = comparerFor(sort);
    if (comparer) visible.sort((a, b) => comparer(a.doc, b.doc) || a.index - b.index);
    if (mode === 'list' && visible.length > 200) {
      /* 文件过多：列表性能不佳，提示切换树形视图（链接点击即切）。 */
      container.innerHTML = '<div class="file-list-overflow"><div class="overflow-warn">⚠</div>当前文件夹文件过多，请切换至<a href="#" data-switch-tree>树形视图</a>以获得更佳的使用体验。</div>';
      const link = container.querySelector('[data-switch-tree]');
      if (link && onSwitchTree) link.onclick = function (event) { event.preventDefault(); onSwitchTree(); };
      return;
    }
    renderRowsInto(root, visible, mode, '', false, false);
    if (!container.querySelector('.file-row')) {
      const empty = document.createElement('p'); empty.className = 'empty-state'; empty.textContent = '没有匹配的文档'; container.append(empty);
    }
  }
  window.VexWriting = {metrics, measureText, renderFiles};
})();
