using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public sealed record OptimizerCandidate(
    Guid OfferId,
    Guid ExternalSkuId,
    Guid ExternalProductId,
    Guid RetailerId,
    string RetailerCode,
    string RetailerName,
    Guid? StoreId,
    string StoreName,
    string StoreKey,
    string ProductName,
    string? Brand,
    string SkuName,
    string? Ean,
    decimal PackPrice,
    StandardQuantity? Quantity,
    UnitPriceCalculation? UnitPrice,
    MatchKind MatchKind,
    decimal SizeDifferenceRatio,
    int PacksNeeded,
    decimal TotalAcquired,
    decimal OverbuyPercent,
    bool IsOverbuyRejected,
    decimal TotalCost,
    decimal StandardCost,
    decimal PromotionSavings,
    string? PromotionApplied,
    string? PromotionWarning,
    Freshness Freshness,
    PriceScope Scope,
    string Context,
    string Explanation,
    bool IsPreferredBrand,
    bool IsFallbackContext = false,
    string? ContextWarning = null
);

public sealed record CandidateGenerationOptions(
    int DefaultSizeTolerancePercent = 15,
    bool DefaultAllowSubstitutes = true,
    int DefaultMaxOverbuyPercent = 20,
    int PriceTtlHours = 6,
    int StockTtlHours = 2,
    bool IncludeMarketplace = false,
    bool AllowOutdatedOffers = false
);

public sealed record PreprocessedOffer(
    RawCatalogOffer Offer,
    StandardQuantity? CandQty,
    NormalizedProductInfo CandNorm,
    string StoreKey,
    string StoreName,
    Freshness Freshness,
    UnitPriceCalculation? UnitPrice
);

public sealed class CatalogIndex
{
    private readonly IReadOnlyList<PreprocessedOffer> _allOffers;
    private readonly ILookup<string, PreprocessedOffer> _byCategory;
    private readonly ILookup<string, PreprocessedOffer> _byEan;
    private readonly Dictionary<string, List<PreprocessedOffer>> _byToken;

