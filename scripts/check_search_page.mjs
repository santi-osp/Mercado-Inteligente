import fs from 'fs';
const fp = fs.readFileSync('src/frontend/src/app/feature-pages.ts', 'utf8');
const lines = fp.split('\n');
let inSearch = false;
let searchLines = [];
for (let i = 0; i < lines.length; i++) {
  if (lines[i].includes('export class SearchPage')) inSearch = true;
  else if (inSearch && lines[i].includes('export class ')) break;
  if (inSearch) searchLines.push(lines[i]);
}
console.log(searchLines.join('\n'));
