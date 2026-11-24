import fs from 'fs';
const m = fs.readFileSync('src/frontend/src/app/pages/market.html', 'utf8');
const lines = m.split('\n');
console.log(lines.slice(180, 260).join('\n'));