    public CatalogIndex(IReadOnlyList<PreprocessedOffer> offers)
    {
        _allOffers = offers;
        _byCategory = offers.ToLookup(o => o.CandNorm.ProductType, StringComparer.OrdinalIgnoreCase);
        _byEan = offers.Where(o => !string.IsNullOrWhiteSpace(o.CandNorm.Ean))
                       .ToLookup(o => o.CandNorm.Ean!, StringComparer.OrdinalIgnoreCase);

        _byToken = new Dictionary<string, List<PreprocessedOffer>>(StringComparer.OrdinalIgnoreCase);
        foreach (var offer in offers)
        {
            var tokens = offer.CandNorm.CleanName.Split([' ', '-', ',', '.'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                if (!_byToken.TryGetValue(token, out var list))
                {
                    list = new List<PreprocessedOffer>();
                    _byToken[token] = list;
                }
                list.Add(offer);
            }
        }
    }

    public IEnumerable<PreprocessedOffer> GetCandidateOffers(NormalizedProductInfo target)
    {
        // 1. If EAN matches, definitely include
        if (!string.IsNullOrWhiteSpace(target.Ean) && _byEan.Contains(target.Ean))
        {
            foreach (var o in _byEan[target.Ean])
                yield return o;
        }

        // 2. If specific category (not generic Abarrotes), return all offers in that category
        if (!string.Equals(target.ProductType, "Abarrotes", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var o in _byCategory[target.ProductType])
                yield return o;
            yield break;
        }

        // 3. For generic Abarrotes:
        var targetTokens = target.CleanName.Split([' ', '-', ',', '.'], StringSplitOptions.RemoveEmptyEntries);
        var seen = new HashSet<Guid>();

        var candidateTokens = targetTokens
            .Where(t => _byToken.ContainsKey(t))
            .OrderBy(t => _byToken[t].Count)
            .ToList();

        var qualifyingTokens = candidateTokens
            .Where(t => _byToken[t].Count < Math.Max(15, _allOffers.Count * 0.3))
            .Take(3)
            .ToList();

        if (qualifyingTokens.Count == 0 && candidateTokens.Count > 0)
        {
            qualifyingTokens.Add(candidateTokens[0]);
        }

        foreach (var token in qualifyingTokens)
        {
            if (_byToken.TryGetValue(token, out var tokenOffers))
            {
                foreach (var o in tokenOffers)
                {
                    if (seen.Add(o.Offer.OfferId))
                        yield return o;
                }
            }
        }

        // Fallback for small catalogs (< 30 offers):
        if (_allOffers.Count < 30)
        {
            foreach (var o in _allOffers)
            {
                if (seen.Add(o.Offer.OfferId))
                    yield return o;
            }
        }
    }
}

public static class CandidateGenerationService
{
    public static PreprocessedOffer PreprocessOffer(RawCatalogOffer offer, int priceTtlHours)
    {
        var candQty = offer.SkuQuantity.HasValue && offer.SkuDimension != Dimension.Unknown
            ? new StandardQuantity(offer.SkuQuantity.Value, offer.SkuDimension)
            : QuantityParser.Parse($"{offer.ProductName} {offer.SkuName}");

        var candNorm = ProductNormalizer.Normalize(
            rawName: $"{offer.ProductName} {offer.SkuName}",
            brand: offer.Brand,
            ean: offer.Ean,
            quantity: candQty?.TotalAmount,
            dimension: candQty?.Dimension ?? Dimension.Unknown
        );

        var now = DateTime.UtcNow;
        var age = now - offer.CheckedAt;
        var freshness = age.TotalHours <= priceTtlHours
            ? Freshness.Fresh
            : age.TotalHours <= priceTtlHours * 2 ? Freshness.Stale : Freshness.Outdated;

        var storeKey = !string.IsNullOrWhiteSpace(offer.StoreKey)
            ? offer.StoreKey
            : offer.StoreId.HasValue
                ? $"store:{offer.StoreId.Value}"
                : $"retailer:{offer.RetailerCode}";

        var storeName = !string.IsNullOrWhiteSpace(offer.StoreName)
            ? offer.StoreName
            : $"{offer.RetailerName} Online";

        var unitPrice = UnitComparisonService.CalculateUnitPrice(offer.Price, candQty);

        return new PreprocessedOffer(
            offer,
            candQty,
            candNorm,
            storeKey,
            storeName,
            freshness,
            unitPrice
        );
    }

    public static IReadOnlyList<OptimizerCandidate> GenerateCandidatesForItem(
        BasketItem item,
        IReadOnlyList<RawCatalogOffer> offers,
        CandidateGenerationOptions options,
        IReadOnlyList<MatchOverride>? overrides = null)
    {
        var preprocessed = offers.Select(o => PreprocessOffer(o, options.PriceTtlHours)).ToList();
        var index = new CatalogIndex(preprocessed);
        return GenerateCandidatesForItem(item, index, options, overrides);
    }

    public static IReadOnlyList<OptimizerCandidate> GenerateCandidatesForItem(
        BasketItem item,
        IReadOnlyList<PreprocessedOffer> preprocessedOffers,
        CandidateGenerationOptions options,
        IReadOnlyList<MatchOverride>? overrides = null)
    {
        var index = new CatalogIndex(preprocessedOffers);
        return GenerateCandidatesForItem(item, index, options, overrides);
    }

