using System.Globalization;
using System.Text.Json;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure.Stores.D1;

// A region and its catalog payload must come from the same anonymous cookie session.
public sealed class D1RegionalCapture(D1FulfillmentSession session, D1CatalogIngest ingest)
{
    public async Task<int> CaptureSearchAsync(D1DeliveryAddress address, string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
            throw new ArgumentException("Invalid D1 search query.", nameof(query));
        var regionId = await session.EstablishAsync(address, ct);
        await session.ConfirmStillValidAsync(ct);
        var (document, payload) = await session.GetCatalogAsync(
            $"api/catalog_system/pub/products/search?ft={Uri.EscapeDataString(query.Trim())}&_from=0&_to=19", ct);
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > 20)
                throw new JsonException("D1 regional search exceeded the requested limit.");
            D1Mapper.Products(document.RootElement);
        }
        await session.ConfirmStillValidAsync(ct);
        if (session.VerifiedRegionId != regionId)
            throw new InvalidDataException("D1 fulfillment region changed before capture.");
        return await ingest.SaveRegionalAsync(payload, regionId, DateTime.UtcNow, ct);
    }

    public async Task<int> CaptureProductAsync(D1DeliveryAddress address, string productId, CancellationToken ct)
    {
        if (!long.TryParse(productId, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id <= 0)
            throw new ArgumentException("Invalid D1 product ID", nameof(productId));
        var regionId = await session.EstablishAsync(address, ct);
        await session.ConfirmStillValidAsync(ct);
        var (document, payload) = await session.GetCatalogAsync(
            $"api/catalog_system/pub/products/search?fq=productId:{id}&_from=0&_to=19", ct);
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() is 0 or > 20)
                throw new JsonException("D1 product detail is missing or exceeds the limit.");
            var products = D1Mapper.Products(document.RootElement);
            if (document.RootElement.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("productId", out var candidate) || candidate.ValueKind != JsonValueKind.String || candidate.GetString() != productId) ||
                products.Count != 1 || products[0].ExternalId != productId)
                throw new JsonException("D1 detail did not match the requested product.");
        }
        await session.ConfirmStillValidAsync(ct);
        if (session.VerifiedRegionId != regionId)
            throw new InvalidDataException("D1 fulfillment region changed before capture.");
        var checkedAt = DateTime.UtcNow;
        return await ingest.SaveRegionalAsync(payload, regionId, checkedAt, ct);
    }
}
