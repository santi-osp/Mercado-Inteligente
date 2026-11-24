using System.Globalization;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MercadoInteligente.Infrastructure;

public sealed class ProductAnalysisService(
    MarketDb db,
    ILogger<ProductAnalysisService> logger
) : IProductAnalysisService
{
    public async Task<ProductAnalysisResult?> AnalyzeProductAsync(Guid productId, CancellationToken ct = default)
    {
        logger.LogDebug("Analyzing product {ProductId}", productId);
        var product = await db.ExternalProducts.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product == null) return null;

        var skus = await db.ExternalSkus.AsNoTracking()
            .Where(s => s.ExternalProductId == product.Id)
            .ToListAsync(ct);

        var skuIds = skus.Select(s => s.Id).ToList();

        var offers = await db.Offers.AsNoTracking()
            .Where(o => skuIds.Contains(o.ExternalSkuId) && o.Availability == AvailabilityStatus.InStock)
            .ToListAsync(ct);

        var sellerIds = offers.Select(o => o.SellerId).Distinct().ToList();
        var sellers = await db.Sellers.AsNoTracking()
            .Where(s => sellerIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s, ct);

        var retailer = await db.Retailers.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == product.RetailerId, ct);
        if (retailer?.Code == "DEMO") return null;
        var retailerName = retailer?.Name ?? "Tienda";
        var retailerCode = retailer?.Code ?? "STORE";

        decimal currentBestPrice = offers.Count > 0 ? offers.Min(o => o.Price) : 0m;
        var cheapestOffer = offers.OrderBy(o => o.Price).FirstOrDefault();
        var cheapestRetailerName = cheapestOffer != null && sellers.TryGetValue(cheapestOffer.SellerId, out var cs) ? cs.Name : retailerName;

        // 1. Presentations & Unit Price
        var presentations = new List<PresentationComparisonItem>();
        UnitPriceInfo? bestUnitPrice = null;
        decimal minUnitPriceVal = decimal.MaxValue;

        foreach (var sku in skus)
        {
            var skuOffers = offers.Where(o => o.ExternalSkuId == sku.Id).ToList();
            var price = skuOffers.Count > 0 ? skuOffers.Min(o => o.Price) : 0m;

            var qty = QuantityParser.Parse(sku.Name) ??
                      (sku.Quantity.HasValue && sku.Dimension != Dimension.Unknown
                          ? new StandardQuantity(sku.Quantity.Value, sku.Dimension, 1, sku.Quantity.Value, sku.Dimension.ToString())
                          : null);

            decimal? unitPrice = null;
            string? unitLabel = null;

            if (price > 0)
            {
                var upCalc = UnitComparisonService.CalculateUnitPrice(price, qty, sku.Name, product.CategoryPath);
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
                                ? $"{qty.TotalAmount:0.##} {qty.Dimension switch { Dimension.Mass => "kg", Dimension.Volume => "L", Dimension.Count => "ud", _ => "" }}"
                                : "1 kg"
                        );
                    }
                }
            }

            presentations.Add(new PresentationComparisonItem(
                SkuId: sku.Id,
                SkuName: sku.Name,
                Package: sku.Name,
                Price: price,
                UnitPrice: unitPrice,
                UnitLabel: unitLabel,
                SavingsPercentVsCheapestUnit: null
            ));
        }

        // Calculate presentation savings relative to best unit price
        if (minUnitPriceVal < decimal.MaxValue)
        {
            presentations = presentations.Select(p =>
            {
                if (p.UnitPrice.HasValue && p.UnitPrice.Value > minUnitPriceVal)
                {
                    var extraPercent = Math.Round(((p.UnitPrice.Value - minUnitPriceVal) / p.UnitPrice.Value) * 100, 1);
                    return p with { SavingsPercentVsCheapestUnit = extraPercent };
                }
                return p with { SavingsPercentVsCheapestUnit = 0m };
            }).ToList();
        }

        // 2. Retailer Prices
        var retailerPrices = offers.Select(o =>
        {
            decimal? diff = currentBestPrice > 0 ? o.Price - currentBestPrice : 0m;
            decimal? discount = o.ListPrice.HasValue && o.ListPrice.Value > o.Price
                ? Math.Round(((o.ListPrice.Value - o.Price) / o.ListPrice.Value) * 100, 1)
                : null;

            var sName = sellers.TryGetValue(o.SellerId, out var sObj) ? sObj.Name : retailerName;

            return new RetailerPriceItem(
                RetailerCode: retailerCode,
                RetailerName: sName,
                Price: o.Price,
                ListPrice: o.ListPrice,
                DiscountPercent: discount,
                PriceScope: o.Scope.ToString(),
                IsVerified: o.Scope == PriceScope.PhysicalVerified,
                DiffVsCheapest: diff
            );
        }).ToList();

        // 3. Historical Price Stats from PriceObservations
        var offerIds = offers.Select(o => o.Id).ToList();
        var observations = await db.PriceObservations.AsNoTracking()
            .Where(po => offerIds.Contains(po.OfferId))
            .OrderBy(po => po.CheckedAt)
            .ToListAsync(ct);

        decimal? histMin = null;
        decimal? histMax = null;
        decimal? histAvg = null;
        string trend = "Estable";
        decimal? trendPercentVsAvg = null;

        if (observations.Count > 0)
        {
            histMin = observations.Min(po => po.Price);
            histMax = observations.Max(po => po.Price);
            histAvg = Math.Round(observations.Average(po => po.Price), 2);

            var firstObs = observations.First().Price;
            var lastObs = observations.Last().Price;
            if (lastObs < firstObs * 0.95m) trend = "A la baja";
            else if (lastObs > firstObs * 1.05m) trend = "Al alza";

            if (histAvg.HasValue && histAvg.Value > 0 && currentBestPrice > 0)
            {
                trendPercentVsAvg = Math.Round(((currentBestPrice - histAvg.Value) / histAvg.Value) * 100, 1);
            }
        }

        // 4. Value Score & Transparent Quality-Price Explanation
        int valueScore = 60;
        string confidence = "Media";
        var valueReasons = new List<string>();

        if (observations.Count >= 5)
        {
            confidence = "Alta";
        }
        else if (observations.Count <= 1)
        {
            confidence = "Datos insuficientes";
        }

        if (histAvg.HasValue && currentBestPrice > 0)
        {
            if (currentBestPrice <= histMin)
            {
                valueScore += 20;
                valueReasons.Add("El precio actual se encuentra en su mínimo histórico registrado.");
            }
            else if (currentBestPrice < histAvg.Value * 0.95m)
            {
                valueScore += 12;
                valueReasons.Add($"El precio actual es inferior al promedio histórico ({histAvg.Value:C0}).");
            }
            else if (currentBestPrice > histAvg.Value * 1.10m)
            {
                valueScore -= 15;
                valueReasons.Add($"El precio actual está por encima del promedio habitual ({histAvg.Value:C0}).");
            }
        }

        var maxDiscount = retailerPrices.Count > 0 ? retailerPrices.Max(r => r.DiscountPercent) : null;
        if (maxDiscount.HasValue && maxDiscount.Value >= 15m)
        {
            valueScore += 15;
            valueReasons.Add($"Cuenta con un descuento promocional activo del {maxDiscount.Value:0.#}%.");
        }

        if (bestUnitPrice != null)
        {
            valueScore += 5;
            valueReasons.Add($"Rendimiento optimizado por unidad de medida a {bestUnitPrice.PricePerBaseUnit:C0} {bestUnitPrice.BaseUnit}.");
        }

        valueScore = Math.Clamp(valueScore, 0, 100);

        bool? isGoodTime = null;
        string? goodTimeReason = null;
        if (histMin.HasValue && currentBestPrice <= histMin.Value)
        {
            isGoodTime = true;
            goodTimeReason = "Excelente momento para comprar: está en el precio más bajo detectado.";
        }
        else if (maxDiscount.HasValue && maxDiscount.Value >= 20m)
        {
            isGoodTime = true;
            goodTimeReason = "Buen momento para comprar debido a promociones vigentes significativas.";
        }
        else if (histAvg.HasValue && currentBestPrice > histAvg.Value * 1.15m)
        {
            isGoodTime = false;
            goodTimeReason = "Podrías esperar. El precio está transitoriamente más alto que su promedio.";
        }
        else
        {
            isGoodTime = true;
            goodTimeReason = "Momento estándar de compra con precio dentro de rangos habituales.";
        }

        return new ProductAnalysisResult(
            ProductId: product.Id,
            ProductName: product.Name,
            Brand: product.Brand,
            Category: product.CategoryPath,
            CurrentBestPrice: currentBestPrice,
            CheapestRetailer: cheapestRetailerName,
            BestUnitPrice: bestUnitPrice,
            MinHistoricalPrice: histMin,
            MaxHistoricalPrice: histMax,
            AvgHistoricalPrice: histAvg,
            PriceTrend: trend,
            TrendPercentVsAvg: trendPercentVsAvg,
            IsGoodTimeToBuy: isGoodTime,
            GoodTimeToBuyReason: goodTimeReason,
            ValueScore: valueScore,
            Confidence: confidence,
            ValueExplanation: string.Join(" ", valueReasons),
            Presentations: presentations,
            RetailerPrices: retailerPrices,
            ObservationsCount: observations.Count
        );
    }

    public async Task<MarketOpportunitiesResult> GetOpportunitiesAsync(CancellationToken ct = default)
    {
        var demoRetailerIds = await db.Retailers.AsNoTracking()
            .Where(r => r.Code == "DEMO")
            .Select(r => r.Id)
            .ToHashSetAsync(ct);

        var products = await db.ExternalProducts.AsNoTracking()
            .Where(p => p.Status == ProductLifecycleState.Active && !demoRetailerIds.Contains(p.RetailerId))
            .Take(150)
            .ToListAsync(ct);

        var productIds = products.Select(p => p.Id).ToList();
        var allSkus = await db.ExternalSkus.AsNoTracking()
            .Where(s => productIds.Contains(s.ExternalProductId))
            .ToListAsync(ct);

        var skuIds = allSkus.Select(s => s.Id).ToList();

        var offers = await db.Offers.AsNoTracking()
            .Where(o => skuIds.Contains(o.ExternalSkuId) && o.Availability == AvailabilityStatus.InStock)
            .ToListAsync(ct);

        var retailers = await db.Retailers.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r, ct);
        var sellers = await db.Sellers.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s, ct);

        var bestUnitPrices = new List<OpportunityItem>();
        var highestDiscounts = new List<OpportunityItem>();
        var bestValues = new List<OpportunityItem>();

        foreach (var product in products)
        {
            var prodSkus = allSkus.Where(s => s.ExternalProductId == product.Id).ToList();
            var prodSkuIds = prodSkus.Select(s => s.Id).ToHashSet();
            var prodOffers = offers.Where(o => prodSkuIds.Contains(o.ExternalSkuId)).ToList();
            if (prodOffers.Count == 0) continue;

            var bestOffer = prodOffers.OrderBy(o => o.Price).First();
            var ret = retailers.TryGetValue(product.RetailerId, out var rObj) ? rObj : null;
            var retCode = ret?.Code ?? "STORE";
            var retName = sellers.TryGetValue(bestOffer.SellerId, out var sObj) ? sObj.Name : (ret?.Name ?? "Tienda");

            decimal? discount = bestOffer.ListPrice.HasValue && bestOffer.ListPrice.Value > bestOffer.Price
                ? Math.Round(((bestOffer.ListPrice.Value - bestOffer.Price) / bestOffer.ListPrice.Value) * 100, 1)
                : null;

            string? unitText = null;
            var sku = prodSkus.FirstOrDefault(s => s.Id == bestOffer.ExternalSkuId);
            if (sku != null)
            {
                var qty = QuantityParser.Parse(sku.Name) ??
                          (sku.Quantity.HasValue && sku.Dimension != Dimension.Unknown
                              ? new StandardQuantity(sku.Quantity.Value, sku.Dimension, 1, sku.Quantity.Value, sku.Dimension.ToString())
                              : null);

                var upCalc = UnitComparisonService.CalculateUnitPrice(bestOffer.Price, qty, product.Name, product.CategoryPath);
                if (upCalc != null)
                {
                    unitText = upCalc.UnitPriceDisplay;
                }
            }

            int valueScore = 65;
            if (discount.HasValue && discount.Value >= 15) valueScore += 15;
            if (unitText != null) valueScore += 10;
            valueScore = Math.Clamp(valueScore, 0, 100);

            if (discount.HasValue && discount.Value >= 10m)
            {
                highestDiscounts.Add(new OpportunityItem(
                    ProductId: product.Id,
                    ProductName: product.Name,
                    Brand: product.Brand,
                    RetailerCode: retCode,
                    RetailerName: retName,
                    Price: bestOffer.Price,
                    ListPrice: bestOffer.ListPrice,
                    DiscountPercent: discount,
                    UnitPriceText: unitText,
                    ValueScore: valueScore,
                    Highlight: $"Ahorras {discount.Value:0.#}%",
                    ImageUrl: product.ImageUrl
                ));
            }

            if (unitText != null)
            {
                bestUnitPrices.Add(new OpportunityItem(
                    ProductId: product.Id,
                    ProductName: product.Name,
                    Brand: product.Brand,
                    RetailerCode: retCode,
                    RetailerName: retName,
                    Price: bestOffer.Price,
                    ListPrice: bestOffer.ListPrice,
                    DiscountPercent: discount,
                    UnitPriceText: unitText,
                    ValueScore: valueScore,
                    Highlight: $"Excelente rendimiento: {unitText}",
                    ImageUrl: product.ImageUrl
                ));
            }

            if (valueScore >= 75)
            {
                bestValues.Add(new OpportunityItem(
                    ProductId: product.Id,
                    ProductName: product.Name,
                    Brand: product.Brand,
                    RetailerCode: retCode,
                    RetailerName: retName,
                    Price: bestOffer.Price,
                    ListPrice: bestOffer.ListPrice,
                    DiscountPercent: discount,
                    UnitPriceText: unitText,
                    ValueScore: valueScore,
                    Highlight: $"Relación calidad-precio destacada ({valueScore}/100)",
                    ImageUrl: product.ImageUrl
                ));
            }
        }

        return new MarketOpportunitiesResult(
            BestUnitPrices: bestUnitPrices.OrderBy(x => x.Price).Take(10).ToList(),
            HighestDiscounts: highestDiscounts.OrderByDescending(x => x.DiscountPercent).Take(10).ToList(),
            BestValueItems: bestValues.OrderByDescending(x => x.ValueScore).Take(10).ToList()
        );
    }

    public async Task<BasketAnalysisResult?> AnalyzeBasketAsync(Guid basketId, CancellationToken ct = default)
    {
        var basket = await db.Baskets.AsNoTracking()
            .Include(b => b.Items)
            .FirstOrDefaultAsync(b => b.Id == basketId, ct);

        if (basket == null) return null;

        var itemsBreakdown = new List<BasketItemAnalysis>();
        var recommendations = new List<string>();
        decimal estimatedTotal = 0m;
        decimal potentialSavings = 0m;

        foreach (var item in basket.Items)
        {
            var demoRetailerIds = await db.Retailers.AsNoTracking()
                .Where(r => r.Code == "DEMO")
                .Select(r => r.Id)
                .ToHashSetAsync(ct);
            var matches = await ProductSearchService.SearchAsync(db, item.Query, ct: ct);
            matches = matches.Where(p => !demoRetailerIds.Contains(p.RetailerId)).ToList();
            if (matches.Count > 0)
            {
                var top = matches.First();
                var topSkuIds = await db.ExternalSkus.AsNoTracking()
                    .Where(s => s.ExternalProductId == top.Id)
                    .Select(s => s.Id)
                    .ToListAsync(ct);

                var offers = await db.Offers.AsNoTracking()
                    .Where(o => topSkuIds.Contains(o.ExternalSkuId) && o.Availability == AvailabilityStatus.InStock)
                    .OrderBy(o => o.Price)
                    .ToListAsync(ct);

                if (offers.Count > 0)
                {
                    var best = offers.First();
                    var worst = offers.Last();
                    var price = best.Price * item.Quantity;
                    estimatedTotal += price;

                    decimal diff = (worst.Price - best.Price) * item.Quantity;
                    if (diff > 0)
                    {
                        potentialSavings += diff;
                    }

                    string? suggestion = null;
                    if (offers.Count > 1 && diff > 1000m)
                    {
                        suggestion = $"Comprando en la tienda más barata ahorras {diff:C0} en este producto.";
                    }

                    itemsBreakdown.Add(new BasketItemAnalysis(
                        ItemId: item.Id,
                        Query: item.Query,
                        SelectedProductName: top.Name,
                        Price: price,
                        ValueScore: 78,
                        Suggestion: suggestion
                    ));
                    continue;
                }
            }

            itemsBreakdown.Add(new BasketItemAnalysis(
                ItemId: item.Id,
                Query: item.Query,
                SelectedProductName: null,
                Price: null,
                ValueScore: null,
                Suggestion: "No se encontraron ofertas locales exactas para este término."
            ));
        }

        if (potentialSavings > 0)
        {
            recommendations.Add($"Puedes ahorrar hasta {potentialSavings:C0} optimizando la distribución de tiendas de tu cesta.");
        }
        else
        {
            recommendations.Add("Tu cesta actual cuenta con precios competitivos en las opciones disponibles.");
        }

        return new BasketAnalysisResult(
            BasketId: basket.Id,
            TotalItems: basket.Items.Count,
            EstimatedTotal: estimatedTotal,
            PotentialSavings: potentialSavings,
            Recommendations: recommendations,
            ItemBreakdowns: itemsBreakdown
        );
    }
}