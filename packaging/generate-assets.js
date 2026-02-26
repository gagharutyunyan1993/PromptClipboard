const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const svgPath = path.join(__dirname, '..', 'logo.svg');
const assetsDir = path.join(__dirname, 'Assets');
const svgBuffer = fs.readFileSync(svgPath);

const assets = [
  { name: 'Square44x44Logo.png', size: 44 },
  { name: 'Square44x44Logo.targetsize-24_altform-unplated.png', size: 24 },
  { name: 'Square44x44Logo.targetsize-32_altform-unplated.png', size: 32 },
  { name: 'Square44x44Logo.targetsize-48_altform-unplated.png', size: 48 },
  { name: 'Square44x44Logo.targetsize-256_altform-unplated.png', size: 256 },
  { name: 'Square71x71Logo.png', size: 71 },
  { name: 'Square150x150Logo.png', size: 150 },
  { name: 'Wide310x150Logo.png', width: 310, height: 150 },
  { name: 'StoreLogo.png', size: 50 },
];

async function main() {
  fs.mkdirSync(assetsDir, { recursive: true });

  for (const asset of assets) {
    const w = asset.width || asset.size;
    const h = asset.height || asset.size;

    if (w !== h) {
      const iconSize = Math.min(w, h) - 10;
      const iconBuf = await sharp(svgBuffer).resize(iconSize, iconSize).png().toBuffer();
      await sharp({
        create: { width: w, height: h, channels: 4, background: { r: 0, g: 0, b: 0, alpha: 0 } }
      })
        .composite([{ input: iconBuf, gravity: 'centre' }])
        .png()
        .toFile(path.join(assetsDir, asset.name));
    } else {
      await sharp(svgBuffer).resize(w, h).png().toFile(path.join(assetsDir, asset.name));
    }
    console.log('Created:', asset.name, w + 'x' + h);
  }
  console.log('All assets generated.');
}

main().catch(e => { console.error(e); process.exit(1); });
