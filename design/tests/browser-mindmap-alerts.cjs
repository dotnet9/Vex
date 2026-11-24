const { chromium } = require('playwright');
const assert = require('node:assert/strict');

(async () => {
  const browser = await chromium.launch({ headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    await page.goto((process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/') + 'Vex.html?preview=1&device=desktop');
    await page.waitForFunction(() => window.VexWorkbench && window.VexTheme);
    await page.addStyleTag({ content: '*,*::before,*::after{transition:none!important;animation:none!important}' });
    const result = await page.evaluate(() => {
      VexWorkbench.applyText('# Root\n\n> [!NOTE]\n> Note body\n\n> [!TIP]\n> Tip body\n\n> [!IMPORTANT]\n> Important body\n\n> [!WARNING]\n> Warning body\n\n> [!CAUTION]\n> Caution body\n\n## Branch');
      VexWorkbench.settings.paragraphs = true;
      VexWorkbench.setView('mindmap');
      VexWorkbench.renderMap();
      const alerts = [...document.querySelectorAll('.map-node[data-kind="alert"]')];
      const parsed = alerts.map(element => ({
        kind: element.dataset.alertKind,
        text: element.querySelector('.map-node-text').textContent,
        border: getComputedStyle(element).borderTopWidth,
        color: getComputedStyle(element).borderTopColor
      }));
      const legend = {
        content: !document.querySelector('.content-legend').hidden,
        alert: !document.querySelector('.alert-legend').hidden
      };
      const styles = [];
      for (const mode of ['light', 'dark']) {
        VexTheme.setMode(mode);
        for (const style of ['quiet', 'colorful', 'outline', 'capsule', 'underline', 'mono']) {
          document.querySelector('#map-scene').dataset.mapStyle = style;
          VexWorkbench.renderMap();
          styles.push(...[...document.querySelectorAll('.map-node[data-kind="alert"]')]
            .map(element => getComputedStyle(element).borderTopWidth));
        }
      }
      VexWorkbench.settings.paragraphs = false;
      VexWorkbench.settings.lists = false;
      VexWorkbench.renderMap();
      return {
        parsed,
        styles,
        legend,
        hiddenCount: document.querySelectorAll('.map-node[data-kind="alert"]').length,
        hiddenLegend: {
          content: document.querySelector('.content-legend').hidden,
          alert: document.querySelector('.alert-legend').hidden
        }
      };
    });
    assert.deepEqual(result.parsed.map(alert => alert.kind), ['note', 'tip', 'important', 'warning', 'caution']);
    assert.ok(result.parsed.every(alert => !alert.text.includes('[!')));
    assert.ok(result.parsed.every(alert => alert.border === '2px'));
    assert.notEqual(result.parsed[1].color, result.parsed[3].color);
    assert.notEqual(result.parsed[3].color, result.parsed[4].color);
    assert.ok(result.styles.every(width => width === '2px'));
    assert.deepEqual(result.legend, { content: true, alert: true });
    assert.equal(result.hiddenCount, 0);
    assert.deepEqual(result.hiddenLegend, { content: true, alert: true });
    console.log('PASS mind-map alerts parse, render with semantic borders in 12 mode/style combinations, and obey visibility settings');
  } finally {
    await browser.close();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
