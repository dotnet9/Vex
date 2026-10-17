const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');

(async () => {
  const output = path.join(process.env.CODEWF_TEMP_ROOT || 'D:/github/temp_files', 'vex-native/parity/prototype');
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: true, executablePath: process.env.VEX_BROWSER_EXECUTABLE || undefined });
  const url = pathToFileURL(path.resolve(__dirname, '../Vex.html')).href;
  const records = [];
  try {
    for (const width of [1280, 980]) {
      const height = width === 1280 ? 820 : 640;
      const inset = width < 1100 ? 24 : 48;
      const context = await browser.newContext({ viewport: { width: width + inset, height: height + inset }, reducedMotion: 'reduce' });
      const page = await context.newPage();
      for (const theme of ['light', 'dark']) {
        for (const layout of ['instant','source','read','map','source-render','source-map','instant-map','three']) {
          await page.goto(`${url}?layout=${layout}&palette=aurora&theme=${theme}`);
          await page.waitForTimeout(180);
          await page.locator('#workspace-folder').evaluate(el => el.textContent = '笔记');
          const bounds = await page.locator('.app-window').boundingBox();
          if (!bounds || Math.abs(bounds.width - width) > 1 || Math.abs(bounds.height - height) > 1) {
            throw new Error(`Unexpected prototype size: ${JSON.stringify(bounds)}; expected ${width} x ${height}`);
          }
          const name = `${width}-${theme}-${layout}`;
          await page.locator('.app-window').screenshot({ path: path.join(output, name + '.png') });
          records.push(await page.evaluate(({ name }) => {
            const windowRect = document.querySelector('.app-window').getBoundingClientRect();
            const result = { name, elements: {} };
            for (const selector of ['.titlebar', '.workbar', '.sidebar', '.edit-options', '.pane-head', '.map-toolbar', '.statusbar', '.view-switch']) {
              const el = document.querySelector(selector), r = el?.getBoundingClientRect();
              if (r && r.width && r.height) result.elements[selector] = { x: r.x - windowRect.x, y: r.y - windowRect.y, width: r.width, height: r.height };
            }
            return result;
          }, { name }));
          if (width === 1280 && theme === 'light' && layout === 'instant') {
            fs.writeFileSync(path.join(output, '主题设计笔记.md'), await page.locator('.source-scroll textarea').inputValue());

            for (let i = 0; i < await page.locator('.file-row').count(); i++) {
              await page.locator('.file-row').nth(i).click();
              const rowText = await page.locator('.file-row').nth(i).textContent();
              const filename = /[^\s]+\.md/.exec(rowText)?.[0];
              if (filename) fs.writeFileSync(path.join(output, filename), await page.locator('.source-scroll textarea').inputValue());
            }
            await page.locator('.file-row').first().click();
            await page.locator('[data-action="typography"]').first().click();
            await page.locator('.app-window').screenshot({ path: path.join(output, 'typography.png') });
            await page.keyboard.press('Escape');
            await page.locator('#workspace-layout').click();
            await page.locator('.app-window').screenshot({ path: path.join(output, 'layouts.png') });
            await page.keyboard.press('Escape');
            const code = page.locator('#instant-content .md-code-block').first();
            await code.scrollIntoViewIfNeeded();
            await page.locator('.app-window').screenshot({ path: path.join(output, 'code-display.png') });
            await code.locator('code').click();
            await page.locator('.app-window').screenshot({ path: path.join(output, 'code-edit.png') });
            await page.keyboard.press('Escape');
            await page.evaluate(() => VexProto.handleAction('dlg-about'));
            await page.locator('.app-window').screenshot({ path: path.join(output, 'about.png') });
            await page.locator('#layer-about [data-close]').click();
            await page.evaluate(() => VexProto.handleAction('dlg-doc', { arg: '鸣谢' }));
            await page.locator('.app-window').screenshot({ path: path.join(output, 'thanks.png') });
            await page.keyboard.press('Escape');
          }
          if (width === 1280 && theme === 'light' && layout === 'map') {
            await page.locator('#map-settings-button').click();
            await page.locator('.app-window').screenshot({ path: path.join(output, 'map-settings.png') });
            await page.locator('[data-action="close-settings"]').click();
            await page.locator('.map-node.content .map-node-button').first().click({ force: true });
            await page.locator('.app-window').screenshot({ path: path.join(output, 'map-inspector.png') });
          }
        }
      }
      await context.close();
    }
    const documents = path.resolve(output, '../笔记');
    fs.mkdirSync(documents, { recursive: true });
    for (const name of fs.readdirSync(output).filter(name => name.endsWith('.md'))) {
      fs.copyFileSync(path.join(output, name), path.join(documents, name));
    }
    fs.writeFileSync(path.join(output, 'metrics.json'), JSON.stringify(records, null, 2));
    console.log('Prototype parity captures: ' + output);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
