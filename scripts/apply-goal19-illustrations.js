const fs = require('fs');

// 1. search.html
let search = fs.readFileSync('src/frontend/src/app/pages/search.html', 'utf8');
const searchEmptyRegex = /<div class="empty-icon" aria-hidden="true">[\s\S]*?<\/svg>\s*<\/div>/;
const searchEmptyNew = `<div class="empty-icon" aria-hidden="true">
          <img src="/assets/illustrations/empty-search.svg" alt="" class="empty-state-illustration" style="width: 120px; height: 120px; max-width: 100%; margin-bottom: 12px;">
        </div>`;
if (searchEmptyRegex.test(search)) {
  search = search.replace(searchEmptyRegex, searchEmptyNew);
  console.log('search.html: replaced empty svg');
}

const productFallbackRegex = /<div class="product-img-fallback" aria-hidden="true">[\s\S]*?<\/svg>\s*<\/div>/;
const productFallbackNew = `<div class="product-img-fallback" aria-hidden="true">
                  <img src="/assets/illustrations/product-placeholder.svg" alt="" class="product-placeholder-img" style="width: 48px; height: 48px; opacity: 0.7;">
                </div>`;
if (productFallbackRegex.test(search)) {
  search = search.replace(productFallbackRegex, productFallbackNew);
  console.log('search.html: replaced product fallback svg');
}

const detailFallbackRegex = /<div class="detail-img-fallback" aria-hidden="true">[\s\S]*?<\/svg>\s*<\/div>/;
const detailFallbackNew = `<div class="detail-img-fallback" aria-hidden="true">
            <img src="/assets/illustrations/product-placeholder.svg" alt="" class="detail-placeholder-img" style="width: 64px; height: 64px; opacity: 0.7;">
          </div>`;
if (detailFallbackRegex.test(search)) {
  search = search.replace(detailFallbackRegex, detailFallbackNew);
  console.log('search.html: replaced detail fallback svg');
}
fs.writeFileSync('src/frontend/src/app/pages/search.html', search, 'utf8');

// 2. home.html
let home = fs.readFileSync('src/frontend/src/app/pages/home.html', 'utf8');
// Section 32: Título: Tu mercado, mejor informado / Texto: Busca productos, compara precios y organiza tus compras.
home = home.replace(
  /<p class="eyebrow">TU MERCADO, MÁS INTELIGENTE<\/p>[\s\S]*?<h1>¿Qué quieres hacer hoy\?<\/h1>[\s\S]*?<p class="muted">Encuentra el mejor precio por kilo o litro, organiza tus compras y ahorra en cada viaje.<\/p>/,
  `<p class="eyebrow">TU MERCADO, MEJOR INFORMADO</p>
    <h1>Tu mercado, mejor informado</h1>
    <p class="muted">Busca productos, compara precios y organiza tus compras.</p>`
);

// Empty activity card SVG -> empty-lists.svg
const homeEmptyRegex = /<div class="empty-icon" aria-hidden="true"><svg width="24" height="24"[\s\S]*?<\/svg><\/div>/;
const homeEmptyNew = `<div class="empty-icon" aria-hidden="true">
    <img src="/assets/illustrations/empty-lists.svg" alt="" class="empty-state-illustration" style="width: 100px; height: 100px; margin-bottom: 12px;">
  </div>`;
if (homeEmptyRegex.test(home)) {
  home = home.replace(homeEmptyRegex, homeEmptyNew);
  console.log('home.html: replaced empty activity svg');
}
fs.writeFileSync('src/frontend/src/app/pages/home.html', home, 'utf8');

// 3. market.html
let market = fs.readFileSync('src/frontend/src/app/pages/market.html', 'utf8');
market = market.replace(
  /<div class="empty-lists-notice">([\s\S]*?)<\/div>/,
  `<div class="empty-lists-notice" style="text-align: center; padding: 24px 12px;">
        <img src="/assets/illustrations/empty-lists.svg" alt="" style="width: 90px; height: 90px; margin-bottom: 8px;">
        <p class="muted" style="margin-bottom: 12px;">Aún no tienes listas. Crea una para comenzar.</p>
        <button type="button" class="primary small-btn" (click)="host.createBasket()">Crear primera lista</button>
      </div>`
);

market = market.replace(
  /<div class="empty-basket-state">([\s\S]*?)<\/div>/,
  `<div class="empty-basket-state" style="text-align: center; padding: 28px 16px;">
            <img src="/assets/illustrations/empty-lists.svg" alt="" style="width: 80px; height: 80px; margin-bottom: 8px;">
            <p class="muted" role="status">Esta lista está vacía. Escribe un producto arriba o agrégalo desde el buscador.</p>
          </div>`
);

market = market.replace(
  /<div class="panel empty-selection-panel">([\s\S]*?)<\/div>/,
  `<div class="panel empty-selection-panel" style="text-align: center; padding: 36px 16px;">
        <img src="/assets/illustrations/empty-lists.svg" alt="" style="width: 90px; height: 90px; margin-bottom: 12px;">
        <p class="muted">Selecciona una lista a la izquierda o crea una nueva.</p>
      </div>`
);
fs.writeFileSync('src/frontend/src/app/pages/market.html', market, 'utf8');

