using System.Globalization;
using System.Net;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure.Stores.Exito;

// Diagnostic only: never persists a region, order form, cookie, address, or catalog payload.
public sealed class ExitoRegionalCapabilityProbe(HttpClient client)
{
    private const decimal DefaultMedellinLon = -75.5932585m;
    private const decimal DefaultMedellinLat = 6.232177m;

    public async Task<ExitoRegionalProbeResult> CheckAsync(decimal longitude = DefaultMedellinLon,
        decimal latitude = DefaultMedellinLat, CancellationToken ct = default)
    {
        var checkedAt = DateTime.UtcNow;
        var context = "NOT_CHECKED";
        var catalog = "NOT_CHECKED";
        var price = "NOT_CHECKED";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            if (longitude is < -180 or > 180 || latitude is < -90 or > 90)
                throw new ArgumentOutOfRangeException(nameof(longitude));

            var coords = $"{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}";
            using var sessionResponse = await client.GetAsync(
                $"api/sessionByCoords?geoCoordinates={Uri.EscapeDataString(coords)}&salesChannel=1",
                HttpCompletionOption.ResponseHeadersRead, deadline.Token);

            if (sessionResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                return new(checkedAt, "DECLINED", "NOT_CHECKED", "NOT_CHECKED", "DECLINED");
            if (!sessionResponse.IsSuccessStatusCode)
                return new(checkedAt, "HTTP_ERROR", "NOT_CHECKED", "NOT_CHECKED", "HTTP_ERROR");

            using var sessionDoc = await ReadBoundedAsync(sessionResponse, deadline.Token);
            var root = sessionDoc.RootElement;
            var region = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("regionId", out var val) &&
                val.ValueKind == JsonValueKind.String ? val.GetString() : null;

            if (string.IsNullOrWhiteSpace(region))
                return new(checkedAt, "INVALID_REGION", "NOT_CHECKED", "NOT_CHECKED", "REGION_NOT_RESOLVED");

            context = "RESOLVED_REGION";

            var channel = JsonSerializer.Serialize(new { salesChannel = "1", regionId = region });
            var facets = new[]
            {
                new { key = "vendido-por", value = "exito" },
                new { key = "channel", value = channel },
                new { key = "locale", value = "es-CO" }
            };
            var variables = JsonSerializer.Serialize(new
            {
                first = 1,
                after = "0",
                sort = "score_desc",
                term = "leche",
                selectedFacets = facets
            });
            var path = "api/graphql?operationName=SearchQuery&variables=" + Uri.EscapeDataString(variables);

            using var catalogResponse = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (catalogResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                return new(checkedAt, context, "DECLINED", "NOT_CHECKED", "DECLINED");
            if (!catalogResponse.IsSuccessStatusCode)
                return new(checkedAt, context, "HTTP_ERROR", "NOT_CHECKED", "HTTP_ERROR");

            using var catalogDoc = await ReadBoundedAsync(catalogResponse, deadline.Token);
            var nodes = ExitoMapper.Nodes(catalogDoc.RootElement);
            if (nodes.Count == 0)
                return new(checkedAt, context, "EMPTY_CATALOG", "NOT_CHECKED", "EMPTY_CATALOG");

            catalog = "RESOLVED_REGIONAL_CATALOG";

            var products = ExitoMapper.Products(nodes);
            if (products.Count == 0)
                return new(checkedAt, context, catalog, "SCHEMA_DRIFT", "NO_VALID_PRODUCT");

            var product = products[0];
            var offers = ExitoMapper.Offers(nodes, product.ExternalId, "fulfillment:" + region, PriceScope.FulfillmentRegion);
            var validPrice = offers.Any(x => x.Price > 0 && x.SellerKind == SellerKind.RetailerDirect);
            price = validPrice ? "VALID_REGIONAL_PRICE" : "NO_VALID_PRICE";

            return new(checkedAt, context, catalog, price, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(checkedAt, context, catalog, price, "TIMEOUT");
        }
        catch (HttpRequestException ex)
        {
            return new(checkedAt, context, catalog, price,
                ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests ? "DECLINED" : "HTTP_ERROR");
        }
        catch (JsonException)
        {
            return new(checkedAt, context, catalog, price, "SCHEMA_DRIFT");
        }
        catch (InvalidOperationException)
        {
            return new(checkedAt, context, catalog, price, "SCHEMA_DRIFT");
        }
    }

    private static async Task<JsonDocument> ReadBoundedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        const int limit = 2 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > limit) throw new JsonException("Exito response too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memory = new MemoryStream();
        var buffer = new byte[16384];
        int size;
        while ((size = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (memory.Length + size > limit) throw new JsonException("Exito response too large.");
            memory.Write(buffer, 0, size);
        }
        return JsonDocument.Parse(memory.ToArray());
    }
}

public sealed record ExitoRegionalProbeResult(DateTime CheckedAt, string RegionContext, string RegionalCatalog,
    string RegionalOnlinePrice, string? ErrorCode);
