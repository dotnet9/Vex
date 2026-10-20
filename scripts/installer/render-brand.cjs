// 开发期从原型 SVG 生成 Inno 可读取的位图；安装包只嵌入离线 BMP。
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');
async function main() {
  for (const [theme, background] of Object.entries({light:'#e8eee5', dark:'#243a2d'})) {
    const size = 224, stride = size * 3;
    const rgb = await sharp(path.join(__dirname, '../../design/assets/vex-logo.svg'))
      .resize(size, size).flatten({background}).removeAlpha().raw().toBuffer();
    const bmp = Buffer.alloc(54 + stride * size);
    bmp.write('BM'); bmp.writeUInt32LE(bmp.length, 2); bmp.writeUInt32LE(54, 10);
    bmp.writeUInt32LE(40, 14); bmp.writeInt32LE(size, 18); bmp.writeInt32LE(size, 22);
    bmp.writeUInt16LE(1, 26); bmp.writeUInt16LE(24, 28); bmp.writeUInt32LE(stride * size, 34);
    for (let y=0; y<size; y++) for (let x=0; x<size; x++) {
      const from=(y*size+x)*3, to=54+(size-1-y)*stride+x*3;
      bmp[to]=rgb[from+2]; bmp[to+1]=rgb[from+1]; bmp[to+2]=rgb[from];
    }
    fs.writeFileSync(path.join(__dirname, `vex-brand-${theme}.bmp`), bmp);
  }
}
main().catch(error => {console.error(error); process.exitCode=1;});