// 4. history.html
let history = fs.readFileSync('src/frontend/src/app/pages/history.html', 'utf8');
// If no history data
const historyNoDataBlock = `@if (!host.historyLoading() && !host.historyError() && !host.historyData()) {
  <div class="empty panel" style="text-align: center; padding: 48px 24px;">
    <img src="/assets/illustrations/empty-history.svg" alt="" style="width: 140px; height: 140px; margin-bottom: 16px;">
    <h3>Sin historial seleccionado</h3>
    <p class="muted">Busca un producto arriba para explorar la evolución de su precio en el tiempo y comparar comercios.</p>
  </div>
}`;

if (!history.includes('empty-history.svg')) {
  history = history.replace(
    /@if \(host\.historyError\(\)\) {[\s\S]*?<\/p>\s*}/,
    `$&

${historyNoDataBlock}`
  );
  
  history = history.replace(
    /<div class="empty">No se encontraron observaciones con los filtros seleccionados\.<\/div>/,
    `<div class="empty" style="text-align: center; padding: 32px 16px;">
        <img src="/assets/illustrations/empty-history.svg" alt="" style="width: 100px; height: 100px; margin-bottom: 12px;">
        <p>No se encontraron observaciones con los filtros seleccionados.</p>
      </div>`
  );
  console.log('history.html: added empty history illustrations');
}
fs.writeFileSync('src/frontend/src/app/pages/history.html', history, 'utf8');

// 5. analysis.html
let analysis = fs.readFileSync('src/frontend/src/app/pages/analysis.html', 'utf8');

// Replace Score: with Valor de compra:
analysis = analysis.replace(/Score:\s*\{\{\s*opp\.valueScore\s*\}\}\/100/g, 'Valor de compra: {{ opp.valueScore }}/100');
analysis = analysis.replace(/\{\{\s*item\.valueScore\s*\}\}\/100/g, 'Valor de compra: {{ item.valueScore }}/100');

// Replace empty icons with illustrations
analysis = analysis.replace(
  /<div class="empty-icon"><svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M20\.59 13\.41l-7\.17 7\.17a2 2 0 0 1-2\.83 0L2 12V2h10l8\.59 8\.59a2 2 0 0 1 0 2\.82z"\/><line x1="7" y1="7" x2="7\.01" y2="7"\/><\/svg><\/div>/,
  `<div class="empty-icon" style="margin-bottom: 12px;">
            <img src="/assets/illustrations/empty-analysis.svg" alt="" style="width: 120px; height: 120px; max-width: 100%;">
          </div>`
);

analysis = analysis.replace(
  /<div class="empty-icon"><svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="8"\/><line x1="21" y1="21" x2="16\.65" y2="16\.65"\/><\/svg><\/div>/,
  `<div class="empty-icon" style="margin-bottom: 12px;">
            <img src="/assets/illustrations/empty-search.svg" alt="" style="width: 120px; height: 120px; max-width: 100%;">
          </div>`
);

analysis = analysis.replace(
  /<div class="empty-icon"><svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="9" cy="21" r="1"\/><circle cx="20" cy="21" r="1"\/><path d="M1 1h4l2\.68 13\.39a2 2 0 0 0 2 1\.61h9\.72a2 2 0 0 0 2-1\.61L23 6H6"\/><\/svg><\/div>/,
  `<div class="empty-icon" style="margin-bottom: 12px;">
            <img src="/assets/illustrations/empty-lists.svg" alt="" style="width: 120px; height: 120px; max-width: 100%;">
          </div>`
);

fs.writeFileSync('src/frontend/src/app/pages/analysis.html', analysis, 'utf8');
console.log('analysis.html: updated illustrations and score wording');

// 6. sources.html
let sources = fs.readFileSync('src/frontend/src/app/pages/sources.html', 'utf8');
if (!sources.includes('store-placeholder.svg')) {
  sources = sources.replace(
    /<div class="flex-align-center gap-2">\s*<span class="retailer-logo-badge font-black text-sm px-2 py-1 rounded bg-slate-100 text-slate-800 border">\{\{ source\.code \}\}<\/span>/,
    `<div class="flex-align-center gap-2">
                    <img src="/assets/illustrations/store-placeholder.svg" alt="" style="width: 28px; height: 28px; border-radius: 6px; flex-shrink: 0;" aria-hidden="true">
                    <span class="retailer-logo-badge store-badge-{{ source.code.toLowerCase() }} font-black text-sm px-2 py-1 rounded bg-slate-100 text-slate-800 border">{{ source.code }}</span>`
  );
  fs.writeFileSync('src/frontend/src/app/pages/sources.html', sources, 'utf8');
  console.log('sources.html: added store-placeholder.svg');
}

