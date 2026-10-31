/* 原型侧铬面量表：与 scripts/ui-verification 的 chrome-metrics 模式逐行同格式（位置、尺寸、内距、
 * 外距、圆角、描边、底色、字号、字重、子元素间距），两边输出可直接 diff 出差距。
 * 视口 1440×900、坐标折算到 .app-window 左上角（实现侧折算到窗口左上角，两者只差原型那 1px 窗框）。
 * 用法：node design/scripts/preview.cjs 起服务后
 *   NODE_PATH=design/tests/node_modules node design/tests/probe-chrome-metrics.cjs
 */
const { chromium } = require('playwright');

const base = process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/';

// 与实现侧 ChromeMetricsProbe 同序同名：左列是表里的标签，右列是原型选择器。
const ROWS = [
  ['.titlebar', '.titlebar'],
  ['.brand', '.brand'],
  ['.tb-menu', '.tb-menu'],
  ['.tb-menuitem', '.tb-menuitem'],
  ['.nav-rail', '.nav-rail'],
  ['.rail-item', '.nav-rail .rail-item'],
  ['.rail-item .ico', '.rail-item .ico'],
  ['.sidebar', '.sidebar'],
  ['.panel-head', '.sidebar .panel-head'],
  ['.panel-title', '.sidebar .panel-title'],
  ['.sidebar-search', '.sidebar-search'],
  ['.search-frame', '.sidebar-search'],
  ['.file-list', '.file-list'],
  ['.file-row', '.file-row'],
  ['.file-row .ico', '.file-row .ico'],
  ['.files-footer', '.files-footer'],
  ['.workspace-main', '.workspace-main'],
  ['.find-strip', '.find-strip'],
  ['.find-strip .input', '.find-strip .input'],
  ['.find-strip .search-frame', '.find-strip .input'],
  ['.find-strip .btn.sm', '.find-strip .btn.sm'],
  ['.find-strip .icon-button', '.find-strip .icon-button'],
  ['.workspace-tabrow', '.workspace-tabrow'],
  ['.workspace-tabs', '.workspace-tabs'],
  ['.workspace-tabs > button', '.workspace-tabs > button'],
  ['.typography-trigger', '.workspace-tabrow .typography-trigger'],
  ['.workspace-viewswitch-more', '.workspace-viewswitch-more'],
  ['#edit-options', '#edit-options'],
  ['.format-button', '#edit-options .format-button'],
  ['.document-statusbar', '.document-statusbar'],
  ['.document-word-count', '.document-word-count'],
  ['.statusbar-zoom', '.statusbar-zoom'],
  ['.structure-sidebar', '.structure-sidebar'],
  ['.structure-item', '.structure-item'],
  ['.workspace-layout-menu', '.workspace-layout-menu'],
  ['#typography-panel', '#typography-panel'],
  ['.typography-grid', '#typography-panel .typography-grid'],
  ['.typography-card', '#typography-panel .typography-card'],
  ['.density-options', '#typography-panel .typography-density'],
  ['.density-options > button', '#typography-panel .typography-density button'],
  ['#map-settings', '#map-settings'],
  ['#node-inspector', '#node-inspector'],
  ['.inspector-head', '.inspector-head'],
  ['.inspector-foot', '.inspector-foot'],
];

