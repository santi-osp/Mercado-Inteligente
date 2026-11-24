const fs = require('fs');
const file = 'tests/MercadoInteligente.Tests/SearchRelevanceTests.cs';
let content = fs.readFileSync(file, 'utf8');
content = content.replace(
  'Scope = PriceScope.FulfillmentRegion,',
  'Scope = PriceScope.OnlineGeneric,'
);
content = content.replace(
  'Context = "fulfillment:medellin-aburra",',
  'Context = "generic",'
);
fs.writeFileSync(file, content, 'utf8');
