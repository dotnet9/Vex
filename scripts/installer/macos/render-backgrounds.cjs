// 仅用于生成离线安装资源；正式打包直接读取 PNG。
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
const copies = {
  zh: ['把维刻，放进你的应用程序。', '将左侧图标拖到右侧「应用程序」。', '复制完成后，从「应用程序」打开维刻，并推出此磁盘映像。'],
  en: ['Move Vex into your applications.', 'Drag the icon on the left to Applications.', 'After copying, open Vex from Applications and eject this disk image.'],
  bilingual: ['把维刻，放进你的应用程序。', 'Drag Vex to Applications · 拖入右侧应用程序', 'Open from Applications, then eject · 复制完成后打开维刻并推出磁盘映像'],
};
function svg([title, description, instruction]) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="740" height="406" viewBox="0 0 740 406">
<defs><radialGradient id="paper" cx="15%" cy="100%" r="75%"><stop stop-color="#24766B" stop-opacity=".05"/><stop offset="1" stop-color="#FFFFFF" stop-opacity="0"/></radialGradient></defs>
<rect width="740" height="406" fill="#FFFFFF"/><rect width="740" height="406" fill="url(#paper)"/>
<g font-family="Microsoft YaHei UI,PingFang SC,Segoe UI,Arial,sans-serif" text-anchor="middle">
  <text x="370" y="78" fill="#253530" font-size="28" font-weight="500">${title}</text>
  <text x="370" y="120" fill="#747C78" font-size="14">${description}</text>
  <text x="370" y="365" fill="#747C78" font-size="12">${instruction}</text>
  <text x="370" y="395" fill="#747C78" font-size="10">Vex · CodeWF</text>
</g>
<g fill="none" stroke="#A1AAA6" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
  <path d="M351 226H389M381 218L389 226L381 234"/>
</g>
<text x="718" y="395" text-anchor="end" fill="#747C78" font-size="9" font-family="Segoe UI,Arial,sans-serif">codewf.com</text>
</svg>`;
}
(async () => {
    for (const [language, copy] of Object.entries(copies)) {
      const source = svg(copy);
      fs.writeFileSync(path.join(__dirname, `background-${language}.svg`), source + '\n');
      await sharp(Buffer.from(source)).png().toFile(path.join(__dirname, `background-${language}.png`));
    }
})().catch(error => {console.error(error); process.exitCode = 1;});
