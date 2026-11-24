import fs from 'fs';
const appTs = fs.readFileSync('src/frontend/src/app/app.ts', 'utf8');
const lines = appTs.split('\n');
console.log('--- 425 to 445 ---');
console.log(lines.slice(425, 445).join('\n'));
console.log('--- 598 to 625 ---');
console.log(lines.slice(598, 625).join('\n'));
