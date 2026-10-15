const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const fixture = require('./standalone-fixture.cjs');
const { chromium } = require('playwright');
(async () => {
  const standalone = fixture(path.resolve(__dirname, '../index.html'));
  const browser = await chromium.launch({headless:true});
  try {
    const page = await browser.newPage({viewport:{width:1280,height:840}});
    await standalone.attach(page);
    const errors=[]; page.on('pageerror',error=>errors.push(error.message));
    await page.goto(standalone.url);
    await page.addStyleTag({content:'*,*::before,*::after{animation:none!important;transition:none!important}'});
    assert.equal(await page.locator('.titlebar .caption-btn').count(),3);
    assert.equal(await page.locator('#vex-theme-panel [data-palette]').count(),8);
    assert.equal(await page.locator('#vex-mode-toggle').count(),1);
    assert.ok(await page.locator('.app-window').evaluate(el=>parseFloat(getComputedStyle(el).borderTopWidth)>=1));
    assert.equal(await page.locator('.titlebar').evaluate(el=>Math.round(el.getBoundingClientRect().height)),40);
    const output=process.argv[2];if(output)fs.mkdirSync(output,{recursive:true});
    for(const culture of ['zh-CN','zh-Hant','en-US','ja-JP']) {
      await page.locator('#vex-language-menu summary').click();await page.locator('#vex-language-menu [data-language="'+culture+'"]').click();
      assert.equal(await page.locator('html').getAttribute('lang'),culture);
      assert.equal(await page.locator('.tb-menuitem').first().textContent(), await page.evaluate(()=>VEX_DATA.languages[VexL.culture].MenuFile));
      const untranslated = await page.evaluate(() => {
        const missing=[];
        function visit(items) { for(const item of items) { if(item.label && /[\u4e00-\u9fff]/.test(item.label) && !/\.md$/.test(item.label) && !['简体中文','繁體中文','日本語'].includes(item.label) && VexL.label(item.label)===item.label && VexL.culture==='en-US')missing.push(item.label); if(item.sub)visit(item.sub); } }
        visit(VexProto.menuData); return missing;
      });
      assert.deepEqual(untranslated,[]);
      await page.evaluate(()=>VexProto.handleAction('dlg-about'));
      await page.locator('#layer-about').waitFor({state:'visible'});
      assert.equal(await page.locator('#about-version').textContent(), await page.evaluate(()=>VEX_DATA.metadata.version));
      assert.equal(await page.locator('#layer-about [data-l10n="AboutVexTitle"]').textContent(),await page.evaluate(()=>VEX_DATA.languages[VexL.culture].AboutVexTitle));
      assert.ok((await page.locator('#about-copyright').textContent()).length>0);
      assert.equal(await page.locator('#about-build').textContent(),'2026-10-15 00:00:00');
      assert.ok(await page.locator('#layer-about .mini-window').evaluate(el=>parseFloat(getComputedStyle(el).borderTopWidth)>=1));
      assert.equal(await page.locator('#layer-about .mini-titlebar [data-close]').count(),1);
      if(output)await page.screenshot({path:path.join(output,'about-'+culture+'.png')});
      await page.locator('#layer-about [data-close]').click();
    }
    await page.evaluate(()=>VexProto.handleAction('dlg-about'));
    for(const scenario of ['available','current','missing','check-failed','download-failed','checksum-failed','install-failed']) {
      await page.locator('#vex-update-scenario').selectOption(scenario);
      await page.locator('#vex-update').click();
      const wait = state=>page.waitForFunction(state=>document.querySelector('#layer-about').dataset.updateState===state,state);
      if(scenario==='check-failed'){await wait('failed');await page.locator('#vex-update').click();}
      await wait(scenario==='current'?'current':scenario==='missing'?'manual':'available');
      if(['current','missing'].includes(scenario))continue;
      await page.locator('#vex-update').click();
      if(['download-failed','checksum-failed'].includes(scenario)){await wait('failed');await page.locator('#vex-update').click();}
      await wait('downloaded');await page.locator('#vex-update').click();
      if(scenario==='install-failed'){await wait('failed');await page.locator('#vex-update').click();}
      await wait('installed');
    }
    await page.locator('#vex-update-scenario').selectOption('available');await page.locator('#vex-update').click();await page.locator('#vex-update-cancel').click();
    assert.equal(await page.locator('#layer-about').getAttribute('data-update-state'),'canceled');
    await page.locator('#layer-about [data-close]').click();
    await page.reload();assert.equal(await page.locator('html').getAttribute('lang'),'ja-JP');
    assert.deepEqual(errors,[]);await standalone.verify(page);
    if(output)for(const width of [1280,980]){await page.setViewportSize({width,height:840});assert.ok(await page.evaluate(()=>{const menu=document.querySelector('.tb-menu').getBoundingClientRect(),controls=document.querySelector('#vex-theme-panel').getBoundingClientRect(),caption=document.querySelector('#caption-btns').getBoundingClientRect();return menu.right<=controls.left&&controls.right<=caption.left;}));await page.screenshot({path:path.join(output,'desktop-'+width+'.png')});}
    console.log('PASS: desktop frame, fixed sample timestamp, four real menu/About languages, generated metadata, seven update scenarios, retry, cancellation and offline reload');
  } finally {await browser.close();standalone.dispose();}
})().catch(error=>{console.error(error);process.exitCode=1;});
