const fs = require('fs');
const file = 'src/backend/MercadoInteligente.Infrastructure/ProductSearchOrchestrator.cs';
let content = fs.readFileSync(file, 'utf8');
content = content.replace(
  'var category = item.Category?.Trim().ToLowerInvariant() ?? string.Empty;',
  'var category = item.CategoryPath?.Trim().ToLowerInvariant() ?? string.Empty;'
);
fs.writeFileSync(file, content, 'utf8');
