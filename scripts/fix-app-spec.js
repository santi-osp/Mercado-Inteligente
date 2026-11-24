const fs = require('fs');
const p = 'src/frontend/src/app/app.spec.ts';
let content = fs.readFileSync(p, 'utf8');

const target = `    const request = http.expectOne(req => req.url === '/api/v1/products/search');`;
const replacement = `    const catalogReqs = http.match('/api/v1/search/products');
    if (catalogReqs.length > 0) {
      catalogReqs[0].flush({ query: 'arroz', totalCount: 0, page: 1, pageSize: 24, items: [], facets: { retailers: [], brands: [], categories: [] } });
    }
    const request = http.expectOne(req => req.url === '/api/v1/products/search');`;

if (content.includes(target)) {
  content = content.replace(target, replacement);
  fs.writeFileSync(p, content, 'utf8');
  console.log('app.spec.ts updated successfully');
} else {
  console.log('Target string not found in app.spec.ts');
}
