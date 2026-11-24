import fs from 'fs';
const s = fs.readFileSync('scripts/capture-ui-audit.mjs', 'utf8');
const lines = s.split('\n');
console.log(lines.slice(650, 745).join('\n'));
