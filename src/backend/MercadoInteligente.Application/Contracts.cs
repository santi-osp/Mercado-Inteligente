using MercadoInteligente.Domain;
namespace MercadoInteligente.Application;
public record ProviderCapabilities(CapabilityState Search, CapabilityState Details, CapabilityState Offers, CapabilityState Stores, CapabilityState Availability, CapabilityState CatalogEnumeration, CapabilityState CategoryEnumeration = CapabilityState.NotSupported);
// Opaque provider-defined region or fulfillment context; not a verified physical-store price.
public record ProviderContext(string Key);
public record ProviderStore(string ExternalId, string Name, decimal? Latitude = null, decimal? Longitude = null);
public record ProviderProduct(string ExternalId, string Name, string? Brand, IReadOnlyList<ProviderSku> Skus,
    string? ProductUrl = null, string? CategoryPath = null, string? ImageUrl = null);
public record ProviderSku(string ExternalId, string Name, string? Ean, decimal? Quantity, Dimension Dimension);
public record ProviderOffer(string SkuId, string SellerId, string SellerName, SellerKind SellerKind, decimal Price, PriceScope Scope, AvailabilityStatus Availability, string Context, string? Conditions = null, decimal? ListPrice = null, decimal? AvailableQuantityRaw = null);
public record CatalogCategory(string Id, string Name, string? ParentId = null, int? EstimatedCount = null, IReadOnlyList<CatalogCategory>? Children = null);
public interface IStoreProvider
{
    string Code { get; }
    bool IsDemo => false;
    ProviderCapabilities Capabilities { get; }
    Task<IReadOnlyList<ProviderProduct>> SearchAsync(string query, ProviderContext context, CancellationToken cancellationToken);
    Task<ProviderProduct?> GetProductAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderOffer>> OffersAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderStore>> GetStoresAsync(ProviderContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderProduct>> EnumerateCatalogAsync(ProviderContext context, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogCategory>> EnumerateCategoriesAsync(ProviderContext context, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<CatalogCategory>>([]);
    Task<IReadOnlyList<ProviderProduct>> EnumerateCatalogByCategoryAsync(string categoryId, ProviderContext context, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<ProviderProduct>>([]);
}
public interface IAiProvider
{
    bool IsConfigured { get; }
    Task<string?> ExplainAsync(string context, CancellationToken cancellationToken);
    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new AiCompletionResult(false, null, "NOT_IMPLEMENTED"));
    Task<AiConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new AiConnectionTestResult(false, 0, "No configurado", 0, null));
}
public interface IGeocodingProvider
{
    bool IsConfigured => false;
    Task<(decimal Latitude, decimal Longitude)?> GeocodeAsync(string address, CancellationToken cancellationToken);
    Task<GeocodingResult?> GeocodeDetailedAsync(string address, CancellationToken cancellationToken) => Task.FromResult<GeocodingResult?>(null);
    Task<GeocodingResult?> ReverseGeocodeAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken) => Task.FromResult<GeocodingResult?>(null);
}

public interface IHaversineDistanceService
{
    decimal CalculateDistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2);
    int CalculateDistanceMeters(decimal lat1, decimal lon1, decimal lat2, decimal lon2);
    decimal EstimateRoadDistanceKm(decimal straightLineKm, decimal? windingFactor = null);
    decimal EstimateDurationMinutes(decimal roadDistanceKm, TransportMode mode);
}
public interface IRoutingProvider
{
    bool IsConfigured => false;
    Task<decimal?> DistanceKmAsync(decimal originLatitude, decimal originLongitude, decimal destinationLatitude, decimal destinationLongitude, CancellationToken cancellationToken);
    Task<RouteInfo> GetRouteAsync(decimal originLatitude, decimal originLongitude, decimal destinationLatitude, decimal destinationLongitude, TransportMode mode, CancellationToken cancellationToken) =>
        Task.FromResult(new RouteInfo(0m, 0, 0m, RouteType.StraightLineEstimate, "None"));
}
public interface ISecretStore { void Set(string name, string value); string? Get(string name); void Delete(string name); }
public interface ISyncCoordinator { Task<SyncRun> StartAsync(string provider, CancellationToken cancellationToken); }

public enum ProviderStatus
{
    Healthy,
    Degraded,
    PartialData,
    Down,
    NotConfigured
}

public record ProviderMetadata(
    string Code,
    string Name,
    string RetailerType,
    string CatalogCoverage,
    ProviderCapabilities Capabilities,
    string Status,
    bool Enabled,
    int PriceTtlHours,
    bool SupportsManualOffers,
    string Description,
    string? WebsiteUrl = null,
    bool IsDemo = false
);

public interface IStoreProviderRegistry
{
    IReadOnlyList<IStoreProvider> GetAllProviders();
    IStoreProvider? GetProvider(string code);
    IReadOnlyList<ProviderMetadata> GetMetadata();
}

