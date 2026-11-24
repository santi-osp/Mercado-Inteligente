import fs from 'fs';
const m = fs.readFileSync('src/frontend/src/app/pages/market.html', 'utf8');
const lines = m.split('\n');
console.log('Total lines:', lines.length);
console.log(lines.slice(0, 70).join('\n'));
