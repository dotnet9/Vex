/* 原型使用离线状态演示；“检查更新”从帮助菜单打开：检查中 → 发现新版 → 下载进度 → 下载完成。
   版本、日期与更新日志全部解析自内置《更新日志》文档（与更新日志.md 同源）。 */
(function () {
  const $ = id => document.getElementById(id);
  let state = 'idle', scenario = 'available', retry = 'checking', timer, progressTimer, generation = 0;
  let lastRatio = null; // 供标题栏药丸同步下载进度
  const failures = new Set();
  const newer = [];               // 检查到的新版本（倒序，含类型化条目）
  const download = { total: 89.7, done: 0 };
  const DOWNLOAD_TICKS = 28;      // 演示用：约 3.4 秒走完进度
  let tick = 0;

  const failureMessages = { 'check-failed':'检查失败，请重试。', 'download-failed':'下载失败，请重试。', 'checksum-failed':'校验失败，请重新下载。', 'install-failed':'安装失败，请重试。' };
  const TAGS = { '新增':'tag-new', '优化':'tag-opt', '修复':'tag-fix', '删除':'tag-remove' };

  function VexEsc(text) { return text.replace(/[&<>"]/g, ch => ({ '&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;' }[ch])); }

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
  // 把一版更新日志解析为类型化条目：✨[新增] → 新增，⚡[优化] → 优化，🐛[修复] → 修复，❌[删除] → 删除。
  // 表情符号只是视觉前缀，标签词才是依据，所以前缀按「非 [ 的任何字符」匹配：《更新日志》换表情不必改这里，
  // 旧记录里的 😄/🔨 前缀也照样能解析。没有 [标签] 的行原样显示，不硬凑一个胶囊。
  function typedEntries(body) {
    return body.split(/\r?\n/).map(line => line.trim()).filter(Boolean).map(line => {
      const m = /^- (?:[^\[]*)\[(新增|优化|修复|删除)\]-(.*)$/.exec(line);
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
        '<li>' + (entry.tag ? '<span class="update-tag ' + TAGS[entry.tag] + '">' + entry.tag + '</span>' : '') + '<span>' + VexEsc(entry.text) + '</span></li>'
      ).join('');
      return header + '<ul class="update-entries">' + entries + '</ul>';
    }).join('');
  }

  function setHero(title, sub) {
    $('update-phase-title').textContent = title;
    $('update-phase-sub').textContent = sub;
    $('update-phase-sub').hidden = !sub;
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
    lastRatio = ratio;
    $('update-progress').hidden = ratio == null;
    pillSync();
    if (ratio == null) return;
    const percent = Math.min(100, Math.round(ratio * 100));
    $('update-progress-bar').style.transform = 'scaleX(' + (percent / 100) + ')';
    $('update-progress-num').textContent = percent + '%';
  }
  function render() {
    const current = currentVersion();
    const latest = newer.length ? newer[0] : null;
    switch (state) {
      case 'checking':
        setHero('正在检查更新...', '正在连接服务器，请稍候...');
        setActions([]);
        setProgress(null);
        break;
      case 'current':
        setHero('已是最新版本', '当前版本 ' + current + '（' + releaseDate(current) + ' 发布）');
        setActions([{ label: '检查更新', onClick: () => operation('checking') }]);
        setProgress(null);
        break;
      case 'available':
        setHero('Vex最新' + latest.version + '版本来了！', '您目前使用版本为' + current + '版本，新版本更新日志如下：');
        setActions([
          { label: '稍后升级', onClick: closeLayer },
          { label: '立即升级', primary: true, onClick: () => operation('downloading') },
        ]);
        setProgress(null);
        break;
      case 'downloading':
        setHero('正在下载更新 ' + latest.version, '');
        setActions([{ label: '取消', onClick: cancelDownload }]);
        break;
      case 'downloaded':
        setHero('下载完成', '新版本 ' + latest.version + ' 已通过校验，可以安装。');
        setActions([
          { label: '稍后升级', onClick: () => { closeLayer(); } },
          { label: '立即安装', primary: true, onClick: () => operation('installing') },
        ]);
        setProgress(null);
        break;
      case 'installing':
        setHero('正在启动安装程序…', '更新包已下载并校验完成。启动后 Vex 将退出，安装将在独立安装程序中继续。');
        setActions([]);
        break;
      case 'installed':
        setHero('更新已安装', '重启应用后生效。');
        setActions([{ label: '完成', onClick: () => { closeLayer(); } }]);
        setProgress(null);
        break;
      case 'failed':
        setHero(failureMessages[scenario] || '检查失败，请重试。', '');
        setActions([{ label: '重试', primary: true, onClick: () => operation(retry) }]);
        setProgress(null);
        break;
      case 'canceled':
        setHero('已取消', '可随时重新检查更新。');
        setActions([{ label: '检查更新', onClick: () => operation('checking') }]);
        setProgress(null);
        break;
    }
    /* 更新日志常驻展开（不再有折叠开关），有新版就显示在按钮上方。 */
    $('update-changes').hidden = newer.length === 0;
    pillSync();
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

  // 收集比当前版本新的版本（倒序）。演示环境当前版本往往就是日志里最新的一版：
  // 合成一条示例新版，展示跨版本升级时“列出之间所有版本”的完整形态。
  function collectNewer() {
    const current = currentVersion();
    newer.length = 0;
    releaseIndex().forEach(item => { if (compareVersion(item.version, current) > 0) newer.push(item); });
    newer.sort((a, b) => compareVersion(b.version, a.version));
    if (!newer.length) {
      newer.push({
        version: '99.0.0',
        date: new Date().toISOString().slice(0, 10),
        body: '- 😄[新增]-示例更新：跨版本升级时，这里列出当前版本之后的所有版本与更新日志。\n- 🔨[优化]-日志使用与正文一致的类型化标签：新增、优化、修复。',
      });
    }
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
      collectNewer();
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
        if ($('layer-update').hidden && window.VexProto) VexProto.toast('新版本已就绪 · 点击标题栏「重启以更新」');
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

  /* 关闭 / 最小化 / 恢复：下载中隐藏窗体不再终止下载，进度转标题栏药丸。 */
  $('layer-update').querySelector('[data-close]').addEventListener('click', () => {
    if (state === 'downloading') { if (window.VexProto) VexProto.toast('下载继续进行 · 进度见标题栏「更新」按钮'); return; }
    generation++; clearTimeout(timer); clearInterval(progressTimer);
  });
  $('update-minimize').addEventListener('click', () => {
    $('layer-update').hidden = true;
    if (state === 'downloading' && window.VexProto) VexProto.toast('已最小化 · 下载继续，进度见标题栏「更新」按钮');
  });
  new MutationObserver(() => {
    if ($('layer-update').hidden) {
      if (state !== 'downloading') { generation++; clearTimeout(timer); clearInterval(progressTimer); }
    } else if (state === 'downloading' || state === 'downloaded' || state === 'installing') {
      render(); // 后台下载 / 最小化后恢复现场，不重新检查
    } else {
      newer.length = 0; check();
    }
  }).observe($('layer-update'), {attributes:true,attributeFilter:['hidden']});
  window.addEventListener('vex-language-changed', render);
  /* ---------- 标题栏药丸 + 日志气泡（与浮窗同源；下载可后台/最小化） ---------- */
  const pillWrap = $('upd-pill-wrap'), pill = $('upd-pill'), pop = $('upd-pop');
  const PILL_GROUP = { '新增':'g-new', '优化':'g-opt', '修复':'g-fix', '删除':'g-rm' };
  function pillSync() {
    if (!pillWrap) return;
    let show = true, txt = '更新', ready = false;
    if (state === 'downloading') {
      const pct = lastRatio == null ? 0 : Math.min(99, Math.round(lastRatio * 100));
      txt = '下载中 ' + pct + '%';
      pill.style.background = 'linear-gradient(90deg, color-mix(in srgb, var(--accent) 74%, var(--fg)) ' + pct + '%, var(--accent) ' + pct + '%)';
    } else {
      pill.style.background = '';
      if (state === 'downloaded' || state === 'installing') { txt = '重启以更新'; ready = true; }
      else if (state === 'available' || state === 'failed' || state === 'canceled') { txt = '更新'; }
      else if (state === 'checking') { show = newer.length > 0; }
      else show = false; // idle / current / manual / installed
    }
    pillWrap.hidden = !show;
    if (!show) return;
    pill.textContent = txt;
    pill.classList.toggle('ready', ready);
    pill.title = state === 'downloading' ? '正在下载更新，点击恢复窗口'
      : (state === 'downloaded' || state === 'installing') ? '新版本已就绪，点击恢复窗口'
      : '发现新版本' + (newer.length ? ' V' + newer[0].version : '') + '，点击查看更新日志';
  }
  function renderPop() {
    const top = newer[0];
    if (!top) return;
    $('upd-pop-ver').textContent = 'V' + top.version + ' 更新日志';
    $('upd-pop-date').textContent = top.date;
    // [系统要求]/[注意] 行升为引用块，其余按类型化标签分组（色点与 update-tag 同源）
    const quote = [], groups = {};
    newer.forEach(v => typedEntries(v.body).forEach(entry => {
      const m = /^\[(系统要求|注意)\](.*)$/.exec(entry.text);
      if (m) { quote.push('[' + m[1] + ']' + VexEsc(m[2])); return; }
      const key = entry.tag || '其他';
      (groups[key] = groups[key] || []).push(VexEsc(entry.text));
    }));
    let html = quote.length ? '<div class="upd-quote">' + quote.join('<br>') + '</div>' : '';
    Object.keys(groups).forEach(tag => {
      html += '<div class="upd-group ' + (PILL_GROUP[tag] || '') + '"><b>' + VexEsc(tag) + '</b><ul>'
        + groups[tag].map(t => '<li>' + t + '</li>').join('') + '</ul></div>';
    });
    $('upd-pop-body').innerHTML = html;
  }
  pill.addEventListener('click', e => {
    e.stopPropagation();
    if (state === 'downloading' || state === 'downloaded' || state === 'installing') {
      pop.hidden = true; $('layer-update').hidden = false; return; // 恢复浮窗现场（不重新检查）
    }
    if (pop.hidden) renderPop();
    pop.hidden = !pop.hidden;
  });
  pop.addEventListener('click', e => e.stopPropagation());
  document.addEventListener('click', () => { pop.hidden = true; });
  $('upd-pop-go').addEventListener('click', () => {
    pop.hidden = true;
    $('layer-update').hidden = false; // 观察者按当前态分派：后台态恢复现场，否则重新检查
    operation('downloading');         // 日志已在气泡看过，直接进入下载
  });
  $('upd-pop-log').addEventListener('click', () => {
    pop.hidden = true;
    if (window.VexDocumentWindow) VexDocumentWindow.open({ builtin: 'changelog' });
    else if (window.VexProto) VexProto.toast('打开《更新日志》文档');
  });
  /* 启动自动检查（演示）：1.4 秒后发现新版 → 只亮药丸不开浮窗，不打断写作 */
  setTimeout(() => {
    if (scenario === 'current' || scenario === 'missing') return;
    collectNewer();
    if (!newer.length) return;
    renderChanges();
    state = 'available';
    render();
    if (window.VexProto) VexProto.toast('发现新版本 V' + newer[0].version + ' · 点击标题栏「更新」查看日志');
  }, 1400);
  // 自动化验证可选择失败场景；演示开关不进入产品界面。
  window.VexUpdate = { setScenario(value) {
    if (!['available','current','missing','check-failed','download-failed','checksum-failed','install-failed'].includes(value)) return;
    generation++; clearTimeout(timer); clearInterval(progressTimer);
    scenario = value; state = 'idle'; failures.clear(); newer.length = 0; render();
  } };
})();
