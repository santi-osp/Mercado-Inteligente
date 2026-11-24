with open('src/backend/MercadoInteligente.Infrastructure/ProductAnalysisService.cs', 'r', encoding='latin-1') as f:
    content = f.read()

# Normalize CRLF to LF for reliable matching
is_crlf = '\r\n' in content
content = content.replace('\r\n', '\n')

# 1. Retailer DEMO check in AnalyzeProductAsync
target1 = '''        var retailer = await db.Retailers.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == product.RetailerId, ct);
        var retailerName = retailer?.Name ?? "Tienda";'''

replacement1 = '''        var retailer = await db.Retailers.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == product.RetailerId, ct);
        if (retailer?.Code == "DEMO") return null;
        var retailerName = retailer?.Name ?? "Tienda";'''

assert target1 in content, 'target1 not found'
content = content.replace(target1, replacement1, 1)

# 2. Align unit price calculation in AnalyzeProductAsync
target2 = '''            if (qty != null && qty.Dimension != Dimension.Unknown && qty.TotalAmount > 0 && price > 0)
            {
                unitPrice = Math.Round(price / qty.TotalAmount, 2);
                unitLabel = qty.Dimension switch
                {
                    Dimension.Mass => "$/kg",
                    Dimension.Volume => "$/L",
                    Dimension.Count => "$/ud",
                    Dimension.Length => "$/m",
                    _ => "$/unidad"
                };

                if (unitPrice.Value < minUnitPriceVal)
                {
                    minUnitPriceVal = unitPrice.Value;
                    bestUnitPrice = new UnitPriceInfo(
                        PricePerBaseUnit: unitPrice.Value,
                        BaseUnit: unitLabel,
                        Dimension: qty.Dimension,
                        TotalAmount: qty.TotalAmount,
                        OriginalPackage: $"{qty.TotalAmount:0.##} {qty.Dimension switch { Dimension.Mass => \"kg\", Dimension.Volume => \"L\", Dimension.Count => \"ud\", _ => \"\" }}"
                    );
                }
            }'''

replacement2 = '''            if (price > 0)
            {
                var upCalc = UnitComparisonService.CalculateUnitPrice(price, qty, sku.Name, product.Category);
                if (upCalc != null)
                {
                    unitPrice = upCalc.PricePerBaseUnit;
                    unitLabel = $"$/{upCalc.BaseUnitSymbol}";

                    if (unitPrice.Value < minUnitPriceVal)
                    {
                        minUnitPriceVal = unitPrice.Value;
                        bestUnitPrice = new UnitPriceInfo(
                            PricePerBaseUnit: unitPrice.Value,
                            BaseUnit: unitLabel,
                            Dimension: qty?.Dimension ?? Dimension.Mass,
                            TotalAmount: qty?.TotalAmount ?? 1.0m,
                            OriginalPackage: qty != null
                                ? $"{qty.TotalAmount:0.##} {qty.Dimension switch { Dimension.Mass => \"kg\", Dimension.Volume => \"L\", Dimension.Count => \"ud\", _ => \"\" }}"
                                : "1 kg"
                        );
                    }
                }
            }'''

assert target2 in content, 'target2 not found'
content = content.replace(target2, replacement2, 1)

# 3. GetOpportunitiesAsync exclude DEMO products & align unit price
target3 = '''    public async Task<MarketOpportunitiesResult> GetOpportunitiesAsync(CancellationToken ct = default)
    {
        var products = await db.ExternalProducts.AsNoTracking()
            .Where(p => p.Status == ProductLifecycleState.Active)
            .Take(150)
            .ToListAsync(ct);'''

replacement3 = '''    public async Task<MarketOpportunitiesResult> GetOpportunitiesAsync(CancellationToken ct = default)
    {
        var demoRetailerIds = await db.Retailers.AsNoTracking()
            .Where(r => r.Code == "DEMO")
            .Select(r => r.Id)
            .ToHashSetAsync(ct);

        var products = await db.ExternalProducts.AsNoTracking()
            .Where(p => p.Status == ProductLifecycleState.Active && !demoRetailerIds.Contains(p.RetailerId))
            .Take(150)
            .ToListAsync(ct);'''

assert target3 in content, 'target3 not found'
content = content.replace(target3, replacement3, 1)

target4 = '''                if (qty != null && qty.Dimension != Dimension.Unknown && qty.TotalAmount > 0)
                {
                    var uPrice = Math.Round(bestOffer.Price / qty.TotalAmount, 2);
                    var uLabel = qty.Dimension switch
                    {
                        Dimension.Mass => "$/kg",
                        Dimension.Volume => "$/L",
                        Dimension.Count => "$/ud",
                        _ => "$/u"
                    };
                    unitText = $"{uPrice:C0} {uLabel}";
                }'''

replacement4 = '''                var upCalc = UnitComparisonService.CalculateUnitPrice(bestOffer.Price, qty, product.Name, product.Category);
                if (upCalc != null)
                {
                    unitText = upCalc.UnitPriceDisplay;
                }'''

assert target4 in content, 'target4 not found'
content = content.replace(target4, replacement4, 1)

# 4. Fix line 407 in AnalyzeBasketAsync
target5 = '''            var matches = await ProductSearchService.SearchAsync(db, item.Query, ct);'''
replacement5 = '''            var demoRetailerIds = await db.Retailers.AsNoTracking()
                .Where(r => r.Code == "DEMO")
                .Select(r => r.Id)
                .ToHashSetAsync(ct);
            var matches = await ProductSearchService.SearchAsync(db, item.Query, ct: ct);
            matches = matches.Where(p => !demoRetailerIds.Contains(p.RetailerId)).ToList();'''

assert target5 in content, 'target5 not found'
content = content.replace(target5, replacement5, 1)

if is_crlf:
    content = content.replace('\n', '\r\n')

with open('src/backend/MercadoInteligente.Infrastructure/ProductAnalysisService.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print('Successfully patched ProductAnalysisService.cs and saved as UTF-8')
