import fs from 'fs';
const fp = fs.readFileSync('src/frontend/src/app/feature-pages.ts', 'utf8');
const lines = fp.split('\n');
console.log(lines.slice(0, 45).join('\n'));
