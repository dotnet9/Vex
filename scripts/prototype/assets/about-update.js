(function () {
  const $ = id => document.getElementById(id);
  let state = 'idle', retry = 'checking', timer, generation = 0, failures = new Set();
  const labels = { idle:'AboutCheckUpdate', checking:'AboutCheckingUpdate', available:'AboutDownload', downloading:'AboutDownloading', downloaded:'AboutInstall', installing:'AboutInstalling', current:'AboutCheckUpdate', failed:'AboutCheckUpdate', canceled:'AboutCheckUpdate', manual:'AboutOpenRelease', installed:'AboutOpenRelease' };
  function render() {
    const busy = ['checking','downloading','installing'].includes(state);
    $('vex-update').textContent = VexL.get(state === 'failed' ? labels[retry === 'downloading' ? 'available' : retry === 'installing' ? 'downloaded' : 'idle'] : labels[state]);
    $('vex-update').disabled = busy;
    $('vex-update-cancel').hidden = !busy;
    $('vex-update-progress').hidden = state !== 'downloading';
    $('layer-about').dataset.updateState = state;
    const key = {idle:'AboutUpdateHint', checking:'AboutCheckingUpdate', available:'AboutUpdateAvailable', downloading:'AboutDownloading', downloaded:'AboutDownloaded', installing:'AboutInstalling', current:'AboutUpToDate', failed:'AboutUpdateFailed', canceled:'AboutCanceled', manual:'AboutOpenRelease'}[state];
    $('vex-update-status').textContent = state === 'installed' ? VexL.label('安装演示完成') : VexL.get(key).replace('{0}', state === 'available' ? '99.0.0' : VexL.label($('vex-update-scenario').selectedOptions[0].dataset.protoLabel));
  }
  function cancel() { generation++; clearTimeout(timer); if (['checking','downloading','installing'].includes(state)) state = 'canceled'; render(); }
  function operation(next) {
    retry = next; state = next; render(); const id = ++generation;
    timer = setTimeout(() => {
      if (id !== generation) return;
      const scenario = $('vex-update-scenario').value;
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
    else if (['manual','installed'].includes(state)) $('vex-release-link').hidden = false;
    else operation('checking');
  };
  $('vex-update-recheck').onclick = () => operation('checking');
  $('vex-update-cancel').onclick = cancel;
  $('vex-update-scenario').onchange = () => { cancel(); state = 'idle'; failures.clear(); $('vex-release-link').hidden = true; render(); };
  $('layer-about').querySelector('[data-close]').addEventListener('click', cancel);
  new MutationObserver(() => { if ($('layer-about').hidden) cancel(); }).observe($('layer-about'), {attributes:true,attributeFilter:['hidden']});
  window.addEventListener('vex-language-changed', render);
  render();
})();
