using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Api;

public static class LocationEndpoints
{
    public static RouteGroupBuilder MapLocationEndpoints(this RouteGroupBuilder api)
    {
    // GOAL-07: Location and Routing endpoints
    api.MapPost("/location/parse", async (LocationParseRequest request, MarketDb db, HttpClient httpClient, CancellationToken ct) =>
    {
        var profiles = await db.LocationProfiles.AsNoTracking().ToListAsync(ct);
        var result = LocationInputParser.Parse(request?.Input, profiles);
        if (result.Source == LocationInputSource.GoogleMapsShortUrl && !string.IsNullOrWhiteSpace(request?.Input))
        {
            result = await LocationInputParser.ResolveGoogleMapsShortUrlAsync(request.Input, httpClient, ct);
        }
        return Results.Ok(new
        {
            result.OriginalInput, result.NormalizedAddress, result.Latitude, result.Longitude,
            Source = result.Source.ToString(), Status = result.Status.ToString(), result.ErrorMessage
        });
    });
    
    api.MapPost("/location/geocode", async (LocationGeocodeRequest request, IGeocodingProvider geocoder, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(request?.Address))
        {
            return Results.BadRequest(new { code = "EMPTY_ADDRESS", message = "La dirección no puede estar vacía." });
        }
        var result = await geocoder.GeocodeDetailedAsync(request.Address, ct);
        if (result == null)
        {
            return Results.NotFound(new { code = "GEOCODE_FAILED", message = "No se pudo geocodificar la dirección." });
        }
        return Results.Ok(result);
    });
    
    api.MapGet("/location/profiles", async (MarketDb db, CancellationToken ct) =>
    {
        var profiles = await db.LocationProfiles.AsNoTracking().OrderBy(p => p.Label).ToListAsync(ct);
        return Results.Ok(profiles);
    });
    
    api.MapPost("/location/profiles", async (SaveLocationProfileRequest request, MarketDb db, IGeocodingProvider geocoder, ICommercialContextCache? contextCache, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(request.Label) || string.IsNullOrWhiteSpace(request.Address))
        {
            return Results.BadRequest(new { code = "INVALID_PROFILE", message = "Etiqueta y dirección son obligatorias." });
        }
    
        decimal? lat = request.Latitude;
        decimal? lon = request.Longitude;
        var formattedAddress = request.FormattedAddress;
        var country = request.Country;
        var countryCode = request.CountryCode;
        var state = request.State;
        var stateCode = request.StateCode;
        var city = request.City;
        var postalCode = request.PostalCode;
        var neighborhood = request.Neighborhood;
        var street = request.Street;
        var number = request.Number ?? request.StreetNumber;
    
        if (!lat.HasValue || !lon.HasValue)
        {
            var geo = geocoder is GoogleGeocodingProvider google
                ? await google.GeocodeDetailedAsync(request.Address, ct)
                : null;
            if (geo is { ProviderStatus: "LIVE_GOOGLE" or "CACHED_VERIFIED" })
            {
                lat = geo.Latitude;
                lon = geo.Longitude;
                formattedAddress ??= geo.FormattedAddress;
                if (geo.Components != null)
                {
                    country ??= geo.Components.Country;
                    countryCode ??= geo.Components.CountryCode;
                    state ??= geo.Components.State;
                    stateCode ??= geo.Components.StateCode;
                    city ??= geo.Components.City;
                    postalCode ??= geo.Components.PostalCode;
                    neighborhood ??= geo.Components.Neighborhood;
                    street ??= geo.Components.Street;
                    number ??= geo.Components.StreetNumber;
                }
            }
        }
        else if (lat.HasValue && lon.HasValue && (street == null || number == null || neighborhood == null) && geocoder.IsConfigured)
        {
            var rev = await geocoder.ReverseGeocodeAsync(lat.Value, lon.Value, ct);
            if (rev?.Components != null)
            {
                country ??= rev.Components.Country;
                countryCode ??= rev.Components.CountryCode;
                state ??= rev.Components.State;
                stateCode ??= rev.Components.StateCode;
                city ??= rev.Components.City;
                postalCode ??= rev.Components.PostalCode;
                neighborhood ??= rev.Components.Neighborhood;
                street ??= rev.Components.Street;
                number ??= rev.Components.StreetNumber;
                formattedAddress ??= rev.FormattedAddress;
            }
        }
    
        var profile = new LocationProfile
        {
            Label = request.Label.Trim(),
            Address = request.Address.Trim(),
            FormattedAddress = formattedAddress?.Trim(),
            Latitude = lat,
            Longitude = lon,
            Country = country?.Trim(),
            CountryCode = countryCode?.Trim(),
            State = state?.Trim(),
            StateCode = stateCode?.Trim(),
            City = city?.Trim(),
            PostalCode = postalCode?.Trim(),
            Neighborhood = neighborhood?.Trim(),
            Street = street?.Trim(),
            Number = number?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    
        db.LocationProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/location/profiles/{profile.Id}", profile);
    });

