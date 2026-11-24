using System.Net;
using System.Text;
using MercadoInteligente.Domain;
using MercadoInteligente.Application;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

public class D1RegionalCaptureTests
{
    private static readonly D1DeliveryAddress Address = new("050030284", "Medellin", "ANTIOQUIA",
        "CL 30 A", "72 - 45", "ROSALES", -75.5932585m, 6.232177m);

    [Theory]
    [InlineData(false, false, false, 1)]
    [InlineData(true, false, false, 0)]
    [InlineData(false, true, false, 0)]
    [InlineData(false, false, true, 0)]
    [InlineData(false, false, false, 0, true)]
    public async Task Only_matching_detail_with_stable_verified_region_can_enter_sqlite(
        bool changedRegion, bool wrongProduct, bool missingPrice, int expected, bool extraMalformed = false)
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-region-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var handler = new RegionalHandler(changedRegion, wrongProduct, missingPrice, extraMalformed);
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
            using var session = new D1FulfillmentSession(client);
            var capture = new D1RegionalCapture(session, new D1CatalogIngest(db, new RawStore(paths, db)));
            if (expected == 1)
                Assert.Equal(1, await capture.CaptureProductAsync(Address, "1092", CancellationToken.None));
            else
                await Assert.ThrowsAnyAsync<Exception>(() => capture.CaptureProductAsync(Address, "1092", CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Equal(expected, await db.Offers.CountAsync());
            Assert.Equal(expected, await db.PriceObservations.CountAsync());
            if (expected == 1)
            {
                var offer = await db.Offers.SingleAsync();
                Assert.Equal(PriceScope.FulfillmentRegion, offer.Scope);
                Assert.Equal("medellin-fulfillment", offer.Context);
                Assert.Equal(1990m, offer.Price);
                Assert.Equal(99999m, offer.AvailableQuantityRaw);
                Assert.Equal(1, handler.CatalogCalls);
            }
        }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { } }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Provider_revalidates_fulfillment_context_after_detail_fetch(bool changedRegion)
    {
        var handler = new RegionalHandler(changedRegion, false, false);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        var region = await session.EstablishAsync(Address, CancellationToken.None);
        var provider = new D1Provider(client, session);
        if (changedRegion)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => provider.OffersAsync("1092", new ProviderContext(region), CancellationToken.None));
            Assert.Null(session.VerifiedRegionId);
        }
        else
        {
            var offer = Assert.Single(await provider.OffersAsync("1092", new ProviderContext(region), CancellationToken.None));
            Assert.Equal(PriceScope.FulfillmentRegion, offer.Scope);
            Assert.Equal(region, offer.Context);
        }
        Assert.Equal(1, handler.CatalogCalls);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task Failed_regional_recapture_preserves_last_known_good_offer(
        bool changedRegion, bool wrongProduct, bool missingPrice)
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-region-retain-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var ingest = new D1CatalogIngest(db, new RawStore(paths, db));
            using (var client = new HttpClient(new RegionalHandler(false, false, false)) { BaseAddress = new Uri("https://www.d1.com.co/") })
            using (var session = new D1FulfillmentSession(client))
                Assert.Equal(1, await new D1RegionalCapture(session, ingest).CaptureProductAsync(Address, "1092", CancellationToken.None));
            db.ChangeTracker.Clear();
            var before = await db.Offers.AsNoTracking().SingleAsync();
            var rawCount = await db.RawCaptures.CountAsync();
            using (var client = new HttpClient(new RegionalHandler(changedRegion, wrongProduct, missingPrice)) { BaseAddress = new Uri("https://www.d1.com.co/") })
            using (var session = new D1FulfillmentSession(client))
                await Assert.ThrowsAnyAsync<Exception>(() => new D1RegionalCapture(session, ingest)
                    .CaptureProductAsync(Address, "1092", CancellationToken.None));
            db.ChangeTracker.Clear();
            var after = await db.Offers.AsNoTracking().SingleAsync();
            Assert.Equal(before.Id, after.Id);
            Assert.Equal(before.Price, after.Price);
            Assert.Equal(before.Context, after.Context);
            Assert.Equal(before.Scope, after.Scope);
            Assert.Equal(before.CheckedAt, after.CheckedAt);
            Assert.Equal(before.RawCaptureId, after.RawCaptureId);
            Assert.Single(await db.PriceObservations.ToListAsync());
            Assert.Equal(rawCount, await db.RawCaptures.CountAsync());
        }
        finally { try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { } }
    }

    [Fact]
    public async Task Invalid_id_never_starts_a_session_or_fetches_catalog()
    {
        using var client = new HttpClient(new RegionalHandler(false, false, false)) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var session = new D1FulfillmentSession(client);
        var capture = new D1RegionalCapture(session, null!);
        await Assert.ThrowsAsync<ArgumentException>(() => capture.CaptureProductAsync(Address, "../segments", CancellationToken.None));
    }

    private sealed class RegionalHandler(bool changedRegion, bool wrongProduct, bool missingPrice, bool extraMalformed = false) : HttpMessageHandler
    {
        private int segmentCalls;
        public int CatalogCalls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            var body = "{}";
            var code = HttpStatusCode.OK;
            if (path.StartsWith("/api/sessions?", StringComparison.Ordinal)) code = HttpStatusCode.BadRequest;
            else if (path == "/api/checkout/pub/orderForm")
                body = "{\"orderFormId\":\"e43e5fd1-70a3-4026-8e7b-710ac73539ab\"}";
            else if (path.StartsWith("/_v/private/segment/", StringComparison.Ordinal)) { }
            else if (path == "/api/segments")
            {
                segmentCalls++;
                body = "{\"channel\":\"1\",\"regionId\":\"" + (changedRegion && segmentCalls == 3 ? "other-region" : "medellin-fulfillment") +
                    "\",\"facets\":\"coordinates=-75.5932585,6.232177;\"}";
            }
            else if (path.StartsWith("/api/catalog_system/pub/products/search?fq=productId:1092", StringComparison.Ordinal))
            {
                CatalogCalls++;
                body = "[{\"productId\":\"" + (wrongProduct ? "999" : "1092") +
                    "\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1092\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"" +
                    (missingPrice ? "MissingPrice" : "Price") + "\":1990,\"AvailableQuantity\":99999}}]}]}]";
                if (extraMalformed) body = body[..^1] + ", {\"items\":[]}]";
            }
            else throw new InvalidOperationException("Unexpected D1 endpoint: " + path);
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
