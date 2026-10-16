// 仅用于生成离线安装资源；正式打包直接读取 PNG。
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require('playwright');
const copies = {
  zh: ['维刻 Vex · 写下想法，看见脉络。', '拖入 Applications，即可安装'],
  en: ['Vex · Write your thoughts. See the connections.', 'Drag to Applications to install'],
  bilingual: ['维刻 Vex · 写下想法，看见脉络。', '拖入 Applications · Drag to Applications'],
};
function svg([slogan, instruction]) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="600" height="380" viewBox="0 0 600 380">
<rect width="600" height="380" fill="#FAF9FC"/>
<g font-family="Segoe UI,Microsoft YaHei,Arial,sans-serif" text-anchor="middle">
  <text x="300" y="62" fill="#262137" font-size="30" font-weight="600">Vex</text>
  <text x="300" y="95" fill="#706780" font-size="13">${slogan}</text>
  <text x="300" y="320" fill="#706780" font-size="13">${instruction}</text>
  <text x="300" y="359" fill="#91889F" font-size="10">CodeWF</text>
</g>
<g fill="none" stroke="#A899C4" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
  <path d="M275 210H325M317 202L325 210L317 218"/>
</g>
<text x="578" y="359" text-anchor="end" fill="#91889F" font-size="9" font-family="Segoe UI,Arial,sans-serif">codewf.com</text>
</svg>`;
}
(async () => {
  const browser = await chromium.launch({headless: true, executablePath: process.env.VEX_BROWSER_EXECUTABLE || undefined});
  try {
    const page = await browser.newPage({viewport:{width:600,height:380},deviceScaleFactor:1});
    for (const [language, copy] of Object.entries(copies)) {
      const source = svg(copy);
      fs.writeFileSync(path.join(__dirname, `background-${language}.svg`), source + '\n');
      await page.setContent('<style>html,body{margin:0;width:600px;height:380px;overflow:hidden}</style>' + source);
      await page.screenshot({path:path.join(__dirname, `background-${language}.png`)});
    }
  } finally { await browser.close(); }
})().catch(error => {console.error(error); process.exitCode = 1;});
