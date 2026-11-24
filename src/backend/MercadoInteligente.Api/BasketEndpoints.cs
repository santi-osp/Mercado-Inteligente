using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Api;

public static class BasketEndpoints
{
    public static RouteGroupBuilder MapBasketEndpoints(this RouteGroupBuilder api)
    {
    api.MapGet("/baskets", async (MarketDb db) => await db.Baskets.Include(x => x.Items).AsNoTracking().ToListAsync());
    api.MapGet("/baskets/{id:guid}", async (Guid id, MarketDb db) =>
    {
        var basket = await db.Baskets.Include(x => x.Items).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        return basket is null ? Results.NotFound(new { code = "NOT_FOUND", message = "Cesta no encontrada." }) : Results.Ok(basket);
    });
    api.MapPost("/baskets", async (HttpContext context, MarketDb db) =>
    {
        CreateBasketRequest? input = null;
        if (context.Request.ContentLength > 0 || (context.Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true))
        {
            try
            {
                input = await context.Request.ReadFromJsonAsync<CreateBasketRequest>();
            }
            catch
            {
                // Ignored if empty or invalid json
            }
        }

        var basket = new Basket
        {
            Name = !string.IsNullOrWhiteSpace(input?.Name) ? input.Name.Trim() : "Mi lista",
            Budget = input?.Budget > 0 ? input.Budget : null,
            MaxStores = input?.MaxStores.HasValue == true ? Math.Clamp(input.MaxStores.Value, 1, 5) : 3,
            PreferredStrategy = input?.PreferredStrategy ?? OptimizationStrategy.Balanced
        };
        db.Baskets.Add(basket);
        await db.SaveChangesAsync();
        return Results.Created($"/api/v1/baskets/{basket.Id}", basket);
    });
    api.MapMethods("/baskets/{id:guid}", new[] { "PUT", "PATCH" }, async (Guid id, UpdateBasketRequest input, MarketDb db) =>
    {
        var basket = await db.Baskets.SingleOrDefaultAsync(x => x.Id == id);
        if (basket is null) return Results.NotFound(new { code = "NOT_FOUND", message = "Cesta no encontrada." });
        if (!string.IsNullOrWhiteSpace(input.Name)) basket.Name = input.Name.Trim();
        if (input.Budget.HasValue) basket.Budget = input.Budget.Value > 0 ? input.Budget.Value : null;
        if (input.MaxStores.HasValue) basket.MaxStores = Math.Clamp(input.MaxStores.Value, 1, 5);
        if (input.PreferredStrategy.HasValue) basket.PreferredStrategy = input.PreferredStrategy.Value;
        await db.SaveChangesAsync();
        return Results.Ok(basket);
    });
    api.MapPost("/baskets/{id:guid}/items", async (Guid id, AddBasketItemRequest input, MarketDb db) =>
    {
        if (string.IsNullOrWhiteSpace(input.Query) || input.Query.Length > 500 || input.Quantity <= 0 ||
            input.Notes?.Length > 2000 || input.ProductVariantId == Guid.Empty)
            return Results.BadRequest(new { code = "INVALID_BASKET_ITEM", message = "Indique un producto y una cantidad positiva." });
        var basket = await db.Baskets.SingleOrDefaultAsync(x => x.Id == id);
        if (basket is null) return Results.NotFound(new { code = "NOT_FOUND", message = "Cesta no encontrada." });
        if (input.ProductVariantId is Guid variantId && !await db.ProductVariants.AnyAsync(x => x.Id == variantId))
            return Results.BadRequest(new { code = "INVALID_BASKET_ITEM", message = "La variante indicada no existe." });
        var item = new BasketItem
        {
            BasketId = id,
            Query = input.Query.Trim(),
            Quantity = input.Quantity,
            ProductVariantId = input.ProductVariantId,
            Dimension = input.Dimension,
            Unit = input.Unit?.Trim(),
            AllowSubstitutesOverride = input.AllowSubstitutesOverride,
            SizeTolerancePercentOverride = input.SizeTolerancePercentOverride,
            PreferredBrand = input.PreferredBrand?.Trim(),
            ExcludedBrand = input.ExcludedBrand?.Trim(),
            Notes = input.Notes
        };
        db.BasketItems.Add(item);
        await db.SaveChangesAsync();
        return Results.Created($"/api/v1/baskets/{id}/items/{item.Id}", item);
    });
    api.MapPut("/baskets/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, UpdateBasketItemRequest input, MarketDb db) =>
    {
        var item = await db.BasketItems.SingleOrDefaultAsync(x => x.BasketId == id && x.Id == itemId);
        if (item is null) return Results.NotFound(new { code = "NOT_FOUND", message = "Item no encontrado." });
        if (!string.IsNullOrWhiteSpace(input.Query)) item.Query = input.Query.Trim();
        if (input.Quantity.HasValue && input.Quantity.Value > 0) item.Quantity = input.Quantity.Value;
        if (input.Dimension.HasValue) item.Dimension = input.Dimension.Value;
        if (input.Unit != null) item.Unit = string.IsNullOrWhiteSpace(input.Unit) ? null : input.Unit.Trim();
        if (input.AllowSubstitutesOverride.HasValue) item.AllowSubstitutesOverride = input.AllowSubstitutesOverride.Value;
        if (input.SizeTolerancePercentOverride.HasValue) item.SizeTolerancePercentOverride = Math.Clamp(input.SizeTolerancePercentOverride.Value, 0, 100);
        if (input.PreferredBrand != null) item.PreferredBrand = string.IsNullOrWhiteSpace(input.PreferredBrand) ? null : input.PreferredBrand.Trim();
        if (input.ExcludedBrand != null) item.ExcludedBrand = string.IsNullOrWhiteSpace(input.ExcludedBrand) ? null : input.ExcludedBrand.Trim();
        if (input.Notes != null) item.Notes = input.Notes;
        await db.SaveChangesAsync();
        return Results.Ok(item);
    });
    api.MapDelete("/baskets/{id:guid}/items/{itemId:guid}", async (Guid id, Guid itemId, MarketDb db) =>
    {
        var item = await db.BasketItems.SingleOrDefaultAsync(x => x.BasketId == id && x.Id == itemId);
        if (item is null) return Results.NotFound(new { code = "NOT_FOUND", message = "Item no encontrado." });
        db.BasketItems.Remove(item);
        await db.SaveChangesAsync();
        return Results.NoContent();
    });
    api.MapDelete("/baskets/{id:guid}", async (Guid id, MarketDb db) =>
    {
        var basket = await db.Baskets.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == id);
        if (basket is null) return Results.NotFound(new { code = "NOT_FOUND", message = "Cesta no encontrada." });
        db.BasketItems.RemoveRange(basket.Items);
        db.Baskets.Remove(basket);
        await db.SaveChangesAsync();
        return Results.NoContent();
    });
    api.MapPost("/baskets/{id:guid}/duplicate", async (Guid id, MarketDb db) =>
    {
        var source = await db.Baskets.Include(x => x.Items).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (source is null) return Results.NotFound(new { code = "NOT_FOUND", message = "Cesta no encontrada." });

        var clone = new Basket
        {
            Name = $"{source.Name} (Copia)",
            Budget = source.Budget,
            MaxStores = source.MaxStores,
            PreferredStrategy = source.PreferredStrategy,
            Items = source.Items.Select(item => new BasketItem
            {
                Query = item.Query,
                Quantity = item.Quantity,
                ProductVariantId = item.ProductVariantId,
                Dimension = item.Dimension,
                Unit = item.Unit,
                AllowSubstitutesOverride = item.AllowSubstitutesOverride,
                SizeTolerancePercentOverride = item.SizeTolerancePercentOverride,
                PreferredBrand = item.PreferredBrand,
                ExcludedBrand = item.ExcludedBrand,
                Notes = item.Notes
            }).ToList()
        };

        db.Baskets.Add(clone);
        await db.SaveChangesAsync();
        return Results.Created($"/api/v1/baskets/{clone.Id}", clone);
    });
    api.MapPost("/baskets/{id:guid}/items/from-product", async (Guid id, AddProductToBasketRequest input, MarketDb db) =>
    {
        var basket = await db.Baskets.SingleOrDefaultAsync(x => x.Id == id);
        if (basket is null) return Results.NotFound(new { code = "NOT_FOUND", message = "Cesta no encontrada." });
        if (string.IsNullOrWhiteSpace(input.Name))
            return Results.BadRequest(new { code = "INVALID_ITEM", message = "El nombre del producto es requerido." });

        var item = new BasketItem
        {
            BasketId = id,
            Query = input.Name.Trim(),
            Quantity = input.Quantity > 0 ? input.Quantity : 1,
            PreferredBrand = string.IsNullOrWhiteSpace(input.Brand) ? null : input.Brand.Trim(),
            Dimension = Dimension.Count,
            Unit = "ud"
        };
        db.BasketItems.Add(item);
        await db.SaveChangesAsync();
        return Results.Created($"/api/v1/baskets/{id}/items/{item.Id}", item);
    });
    api.MapPost("/baskets/{id:guid}/optimize", async (
        Guid id,
        OptimizeBasketRequest? request,
        MarketDb db,
        IGeocodingProvider geocoder,
        StoreRouteOptimizer routeOptimizer,
        IShoppingContextResolver contextResolver,
        IOfferContextPolicy contextPolicy,
        CancellationToken ct) =>
    {
        var basket = await db.Baskets.Include(x => x.Items).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (basket is null) return Results.NotFound(new { code = "NOT_FOUND", message = "Cesta no encontrada." });
        if (basket.Items.Count == 0) return Results.BadRequest(new { code = "EMPTY_BASKET", message = "La cesta no tiene productos para optimizar." });
    
        var shoppingContext = await contextResolver.ResolveContextAsync(
            request?.LocationInput,
            request?.OriginLatitude,
            request?.OriginLongitude,
            ct
        );
        var offers = await CatalogOfferLoader.LoadOffersAsync(db, shoppingContext, contextPolicy, ct);
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new AppSettings();
        var overrides = await db.MatchOverrides.AsNoTracking().ToListAsync(ct);
    
        var (locCtx, storeMap) = await ResolveLocationContextAsync(
            request?.OriginLatitude,
            request?.OriginLongitude,
            request?.LocationInput,
            request?.ReturnToOrigin ?? true,
            request?.TransportMode ?? TransportMode.Driving,
            request?.CostPerKm ?? 2500m,
            request?.PriceWeight ?? 1.0m,
            request?.DistanceWeight ?? 1.0m,
            request?.ConvenienceWeight ?? 1.0m,
            db,
            geocoder,
            ct
        );
    
        var options = new OptimizerExecutionOptions(
            Strategy: request?.Strategy,
            MaxStores: request?.MaxStores,
            Budget: request?.Budget,
            AllowSubstitutes: request?.AllowSubstitutes,
            SizeTolerancePercent: request?.SizeTolerancePercent,
            MaxOverbuyPercent: request?.MaxOverbuyPercent,
            StorePenalty: request?.StorePenalty ?? 5000m,
            LocationContext: locCtx,
            StoreMap: storeMap,
            ShoppingContext: shoppingContext
        );
    
        var solution = BasketOptimizer.Optimize(basket, offers, settings, overrides, options);
    
        if (solution.RoutePlan != null && solution.RoutePlan.OrderedStores.Count > 0 && locCtx != null)
        {
            var upgradedRoute = await routeOptimizer.OptimizeRouteAsync(
                locCtx.OriginLatitude,
                locCtx.OriginLongitude,
                solution.RoutePlan.OrderedStores,
                returnToOrigin: locCtx.ReturnToOrigin,
                transportMode: locCtx.TransportMode,
                costPerKm: locCtx.CostPerKm,
                cancellationToken: ct
            );
            solution = solution with { RoutePlan = upgradedRoute };
        }
    
        return Results.Ok(solution);
    });
    api.MapPost("/optimizer/simulate", async (
        SimulateOptimizationRequest request,
        MarketDb db,
        IGeocodingProvider geocoder,
        StoreRouteOptimizer routeOptimizer,
        IShoppingContextResolver contextResolver,
        IOfferContextPolicy contextPolicy,
        CancellationToken ct) =>
    {
        if (request.Items == null || request.Items.Count == 0)
            return Results.BadRequest(new { code = "EMPTY_ITEMS", message = "Indique al menos un producto para simular la optimización." });
    
        var transientBasket = new Basket
        {
            Name = "Simulación",
            Budget = request.Budget,
            MaxStores = request.MaxStores,
            PreferredStrategy = request.Strategy ?? OptimizationStrategy.MaxSavings,
            Items = request.Items.Select(i => new BasketItem
            {
                Query = i.Query.Trim(),
                Quantity = i.Quantity > 0 ? i.Quantity : 1,
                Dimension = i.Dimension,
                Unit = i.Unit?.Trim(),
                AllowSubstitutesOverride = i.AllowSubstitutes,
                SizeTolerancePercentOverride = i.SizeTolerancePercent,
                PreferredBrand = i.PreferredBrand?.Trim(),
                ExcludedBrand = i.ExcludedBrand?.Trim(),
                Notes = i.Notes
            }).ToList()
        };
    
        var shoppingContext = await contextResolver.ResolveContextAsync(
            request.LocationInput,
            request.OriginLatitude,
            request.OriginLongitude,
            ct
        );
        var offers = await CatalogOfferLoader.LoadOffersAsync(db, shoppingContext, contextPolicy, ct);
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new AppSettings();
        var overrides = await db.MatchOverrides.AsNoTracking().ToListAsync(ct);
    
        var (locCtx, storeMap) = await ResolveLocationContextAsync(
            request.OriginLatitude,
            request.OriginLongitude,
            request.LocationInput,
            request.ReturnToOrigin,
            request.TransportMode,
            request.CostPerKm,
            request.PriceWeight,
            request.DistanceWeight,
            request.ConvenienceWeight,
            db,
            geocoder,
            ct
        );
    
        var options = new OptimizerExecutionOptions(
            Strategy: request.Strategy,
            MaxStores: request.MaxStores,
            Budget: request.Budget,
            AllowSubstitutes: request.AllowSubstitutes,
            SizeTolerancePercent: request.SizeTolerancePercent,
            MaxOverbuyPercent: request.MaxOverbuyPercent,
            StorePenalty: request.StorePenalty ?? 5000m,
            LocationContext: locCtx,
            StoreMap: storeMap,
            ShoppingContext: shoppingContext
        );
    
        var solution = BasketOptimizer.Optimize(transientBasket, offers, settings, overrides, options);
    
        if (solution.RoutePlan != null && solution.RoutePlan.OrderedStores.Count > 0 && locCtx != null)
        {
            var upgradedRoute = await routeOptimizer.OptimizeRouteAsync(
                locCtx.OriginLatitude,
                locCtx.OriginLongitude,
                solution.RoutePlan.OrderedStores,
                returnToOrigin: locCtx.ReturnToOrigin,
                transportMode: locCtx.TransportMode,
                costPerKm: locCtx.CostPerKm,
                cancellationToken: ct
            );
            solution = solution with { RoutePlan = upgradedRoute };
        }
    
        return Results.Ok(solution);
    });

