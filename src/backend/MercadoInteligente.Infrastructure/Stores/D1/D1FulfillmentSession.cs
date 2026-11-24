using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MercadoInteligente.Infrastructure.Stores.D1;

public sealed record D1DeliveryAddress(string PostalCode, string City, string State, string Street,
    string Number, string Neighborhood, decimal Longitude, decimal Latitude);

// Each instance owns one anonymous cookie jar. Never persist its cookies or order form ID.
public sealed class D1FulfillmentSession : IDisposable
{
    private readonly HttpClient client;
    private readonly HttpMessageHandler? ownedHandler;
    private string? regionId;
    private D1DeliveryAddress? establishedAddress;
    public string? VerifiedRegionId => regionId;
    public async Task ConfirmStillValidAsync(CancellationToken ct)
    {
        var expected = regionId ?? throw new InvalidOperationException("A region has not been established.");
        try
        {
            using var segment = await GetJsonAsync("api/segments", ct);
            var root = segment.RootElement;
            var current = root.GetProperty("regionId").GetString();
            var channel = root.GetProperty("channel").GetString();
            var facets = root.TryGetProperty("facets", out var facetValue) && facetValue.ValueKind == JsonValueKind.String
                ? facetValue.GetString() : null;
            if (current != expected || channel != "1" || establishedAddress is null || !CoordinatesMatch(facets, establishedAddress))
                throw new InvalidDataException("D1 fulfillment region changed or expired.");
        }
        catch
        {
            regionId = null;
            establishedAddress = null;
            throw;
        }
    }

    public D1FulfillmentSession()
    {
        var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true,
            AllowAutoRedirect = false };
        ownedHandler = handler;
        client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/"), Timeout = TimeSpan.FromSeconds(15) };
    }

    // Inject a transport only for deterministic contract tests; production uses the isolated jar above.
    public D1FulfillmentSession(HttpClient client) => this.client = client;

    public async Task<string> EstablishAsync(D1DeliveryAddress address, CancellationToken ct)
    {
        regionId = null;
        establishedAddress = null;
        if (string.IsNullOrWhiteSpace(address.PostalCode) || string.IsNullOrWhiteSpace(address.City) ||
            string.IsNullOrWhiteSpace(address.Street) || string.IsNullOrWhiteSpace(address.Number) ||
            string.IsNullOrWhiteSpace(address.Neighborhood) ||
            address.Longitude is < -180 or > 180 || address.Latitude is < -90 or > 90)
            throw new ArgumentException("A valid delivery address is required.", nameof(address));

        try
        {
            // The archived diagnostic observed HTTP 400 here in all three successful context runs.
            // Never accept arbitrary failures: only this specific optional initialization may return 400.
            using (var initialization = await client.PostAsJsonAsync("api/sessions?items=*", new { }, ct))
                if (initialization.StatusCode != HttpStatusCode.BadRequest) initialization.EnsureSuccessStatusCode();

            using var order = await GetJsonAsync("api/checkout/pub/orderForm", ct);
            var orderId = order.RootElement.GetProperty("orderFormId").GetString();
            if (string.IsNullOrWhiteSpace(orderId) || !Guid.TryParse(orderId, out _))
                throw new JsonException("Anonymous D1 order form ID is invalid.");

            var delivery = new
            {
                addressType = "residential", receiverName = "", isDisposable = true,
                postalCode = address.PostalCode, city = address.City, state = address.State,
                country = "COL", street = address.Street, number = address.Number,
                neighborhood = address.Neighborhood, complement = "", reference = "",
                geoCoordinates = new[] { address.Longitude, address.Latitude }
            };
            var payload = new { address = delivery, sc = "1" };
            using (var simulation = await client.PostAsJsonAsync($"_v/private/segment/simulation/{orderId}", payload, ct))
                simulation.EnsureSuccessStatusCode();
            using (var confirmation = await client.PostAsJsonAsync($"_v/private/segment/{orderId}",
                new { email = (string?)null, sc = "1", address = delivery, shippingMethod = "programado" }, ct))
                confirmation.EnsureSuccessStatusCode();

            using var segment = await GetJsonAsync("api/segments", ct);
            var root = segment.RootElement;
            var channel = root.GetProperty("channel").GetString();
            var region = root.GetProperty("regionId").GetString();
            var facets = root.TryGetProperty("facets", out var facetValue) && facetValue.ValueKind == JsonValueKind.String
                ? facetValue.GetString() : null;
            if (channel != "1" || string.IsNullOrWhiteSpace(region) || !CoordinatesMatch(facets, address))
                throw new InvalidDataException("D1 did not confirm a fulfillment region.");
            regionId = region;
            establishedAddress = address;
            return region;
        }
        catch
        {
            regionId = null;
            establishedAddress = null;
            throw;
        }
    }

    private static bool CoordinatesMatch(string? facets, D1DeliveryAddress address)
    {
        var expected = $"{address.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{address.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
        return facets?.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Any(facet => facet == $"coordinates={expected}") == true;
    }

    internal async Task<(JsonDocument Document, byte[] Payload)> GetCatalogAsync(string path, CancellationToken ct)
    {
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
        var payload = memory.ToArray();
        return (JsonDocument.Parse(payload), payload);
    }

    internal async Task<JsonDocument> GetJsonAsync(string path, CancellationToken ct)
    {
        var (document, _) = await GetCatalogAsync(path, ct);
        return document;
    }

    public void Dispose()
    {
        regionId = null;
        establishedAddress = null;
        if (ownedHandler is not null) { client.Dispose(); ownedHandler.Dispose(); }
    }
}
