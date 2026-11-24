using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public sealed record UnitPriceInfo(
    decimal PricePerBaseUnit,
    string BaseUnit,
    Dimension Dimension,
    decimal TotalAmount,
    string OriginalPackage
);

public sealed record PresentationComparisonItem(
    Guid SkuId,
    string SkuName,
    string Package,
    decimal Price,
    decimal? UnitPrice,
    string? UnitLabel,
    decimal? SavingsPercentVsCheapestUnit
);

public sealed record RetailerPriceItem(
    string RetailerCode,
    string RetailerName,
    decimal Price,
    decimal? ListPrice,
    decimal? DiscountPercent,
    string PriceScope,
    bool IsVerified,
    decimal? DiffVsCheapest
);

public sealed record ProductAnalysisResult(
    Guid ProductId,
    string ProductName,
    string? Brand,
    string? Category,
    decimal CurrentBestPrice,
    string CheapestRetailer,
    UnitPriceInfo? BestUnitPrice,
    decimal? MinHistoricalPrice,
    decimal? MaxHistoricalPrice,
    decimal? AvgHistoricalPrice,
    string PriceTrend,
    decimal? TrendPercentVsAvg,
    bool? IsGoodTimeToBuy,
    string? GoodTimeToBuyReason,
    int ValueScore,
    string Confidence,
    string ValueExplanation,
    IReadOnlyList<PresentationComparisonItem> Presentations,
    IReadOnlyList<RetailerPriceItem> RetailerPrices,
    int ObservationsCount
);

public sealed record OpportunityItem(
    Guid ProductId,
    string ProductName,
    string? Brand,
    string RetailerCode,
    string RetailerName,
    decimal Price,
    decimal? ListPrice,
    decimal? DiscountPercent,
    string? UnitPriceText,
    int ValueScore,
    string Highlight,
    string? ImageUrl
);

public sealed record MarketOpportunitiesResult(
    IReadOnlyList<OpportunityItem> BestUnitPrices,
    IReadOnlyList<OpportunityItem> HighestDiscounts,
    IReadOnlyList<OpportunityItem> BestValueItems
);

public sealed record BasketItemAnalysis(
    Guid ItemId,
    string Query,
    string? SelectedProductName,
    decimal? Price,
    int? ValueScore,
    string? Suggestion
);

public sealed record BasketAnalysisResult(
    Guid BasketId,
    int TotalItems,
    decimal EstimatedTotal,
    decimal PotentialSavings,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<BasketItemAnalysis> ItemBreakdowns
);

public interface IProductAnalysisService
{
    Task<ProductAnalysisResult?> AnalyzeProductAsync(Guid productId, CancellationToken ct = default);
    Task<MarketOpportunitiesResult> GetOpportunitiesAsync(CancellationToken ct = default);
    Task<BasketAnalysisResult?> AnalyzeBasketAsync(Guid basketId, CancellationToken ct = default);
}