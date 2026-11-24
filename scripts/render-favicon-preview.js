const fs = require('fs');
const path = require('path');
const { chromium } = require(path.resolve('src/frontend/node_modules/playwright'));

(async () => {
  const browser = await chromium.launch({ channel: 'chrome', headless: true });
  const page = await browser.newPage();
  fs.mkdirSync('docs/ui-audit', { recursive: true });

  // Light and Dark mock browser tabs displaying the favicon at 16px and 32px
  const html = `<!DOCTYPE html><html><body style="margin:20px;background:#1E293B;color:#F8FAFC;font-family:sans-serif;">
    <div style="background:#0F172A;padding:24px;border-radius:12px;display:inline-block;">
      <h3 style="margin-top:0;font-size:14px;color:#94A3B8;">Dark Chrome Mock (16px & 32px)</h3>
      <div style="display:flex;align-items:center;gap:12px;background:#334155;padding:8px 16px;border-radius:8px 8px 0 0;width:240px;">
        <img src="data:image/png;base64,${fs.readFileSync('src/frontend/public/favicon-16x16.png').toString('base64')}" width="16" height="16" />
        <span style="font-size:12px;font-weight:500;">Mercado Inteligente</span>
      </div>
      <div style="margin-top:16px;display:flex;align-items:center;gap:16px;">
        <div>
          <span style="font-size:11px;color:#94A3B8;">16×16 px:</span><br/>
          <img src="data:image/png;base64,${fs.readFileSync('src/frontend/public/favicon-16x16.png').toString('base64')}" width="16" height="16" style="margin-top:4px;" />
        </div>
        <div>
          <span style="font-size:11px;color:#94A3B8;">32×32 px:</span><br/>
          <img src="data:image/png;base64,${fs.readFileSync('src/frontend/public/favicon-32x32.png').toString('base64')}" width="32" height="32" style="margin-top:4px;" />
        </div>
        <div>
          <span style="font-size:11px;color:#94A3B8;">64×64 px:</span><br/>
          <img src="data:image/png;base64,${fs.readFileSync('src/frontend/public/icons/icon-72x72.png').toString('base64')}" width="64" height="64" style="margin-top:4px;border-radius:8px;" />
        </div>
      </div>
    </div>
  </body></html>`;

  await page.setContent(html);
  const buf = await page.locator('div').first().screenshot();
  fs.writeFileSync('docs/ui-audit/favicon-preview.png', buf);
  console.log('Saved docs/ui-audit/favicon-preview.png');
  await browser.close();
})();
