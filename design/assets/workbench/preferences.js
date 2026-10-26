/* Shared preference surface. MCP states are isolated, local prototype scenarios. */
(() => {
  'use strict';
  const esc = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const query = new URLSearchParams(location.search), isolated = query.get('preview') === '1';
  const defaults = {language:'zh-CN', startup:'empty', autoSave:false, saveDelay:2, recovery:true, encoding:'UTF-8', newline:'system', size:16, font:'system', codeSize:13, indent:2, autoPair:true, codeWrap:false, lineNumbers:true, focus:false, typewriter:false, math:true, chemistry:true, mermaid:true, subscript:true, highlight:true, autoLink:true, softBreak:true, imageCopy:true, imageRelative:true, imageFolder:'assets', theme:'light', palette:'indigo', typography:'Simple', compact:false, zoom:100, wordCountVisible:true, motion:true, toolbarPin:true, exportFormat:'html', exportFolder:'文档所在目录', exportOpen:true, pdfSize:'A4', pdfMargin:20, htmlStyle:true, mcpEnabled:false, mcpPort:17891, mcpToken:'vx_preview_local_token_for_demo', mcpScope:'document', mcpFolder:'', mcpConfirm:true};
  Object.assign(defaults,{watermarkPreview:false,watermarkExport:false,watermarkText:'维刻 Vex',watermarkOpacity:12,watermarkFontSize:21,watermarkColor:'#24766B',watermarkWeight:400});
  /* 界面密度与正文字号的真实源在 theme.js（外观菜单同源），这里仅镜像值用于偏好呈现与回放。 */
  Object.assign(defaults,{uiDensity:window.VexTheme ? VexTheme.state.density : 'normal',mdSize:window.VexTheme ? VexTheme.state.mdsize : 'normal'});
  const values = {...defaults};
  for (const key of Object.keys(values)) {
    try {
      const saved = isolated ? null : localStorage.getItem('vex-prototype-pref:' + key);
      if (saved === null) continue;
      const parsed = JSON.parse(saved);
      if (typeof parsed !== typeof defaults[key]) continue;
      if (typeof parsed === 'number' && !Number.isFinite(parsed)) continue;
      values[key] = parsed;
    } catch {}
  }
  if (query.get('child') === '1') values.mcpEnabled = false;
  for (const key of ['theme','palette','typography']) if (query.has(key)) values[key] = query.get(key);
  if (query.has('lang')) values.language = ({en:'en-US',zh:'zh-CN'})[query.get('lang')] || query.get('lang');
  let adapter = {}, host, releaseOverlay, section = 'general', tab = 'server', clientTab = 'http', closeTimer, testTimer, generation = 0;
  let mcpState = 'stopped', mcpMessage = '服务已停止', audit = [], request = null, mcpDraft, mcpSaveTimer, serviceTimer, serviceRevision = 0, mcpError = '';
  const categories = [['general','通用与文件','file'],['editor','编辑器','edit'],['images','图片','image'],['appearance','外观','palette'],['export','导出','export'],['mcp','MCP','terminal']];
  const icon = name => `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><use href="#i-${name}"/></svg>`;
  const control = (key, label, options, note = '') => {
    const value = values[key], prefix = `<label class="vex-pref-row"><span>${label}${note ? `<small>${note}</small>` : ''}</span>`;
    if (!options) return prefix + `<input type="checkbox" role="switch" data-vex-pref="${key}" ${value ? 'checked' : ''}></label>`;
    if (Array.isArray(options)) return prefix + `<select data-vex-pref="${key}">${options.map(item => {const [v,t] = Array.isArray(item) ? item : [item,item]; return `<option value="${esc(v)}" ${String(value) === String(v) ? 'selected' : ''}>${esc(t)}</option>`;}).join('')}</select></label>`;
    if (options.range) return prefix + `<span class="vex-range"><input type="range" data-vex-pref="${key}" data-unit="${options.unit || ''}" min="${options.min}" max="${options.max}" step="${options.step || 1}" value="${value}"><output>${value}${options.unit || ''}</output></span></label>`;
    return prefix + `<input data-vex-pref="${key}" type="${options.type || 'text'}" value="${esc(value)}" ${options.maxlength ? `maxlength="${options.maxlength}"` : ''} ${options.min ? `min="${options.min}" max="${options.max}"` : ''}></label>`;
  };
  const heading = text => `<h4 class="vex-settings-section">${text}</h4>`;
  /* 目录行与桌面版 FolderRow 对齐：输入框 + 「…」浏览按钮。 */
  const folderControl = (key, label) => `<div class="vex-pref-row"><span>${label}</span><span class="vex-pref-folder"><input data-vex-pref="${key}" type="text" aria-label="${esc(label)}" value="${esc(values[key])}"><button type="button" class="vex-icon-button" data-vex-browse="${key}" title="选择目录" aria-label="选择目录">…</button></span></div>`;
  function content() {
    switch (section) {
      case 'general': return control('startup','启动时',[['empty','空白工作区'],['recent','恢复上次工作区']]) + control('autoSave','自动保存') + control('saveDelay','停止输入后保存',[[2,'2 秒'],[5,'5 秒'],[10,'10 秒']]) + control('recovery','保留恢复草稿') + heading('新文档默认值') + control('encoding','编码',['UTF-8','UTF-8 BOM','GB18030']) + control('newline','换行符',[['system','跟随系统'],['LF','LF'],['CRLF','CRLF']]);
      case 'editor': return control('font','编辑区字体',[['system','系统默认'],['serif','系统衬线'],['mono','等宽']]) + control('indent','缩进',[[2,'2 空格'],[4,'4 空格'],[8,'8 空格']]) + heading('代码与源码') + control('codeWrap','代码自动换行') + control('lineNumbers','显示源码行号');
      case 'images': return control('imageCopy','插入时复制图片') + control('imageRelative','使用相对路径') + folderControl('imageFolder','资源目录');
      case 'appearance': return control('motion','界面动效') + heading('文档水印') + control('watermarkText','水印文字',{maxlength:60}) + control('watermarkFontSize','水印字号',{range:true,min:12,max:36,unit:' px'}) + control('watermarkColor','前景色',{type:'color'}) + control('watermarkWeight','粗细',[[300,'细'],[400,'常规'],[700,'粗']]) + control('watermarkOpacity','不透明度',{range:true,min:0,max:50,unit:'%'}) + '<div class="vex-watermark-sample" aria-label="水印样式预览"><strong>写下想法，看见脉络。</strong><span>维刻 Vex</span></div>';
      case 'export': return control('exportOpen','导出后打开') + heading('PDF') + control('pdfSize','纸张',['A4','Letter','A5']) + control('pdfMargin','页边距',{range:true,min:10,max:30,unit:' mm'}) + heading('HTML') + control('htmlStyle','保留当前文档排版');
      case 'mcp': return mcpContent();
      default: return '';
    }
  }
  function palettes() {
    return `<div class="vex-pref-row"><span>界面配色</span><div class="vex-palette-swatches" role="group" aria-label="界面配色">${VexPalettes.map(p => `<button class="vex-swatch" data-vex-palette="${p.key}" aria-label="${p.label}" title="${p.label}" aria-pressed="${values.palette === p.key}" style="--swatch:${p.light}"></button>`).join('')}</div></div>`;
  }
  function draftRow(key, label, type = 'text') {
    const value = mcpDraft[key];
    return `<label class="vex-pref-row"><span>${label}</span><input data-mcp-field="${key}" type="${type}" value="${esc(value)}" ${type === 'number' ? 'min="1024" max="65535"' : ''}></label>`;
  }
  function mcpTabsHtml() {
    return `<div class="vex-mcp-tabs" role="tablist" aria-label="MCP">${[['server','服务'],['clients','客户端'],['audit','审计']].map(([key,label]) => `<button role="tab" data-mcp-tab="${key}" aria-selected="${tab === key}">${label}</button>`).join('')}</div>`;
  }
  function mcpClientPane() {
    const endpoint = `http://127.0.0.1:${values.mcpPort}/mcp/`;
    if (clientTab === 'http') return '<pre class="vex-code" id="vex-client-config">' + esc(JSON.stringify({mcpServers:{vex:{url:endpoint,headers:{Authorization:'Bearer ' + values.mcpToken}}}},null,2)) + '</pre><div class="vex-mcp-actions"><button class="vex-button" data-mcp-action="copy-config">复制配置</button><button class="vex-button primary" data-mcp-action="test">测试连接</button></div><p id="vex-test-result" role="status"></p>';
    return '<pre class="vex-code">' + esc(JSON.stringify({mcpServers:{vex:{command:'vex',args:['mcp','bridge','--endpoint',endpoint],env:{VEX_MCP_TOKEN:values.mcpToken}}}},null,2)) + '</pre><div class="vex-mcp-actions"><button class="vex-button" data-mcp-action="read">读取当前文档</button><button class="vex-button" data-mcp-action="write">请求修改文档</button></div><div id="vex-mcp-request"></div>';
  }
  function mcpBody() {
    const status = `<div class="vex-mcp-status" data-state="${mcpState}" role="status"><i></i><span>${esc(mcpMessage)}</span><small style="margin-left:auto;color:var(--muted)">原型演示</small></div>`;
    if (tab === 'server') return `<label class="vex-pref-row"><span>启用 MCP 服务<small>仅监听 127.0.0.1</small></span><input type="checkbox" role="switch" data-mcp-field="mcpEnabled" ${mcpDraft.mcpEnabled ? 'checked' : ''}></label>` + status + draftRow('mcpPort','端口','number') + `<div class="vex-pref-row"><span>访问令牌</span><span class="vex-token-field"><input data-mcp-field="mcpToken" aria-label="访问令牌" type="password" value="${esc(mcpDraft.mcpToken)}"><span class="vex-token-actions"><button class="vex-icon-button" data-mcp-action="reveal" title="显示或隐藏令牌" aria-label="显示或隐藏令牌" aria-pressed="false">${icon('eye')}</button><button class="vex-icon-button" data-mcp-action="token" title="重新生成令牌" aria-label="重新生成令牌">${icon('refresh')}</button></span></span></div>` + `<label class="vex-pref-row"><span>授权范围</span><select data-mcp-field="mcpScope">${[['document','当前文档'],['folder','当前文件夹'],['custom','指定目录']].map(([v,t])=>`<option value="${v}" ${mcpDraft.mcpScope === v ? 'selected' : ''}>${t}</option>`).join('')}</select></label>` + (mcpDraft.mcpScope === 'custom' ? draftRow('mcpFolder','授权目录') : '') + `<label class="vex-pref-row"><span>修改前确认</span><input type="checkbox" role="switch" data-mcp-field="mcpConfirm" ${mcpDraft.mcpConfirm ? 'checked' : ''}></label><p id="vex-mcp-error" class="vex-field-error" role="alert" ${mcpError ? '' : 'hidden'}>${esc(mcpError)}</p>`;
    if (tab === 'clients') {
      const subtabs = `<div class="vex-mcp-tabs vex-mcp-subtabs" role="tablist" aria-label="客户端接入方式">${[['http','HTTP 客户端配置'],['stdio','stdio 桥接']].map(([key,label]) => `<button role="tab" data-mcp-sub="${key}" aria-selected="${clientTab === key}">${label}</button>`).join('')}</div>`;
      return status + subtabs + '<div class="vex-mcp-subbody">' + mcpClientPane() + '</div>';
    }
    return status + `<table class="vex-audit"><thead><tr><th>时间</th><th>操作</th><th>结果</th></tr></thead><tbody>${audit.length ? audit.map(a => `<tr><td>${a.time}</td><td>${esc(a.action)}</td><td>${esc(a.result)}</td></tr>`).join('') : '<tr><td colspan="3">暂无调用记录</td></tr>'}</tbody></table>`;
  }
  function mcpContent() {
    if (adapter.mobile) return '<p>当前设备不提供常驻 MCP 服务。</p>';
    if (query.get('child') === '1') return '<p>文档窗口不运行 MCP 服务。</p>';
    return mcpTabsHtml() + '<div class="vex-mcp-body">' + mcpBody() + '</div>';
  }
  function render(animate = false) {
    if (!host) return;
    host.querySelectorAll('[data-vex-section]').forEach(b => b.setAttribute('aria-selected',String(b.dataset.vexSection === section)));
    const pane = host.querySelector('.vex-settings-content');
    pane.dataset.section = section;
    pane.setAttribute('aria-label',categories.find(c => c[0] === section)[1]);
    pane.removeAttribute('aria-labelledby');
    pane.innerHTML = content();
    pane.scrollTop = 0;
    if (animate && !matchMedia('(prefers-reduced-motion:reduce)').matches && values.motion) pane.animate?.([{opacity:.4,translate:'0 5px'},{opacity:1,translate:'0 0'}],{duration:180,easing:'cubic-bezier(.2,.8,.2,1)'});
  }
  function set(key, value, persist = true) {
    values[key] = value;
    if (persist && !isolated) try { localStorage.setItem('vex-prototype-pref:' + key,JSON.stringify(value)); } catch {}
    document.documentElement.dataset.motion = String(values.motion);
    document.documentElement.dataset.codeWrap = String(values.codeWrap);
    if (key.startsWith('watermark')) VexWatermark.apply(values);
    adapter.apply?.(key,value,values);
    window.dispatchEvent(new CustomEvent('vex-preference-changed',{detail:{key,value}}));
  }
  function close() {
    if (!host || host.hidden) return;
    host.classList.add('is-closing');
    clearTimeout(closeTimer); generation++; clearTimeout(testTimer); request = null;
    saveMcp();
    closeTimer = setTimeout(() => { host.hidden = true; host.classList.remove('is-closing'); releaseOverlay?.(); releaseOverlay = null; },matchMedia('(prefers-reduced-motion:reduce)').matches || !values.motion ? 0 : 140);
  }
  function open(category = 'general') {
    adapter.beforeOpen?.();
    Object.assign(values,adapter.currentAppearance?.() || {});
    if (!host) {
      host = document.createElement('div'); host.className = 'vex-settings-host'; host.hidden = true;
      host.innerHTML = `<section class="vex-settings" role="dialog" aria-modal="true" aria-labelledby="vex-settings-title"><header class="vex-settings-title"><button class="vex-icon-button" data-vex-back aria-label="返回设置分类" hidden>${icon('arrow-left')}</button><h2 id="vex-settings-title">偏好设置</h2><button class="vex-icon-button" data-vex-close aria-label="关闭偏好设置">${icon('x')}</button></header><div class="vex-settings-layout"><nav class="vex-settings-nav" role="tablist" aria-label="设置分类">${categories.map(([key,label,name]) => `<button role="tab" data-vex-section="${key}" aria-selected="false">${icon(name)}<span>${label}</span></button>`).join('')}</nav><div class="vex-settings-content" role="tabpanel" aria-labelledby="vex-section-title"></div></div></section>`;
      VexOverlay.mount(host);
      host.addEventListener('click',event => {
        if (event.target === host || event.target.closest('[data-vex-close]')) { close(); return; }
        const nav = event.target.closest('[data-vex-section]');
        if (nav) { section = nav.dataset.vexSection; render(true); detail(true); if (smallScreen()) host.querySelector('[data-vex-back]').focus(); return; }
        if (event.target.closest('[data-vex-back]')) { detail(false); host.querySelector('.vex-settings-nav [aria-selected=true]').focus(); return; }
        const swatch = event.target.closest('[data-vex-palette]'); if (swatch) { set('palette',swatch.dataset.vexPalette); render(); return; }
        const browse = event.target.closest('[data-vex-browse]'); if (browse) { pickFolder(browse.dataset.vexBrowse); return; }
        const sub = event.target.closest('[data-mcp-sub]');
        if (sub) {
          /* 与一级页签同理：页签条保持稳定，只换内容体，避免整块重渲染抖动。 */
          clientTab = sub.dataset.mcpSub;
          host.querySelectorAll('.vex-mcp-subtabs [data-mcp-sub]').forEach(b => b.setAttribute('aria-selected', String(b.dataset.mcpSub === clientTab)));
          const body = host.querySelector('.vex-mcp-subbody');
          if (body) body.innerHTML = mcpClientPane();
          return;
        }
        const t = event.target.closest('[data-mcp-tab]');
        if (t) {
          /* 页签切换只更新内容体，页签条保持稳定，避免整块重渲染造成的抖动。 */
          tab = t.dataset.mcpTab;
          host.querySelectorAll('.vex-mcp-tabs [data-mcp-tab]').forEach(b => b.setAttribute('aria-selected', String(b.dataset.mcpTab === tab)));
          const body = host.querySelector('.vex-mcp-body');
          if (body) body.innerHTML = mcpBody();
          return;
        }
        const action = event.target.closest('[data-mcp-action]'); if (action) mcpAction(action.dataset.mcpAction);
      });
      host.addEventListener('input',event => {
        const el = event.target;
        if (el.dataset.vexPref && el.type === 'range') { const key = el.dataset.vexPref; set(key,Number(el.value)); el.nextElementSibling.value = el.value + el.dataset.unit; }
        if (el.dataset.vexPref && ['text','number'].includes(el.type)) {
          const key = el.dataset.vexPref;
          set(key,typeof defaults[key] === 'number' ? Number(el.value) : el.value);
        }
        if (el.dataset.mcpField && !['checkbox','select-one'].includes(el.type)) {
          mcpDraft[el.dataset.mcpField] = el.value;
          clearTimeout(mcpSaveTimer);
          mcpSaveTimer = setTimeout(saveMcp,400);
        }
      });
      host.addEventListener('change',event => {
        const el = event.target;
        if (el.dataset.vexPref) { const key = el.dataset.vexPref; set(key,el.type === 'checkbox' ? el.checked : typeof defaults[key] === 'number' ? Number(el.value) : el.value); }
        if (el.dataset.mcpField) { mcpDraft[el.dataset.mcpField] = el.type === 'checkbox' ? el.checked : el.value; saveMcp(); if (el.dataset.mcpField === 'mcpScope') render(); }
      });
    }
    section = categories.some(c => c[0] === category) ? category : 'general';
    mcpDraft = {...values}; clearTimeout(closeTimer); host.classList.remove('is-closing'); host.hidden = false;
    host.dataset.small = String(smallScreen());
    if (!releaseOverlay) releaseOverlay = VexOverlay.lock(host);
    detail(!smallScreen() || category !== 'general');
    render();
    var opener = Array.from(host.querySelectorAll('button:not([disabled]), input, select')).find(function (el) { return !el.closest('[inert]') && !el.matches('[data-vex-close], [data-vex-back]'); });
    (opener || host.querySelector('[data-vex-close]')).focus();
  }
  function smallScreen() { return VexOverlay.width() <= 650 || adapter.phone; }
  function detail(show) {
    const small = smallScreen();
    host.querySelector('.vex-settings').dataset.detail = String(show);
    host.querySelector('#vex-settings-title').textContent = small && show ? categories.find(c => c[0] === section)[1] : '偏好设置';
    host.querySelector('[data-vex-back]').hidden = !small || !show;
    host.querySelector('.vex-settings-nav').inert = small && show;
    host.querySelector('.vex-settings-content').inert = small && !show;
  }
  window.addEventListener('resize',() => {
    if (!host || host.hidden) return;
    host.dataset.small = String(smallScreen());
    detail(host.querySelector('.vex-settings').dataset.detail === 'true');
  });
  function record(action, result) { audit.unshift({time:new Date().toLocaleTimeString('zh-CN',{hour12:false}),action,result}); }
  function refreshMcpStatus() {
    const status = host?.querySelector('.vex-mcp-status');
    if (status) { status.dataset.state = mcpState; status.querySelector('span').textContent = mcpMessage; }
    const toggle = host?.querySelector('[data-mcp-field=mcpEnabled]');
    if (toggle) toggle.checked = mcpDraft.mcpEnabled;
    const error = host?.querySelector('#vex-mcp-error');
    if (error) { error.hidden = !mcpError; error.textContent = mcpError; }
  }
  function saveMcp() {
    clearTimeout(mcpSaveTimer);
    if (!mcpDraft || adapter.mobile || query.get('child') === '1') return;
    const port = Number(mcpDraft.mcpPort);
    mcpError = !Number.isInteger(port) || port < 1024 || port > 65535 ? '端口须为 1024–65535 之间的整数'
      : mcpDraft.mcpToken.trim().length < 24 ? '访问令牌至少需要 24 个字符'
      : mcpDraft.mcpScope === 'custom' && !/^(?:[a-z]:[\\/]|\/)/i.test(mcpDraft.mcpFolder.trim()) ? '请输入授权目录的绝对路径' : '';
    // Stopping remains available even while a different field is invalid.
    if (!mcpDraft.mcpEnabled) {
      serviceRevision++; clearTimeout(serviceTimer);
      if (mcpState !== 'stopped') record('停止服务','完成');
      mcpState = 'stopped'; mcpMessage = '服务已停止'; set('mcpEnabled',false);
    }
    if (mcpError) {
      mcpDraft.mcpEnabled = values.mcpEnabled;
      refreshMcpStatus(); return;
    }
    const restart = port !== values.mcpPort || mcpDraft.mcpToken.trim() !== values.mcpToken;
    for (const key of Object.keys(defaults).filter(k => k.startsWith('mcp'))) {
      const value = key === 'mcpPort' ? port : key === 'mcpToken' || key === 'mcpFolder' ? mcpDraft[key].trim() : mcpDraft[key];
      set(key,value);
    }
    if (values.mcpEnabled && (restart || !['running','starting'].includes(mcpState))) {
      const revision = ++serviceRevision;
      clearTimeout(serviceTimer);
      mcpState = 'starting'; mcpMessage = '正在启动…';
      serviceTimer = setTimeout(() => {
        if (revision !== serviceRevision) return;
        const busy = query.get('mcpScenario') === 'port-busy';
        mcpState = busy ? 'error' : 'running';
        mcpMessage = busy ? '启动失败：端口已被占用，请更换端口' : `运行中 · http://127.0.0.1:${values.mcpPort}/mcp/`;
        if (busy) { mcpDraft.mcpEnabled = false; set('mcpEnabled',false); }
        record('启动服务',busy ? '端口占用' : '成功'); refreshMcpStatus();
      },400);
    }
    refreshMcpStatus();
  }
  /* 浏览器只能取到目录名、取不到绝对路径，所以写回名称并说明桌面版的差异，不假装读到了完整路径。 */
  async function pickFolder(key) {
    if (!window.showDirectoryPicker) { adapter.toast?.('当前浏览器不支持目录选择，请直接输入路径'); return; }
    let handle;
    try { handle = await window.showDirectoryPicker(); }
    catch (error) { if (error?.name !== 'AbortError') adapter.toast?.('未能读取所选目录'); return; }
    set(key, handle.name);
    const field = host.querySelector(`[data-vex-pref="${key}"]`); if (field) field.value = values[key];
    adapter.toast?.(`已选择目录「${handle.name}」，桌面版会写入完整路径`);
  }
  async function copy(text) { try { await navigator.clipboard.writeText(text); adapter.toast?.('已复制到剪贴板'); } catch { adapter.toast?.('复制不可用，请选择配置文本复制'); } }
  function mcpAction(action) {
    if (adapter.mobile || query.get('child') === '1') return;
    if (action === 'reveal') { const input = host.querySelector('[data-mcp-field=mcpToken]'); input.type = input.type === 'password' ? 'text' : 'password'; host.querySelector('[data-mcp-action=reveal]').setAttribute('aria-pressed',String(input.type === 'text')); return; }
    if (action === 'token') { mcpDraft.mcpToken = 'vx_' + crypto.randomUUID().replaceAll('-',''); saveMcp(); render(); return; }
    if (action === 'copy-config') { copy(host.querySelector('#vex-client-config').textContent); return; }
    if (action === 'test') {
      const output = host.querySelector('#vex-test-result'); output.textContent = '正在测试连接…';
      generation++; clearTimeout(testTimer); const revision = generation;
      testTimer = setTimeout(() => { if (revision !== generation || !output.isConnected) return; const scenario = query.get('mcpScenario'); const result = mcpState !== 'running' ? '连接失败：服务未启动' : scenario === 'unauthorized' ? '连接失败：令牌验证失败' : scenario === 'timeout' ? '连接超时，请重试' : '连接成功 · 已完成初始化'; output.textContent = result; record('测试连接',result); },500); return;
    }
    if (action === 'read' || action === 'write') {
      const area = host.querySelector('#vex-mcp-request');
      if (mcpState !== 'running') { if (area) area.innerHTML = '<p class="vex-field-error">请先启动服务</p>'; return; }
      if (action === 'read') { area.innerHTML = '<pre class="vex-code">' + esc((adapter.getText?.() || '# 当前文档').slice(0,700)) + '</pre>'; record('读取当前文档','成功'); return; }
      request = {text:adapter.getText?.() || ''};
      area.innerHTML = '<div class="vex-mcp-request"><strong>客户端请求修改当前文档</strong><p>在文档末尾追加一个段落。</p><div class="vex-mcp-actions"><button class="vex-button" data-mcp-action="reject">拒绝</button><button class="vex-button primary" data-mcp-action="approve">允许本次修改</button></div></div>';
      if (!values.mcpConfirm) mcpAction('approve'); return;
    }
    if (action === 'approve' || action === 'reject') {
      const area = host.querySelector('#vex-mcp-request');
      let result = '已拒绝';
      if (action === 'approve' && request) { const current = adapter.getText?.() || ''; if (current !== request.text) result = '文档已变更，请重新请求'; else {adapter.replaceText?.(current + '\n\n通过 MCP 追加的演示段落。\n'); result = '修改已完成，可撤销';} }
      area.textContent = result; record('修改当前文档',result); request = null;
    }
  }
  window.addEventListener('storage',event => { if (!event.key?.startsWith('vex-prototype-pref:')) return; const key = event.key.slice('vex-prototype-pref:'.length); if (!(key in defaults) || (query.get('child') === '1' && key.startsWith('mcp'))) return; try {set(key,event.newValue === null ? defaults[key] : JSON.parse(event.newValue),false);} catch {} });
  document.addEventListener('keydown',event => {
    if ((event.ctrlKey || event.metaKey) && event.key === ',' && !window.VexPresentation?.active) { event.preventDefault(); event.stopImmediatePropagation(); open(); return; }
    if (!host || host.hidden || event.isComposing) return;
    if (event.key === 'Escape') { event.preventDefault(); event.stopImmediatePropagation(); close(); }
    if (event.key === 'Tab') { const items = [...host.querySelectorAll('button,input,select')].filter(el => !el.disabled && !el.closest('[inert]') && el.getClientRects().length); const first = items[0], last = items.at(-1); if (event.shiftKey && document.activeElement === first) {event.preventDefault();last?.focus();} else if (!event.shiftKey && document.activeElement === last) {event.preventDefault();first?.focus();} }
    if (event.ctrlKey || event.metaKey) event.stopImmediatePropagation();
  },true);
  window.VexPreferences = {values,open,close,set,configure(value) { adapter = value; for (const [key,value] of Object.entries(values)) set(key,value,false); if (values.mcpEnabled) { mcpDraft = {...values}; saveMcp(); } }};
})();
