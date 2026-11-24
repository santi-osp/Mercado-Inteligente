using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure.Stores.D1;
using MercadoInteligente.Infrastructure.Stores.Exito;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure;

public sealed class ShoppingContextService(
    MarketDb db,
    IGeocodingProvider? geocoder = null,
    ExitoProvider? exitoProvider = null,
    Func<D1FulfillmentSession>? d1SessionFactory = null,
    ICommercialContextCache? contextCache = null
) : IShoppingContextResolver
{
    public async Task<ShoppingContext> ResolveContextAsync(
        string? locationInput = null,
        decimal? reqLatitude = null,
        decimal? reqLongitude = null,
        CancellationToken ct = default)
    {
        var profiles = await db.LocationProfiles.AsNoTracking().ToListAsync(ct);
        var parsed = LocationInputParser.Parse(locationInput ?? "", profiles);

        var profile = profiles.FirstOrDefault(p =>
            string.Equals(p.Label, locationInput?.Trim(), StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrWhiteSpace(locationInput) && string.Equals(p.Address, locationInput.Trim(), StringComparison.OrdinalIgnoreCase)) ||
            (p.Latitude == reqLatitude && p.Longitude == reqLongitude && p.Latitude.HasValue) ||
            (p.Latitude == parsed.Latitude && p.Longitude == parsed.Longitude && p.Latitude.HasValue));

        decimal? latitude = reqLatitude ?? parsed.Latitude;
        decimal? longitude = reqLongitude ?? parsed.Longitude;

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

        var addressKey = profile is not null
            ? null
            : (!string.IsNullOrWhiteSpace(parsed.NormalizedAddress) ? parsed.NormalizedAddress : locationInput);

        var retailers = await db.Retailers.AsNoTracking().ToDictionaryAsync(r => r.Code, r => r.Id, ct);
        retailers.TryGetValue("D1", out var d1RetailerId);
        retailers.TryGetValue("EXITO", out var exitoRetailerId);
        retailers.TryGetValue("ARA", out var araRetailerId);

        // 1. Resolve Éxito
        EffectiveProviderContext exitoResolved;
        var cachedExito = contextCache?.Get("EXITO", profile?.Id, addressKey);
        if (cachedExito is not null && !cachedExito.IsFallback)
        {
            exitoResolved = cachedExito;
        }
        else if (latitude.HasValue && longitude.HasValue && exitoProvider is not null)
        {
            try
            {
                var region = await exitoProvider.ResolveRegionAsync(longitude.Value, latitude.Value, ct);
                exitoResolved = new EffectiveProviderContext(
                    RetailerId: exitoRetailerId != Guid.Empty ? exitoRetailerId : null,
                    ProviderCode: "EXITO",
                    RequestedLocationProfileId: profile?.Id,
                    PriceScope: PriceScope.FulfillmentRegion,
                    PriceContext: region.Key,
                    RegionId: region.Key,
                    ResolutionStatus: ContextResolutionStatus.Resolved,
                    IsFallback: false,
                    ResolvedAt: DateTime.UtcNow,
                    StatusOverride: "RESOLVED"
                );
                contextCache?.Set("EXITO", profile?.Id, addressKey, exitoResolved);
            }
            catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
            {
                if (ct.IsCancellationRequested) throw;
                exitoResolved = new EffectiveProviderContext(
                    RetailerId: exitoRetailerId != Guid.Empty ? exitoRetailerId : null,
                    ProviderCode: "EXITO",
                    RequestedLocationProfileId: profile?.Id,
                    PriceScope: PriceScope.OnlineGeneric,
                    PriceContext: "online-generic",
                    ResolutionStatus: ContextResolutionStatus.Fallback,
                    IsFallback: true,
                    ResolvedAt: DateTime.UtcNow,
                    StatusOverride: "ONLINE_GENERIC_FALLBACK"
                );
            }
        }
        else
        {
            exitoResolved = new EffectiveProviderContext(
                RetailerId: exitoRetailerId != Guid.Empty ? exitoRetailerId : null,
                ProviderCode: "EXITO",
                RequestedLocationProfileId: profile?.Id,
                PriceScope: PriceScope.OnlineGeneric,
                PriceContext: "online-generic",
                ResolutionStatus: ContextResolutionStatus.Fallback,
                IsFallback: true,
                ResolvedAt: DateTime.UtcNow,
                StatusOverride: "ONLINE_GENERIC"
            );
        }

        // 2. Resolve D1
        EffectiveProviderContext d1Context;
        var cachedD1 = contextCache?.Get("D1", profile?.Id, addressKey);
        if (cachedD1 is not null && !cachedD1.IsFallback)
        {
            d1Context = cachedD1;
        }
        else
        {
            D1DeliveryAddress? delivery = null;
            if (profile is { Latitude: not null, Longitude: not null } &&
                !string.IsNullOrWhiteSpace(profile.PostalCode) && !string.IsNullOrWhiteSpace(profile.City) &&
                !string.IsNullOrWhiteSpace(profile.State) && !string.IsNullOrWhiteSpace(profile.Street) &&
                !string.IsNullOrWhiteSpace(profile.Number) && !string.IsNullOrWhiteSpace(profile.Neighborhood))
            {
                delivery = new(profile.PostalCode, profile.City, profile.State, profile.Street,
                    profile.Number, profile.Neighborhood, profile.Longitude.Value, profile.Latitude.Value);
            }

            if (delivery is not null && d1SessionFactory != null)
            {
                try
                {
                    using var session = d1SessionFactory.Invoke();
                    var regionId = await session.EstablishAsync(delivery, ct);
                    if (!string.IsNullOrWhiteSpace(regionId))
                    {
                        d1Context = new EffectiveProviderContext(
                            RetailerId: d1RetailerId != Guid.Empty ? d1RetailerId : null,
                            ProviderCode: "D1",
                            RequestedLocationProfileId: profile?.Id,
                            PriceScope: PriceScope.FulfillmentRegion,
                            PriceContext: "fulfillment:" + regionId,
                            FulfillmentRegion: regionId,
                            ResolutionStatus: ContextResolutionStatus.Resolved,
                            IsFallback: false,
                            ResolvedAt: DateTime.UtcNow,
                            StatusOverride: "RESOLVED"
                        );
                        contextCache?.Set("D1", profile?.Id, addressKey, d1Context);
                    }
                    else
                    {
                        d1Context = new EffectiveProviderContext(
                            RetailerId: d1RetailerId != Guid.Empty ? d1RetailerId : null,
                            ProviderCode: "D1",
                            RequestedLocationProfileId: profile?.Id,
                            PriceScope: PriceScope.OnlineGeneric,
                            PriceContext: "online-generic",
                            ResolutionStatus: ContextResolutionStatus.Fallback,
                            IsFallback: true,
                            ResolvedAt: DateTime.UtcNow,
                            StatusOverride: "ONLINE_GENERIC"
                        );
                    }
                }
                catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or
                    System.Text.Json.JsonException or InvalidDataException or ArgumentException)
                {
                    if (ct.IsCancellationRequested) throw;
                    d1Context = new EffectiveProviderContext(
                        RetailerId: d1RetailerId != Guid.Empty ? d1RetailerId : null,
                        ProviderCode: "D1",
                        RequestedLocationProfileId: profile?.Id,
                        PriceScope: PriceScope.OnlineGeneric,
                        PriceContext: "online-generic",
                        ResolutionStatus: ContextResolutionStatus.Fallback,
                        IsFallback: true,
                        ResolvedAt: DateTime.UtcNow,
                        StatusOverride: "ONLINE_GENERIC_FALLBACK"
                    );
                }
            }
            else
            {
                d1Context = new EffectiveProviderContext(
                    RetailerId: d1RetailerId != Guid.Empty ? d1RetailerId : null,
                    ProviderCode: "D1",
                    RequestedLocationProfileId: profile?.Id,
                    PriceScope: PriceScope.OnlineGeneric,
                    PriceContext: "online-generic",
                    ResolutionStatus: ContextResolutionStatus.Fallback,
                    IsFallback: true,
                    ResolvedAt: DateTime.UtcNow,
                    StatusOverride: "ONLINE_GENERIC"
                );
            }
        }

        // 3. Resolve Ara
        var araContext = new EffectiveProviderContext(
            RetailerId: araRetailerId != Guid.Empty ? araRetailerId : null,
            ProviderCode: "ARA",
            RequestedLocationProfileId: profile?.Id,
            PriceScope: PriceScope.Store,
            PriceContext: "catalog",
            ResolutionStatus: ContextResolutionStatus.Resolved,
            IsFallback: false,
            ResolvedAt: DateTime.UtcNow,
            StatusOverride: "RESOLVED"
        );

        var validationStatus = latitude.HasValue && longitude.HasValue
            ? "LOCATION_RESOLVED"
            : "LOCATION_UNVERIFIED";

        return new ShoppingContext(
            LocationProfileId: profile?.Id,
            NormalizedAddress: parsed.NormalizedAddress ?? profile?.Address,
            Latitude: latitude,
            Longitude: longitude,
            ProviderContexts: [d1Context, exitoResolved, araContext],
            ResolvedAt: DateTime.UtcNow,
            ValidationStatus: validationStatus,
            HasFallbackProviders: d1Context.IsFallback || exitoResolved.IsFallback
        );
    }
}
