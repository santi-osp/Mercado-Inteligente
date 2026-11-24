using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure.Stores.D1;
using MercadoInteligente.Infrastructure.Stores.Exito;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure;

public enum SyncPriority
{
    OnDemand = 1,
    Basket = 2,
    Maintenance = 3
}

public sealed record SyncExecutionResult(
    string Provider,
    string Status,
    int ItemsReceived,
    int OffersProcessed,
    int OffersChanged,
    string? ErrorCode = null
);

public sealed record BasketSyncResult(
    Guid BasketId,
    int TotalItems,
    int QueriesExecuted,
    List<SyncExecutionResult> Results,
    ShoppingContext? ShoppingContext = null
);

public sealed record MaintenanceSyncResult(
    int StaleOffersFound,
    int QueriesExecuted,
    List<SyncExecutionResult> Results
);

public interface ISyncEngine
{
    Task<int> AbortHangingRunsAsync(CancellationToken ct = default);
    Task<SyncExecutionResult> SyncOnDemandAsync(string provider, string query, string? context = null, CancellationToken ct = default);
    Task<BasketSyncResult> SyncBasketAsync(Guid? basketId = null, string? exitoContext = null, CancellationToken ct = default, string? locationInput = null);
    Task<MaintenanceSyncResult> SyncMaintenanceStaleAsync(int maxItems = 10, CancellationToken ct = default);
    Task<List<SyncRun>> GetRecentRunsAsync(int limit = 20, CancellationToken ct = default);
}

