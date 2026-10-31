/* Optional visual checks. Requires Playwright and the local preview server. */
const {chromium}=require('playwright');
const assert=require('node:assert/strict'),fs=require('node:fs');
const path=require('node:path');
const out=path.resolve(process.env.VEX_CAPTURE_DIR || path.join(__dirname,'../../artifacts/prototype-resource-review'));fs.mkdirSync(out,{recursive:true});
(async()=>{
 const browser=await chromium.launch({headless:true,...(process.env.VEX_BROWSER_CHANNEL ? {channel:process.env.VEX_BROWSER_CHANNEL} : {})});
 const page=await browser.newPage({viewport:{width:1600,height:1000}}),errors=[],missing=[];
 page.on('pageerror',e=>errors.push(e.message));page.on('response',r=>{if(r.status()>=400)missing.push(r.url())});
 const base=process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/';
 async function open(query=''){await page.goto(base+'Vex.html?preview=1&'+query);await page.waitForFunction(()=>window.VexWorkbench&&window.VexTypography);await page.waitForTimeout(150)}
 async function shot(name){await page.waitForTimeout(300);await page.screenshot({path:out+'/'+name+'.png'});}
 await open('layout=read');
 assert.equal(await page.evaluate(()=>new Set(VexPalettes.map(x=>x.key)).size),9);
 assert.equal(await page.evaluate(()=>new Set(VexTypes.map(x=>x.key)).size),13);
 const palette=await page.evaluate(()=>{
  const list=[];for(const mode of ['light','dark'])for(const p of VexPalettes){VexTheme.setMode(mode);VexTheme.setPalette(p.key);const s=getComputedStyle(document.documentElement),keys=['--bg','--fg','--muted','--accent','--on-accent','--border','--panel'];list.push({mode,key:p.key,values:keys.map(k=>s.getPropertyValue(k).trim())})}return list;
 });
 assert.equal(palette.length,18);for(const x of palette)assert(x.values.every(v=>v&&!/undefined|StaticResource/.test(v)),JSON.stringify(x));
 const text='# 文档\n\n正文保持一致。\n\n## 负责方与职责\n\n| 负责方 | 职责 | 示例 |\n| --- | --- | --- |\n| 编辑器 | 接收输入、处理选区和中文候选，生成可撤销的编辑动作 | 即时编辑、源码、查找替换与表格 |\n| 应用 | 文件读写、偏好、窗口、导出与本机协作 | 已有文件自动保存；保存失败时保留内容 |\n\n> 写下想法。\n';
 await page.evaluate(t=>{VexTheme.setMode('light');VexTheme.setPalette('indigo');VexWorkspace.setLayout('read');VexWorkbench.applyText(t)},text);
 const types=await page.evaluate(()=>VexTypes.map(t=>{VexTypography.choose(t.key);const body=document.querySelector('#markdown-preview'),h=body.querySelector('h2'),s=getComputedStyle(h),b=getComputedStyle(body);return {key:t.key,color:s.color,fontSize:b.fontSize,headingSize:s.fontSize,border:s.borderLeftWidth+' '+s.borderBottomStyle,background:s.backgroundColor,align:s.textAlign,font:s.fontFamily}}));
 assert.equal(new Set(types.map(({key,...v})=>JSON.stringify(v))).size,types.length,'Each typography needs a distinct rendered signature');
 assert.equal(await page.evaluate(()=>VexWorkbench.text),text);
 await page.evaluate(()=>VexTypography.choose('Simple'));
 const widths=await page.locator('#markdown-preview th').evaluateAll(els=>els.map(x=>x.getBoundingClientRect().width));
 assert(widths[0]<widths[1]*.6,JSON.stringify(widths));await shot('table-content-width');
 await page.locator('#vex-appearance-menu>summary').click();await shot('titlebar-appearance');
 for(const size of [{width:800,height:480},{width:1024,height:768}]){
  await page.setViewportSize(size);await page.locator('#vex-appearance-menu').evaluate(el=>el.open=true);
  assert.equal(await page.locator('#vex-appearance-menu [data-palette]').count(),9);
  await page.locator('#vex-theme-panel [data-palette="terracotta"]').scrollIntoViewIfNeeded();
  const rect=await page.locator('#vex-theme-panel [data-palette="terracotta"]').boundingBox();assert(rect.y+rect.height<size.height);await shot('palette-menu-'+size.width);
 }
 await page.setViewportSize({width:1600,height:1000});
 for(const platform of ['windows','linux','macos']){
  await open('platform='+platform);
  const centers=await page.evaluate(()=>[...document.querySelectorAll('#vex-appearance-menu>summary,#vex-language-menu>summary,.titlebar>.caption-btns>.caption-btn')].map(x=>{const r=x.getBoundingClientRect();return r.y+r.height/2}));
  assert(Math.max(...centers)-Math.min(...centers)<1,platform+': '+centers);await shot('titlebar-'+platform);
 }
 await open('layout=read');
 await page.evaluate(()=>{VexWorkspace.setLayout('read');VexWorkbench.applyText('# 图表图鉴\n\n'+VexMermaid.items.map(x=>'## '+x.label+'\n'+VexMermaid.snippet(x.key)).join('\n'));});
 // 图表按 IntersectionObserver 懒渲染：逐张滚入视口，直到 22 张全部完成。
 const hosts=page.locator('#markdown-preview .md-diagram');
 for(let index=0;index<await hosts.count();index++)await hosts.nth(index).scrollIntoViewIfNeeded();
 await page.waitForFunction(()=>{const nodes=[...document.querySelectorAll('#markdown-preview .md-diagram')];return nodes.length===22&&nodes.every(node=>node.dataset.rendered==='true');},null,{timeout:120000});
 const diagram=await page.evaluate(()=>[...document.querySelectorAll('#markdown-preview .md-diagram')].map(el=>{const svg=el.querySelector('svg'),box=svg.viewBox.baseVal,rect=svg.getBoundingClientRect();return {label:el.dataset.label,width:rect.width,height:rect.height,viewBox:box.width,viewBoxHeight:box.height,overflow:getComputedStyle(el).overflowX};}));
 // 图表按原生尺寸绘制，正文更窄时等比缩放（max-width:100%），不得裁切或变形。
 for(const d of diagram)assert(d.width>0&&d.width<=d.viewBox+1&&Math.abs(d.height/d.width-d.viewBoxHeight/d.viewBox)<0.01,JSON.stringify(d));
 for(const mode of ['light','dark']){
  await page.evaluate(m=>VexTheme.setMode(m),mode);
  for(const label of ['甘特图','C4 架构图','XY 坐标图']){
   const node=page.locator('#markdown-preview .md-diagram').filter({has:page.locator('svg[aria-label="'+label+'"]')});
   // Capture the whole diagram at its native size, then restore reading width.
   await node.evaluate(el=>{el.style.width='1250px';el.style.maxWidth='none'});
   await node.screenshot({path:out+'/'+label+'-'+mode+'.png'});
   await node.evaluate(el=>{el.style.width='';el.style.maxWidth=''});
  }
 }
 const clipping=await page.evaluate(()=>{
  const svg=document.querySelector('#markdown-preview svg[aria-label="XY 坐标图"]'),rect=svg.getBoundingClientRect();
  return [...svg.querySelectorAll('text')].filter(t=>{const r=t.getBoundingClientRect();return r.left<rect.left-.5||r.right>rect.right+.5||r.top<rect.top-.5||r.bottom>rect.bottom+.5}).map(x=>x.textContent);
 });assert.deepEqual(clipping,[],'XY labels fit the SVG');
 await page.setViewportSize({width:800,height:480});await shot('diagrams-narrow');
 assert(await page.locator('#markdown-preview .md-diagram').filter({has:page.locator('svg[aria-label="甘特图"]')}).evaluate(el=>{const svg=el.querySelector('svg');return svg.getBoundingClientRect().width<=el.getBoundingClientRect().width+.5&&el.scrollWidth<=el.clientWidth+1;}),'Narrow pane scales diagrams instead of clipping them');
 await page.setViewportSize({width:1600,height:1000});
 await page.evaluate(()=>{VexWorkspace.setLayout('instant');VexWorkbench.applyText('# 即时图表\n'+['gantt','c4','xy'].map(k=>VexMermaid.snippet(k)).join('\n'));});
 await page.waitForFunction(()=>document.querySelectorAll('#instant-content .diagram-preview svg').length===3,null,{timeout:15000}).catch(async()=>{console.log('instant surfaces',await page.locator('.diagram-preview').count());throw Error('instant diagrams missing')});
 const svgBefore=await page.locator('.diagram-preview svg').first().evaluate(el=>el.id);
 await page.evaluate(()=>VexTheme.setMode('light'));assert.equal(await page.locator('.diagram-preview svg').first().evaluate(el=>el.id),svgBefore);
 await page.evaluate(()=>{VexWorkspace.setLayout('map');VexWorkbench.applyText('# 用公式表达规律\n\n## 从行内公式开始\n\n圆的面积可以用公式表达。\n\n## 数学方程\n\n### 求根公式与矩阵\n\n## 化学表达式\n\n水与配平反应。\n');VexWorkbench.fit();});await page.waitForTimeout(400);await shot('map-inline-hints');
 const hint=await page.locator('.map-node:not(.content) .map-node-button').evaluateAll(els=>els.map(el=>{const t=el.querySelector('.map-node-text').getBoundingClientRect(),h=el.querySelector('.map-node-type').getBoundingClientRect();return {text:el.textContent,offset:Math.abs(t.y+t.height/2-h.y-h.height/2),font:parseFloat(getComputedStyle(el.querySelector('.map-node-type')).fontSize)}}));
 for(const h of hint)assert(h.offset<1&&h.font<=9,JSON.stringify(h));
 for(const platform of ['Windows','Linux','macOS'])for(const scenario of ['ready','installed','installing','error']){
  await page.goto(base+'Vex-Installer-'+platform+'.html?scenario='+scenario+'&progress=46');
  await page.waitForTimeout(80);assert((await page.locator('#installer-root').innerText()).length>20);
  if(scenario==='installing')assert.equal(await page.locator('[role=progressbar]').getAttribute('aria-valuenow'),'46');
  if(scenario==='error'){
   await page.locator('[data-action=install]').click();if(platform==='Linux')await page.locator('[data-dialog-button="1"]').click();
   await page.waitForFunction(()=>document.querySelector('#installer-root').dataset.state==='error',null,{timeout:10000});
   await page.locator('[data-action=retry]').click();await page.waitForFunction(()=>document.querySelector('#installer-root').dataset.state==='done',null,{timeout:10000});
  }
  if(scenario==='ready')await shot('installer-'+platform);
 }
 await page.goto(base+'Appearance.html');await page.waitForTimeout(200);assert.equal(await page.locator('[data-type]').count(),13);
 await shot('appearance-comparison');
 assert.deepEqual(errors,[]);assert.deepEqual(missing,[]);
 fs.writeFileSync(out+'/results.json',JSON.stringify({palette,types,widths,diagram,hint,errors,missing},null,2));
 console.log('PASS: 9 palettes × 2 modes; 13 distinct typographies; content-sized table; 22 diagrams; XY bounds; editable diagrams; titlebar alignment on 3 platforms; inline map hints; 12 installer scenarios.');
 await browser.close();
})().catch(e=>{console.error(e);process.exit(1)});
