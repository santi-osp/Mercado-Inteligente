using System.Globalization;
using System.Net;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure.Stores.Exito;

// Read-only bounded FastStore adapter. No background polling or implicit marketplace inclusion.
public sealed class ExitoProvider(HttpClient client) : IStoreProvider
{
    public string Code => "EXITO";
    public ProviderCapabilities Capabilities { get; } = new(CapabilityState.Partial, CapabilityState.Partial,
        CapabilityState.Partial, CapabilityState.NotSupported, CapabilityState.Partial, CapabilityState.NotSupported);
    public static ProviderContext OnlineGeneric => new("online-generic");

    private readonly Dictionary<string, DateTime> verifiedRegions = new(StringComparer.Ordinal);
    private static readonly TimeSpan RequestDeadline = TimeSpan.FromSeconds(15);

    public async Task<ProviderContext> ResolveRegionAsync(decimal longitude, decimal latitude,
        CancellationToken ct)
    {
        if (longitude is < -180 or > 180 || latitude is < -90 or > 90)
            throw new ArgumentOutOfRangeException(nameof(longitude));
        var coords = $"{longitude.ToString(CultureInfo.InvariantCulture)},{latitude.ToString(CultureInfo.InvariantCulture)}";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(RequestDeadline);
        using var response = await client.GetAsync($"api/sessionByCoords?geoCoordinates={Uri.EscapeDataString(coords)}&salesChannel=1",
            HttpCompletionOption.ResponseHeadersRead, deadline.Token);
        response.EnsureSuccessStatusCode();
        using var document = await ReadBoundedAsync(response, deadline.Token);
        var root = document.RootElement;
        var region = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("regionId", out var value) &&
            value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        if (string.IsNullOrWhiteSpace(region)) throw new JsonException("Exito region could not be resolved.");
        verifiedRegions.Clear();
        verifiedRegions[region] = DateTime.UtcNow.AddMinutes(5);
        return new ProviderContext("fulfillment:" + region);
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

    private async Task<IReadOnlyList<JsonElement>> FetchPageAsync(string term, ProviderContext context, int page, CancellationToken ct)
    {
        var region = context.Key switch
        {
            "online-generic" => null,
            _ when context.Key.StartsWith("fulfillment:", StringComparison.Ordinal) && context.Key.Length > 12 => context.Key[12..],
            _ => throw new ArgumentException("Unverified Exito context.", nameof(context))
        };
        if (region is not null && (!verifiedRegions.TryGetValue(region, out var expiresAt) || expiresAt <= DateTime.UtcNow))
            throw new InvalidOperationException("Exito fulfillment region is not verified in this provider instance.");
        var channel = JsonSerializer.Serialize(new { salesChannel = "1", regionId = region });
        var facets = new[] { new { key = "vendido-por", value = "exito" },
            new { key = "channel", value = channel }, new { key = "locale", value = "es-CO" } };
        var variables = JsonSerializer.Serialize(new { first = 20, after = (page * 20).ToString(CultureInfo.InvariantCulture),
            sort = "score_desc", term, selectedFacets = facets });
        var path = "api/graphql?operationName=SearchQuery&variables=" + Uri.EscapeDataString(variables);
        for (var attempt = 0; ; attempt++)
        {
            using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);
            if (attempt == 0 && response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout or
                HttpStatusCode.ServiceUnavailable or HttpStatusCode.InternalServerError)
            {
                await Task.Delay(150, ct);
                continue;
            }
            response.EnsureSuccessStatusCode();
            using var document = await ReadBoundedAsync(response, ct);
            return ExitoMapper.Nodes(document.RootElement).Select(node => node.Clone()).ToArray();
        }
    }

    private async Task<IReadOnlyList<JsonElement>> SearchNodesAsync(string query, ProviderContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(query)) return [];
        if (query.Length > 200) throw new ArgumentException("Query too long.", nameof(query));
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(RequestDeadline);
        var nodes = new List<JsonElement>();
        for (var page = 0; page < 3; page++)
        {
            var batch = await FetchPageAsync(query.Trim(), context, page, deadline.Token);
            nodes.AddRange(batch);
            if (batch.Count < 20) break;
        }
        return nodes;
    }

    public async Task<IReadOnlyList<ProviderProduct>> SearchAsync(string query, ProviderContext context, CancellationToken cancellationToken) =>
        ExitoMapper.Products(await SearchNodesAsync(query, context, cancellationToken));

    public async Task<ProviderProduct?> GetProductAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
    {
        var products = await SearchAsync(externalProductId, context, cancellationToken);
        return products.SingleOrDefault(product => product.ExternalId == externalProductId);
    }

    public async Task<IReadOnlyList<ProviderOffer>> OffersAsync(string externalProductId, ProviderContext context,
        CancellationToken cancellationToken)
    {
        // A text search is not an exhaustive ID lookup. Never attribute a different product offer.
        var nodes = await SearchNodesAsync(externalProductId, context, cancellationToken);
        return ExitoMapper.Offers(nodes, externalProductId, context.Key,
            context.Key == "online-generic" ? PriceScope.OnlineGeneric : PriceScope.FulfillmentRegion);
    }

    public Task<IReadOnlyList<ProviderStore>> GetStoresAsync(ProviderContext context, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProviderStore>>([]);
    public Task<IReadOnlyList<ProviderProduct>> EnumerateCatalogAsync(ProviderContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Complete Exito catalog enumeration is not verified.");
}
