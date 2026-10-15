const fs = require('node:fs');
const path = require('node:path');
const { pathToFileURL } = require('node:url');
const { chromium } = require('playwright');

(async () => {
  const output = path.resolve(__dirname, '../../artifacts/parity/prototype');
  fs.mkdirSync(output, { recursive: true });
  const browser = await chromium.launch({ headless: true });
  const url = pathToFileURL(path.resolve(__dirname, '../index.html')).href;
  const records = [];
  try {
    for (const width of [1280, 980]) {
      const context = await browser.newContext({ viewport: { width: width + 48, height: (width === 1280 ? 820 : 640) + 48 }, reducedMotion: 'reduce' });
      const page = await context.newPage();
      for (const theme of ['light', 'dark']) {
        for (const [view, edit] of [['edit', 'instant'], ['edit', 'source'], ['read', 'instant'], ['mindmap', 'instant']]) {
          await page.goto(`${url}?view=${view}&edit=${edit}&palette=aurora&theme=${theme}`);
          await page.waitForTimeout(120);
          const name = `${width}-${theme}-${view}-${edit}`;
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
          if (width === 1280 && theme === 'light' && view === 'edit' && edit === 'instant') {
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
          }
          if (width === 1280 && theme === 'light' && view === 'mindmap') {
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
    const documents = path.resolve(output, '../documents');
    fs.mkdirSync(path.join(documents, '笔记'), { recursive: true });
    for (const name of fs.readdirSync(output).filter(name => name.endsWith('.md'))) {
      fs.copyFileSync(path.join(output, name), path.join(documents, name === '主题设计笔记.md' ? '笔记/' + name : name));
    }
    fs.writeFileSync(path.join(output, 'metrics.json'), JSON.stringify(records, null, 2));
    console.log('Prototype parity captures: ' + output);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
