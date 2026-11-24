const fs = require('fs');
let content = fs.readFileSync('scripts/capture-ui-audit.mjs', 'utf8');
content = content.replace("['current', 'before', 'after', 'goal18']", "['current', 'before', 'after', 'goal18', 'goal19']");
fs.writeFileSync('scripts/capture-ui-audit.mjs', content, 'utf8');
console.log('Success updating capture-ui-audit.mjs');
