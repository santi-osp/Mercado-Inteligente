const fs = require('fs');

// 1. Fix search.html
let search = fs.readFileSync('src/frontend/src/app/pages/search.html', 'utf8');
search = search.replace(/@if\s*\(prod\.discount\)\s*\{[\r\n\s]*<span class="discount-badge">-{{ prod\.discount }}%<\/span>[\r\n\s]*\}/g,
  `@if (prod.discountPercentage) {\n                <span class="discount-badge">-{{ prod.discountPercentage }}%</span>\n              }`);

search = search.replace(/@if\s*\(prod\.unitPrice\)\s*\{[\r\n\s]*<span class="unit-price">\{\{\s*prod\.unitPrice\s*\}\}<\/span>[\r\n\s]*\}/g,
  `@if (prod.unitPrice) {\n                <span class="unit-price">\${{ prod.unitPrice | number:'1.0-0' }} / {{ prod.unitLabel }}</span>\n              }`);
fs.writeFileSync('src/frontend/src/app/pages/search.html', search, 'utf8');
console.log('search.html fixed');

// 2. Fix app.html
let app = fs.readFileSync('src/frontend/src/app/app.html', 'utf8');
const oldRecBlock = /@for\s*\(rec of msg\.recommendedProducts;\s*track\s*rec\.productId \+ rec\.retailer\)[\s\S]*?<\/div>[\r\n\s]*\}[\r\n\s]*<\/div>[\r\n\s]*\}/;
const newRecBlock = `@for (rec of msg.recommendedProducts; track rec.name + rec.retailer) {
                  <div class="rec-card">
                    <div class="rec-info">
                      <strong>{{ rec.name }}</strong>
                      <span class="rec-retailer">{{ rec.retailer }}</span>
                      @if (rec.reason) { <small class="rec-reason">{{ rec.reason }}</small> }
                    </div>
                    <div class="rec-pricing">
                      <span class="rec-price">\${{ rec.price | number:'1.0-0' }} COP</span>
                    </div>
                    <div class="rec-actions">
                      @if (rec.productUrl) {
                        <a [href]="rec.productUrl" target="_blank" rel="noopener noreferrer" class="secondary small-btn">Ver en tienda ↗</a>
                      }
                      <button type="button" class="primary small-btn" (click)="addProductFromSearch(selectedBasketId() || '', { name: rec.name, brand: null, quantity: 1 })">+ Agregar</button>
                    </div>
                  </div>
                }
              </div>
            }`;

if (oldRecBlock.test(app)) {
  app = app.replace(oldRecBlock, newRecBlock);
  fs.writeFileSync('src/frontend/src/app/app.html', app, 'utf8');
  console.log('app.html fixed');
} else {
  console.log('oldRecBlock did not match in app.html');
}
