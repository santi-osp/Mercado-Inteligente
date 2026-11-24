using System.Net;
using System.Text;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.Exito;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

public class ExitoSearchSyncTests
{
    private static string FastStoreResponse(int count, int start = 1, bool isMkp = false, string sellerId = "1")
    {
        var edges = Enumerable.Range(start, count).Select(id => new
        {
            node = new
            {
                name = $"Producto {id}",
                isVariantOf = new { productGroupID = $"G{id}" },
                brand = new { brandName = "Marca" },
                productType = new { isMkp },
                items = new[]
                {
                    new
                    {
                        itemId = $"item-{id}",
                        name = $"Producto {id} 500g",
                        ean = $"770000000{id:D4}",
                        sellers = new[]
                        {
                            new
                            {
                                sellerId,
                                sellerName = "Éxito",
                                commertialOffer = new
                                {
                                    Price = 3000 + id * 100,
                                    ListPrice = 3500 + id * 100,
                                    AvailableQuantity = 50
                                }
                            }
                        }
                    }
                }
            }
        });

        return JsonSerializer.Serialize(new
        {
            data = new
            {
                search = new
                {
                    products = new
                    {
                        pageInfo = new { totalCount = count },
                        edges
                    }
                }
            }
        });
    }

    [Fact]
    public async Task Successful_manual_refresh_records_run_and_cooldown_prevents_immediate_subsequent_call()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-sync-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();

            var handler = new SinglePageHandler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
            var sync = new ExitoSearchSync(client, new ExitoCatalogIngest(db, new RawStore(paths, db)), db);

            var result = await sync.RefreshAsync("leche", null, CancellationToken.None);
            Assert.Equal("COMPLETED", result.Status);
            Assert.Equal(1, result.OffersSeen);
            Assert.Equal(1, handler.Calls);

            var offer = await db.Offers.SingleAsync();
            Assert.Equal(3100m, offer.Price);
            Assert.Equal(PriceScope.OnlineGeneric, offer.Scope);

            var run = await db.SyncRuns.SingleAsync();
            Assert.Equal("EXITO", run.Provider);
            Assert.Equal(SyncState.Completed, run.State);
            Assert.Equal(1, run.ItemsReceived);
            Assert.Equal(1, run.OffersProcessed);
            Assert.Equal(1, run.OffersChanged);
            Assert.Equal(1, run.ProductsCreated);
            Assert.Equal(1, run.SkusCreated);
            Assert.Equal(1, run.RawNew);
            Assert.Null(run.ErrorCode);
            Assert.NotNull(run.FinishedAt);

            // Cooldown test
            var coolResult = await sync.RefreshAsync("leche", null, CancellationToken.None);
            Assert.Equal("COOLDOWN", coolResult.Status);
            Assert.Equal(1, handler.Calls); // No second HTTP request fired
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Rate_limit_marks_run_failed_without_deleting_existing_offers()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-429-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();

            var retailer = new Retailer { Code = "EXITO", Name = "Éxito" };
            var product = new ExternalProduct { RetailerId = retailer.Id, ExternalId = "G1", Name = "Leche" };
            var sku = new ExternalSku { ExternalProductId = product.Id, ExternalId = "item-1", Name = "Leche 500g" };
            var seller = new Seller { RetailerId = retailer.Id, ExternalId = "1", Name = "Éxito", Kind = SellerKind.RetailerDirect };
            var offer = new Offer { ExternalSkuId = sku.Id, SellerId = seller.Id, Context = "online-generic", Price = 3200m, Scope = PriceScope.OnlineGeneric };
            db.AddRange(retailer, product, sku, seller, offer);
            await db.SaveChangesAsync();

            using var client = new HttpClient(new StatusHandler(HttpStatusCode.TooManyRequests)) { BaseAddress = new Uri("https://www.exito.com/") };
            var sync = new ExitoSearchSync(client, new ExitoCatalogIngest(db, new RawStore(paths, db)), db);

