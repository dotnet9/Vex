/* i18n 覆盖门禁：en-US 界面壳层不得残留中文（样例文档内容、语言名、版权行、更新日志正文除外）。
 * 覆盖面：基础壳、六个顶部菜单、偏好设置六分类、关于/更新对话框、查找条、three 布局（章节/导图工具条+导图样式弹层）。
 * 用法: node design/tests/browser-i18n-coverage.cjs  （需 Playwright 与 VEX_PREVIEW_URL，默认 http://127.0.0.1:4317/design/） */
const path = require('node:path');
const { chromium } = require('playwright');
const BASE = process.env.VEX_PREVIEW_URL || 'http://127.0.0.1:4317/design/';
const CJK = /[\u3400-\u9fff]/;
const ALLOW_TEXT = new Set(['简体中文', '繁體中文', '日本語', 'English', '中', '繁', '日']);
/* 内容性区域（样例文档、文件名、导图节点、水印示例、更新日志正文等）不算壳层文案。 */
const CONTENT_ROOTS = '.proto-toast,[class*=toast],.instant-scroll,.reading-scroll,.source-scroll,.md-body,.map-stage,#map-viewport,.structure-sidebar,.sidebar-inner,.node-inspector,.vex-watermark-sample,#about-copyright,.lang-chip,#update-list .update-entries span:not(.update-tag),.upd-quote,#upd-pop-body,.document-title,.titlebar-document,.chapter-rename,#upd-pill-wrap .upd-pop,#about-title,.slogan';

function probe() {
  /* 常量内联（本函数序列化到浏览器执行，闭包变量不随身）。 */
  const ALLOW_TEXT = new Set(['简体中文', '繁體中文', '日本語', 'English', '中', '繁', '日']);
  const CONTENT_ROOTS = '.proto-toast,[class*=toast],.instant-scroll,.reading-scroll,.source-scroll,.md-body,.map-stage,#map-viewport,.structure-sidebar,.sidebar-inner,.node-inspector,.vex-watermark-sample,#about-copyright,.lang-chip,#update-list .update-entries span:not(.update-tag),.upd-quote,#upd-pop-body,.document-title,.titlebar-document,.chapter-rename,#upd-pill-wrap .upd-pop,#about-title,.slogan';
  const CJK = /[\u3400-\u9fff]/;
  const violations = [];
  const seen = new Set();
  const inContent = el => el.closest(CONTENT_ROOTS) !== null;
  const isVisible = el => { if (!el.getClientRects().length) return false; const st = getComputedStyle(el); if (st.visibility === 'hidden' || +st.opacity < 0.05) return false; const r = el.getBoundingClientRect(); return r.width >= 1 && r.height >= 1; };
  for (const el of document.querySelectorAll('body *')) {
    if (!isVisible(el) || inContent(el)) continue;
    if (el.closest('.about-rows') && el.children.length) continue;
    const own = [...el.childNodes].filter(n => n.nodeType === 3).map(n => n.textContent).join('').trim();
    const check = (kind, value) => {
      if (!value || !CJK.test(value)) return;
      const text = value.trim();
      if (ALLOW_TEXT.has(text)) return;
      const key = kind + '|' + text;
      if (seen.has(key)) return;
      seen.add(key);
      violations.push({ kind, text: text.slice(0, 50), where: el.id ? '#' + el.id : (el.className && String(el.className).split(' ')[0] || el.tagName) });
    };
    check('text', own);
    check('title', el.getAttribute && el.getAttribute('title'));
    check('aria', el.getAttribute && el.getAttribute('aria-label'));
    check('placeholder', el.getAttribute && el.getAttribute('placeholder'));
  }
  return violations;
}

(async () => {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
  const failures = [];
  const watch = (tag, p) => { p.on('pageerror', e => console.error('  [pageerror:' + tag + '] ' + String(e && e.message || e).slice(0, 120))); };
  watch('instant', page);
  const collect = where => page.evaluate(probe).then(list => list.forEach(v => failures.push(where + ' :: ' + v.kind + ' "' + v.text + '" @' + v.where)));
  await page.goto(BASE + 'Vex.html?preview=1&device=desktop&layout=instant', { waitUntil: 'load' });
  await page.waitForFunction(() => window.VexWorkspace && window.VexL && window.VexPreferences, null, { timeout: 20000 });
  await page.evaluate(() => VexL.set('en-US'));
  await page.waitForTimeout(600);
  await collect('base');
  const menus = await page.locator('.tb-menuitem').count();
  for (let i = 0; i < menus; i++) { await page.locator('.tb-menuitem').nth(i).click(); await page.waitForTimeout(220); await collect('menu' + i); await page.keyboard.press('Escape'); await page.waitForTimeout(120); }
  for (const cat of ['general', 'editor', 'images', 'appearance', 'export', 'mcp']) {
    await page.evaluate(c => VexPreferences.open(c), cat); await page.waitForTimeout(350);
    await collect('pref-' + cat);
    await page.evaluate(() => VexPreferences.close()); await page.waitForTimeout(300);
  }
  const helpIdx = await page.evaluate(() => [...document.querySelectorAll('.tb-menuitem')].findIndex(m => /Help|说明|說明|ヘルプ/.test(m.textContent)));
  if (helpIdx >= 0) {
    await page.locator('.tb-menuitem').nth(helpIdx).click(); await page.waitForTimeout(200);
    const about = page.locator('.menu-item[data-menu-label="关于 Vex"]');
    if (await about.count()) { await about.first().click(); await page.waitForTimeout(450); await collect('dlg-about'); await page.keyboard.press('Escape'); await page.waitForTimeout(300); }
    await page.locator('.tb-menuitem').nth(helpIdx).click(); await page.waitForTimeout(200);
    const upd = page.locator('.menu-item[data-menu-label="检查更新"]');
    if (await upd.count()) { await upd.first().click(); await page.waitForTimeout(700); await collect('dlg-update'); await page.keyboard.press('Escape'); await page.waitForTimeout(300); }
  }
  const findBtn = page.locator('[data-action="find"]').first();
  if (await findBtn.isVisible()) { await findBtn.click(); await page.waitForTimeout(350); await collect('findbar'); await page.keyboard.press('Escape'); }
  /* 同页复用：导航到 three 布局（章节/导图工具条 + 导图样式弹层）。 */
  const page2 = page;
  watch('three', page2);
  await page2.goto(BASE + 'Vex.html?preview=1&device=desktop&layout=three', { waitUntil: 'load' });
  await page2.waitForFunction(() => window.VexWorkspace && window.VexL, null, { timeout: 20000 });
  await page2.evaluate(() => VexL.set('en-US'));
  await page2.waitForTimeout(700);
  await collect('three-base');
  const trigger = page2.locator('.map-appearance-trigger').first();
  if (await trigger.count()) { await trigger.click(); await page2.waitForTimeout(400); await collect('map-appearance'); await page2.keyboard.press('Escape'); }
  await browser.close();
  if (failures.length) {
    console.error('i18n 覆盖未通过（' + failures.length + ' 处中文残留）：');
    failures.forEach(f => console.error('  ' + f));
    process.exit(1);
  }
  console.log('PASS: en-US 界面壳层无中文残留（菜单/偏好/对话框/查找条/章节与导图工具条）');
})().catch(e => { console.error(e); process.exit(1); });
