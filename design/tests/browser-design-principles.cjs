/* Design contract regression: rendered contrast, previews and recoverable tasks. */
const {chromium}=require('playwright');
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const base=process.env.VEX_PREVIEW_URL||'http://127.0.0.1:4317/design/';
const out=path.resolve(process.env.VEX_CAPTURE_DIR||path.join(__dirname,'../../artifacts/prototype-design-principles'));
fs.mkdirSync(out,{recursive:true});
(async()=>{
 const browser=await chromium.launch({headless:true,...(process.env.VEX_BROWSER_CHANNEL?{channel:process.env.VEX_BROWSER_CHANNEL}:{})});
 const page=await browser.newPage({viewport:{width:1440,height:1000}}),errors=[];
 page.on('pageerror',e=>errors.push(e.message));
 async function open(query=''){await page.goto(base+'Vex.html?preview=1&'+query);await page.waitForFunction(()=>window.VexWorkflows&&window.VexWorkspace);}
 async function shot(name){await page.waitForTimeout(150);await page.screenshot({path:path.join(out,name+'.png')});}
 await open('layout=read');
 const article='# 让文章保持清晰\n\n先记录观察，再整理思路。\n\n## 结构与表达\n\n正文包含[链接示例](https://example.com)、**重点文字**与 `code`。\n\n> 引用帮助读者理解。\n\n### 技术细节\n\n说明每一步。\n\n#### 参考资料\n\n| 阶段 | 阅读重点 |\n| --- | --- |\n| 写作 | 把零散的想法整理成清楚的文章 |\n';
 await page.evaluate(text=>{VexWorkbench.applyText(text);VexWorkspace.setLayout('read');},article);
 const colors=await page.evaluate(()=>{
  const canvas=document.createElement('canvas');canvas.width=canvas.height=1;const ctx=canvas.getContext('2d',{willReadFrequently:true});
  function rgba(css){ctx.clearRect(0,0,1,1);ctx.fillStyle=css;ctx.fillRect(0,0,1,1);return [...ctx.getImageData(0,0,1,1).data].map((v,i)=>i===3?v/255:v);}
  function over(a,b){return a.slice(0,3).map((v,i)=>v*a[3]+b[i]*(1-a[3])).concat(1);}
  function background(el){if(!el)return [255,255,255,1];const color=rgba(getComputedStyle(el).backgroundColor);return color[3]===1?color:over(color,background(el.parentElement));}
  function luminance(c){return c.slice(0,3).map(v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4}).reduce((s,v,i)=>s+v*[.2126,.7152,.0722][i],0);}
  function contrast(a,b){const x=luminance(a),y=luminance(b);return (Math.max(x,y)+.05)/(Math.min(x,y)+.05);}
  const failures=[],ui=[],types=[];const body=document.querySelector('#markdown-preview');
  for(const mode of ['light','dark'])for(const p of VexPalettes){
   VexTheme.setMode(mode);VexTheme.setPalette(p.key);
   const root=getComputedStyle(document.documentElement),token=k=>root.getPropertyValue('--'+k).trim();
   const pairs=[['fg','paper'],['muted','panel'],['muted','hover'],['on-primary','primary'],['on-primary','primary-hover'],['on-primary','primary-pressed'],['on-accent','accent'],['status-error','status-error-bg']];
   const ratios=pairs.map(([a,b])=>({pair:a+'/'+b,ratio:contrast(rgba(token(a)),rgba(token(b)))}));
   for(const x of ratios)if(x.ratio<4.5)failures.push({mode,palette:p.key,...x});
   /* 规则 §5 第 2 条：accent↔accentSoft 属强调字/UI 组件角色，按 3:1 判（WCAG 1.4.11）。
      套 4.5 会逼着把中明度 accent（鼠尾草 3.20）整体压暗一档，§2 明确不允许这么做。 */
   const softRatio=contrast(rgba(token('accent')),rgba(token('accent-soft')));
   ratios.push({pair:'accent/accent-soft',ratio:softRatio});
   if(softRatio<3)failures.push({mode,palette:p.key,pair:'accent/accent-soft',ratio:softRatio});
   /* control-border 是规则 §3 的固定派生（ink@20%→paper / ink@26%→paper），§5 校验清单不含它；
      这里核对派生公式一致性，而不是套用面向文字的 3:1 / 4.5:1 阈值。 */
   const inkC=rgba(token('fg')),paperC=rgba(token('paper')),borderC=rgba(token('control-border')),borderWeight=mode==='dark'?0.26:0.20;
   const borderExpected=inkC.slice(0,3).map((v,i)=>v*borderWeight+paperC[i]*(1-borderWeight));
   const derivationDelta=Math.max(...borderC.slice(0,3).map((v,i)=>Math.abs(v-borderExpected[i])));
   ratios.push({pair:'control-border/paper',ratio:contrast(borderC,paperC),derivationDelta});
   if(derivationDelta>2)failures.push({mode,palette:p.key,pair:'control-border/paper',derivationDelta,expected:borderExpected});
   ui.push({mode,palette:p.key,ratios});
   if(getComputedStyle(document.body).backgroundColor!=='rgb(234, 236, 234)')failures.push({mode,palette:p.key,error:'Stage changed'});
   for(const t of VexTypes){
    VexTypography.choose(t.key);
    const sample={mode,palette:p.key,type:t.key,minimum:Infinity};
    for(const selector of ['h1','h2','h3','h4','p','a','blockquote']){
     const el=body.querySelector(selector),bg=background(el),fg=over(rgba(getComputedStyle(el).color),bg),ratio=contrast(fg,bg);
     sample.minimum=Math.min(sample.minimum,ratio);
     if(ratio<4.5)failures.push({mode,palette:p.key,type:t.key,selector,ratio,fg,bg});
    }
    types.push(sample);
   }
  }
  VexTheme.setMode('light');VexTheme.setPalette('indigo');VexTypography.choose('Simple');
  return {ui,types,failures};
 });
 fs.writeFileSync(path.join(out,'contrast.json'),JSON.stringify(colors,null,2));
 assert.deepEqual(colors.failures,[],'All rendered text and semantic control roles meet their contrast target');
 // Independent true-size previews, keyboard navigation and position preservation.
 await page.locator('[data-action=typography]:visible').first().click();
 assert.equal(await page.locator('.typography-card:visible').count(),13);
 const previews=await page.locator('.typography-card').evaluateAll(cards=>cards.map(card=>{const paper=card.querySelector('.type-swatch'),doc=card.querySelector('.type-document');return {key:card.dataset.typographyKey,preview:paper.dataset.previewType,size:parseFloat(getComputedStyle(doc).fontSize),fits:doc.getBoundingClientRect().bottom<=paper.getBoundingClientRect().bottom+1};}));
 for(const item of previews)assert(item.key===item.preview&&item.size>=13&&item.fits,JSON.stringify(item));
 const card=page.locator('.typography-card').first();await card.focus();await page.keyboard.press('ArrowRight');assert.equal(await page.evaluate(()=>VexTypography.state.key),'Simple');
 await shot('typography-light');await page.keyboard.press('End');await page.keyboard.press('Enter');assert.equal(await page.evaluate(()=>VexTypography.state.key),'WeChatFormat');
 assert.equal(await page.evaluate(()=>VexWorkbench.text),article);await page.evaluate(()=>VexTheme.setMode('dark'));await shot('typography-dark');
 await page.keyboard.press('Escape');
 await page.locator('#vex-appearance-menu>summary').click();await shot('appearance-recommended');assert.equal(await page.locator('#vex-appearance-menu [data-palette]').count(),9);
 await page.setViewportSize({width:800,height:480});await page.locator('#vex-appearance-menu [data-palette]').last().scrollIntoViewIfNeeded();const rect=await page.locator('#vex-appearance-menu [data-palette]').last().boundingBox();assert(rect.y+rect.height<480);await shot('appearance-small');
 // A long command submenu keeps its width and scrolls to the last keyboard target.
 await page.keyboard.press('Escape');await page.locator('.tb-menuitem').filter({hasText:'段落'}).click();
 const owner=page.locator('.menu-flyout .menu-item').filter({hasText:'Mermaid 图表'});await owner.click();
 const submenu=page.locator('.submenu:visible').filter({has:page.locator('.menu-item').filter({hasText:'流程图'})});
 assert.equal(await submenu.locator('.menu-item').count(),22);
 assert(Math.abs((await submenu.boundingBox()).width-248)<1);await submenu.locator('.menu-item').first().focus();await page.keyboard.press('End');
 assert((await page.locator(':focus').innerText()).includes('矩形树图'));
 const last=await page.locator(':focus').boundingBox();assert(last.y+last.height<480);await shot('long-menu-keyboard');
 await page.setViewportSize({width:1440,height:1000});
 // Saving failure is persistent, and retry never alters the article or undo history.
 await open('scenario=save-error&layout=source');await page.evaluate(t=>VexWorkbench.applyText(t),article);
 await page.evaluate(()=>VexProto.handleAction('save'));assert(await page.locator('.workflow-notice').innerText().then(s=>s.includes('下载失败')));
 await page.evaluate(()=>VexWorkbench.applyText(VexWorkbench.text+'\n新增观察。'));assert.equal(await page.evaluate(()=>VexWorkbench.documents[0].saveError),true);
 await shot('save-failure');const beforeSave=await page.evaluate(()=>({text:VexWorkbench.text,undo:VexWorkbench.state.undo.length}));
 const saveDownload=page.waitForEvent('download');await page.locator('[data-workflow=retry-save]').click();await saveDownload;
 assert.deepEqual(await page.evaluate(()=>({text:VexWorkbench.text,undo:VexWorkbench.state.undo.length})),beforeSave);assert.equal(await page.locator('.workflow-notice').isHidden(),true);
 // HTML failure has a separate retry and does not mark Markdown as saved.
 await open('scenario=export-error&layout=read');await page.evaluate(t=>VexWorkbench.applyText(t),article);await page.evaluate(()=>VexProto.handleAction('export-html'));await shot('export-failure');
 const htmlDownload=page.waitForEvent('download');await page.locator('[data-workflow=retry-export]').click();const exported=await htmlDownload;assert(exported.suggestedFilename().endsWith('.html'));assert.equal(await page.evaluate(()=>VexWorkbench.state.dirty),true);assert.equal(await page.evaluate(()=>VexWorkbench.text),article);
 // External changes require a choice; cancel restores position, acceptance can be undone.
 await open('scenario=external-change&layout=source');const original=await page.evaluate(()=>VexWorkbench.text);
 await page.evaluate(()=>{const input=document.querySelector('#markdown-input');input.setSelectionRange(8,12);document.querySelector('.source-scroll').scrollTop=90;});
 const position=await page.evaluate(()=>({start:document.querySelector('#markdown-input').selectionStart,top:document.querySelector('.source-scroll').scrollTop}));
 await page.locator('[data-workflow=compare]').click();await shot('external-comparison');await page.keyboard.press('Escape');await page.waitForTimeout(80);
 assert.equal(await page.evaluate(()=>VexWorkbench.text),original);assert.deepEqual(await page.evaluate(()=>({start:document.querySelector('#markdown-input').selectionStart,top:document.querySelector('.source-scroll').scrollTop})),position);
 await page.locator('[data-workflow=compare]').click();await page.locator('[data-workflow=load-external]').click();assert.notEqual(await page.evaluate(()=>VexWorkbench.text),original);await page.evaluate(()=>VexWorkbench.history('undo'));assert.equal(await page.evaluate(()=>VexWorkbench.text),original);
 // Recovery creates an independent draft instead of replacing the current file.
 await open('scenario=draft');const originalName=await page.evaluate(()=>VexWorkbench.name);await page.locator('[data-workflow=recover]').click();await shot('draft-recovery');await page.locator('[data-workflow=restore-drafts]').click();assert((await page.evaluate(()=>VexWorkbench.name)).endsWith('.恢复.md'));assert(await page.evaluate(name=>VexWorkbench.documents.some(d=>d.name===name),originalName));assert.equal(await page.evaluate(()=>VexWorkbench.state.dirty),true);
 // Caption close and menu exit share the unsaved guard; cancelling keeps the document.
 await page.locator('#caption-btns [data-action=exit-app]').click();assert.equal(await page.locator('.exit-dialog-host').isVisible(),true);await page.locator('[data-exit=cancel]').last().click();assert.equal(await page.locator('.app-window').isVisible(),true);
 await page.evaluate(()=>VexWorkbench.state.failNextSave=true);await page.locator('#caption-btns [data-action=exit-app]').click();await page.locator('[data-exit=save]').click();await page.locator('.exit-dialog-error').waitFor({state:'visible'});assert.equal(await page.locator('.exit-dialog-host').isVisible(),true);await page.locator('[data-exit=cancel]').last().click();
 assert.equal(await page.locator('.prototype-draft').count(),0,'There is only one recovery flow');
 // Open a real repository article and verify the downloaded Markdown and HTML.
 await open('layout=source');
 const realFile=path.resolve(__dirname,'../../docs/原型与资源.md');
 const realText=fs.readFileSync(realFile,'utf8').replace(/\r\n/g,'\n');
 await page.locator('#open-file-input').setInputFiles(realFile);await page.waitForFunction(name=>VexWorkbench.name===name,path.basename(realFile));
 assert.equal((await page.evaluate(()=>VexWorkbench.text)).replace(/\r\n/g,'\n'),realText);
 await page.evaluate(()=>VexWorkbench.applyText(VexWorkbench.text+'\n\n这是一段用于验收的真实写作记录。\n'));
 await page.evaluate(()=>{const input=document.querySelector('#markdown-input');input.focus();input.setSelectionRange(600,610);document.querySelector('.source-scroll').scrollTop=140;});await page.waitForTimeout(80);
 const snapshot=()=>page.evaluate(()=>({text:VexWorkbench.text,selection:[document.querySelector('#markdown-input').selectionStart,document.querySelector('#markdown-input').selectionEnd],top:document.querySelector('.source-scroll').scrollTop,undo:VexWorkbench.state.undo.length}));
 const beforeAppearance=await snapshot();
 await page.evaluate(()=>{for(const mode of ['light','dark'])for(const p of VexPalettes){VexTheme.setMode(mode);VexTheme.setPalette(p.key);}for(const t of VexTypes)VexTypography.choose(t.key);VexTypography.toggleCompact();VexTypography.toggleCompact();});
 assert.deepEqual(await snapshot(),beforeAppearance,'Appearance retains text, selection, history and position');
 await page.evaluate(()=>VexWorkspace.setLayout('source-render'));await page.waitForTimeout(80);await page.evaluate(()=>VexWorkspace.setLayout('source'));await page.waitForTimeout(80);assert.deepEqual(await snapshot(),beforeAppearance,'Layout retains the editing position');
 await page.evaluate(()=>{const node=VexWorkbench.state.nodes.find(x=>x.kind==='heading'&&x.level===2);VexChapters.rename(node,'文档与视图 · 已整理');});
 assert((await page.evaluate(()=>VexWorkbench.text)).includes('## 文档与视图 · 已整理'));await page.evaluate(()=>VexWorkbench.history('undo'));assert.equal(await page.evaluate(()=>VexWorkbench.text),beforeAppearance.text);
 await page.evaluate(()=>{const node=VexWorkbench.state.nodes.find(x=>x.kind==='heading'&&x.level===2);VexChapters.rename(node,'文档与视图 · 已整理');});
 const finalArticle=await page.evaluate(()=>VexWorkbench.text);
 const actualSave=page.waitForEvent('download');await page.evaluate(()=>VexProto.handleAction('save'));const markdown=await actualSave;assert.equal(fs.readFileSync(await markdown.path(),'utf8'),finalArticle);assert.equal(await page.evaluate(()=>VexWorkbench.state.dirty),false);
 const actualExport=page.waitForEvent('download');await page.evaluate(()=>VexProto.handleAction('export-html'));const html=fs.readFileSync(await (await actualExport).path(),'utf8');assert(html.includes('文档与视图 · 已整理')&&html.includes('这是一段用于验收的真实写作记录。'));await shot('real-article-complete');
 await page.goto(base+'Appearance.html');await page.locator('[data-review-types=all]').click();assert.equal(await page.locator('.type-choice .type-swatch').count(),13);const previewText=await page.locator('.type-choice .type-document>p').evaluateAll(nodes=>nodes.map(node=>parseFloat(getComputedStyle(node).fontSize)));assert(previewText.every(size=>size>=13));await shot('appearance-review');
 assert.deepEqual(errors,[]);
 fs.writeFileSync(path.join(out,'results.json'),JSON.stringify({combinations:colors.types.length,previews,errors,tasks:['save retry','export retry','external compare/cancel/accept/undo','draft recovery','caption close guard','real Markdown open/edit/chapter/undo/save/export','appearance preserves selection/history/position','independent review previews']},null,2));
 console.log('PASS: 216 rendered palette/mode/typography combinations; semantic contrast; 12 independent previews; recommendation filters; keyboard scrolling; save/export retry; external comparison and undo; independent draft recovery; guarded caption close.');
 await browser.close();
})().catch(error=>{console.error(error);process.exit(1)});
