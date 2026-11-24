import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const require = createRequire(import.meta.url);
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '..');
const distDir = path.join(rootDir, 'src/frontend/dist/mercado-inteligente/browser');
const playwrightPath = path.join(rootDir, 'src/frontend/node_modules/playwright');
const { chromium } = require(playwrightPath);

const PORT = 4311;
const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.json': 'application/json; charset=utf-8'
};

const server = http.createServer((req, res) => {
  let reqPath = req.url.split('?')[0];
  if (reqPath === '/ngsw-worker.js' || reqPath === '/ngsw.json') {
    res.writeHead(404);
    return res.end('Not found');
  }
  if (reqPath === '/') reqPath = '/index.html';
  let filePath = path.join(distDir, reqPath);
  if (!fs.existsSync(filePath) || fs.statSync(filePath).isDirectory()) {
    filePath = path.join(distDir, 'index.html');
  }
  const ext = path.extname(filePath).toLowerCase();
  res.writeHead(200, { 'Content-Type': MIME_TYPES[ext] || 'application/octet-stream' });
  fs.createReadStream(filePath).pipe(res);
});

await new Promise(r => server.listen(PORT, '127.0.0.1', r));

const browser = await chromium.launch({ channel: 'chrome', headless: true });
const context = await browser.newContext({
  serviceWorkers: 'block',
  viewport: { width: 1440, height: 1000 }
});
const page = await context.newPage();

await page.route('**/api/**', route => {
  const url = route.request().url();
  console.log('[INTERCEPTED API]', route.request().method(), url);
  if (url.includes('/api/v1/system/health')) {
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ database: 'ok', products: 1520, ai: 'ready', google: 'configured', mode: 'local-first' }) });
  }
  if (url.includes('/api/v1/settings')) {
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ maxStores: 3, sizeTolerancePercent: 15, allowSubstitutes: true, maxOverbuyPercent: 20, priceTtlHours: 24, includeMarketplace: false, allowUnverifiedLocationsForTravel: false }) });
  }
  if (url.includes('/api/v1/location/profiles')) {
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([{ id: 'p1', label: 'Casa', address: 'Circular 1 # 70-15', formattedAddress: 'Circular 1 # 70-15, Medellín', latitude: 6.2425, longitude: -75.5925 }]) });
  }
  route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ status: 'ok' }) });
});

await page.goto('http://127.0.0.1:' + PORT + '/');
await page.waitForTimeout(1000);

const text = await page.locator('body').innerText();
console.log('Body snippet:', text.slice(0, 300).replace(/\s+/g, ' '));

await browser.close();
server.close();
