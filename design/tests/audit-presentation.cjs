const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const fixture = require('./standalone-fixture.cjs');
(async () => {
  const standalone = fixture(path.resolve(__dirname, '../Vex.html'));
  const browser = await chromium.launch({headless:true, executablePath:process.env.VEX_BROWSER_EXECUTABLE || undefined});
  const output = path.join(process.env.CODEWF_TEMP_ROOT || require('node:os').tmpdir(), 'vex-presentation-prototype');
  fs.mkdirSync(output,{recursive:true});
  try {
    const page = await browser.newPage({viewport:{width:1920,height:1032}});
    const errors=[];page.on('pageerror',e=>errors.push(e.message));
    await standalone.attach(page);await page.goto(standalone.url+'?theme=light&layout=three');
    const text='# 总览\n\n开场。\n\n## 代码与公式\n\n```cs\n# 不应分页\nvar value = 42;\n```\n\n## 结论\n\n| A | B |\n| --- | --- |\n| 1 | 2 |';
    await page.evaluate(text=>VexWorkbench.applyText(text),text);
    for(const theme of ['light','dark'])for(const layout of ['instant','source','read','map','source-render','source-map','instant-map','three']){
      await page.evaluate(({layout,theme})=>{VexWorkspace.setLayout(layout);VexTheme.setMode(theme);},{layout,theme});
      const before=await page.evaluate(()=>({layout:VexWorkspace.current,mode:VexWorkbench.state.editMode,text:VexWorkbench.text,prefs:localStorage.getItem('vex-design-layout')}));
      await page.locator('#present-button').click();
      assert.equal(await page.locator('#presentation-stage').isVisible(),true);
      assert.equal(await page.locator('#presentation-progress').textContent(),'1 / 3');
      assert.equal(await page.locator('#presentation-previous').isDisabled(),true);
      await page.locator('#presentation-map-toggle').click();
      assert.equal(await page.locator('#presentation-map').isVisible(),true);
      assert.ok(await page.locator('#presentation-map-scene .map-node').count()>3);
      const nodeCount=await page.locator('#presentation-map-scene .map-node').count();
      await page.locator('#presentation-map-scene .root .map-collapse').click();
      assert.equal(await page.locator('#presentation-map-scene .map-node').count(),1);
      await page.locator('#presentation-map-scene .root .map-collapse').click();
      assert.equal(await page.locator('#presentation-map-scene .map-node').count(),nodeCount);
      await page.locator('#presentation-map-scene .map-node-button[title="代码与公式"]').dblclick();
      assert.equal(await page.locator('#presentation-progress').textContent(),'2 / 3');
      assert.match(await page.locator('#presentation-body').innerText(),/var value = 42/);
      await page.locator('#presentation-map-toggle').click();
      await page.locator('#presentation-map-scene .map-node-button[title="结论"]').focus();
      await page.keyboard.press('Enter');
      assert.equal(await page.locator('#presentation-progress').textContent(),'3 / 3');
      await page.locator('#presentation-map-toggle').click();
      const initialTransform=await page.locator('#presentation-map-scene').evaluate(el=>el.style.transform);
      await page.locator('[data-action="presentation-map-larger"]').click();
      assert.notEqual(await page.locator('#presentation-map-scene').evaluate(el=>el.style.transform),initialTransform);
      const viewport=await page.locator('#presentation-map').boundingBox();
      await page.mouse.move(viewport.x+viewport.width/2,viewport.y+viewport.height/2);await page.mouse.wheel(0,300);
      if(layout==='three')await page.screenshot({path:path.join(output,theme+'-map.png')});
      const exit=await page.locator('[data-action="presentation-exit"]').boundingBox();
      assert.ok(exit.x>1800,'Exit remains at the right in map presentation');
      await page.locator('#presentation-outline-toggle').click();
      assert.equal(await page.locator('#presentation-outline').isVisible(),true);
      assert.equal(await page.locator('#presentation-chapters button').count(),3);
      await page.locator('[data-chapter="1"]').click();
      assert.match(await page.locator('#presentation-body').innerText(),/var value = 42/);
      assert.equal(await page.locator('#presentation-body h1').count(),0);
      await page.keyboard.press('PageDown');
      assert.equal(await page.locator('#presentation-body table').count(),1);
      assert.equal(await page.locator('#presentation-next').isDisabled(),true);
      for(let i=0;i<10;i++){await page.keyboard.press('ArrowLeft');await page.keyboard.press('ArrowRight');}
      if(layout==='three'){
        await page.screenshot({path:path.join(output,theme+'-content.png')});
        await page.keyboard.press('o');await page.screenshot({path:path.join(output,theme+'-outline.png')});
      }
      await page.keyboard.press('Escape');
      assert.equal(await page.locator('#presentation-stage').isVisible(),false);
      assert.deepEqual(await page.evaluate(()=>({layout:VexWorkspace.current,mode:VexWorkbench.state.editMode,text:VexWorkbench.text,prefs:localStorage.getItem('vex-design-layout')})),before);
    }
    await page.setViewportSize({width:980,height:640});await page.keyboard.press('F6');
    const toolbar=await page.locator('#presentation-tools').boundingBox();assert.ok(toolbar.x>=0&&toolbar.x+toolbar.width<=980);
    await page.keyboard.press('End');assert.equal(await page.locator('#presentation-progress').textContent(),'3 / 3');
    await page.keyboard.press('Home');assert.equal(await page.locator('#presentation-progress').textContent(),'1 / 3');
    await page.keyboard.press('F6');assert.equal(await page.locator('#presentation-stage').isVisible(),false);
    assert.deepEqual(errors,[]);console.log('PASS presentation: 16 theme/layout combinations, 320 keyboard page changes, outline jumps, boundaries, restoration and narrow screen');
  } finally {await browser.close();standalone.dispose();}
})().catch(error=>{console.error(error);process.exitCode=1;});
