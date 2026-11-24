const fs = require('fs');
const file = 'src/backend/MercadoInteligente.Infrastructure/ProductSearchOrchestrator.cs';
let content = fs.readFileSync(file, 'utf8');

// Replace unitLabel
content = content.replace(
  'unitLabel = $" / {skuBest.Unit}";',
  'unitLabel = skuBest.Unit.Trim().TrimStart(\'/\', \' \');'
);
content = content.replace(
  'unitLabel = $" / {unitStr}";',
  'unitLabel = unitStr;'
);

// Replace sort
const oldSort = `        // 7. Sort
        filtered = request.Sort?.ToLowerInvariant() switch
        {
            "price_asc" => filtered.OrderBy(i => i.Price),
            "price_desc" => filtered.OrderByDescending(i => i.Price),
            "unit_price_asc" => filtered.OrderBy(i => i.UnitPrice ?? decimal.MaxValue),
            "recent" => filtered.OrderByDescending(i => i.UpdatedAt),
            "discount_desc" => filtered.OrderByDescending(i => i.DiscountPercentage ?? 0),
            _ => filtered // Default relevance order from FTS
        };`;

const newSort = `        // 7. Sort
        var sort = request.Sort?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(sort) || sort == "relevance")
        {
            if (hasQuery)
            {
                filtered = filtered
                    .OrderByDescending(i => CalculateRelevanceScore(i, query!))
                    .ThenBy(i => i.Price);
            }
        }
        else
        {
            filtered = sort switch
            {
                "price_asc" => filtered.OrderBy(i => i.Price),
                "price_desc" => filtered.OrderByDescending(i => i.Price),
                "unit_price_asc" => filtered.OrderBy(i => i.UnitPrice ?? decimal.MaxValue),
                "recent" => filtered.OrderByDescending(i => i.UpdatedAt),
                "discount_desc" => filtered.OrderByDescending(i => i.DiscountPercentage ?? 0),
                _ => filtered
            };
        }`;

content = content.replace(oldSort, newSort);

// Add CalculateRelevanceScore method before closing class
const method = `
    private static int CalculateRelevanceScore(ProductSearchResultItem item, string query)
    {
        var score = 0;
        var q = query.Trim().ToLowerInvariant();
        var name = item.Name.Trim().ToLowerInvariant();
        var brand = item.Brand?.Trim().ToLowerInvariant() ?? string.Empty;
        var category = item.Category?.Trim().ToLowerInvariant() ?? string.Empty;

        if (name == q)
        {
            score += 1000;
        }
        else if (name.StartsWith(q, StringComparison.OrdinalIgnoreCase))
        {
            score += 800;
        }

        var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length > 0)
        {
            var matchedTokens = tokens.Count(t => name.Contains(t, StringComparison.OrdinalIgnoreCase));
            if (matchedTokens == tokens.Length)
            {
                score += 600;
            }
            else if (matchedTokens > 0)
            {
                score += 400;
            }
        }

        if (!string.IsNullOrEmpty(brand) && (brand.Contains(q, StringComparison.OrdinalIgnoreCase) || q.Contains(brand, StringComparison.OrdinalIgnoreCase)))
        {
            score += 300;
        }

        if (!string.IsNullOrEmpty(category) && (category.Contains(q, StringComparison.OrdinalIgnoreCase) || q.Contains(category, StringComparison.OrdinalIgnoreCase)))
        {
            score += 200;
        }

        return score;
    }
}
`;

const lastBrace = content.lastIndexOf('}');
content = content.substring(0, lastBrace) + method;

fs.writeFileSync(file, content, 'utf8');
console.log('ProductSearchOrchestrator updated successfully');
