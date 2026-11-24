using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public enum ContextResolutionStatus
{
    Resolved,
    Fallback,
    Failed,
    NotApplicable
}

public sealed record EffectiveProviderContext(
    Guid? RetailerId,
    string ProviderCode,
    Guid? RequestedLocationProfileId,
    PriceScope PriceScope,
    string PriceContext,
    string? ExternalContextId = null,
    string? RegionId = null,
    string? FulfillmentRegion = null,
    string? SalesChannel = null,
    ContextResolutionStatus ResolutionStatus = ContextResolutionStatus.Resolved,
    bool IsFallback = false,
    DateTime? ResolvedAt = null,
    string? StatusOverride = null
)
{
    // Compatibility helpers
    public string Provider => ProviderCode;
    public string Key => PriceContext;
    public PriceScope Scope => PriceScope;
    public string Status => StatusOverride ?? (ResolutionStatus switch
    {
        ContextResolutionStatus.Resolved => "RESOLVED",
        ContextResolutionStatus.Fallback => "ONLINE_GENERIC_FALLBACK",
        ContextResolutionStatus.Failed => "FAILED",
        _ => "ONLINE_GENERIC"
    });
}

public sealed record ShoppingContext(
    Guid? LocationProfileId,
    string? NormalizedAddress,
    decimal? Latitude,
    decimal? Longitude,
    IReadOnlyList<EffectiveProviderContext> ProviderContexts,
    DateTime ResolvedAt,
    string ValidationStatus,
    bool HasFallbackProviders = false
)
{
    public IReadOnlyList<EffectiveProviderContext> Providers => ProviderContexts;

    public EffectiveProviderContext? GetContextForProvider(string providerCode) =>
        ProviderContexts.FirstOrDefault(p => string.Equals(p.ProviderCode, providerCode, StringComparison.OrdinalIgnoreCase));

    public EffectiveProviderContext? GetContextForRetailer(Guid retailerId) =>
        ProviderContexts.FirstOrDefault(p => p.RetailerId == retailerId);
}

public interface IShoppingContextResolver
{
    Task<ShoppingContext> ResolveContextAsync(
        string? locationInput = null,
        decimal? reqLatitude = null,
        decimal? reqLongitude = null,
        CancellationToken ct = default);
}

public enum OfferCompatibility
{
    Incompatible,
    Fallback,
    Compatible
}

public sealed record OfferCompatibilityEvaluation(
    OfferCompatibility Compatibility,
    string Reason,
    bool IsFallback = false
);

public interface IOfferContextPolicy
{
    OfferCompatibilityEvaluation Evaluate(
        PriceScope offerScope,
        string offerContext,
        string providerCode,
        EffectiveProviderContext? providerContext,
        bool allowGenericFallbackIfRegionalMissing = true);

    OfferCompatibilityEvaluation Evaluate(
        RawCatalogOffer offer,
        EffectiveProviderContext? providerContext,
        bool allowGenericFallbackIfRegionalMissing = true);
}

public sealed record TravelLocation(
    Guid? StoreId,
    string StoreName,
    string RetailerCode,
    decimal Latitude,
    decimal Longitude,
    decimal? DistanceKm,
    bool IsPhysicalStoreProxy,
    string VerificationStatus = "UNCONFIRMED_ONLINE_REGIONAL",
    string TransparencyNotice = "Precio online/regional; precio físico no confirmado"
);

public interface ICommercialContextCache
{
    EffectiveProviderContext? Get(string providerCode, Guid? locationProfileId, string? addressOrCoordinateKey);
    void Set(string providerCode, Guid? locationProfileId, string? addressOrCoordinateKey, EffectiveProviderContext context, TimeSpan? ttl = null);
    void Invalidate(Guid? locationProfileId = null);
    void Clear();
}
