/* Run against preview.cjs with Playwright available through NODE_PATH. */
const {chromium} = require('playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
(async () => {
  const browser = await chromium.launch({channel:process.env.VEX_BROWSER_CHANNEL || 'msedge',headless:true});
  try {
    const page = await browser.newPage({viewport:{width:1280,height:820}}), errors = [];
    page.on('pageerror',error => errors.push(error.message));
    await page.goto((process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/') + 'Vex.html');
    await page.evaluate(() => VexWorkbench.loadDocument(VexWorkbench.documents.findIndex(doc => doc.name === 'Mermaid 图表图鉴.md')));
    await page.locator('.instant-scroll .diagram-preview svg').first().waitFor();
    assert.ok(await page.locator('.instant-scroll .diagram-preview svg').count() < 22,'Only nearby diagrams render on opening');
    assert.equal(await page.locator('#markdown-preview svg').count(),0,'Hidden reading view does not render diagrams');
    const report = [];
    for (const mode of ['instant','read']) {
      await page.evaluate(mode => mode === 'read' ? VexWorkbench.setView('read') : VexWorkbench.setEditMode('instant'),mode);
      // Let layout restoration and view synchronization finish before scrolling diagrams.
      await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      const diagrams = page.locator(mode === 'read' ? '#markdown-preview .md-diagram' : '.instant-scroll .diagram-preview');
      assert.equal(await diagrams.count(),22);
      for (let index=0; index<22; index++) {
        const diagram = diagrams.nth(index);
        await diagram.scrollIntoViewIfNeeded();
        await diagram.locator('svg').first().waitFor();
        const size = await diagram.evaluate(host => {
          const svg=host.querySelector('svg'), rect=svg.getBoundingClientRect(), style=getComputedStyle(host);
          return {overflowX:host.scrollWidth-host.clientWidth,overflowY:host.scrollHeight-host.clientHeight,width:rect.width,height:rect.height,scroll:style.overflowX};
        });
        assert.ok(size.width>0 && size.height>0,`${mode} ${index}: complete SVG is visible`);
        assert.ok(size.overflowX<=1 && size.overflowY<=1,`${mode} ${index}: no internal clipping or scrolling: ${JSON.stringify(size)}`);
      }
      const rendered = await diagrams.evaluateAll(hosts => hosts.filter(host => host.querySelector('svg')).length);
      assert.equal(rendered,22);
      report.push({mode,rendered});
    }
    // Both views remain in the DOM; cached instances must have distinct SVG IDs.
    const duplicates = await page.locator('.md-diagram svg [id],.diagram-preview svg [id],.md-diagram svg[id],.diagram-preview svg[id]').evaluateAll(elements => {
      const owners=new Map(),duplicates=[];
      for(const element of elements){const host=element.closest('.md-diagram,.diagram-preview'),owner=owners.get(element.id);if(owner && owner!==host)duplicates.push(element.id);owners.set(element.id,host);}
      return duplicates;
    });
    assert.deepEqual(duplicates,[],'Cached SVGs have independent marker, node and CSS IDs');
    await page.setViewportSize({width:760,height:820});
    for (let index=0; index<22; index++) {
      const diagram=page.locator('#markdown-preview .md-diagram').nth(index);await diagram.scrollIntoViewIfNeeded();
      assert.ok(await diagram.evaluate(host=>host.scrollWidth<=host.clientWidth+1),`Narrow diagram ${index} fits`);
    }
    if (process.env.VEX_CAPTURE_DIR) {
      fs.mkdirSync(process.env.VEX_CAPTURE_DIR,{recursive:true});
      await page.locator('#markdown-preview .md-diagram').nth(15).scrollIntoViewIfNeeded();
      await page.screenshot({path:path.join(process.env.VEX_CAPTURE_DIR,'mermaid-responsive.png')});
    }
    assert.deepEqual(errors,[]);console.log(JSON.stringify({report,errors}));
  } finally { await browser.close(); }
})().catch(error=>{console.error(error);process.exitCode=1;});
