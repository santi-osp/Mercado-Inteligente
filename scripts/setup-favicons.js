const fs = require('fs');
const path = require('path');
const { chromium } = require(path.resolve('src/frontend/node_modules/playwright'));

// SVG definition for Mercado Inteligente Favicon
// Two-color: Brand Green #167D5A and Pure White #FFFFFF
// Geometric M with integrated comparison/price tag geometry
const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">
  <rect width="512" height="512" rx="112" fill="#167D5A"/>
  <!-- Geometric M with comparison line -->
  <path d="M112 392V144c0-13.25 10.75-24 24-24h32c10.5 0 20 6.5 23.5 16.5L256 312l64.5-175.5c3.5-10 13-16.5 23.5-16.5h32c13.25 0 24 10.75 24 24v248c0 13.25-10.75 24-24 24h-28c-13.25 0-24-10.75-24-24V240l-51 138c-3.5 9.5-12.5 16-23 16s-19.5-6.5-23-16l-51-138v152c0 13.25-10.75 24-24 24h-28c-13.25 0-24-10.75-24-24z" fill="#FFFFFF"/>
  <!-- Comparison dot / price badge accent in white with subtle cutout -->
  <circle cx="392" cy="136" r="32" fill="#10B981" stroke="#FFFFFF" stroke-width="12"/>
</svg>`;

// Also a version without the dot that is ultra clean for 16px
const svgClean = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">
  <rect width="512" height="512" rx="112" fill="#167D5A"/>
  <path d="M120 392V144c0-13.25 10.75-24 24-24h28c10.5 0 20 6.5 23.5 16.5L256 304l60.5-167.5c3.5-10 13-16.5 23.5-16.5h28c13.25 0 24 10.75 24 24v248c0 13.25-10.75 24-24 24h-24c-13.25 0-24-10.75-24-24V236l-48 132c-3.5 9.5-12.5 16-23 16s-19.5-6.5-23-16l-48-132v156c0 13.25-10.75 24-24 24h-24c-13.25 0-24-10.75-24-24z" fill="#FFFFFF"/>
  <!-- Price comparison bar accent -->
  <rect x="220" y="380" width="72" height="36" rx="12" fill="#FFFFFF"/>
</svg>`;

fs.writeFileSync('scripts/generate-favicons.js', `
const fs = require('fs');
const path = require('path');
const { chromium } = require(path.resolve('src/frontend/node_modules/playwright'));

const svg = \`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">
  <rect width="512" height="512" rx="112" fill="#167D5A"/>
  <path d="M120 392V144c0-13.25 10.75-24 24-24h28c10.5 0 20 6.5 23.5 16.5L256 304l60.5-167.5c3.5-10 13-16.5 23.5-16.5h28c13.25 0 24 10.75 24 24v248c0 13.25-10.75 24-24 24h-24c-13.25 0-24-10.75-24-24V236l-48 132c-3.5 9.5-12.5 16-23 16s-19.5-6.5-23-16l-48-132v156c0 13.25-10.75 24-24 24h-24c-13.25 0-24-10.75-24-24z" fill="#FFFFFF"/>
  <rect x="220" y="376" width="72" height="40" rx="12" fill="#FFFFFF"/>
</svg>\`;

// Write SVGs
fs.writeFileSync('src/frontend/public/favicon.svg', svg);
fs.writeFileSync('src/frontend/src/favicon.svg', svg);
fs.mkdirSync('assets/icons', { recursive: true });
fs.writeFileSync('assets/icons/favicon.svg', svg);

function createIco(png16, png32) {
  // Simple multi-image ICO container with PNG payloads
  const count = 2;
  const headerSize = 6;
  const entrySize = 16;
  const header = Buffer.alloc(headerSize);
  header.writeUInt16LE(0, 0); // reserved
  header.writeUInt16LE(1, 2); // type 1 = icon
  header.writeUInt16LE(count, 4); // 2 images

  const offset1 = headerSize + entrySize * count;
  const offset2 = offset1 + png16.length;

  const entry1 = Buffer.alloc(entrySize);
  entry1.writeUInt8(16, 0); // width
  entry1.writeUInt8(16, 1); // height
  entry1.writeUInt8(0, 2); // color count
  entry1.writeUInt8(0, 3); // reserved
  entry1.writeUInt16LE(1, 4); // color planes
  entry1.writeUInt16LE(32, 6); // bpp
  entry1.writeUInt32LE(png16.length, 8); // size
  entry1.writeUInt32LE(offset1, 12); // offset

  const entry2 = Buffer.alloc(entrySize);
  entry2.writeUInt8(32, 0); // width
  entry2.writeUInt8(32, 1); // height
  entry2.writeUInt8(0, 2); // color count
  entry2.writeUInt8(0, 3); // reserved
  entry2.writeUInt16LE(1, 4); // color planes
  entry2.writeUInt16LE(32, 6); // bpp
  entry2.writeUInt32LE(png32.length, 8); // size
  entry2.writeUInt32LE(offset2, 12); // offset

  return Buffer.concat([header, entry1, entry2, png16, png32]);
}

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const page = await browser.newPage();
  
  const targets = [
    { size: 16, files: ['src/frontend/public/favicon-16x16.png'] },
    { size: 32, files: ['src/frontend/public/favicon-32x32.png'] },
    { size: 72, files: ['src/frontend/public/icons/icon-72x72.png'] },
    { size: 96, files: ['src/frontend/public/icons/icon-96x96.png'] },
    { size: 128, files: ['src/frontend/public/icons/icon-128x128.png'] },
    { size: 144, files: ['src/frontend/public/icons/icon-144x144.png'] },
    { size: 152, files: ['src/frontend/public/icons/icon-152x152.png'] },
    { size: 180, files: ['src/frontend/public/apple-touch-icon.png'] },
    { size: 192, files: ['src/frontend/public/android-chrome-192x192.png', 'src/frontend/public/icons/icon-192x192.png'] },
    { size: 384, files: ['src/frontend/public/icons/icon-384x384.png'] },
    { size: 512, files: ['src/frontend/public/android-chrome-512x512.png', 'src/frontend/public/icons/icon-512x512.png'] }
  ];

  let png16Buf = null;
  let png32Buf = null;

  for (const t of targets) {
    await page.setViewportSize({ width: t.size, height: t.size });
    await page.setContent(\`<!DOCTYPE html><html><body style="margin:0;padding:0;background:transparent;overflow:hidden;">
      \${svg.replace('width="512" height="512"', 'width="' + t.size + '" height="' + t.size + '"')}
    </body></html>\`);
    const buf = await page.screenshot({ omitBackground: true });
    if (t.size === 16) png16Buf = buf;
    if (t.size === 32) png32Buf = buf;

    for (const f of t.files) {
      fs.mkdirSync(path.dirname(f), { recursive: true });
      fs.writeFileSync(f, buf);
      console.log('Generated:', f, 'size:', t.size);
    }
  }

  // Generate ICO
  const icoBuf = createIco(png16Buf, png32Buf);
  fs.writeFileSync('src/frontend/public/favicon.ico', icoBuf);
  fs.writeFileSync('src/frontend/src/favicon.ico', icoBuf);
  fs.writeFileSync('assets/icons/favicon.ico', icoBuf);
  console.log('Generated: favicon.ico, bytes:', icoBuf.length);

  await browser.close();
  console.log('All favicon and app icon assets generated successfully!');
})();
`);
