using System.Net;
using System.Text.Json;
using MercadoInteligente.Application;

namespace MercadoInteligente.Infrastructure.Stores.D1;

// Explicit, bounded diagnostic only. Never invoked by ordinary health reads or search.
public sealed class D1HealthProbe(HttpClient client)
{
    public async Task<D1ProbeResult> CheckAsync(CancellationToken ct)
    {
        var checkedAt = DateTime.UtcNow;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response = await client.GetAsync("api/catalog_system/pub/products/search?ft=arroz&_from=0&_to=0",
                HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                return new D1ProbeResult(checkedAt, "DECLINED", "NOT_CHECKED");
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.PartialContent)
                return new D1ProbeResult(checkedAt, "HTTP_ERROR", "NOT_CHECKED");
            if (response.Content.Headers.ContentLength > 2 * 1024 * 1024)
                return new D1ProbeResult(checkedAt, "SCHEMA_DRIFT", "NOT_CHECKED");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var memory = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, deadline.Token)) != 0)
            {
                if (memory.Length + read > 2 * 1024 * 1024)
                    return new D1ProbeResult(checkedAt, "SCHEMA_DRIFT", "NOT_CHECKED");
                memory.Write(buffer, 0, read);
            }
            using var document = JsonDocument.Parse(memory.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() != 1)
                return new D1ProbeResult(checkedAt, "SCHEMA_DRIFT", "NOT_CHECKED");
            var product = AssertSingleProduct(document.RootElement);
            var offers = D1Mapper.Offers(document.RootElement, product.ExternalId, new ProviderContext("online-generic"));
            return new D1ProbeResult(checkedAt, "VALID", offers.Any(x => x.Price > 0) ? "VALID_GENERIC_ONLINE" : "NO_VALID_PRICE");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return new D1ProbeResult(checkedAt, "TIMEOUT", "NOT_CHECKED"); }
        catch (HttpRequestException)
        { return new D1ProbeResult(checkedAt, "HTTP_ERROR", "NOT_CHECKED"); }
        catch (JsonException)
        { return new D1ProbeResult(checkedAt, "SCHEMA_DRIFT", "NOT_CHECKED"); }
        catch (InvalidOperationException)
        { return new D1ProbeResult(checkedAt, "SCHEMA_DRIFT", "NOT_CHECKED"); }
    }

    private static ProviderProduct AssertSingleProduct(JsonElement root)
    {
        var product = D1Mapper.Products(root).Single();
        if (product.Skus.Count == 0) throw new JsonException("D1 probe has no SKU.");
        return product;
    }
}

public sealed record D1ProbeResult(DateTime CheckedAt, string Search, string GenericOnlinePrice);
