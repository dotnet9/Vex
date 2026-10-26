// 开发期生成跨平台品牌图标；正式打包直接读取仓库内的离线资源。
const fs = require('node:fs/promises');
const path = require('node:path');
const sharp = require('sharp');
async function main() {
  const root = path.resolve(__dirname, '..');
  const svg = path.join(root, 'assets', 'logo.svg');
  await sharp(svg).resize(256, 256).png().toFile(path.join(root, 'assets', 'logo.png'));
  const sizes = [16, 24, 32, 48, 64, 128, 256];
  const frames = await Promise.all(sizes.map(size => sharp(svg).resize(size, size).png().toBuffer()));
  const header = Buffer.alloc(6 + sizes.length * 16);
  header.writeUInt16LE(1, 2); header.writeUInt16LE(sizes.length, 4);
  let offset = header.length;
  sizes.forEach((size, i) => {
    const at = 6 + i * 16;
    header[at] = size % 256; header[at + 1] = size % 256;
    header.writeUInt16LE(1, at + 4); header.writeUInt16LE(32, at + 6);
    header.writeUInt32LE(frames[i].length, at + 8); header.writeUInt32LE(offset, at + 12);
    offset += frames[i].length;
  });
  await fs.writeFile(path.join(root, 'assets', 'logo.ico'), Buffer.concat([header, ...frames]));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