    api.MapPut("/location/profiles/{id:guid}", async (Guid id, SaveLocationProfileRequest request, MarketDb db, IGeocodingProvider geocoder, ICommercialContextCache? contextCache, CancellationToken ct) =>
    {
        var profile = await db.LocationProfiles.FindAsync([id], ct);
        if (profile == null) return Results.NotFound(new { code = "NOT_FOUND", message = "Perfil de ubicación no encontrado." });

        if (string.IsNullOrWhiteSpace(request.Label) || string.IsNullOrWhiteSpace(request.Address))
        {
            return Results.BadRequest(new { code = "INVALID_PROFILE", message = "Etiqueta y dirección son obligatorias." });
        }

        decimal? lat = request.Latitude;
        decimal? lon = request.Longitude;
        var formattedAddress = request.FormattedAddress;
        var country = request.Country;
        var countryCode = request.CountryCode;
        var state = request.State;
        var stateCode = request.StateCode;
        var city = request.City;
        var postalCode = request.PostalCode;
        var neighborhood = request.Neighborhood;
        var street = request.Street;
        var number = request.Number ?? request.StreetNumber;

        if (!lat.HasValue || !lon.HasValue)
        {
            var geo = geocoder is GoogleGeocodingProvider google
                ? await google.GeocodeDetailedAsync(request.Address, ct)
                : null;
            if (geo is { ProviderStatus: "LIVE_GOOGLE" or "CACHED_VERIFIED" })
            {
                lat = geo.Latitude;
                lon = geo.Longitude;
                formattedAddress ??= geo.FormattedAddress;
                if (geo.Components != null)
                {
                    country ??= geo.Components.Country;
                    countryCode ??= geo.Components.CountryCode;
                    state ??= geo.Components.State;
                    stateCode ??= geo.Components.StateCode;
                    city ??= geo.Components.City;
                    postalCode ??= geo.Components.PostalCode;
                    neighborhood ??= geo.Components.Neighborhood;
                    street ??= geo.Components.Street;
                    number ??= geo.Components.StreetNumber;
                }
            }
        }
        else if (lat.HasValue && lon.HasValue && (street == null || number == null || neighborhood == null) && geocoder.IsConfigured)
        {
            var rev = await geocoder.ReverseGeocodeAsync(lat.Value, lon.Value, ct);
            if (rev?.Components != null)
            {
                country ??= rev.Components.Country;
                countryCode ??= rev.Components.CountryCode;
                state ??= rev.Components.State;
                stateCode ??= rev.Components.StateCode;
                city ??= rev.Components.City;
                postalCode ??= rev.Components.PostalCode;
                neighborhood ??= rev.Components.Neighborhood;
                street ??= rev.Components.Street;
                number ??= rev.Components.StreetNumber;
                formattedAddress ??= rev.FormattedAddress;
            }
        }

        profile.Label = request.Label.Trim();
        profile.Address = request.Address.Trim();
        profile.FormattedAddress = formattedAddress?.Trim();
        profile.Latitude = lat;
        profile.Longitude = lon;
        profile.Country = country?.Trim();
        profile.CountryCode = countryCode?.Trim();
        profile.State = state?.Trim();
        profile.StateCode = stateCode?.Trim();
        profile.City = city?.Trim();
        profile.PostalCode = postalCode?.Trim();
        profile.Neighborhood = neighborhood?.Trim();
        profile.Street = street?.Trim();
        profile.Number = number?.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        contextCache?.Invalidate(id);
        return Results.Ok(profile);
    });
    