            var ex = await Assert.ThrowsAsync<HttpRequestException>(() => sync.RefreshAsync("leche", null, CancellationToken.None));
            Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);

            db.ChangeTracker.Clear();
            Assert.Equal(3200m, (await db.Offers.SingleAsync()).Price);
            var run = await db.SyncRuns.SingleAsync();
            Assert.Equal(SyncState.Failed, run.State);
            Assert.Equal("RATE_LIMITED", run.ErrorCode);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, 2, "COMPLETED")]
    [InlineData(HttpStatusCode.Forbidden, 1, "FORBIDDEN")]
    public async Task Transient_retry_is_bounded_and_denials_never_retried(HttpStatusCode firstStatus, int expectedCalls, string outcome)
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-retry-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();

            var handler = new RecoveryHandler(firstStatus);
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
            var sync = new ExitoSearchSync(client, new ExitoCatalogIngest(db, new RawStore(paths, db)), db);

            if (outcome == "COMPLETED")
            {
                var result = await sync.RefreshAsync("arroz", null, CancellationToken.None);
                Assert.Equal("COMPLETED", result.Status);
            }
            else
            {
                await Assert.ThrowsAsync<HttpRequestException>(() => sync.RefreshAsync("arroz", null, CancellationToken.None));
            }

            Assert.Equal(expectedCalls, handler.Calls);
            db.ChangeTracker.Clear();
            var run = await db.SyncRuns.SingleAsync();
            Assert.Equal(outcome == "COMPLETED" ? SyncState.Completed : SyncState.Failed, run.State);
            Assert.Equal(outcome == "COMPLETED" ? null : outcome, run.ErrorCode);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Concurrent_call_returns_busy_while_lock_is_held()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-busy-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();

            var sharedGate = new ExitoRefreshGate();
            await sharedGate.Semaphore.WaitAsync(); // manually hold lock

            using var client = new HttpClient(new SinglePageHandler()) { BaseAddress = new Uri("https://www.exito.com/") };
            var sync = new ExitoSearchSync(client, new ExitoCatalogIngest(db, new RawStore(paths, db)), db, sharedGate);

            var result = await sync.RefreshAsync("leche", null, CancellationToken.None);
            Assert.Equal("BUSY", result.Status);
            Assert.Equal(0, result.OffersSeen);

            sharedGate.Semaphore.Release();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Regional_context_is_passed_to_channel_facet_and_scope()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-reg-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();

            var handler = new RegionalQueryHandler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.exito.com/") };
            var sync = new ExitoSearchSync(client, new ExitoCatalogIngest(db, new RawStore(paths, db)), db);

            var context = new ProviderContext("fulfillment:test-region-123");
            var result = await sync.RefreshAsync("leche", context, CancellationToken.None);

            Assert.Equal("COMPLETED", result.Status);
            Assert.True(handler.SawRegionInFacet);

            var offer = await db.Offers.SingleAsync();
            Assert.Equal(PriceScope.FulfillmentRegion, offer.Scope);
            Assert.Equal("fulfillment:test-region-123", offer.Context);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class SinglePageHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FastStoreResponse(1), Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    private sealed class RecoveryHandler(HttpStatusCode firstStatus) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var status = Calls == 1 ? firstStatus : HttpStatusCode.OK;
            var content = Calls == 1 && firstStatus != HttpStatusCode.OK
                ? new StringContent("error", Encoding.UTF8, "text/plain")
                : new StringContent(FastStoreResponse(1), Encoding.UTF8, "application/json");
            return Task.FromResult(new HttpResponseMessage(status) { Content = content });
        }
    }

    private sealed class RegionalQueryHandler : HttpMessageHandler
    {
        public bool SawRegionInFacet { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var query = request.RequestUri!.Query;
            if (query.Contains("test-region-123"))
                SawRegionInFacet = true;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FastStoreResponse(1), Encoding.UTF8, "application/json")
            });
        }
    }
}
