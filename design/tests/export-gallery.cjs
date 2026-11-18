// 图鉴缩略图导出（一次性工具）：mermaid.render 直接出 SVG（htmlLabels:false 走原生 <text>，
// SvgImage 可渲染；默认主题输出 #hex 字面量）→ <style> class 颜色规则内联到元素 → 删 style 块。
// 产物 src/Vex/Assets/MermaidGallery/*.svg，由 Vex 端后台线程预解析+缓存（弹出即现）。
const { chromium } = require('playwright');
const OUT = 'E:/github/Apps/Vex/src/Vex/Assets/MermaidGallery';

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 1280, height: 1000 }, deviceScaleFactor: 2 });
  await page.goto('http://127.0.0.1:4319/design/Vex.html?preview=1', { waitUntil: 'networkidle' });
  await page.waitForTimeout(2000);

  // 独立 mermaid 初始化：默认主题（#ECECFF 节点），htmlLabels:false 走原生 text（SvgImage 兼容）。
  await page.evaluate(() => {
    mermaid.initialize({
      startOnLoad: false,
      securityLevel: 'loose',
      theme: 'default',
      fontFamily: '"Segoe UI", "Microsoft YaHei UI", sans-serif',
      flowchart: { htmlLabels: false },
      sequence: { mirrorActors: false },
    });
  });

  const items = await page.evaluate(() => (window.VexMermaid ? VexMermaid.items.map(i => ({ key: i.key, source: i.source })) : []));
  let count = 0;
  for (const item of items) {
    const svg = await page.evaluate(async ({ key, source }) => {
      const { svg: raw } = await mermaid.render('exp-' + key, source);
      const host = document.createElement('div');
      host.style.display = 'none';
      host.innerHTML = raw;
      document.body.appendChild(host); // style.sheet 需要元素在文档里才生成
      const styleEl = host.querySelector('style');
      // <style> 里 class 颜色规则（fill/stroke/color）展开为元素 style 属性。
      if (styleEl && styleEl.sheet) {
        for (const rule of styleEl.sheet.cssRules) {
          if (!rule.selectorText || !rule.style || !rule.selectorText.includes('.') || rule.selectorText.includes('@')) continue;
          const props = [];
          for (const prop of rule.style) {
            if (!['fill', 'stroke', 'color'].includes(prop)) continue;
            const value = rule.style.getPropertyValue(prop).trim();
            if (value) props.push([prop, value]);
          }
          if (!props.length) continue;
          let targets;
          try { targets = host.querySelectorAll(rule.selectorText); } catch { continue; }
          for (const target of targets) {
            let inline = '';
            for (const [name, value] of props) inline += name + ':' + value + ';';
            target.setAttribute('style', (target.getAttribute('style') || '') + ';' + inline);
          }
        }
      }
      // foreignObject（HTML label，SvgImage 不渲染）→ <text>：位置取 foreignObject 中心，颜色取父链文字色。
      host.querySelectorAll('foreignObject').forEach(fo => {
        const label = (fo.querySelector('div')?.textContent || '').trim();
        const width = parseFloat(fo.getAttribute('width') || '0');
        const height = parseFloat(fo.getAttribute('height') || '0');
        const colorMatch = (fo.closest('g')?.getAttribute('style') || '').match(/color:\s*([^;]+)/);
        const color = colorMatch ? colorMatch[1].trim() : 'rgb(51, 51, 51)';
        const text = document.createElementNS('http://www.w3.org/2000/svg', 'text');
        text.setAttribute('x', (width / 2).toFixed(1));
        text.setAttribute('y', (height / 2 + 5).toFixed(1));
        text.setAttribute('text-anchor', 'middle');
        text.setAttribute('fill', color);
        text.setAttribute('font-size', '14');
        text.setAttribute('font-family', '"Segoe UI", "Microsoft YaHei UI", sans-serif');
        text.textContent = label;
        fo.replaceWith(text);
      });
      // 删 style 块（颜色已内联；字体/阴影 SvgImage 不解析）。
      styleEl?.remove();
      let result = host.innerHTML;
      if (!/<svg[^>]*xmlns=/.test(result)) result = result.replace('<svg', '<svg xmlns="http://www.w3.org/2000/svg"');
      host.remove();
      return result;
    }, item);

    const fs = require('node:fs');
    fs.writeFileSync(OUT + '/' + item.key + '.svg', svg, 'utf8');
    count++;
    console.log('svg:', item.key, svg.length);
  }
  await browser.close();
  console.log('exported:', count, 'svgs to', OUT);
  process.exit(count === 24 ? 0 : 1);
})();
