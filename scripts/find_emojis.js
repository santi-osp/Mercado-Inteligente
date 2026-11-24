const fs = require('fs');
const path = require('path');
const dir = 'src/frontend/src/app';

function checkDir(d) {
  for (const entry of fs.readdirSync(d, { withFileTypes: true })) {
    const full = path.join(d, entry.name);
    if (entry.isDirectory()) {
      checkDir(full);
    } else if (entry.name.endsWith('.html') || entry.name.endsWith('.ts')) {
      const content = fs.readFileSync(full, 'utf8');
      const lines = content.split('\n');
      lines.forEach((line, idx) => {
        // match typical emojis
        const emojiMatch = line.match(/[\u{1F300}-\u{1FAFF}\u{2600}-\u{26FF}\u{2700}-\u{27BF}]/u);
        if (emojiMatch) {
          console.log(`${full}:${idx + 1}: ${line.trim()}`);
        }
      });
    }
  }
}
checkDir(dir);
