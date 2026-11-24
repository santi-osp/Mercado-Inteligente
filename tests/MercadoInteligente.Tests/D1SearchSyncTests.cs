using System.Net;
using System.Text;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

public class D1SearchSyncTests
{
    [Fact]
    public async Task Successful_manual_refresh_records_run_and_does_not_fetch_extra_pages()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-sync-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var handler = new Handler();
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
            var sync = new D1SearchSync(client, new D1CatalogIngest(db, new RawStore(paths, db)), db);
            var result = await sync.RefreshAsync("arroz", CancellationToken.None);
            Assert.Equal("COMPLETED", result.Status);
            Assert.Equal(1, result.OffersSeen);
            Assert.Equal(1, handler.Calls);
            Assert.Equal(1990m, (await db.Offers.SingleAsync()).Price);
            var run = await db.SyncRuns.SingleAsync();
            Assert.Equal(SyncState.Completed, run.State);
            Assert.Equal(1, run.ItemsReceived);
            Assert.Equal(1, run.OffersProcessed);
            Assert.Equal(1, run.OffersChanged);
            Assert.Equal(1, run.ProductsCreated);
            Assert.Equal(1, run.SkusCreated);
            Assert.Equal(1, run.RawNew);
            Assert.Equal(0, run.ProductsUpdated);
            Assert.Equal(0, run.SkusUpdated);
            Assert.Equal(0, run.RawDeduplicated);
            Assert.NotNull(run.FinishedAt);
            Assert.Null(run.ErrorCode);
            Assert.Equal("COOLDOWN", (await sync.RefreshAsync("arroz", CancellationToken.None)).Status);
            Assert.Equal(1, handler.Calls);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Rate_limit_marks_run_failed_without_deleting_an_existing_offer()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-fail-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var retailer = new Retailer { Code = "D1", Name = "D1" };
            var product = new ExternalProduct { RetailerId = retailer.Id, ExternalId = "1092", Name = "Arroz" };
            var sku = new ExternalSku { ExternalProductId = product.Id, ExternalId = "1092", Name = "Arroz" };
            var seller = new Seller { RetailerId = retailer.Id, ExternalId = "1", Name = "D1" };
            var offer = new Offer { ExternalSkuId = sku.Id, SellerId = seller.Id, Context = "online-generic", Price = 1990m, Scope = PriceScope.OnlineGeneric };
            db.AddRange(retailer, product, sku, seller, offer);
            await db.SaveChangesAsync();
            using var client = new HttpClient(new RateLimitHandler()) { BaseAddress = new Uri("https://www.d1.com.co/") };
            var sync = new D1SearchSync(client, new D1CatalogIngest(db, new RawStore(paths, db)), db);
            var error = await Assert.ThrowsAsync<HttpRequestException>(() => sync.RefreshAsync("arroz", CancellationToken.None));
            Assert.Equal(HttpStatusCode.TooManyRequests, error.StatusCode);
            db.ChangeTracker.Clear();
            Assert.Equal(1990m, (await db.Offers.SingleAsync()).Price);
            var run = await db.SyncRuns.SingleAsync();
            Assert.Equal(SyncState.Failed, run.State);
            Assert.Equal("RATE_LIMITED", run.ErrorCode);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Schema_drift_marks_run_failed_without_creating_an_offer()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-drift-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            using var client = new HttpClient(new DriftHandler()) { BaseAddress = new Uri("https://www.d1.com.co/") };
            var sync = new D1SearchSync(client, new D1CatalogIngest(db, new RawStore(paths, db)), db);
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => sync.RefreshAsync("arroz", CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Empty(await db.Offers.ToListAsync());
            Assert.Equal("SCHEMA_DRIFT", (await db.SyncRuns.SingleAsync()).ErrorCode);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false, "RATE_LIMITED")]
    [InlineData(true, "CANCELLED")]
    public async Task Failed_or_cancelled_second_page_preserves_first_page_and_marks_run_failed(bool cancel, string errorCode)
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-pages-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            using var source = new CancellationTokenSource();
            var handler = new SecondPageFailureHandler(cancel, source);
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
            var sync = new D1SearchSync(client, new D1CatalogIngest(db, new RawStore(paths, db)), db);
            if (cancel)
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sync.RefreshAsync("arroz", source.Token));
            else
                await Assert.ThrowsAsync<HttpRequestException>(() => sync.RefreshAsync("arroz", source.Token));
            db.ChangeTracker.Clear();
            Assert.Equal(2, handler.Calls);
            Assert.Equal(20, await db.Offers.CountAsync());
            Assert.All(await db.Offers.ToListAsync(), offer => Assert.Equal(1990m, offer.Price));
            Assert.Equal(20, await db.PriceObservations.CountAsync());
            var run = await db.SyncRuns.SingleAsync();
            Assert.Equal(SyncState.Failed, run.State);
            Assert.Equal(errorCode, run.ErrorCode);
            Assert.Equal(20, run.ItemsReceived);
            Assert.Equal(20, run.OffersProcessed);
            Assert.Equal(20, run.OffersChanged);
            Assert.Equal(20, run.ProductsCreated);
            Assert.Equal(20, run.SkusCreated);
            Assert.Equal(1, run.RawNew);
            Assert.Equal(0, run.RawDeduplicated);
            Assert.NotNull(run.FinishedAt);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "FORBIDDEN")]
    [InlineData(HttpStatusCode.InternalServerError, "HTTP_ERROR")]
    public async Task Provider_http_failure_records_controlled_status(HttpStatusCode status, string code)
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-http-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            using var client = new HttpClient(new StatusHandler(status)) { BaseAddress = new Uri("https://www.d1.com.co/") };
            var sync = new D1SearchSync(client, new D1CatalogIngest(db, new RawStore(paths, db)), db);
            var error = await Assert.ThrowsAsync<HttpRequestException>(() => sync.RefreshAsync("arroz", CancellationToken.None));
            Assert.Equal(status, error.StatusCode);
            db.ChangeTracker.Clear();
            Assert.Empty(await db.Offers.ToListAsync());
            Assert.Equal(code, (await db.SyncRuns.SingleAsync()).ErrorCode);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Empty_first_page_completes_without_removing_existing_offers()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-empty-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var ingest = new D1CatalogIngest(db, new RawStore(paths, db));
            using var initial = new HttpClient(new Handler()) { BaseAddress = new Uri("https://www.d1.com.co/") };
            Assert.Equal("COMPLETED", (await new D1SearchSync(initial, ingest, db).RefreshAsync("arroz", CancellationToken.None)).Status);
            using var empty = new HttpClient(new EmptyHandler()) { BaseAddress = new Uri("https://www.d1.com.co/") };
            var result = await new D1SearchSync(empty, ingest, db).RefreshAsync("arroz", CancellationToken.None);
            Assert.Equal("COMPLETED", result.Status);
            Assert.Equal(0, result.OffersSeen);
            db.ChangeTracker.Clear();
            Assert.Equal(1990m, (await db.Offers.SingleAsync()).Price);
            Assert.Equal(2, await db.SyncRuns.CountAsync());
            var emptyRun = (await db.SyncRuns.OrderBy(x => x.StartedAt).ToListAsync()).Last();
            Assert.Equal(0, emptyRun.ItemsReceived);
            Assert.Equal(0, emptyRun.OffersProcessed);
            Assert.Equal(0, emptyRun.OffersChanged);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Oversized_page_marks_run_failed_without_overwriting_valid_data()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-large-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            using var client = new HttpClient(new LargeHandler()) { BaseAddress = new Uri("https://www.d1.com.co/") };
            var sync = new D1SearchSync(client, new D1CatalogIngest(db, new RawStore(paths, db)), db);
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => sync.RefreshAsync("arroz", CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Empty(await db.Offers.ToListAsync());
            Assert.Empty(await db.RawCaptures.ToListAsync());
            Assert.Equal("SCHEMA_DRIFT", (await db.SyncRuns.SingleAsync()).ErrorCode);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Excess_page_count_fails_before_ingesting_any_product()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-count-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            using var client = new HttpClient(new ExcessHandler()) { BaseAddress = new Uri("https://www.d1.com.co/") };
            var sync = new D1SearchSync(client, new D1CatalogIngest(db, new RawStore(paths, db)), db);
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => sync.RefreshAsync("arroz", CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Empty(await db.Offers.ToListAsync());
            Assert.Equal("SCHEMA_DRIFT", (await db.SyncRuns.SingleAsync()).ErrorCode);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, 2, "COMPLETED")]
    [InlineData(HttpStatusCode.Forbidden, 1, "FORBIDDEN")]
    [InlineData(HttpStatusCode.TooManyRequests, 1, "RATE_LIMITED")]
    public async Task Transient_retry_is_bounded_and_denials_are_never_retried(
        HttpStatusCode firstStatus, int calls, string outcome)
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-retry-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var handler = new RecoveryHandler(firstStatus);
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
            var sync = new D1SearchSync(client, new D1CatalogIngest(db, new RawStore(paths, db)), db);
            if (outcome == "COMPLETED")
                Assert.Equal(outcome, (await sync.RefreshAsync("arroz", CancellationToken.None)).Status);
            else
                await Assert.ThrowsAsync<HttpRequestException>(() => sync.RefreshAsync("arroz", CancellationToken.None));
            Assert.Equal(calls, handler.Calls);
            db.ChangeTracker.Clear();
            var run = await db.SyncRuns.SingleAsync();
            Assert.Equal(outcome == "COMPLETED" ? SyncState.Completed : SyncState.Failed, run.State);
            Assert.Equal(outcome == "COMPLETED" ? null : outcome, run.ErrorCode);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Repeated_transient_failure_stops_after_one_retry()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-retry-fail-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var handler = new RecoveryHandler(HttpStatusCode.ServiceUnavailable, recover: false);
            using var client = new HttpClient(handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
            var sync = new D1SearchSync(client, new D1CatalogIngest(db, new RawStore(paths, db)), db);
            var error = await Assert.ThrowsAsync<HttpRequestException>(() => sync.RefreshAsync("arroz", CancellationToken.None));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, error.StatusCode);
            Assert.Equal(2, handler.Calls);
            db.ChangeTracker.Clear();
            Assert.Equal("HTTP_ERROR", (await db.SyncRuns.SingleAsync()).ErrorCode);
            Assert.Empty(await db.Offers.ToListAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class RecoveryHandler(HttpStatusCode firstStatus, bool recover = true) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            var status = Calls == 1 || !recover ? firstStatus : HttpStatusCode.OK;
            return Task.FromResult(new HttpResponseMessage(status)
                { Content = new StringContent("[]", Encoding.UTF8, "application/json") });
        }
    }

    private sealed class ExcessHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[" +
                string.Join(",", Enumerable.Repeat("{}", 21)) + "]", Encoding.UTF8, "application/json") });
    }

    private sealed class LargeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(new string('x', 2 * 1024 * 1024 + 1), Encoding.UTF8, "application/json") });
    }

    [Fact]
    public async Task Provider_timeout_is_not_misreported_as_user_cancellation()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-timeout-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            using var client = new HttpClient(new TimeoutHandler()) { BaseAddress = new Uri("https://www.d1.com.co/") };
            var sync = new D1SearchSync(client, new D1CatalogIngest(db, new RawStore(paths, db)), db);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sync.RefreshAsync("arroz", CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Empty(await db.Offers.ToListAsync());
            Assert.Equal("TIMEOUT", (await db.SyncRuns.SingleAsync()).ErrorCode);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(new TaskCanceledException("Request timed out."));
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    private sealed class EmptyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]", Encoding.UTF8, "application/json") });
    }

    private sealed class SecondPageFailureHandler(bool cancel, CancellationTokenSource source) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            if (Calls == 2)
            {
                Assert.Contains("_from=20", request.RequestUri!.Query);
                if (cancel)
                {
                    source.Cancel();
                    return Task.FromCanceled<HttpResponseMessage>(ct);
                }
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            }
            Assert.Contains("_from=0", request.RequestUri!.Query);
            var products = Enumerable.Range(1, 20).Select(id =>
                new { productId = id.ToString(), productName = "Arroz", items = new[] {
                    new { itemId = id.ToString(), name = "Arroz 500 G", sellers = new[] {
                        new { sellerId = "1", sellerName = "D1", commertialOffer = new { Price = 1990, IsAvailable = true, AvailableQuantity = 99999 } }
                    } }
                } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(products), Encoding.UTF8, "application/json") });
        }
    }

    private sealed class DriftHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1092\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{}}]}]}]", Encoding.UTF8, "application/json") });
    }

    private sealed class RateLimitHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
    }

    private sealed class Handler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Assert.Contains("ft=arroz", request.RequestUri!.Query);
            var json = "[{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1092\",\"name\":\"Arroz 500 G\",\"sellers\":[{\"sellerId\":\"1\",\"sellerName\":\"D1\",\"commertialOffer\":{\"Price\":1990,\"IsAvailable\":true,\"AvailableQuantity\":99999}}]}]}]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