        return api;
    }

    static async Task<(OptimizerLocationContext? Context, Dictionary<string, Store> StoreMap)> ResolveLocationContextAsync(
        decimal? reqLat,
        decimal? reqLon,
        string? locationInput,
        bool returnToOrigin,
        TransportMode transportMode,
        decimal costPerKm,
        decimal priceWeight,
        decimal distanceWeight,
        decimal convenienceWeight,
        MarketDb db,
        IGeocodingProvider geocoder,
        CancellationToken ct)
    {
        decimal? originLat = reqLat;
        decimal? originLon = reqLon;
    
        if ((!originLat.HasValue || !originLon.HasValue) && !string.IsNullOrWhiteSpace(locationInput))
        {
            var profiles = await db.LocationProfiles.AsNoTracking().ToListAsync(ct);
            var parseResult = LocationInputParser.Parse(locationInput, profiles);
            if (parseResult.Latitude.HasValue && parseResult.Longitude.HasValue)
            {
                originLat = parseResult.Latitude.Value;
                originLon = parseResult.Longitude.Value;
            }
            else if (!string.IsNullOrWhiteSpace(parseResult.NormalizedAddress))
            {
                var geo = geocoder is GoogleGeocodingProvider google
                    ? await google.GeocodeDetailedAsync(parseResult.NormalizedAddress, ct)
                    : null;
                if (geo is { ProviderStatus: "LIVE_GOOGLE" or "CACHED_VERIFIED" })
                {
                    originLat = geo.Latitude;
                    originLon = geo.Longitude;
                }
            }
        }
    
        if (!originLat.HasValue || !originLon.HasValue)
        {
            return (null, new Dictionary<string, Store>());
        }
    
        var locationContext = new OptimizerLocationContext(
            OriginLatitude: originLat.Value,
            OriginLongitude: originLon.Value,
            ReturnToOrigin: returnToOrigin,
            TransportMode: transportMode,
            CostPerKm: costPerKm,
            PriceWeight: priceWeight,
            DistanceWeight: distanceWeight,
            ConvenienceWeight: convenienceWeight
        );
    
        var allStores = await db.Stores.AsNoTracking().ToListAsync(ct);
        var retailers = await db.Retailers.AsNoTracking().ToDictionaryAsync(r => r.Id, r => r, ct);
        var storeMap = new Dictionary<string, Store>(StringComparer.OrdinalIgnoreCase);
    
        foreach (var st in allStores)
        {
            if (retailers.TryGetValue(st.RetailerId, out var ret))
            {
                st.Retailer = ret;
            }
            storeMap[st.Id.ToString()] = st;
            if (!string.IsNullOrWhiteSpace(st.ExternalId))
            {
                storeMap[st.ExternalId] = st;
            }
            if (!string.IsNullOrWhiteSpace(st.Name))
            {
                storeMap[st.Name] = st;
            }
        }
    
        return (locationContext, storeMap);
    }
}