public sealed class SyncEngine(
    MarketDb db,
    D1SearchSync d1Sync,
    ExitoSearchSync exitoSync,
    IFreshnessService freshnessService,
    IGeocodingProvider? geocoder = null,
    ExitoProvider? exitoProvider = null,
    D1CatalogIngest? d1Ingest = null,
    Func<D1FulfillmentSession>? d1SessionFactory = null,
    ICommercialContextCache? contextCache = null
) : ISyncEngine
{
    public async Task<int> AbortHangingRunsAsync(CancellationToken ct = default)
    {
        var hanging = await db.SyncRuns
            .Where(r => r.State == SyncState.Running)
            .ToListAsync(ct);

        if (hanging.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var run in hanging)
        {
            run.State = SyncState.Aborted;
            run.FinishedAt = now;
            run.ErrorCode = "ABORTED_ON_STARTUP";
        }

        await db.SaveChangesAsync(ct);
        return hanging.Count;
    }

    public async Task<SyncExecutionResult> SyncOnDemandAsync(string provider, string query, string? context = null, CancellationToken ct = default)
    {
        return await ExecuteProviderSyncAsync(provider, query, context, "OnDemand", ct);
    }

    public async Task<BasketSyncResult> SyncBasketAsync(Guid? basketId = null, string? exitoContext = null, CancellationToken ct = default, string? locationInput = null)
    {
        var basket = basketId.HasValue
            ? await db.Baskets.Include(b => b.Items).FirstOrDefaultAsync(b => b.Id == basketId.Value, ct)
            : await db.Baskets.Include(b => b.Items).OrderByDescending(b => b.CreatedAt).FirstOrDefaultAsync(ct);

        if (basket is null)
        {
            basket = new Basket { Name = "Mi mercado" };
            db.Baskets.Add(basket);
            await db.SaveChangesAsync(ct);
        }

        var queries = basket.Items
            .Select(i => i.Query?.Trim())
            .Where(q => !string.IsNullOrWhiteSpace(q))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var results = new List<SyncExecutionResult>();
        var profiles = await db.LocationProfiles.AsNoTracking().ToListAsync(ct);
        var parsed = LocationInputParser.Parse(locationInput ?? "", profiles);
        var profile = profiles.FirstOrDefault(p =>
            string.Equals(p.Label, locationInput?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            (p.Latitude == parsed.Latitude && p.Longitude == parsed.Longitude && p.Latitude.HasValue));
        decimal? latitude = parsed.Latitude;
        decimal? longitude = parsed.Longitude;
        if ((!latitude.HasValue || !longitude.HasValue) && !string.IsNullOrWhiteSpace(parsed.NormalizedAddress))
        {
            var geocoded = geocoder is GoogleGeocodingProvider google
                ? await google.GeocodeDetailedAsync(parsed.NormalizedAddress, ct)
                : null;
            if (geocoded is { ProviderStatus: "LIVE_GOOGLE" or "CACHED_VERIFIED" })
            {
                latitude = geocoded.Latitude;
                longitude = geocoded.Longitude;
            }
        }
                var retailers = await db.Retailers.AsNoTracking().ToDictionaryAsync(r => r.Code, r => r.Id, ct);
        retailers.TryGetValue("D1", out var d1RetailerId);
        retailers.TryGetValue("EXITO", out var exitoRetailerId);
        retailers.TryGetValue("ARA", out var araRetailerId);

        var d1Context = new EffectiveProviderContext(d1RetailerId != Guid.Empty ? d1RetailerId : null, "D1", profile?.Id, PriceScope.OnlineGeneric, "online-generic", ResolutionStatus: ContextResolutionStatus.Fallback, IsFallback: true, ResolvedAt: DateTime.UtcNow, StatusOverride: "ONLINE_GENERIC");
        var exitoResolved = new EffectiveProviderContext(exitoRetailerId != Guid.Empty ? exitoRetailerId : null, "EXITO", profile?.Id, PriceScope.OnlineGeneric, "online-generic", ResolutionStatus: ContextResolutionStatus.Fallback, IsFallback: true, ResolvedAt: DateTime.UtcNow, StatusOverride: "ONLINE_GENERIC");
        var d1RegionalOffers = 0;
        var d1UsedFallback = false;
        var exitoRegionalOffers = 0;
        var exitoUsedFallback = false;
        if (latitude.HasValue && longitude.HasValue && exitoProvider is not null)
        {
            try
            {
                var region = await exitoProvider.ResolveRegionAsync(longitude.Value, latitude.Value, ct);
                exitoResolved = new(exitoRetailerId != Guid.Empty ? exitoRetailerId : null, "EXITO", profile?.Id, PriceScope.FulfillmentRegion, region.Key, RegionId: region.Key, ResolutionStatus: ContextResolutionStatus.Resolved, IsFallback: false, ResolvedAt: DateTime.UtcNow, StatusOverride: "RESOLVED");
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            {
                if (ct.IsCancellationRequested) throw;
            }
        }
        D1DeliveryAddress? delivery = null;
        if (profile is { Latitude: not null, Longitude: not null } &&
            !string.IsNullOrWhiteSpace(profile.PostalCode) && !string.IsNullOrWhiteSpace(profile.City) &&
            !string.IsNullOrWhiteSpace(profile.State) && !string.IsNullOrWhiteSpace(profile.Street) &&
            !string.IsNullOrWhiteSpace(profile.Number) && !string.IsNullOrWhiteSpace(profile.Neighborhood))
            delivery = new(profile.PostalCode, profile.City, profile.State, profile.Street,
                profile.Number, profile.Neighborhood, profile.Longitude.Value, profile.Latitude.Value);

        foreach (var query in queries)
        {
            ct.ThrowIfCancellationRequested();
            // Priority 2: Sync D1
            SyncExecutionResult d1Res;
            if (delivery is not null && d1Ingest is not null)
            {
                try
                {
                    using var session = d1SessionFactory?.Invoke() ?? new D1FulfillmentSession();
                    var capture = new D1RegionalCapture(session, d1Ingest);
                    var count = await capture.CaptureSearchAsync(delivery, query!, ct);
                    d1Context = new(d1RetailerId != Guid.Empty ? d1RetailerId : null, "D1", profile?.Id, PriceScope.FulfillmentRegion, "fulfillment:" + session.VerifiedRegionId, FulfillmentRegion: session.VerifiedRegionId, ResolutionStatus: ContextResolutionStatus.Resolved, IsFallback: false, ResolvedAt: DateTime.UtcNow, StatusOverride: "RESOLVED");
                    d1RegionalOffers += count;
                    d1Res = new("D1", "COMPLETED", count, count, count);
                }
                catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or
                    System.Text.Json.JsonException or InvalidDataException)
                {
                    if (ct.IsCancellationRequested) throw;
                    d1UsedFallback = true;
                    d1Res = await ExecuteProviderSyncAsync("D1", query!, null, "BasketFallback", ct);
                }
            }
            else
                d1Res = await ExecuteProviderSyncAsync("D1", query!, null, "Basket", ct);
            results.Add(d1Res);

            // Priority 2: Sync Exito
            var exitoRes = await ExecuteProviderSyncAsync("EXITO", query!, exitoResolved.Key, "Basket", ct);
            if (exitoResolved.Status == "RESOLVED")
            {
                if (exitoRes.Status == "COMPLETED") exitoRegionalOffers += exitoRes.OffersProcessed;
                else
                {
                    exitoUsedFallback = true;
                    exitoRes = await ExecuteProviderSyncAsync("EXITO", query!, null, "BasketFallback", ct);
                }
            }
            results.Add(exitoRes);
        }

                if (d1UsedFallback && d1RegionalOffers > 0)
            d1Context = d1Context with { PriceScope = PriceScope.Unknown, PriceContext = "mixed", ResolutionStatus = ContextResolutionStatus.Fallback, IsFallback = true, StatusOverride = "MIXED_SCOPES" };
        else if (d1UsedFallback || (d1Context.Status == "RESOLVED" && d1RegionalOffers == 0))
            d1Context = d1Context with { PriceScope = PriceScope.OnlineGeneric, PriceContext = "online-generic", ResolutionStatus = ContextResolutionStatus.Fallback, IsFallback = true, StatusOverride = d1UsedFallback ? "ONLINE_GENERIC_FALLBACK" : "NO_REGIONAL_OFFERS" };
                if (exitoUsedFallback && exitoRegionalOffers > 0)
            exitoResolved = exitoResolved with { PriceScope = PriceScope.Unknown, PriceContext = "mixed", ResolutionStatus = ContextResolutionStatus.Fallback, IsFallback = true, StatusOverride = "MIXED_SCOPES" };
        else if (exitoUsedFallback || (exitoResolved.Status == "RESOLVED" && exitoRegionalOffers == 0))
            exitoResolved = exitoResolved with { PriceScope = PriceScope.OnlineGeneric, PriceContext = "online-generic", ResolutionStatus = ContextResolutionStatus.Fallback, IsFallback = true, StatusOverride = exitoUsedFallback ? "ONLINE_GENERIC_FALLBACK" : "NO_REGIONAL_OFFERS" };

        var addressKey = profile is not null
            ? null
            : (!string.IsNullOrWhiteSpace(parsed.NormalizedAddress) ? parsed.NormalizedAddress : locationInput);

        if (!d1Context.IsFallback && d1Context.ResolutionStatus == ContextResolutionStatus.Resolved)
        {
            contextCache?.Set("D1", profile?.Id, addressKey, d1Context);
        }

        if (!exitoResolved.IsFallback && exitoResolved.ResolutionStatus == ContextResolutionStatus.Resolved)
        {
            contextCache?.Set("EXITO", profile?.Id, addressKey, exitoResolved);
        }

        var context = new ShoppingContext(profile?.Id, parsed.NormalizedAddress, latitude, longitude,
            [d1Context, exitoResolved], DateTime.UtcNow,
            latitude.HasValue && longitude.HasValue ? "LOCATION_RESOLVED" : "LOCATION_UNVERIFIED",
            HasFallbackProviders: d1Context.IsFallback || exitoResolved.IsFallback);
        return new BasketSyncResult(basket.Id, basket.Items.Count, queries.Count, results, context);
    }

    public async Task<MaintenanceSyncResult> SyncMaintenanceStaleAsync(int maxItems = 10, CancellationToken ct = default)
    {
        var settings = await db.Settings.FirstOrDefaultAsync(ct) ?? new AppSettings();
        var cutoff = DateTime.UtcNow.AddHours(-settings.PriceTtlHours);

        // Find stale offers that need refresh using freshness service
        var candidates = await db.Offers
            .Where(o => o.CheckedAt < cutoff)
            .OrderBy(o => o.CheckedAt)
            .Take(maxItems * 2)
            .ToListAsync(ct);

        var staleOffers = candidates
            .Where(o => freshnessService.IsStaleOrOutdated(o.CheckedAt, TimeSpan.FromHours(settings.PriceTtlHours)))
            .Take(maxItems)
            .ToList();

        var results = new List<SyncExecutionResult>();
        if (staleOffers.Count == 0)
        {
            return new MaintenanceSyncResult(0, 0, results);
        }

        // Collect product queries for these offers
        var skuIds = staleOffers.Select(o => o.ExternalSkuId).Distinct().ToList();
        var skus = await db.ExternalSkus
            .Where(s => skuIds.Contains(s.Id))
            .ToListAsync(ct);

        var productIds = skus.Select(s => s.ExternalProductId).Distinct().ToList();
        var products = await db.ExternalProducts
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(ct);

        var queries = products
            .Select(p => p.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        foreach (var query in queries)
        {
            ct.ThrowIfCancellationRequested();
            var d1Res = await ExecuteProviderSyncAsync("D1", query, null, "Maintenance", ct);
            results.Add(d1Res);

            var exitoRes = await ExecuteProviderSyncAsync("EXITO", query, null, "Maintenance", ct);
            results.Add(exitoRes);
        }

        return new MaintenanceSyncResult(staleOffers.Count, queries.Count, results);
    }

    public async Task<List<SyncRun>> GetRecentRunsAsync(int limit = 20, CancellationToken ct = default)
    {
        return await db.SyncRuns
            .OrderByDescending(r => r.StartedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(ct);
    }

    private async Task<SyncExecutionResult> ExecuteProviderSyncAsync(
        string provider,
        string query,
        string? context,
        string trigger,
        CancellationToken ct)
    {
        var upper = provider.ToUpperInvariant();
        try
        {
            if (upper == "D1")
            {
                var res = await d1Sync.RefreshAsync(query, trigger, ct);
                return new SyncExecutionResult("D1", res.Status, res.OffersSeen, res.OffersSeen, 0);
            }
            if (upper == "EXITO")
            {
                ProviderContext? pContext = null;
                if (!string.IsNullOrWhiteSpace(context))
                {
                    pContext = new ProviderContext(context);
                }
                var res = await exitoSync.RefreshAsync(query, pContext, trigger, ct);
                return new SyncExecutionResult("EXITO", res.Status, res.OffersSeen, res.OffersSeen, 0);
            }

            return new SyncExecutionResult(provider, "UNKNOWN_PROVIDER", 0, 0, 0, "PROVIDER_NOT_SUPPORTED");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new SyncExecutionResult(provider, "TIMEOUT", 0, 0, 0, "TIMEOUT");
        }
        catch (HttpRequestException ex)
        {
            return new SyncExecutionResult(provider, "NETWORK_ERROR", 0, 0, 0, ex.StatusCode?.ToString() ?? "HTTP_ERROR");
        }
        catch (Exception ex)
        {
            return new SyncExecutionResult(provider, "FAILED", 0, 0, 0, ex.GetType().Name);
        }
    }
}

