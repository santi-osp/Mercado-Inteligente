import fs from 'fs';
const appTs = fs.readFileSync('src/frontend/src/app/app.ts', 'utf8');
const lines = appTs.split('\n');
lines.forEach((l, i) => {
  if (l.includes('mobileMenu') || l.includes('navigate(') || l.includes('readonly nav =')) {
    console.log(i + ': ' + l.trim());
  }
});
