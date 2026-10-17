const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const fixture = require('./standalone-fixture.cjs');
const { chromium } = require('playwright');
(async () => {
  const standalone = fixture(path.resolve(__dirname, '../Vex.html'));
  const browser = await chromium.launch({headless:true, executablePath: process.env.VEX_BROWSER_EXECUTABLE || undefined});
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
      assert.equal(await page.locator('#about-copyright').textContent(),'Copyright © 2026 Vex(维刻)');
      assert.equal(await page.locator('#layer-about .about-row').count(),4);
      assert.equal(await page.locator('#layer-about .about-row').nth(1).locator('.v').textContent(),'.NET 10，Avalonia');
      assert.equal(await page.locator('#about-build, #vex-update-recheck, #vex-update-scenario, .proto-update-card').count(),0);
      assert.equal(await page.locator('#vex-update-status').isVisible(),false);
      assert.ok(await page.locator('#layer-about .mini-window').evaluate(el=>parseFloat(getComputedStyle(el).borderTopWidth)>=1));
      assert.equal(await page.locator('#layer-about .mini-titlebar [data-close]').count(),1);
      if(output)await page.screenshot({path:path.join(output,'about-'+culture+'.png')});
      await page.locator('#layer-about [data-close]').click();
    }
    await page.evaluate(()=>VexProto.handleAction('dlg-about'));
    await page.locator('#layer-about [data-action="dlg-doc"]').click();
    assert.equal(await page.locator('#layer-about').isVisible(),false);
    assert.equal(await page.locator('#doc-content-changelog').isVisible(),true);
    assert.equal(await page.locator('#layer-doc .doc-head, #layer-doc h1').count(),0);
    await page.locator('#layer-doc [data-close]').click();
    await page.evaluate(()=>VexProto.handleAction('dlg-doc',{arg:'鸣谢'}));
    assert.equal(await page.locator('#doc-content-thanks').isVisible(),true);
    const references = new Set(['Vex/Vex.csproj','Vex.Controls/Vex.Controls.csproj','Vex.Controls.Themes/Vex.Controls.Themes.csproj'].flatMap(file =>
      Array.from(fs.readFileSync(path.resolve(__dirname,'../../src',file),'utf8').matchAll(/<PackageReference\s+Include="([^"]+)"/g), match => match[1])));
    const companions = new Set(['Avalonia.Desktop','Avalonia.Fonts.Inter','Microsoft.SourceLink.GitHub']);
    const credits = await page.locator('#doc-content-thanks a').evaluateAll(links => links.map(link => ({ name:link.textContent, href:link.href })));
    assert.deepEqual(credits.map(item => item.name).sort(), [...references].filter(name => !companions.has(name)).sort(), 'Thanks matches important direct NuGet references');
    credits.forEach(item => assert.equal(item.href,'https://www.nuget.org/packages/'+item.name));
    await page.setViewportSize({width:980,height:640});
    assert.ok(await page.locator('#layer-doc .doc-body').evaluate(el=>el.scrollHeight <= el.clientHeight + 1),'Thanks fits one screen at 980 × 640');
    if(output)await page.screenshot({path:path.join(output,'thanks-narrow.png')});
    await page.setViewportSize({width:1280,height:840});
    await page.locator('#layer-doc [data-close]').click();
    await page.evaluate(()=>VexProto.handleAction('dlg-about'));
    await page.locator('#about-check-updates').click();
    assert.equal(await page.locator('#layer-about').isVisible(),false);
    assert.equal(await page.locator('#layer-update').isVisible(),true);
    for(const scenario of ['available','current','missing','check-failed','download-failed','checksum-failed','install-failed']) {
      await page.evaluate(value=>VexUpdate.setScenario(value),scenario);
      const wait = state=>page.waitForFunction(state=>document.querySelector('#layer-update').dataset.updateState===state,state);
      if(scenario==='check-failed'){await wait('failed');await page.locator('#vex-update').click();}
      await wait(scenario==='current'?'current':scenario==='missing'?'manual':'available');
      if(['current','missing'].includes(scenario))continue;
      await page.locator('#vex-update').click();
      if(['download-failed','checksum-failed'].includes(scenario)){await wait('failed');await page.locator('#vex-update').click();}
      await wait('downloaded');await page.locator('#vex-update').click();
      if(scenario==='install-failed'){await wait('failed');await page.locator('#vex-update').click();}
      await wait('installed');
    }
    await page.evaluate(()=>VexUpdate.setScenario('available'));
    await page.waitForFunction(()=>document.querySelector('#layer-update').dataset.updateState==='available');
    await page.locator('#vex-update').click();
    await page.locator('#layer-update [data-close]').click();
    await page.waitForTimeout(300);
    assert.equal(await page.locator('#layer-update').getAttribute('data-update-state'),'downloaded');
    await page.evaluate(()=>VexProto.handleAction('dlg-about'));
    await page.locator('#about-check-updates').click();
    assert.equal(await page.locator('#vex-update').isEnabled(),true);
    assert.equal(await page.locator('#layer-update').getAttribute('data-update-state'),'downloaded');
    await page.locator('#layer-update [data-close]').click();
    await page.evaluate(()=>VexProto.handleAction('dlg-update'));
    assert.equal(await page.locator('#layer-update').getAttribute('data-update-state'),'downloaded');
    await page.locator('#layer-update [data-close]').click();
    await page.reload();assert.equal(await page.locator('html').getAttribute('lang'),'ja-JP');
    assert.deepEqual(errors,[]);await standalone.verify(page);
    if(output)for(const width of [1280,980]){await page.setViewportSize({width,height:840});assert.ok(await page.evaluate(()=>{const menu=document.querySelector('.tb-menu').getBoundingClientRect(),controls=document.querySelector('#vex-theme-panel').getBoundingClientRect(),caption=document.querySelector('#caption-btns').getBoundingClientRect();return menu.right<=controls.left&&controls.right<=caption.left;}));await page.screenshot({path:path.join(output,'desktop-'+width+'.png')});}
    console.log('PASS: compact About, four languages, shared update view, seven update scenarios, retry, continued download on close and offline reload');
  } finally {await browser.close();standalone.dispose();}
})().catch(error=>{console.error(error);process.exitCode=1;});
