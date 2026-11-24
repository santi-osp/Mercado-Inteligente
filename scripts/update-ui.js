const fs = require('fs');
const path = require('path');

exports.writeB64 = function(relPath, b64) {
  const fullPath = path.resolve(__dirname, '..', relPath);
  fs.mkdirSync(path.dirname(fullPath), { recursive: true });
  fs.writeFileSync(fullPath, Buffer.from(b64, 'base64'));
  console.log('Wrote ' + relPath + ' (' + fs.statSync(fullPath).size + ' bytes)');
};
