// Reproducible HTML reference images; never used by the production installer.
const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { chromium } = require('../../../design/tests/node_modules/playwright');

(async () => {
  const output = path.resolve(process.argv[2]);
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: true, executablePath: process.env.VEX_BROWSER_EXECUTABLE || undefined });
  try {
    for (const language of ['zh', 'en']) for (const theme of ['light', 'dark']) {
      const page = await browser.newPage({ viewport: { width: 1000, height: 900 }, reducedMotion: 'reduce' });
      await page.clock.install();
      const open = async (scenario = 'new') => {
        await page.goto(pathToFileURL(path.resolve(__dirname, '../../../design/Vex-Installer-Windows.html')).href + `?lang=${language}&theme=${theme}&scenario=${scenario}`);
        await page.locator('body').evaluate(n => { n.style.cssText = 'display:block;padding:0;margin:0;min-height:0'; });
        await page.locator('.brand-link').evaluate(n => { n.textContent = 'CodeWF · 1.0.0'; });
      };
      const capture = async name => {
        await page.locator('#installerWindow').screenshot({ path: path.join(output, `${language}-${theme}-${name}.png`) });
      };
      await open(); await capture('welcome');
      await page.locator('[data-action=license]').click(); await capture('license');
      await page.locator('[data-action=modal-dismiss]').click();
      await page.locator('summary').click(); await capture('custom');
      await page.locator('summary').click();
      await page.locator('[data-action=install]').click();
      await page.clock.runFor(4000); await capture('complete');
      await open(); await page.locator('[data-action=install]').click();
      await page.evaluate(() => { state.progress = 100; render(); });
      await capture('progress');
      await open('installed'); await capture('maintenance');
      await page.close();
    }
  } finally { await browser.close(); }
  console.log(`HTML references: ${output}`);
})().catch(error => { console.error(error); process.exitCode = 1; });
