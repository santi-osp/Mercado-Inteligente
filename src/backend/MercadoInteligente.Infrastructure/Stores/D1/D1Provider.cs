using System.Globalization;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure.Stores.D1;

// Not registered in production until fulfillment context and persistence are validated.
public sealed class D1Provider(HttpClient client, D1FulfillmentSession? session = null) : IStoreProvider
{
    public string Code => "D1";
    public ProviderCapabilities Capabilities => DeclaredCapabilities;
    public static ProviderCapabilities DeclaredCapabilities { get; } = new(CapabilityState.Partial, CapabilityState.Partial,
        CapabilityState.Partial, CapabilityState.NotSupported, CapabilityState.Partial, CapabilityState.NotSupported);

    private async Task<PriceScope> ScopeAsync(ProviderContext context, CancellationToken ct)
    {
        if (context.Key == "online-generic") return PriceScope.OnlineGeneric;
        if (session?.VerifiedRegionId != context.Key) throw new InvalidOperationException("D1 region is not verified in this session.");
        await session.ConfirmStillValidAsync(ct);
        return PriceScope.FulfillmentRegion;
    }

    private async Task<JsonDocument> FetchAsync(string path, ProviderContext context, CancellationToken ct)
    {
        if (context.Key != "online-generic")
            return await (session ?? throw new InvalidOperationException("Missing D1 fulfillment session.")).GetJsonAsync(path, ct);
        using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 2 * 1024 * 1024)
            throw new JsonException("D1 catalog page exceeded the size limit.");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memory = new MemoryStream();
        var buffer = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (memory.Length + read > 2 * 1024 * 1024)
                throw new JsonException("D1 catalog page exceeded the size limit.");
            memory.Write(buffer, 0, read);
        }
        return JsonDocument.Parse(memory.ToArray());
    }

    public async Task<IReadOnlyList<ProviderProduct>> SearchAsync(string query, ProviderContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(query)) return [];
        if (query.Length > 200) throw new ArgumentException("Query too long", nameof(query));
        await ScopeAsync(context, cancellationToken);
        var products = new Dictionary<string, ProviderProduct>(StringComparer.Ordinal);
        // Bounded search: this is not complete catalog enumeration.
        for (var page = 0; page < 3; page++)
        {
            var from = page * 20;
            using var document = await FetchAsync($"api/catalog_system/pub/products/search?ft={Uri.EscapeDataString(query.Trim())}&_from={from}&_to={from + 19}", context, cancellationToken);
            await RevalidateContextAsync(context, cancellationToken);
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() > 20)
                throw new JsonException("D1 page exceeded the requested limit.");
            var mapped = D1Mapper.Products(document.RootElement);
            foreach (var product in mapped)
            {
                if (products.TryGetValue(product.ExternalId, out var previous))
                {
                    var merged = previous.Skus.ToDictionary(x => x.ExternalId, StringComparer.Ordinal);
                    foreach (var sku in product.Skus)
                    {
                        if (merged.TryGetValue(sku.ExternalId, out var prior) && prior != sku)
                            throw new JsonException("D1 SKU identity conflicts across search pages.");
                        merged.TryAdd(sku.ExternalId, sku);
                    }
                    products[product.ExternalId] = previous with { Skus = merged.Values.ToArray() };
                }
                else products.Add(product.ExternalId, product);
            }
            if (document.RootElement.GetArrayLength() < 20) break;
        }
        return products.Values.ToArray();
    }

    public async Task<ProviderProduct?> GetProductAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateProductId(externalProductId);
        await ScopeAsync(context, cancellationToken);
        using var catalog = await FetchProductAsync(externalProductId, context, cancellationToken);
        await RevalidateContextAsync(context, cancellationToken);
        return ValidateDetail(catalog.RootElement, externalProductId);
    }

    public async Task<IReadOnlyList<ProviderOffer>> OffersAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ValidateProductId(externalProductId);
        var scope = await ScopeAsync(context, cancellationToken);
        using var catalog = await FetchProductAsync(externalProductId, context, cancellationToken);
        await RevalidateContextAsync(context, cancellationToken);
        ValidateDetail(catalog.RootElement, externalProductId);
        return D1Mapper.Offers(catalog.RootElement, externalProductId, context)
            .Select(offer => offer with { Scope = scope }).ToArray();
    }

    private static ProviderProduct ValidateDetail(JsonElement catalog, string productId)
    {
        if (catalog.ValueKind != JsonValueKind.Array || catalog.GetArrayLength() is 0 or > 20)
            throw new JsonException("D1 product detail is missing or exceeds the limit.");
        var products = D1Mapper.Products(catalog);
        if (catalog.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object ||
            !item.TryGetProperty("productId", out var id) || id.ValueKind != JsonValueKind.String || id.GetString() != productId) ||
            products.Count != 1 || products[0].ExternalId != productId)
            throw new JsonException("D1 detail did not match the requested product.");
        return products[0];
    }

    private async Task RevalidateContextAsync(ProviderContext context, CancellationToken ct)
    {
        if (context.Key == "online-generic") return;
        await (session ?? throw new InvalidOperationException("Missing D1 fulfillment session.")).ConfirmStillValidAsync(ct);
        if (session.VerifiedRegionId != context.Key)
            throw new InvalidOperationException("D1 fulfillment region changed during the request.");
    }

    private static long ValidateProductId(string id)
    {
        if (string.IsNullOrEmpty(id) || !long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0)
            throw new ArgumentException("Invalid D1 product ID", nameof(id));
        return number;
    }

    private Task<JsonDocument> FetchProductAsync(string id, ProviderContext context, CancellationToken ct) =>
        FetchAsync($"api/catalog_system/pub/products/search?fq=productId:{ValidateProductId(id)}&_from=0&_to=19", context, ct);

    public Task<IReadOnlyList<ProviderStore>> GetStoresAsync(ProviderContext context, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProviderStore>>([]);
    public Task<IReadOnlyList<ProviderProduct>> EnumerateCatalogAsync(ProviderContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Complete D1 catalog enumeration is not validated.");
}
