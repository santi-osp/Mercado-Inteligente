import fs from 'fs';
const appSpec = fs.readFileSync('src/frontend/src/app/app.spec.ts', 'utf8');
const lines = appSpec.split('\n');
lines.forEach((l, i) => {
  if (l.includes('product-query') || l.includes('searchQuery') || l.includes('searchProducts') || l.includes('search-form')) {
    console.log(i + ': ' + l.trim());
  }
});
