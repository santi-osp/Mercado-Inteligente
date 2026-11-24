using System.Net;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure.Stores.Exito;

// Explicit, bounded diagnostic only. Never invoked by ordinary health reads or search.
public sealed class ExitoHealthProbe(HttpClient client)
{
    public async Task<ExitoProbeResult> CheckAsync(CancellationToken ct)
    {
        var checkedAt = DateTime.UtcNow;
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            var channel = JsonSerializer.Serialize(new { salesChannel = "1" });
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

            using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                return new ExitoProbeResult(checkedAt, "DECLINED", "NOT_CHECKED");
            if (!response.IsSuccessStatusCode)
                return new ExitoProbeResult(checkedAt, "HTTP_ERROR", "NOT_CHECKED");

            if (response.Content.Headers.ContentLength > 2 * 1024 * 1024)
                return new ExitoProbeResult(checkedAt, "SCHEMA_DRIFT", "NOT_CHECKED");

            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var memory = new MemoryStream();
            var buffer = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(buffer, deadline.Token)) != 0)
            {
                if (memory.Length + read > 2 * 1024 * 1024)
                    return new ExitoProbeResult(checkedAt, "SCHEMA_DRIFT", "NOT_CHECKED");
                memory.Write(buffer, 0, read);
            }

            using var document = JsonDocument.Parse(memory.ToArray());
            var nodes = ExitoMapper.Nodes(document.RootElement);
            if (nodes.Count == 0)
                return new ExitoProbeResult(checkedAt, "SCHEMA_DRIFT", "NOT_CHECKED");

            var products = ExitoMapper.Products(nodes);
            if (products.Count == 0)
                return new ExitoProbeResult(checkedAt, "SCHEMA_DRIFT", "NOT_CHECKED");

            var product = products[0];
            var offers = ExitoMapper.Offers(nodes, product.ExternalId, "online-generic", PriceScope.OnlineGeneric);

            var validPrice = offers.Any(x => x.Price > 0 && x.SellerKind == SellerKind.RetailerDirect);
            return new ExitoProbeResult(checkedAt, "VALID", validPrice ? "VALID_GENERIC_ONLINE" : "NO_VALID_PRICE");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ExitoProbeResult(checkedAt, "TIMEOUT", "NOT_CHECKED");
        }
        catch (HttpRequestException)
        {
            return new ExitoProbeResult(checkedAt, "HTTP_ERROR", "NOT_CHECKED");
        }
        catch (JsonException)
        {
            return new ExitoProbeResult(checkedAt, "SCHEMA_DRIFT", "NOT_CHECKED");
        }
        catch (InvalidOperationException)
        {
            return new ExitoProbeResult(checkedAt, "SCHEMA_DRIFT", "NOT_CHECKED");
        }
    }
}

public sealed record ExitoProbeResult(DateTime CheckedAt, string Search, string GenericOnlinePrice);
