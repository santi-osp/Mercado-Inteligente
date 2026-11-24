using System.Net;
using System.Text.Json;
using MercadoInteligente.Application;

namespace MercadoInteligente.Infrastructure.Stores.D1;

// Diagnostic only: never persists a region, order form, cookie, address, or catalog payload.
// A new anonymous session is created for each invocation, not shared with refresh or capture.
public sealed class D1RegionalCapabilityProbe
{
    private static readonly D1DeliveryAddress DiagnosticAddress = new("050030284", "Medellin", "ANTIOQUIA",
        "CL 30 A", "72 - 45", "ROSALES", -75.5932585m, 6.232177m);
    private readonly Func<D1FulfillmentSession> createSession;

    public D1RegionalCapabilityProbe() : this(() => new D1FulfillmentSession()) { }

    // A fresh scripted session per invocation makes the diagnostic testable without Internet.
    public D1RegionalCapabilityProbe(Func<D1FulfillmentSession> createSession) =>
        this.createSession = createSession ?? throw new ArgumentNullException(nameof(createSession));

    public async Task<D1RegionalProbeResult> CheckAsync(CancellationToken ct)
    {
        var checkedAt = DateTime.UtcNow;
        var context = "NOT_CHECKED";
        var catalog = "NOT_CHECKED";
        var price = "NOT_CHECKED";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var session = createSession();
            var region = await session.EstablishAsync(DiagnosticAddress, deadline.Token);
            await session.ConfirmStillValidAsync(deadline.Token);
            context = "ESTABLISHED_NOT_REVALIDATED";
            var (document, _) = await session.GetCatalogAsync(
                "api/catalog_system/pub/products/search?ft=arroz&_from=0&_to=0", deadline.Token);
            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() != 1)
                    throw new JsonException("Unexpected D1 regional catalog shape.");
                var product = D1Mapper.Products(document.RootElement).Single();
                if (product.Skus.Count == 0) throw new JsonException("No D1 regional SKU.");
                var offers = D1Mapper.Offers(document.RootElement, product.ExternalId, new ProviderContext(region));
                await session.ConfirmStillValidAsync(deadline.Token);
                if (session.VerifiedRegionId != region)
                    throw new InvalidDataException("D1 fulfillment region changed during diagnostic.");
                context = "VERIFIED_FULFILLMENT_REGION";
                catalog = "VALID_REGIONAL_CATALOG";
                price = offers.Any(x => x.Price > 0) ? "VALID_FULFILLMENT_REGION_PRICE" : "NO_VALID_PRICE";
            }
            return new(checkedAt, context, catalog, price, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        { return new(checkedAt, context, catalog, price, "TIMEOUT"); }
        catch (HttpRequestException ex)
        { return new(checkedAt, context, catalog, price,
            ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests ? "DECLINED" : "HTTP_ERROR"); }
        catch (JsonException)
        { return new(checkedAt, context, catalog, price, "SCHEMA_DRIFT"); }
        catch (InvalidDataException)
        { return new(checkedAt, "INVALIDATED", "NOT_CHECKED", "NOT_CHECKED", "REGION_INVALID"); }
        catch (InvalidOperationException)
        { return new(checkedAt, context, catalog, price, "SCHEMA_DRIFT"); }
    }
}

public sealed record D1RegionalProbeResult(DateTime CheckedAt, string RegionContext, string RegionalCatalog,
    string RegionalOnlinePrice, string? ErrorCode);
