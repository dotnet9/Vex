/* 计算样式快照：整理前后逐元素对比，证明零视觉回归。
 * 用法：node design/scripts/preview.cjs（或复用已跑的 4317）
 *   NODE_PATH="E:/github/Libs/CodeWF.Avalonia/design/tests/node_modules" node design/tests/snapshot-styles.cjs <输出json路径> [场景URL参数]
 */
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');

const base = process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/';
const output = path.resolve(process.argv[2] || path.join(__dirname, '../../artifacts/styles-snapshot.json'));
const query = process.argv[3] || '?device=desktop&layout=instant&theme=light';

const PROPS = [
  'display', 'position', 'visibility', 'opacity', 'z-index',
  'width', 'height', 'margin-top', 'margin-right', 'margin-bottom', 'margin-left',
  'padding-top', 'padding-right', 'padding-bottom', 'padding-left',
  'border-top-width', 'border-right-width', 'border-bottom-width', 'border-left-width',
  'border-top-style', 'border-color', 'border-radius',
  'color', 'background-color', 'background-image',
  'font-family', 'font-size', 'font-weight', 'font-style', 'line-height', 'letter-spacing', 'word-spacing',
  'text-align', 'text-decoration-line', 'text-transform', 'white-space', 'text-overflow', 'overflow', 'overflow-x', 'overflow-y',
  'flex-direction', 'flex-wrap', 'flex-grow', 'flex-shrink', 'flex-basis',
  'grid-template-columns', 'grid-template-rows', 'gap', 'row-gap', 'column-gap',
  'justify-content', 'align-items', 'align-content',
  'top', 'right', 'bottom', 'left', 'transform', 'box-shadow', 'cursor',
];

(async () => {
  const browser = await chromium.launch();
  const vp = (process.env.VEX_VIEWPORT||'1440x900').split('x').map(Number); const page = await browser.newPage({ viewport: { width: vp[0], height: vp[1] } });
  await page.goto(base + 'Vex.html' + query, { waitUntil: 'networkidle' });
  await page.waitForTimeout(1200);
  const snapshot = await page.evaluate((props) => {
    const out = [];
    const walker = document.createTreeWalker(document.documentElement, NodeFilter.SHOW_ELEMENT);
    let node; let index = 0;
    const pathOf = el => {
      const parts = [];
      for (let cur = el; cur && cur !== document.documentElement; cur = cur.parentElement) {
        const parent = cur.parentElement;
        const sib = parent ? Array.from(parent.children) : [];
        parts.unshift(`${cur.tagName.toLowerCase()}${sib.length > 1 ? '[' + (sib.indexOf(cur) + 1) + ']' : ''}`);
      }
      return parts.join('/');
    };
    while ((node = walker.nextNode())) {
      if (node.tagName === 'SCRIPT' || node.tagName === 'LINK' || node.tagName === 'META' || node.tagName === 'TITLE') continue;
      const cs = getComputedStyle(node);
      const rec = { i: index++, p: pathOf(node), id: node.id || undefined, s: {} };
      for (const prop of props) rec.s[prop] = cs.getPropertyValue(prop);
      if (node.tagName === 'SVG' || node.closest('svg')) for (const prop of ['stroke', 'stroke-width', 'fill']) rec.s[prop] = cs.getPropertyValue(prop);
      const text = node.childNodes.length && Array.from(node.childNodes).filter(n => n.nodeType === 3).map(n => n.textContent.trim()).join(' ');
      if (text) rec.t = text.slice(0, 80);
      out.push(rec);
    }
    return out;
  }, PROPS);
  fs.mkdirSync(path.dirname(output), { recursive: true });
  fs.writeFileSync(output, JSON.stringify(snapshot));
  console.log('snapshot elements=' + snapshot.length + ' -> ' + output);
  await browser.close();
})();