const collect = page => page.evaluate(arg => {
  const num = v => Math.round(parseFloat(v) * 10) / 10;
  const box = s => [num(s['padding-top']), num(s['padding-right']), num(s['padding-bottom']), num(s['padding-left'])].join(' ');
  const marg = s => [num(s['margin-top']), num(s['margin-right']), num(s['margin-bottom']), num(s['margin-left'])].join(' ');
  const bor = s => [num(s['border-top-width']), num(s['border-right-width']), num(s['border-bottom-width']), num(s['border-left-width'])].join(' ');
  const frame = document.querySelector('.app-window').getBoundingClientRect();
  const lines = [];
  for (const [label, sel] of arg) {
    const el = document.querySelector(sel);
    const style = el && getComputedStyle(el);
    const rect = el && el.getBoundingClientRect();
    if (!el || !rect || rect.width < 0.5 || rect.height < 0.5 || style.display === 'none' || style.visibility === 'hidden') {
      lines.push(`MISS\t${label}`);
      continue;
    }
    const kids = Array.from(el.children).filter(c => c.getBoundingClientRect().width > 0.5 && c.getBoundingClientRect().height > 0.5);
    const horizontal = kids.length > 1 && kids[kids.length - 1].getBoundingClientRect().right - kids[0].getBoundingClientRect().left
      >= kids[kids.length - 1].getBoundingClientRect().bottom - kids[0].getBoundingClientRect().top;
    const gaps = [];
    for (let i = 1; i < kids.length; i++) {
      const a = kids[i - 1].getBoundingClientRect(), b = kids[i].getBoundingClientRect();
      gaps.push(num(horizontal ? b.left - a.right : b.top - a.bottom));
    }
    const radius = style.borderRadius.split(' ')[0];
    lines.push([
      label, 'vis',
      `x${num(rect.left - frame.left)}`, `y${num(rect.top - frame.top)}`,
      `w${num(rect.width)}`, `h${num(rect.height)}`,
      `r${num(frame.right - rect.right)}`, `b${num(frame.bottom - rect.bottom)}`,
      `pad[${box(style)}]`, `marg[${marg(style)}]`, `rad[${num(radius)}]`, `bor[${bor(style)}]`,
      `bg[${style.backgroundColor}]`, `fs[${num(style.fontSize)}]`, `fw[${style.fontWeight}]`,
      `gap[${style.gap === 'normal' ? '-' : num(style.gap)}]`, `cg[${gaps.join(' ')}]`,
    ].join('\t'));
  }
  return lines.join('\n');
}, ROWS);

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  const click = async sel => page.evaluate(s => { const e = document.querySelector(s); if (e) e.click(); }, sel);
  const open = async query => {
    await page.goto(base + 'Vex.html?preview=1&device=desktop' + query);
    await page.waitForFunction(() => window.VexWorkspace && window.VexChapters);
    await page.waitForTimeout(500);
  };
  const dump = async state => { console.log(`### ${state}`); console.log(await collect(page)); console.log(''); };

  await open('&layout=read');
  await dump('read layout, chrome closed');
  await click('[data-action="find"]'); await page.waitForTimeout(260);
  await dump('find strip open');
  await click('[data-action="find"]'); await page.waitForTimeout(260);
  await click('#workspace-layout'); await page.waitForTimeout(200);
  await dump('layout menu open');
  await click('#workspace-layout'); await page.waitForTimeout(200);
  await click('[data-action="typography"]'); await page.waitForTimeout(300);
  await dump('typography panel open');
  await page.keyboard.press('Escape'); await page.waitForTimeout(300);
  await click('#outline-toggle'); await page.waitForTimeout(700);
  await dump('outline open');
  await click('.tb-menuitem'); await page.waitForTimeout(200);
  console.log('### title menu flyout');
  console.log(await page.evaluate(() => {
    const frame = document.querySelector('.app-window').getBoundingClientRect();
    const row = (label, el) => {
      if (!el || !el.getBoundingClientRect().width) return `MISS\t${label}`;
      const r = el.getBoundingClientRect(), s = getComputedStyle(el);
      const n = v => Math.round(parseFloat(v) * 10) / 10;
      return [label, 'vis', `x${n(r.left - frame.left)}`, `y${n(r.top - frame.top)}`, `w${n(r.width)}`, `h${n(r.height)}`,
        `pad[${[s['padding-top'], s['padding-right'], s['padding-bottom'], s['padding-left']].map(n).join(' ')}]`,
        `rad[${n(s.borderRadius.split(' ')[0])}]`, `bor[${[s['border-top-width'], s['border-right-width'], s['border-bottom-width'], s['border-left-width']].map(n).join(' ')}]`,
        `bg[${s.backgroundColor}]`, `fs[${n(s.fontSize)}]`, `fw[${s.fontWeight}]`].join('\t');
    };
    return [
      row('.flyout', document.querySelector('.tb-dropdown')),
      row('.menu-item', document.querySelector('.tb-dropdown .menu-item')),
      row('  .menu-item text', document.querySelector('.tb-dropdown .menu-item')),
      row('.menu-sep', document.querySelector('.tb-dropdown .menu-sep')),
    ].join('\n');
  }));

  await open('&layout=map');
  await click('#map-settings-button'); await page.waitForTimeout(240);
  await dump('map layout, settings open');
  await click('#map-settings-button'); await page.waitForTimeout(240);
  await click('.map-node'); await page.waitForTimeout(240);
  await dump('map layout, node selected');
  await browser.close();
})().catch(e => { console.error('FAIL:', e.message); process.exit(1); });
