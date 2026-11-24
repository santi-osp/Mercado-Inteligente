using System.Net;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using MercadoInteligente.Infrastructure.Stores.Exito;
using MercadoInteligente.Infrastructure.Stores.Ara;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Api;

public static class ProviderEndpoints
{
    public static RouteGroupBuilder MapProviderEndpoints(this RouteGroupBuilder api)
    {
    api.MapPost("/providers/d1/refresh", async (string? q, D1SearchSync sync, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 200)
            return Results.BadRequest(new { code = "INVALID_QUERY", message = "Indique entre 1 y 200 caracteres." });
        try
        {
            var result = await sync.RefreshAsync(q, ct);
            return result.Status switch
            {
                "BUSY" or "COOLDOWN" => Results.Conflict(new { code = result.Status, message = "Espere antes de volver a actualizar D1." }),
                _ => Results.Ok(result)
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return Results.Json(new { code = "PROVIDER_TIMEOUT", message = "D1 no respondió a tiempo. Se conservaron los datos anteriores." }, statusCode: 503); }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
        { return Results.Json(new { code = "RATE_LIMITED", message = "D1 limitó la consulta. Se conservaron los datos anteriores." }, statusCode: 503); }
        catch (HttpRequestException)
        { return Results.Json(new { code = "PROVIDER_UNAVAILABLE", message = "D1 no respondió. Se conservaron los datos anteriores." }, statusCode: 503); }
        catch (System.Text.Json.JsonException)
        { return Results.Json(new { code = "SCHEMA_DRIFT", message = "La respuesta de D1 cambió. Se conservaron los datos anteriores." }, statusCode: 503); }
    });
    api.MapGet("/providers/d1/offers", async (string? q, MarketDb db) =>
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 200) return Results.BadRequest(new { code = "INVALID_QUERY" });
        var rows = await (from offer in db.Offers.AsNoTracking()
                          join sku in db.ExternalSkus on offer.ExternalSkuId equals sku.Id
                          join product in db.ExternalProducts on sku.ExternalProductId equals product.Id
                          join retailer in db.Retailers on product.RetailerId equals retailer.Id
                          join seller in db.Sellers on offer.SellerId equals seller.Id
                          where retailer.Code == "D1" && offer.Scope == PriceScope.OnlineGeneric && offer.Context == "online-generic" && (product.Name.Contains(q) || sku.Name.Contains(q))
                          orderby offer.CheckedAt descending
                          select new { product.Id, ProductName = product.Name, SkuName = sku.Name, sku.Ean,
                              offer.Price, offer.ListPrice, offer.Scope, offer.Availability, offer.Context, offer.CheckedAt,
                              SellerName = seller.Name, SellerKind = seller.Kind })
            .Take(50).ToListAsync();
        return Results.Ok(rows);
    });
    api.MapPost("/sync/basket", async (Guid? basketId, string? exitoContext, string? locationInput, ISyncEngine syncEngine, CancellationToken ct) =>
        Results.Ok(await syncEngine.SyncBasketAsync(basketId, exitoContext, ct, locationInput)));
    api.MapPost("/sync/maintenance", async (int? maxItems, ISyncEngine syncEngine, CancellationToken ct) =>
        Results.Ok(await syncEngine.SyncMaintenanceStaleAsync(maxItems ?? 10, ct)));
    api.MapGet("/sync/runs", async (int? limit, ISyncEngine syncEngine, CancellationToken ct) =>
        Results.Ok(await syncEngine.GetRecentRunsAsync(limit ?? 20, ct)));
    // This is local evidence and declared capability, not a network probe or a live D1 health certificate.
    // Explicit diagnostic: one public generic-online search page, no cookies, no SQLite writes.
    api.MapPost("/providers/d1/probe", async (D1HealthProbe probe, CancellationToken ct) =>
        Results.Ok(await probe.CheckAsync(ct)));
    // Separate opt-in regional diagnostic; the ordinary GET health endpoint remains local and historical.
    api.MapPost("/providers/d1/probe/regional", async (D1RegionalCapabilityProbe probe, CancellationToken ct) =>
        Results.Ok(await probe.CheckAsync(ct)));
    api.MapGet("/providers/d1/health", async (MarketDb db) =>
    {
        var last = await db.SyncRuns.AsNoTracking().Where(x => x.Provider == "D1")
            .OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync();
        return Results.Ok(new
        {
            code = "D1", status = "PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH",
            capabilities = D1Provider.DeclaredCapabilities,
            lastRun = last is null ? null : new { last.State, last.StartedAt, last.FinishedAt, last.ErrorCode, last.ItemsReceived, last.OffersProcessed, last.OffersChanged, last.ProductsCreated, last.ProductsUpdated, last.SkusCreated, last.SkusUpdated, last.RawNew, last.RawDeduplicated }
        });
    });
    api.MapPost("/providers/exito/refresh", async (string? q, ExitoSearchSync sync, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 200)
            return Results.BadRequest(new { code = "INVALID_QUERY", message = "Indique entre 1 y 200 caracteres." });
        try
        {
            var result = await sync.RefreshAsync(q, null, ct);
            return result.Status switch
            {
                "BUSY" or "COOLDOWN" => Results.Conflict(new { code = result.Status, message = "Espere antes de volver a actualizar Éxito." }),
                _ => Results.Ok(result)
            };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return Results.Json(new { code = "PROVIDER_TIMEOUT", message = "Éxito no respondió a tiempo. Se conservaron los datos anteriores." }, statusCode: 503); }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.TooManyRequests)
        { return Results.Json(new { code = "RATE_LIMITED", message = "Éxito limitó la consulta. Se conservaron los datos anteriores." }, statusCode: 503); }
        catch (HttpRequestException)
        { return Results.Json(new { code = "PROVIDER_UNAVAILABLE", message = "Éxito no respondió. Se conservaron los datos anteriores." }, statusCode: 503); }
        catch (System.Text.Json.JsonException)
        { return Results.Json(new { code = "SCHEMA_DRIFT", message = "La respuesta de Éxito cambió. Se conservaron los datos anteriores." }, statusCode: 503); }
    });
    api.MapGet("/providers/exito/offers", async (string? q, string? context, MarketDb db) =>
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 200) return Results.BadRequest(new { code = "INVALID_QUERY" });
        var effectiveContext = string.IsNullOrWhiteSpace(context) ? "online-generic" : context.Trim();
        var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync();
        var includeMarketplace = settings?.IncludeMarketplace ?? false;
        var rows = await (from offer in db.Offers.AsNoTracking()
                          join sku in db.ExternalSkus on offer.ExternalSkuId equals sku.Id
                          join product in db.ExternalProducts on sku.ExternalProductId equals product.Id
                          join retailer in db.Retailers on product.RetailerId equals retailer.Id
                          join seller in db.Sellers on offer.SellerId equals seller.Id
                          where retailer.Code == "EXITO" && offer.Context == effectiveContext &&
                                (includeMarketplace || seller.Kind == SellerKind.RetailerDirect) &&
                                (product.Name.Contains(q) || sku.Name.Contains(q))
                          orderby offer.CheckedAt descending
                          select new { product.Id, ProductName = product.Name, SkuName = sku.Name, sku.Ean,
                              offer.Price, offer.ListPrice, offer.Scope, offer.Availability, offer.Context, offer.CheckedAt,
                              SellerName = seller.Name, SellerKind = seller.Kind })
            .Take(50).ToListAsync();
        return Results.Ok(rows);
    });
    api.MapPost("/providers/exito/probe", async (ExitoHealthProbe probe, CancellationToken ct) =>
        Results.Ok(await probe.CheckAsync(ct)));
    api.MapPost("/providers/exito/probe/regional", async (ExitoRegionalCapabilityProbe probe, CancellationToken ct) =>
        Results.Ok(await probe.CheckAsync(ct: ct)));
    api.MapGet("/providers/exito/health", async (MarketDb db, ExitoProvider provider) =>
    {
        var last = await db.SyncRuns.AsNoTracking().Where(x => x.Provider == "EXITO")
            .OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync();
        return Results.Ok(new
        {
            code = "EXITO", status = "PARTIAL_FASTSTORE_MANUAL_REFRESH",
            capabilities = provider.Capabilities,
            lastRun = last is null ? null : new { last.State, last.StartedAt, last.FinishedAt, last.ErrorCode, last.ItemsReceived, last.OffersProcessed, last.OffersChanged, last.ProductsCreated, last.ProductsUpdated, last.SkusCreated, last.SkusUpdated, last.RawNew, last.RawDeduplicated }
        });
    });
    api.MapGet("/providers", (IStoreProviderRegistry registry) =>
        Results.Ok(registry.GetMetadata()));
    
    api.MapGet("/providers/{code}", (string code, IStoreProviderRegistry registry) =>
    {
        var metadata = registry.GetMetadata().FirstOrDefault(m => m.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
        return metadata is not null ? Results.Ok(metadata) : Results.NotFound(new { error = $"Proveedor '{code}' no encontrado." });
    });
    
    // Tiendas Ara: Cobertura parcial honesta (Folletos públicos y entradas manuales)
    api.MapGet("/stores/ara/health", async (AraHealthProbe probe, CancellationToken ct) =>
        Results.Ok(await probe.CheckHealthAsync(ct)));
    
    api.MapPost("/stores/ara/brochure", async (HttpRequest request, AraBrochureIngest ingest, CancellationToken ct) =>
    {
        using var ms = new MemoryStream();
        await request.Body.CopyToAsync(ms, ct);
        var payload = ms.ToArray();
        if (payload.Length == 0)
            return Results.BadRequest(new { error = "El cuerpo de la solicitud no puede estar vacío." });
    
        try
        {
            var result = await ingest.IngestAsync(payload, ct);
            return Results.Ok(result);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Results.BadRequest(new { error = "JSON inválido o excede el límite permitido.", detail = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return Results.Problem(detail: ex.Message, statusCode: 500);
        }
    });
    
    api.MapGet("/stores/ara/manual", async (AraManualOfferService manualService, CancellationToken ct) =>
        Results.Ok(await manualService.GetManualOffersAsync(ct)));
    
    api.MapPost("/stores/ara/manual", async (CreateAraManualOfferRequest request, AraManualOfferService manualService, CancellationToken ct) =>
    {
        try
        {
            var created = await manualService.AddManualOfferAsync(request, ct);
            return Results.Created($"/api/v1/stores/ara/manual/{created.OfferId}", created);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    });
    
    api.MapDelete("/stores/ara/manual/{id:guid}", async (Guid id, AraManualOfferService manualService, CancellationToken ct) =>
    {
        var deleted = await manualService.DeleteManualOfferAsync(id, ct);
        return deleted ? Results.NoContent() : Results.NotFound(new { error = $"Oferta manual {id} no encontrada." });
    });
    
    // GOAL-05 Matching, Equivalences, Unit Comparisons and Overrides

        return api;
    }
}
