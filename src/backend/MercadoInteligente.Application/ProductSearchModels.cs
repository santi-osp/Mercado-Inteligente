using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public sealed record ProductSearchRequest(
    string? Query,
    IReadOnlyList<string>? SelectedRetailers = null,
    Guid? LocationProfileId = null,
    string? LocationInput = null,
    bool ForceRefresh = false,
    int Page = 1,
    int PageSize = 24,
    string? Sort = "relevance",
    ProductSearchFilters? Filters = null,
    bool LocalOnly = false
);

public sealed record ProductSearchFilters(
    IReadOnlyList<string>? Retailers = null,
    IReadOnlyList<string>? Brands = null,
    IReadOnlyList<string>? Categories = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    bool? InStockOnly = null,
    bool? PromoOnly = null
);

public sealed record ProductSearchResultItem(
    Guid ProductId,
    Guid? ExternalSkuId,
    string Name,
    string? Brand,
    string? Presentation,
    string? ImageUrl,
    string Retailer,
    string RetailerCode,
    decimal Price,
    decimal? ListPrice,
    decimal? DiscountPercentage,
    string? Promotion,
    decimal? UnitPrice,
    string? UnitLabel,
    string Availability,
    string Freshness,
    string PriceScope,
    DateTime UpdatedAt,
    string? ProductUrl,
    string? CategoryPath,
    bool IsMarketplace = false,
    string? SellerDisplay = null,
    bool IsFallbackContext = false,
    string? ContextWarning = null,
    string? LiveStatus = null,
    string? UnitPriceDisplay = null,
    string? UnitPriceUnit = null,
    string? PricingMode = null,
    string? PriceBasis = null,
    string? PriceNotes = null
);

public sealed record FacetCount(string Value, int Count);

public sealed record ProductSearchFacets(
    IReadOnlyList<FacetCount> Retailers,
    IReadOnlyList<FacetCount> Brands,
    IReadOnlyList<FacetCount> Categories,
    decimal? MinPrice,
    decimal? MaxPrice
);

public sealed class SearchSession
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Query { get; init; } = "";
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public Dictionary<string, string> RetailerStatus { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public int LocalCount { get; set; }
    public int NewCount { get; set; }
    public int UpdatedCount { get; set; }
    public int UnchangedCount { get; set; }
    public int UnavailableCount { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Warnings { get; init; } = [];
}

public sealed record ProductSearchResponse(
    IReadOnlyList<ProductSearchResultItem> Items,
    int TotalCount,
    int Page,
    int PageSize,
    ProductSearchFacets Facets,
    IReadOnlyList<string> Warnings,
    SearchSession? Session = null,
    int TotalPages = 1,
    bool HasPrevious = false,
    bool HasNext = false,
    string? Query = null
)
{
    public int Total => TotalCount;
}

public enum CatalogImportStatus
{
    Pending,
    Running,
    Paused,
    Completed,
    Partial,
    Failed,
    Cancelled
}

public sealed class CatalogImportJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string RetailerCode { get; init; } = "";
    public string RetailerName { get; init; } = "";
    public IReadOnlyList<string> CategoryIds { get; init; } = [];
    public CatalogImportStatus Status { get; set; } = CatalogImportStatus.Pending;
    public int TotalItems { get; set; }
    public int ProcessedItems { get; set; }
    public int NewItems { get; set; }
    public int UpdatedItems { get; set; }
    public int UnchangedItems { get; set; }
    public int ErrorCount { get; set; }
    public double ProgressPercentage => TotalItems > 0 ? Math.Round((double)ProcessedItems / TotalItems * 100, 1) : 0;
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public string? CurrentCategoryName { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed record CatalogImportRequest(
    string RetailerCode,
    IReadOnlyList<string> CategoryIds,
    bool SelectAll = false
);
