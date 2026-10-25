/* 原型使用离线状态演示；“检查更新”从帮助菜单打开：检查中 → 发现新版 → 下载进度 → 下载完成。
   版本、日期与更新日志全部解析自内置《更新日志》文档（与更新日志.md 同源）。 */
(function () {
  const $ = id => document.getElementById(id);
  let state = 'idle', scenario = 'available', retry = 'checking', timer, progressTimer, generation = 0;
  const failures = new Set();
  const newer = [];               // 检查到的新版本（倒序，含类型化条目）
  const download = { total: 89.7, done: 0 };
  const DOWNLOAD_TICKS = 28;      // 演示用：约 3.4 秒走完进度
  let tick = 0;

  const failureMessages = { 'check-failed':'检查失败，请重试。', 'download-failed':'下载失败，请重试。', 'checksum-failed':'校验失败，请重新下载。', 'install-failed':'安装失败，请重试。' };
  const TAGS = { '新增':'tag-new', '优化':'tag-opt', '修复':'tag-fix', '删除':'tag-remove' };

  function escapeHtml(text) { return text.replace(/[&<>"]/g, ch => ({ '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;' }[ch])); }

  function releaseIndex() {
    const text = (window.VexDocuments && VexDocuments.changelog && VexDocuments.changelog.text) || '';
    const sections = [];
    // 带 /m 的 $ 会匹配到空行的行尾，正文被截成空串；改成显式前置换行 + 串尾判断。
    const pattern = /\n## V(\d+(?:\.\d+)+)（([^）\n]+)）\n([\s\S]*?)(?=\n## V\d+(?:\.\d+)+（|$)/g;
    let match;
    while ((match = pattern.exec('\n' + text))) sections.push({ version: match[1], date: match[2].trim(), body: match[3].trim() });
    return sections;
  }
  function compareVersion(a, b) {
    const left = a.split('.').map(Number), right = b.split('.').map(Number);
    for (let i = 0; i < 3; i++) if ((left[i] || 0) !== (right[i] || 0)) return (left[i] || 0) - (right[i] || 0);
    return 0;
  }
  function currentVersion() { return (window.VexProject ? VexProject.version : '').split('+')[0]; }
  function releaseDate(version) {
    const found = releaseIndex().find(item => item.version === version);
    return found ? found.date : '';
  }
  // 把一版更新日志解析为类型化条目：😄[新增] → 新增，🔨[优化] → 优化，🐛[修复] → 修复。
  function typedEntries(body) {
    return body.split(/\r?\n/).map(line => line.trim()).filter(Boolean).map(line => {
      const m = /^- (?:😄|🔨|🐛|❌)\[(新增|优化|修复|删除)\]-(.*)$/.exec(line);
      return m ? { tag: m[1], text: m[2] } : { tag: null, text: line.replace(/^- /, '') };
    });
  }

  function renderChanges() {
    const list = $('update-list');
    list.innerHTML = newer.map(item => {
      const header = newer.length > 1
        ? '<div class="update-version"><strong>V' + item.version + '</strong><span class="update-date">' + item.date + ' 发布</span></div>'
        : '';
      const entries = typedEntries(item.body).map(entry =>
        '<li>' + (entry.tag ? '<span class="update-tag ' + TAGS[entry.tag] + '">' + entry.tag + '</span>' : '') + '<span>' + escapeHtml(entry.text) + '</span></li>'
      ).join('');
      return header + '<ul class="update-entries">' + entries + '</ul>';
    }).join('');
  }

  function icon(name) { $('update-icon').innerHTML = '<svg class="ico"><use href="#i-' + name + '"/></svg>'; }
  function setHero(title, sub, name, spinning) {
    $('update-phase-title').textContent = title;
    $('update-phase-sub').textContent = sub;
    $('update-phase-sub').hidden = !sub;
    icon(name);
    $('update-icon').classList.toggle('spinning', !!spinning);
  }
  function setActions(buttons) {
    const row = $('update-actions');
    row.replaceChildren();
    buttons.forEach(spec => {
      const button = document.createElement('button');
      button.className = 'btn sm' + (spec.primary ? ' primary' : '');
      button.textContent = spec.label;
      button.onclick = spec.onClick;
      row.appendChild(button);
    });
  }
  function setProgress(ratio) {
    $('update-progress').hidden = ratio == null;
    if (ratio == null) return;
    const percent = Math.min(100, Math.round(ratio * 100));
    $('update-progress-bar').style.width = percent + '%';
    $('update-progress-num').textContent = percent + '%';
  }
  function showChanges(open) {
    $('update-changes').hidden = newer.length === 0;
    $('update-changes').open = !!open;
  }

  function render() {
    const current = currentVersion();
    const latest = newer.length ? newer[0] : null;
    switch (state) {
      case 'checking':
        setHero('正在检查更新...', '正在连接服务器，请稍候...', 'refresh', true);
        setActions([]);
        setProgress(null); showChanges(false);
        break;
      case 'current':
        setHero('已是最新版本', '当前版本 ' + current + '（' + releaseDate(current) + ' 发布）', 'check', false);
        setActions([{ label: '检查更新', onClick: () => operation('checking') }]);
        setProgress(null); showChanges(false);
        break;
      case 'available': {
        setHero('发现新版本 ' + latest.version,
          latest.date + ' 发布' + (newer.length > 1 ? ' · 共 ' + newer.length + ' 个版本' : ''),
          'up', false);
        setActions([{ label: '下载更新', primary: true, onClick: () => operation('downloading') }]);
        setProgress(null); showChanges(false);
        break;
      }
      case 'downloading':
        setHero('正在下载更新 ' + latest.version, '', 'download', false);
        setActions([{ label: '取消', onClick: cancelDownload }]);
        break;
      case 'downloaded':
        setHero('下载完成', '新版本 ' + latest.version + ' 已通过校验，可以安装。', 'check', false);
        setActions([
          { label: '稍后安装', onClick: () => { closeLayer(); } },
          { label: '立即安装', primary: true, onClick: () => operation('installing') },
        ]);
        setProgress(null); showChanges(true);
        break;
      case 'installing':
        setHero('正在启动安装程序…', '更新包已下载并校验完成。启动后 Vex 将退出，安装将在独立安装程序中继续。', 'refresh', true);
        setActions([]);
        break;
      case 'installed':
        setHero('更新已安装', '重启应用后生效。', 'check', false);
        setActions([{ label: '完成', onClick: () => { closeLayer(); } }]);
        setProgress(null); showChanges(false);
        break;
      case 'failed':
        setHero(failureMessages[scenario] || '检查失败，请重试。', '', 'refresh', false);
        setActions([{ label: '重试', primary: true, onClick: () => operation(retry) }]);
        setProgress(null); showChanges(false);
        break;
      case 'canceled':
        setHero('已取消', '可随时重新检查更新。', 'up', false);
        setActions([{ label: '检查更新', onClick: () => operation('checking') }]);
        setProgress(null); showChanges(false);
        break;
    }
  }

  function closeLayer() { $('layer-update').hidden = true; }

  // 更新交接后继续在当前标签页演示对应平台的安装流程。
  function showInstallerHandoff() {
    const query = new URLSearchParams(location.search);
    const installers = { windows:'Vex-Installer-Windows.html', macos:'Vex-Installer-macOS.html', linux:'Vex-Installer-Linux.html' };
    const target = new URL(installers[query.get('platform')] || installers.windows, location.href);
    ['theme','palette','lang'].forEach(key => { const value = query.get(key); if (value) target.searchParams.set(key, value); });
    location.assign(target.href);
  }
  function cancelDownload() {
    generation++; clearTimeout(timer); clearInterval(progressTimer);
    state = 'canceled';
    render();
  }

  function check() {
    state = 'checking'; render();
    const id = ++generation;
    timer = setTimeout(() => {
      if (id !== generation) return;
      if (scenario === 'check-failed' && !failures.has(scenario)) { failures.add(scenario); state = 'failed'; render(); return; }
      if (scenario === 'current') {
        newer.length = 0;
        state = 'current'; render();
        return;
      }
      if (scenario === 'missing') { state = 'manual'; render(); window.open('https://github.com/dotnet9/Vex/releases/latest', '_blank', 'noopener,noreferrer'); return; }
      const current = currentVersion();
      newer.length = 0;
      releaseIndex().forEach(item => { if (compareVersion(item.version, current) > 0) newer.push(item); });
      newer.sort((a, b) => compareVersion(b.version, a.version));
      // 演示环境当前版本往往就是日志里最新的一版：合成一条示例新版，
      // 展示跨版本升级时“列出之间所有版本”的完整形态。
      if (!newer.length) {
        newer.push({
          version: '99.0.0',
          date: new Date().toISOString().slice(0, 10),
          body: '- 😄[新增]-示例更新：跨版本升级时，这里列出当前版本之后的所有版本与更新日志。\n- 🔨[优化]-日志使用与正文一致的类型化标签：新增、优化、修复。',
        });
      }
      renderChanges();
      state = 'available'; render();
    }, 250);
  }

  function startDownload() {
    state = 'downloading';
    download.done = 0; tick = 0;
    render();
    setProgress(0);
    const id = ++generation;
    progressTimer = setInterval(() => {
      if (id !== generation) { clearInterval(progressTimer); return; }
      tick++;
      download.done = Math.min(download.total, download.total * tick / DOWNLOAD_TICKS);
      const ratio = download.done / download.total;
      const minutes = Math.max(1, Math.round((DOWNLOAD_TICKS - tick) * 0.12 * 10) / 10);
      $('update-phase-sub').textContent = '已下载 ' + download.done.toFixed(1) + ' MB / ' + download.total.toFixed(1) + ' MB · 约 ' + minutes + ' 分钟';
      $('update-phase-sub').hidden = false;
      setProgress(ratio);
      if (scenario === 'download-failed' && tick === 4 && !failures.has(scenario)) { clearInterval(progressTimer); failures.add(scenario); state = 'failed'; render(); setProgress(null); return; }
      if (scenario === 'checksum-failed' && tick === DOWNLOAD_TICKS && !failures.has(scenario)) { clearInterval(progressTimer); failures.add(scenario); state = 'failed'; render(); setProgress(null); return; }
      if (tick >= DOWNLOAD_TICKS) {
        clearInterval(progressTimer);
        state = 'downloaded'; render();
      }
    }, 120);
  }

  function operation(next) {
    retry = next;
    if (next === 'checking') { check(); return; }
    if (next === 'downloading') { startDownload(); return; }
    if (next === 'installing') {
      state = 'installing'; render();
      const id = ++generation;
      timer = setTimeout(() => {
        if (id !== generation) return;
        if (scenario === 'install-failed' && !failures.has(scenario)) {
          failures.add(scenario);
          state = 'failed';
          render();
          return;
        }
        showInstallerHandoff();
      }, 500);
    }
  }

  $('layer-update').querySelector('[data-close]').addEventListener('click', () => { generation++; clearTimeout(timer); clearInterval(progressTimer); });
  new MutationObserver(() => {
    if ($('layer-update').hidden) { generation++; clearTimeout(timer); clearInterval(progressTimer); }
    else { newer.length = 0; check(); }
  }).observe($('layer-update'), {attributes:true,attributeFilter:['hidden']});
  window.addEventListener('vex-language-changed', render);
  // 自动化验证可选择失败场景；演示开关不进入产品界面。
  window.VexUpdate = { setScenario(value) {
    if (!['available','current','missing','check-failed','download-failed','checksum-failed','install-failed'].includes(value)) return;
    generation++; clearTimeout(timer); clearInterval(progressTimer);
    scenario = value; state = 'idle'; failures.clear(); newer.length = 0;
  } };
})();
