const fs = require('fs');
const file = 'tests/MercadoInteligente.Tests/SearchRelevanceTests.cs';
let content = fs.readFileSync(file, 'utf8');
if (!content.includes('using Microsoft.EntityFrameworkCore;')) {
  content = 'using Microsoft.EntityFrameworkCore;\n' + content;
  fs.writeFileSync(file, content, 'utf8');
}
