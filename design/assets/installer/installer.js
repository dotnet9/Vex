(() => {
 'use strict';
 const params=new URLSearchParams(location.search),platform=document.body.dataset.platform,host=document.getElementById('installer-root'),modalHost=document.getElementById('system-modal');
 const device='desktop';
 const esc=s=>String(s).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
 const version=window.VexProject.version;
 /* 维护页要区分“升级到新版本”和“修复当前版本”：?installed=1.9.0 表示本机已装版本，缺省视为与安装包相同；?installedOn 是这台机器上装好它的日期。 */
 const installedVersion=params.get('installed')??version;
 const knownInstalled=installedVersion!=='';
 const installedOn=params.get('installedOn')||'2026-10-20';
 const releaseDate=window.VexProject.releaseDate||'';
 const segments=s=>String(s).split(/[.\-_+]/).map(Number).filter(Number.isFinite);
 const compareVersions=(a,b)=>{const x=segments(a),y=segments(b);for(let i=0;i<Math.max(x.length,y.length);i++){const d=(x[i]||0)-(y[i]||0);if(d)return d;}return 0;};
 const canUpgrade=knownInstalled&&compareVersions(installedVersion,version)<0;
 /* 右下角那颗按钮就是「执行所选操作」：卸载单选给「卸载」，其余按版本关系给「升级」或「修复安装」。 */
 const maintenanceLabel=()=>maintenanceChoice==='remove'?'卸载':canUpgrade?'升级':'修复安装';
 const theme=params.get('theme')||'light';document.documentElement.dataset.theme=theme;document.body.classList.toggle('dark',theme==='dark');
 let state=['installed','done','installing'].includes(params.get('scenario'))?params.get('scenario'):'ready',progress=state==='installing'?Math.max(0,Math.min(100,params.has('progress')?Number(params.get('progress'))||0:46)):0,timer=null,operation='install',fail=params.get('scenario')==='error',directory='C:\\Users\\你\\AppData\\Local\\Programs\\Vex',custom=false,shortcut=true,association=true,maintenanceDetails=false,maintenanceChoice='repair',changelogOpen=false,changelogOlder=false;
 const img='<img src="assets/vex-logo.svg" alt="维刻 Vex">',btn=(label,act,primary=false,other='')=>`<button class="btn ${primary?'primary':'quiet'}" data-action="${act}" ${other}>${label}</button>`;
 const chevron=open=>`<svg class="install-chevron${open?' open':''}" xmlns="http://www.w3.org/2000/svg" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg>`;
 /* 安装包自带的那份更新日志就是右侧一栏的内容：只列比本机版本新的小节，版本相同时给本版本自己。 */
 const CHANGELOG_TAGS={'新增':'new','优化':'opt','修复':'fix','删除':'remove'};
 function changelogReleases(){
   const text=(window.VexDocuments&&window.VexDocuments.changelog?window.VexDocuments.changelog.text:'')||'';
   /* 不用 /m 的 $：它会匹配空行行尾，正文会被截成空串。改成显式的前置换行 + 串尾判断。 */
   const all=[...('\n'+text).matchAll(/\n## V(\d+(?:\.\d+)+)（([^）\n]+)）\n([\s\S]*?)(?=\n## V\d+(?:\.\d+)+（|$)/g)]
     .map(entry=>({version:entry[1],date:entry[2].trim(),body:entry[3].trim()}));
   const newer=all.filter(item=>!knownInstalled||compareVersions(item.version,installedVersion)>0);
   return newer.length?newer:all.filter(item=>item.version===version);
 }
 function changelogPanel(){
   const releases=changelogReleases();
   /* 跨版本升级会列出中间所有版本：默认只展开最近一版，其余收进一颗按钮，免得整栏都在滚动。 */
   const shown=changelogOlder?releases:releases.slice(0,1);
   const more=shown.length<releases.length?`<button class="changelog-more" data-action="changelog-more" aria-expanded="false">查看更早 ${releases.length-shown.length} 个版本${chevron(false)}</button>`:changelogOlder&&releases.length>1?`<button class="changelog-more" data-action="changelog-more" aria-expanded="true">收起更早版本${chevron(true)}</button>`:'';
   const body=shown.length?shown.map(item=>`<section class="changelog-release"><header><strong>V${esc(item.version)}</strong><span>${esc(item.date)}</span></header><ul>${item.body.split(/\r?\n/).map(line=>line.trim()).filter(Boolean).map(line=>{
     const typed=/^- (?:✨|😄|⚡|🔨|🐛|❌)\[(新增|优化|修复|删除)\]-(.*)$/.exec(line);
     return typed?`<li><i class="tag ${CHANGELOG_TAGS[typed[1]]}">${typed[1]}</i><span>${esc(typed[2])}</span></li>`:`<li><span>${esc(line.replace(/^- /,''))}</span></li>`;
   }).join('')}</ul></section>`).join(''):'<p class="changelog-empty">这个安装包没有附带更新日志。</p>';
   return `<aside class="changelog-panel"><header class="changelog-head"><strong>更新日志</strong><span>安装包 ${version}${canUpgrade?` · ${esc(installedVersion)} 之后`:''}</span></header><div class="changelog-body">${body}${more}</div></aside>`;
 }
 const chrome=(title,mac=false)=>`<header class="installer-chrome ${mac?'mac-chrome':''}">${mac?'<div class="traffic-lights"><i></i><i></i><i></i></div>':img}<span>${title}</span>${mac?'':`<button data-action="cancel" aria-label="关闭">×</button>`}</header>`;
 const progressHTML=()=>`<div class="progress-wrap" style="--install-progress:${progress}%"><div class="progress-header"><strong>${progress}%</strong></div><div class="progress-track" role="progressbar" aria-label="${operation==='remove'?'移除进度':operation==='repair'?(canUpgrade?'升级进度':'修复进度'):'安装进度'}" aria-valuenow="${progress}" aria-valuemin="0" aria-valuemax="100"><span style="width:var(--install-progress)"></span></div></div>`;
 const footer=()=>`<footer class="install-footer"><span>CodeWF · ${version}</span><a href="${esc(window.VexProject.website)}" target="_blank" rel="noopener">${esc(new URL(window.VexProject.website).hostname)} ↗</a>${platform==='windows'?btn('关闭','cancel'):''}</footer>`;
 const complete=()=>operation==='remove'?'维刻已移除':'准备好，写下新想法';
 function toast(text){document.querySelector('.installer-toast')?.remove();const el=document.createElement('div');el.className='installer-toast';el.textContent=text;document.body.append(el);setTimeout(()=>el.remove(),3000);}
 function dialog(title,text,buttons,fields=''){modalHost.hidden=false;modalHost.innerHTML=`<section class="system-dialog" role="dialog" aria-modal="true" aria-labelledby="dialog-title">${img}<h2 id="dialog-title">${title}</h2><p>${text}</p>${fields}<div class="dialog-actions">${buttons.map((b,i)=>`<button class="btn ${b.primary?'primary':''} ${b.danger?'danger':''}" data-dialog-button="${i}">${b.label}</button>`).join('')}</div></section>`;modalHost.querySelectorAll('[data-dialog-button]').forEach(el=>el.onclick=()=>buttons[Number(el.dataset.dialogButton)].run());setTimeout(()=>modalHost.querySelector('input,button')?.focus(),0);}
 const closeDialog=()=>{modalHost.hidden=true;modalHost.innerHTML='';};
 function stop(){clearInterval(timer);timer=null;}
 /* 卸载确认只有一处实现：维护行的快捷卸载与右下角跟随单选的按钮走同一段流程。 */
 function confirmRemove(){dialog('移除维刻？','将移除应用程序。独立保存在文件夹中的文档将保留。',[{label:'取消',run:closeDialog},{label:'确认移除',danger:true,run:()=>start('remove')}]);}
 function start(op='install'){
   closeDialog();operation=op;state='installing';progress=0;render();stop();
   timer=setInterval(()=>{
     if(!modalHost.hidden)return;
     progress=Math.min(100,progress+4+Math.floor(Math.random()*6));
     if(fail&&progress>=53){stop();state='error';fail=false;render();return;}
     if(progress===100){stop();state='done';}
     if(state==='installing'){
       const bar=host.querySelector('[role=progressbar]');
       if(bar){
         bar.setAttribute('aria-valuenow',progress);
         bar.parentElement.style.setProperty('--install-progress',progress+'%');
         host.querySelector('.progress-header strong').textContent=progress+'%';
       }
     }else render();
   },260);
 }
 function cancel(){if(state==='installing')dialog('取消这次操作？','当前进度将停止，你可以稍后重新开始。',[{label:'继续',primary:true,run:closeDialog},{label:'取消操作',run:()=>{stop();closeDialog();state='cancelled';render();}}]);else if(state==='ready'||state==='installed')dialog('退出安装？','你可以随时重新打开这个安装包。',[{label:'留下',run:closeDialog},{label:'退出',primary:true,run:()=>{closeDialog();state='cancelled';render();}}]);else{stop();state='cancelled';render();}}
 function openApp(){openDocument();}
 /* 更新日志入口在安装包里也打开程序内视图：与帮助菜单看到的是同一份内置《更新日志》。 */
 function openDocument(name){const url=new URL('Vex.html',location.href);url.searchParams.set('platform',platform);url.searchParams.set('device',device);url.searchParams.set('theme',theme);if(name)url.searchParams.set('document',name);location.href=url.href;}
 function errorBody(){return `<div class="error-symbol">!</div><h2>还差一点，就准备好了</h2><p class="description">应用文件未能完整复制。请检查可用空间与目标位置后重试。</p><div class="information-card">示例错误：目标位置暂时不可写<br>错误代码：VEX-DEMO-102</div><div class="install-actions">${btn('取消','cancel')}${btn('重新尝试','retry',true)}</div>`;}
 function windows() {
   const actions=(primary='',label='安装',secondary='关闭')=>`<div class="install-actions">${state==='ready'?'<a class="license-link" href="#" data-action="license">基于 MIT 协议开源</a>':''}<span class="action-spacer"></span>${secondary?btn(secondary,'cancel'):''}${primary?btn(label,primary,true):''}</div>`;
   let content='';
   if(state==='ready') content=`
     <h2>让想法，慢慢成形。</h2>
     <details class="install-details" ${custom?'open':''}>
       <summary>自定义安装选项<svg class="install-chevron lucide lucide-chevron-down" xmlns="http://www.w3.org/2000/svg" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="m6 9 6 6 6-6"/></svg></summary>
       <div class="detail-body">
         <div class="directory-row"><input class="field" id="install-directory" aria-label="安装位置" value="${esc(directory)}">${btn('浏览…','browse')}</div>
         <label class="option-row"><input type="checkbox" id="shortcut" ${shortcut?'checked':''}>创建桌面快捷方式</label>
         <label class="option-row"><input type="checkbox" id="association" ${association?'checked':''}>注册 Markdown 打开方式候选</label>
       </div>
     </details>${actions('install','立即安装','')}`;
   else if(state==='installing') content=`
     <h2>${operation==='remove'?'正在移除':operation==='repair'?(canUpgrade?'正在升级':'正在修复'):'正在安装'}</h2>
     <p class="description">稍等片刻，即可开始写作。</p>${progressHTML()}
     ${actions('','','取消安装')}`;
   else if(state==='installed') content=`
     <div class="step-caption">已安装 · ${knownInstalled?`${esc(installedVersion)} · 安装于 ${esc(installedOn)}`:'版本未知'}</div>
     <div class="package-heading"><h2>安装包 ${version}</h2><div class="package-meta">${releaseDate?`<span class="release-date">${esc(releaseDate)} 发布</span>`:''}<button class="chip-link${changelogOpen?' on':''}" data-action="changelog" aria-expanded="${changelogOpen}">${changelogOpen?'收起更新日志':'查看更新日志'}</button></div></div>
     ${maintenanceDetails?'':`<div class="information-card">安装位置<br><span class="install-file">${esc(directory)}</span></div>`}
     <div class="maintenance-links">${btn('安装详情'+chevron(maintenanceDetails),'details',false,`aria-expanded="${maintenanceDetails}"`)}</div>
     ${maintenanceDetails?`<div class="maintenance-details">
       <label class="maintenance-option"><input type="radio" name="maintenance-choice" value="repair" ${maintenanceChoice==='repair'?'checked':''}><span>修复 / 重新安装：Vex</span></label>
       <label class="maintenance-option"><input type="radio" name="maintenance-choice" value="remove" ${maintenanceChoice==='remove'?'checked':''}><span>卸载（当前用户）：Vex</span></label>
       <strong class="details-label">所选安装位置</strong>
       <div class="location-list" role="listbox" aria-label="所选安装位置"><span class="location-item selected" role="option" aria-selected="true">${esc(directory)}</span></div>
     </div>`:''}
     <div class="install-actions"><span class="action-spacer"></span>${btn('关闭','cancel')}${btn(maintenanceLabel(),'execute',maintenanceChoice!=='remove')}</div>`;
   else if(state==='error') content=`
     <div class="error-symbol">!</div><h2>安装未完成</h2>
     <p class="description">检查安装位置和可用空间后重试。</p>
     <div class="information-card">示例错误：目标位置暂时不可写</div>${actions('retry','重试')}`;
   else content=`
     <div class="success-symbol">✓</div><h2>${operation==='remove'?'已移除':'一切就绪，<br>让想法开始生长。'}</h2>
     <p class="description">${operation==='remove'?'你的文档保留在原来的位置。':'你的专属 Markdown 空间已准备完毕。'}</p>
     ${operation==='remove'?'':actions('open','写点什么','')}`;
   return `<div class="scene"><section class="install-window windows-minimal">
     <button class="window-close" data-action="cancel" aria-label="关闭" title="关闭"><svg class="lucide lucide-x" xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M18 6 6 18"/><path d="m6 6 12 12"/></svg></button>
     <div class="installer-layout">
       <aside class="brand-panel"><div class="brand-identity"><img class="hero-icon" src="assets/vex-logo.svg" alt=""><h1>维刻 Vex</h1><p>写下想法，看见脉络。</p></div></aside>
       <div class="installer-content">${content}</div>
       ${state==='installed'&&changelogOpen?changelogPanel():''}
     </div>
   </section></div>`;
 }
 function mac(){let content;if(state==='ready'||state==='installed')content=`<h1>把维刻，放进你的应用程序。</h1><p>将左侧图标拖到右侧，或点击「应用程序」开始复制。</p><div class="mac-drag-area"><button draggable="true" data-action="mac-source" id="mac-app">${img}<span>维刻 Vex</span></button><span class="arrow">⟶</span><button class="mac-folder-target" data-action="install" id="mac-target"><span class="mac-folder">A</span><span>应用程序</span></button></div><p class="mac-note">复制完成后，从「应用程序」打开维刻，并推出此磁盘映像。</p>`;else if(state==='installing')content=`<h1>正在复制到「应用程序」</h1><p>稍等片刻，即可开始写作。</p><div class="mac-progress">${progressHTML()}</div><div class="install-actions">${btn('取消复制','cancel')}</div>`;else if(state==='error')content=errorBody();else content=`<div class="success-symbol">✓</div><h1>维刻，已经就位。</h1><p>应用程序已复制完成。你可以推出磁盘映像。</p><div class="install-actions">${btn('推出映像','eject')}${btn('打开维刻','open',true)}</div>`;return `<div class="scene mac-scene"><section class="install-window mac-window">${chrome('维刻 Vex',true)}<div class="mac-volume"><span>▣　Vex ${version}</span><button data-action="eject" aria-label="推出磁盘映像">⏏</button></div><div class="mac-content">${content}</div></section></div>`;}
 function linux(){let content;if(state==='ready'||state==='installed')content=`<div class="linux-app-header">${img}<div><h1>维刻 Vex</h1><p>Markdown 写作与文章结构</p></div></div><div class="install-actions" style="justify-content:flex-start;padding:0;margin:0 0 28px">${state==='installed'?btn('打开','open',true)+btn('移除…','remove'):btn('安装软件包','install',true)}<span class="pill">本地 DEB 软件包</span></div><h3>写下想法，看见脉络。</h3><p>在同一个空间里写作、阅读与整理脉络。以本地 Markdown 文件保存你的想法。</p><dl class="package-info"><dt>版本</dt><dd>${version}</dd><dt>软件源</dt><dd>Vex-${version}-linux-x64.deb</dd><dt>架构</dt><dd>amd64</dd><dt>许可证</dt><dd>MIT</dd><dt>开发者</dt><dd>Vex 贡献者 / CodeWF</dd></dl><p class="terms-note">安装由系统软件包管理器处理。</p>`;else if(state==='installing')content=`<div class="linux-app-header">${img}<div><h1>${operation==='remove'?'正在移除':'正在安装'}维刻</h1><p>由软件包管理器处理</p></div></div>${progressHTML()}<div class="install-actions">${btn('取消','cancel')}</div>`;else if(state==='error')content=errorBody();else content=`<div class="success-symbol">✓</div><h2>${complete()}</h2><p class="description">${operation==='remove'?'应用已移除，你的文档仍在原来的文件夹中。':'你现在可以从应用列表中打开维刻。'}</p><div class="install-actions">${btn('完成','finish')}${operation==='remove'?'':btn('打开维刻','open',true)}</div>`;return `<div class="scene"><section class="install-window linux-window">${chrome('软件安装 · 维刻 Vex')}<div class="linux-layout"><aside class="linux-sidebar"><strong>软件</strong><div>探索</div><div class="selected">本地软件包</div><div>已安装</div><div>更新</div></aside><div class="linux-main">${content}</div></div></section></div>`;}
 function render(){if(state==='cancelled'){host.innerHTML=`<div class="scene"><section class="install-window" style="width:min(580px,100%);min-height:420px"><div class="cancelled-screen">${img}<h2>下次见，想法会等你。</h2><p>本次流程已结束。</p>${btn('重新开始','restart',true)}<a href="index.html">返回原型导航</a></div></section></div>`;return;}const installingBefore=host.dataset.state==='installing';host.dataset.state=state;host.dataset.details=state==='installed'&&maintenanceDetails?'open':'closed';host.dataset.changelog=state==='installed'&&changelogOpen?'open':'closed';host.innerHTML=({windows,macos:mac,linux}[platform]||windows)();if(!matchMedia('(prefers-reduced-motion:reduce)').matches&&((!installingBefore&&state==='installing')||state!=='installing'))host.querySelector('.installer-content,.mac-content,.linux-main,.system-install-page,.android-sheet,.release-content')?.animate?.([{opacity:.3,translate:'0 5px'},{opacity:1,translate:'0 0'}],{duration:220,easing:'cubic-bezier(.2,.8,.2,1)'});if(platform==='macos'){const app=document.getElementById('mac-app'),target=document.getElementById('mac-target');if(app)app.ondragstart=e=>e.dataTransfer.setData('text/plain','Vex.app');if(target){target.ondragover=e=>{e.preventDefault();target.classList.add('drop');};target.ondragleave=()=>target.classList.remove('drop');target.ondrop=e=>{e.preventDefault();install();};}}}
 function install(){if(platform==='windows'){if(document.getElementById('install-directory'))directory=document.getElementById('install-directory').value.trim();shortcut=document.getElementById('shortcut')?.checked??shortcut;association=document.getElementById('association')?.checked??association;if(!/^[a-z]:\\/i.test(directory)||/[<>"|?*]/.test(directory)||directory.slice(2).includes(':')){toast('请填写有效的 Windows 绝对路径，例如 C:\\Apps\\Vex');return;}start();}
 else if(platform==='linux')dialog('需要系统授权','安装本地软件包需要管理员授权。这里演示系统授权后的流程，不收集密码。',[{label:'取消',run:closeDialog},{label:'模拟授权并安装',primary:true,run:()=>start()}]);
 else if(platform==='macos'&&params.get('scenario')==='installed')dialog('已存在「维刻 Vex」','「应用程序」中已有相同名称的应用。是否替换为此版本？',[{label:'保留现有版本',run:closeDialog},{label:'替换',primary:true,run:()=>start()}]);
 else start();}
 document.addEventListener('click',event=>{const el=event.target.closest('[data-action]');if(!el)return;event.preventDefault();switch(el.dataset.action){case 'install':install();break;case 'retry':start(operation);break;case 'cancel':cancel();break;case 'open':openApp();break;case 'release':window.open('https://github.com/dotnet9/Vex/releases','_blank','noopener');break;case 'finish':location.href='index.html';break;case 'restart':state='ready';operation='install';render();break;case 'details':maintenanceDetails=!maintenanceDetails;render();break;case 'execute':if(maintenanceChoice==='remove')confirmRemove();else start('repair');break;case 'changelog':changelogOpen=!changelogOpen;render();break;case 'changelog-more':changelogOlder=!changelogOlder;render();break;case 'browse':custom=true;if(document.getElementById('install-directory'))directory=document.getElementById('install-directory').value;dialog('选择安装位置','填写目标文件夹，预览自定义安装位置。',[{label:'取消',run:closeDialog},{label:'选择文件夹',primary:true,run:()=>{directory=document.getElementById('browse-path').value;closeDialog();render();}}],`<label for="browse-path">文件夹</label><input class="field" id="browse-path" value="${esc(directory)}" style="margin-bottom:22px">`);break;case 'license':dialog('MIT 开源许可','允许使用、复制、修改、合并、发布、分发、再许可及销售本软件的副本，需保留版权及许可声明。软件按原样提供，不作担保。',[{label:'知道了',primary:true,run:closeDialog}]);break;case 'mac-source':toast('将应用图标拖到右侧「应用程序」，或点击右侧文件夹');break;case 'eject':if(state==='installing'){cancel();return;}state='cancelled';render();toast('磁盘映像已推出（流程预览）');break;}});
 /* 单选只改右下角的动作文案，不整页重绘：重绘会把键盘焦点从单选按钮上甩掉。 */
 document.addEventListener('change',event=>{const picked=event.target.closest('input[name="maintenance-choice"]');if(!picked)return;maintenanceChoice=picked.value;const execute=host.querySelector('[data-action=execute]');if(execute){execute.textContent=maintenanceLabel();execute.className='btn '+(maintenanceChoice==='remove'?'quiet':'primary');}});
 document.addEventListener('keydown',event=>{if(event.key==='Escape'&&!modalHost.hidden)closeDialog();if(event.key==='Tab'&&!modalHost.hidden){const els=[...modalHost.querySelectorAll('input,button')],first=els[0],last=els.at(-1);if(event.shiftKey&&document.activeElement===first){event.preventDefault();last.focus();}else if(!event.shiftKey&&document.activeElement===last){event.preventDefault();first.focus();}}});
 render();
 const release=document.createElement('a');release.className='release-source';release.href='https://github.com/dotnet9/Vex/releases';release.target='_blank';release.rel='noopener';release.textContent='GitHub Releases · Windows EXE / Linux DEB / macOS DMG · SHA256';document.body.append(release);
})();
