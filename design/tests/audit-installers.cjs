const assert = require('node:assert/strict');
const path = require('node:path');
const fs = require('node:fs');
const os = require('node:os');
const { chromium } = require('playwright');
const standaloneFixture = require('./standalone-fixture.cjs');

(async () => {
  const browser = await chromium.launch({
    headless: true,
    executablePath: process.env.VEX_BROWSER_EXECUTABLE || undefined,
  });
  const screenshotDirectory = fs.mkdtempSync(path.join(os.tmpdir(), 'vex-installer-screenshots-'));
  try {
    for (const [name, platform] of [['Windows', 'windows'], ['Linux', 'linux'], ['macOS', 'macos']]) {
      const fixture = standaloneFixture(path.resolve(__dirname, `../Vex-Installer-${name}.html`));
      const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, reducedMotion: 'reduce' });
      const errors = [];
      page.on('pageerror', error => errors.push(error.message));
      await fixture.attach(page);
      const action = name => page.locator(`[data-action="${name}"]`).last().click();
      const state = () => page.evaluate(() => CodeWFInstallerPrototype.state);
      const completeOperation = async step => {
        await page.clock.runFor(4000);
        assert.equal((await state()).step, step);
      };
      const open = async query => {
        await page.goto(fixture.url + (query || ''));
        assert.equal(await page.locator('#installerWindow').getAttribute('data-installer-platform'), platform);
      };
      try {
        await open();
        assert.equal(await page.locator('#platforms,#productSelect,#prototypeToolbar,#stageButtons,#scenarioSelect,.masthead,.preview-controls,.footnote').count(), 0);
        assert.deepEqual(await page.locator('body > :not(script)').evaluateAll(nodes => nodes.map(node => node.id)), ['installerWindow']);
        await page.evaluate(() => CodeWFInstallerPrototype.set({ platform: 'other', product: 'other' }));
        assert.equal((await state()).platform, platform);
        assert.equal((await state()).product, 'vex');
        assert.equal(await page.locator('.slogan,.hero-tagline,.eyebrow').innerText(), '维刻 Vex · 写下想法，看见脉络。');
        assert.equal(await page.evaluate(() => CodeWFInstallerPrototype.config.chineseName), '维刻');

        for (const theme of ['light', 'dark']) {
          await page.emulateMedia({ colorScheme: theme });
          await page.waitForFunction(value => document.documentElement.dataset.theme === value, theme);
          assert.equal(await page.locator('html').getAttribute('data-theme'), theme);
          for (const width of [1440, 800, 390, 320]) {
            await page.setViewportSize({ width, height: 900 });
            assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), `${name}/${theme}/${width}: no horizontal page overflow`);
            const start = page.locator(platform === 'windows' ? '#primaryAction' : platform === 'linux' ? '[data-action="linux-install"]' : '#applicationsTarget');
            const box = await start.boundingBox();
            assert.ok(box && box.x >= 0 && box.x + box.width <= width, `${name}/${width}: action stays in viewport`);
            await page.screenshot({ path: path.join(screenshotDirectory, `${name}-${theme}-${width}.png`) });
          }
        }
        await page.setViewportSize({ width: 1440, height: 900 });
        if (platform !== 'windows') {
          await action('window-minimize');
          assert.equal((await state()).minimized, true);
          await action('window-minimize');
          assert.equal((await state()).minimized, false);
        }
        await page.clock.install();

        if (platform === 'windows') {
          assert.equal(await page.locator('.steps,.sidebar,canvas,#licenseAccepted,[data-action="next"]').count(), 0);
          assert.equal(await page.locator('#installPath').isVisible(), false);
          assert.ok((await page.locator('#installerWindow').boundingBox()).height < 400);
          await action('install');
          assert.equal((await state()).step, 'installing', 'One click starts installation');
          await completeOperation('complete');
          await action('finish');
          assert.equal((await state()).launch, false);
          assert.equal(await page.locator('#installerWindow').isVisible(), false);

          await open();
          await page.locator('#customSettings summary').click();
          await page.locator('#installPath').fill('C:\\Windows\\System32');
          assert.equal(await page.locator('#primaryAction').isDisabled(), true);
          await page.locator('#installPath').fill('relative/path');
          assert.equal(await page.locator('#primaryAction').isDisabled(), true);
          await action('browse');
          assert.equal(await page.locator('#screen').getAttribute('inert'), '');
          await page.keyboard.press('Escape');
          assert.equal(await page.locator('[data-action="browse"]').evaluate(node => node === document.activeElement), true);
          await action('browse');
          await action('pick-folder');
          assert.equal((await state()).path, 'D:\\Apps\\Vex');
          await page.locator('#desktopShortcut').check();
          await page.locator('#fileAssociations').uncheck();
          await action('license');
          assert.ok((await page.locator('.license').innerText()).includes('Permission is hereby granted'));
          await action('modal-dismiss');
          assert.equal(await page.locator('#installPath').inputValue(), 'D:\\Apps\\Vex');
          await page.locator('#customSettings summary').click();
          await action('install');
          assert.equal((await state()).desktop, true);
          assert.equal((await state()).association, false);
          await page.clock.runFor(700);
          await action('cancel');
          assert.equal(await page.locator('[role="dialog"]').count(), 1);
          const pausedProgress = (await state()).progress;
          await page.clock.runFor(1000);
          assert.equal((await state()).progress, pausedProgress);
          await action('modal-dismiss');
          await page.screenshot({ path: path.join(screenshotDirectory, 'Windows-installing.png') });
          await completeOperation('complete');
          await page.screenshot({ path: path.join(screenshotDirectory, 'Windows-complete.png') });
          await action('open-app');
          assert.equal((await state()).launch, true);
          assert.equal(await page.locator('#installerWindow').isVisible(), false);

          await open('?scenario=installed');
          await action('repair');
          await completeOperation('complete');
          assert.equal((await state()).operation, 'repair');
          await open('?scenario=installed');
          await action('remove');
          await page.locator('#keepData').uncheck();
          await action('uninstall');
          await completeOperation('removed');

          await open('?scenario=error');
          await action('install');
          await page.clock.runFor(2000);
          assert.equal((await state()).step, 'error');
          await action('retry');
          await completeOperation('complete');

          await open();
          await action('install');
          await action('window-close');
          await action('stop');
          assert.equal((await state()).step, 'welcome');
          assert.equal((await state()).installed, false);
        } else if (platform === 'linux') {
          assert.equal(await page.locator('.wizard,.dmg-items').count(), 0);
          await action('linux-install');
          assert.equal(await page.locator('#previewPassword').getAttribute('readonly'), '');
          await page.keyboard.press('Escape');
          assert.equal((await state()).step, 'welcome');
          await action('linux-install');
          await action('authorize');
          await action('cancel');
          await action('stop');
          assert.equal((await state()).installed, false);
          await action('linux-install');
          await action('authorize');
          await completeOperation('complete');
          await action('linux-remove');
          assert.ok((await page.locator('.dialog p').innerText()).includes('移除'));
          await action('authorize');
          await action('cancel');
          await action('stop');
          assert.equal((await state()).installed, true);
          await action('linux-remove');
          await action('authorize');
          await completeOperation('removed');
          assert.equal((await state()).installed, false);

          await open('?scenario=error');
          await action('linux-install');
          await action('authorize');
          await page.clock.runFor(2000);
          assert.equal((await state()).step, 'error');
          await action('retry');
          await action('authorize');
          await completeOperation('complete');
        } else {
          assert.equal(await page.locator('.wizard,.linux-content,[data-action="mode-pkg"]').count(), 0);
          await page.locator('#dmgApp').dragTo(page.locator('#applicationsTarget'));
          assert.equal((await state()).step, 'copying');
          await action('cancel');
          await action('stop');
          await page.locator('#dmgApp').focus();
          await page.keyboard.press('Enter');
          await completeOperation('complete');
          await action('copy');
          assert.equal((await state()).modal, 'replace');
          await action('modal-dismiss');
          await action('copy');
          await action('replace');
          await completeOperation('complete');
          await action('eject');
          assert.equal(await page.locator('#installerWindow').isVisible(), false);

          await open('?scenario=error');
          await action('copy');
          await page.clock.runFor(2000);
          assert.equal((await state()).modal, 'copy-error');
          await action('retry');
          await completeOperation('complete');
        }
        await open('?lang=en&theme=dark');
        assert.equal(await page.locator('html').getAttribute('lang'), 'en');
        assert.equal(await page.locator('html').getAttribute('data-theme'), 'dark');
        assert.equal(await page.locator('.slogan,.hero-tagline,.eyebrow').innerText(), 'Vex · Write your thoughts. See the connections.');
        await fixture.verify(page);
        assert.deepEqual(errors, [], `${name}: no browser exceptions`);
        console.log(`Passed: ${name} standalone layout, installation, cancellation, retry, and platform isolation.`);
      } finally {
        await page.close();
        fixture.dispose();
      }
    }
    console.log(`Installer screenshots: ${screenshotDirectory}`);
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
