const fs = require('fs');
const file = 'tests/MercadoInteligente.Tests/SearchRelevanceTests.cs';
let content = fs.readFileSync(file, 'utf8');
content = content.replace(
  'var db = scope.ServiceProvider.GetRequiredService<MarketDb>();',
  'var db = scope.ServiceProvider.GetRequiredService<MarketDb>();\n            await db.Database.MigrateAsync();'
);
fs.writeFileSync(file, content, 'utf8');