    api.MapDelete("/location/profiles/{id:guid}", async (Guid id, MarketDb db, ICommercialContextCache? contextCache, CancellationToken ct) =>
    {
        var profile = await db.LocationProfiles.FindAsync(new object[] { id }, ct);
        if (profile == null) return Results.NotFound();
        db.LocationProfiles.Remove(profile);
        await db.SaveChangesAsync(ct);
        contextCache?.Invalidate(id);
        return Results.NoContent();
    });
    
    api.MapGet("/location/stores", async (decimal? originLat, decimal? originLon, string? retailer, MarketDb db, CancellationToken ct) =>
    {
        var stores = await db.Stores.AsNoTracking().ToListAsync(ct);
        var retailers = await db.Retailers.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r, ct);
    
        if (!string.IsNullOrWhiteSpace(retailer))
        {
            stores = stores.Where(s =>
            {
                if (retailers.TryGetValue(s.RetailerId, out var ret))
                {
                    return string.Equals(ret.Code, retailer, StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(ret.Name, retailer, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }).ToList();
        }
    
        var dtos = stores.Select(s =>
        {
            retailers.TryGetValue(s.RetailerId, out var ret);
            decimal? distanceKm = null;
            if (originLat.HasValue && originLon.HasValue && s.Latitude.HasValue && s.Longitude.HasValue)
            {
                distanceKm = HaversineService.CalculateDistanceKm(originLat.Value, originLon.Value, s.Latitude.Value, s.Longitude.Value);
            }
            return new
            {
                s.Id,
                RetailerCode = ret?.Code ?? "",
                RetailerName = ret?.Name ?? "",
                s.ExternalId,
                s.Name,
                s.Latitude,
                s.Longitude,
                DistanceKm = distanceKm.HasValue ? Math.Round(distanceKm.Value, 2) : (decimal?)null,
                EstimatedRoadKm = distanceKm.HasValue ? Math.Round(HaversineService.EstimateRoadDistanceKm(distanceKm.Value), 2) : (decimal?)null
            };
        });
    
        if (originLat.HasValue && originLon.HasValue)
        {
            dtos = dtos.OrderBy(s => s.DistanceKm ?? decimal.MaxValue);
        }
    
        return Results.Ok(dtos);
    });
    
    api.MapPost("/location/stores/seed", async (MarketDb db) =>
    {
        await SeedMedellinDataAsync(db);
        return Results.Ok(new { message = "Stores and profiles seeded successfully." });
    });
    
    api.MapPost("/routes/optimize", async (OptimizeRouteRequest request, StoreRouteOptimizer optimizer, MarketDb db, CancellationToken ct) =>
    {
        if (request.StoreIds == null || request.StoreIds.Count == 0)
        {
            return Results.BadRequest(new { code = "EMPTY_STORES", message = "Indique al menos una tienda para optimizar la ruta." });
        }
    
        var stores = await db.Stores
            .Where(s => request.StoreIds.Contains(s.Id))
            .AsNoTracking()
            .ToListAsync(ct);
    
        if (stores.Count == 0)
        {
            return Results.NotFound(new { code = "STORES_NOT_FOUND", message = "No se encontraron las tiendas especificadas." });
        }
    
        var plan = await optimizer.OptimizeRouteAsync(
            request.OriginLatitude,
            request.OriginLongitude,
            stores,
            returnToOrigin: request.ReturnToOrigin,
            transportMode: request.TransportMode,
            costPerKm: request.CostPerKm,
            maxRouteKm: request.MaxRouteKm,
            cancellationToken: ct
        );
    
        return Results.Ok(plan);
    });
    
    api.MapGet("/location/usage", (GoogleServiceUsageTracker tracker, GoogleGeocodingProvider geocoder, GoogleRoutingProvider router) =>
    {
        var usages = tracker.GetRecentUsage(30);
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var todayUsage = usages.FirstOrDefault(u => u.Date == today);
        return Results.Ok(new
        {
            GeocodingConfigured = geocoder.IsConfigured,
            RoutingConfigured = router.IsConfigured,
            TodayGeocodeCalls = todayUsage?.GeocodeCount ?? 0,
            TodayRouteCalls = todayUsage?.RouteCount ?? 0,
            RecentDailyUsage = usages
        });
    });

        return api;
    }

    static async Task SeedMedellinDataAsync(MarketDb db)
    {
        var d1 = await db.Retailers.FirstOrDefaultAsync(r => r.Code == "D1");
        if (d1 is null)
        {
            d1 = new Retailer { Code = "D1", Name = "D1" };
            db.Retailers.Add(d1);
            await db.SaveChangesAsync();
        }
    
        var exito = await db.Retailers.FirstOrDefaultAsync(r => r.Code == "EXITO");
        if (exito is null)
        {
            exito = new Retailer { Code = "EXITO", Name = "Éxito" };
            db.Retailers.Add(exito);
            await db.SaveChangesAsync();
        }
    
        var ara = await db.Retailers.FirstOrDefaultAsync(r => r.Code == "ARA");
        if (ara is null)
        {
            ara = new Retailer { Code = "ARA", Name = "Tiendas Ara" };
            db.Retailers.Add(ara);
            await db.SaveChangesAsync();
        }
    
        if (!await db.Stores.AnyAsync())
        {
            var medellinStores = new List<Store>
            {
                new() { RetailerId = d1.Id, ExternalId = "D1-LAURELES", Name = "D1 Laureles (Circular 1)", Latitude = 6.2425m, Longitude = -75.5925m },
                new() { RetailerId = d1.Id, ExternalId = "D1-POBLADO", Name = "D1 El Poblado (Cra 43A)", Latitude = 6.2088m, Longitude = -75.5678m },
                new() { RetailerId = d1.Id, ExternalId = "D1-BELEN", Name = "D1 Belén (Calle 30)", Latitude = 6.2312m, Longitude = -75.5991m },
                new() { RetailerId = d1.Id, ExternalId = "D1-CENTRO", Name = "D1 Centro (Calle 50)", Latitude = 6.2518m, Longitude = -75.5636m },
                new() { RetailerId = d1.Id, ExternalId = "D1-ENVIGADO", Name = "D1 Envigado (Parque)", Latitude = 6.1685m, Longitude = -75.5861m },
    
                new() { RetailerId = exito.Id, ExternalId = "EXITO-LAURELES", Name = "Éxito Laureles", Latitude = 6.2461m, Longitude = -75.5942m },
                new() { RetailerId = exito.Id, ExternalId = "EXITO-POBLADO", Name = "Éxito Poblado", Latitude = 6.2072m, Longitude = -75.5714m },
                new() { RetailerId = exito.Id, ExternalId = "EXITO-COLOMBIA", Name = "Éxito Colombia", Latitude = 6.2570m, Longitude = -75.5855m },
                new() { RetailerId = exito.Id, ExternalId = "EXITO-CENTRO", Name = "Éxito San Antonio Centro", Latitude = 6.2482m, Longitude = -75.5691m },
                new() { RetailerId = exito.Id, ExternalId = "EXITO-ENVIGADO", Name = "Éxito Viva Envigado", Latitude = 6.1775m, Longitude = -75.5912m },
    
                new() { RetailerId = ara.Id, ExternalId = "ARA-LAURELES", Name = "Ara Laureles (Av Nutibara)", Latitude = 6.2440m, Longitude = -75.5950m },
                new() { RetailerId = ara.Id, ExternalId = "ARA-BELEN", Name = "Ara Belén (Parque)", Latitude = 6.2320m, Longitude = -75.5980m },
                new() { RetailerId = ara.Id, ExternalId = "ARA-CENTRO", Name = "Ara San Antonio Centro", Latitude = 6.2490m, Longitude = -75.5680m }
            };
            db.Stores.AddRange(medellinStores);
            foreach (var store in medellinStores) store.VerificationStatus = "DEMO";
            await db.SaveChangesAsync();
        }
        else if (!await db.Stores.AnyAsync(s => s.RetailerId == ara.Id))
        {
            var araStores = new List<Store>
            {
                new() { RetailerId = ara.Id, ExternalId = "ARA-LAURELES", Name = "Ara Laureles (Av Nutibara)", Latitude = 6.2440m, Longitude = -75.5950m },
                new() { RetailerId = ara.Id, ExternalId = "ARA-BELEN", Name = "Ara Belén (Parque)", Latitude = 6.2320m, Longitude = -75.5980m },
                new() { RetailerId = ara.Id, ExternalId = "ARA-CENTRO", Name = "Ara San Antonio Centro", Latitude = 6.2490m, Longitude = -75.5680m }
            };
            db.Stores.AddRange(araStores);
            foreach (var store in araStores) store.VerificationStatus = "DEMO";
            await db.SaveChangesAsync();
        }
    
                var lavaquita = await db.Retailers.FirstOrDefaultAsync(r => r.Code == "LAVAQUITA");
        if (lavaquita is null)
        {
            lavaquita = new Retailer { Code = "LAVAQUITA", Name = "Supermercado La Vaquita" };
            db.Retailers.Add(lavaquita);
            await db.SaveChangesAsync();
        }

        var mundohuevo = await db.Retailers.FirstOrDefaultAsync(r => r.Code == "MUNDOHUEVO");
        if (mundohuevo is null)
        {
            mundohuevo = new Retailer { Code = "MUNDOHUEVO", Name = "Mundo Huevo" };
            db.Retailers.Add(mundohuevo);
            await db.SaveChangesAsync();
        }

        var olimpica = await db.Retailers.FirstOrDefaultAsync(r => r.Code == "OLIMPICA");
        if (olimpica is null)
        {
            olimpica = new Retailer { Code = "OLIMPICA", Name = "Supertiendas Olímpica" };
            db.Retailers.Add(olimpica);
            await db.SaveChangesAsync();
        }

        var jumbo = await db.Retailers.FirstOrDefaultAsync(r => r.Code == "JUMBO");
        if (jumbo is null)
        {
            jumbo = new Retailer { Code = "JUMBO", Name = "Hipermercados Jumbo" };
            db.Retailers.Add(jumbo);
            await db.SaveChangesAsync();
        }

        var pricesmart = await db.Retailers.FirstOrDefaultAsync(r => r.Code == "PRICESMART");
        if (pricesmart is null)
        {
            pricesmart = new Retailer { Code = "PRICESMART", Name = "PriceSmart Club de Membresía" };
            db.Retailers.Add(pricesmart);
            await db.SaveChangesAsync();
        }

        if (!await db.Stores.AnyAsync(s => s.RetailerId == lavaquita.Id))
        {
            db.Stores.AddRange(new Store[]
            {
                new() { RetailerId = lavaquita.Id, ExternalId = "LAVAQUITA-LAURELES", Name = "La Vaquita Laureles (Av 33)", Latitude = 6.2372m, Longitude = -75.5921m, VerificationStatus = "DEMO" },
                new() { RetailerId = lavaquita.Id, ExternalId = "LAVAQUITA-POBLADO", Name = "La Vaquita Poblado (Calle 10)", Latitude = 6.2105m, Longitude = -75.5702m, VerificationStatus = "DEMO" },
                new() { RetailerId = lavaquita.Id, ExternalId = "LAVAQUITA-SABANETA", Name = "La Vaquita Sabaneta", Latitude = 6.1512m, Longitude = -75.6154m, VerificationStatus = "DEMO" }
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Stores.AnyAsync(s => s.RetailerId == mundohuevo.Id))
        {
            db.Stores.AddRange(new Store[]
            {
                new() { RetailerId = mundohuevo.Id, ExternalId = "MH-LAURELES", Name = "Mundo Huevo Laureles", Latitude = 6.2415m, Longitude = -75.5942m, VerificationStatus = "DEMO" },
                new() { RetailerId = mundohuevo.Id, ExternalId = "MH-BELEN", Name = "Mundo Huevo Belén", Latitude = 6.2301m, Longitude = -75.5998m, VerificationStatus = "DEMO" },
                new() { RetailerId = mundohuevo.Id, ExternalId = "MH-POBLADO", Name = "Mundo Huevo Poblado", Latitude = 6.2089m, Longitude = -75.5714m, VerificationStatus = "DEMO" }
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Stores.AnyAsync(s => s.RetailerId == olimpica.Id))
        {
            db.Stores.AddRange(new Store[]
            {
                new() { RetailerId = olimpica.Id, ExternalId = "OLI-CENTRO", Name = "Olímpica Junín Centro", Latitude = 6.2514m, Longitude = -75.5681m, VerificationStatus = "DEMO" },
                new() { RetailerId = olimpica.Id, ExternalId = "OLI-BELEN", Name = "Olímpica Belén Los Alpes", Latitude = 6.2321m, Longitude = -75.6012m, VerificationStatus = "DEMO" },
                new() { RetailerId = olimpica.Id, ExternalId = "OLI-ENVIGADO", Name = "Olímpica Envigado Principal", Latitude = 6.1689m, Longitude = -75.5843m, VerificationStatus = "DEMO" }
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Stores.AnyAsync(s => s.RetailerId == jumbo.Id))
        {
            db.Stores.AddRange(new Store[]
            {
                new() { RetailerId = jumbo.Id, ExternalId = "JMB-VEGAS", Name = "Jumbo Las Vegas", Latitude = 6.2034m, Longitude = -75.5789m, VerificationStatus = "DEMO" },
                new() { RetailerId = jumbo.Id, ExternalId = "JMB-SANTAFE", Name = "Jumbo Santafé Medellín", Latitude = 6.1978m, Longitude = -75.5734m, VerificationStatus = "DEMO" },
                new() { RetailerId = jumbo.Id, ExternalId = "JMB-65", Name = "Jumbo Carrera 65", Latitude = 6.2601m, Longitude = -75.5812m, VerificationStatus = "DEMO" }
            });
            await db.SaveChangesAsync();
        }

        if (!await db.Stores.AnyAsync(s => s.RetailerId == pricesmart.Id))
        {
            db.Stores.AddRange(new Store[]
            {
                new() { RetailerId = pricesmart.Id, ExternalId = "PS-MEDELLIN", Name = "PriceSmart Medellín (El Poblado)", Latitude = 6.2045m, Longitude = -75.5768m, VerificationStatus = "DEMO" }
            });
            await db.SaveChangesAsync();
        }

        if (!await db.LocationProfiles.AnyAsync())
        {
            var defaultProfiles = new List<LocationProfile>
            {
                new() { Label = "Casa", Address = "Circular 1 # 70-20, Laureles, Medellín", Latitude = 6.2425m, Longitude = -75.5925m },
                new() { Label = "Trabajo", Address = "Carrera 43A # 1-50, El Poblado, Medellín", Latitude = 6.2088m, Longitude = -75.5678m }
            };
            db.LocationProfiles.AddRange(defaultProfiles);
            await db.SaveChangesAsync();
        }
    }
}
