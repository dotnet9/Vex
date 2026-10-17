/* 原型使用离线状态演示；页面仅呈现普通用户需要的更新操作。 */
(function () {
  const $ = id => document.getElementById(id);
  let state = 'idle', scenario = 'available', retry = 'checking', timer, generation = 0;
  const failures = new Set();
  const labels = { idle:'AboutCheckUpdate', checking:'AboutCheckingUpdate', available:'AboutDownload', downloading:'AboutDownloading', downloaded:'AboutInstall', installing:'AboutInstalling', current:'AboutCheckUpdate', canceled:'AboutCheckUpdate', manual:'AboutOpenRelease', installed:'AboutCheckUpdate' };
  const failureMessages = { 'check-failed':'检查失败，请重试。', 'download-failed':'下载失败，请重试。', 'checksum-failed':'校验失败，请重新下载。', 'install-failed':'安装失败，请重试。' };
  function render() {
    $('vex-update').textContent = state === 'failed' ? VexL.label('重试') : VexL.get(labels[state]);
    $('vex-update').disabled = ['checking','downloading','installing'].includes(state);
    $('layer-update').dataset.updateState = state;
    $('vex-update-cancel').hidden = !['checking','downloading','installing'].includes(state);
    const key = { checking:'AboutCheckingUpdate', available:'AboutUpdateAvailable', downloading:'AboutDownloading', downloaded:'AboutDownloaded', installing:'AboutInstalling', current:'AboutUpToDate', canceled:'AboutCanceled' }[state];
    const message = state === 'failed' ? VexL.label(failureMessages[scenario]) : state === 'installed' ? VexL.label('更新已安装。') : state === 'manual' ? VexL.label('请前往发布页下载安装包。') : key ? VexL.get(key).replace('{0}', '99.0.0') : '';
    $('vex-update-status').textContent = message;
    $('vex-update-status').hidden = !message;
  }
  function cancel() {
    generation++; clearTimeout(timer);
    if (['checking','downloading','installing'].includes(state)) state = 'canceled';
    render();
  }
  function operation(next) {
    retry = next; state = next; render(); const id = ++generation;
    timer = setTimeout(() => {
      if (id !== generation) return;
      const shouldFail = next === 'checking' && scenario === 'check-failed' || next === 'downloading' && ['download-failed','checksum-failed'].includes(scenario) || next === 'installing' && scenario === 'install-failed';
      if (shouldFail && !failures.has(scenario)) { failures.add(scenario); state = 'failed'; }
      else state = next === 'checking' ? scenario === 'current' ? 'current' : scenario === 'missing' ? 'manual' : 'available' : next === 'downloading' ? 'downloaded' : 'installed';
      render();
    }, 250);
  }
  $('vex-update').onclick = () => {
    if (state === 'failed') operation(retry);
    else if (state === 'available') operation('downloading');
    else if (state === 'downloaded') operation('installing');
    else if (state === 'manual') window.open('https://github.com/dotnet9/Vex/releases/latest', '_blank', 'noopener,noreferrer');
    else operation('checking');
  };
  $('vex-update-cancel').onclick = cancel;
  new MutationObserver(() => { if (!$('layer-update').hidden && state === 'idle') operation('checking'); }).observe($('layer-update'), {attributes:true,attributeFilter:['hidden']});
  window.addEventListener('vex-language-changed', render);
  // 自动化验证可选择失败场景；演示开关不进入产品界面。
  window.VexUpdate = { setScenario(value) {
    if (!['available','current','missing','check-failed','download-failed','checksum-failed','install-failed'].includes(value)) return;
    cancel(); scenario = value; state = 'idle'; failures.clear(); render();
    if (!$('layer-update').hidden) operation('checking');
  } };
  render();
})();