    public static IReadOnlyList<OptimizerCandidate> GenerateCandidatesForItem(
        BasketItem item,
        CatalogIndex catalogIndex,
        CandidateGenerationOptions options,
        IReadOnlyList<MatchOverride>? overrides = null)
    {
        var allowSubstitutes = item.AllowSubstitutesOverride ?? options.DefaultAllowSubstitutes;
        var sizeTolerancePercent = item.SizeTolerancePercentOverride ?? options.DefaultSizeTolerancePercent;
        var sizeToleranceRatio = (decimal)sizeTolerancePercent / 100.0m;
        var maxOverbuyPercent = (decimal)options.DefaultMaxOverbuyPercent;

        // Parse desired item specs
        var targetParsed = QuantityParser.Parse(item.Query);
        var targetDimension = item.Dimension != Dimension.Unknown
            ? item.Dimension
            : (targetParsed?.Dimension ?? Dimension.Unknown);

        decimal targetBaseAmount;
        if (!string.IsNullOrWhiteSpace(item.Unit) && item.Dimension != Dimension.Unknown)
        {
            // Explicit unit given on item (e.g. 400g)
            targetBaseAmount = item.Quantity;
        }
        else if (targetParsed != null && targetParsed.TotalAmount > 0)
        {
            targetBaseAmount = (item.Quantity > 0 ? item.Quantity : 1m) * targetParsed.TotalAmount;
        }
        else
        {
            targetBaseAmount = item.Quantity > 0 ? item.Quantity : 1m;
        }

        var targetNorm = ProductNormalizer.Normalize(item.Query, dimension: targetDimension, quantity: targetParsed?.TotalAmount);

        var candidates = new List<OptimizerCandidate>();
        var targetedOffers = catalogIndex.GetCandidateOffers(targetNorm);

        foreach (var prep in targetedOffers)
        {
            var offer = prep.Offer;

            // 1. Seller marketplace filter
            if (!options.IncludeMarketplace && offer.SellerKind == SellerKind.Marketplace)
                continue;
            if (!options.AllowOutdatedOffers && prep.Freshness == Freshness.Outdated)
                continue;

            // 2. Availability filter: do not offer OutOfStock or NotSold
            if (offer.Availability is AvailabilityStatus.OutOfStock or AvailabilityStatus.NotSold)
                continue;

            // 3. Brand exclusion
            if (!string.IsNullOrWhiteSpace(item.ExcludedBrand) &&
                !string.IsNullOrWhiteSpace(offer.Brand) &&
                offer.Brand.Contains(item.ExcludedBrand.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var isPreferredBrand = !string.IsNullOrWhiteSpace(item.PreferredBrand) &&
                                   !string.IsNullOrWhiteSpace(offer.Brand) &&
                                   offer.Brand.Contains(item.PreferredBrand.Trim(), StringComparison.OrdinalIgnoreCase);

            var candQty = prep.CandQty;
            var candNorm = prep.CandNorm;

            var eanMatch = !string.IsNullOrWhiteSpace(targetNorm.Ean) &&
                           string.Equals(candNorm.Ean, targetNorm.Ean, StringComparison.OrdinalIgnoreCase);
            var categoryMatch = string.Equals(candNorm.ProductType, targetNorm.ProductType, StringComparison.OrdinalIgnoreCase);

            if (!eanMatch && !categoryMatch)
                continue;

            // 5. Match Evaluation
            MatchOverride? matchedOverride = null;
            if (overrides is not null)
            {
                matchedOverride = overrides.FirstOrDefault(o => o.ExternalSkuId == offer.ExternalSkuId);
            }

            var matchResult = DeterministicMatcher.Evaluate(
                targetNorm,
                candNorm,
                matchOverride: matchedOverride,
                sizeToleranceRatio: sizeToleranceRatio,
                rankCandidates: false
            );

            // Check eligibility based on MatchKind
            if (matchResult.Kind is MatchKind.NotEquivalent or MatchKind.Candidate)
                continue;

            if (matchResult.Kind == MatchKind.Substitute && !allowSubstitutes)
                continue;

            // 6. Quantity coverage and overbuy calculation
            int packsNeeded;
            decimal totalAcquired;
            decimal overbuyPercent;
            bool isOverbuyRejected;

            if (targetParsed == null && targetDimension == Dimension.Unknown &&
                (string.IsNullOrWhiteSpace(item.Unit) || item.Unit.ToLowerInvariant() is "und" or "unds" or "unidad" or "unidades"))
            {
                // Discrete pack count: user asked for product in general (e.g. "Café Molido" x 1)
                packsNeeded = (int)Math.Max(1, item.Quantity);
                totalAcquired = packsNeeded * (candQty?.TotalAmount ?? 1m);
                overbuyPercent = 0m;
                isOverbuyRejected = false;
            }
            else if (candQty is not null && candQty.TotalAmount > 0)
            {
                // Compatible physical dimension?
                if (targetDimension != Dimension.Unknown && !QuantityParser.AreDimensionsCompatible(targetDimension, candQty.Dimension))
                    continue;

                packsNeeded = (int)Math.Max(1, Math.Ceiling(targetBaseAmount / candQty.TotalAmount));
                totalAcquired = packsNeeded * candQty.TotalAmount;
                overbuyPercent = ((totalAcquired - targetBaseAmount) / targetBaseAmount) * 100.0m;
                isOverbuyRejected = overbuyPercent > maxOverbuyPercent;
            }
            else
            {
                // Unspecified quantity: assume discrete count
                packsNeeded = (int)Math.Max(1, Math.Ceiling(targetBaseAmount));
                totalAcquired = packsNeeded;
                overbuyPercent = 0m;
                isOverbuyRejected = false;
            }

            // 7. Promotion Evaluation
            var promoEval = PromotionEvaluator.Evaluate(offer.Price, packsNeeded, offer.PromotionConditions);

            var sizeDiffRatio = (candQty != null && targetNorm.Quantity != null && targetNorm.Quantity.TotalAmount > 0)
                ? Math.Abs(candQty.TotalAmount - targetNorm.Quantity.TotalAmount) / targetNorm.Quantity.TotalAmount
                : 0m;

            candidates.Add(new OptimizerCandidate(
                OfferId: offer.OfferId,
                ExternalSkuId: offer.ExternalSkuId,
                ExternalProductId: offer.ExternalProductId,
                RetailerId: offer.RetailerId,
                RetailerCode: offer.RetailerCode,
                RetailerName: offer.RetailerName,
                StoreId: offer.StoreId,
                StoreName: prep.StoreName,
                StoreKey: prep.StoreKey,
                ProductName: offer.ProductName,
                Brand: offer.Brand,
                SkuName: offer.SkuName,
                Ean: offer.Ean,
                PackPrice: offer.Price,
                Quantity: candQty,
                UnitPrice: prep.UnitPrice,
                MatchKind: matchResult.Kind,
                SizeDifferenceRatio: sizeDiffRatio,
                PacksNeeded: packsNeeded,
                TotalAcquired: totalAcquired,
                OverbuyPercent: overbuyPercent,
                IsOverbuyRejected: isOverbuyRejected,
                TotalCost: promoEval.TotalCost,
                StandardCost: promoEval.StandardCost,
                PromotionSavings: promoEval.Savings,
                PromotionApplied: promoEval.AppliedPromotion,
                PromotionWarning: promoEval.ConditionWarning,
                Freshness: prep.Freshness,
                Scope: offer.Scope,
                Context: offer.Context,
                Explanation: matchResult.Evidence,
                IsPreferredBrand: isPreferredBrand,
                IsFallbackContext: offer.IsFallbackContext,
                ContextWarning: offer.ContextWarning
            ));
        }

        return candidates;
    }
}

public sealed record RawCatalogOffer(
    Guid OfferId,
    Guid ExternalSkuId,
    Guid ExternalProductId,
    Guid RetailerId,
    string RetailerCode,
    string RetailerName,
    Guid? StoreId,
    string? StoreName,
    Guid SellerId,
    string SellerName,
    SellerKind SellerKind,
    string ProductName,
    string? Brand,
    string SkuName,
    string? Ean,
    decimal? SkuQuantity,
    Dimension SkuDimension,
    decimal Price,
    decimal? ListPrice,
    PriceScope Scope,
    AvailabilityStatus Availability,
    string Context,
    string? PromotionConditions,
    DateTime CheckedAt,
    string? StoreKey = null,
    bool IsFallbackContext = false,
    string? ContextWarning = null
);
