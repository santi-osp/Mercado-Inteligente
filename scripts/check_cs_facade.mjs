import fs from 'fs';
const cs = fs.readFileSync('src/frontend/src/app/facades/catalog-search.facade.ts', 'utf8');
const lines = cs.split('\n');
console.log(lines.slice(0, 50).join('\n'));
