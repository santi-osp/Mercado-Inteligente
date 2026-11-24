using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using MercadoInteligente.Infrastructure.Stores.Exito;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MercadoInteligente.Tests;

public class Goal04DataLifecycleTests
{
    private static DbContextOptions<MarketDb> Options(string path) =>
        new DbContextOptionsBuilder<MarketDb>()
            .UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                ForeignKeys = true,
                DefaultTimeout = 10,
                Pooling = false
            }.ToString())
            .Options;

    [Fact]
    public void FreshnessService_BoundaryEvaluation_BehavesDeterministically()
    {
        var service = new FreshnessService();
        var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var ttl = TimeSpan.FromHours(6);
        var outdatedThreshold = TimeSpan.FromHours(18);

        // Unknown
        Assert.Equal(Freshness.Unknown, service.Evaluate(null, ttl, outdatedThreshold, now));
        Assert.Equal(Freshness.Unknown, service.Evaluate(default(DateTime), ttl, outdatedThreshold, now));

        // Future timestamp (clock skew guarded)
        Assert.Equal(Freshness.Fresh, service.Evaluate(now.AddMinutes(5), ttl, outdatedThreshold, now));

        // Exactly at boundary
        Assert.Equal(Freshness.Fresh, service.Evaluate(now - TimeSpan.FromHours(6), ttl, outdatedThreshold, now));

        // Stale (between 6h and 18h)
        Assert.Equal(Freshness.Stale, service.Evaluate(now - TimeSpan.FromHours(6).Add(TimeSpan.FromSeconds(1)), ttl, outdatedThreshold, now));
        Assert.Equal(Freshness.Stale, service.Evaluate(now - TimeSpan.FromHours(12), ttl, outdatedThreshold, now));
        Assert.Equal(Freshness.Stale, service.Evaluate(now - TimeSpan.FromHours(18), ttl, outdatedThreshold, now));

        // Outdated (> 18h)
        Assert.Equal(Freshness.Outdated, service.Evaluate(now - TimeSpan.FromHours(18).Add(TimeSpan.FromSeconds(1)), ttl, outdatedThreshold, now));
        Assert.Equal(Freshness.Outdated, service.Evaluate(now - TimeSpan.FromDays(2), ttl, outdatedThreshold, now));
    }

    [Fact]
    public void FreshnessService_DomainSpecificTtls_EvaluatedCorrectly()
    {
        var service = new FreshnessService();
        var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var settings = new AppSettings
        {
            PriceTtlHours = 6,
            StockTtlHours = 2,
            ProductTtlDays = 7,
            StoresTtlDays = 7,
            RouteTtlHours = 24,
            PromoTtlHours = 4
        };

        // Price: 5h is Fresh, 10h is Stale
        Assert.Equal(Freshness.Fresh, service.GetPriceFreshness(now.AddHours(-5), settings, now));
        Assert.Equal(Freshness.Stale, service.GetPriceFreshness(now.AddHours(-10), settings, now));

        // Stock: 1h is Fresh, 3h is Stale
        Assert.Equal(Freshness.Fresh, service.GetStockFreshness(now.AddHours(-1), settings, now));
        Assert.Equal(Freshness.Stale, service.GetStockFreshness(now.AddHours(-3), settings, now));

        // Promo: 3h is Fresh, 5h is Stale
        Assert.Equal(Freshness.Fresh, service.GetPromoFreshness(now.AddHours(-3), settings, now));
        Assert.Equal(Freshness.Stale, service.GetPromoFreshness(now.AddHours(-5), settings, now));

        // Product: 5d is Fresh, 10d is Stale
        Assert.Equal(Freshness.Fresh, service.GetProductFreshness(now.AddDays(-5), settings, now));
        Assert.Equal(Freshness.Stale, service.GetProductFreshness(now.AddDays(-10), settings, now));
    }

    [Fact]
    public void PriceAnomalyDetector_IdentifiesInvalidAndSuspiciousPrices()
    {
        var detector = new PriceAnomalyDetector();

        // Invalid: price <= 0
        var zero = detector.ValidateOfferPrice(0m);
        Assert.Equal(PriceAnomalyStatus.Invalid, zero.Status);

        var negative = detector.ValidateOfferPrice(-500m);
        Assert.Equal(PriceAnomalyStatus.Invalid, negative.Status);

        // Invalid: listPrice <= 0
        var invalidList = detector.ValidateOfferPrice(1000m, listPrice: -100m);
        Assert.Equal(PriceAnomalyStatus.Invalid, invalidList.Status);

        // Suspicious: offer price > listPrice * 1.5
        var markup = detector.ValidateOfferPrice(2000m, listPrice: 1000m);
        Assert.Equal(PriceAnomalyStatus.Suspicious, markup.Status);
        Assert.Contains("supera el precio de lista", markup.Reason);

        // Suspicious: extreme price drop (> 80% decrease, ratio < 0.20)
        var drop = detector.ValidateOfferPrice(1500m, previousPrice: 10000m);
        Assert.Equal(PriceAnomalyStatus.Suspicious, drop.Status);
        Assert.Contains("Caída extrema de precio", drop.Reason);

        // Suspicious: extreme price surge (> 500% increase, ratio > 5.0)
        var surge = detector.ValidateOfferPrice(60000m, previousPrice: 8000m);
        Assert.Equal(PriceAnomalyStatus.Suspicious, surge.Status);
        Assert.Contains("Aumento extremo de precio", surge.Reason);

        // Valid: reasonable price change
        var valid = detector.ValidateOfferPrice(9500m, listPrice: 10000m, previousPrice: 9000m);
        Assert.Equal(PriceAnomalyStatus.Valid, valid.Status);
        Assert.Null(valid.Reason);
    }

    [Fact]
    public void ProductLifecycleService_TransitionsAndReactivation_WorkCorrectly()
    {
        var service = new ProductLifecycleService();
        var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var product = new ExternalProduct
        {
            Name = "Leche Entera",
            Status = ProductLifecycleState.Unknown,
            MissingCount = 0
        };

        // Seen -> Active
        service.MarkProductSeen(product, now);
        Assert.Equal(ProductLifecycleState.Active, product.Status);
        Assert.Equal(0, product.MissingCount);
        Assert.Equal(now, product.LastSeenAt);

        // Absent 1 time -> Still Active, missingCount = 1
        service.MarkProductAbsent(product, now.AddHours(6), missingThreshold: 2, inactiveDays: 30);
        Assert.Equal(ProductLifecycleState.Active, product.Status);
        Assert.Equal(1, product.MissingCount);

        // Absent 2nd time -> Missing
        service.MarkProductAbsent(product, now.AddHours(12), missingThreshold: 2, inactiveDays: 30);
        Assert.Equal(ProductLifecycleState.Missing, product.Status);
        Assert.Equal(2, product.MissingCount);

        // Reappearing -> Reactivated to Active, MissingCount reset to 0
        service.MarkProductSeen(product, now.AddDays(5));
        Assert.Equal(ProductLifecycleState.Active, product.Status);
        Assert.Equal(0, product.MissingCount);
        Assert.Equal(now.AddDays(5), product.LastSeenAt);

        // Absent for > 30 days -> Inactive
        service.MarkProductAbsent(product, now.AddDays(40), missingThreshold: 2, inactiveDays: 30);
        Assert.Equal(ProductLifecycleState.Inactive, product.Status);
    }

    [Fact]
    public void SmartPriceHistoryService_OnlyRecordsObservationsOnRealChanges()
    {
        var service = new SmartPriceHistoryService();
        var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var offer = new Offer
        {
            Price = 4500m,
            ListPrice = 5000m,
            Availability = AvailabilityStatus.InStock,
            PromotionConditions = "Lleva 2 por $8000",
            CheckedAt = now.AddHours(-1)
        };

        // Identical observation: no new row generated, CheckedAt updated
        var obsIdentical = service.RecordObservationIfChanged(
            offer, 4500m, 5000m, AvailabilityStatus.InStock, "Lleva 2 por $8000", null, now);
        Assert.Null(obsIdentical);
        Assert.Equal(now, offer.CheckedAt);

        // Price change: observation generated
        var obsPrice = service.RecordObservationIfChanged(
            offer, 4200m, 5000m, AvailabilityStatus.InStock, "Lleva 2 por $8000", null, now.AddHours(1));
        Assert.NotNull(obsPrice);
        Assert.Equal(4200m, obsPrice!.Price);
        Assert.Equal(offer.Id, obsPrice.OfferId);
        Assert.Equal(4200m, offer.Price);

        // Availability change: observation generated
        var obsAvail = service.RecordObservationIfChanged(
            offer, 4200m, 5000m, AvailabilityStatus.OutOfStock, "Lleva 2 por $8000", null, now.AddHours(2));
        Assert.NotNull(obsAvail);
        Assert.Equal(AvailabilityStatus.OutOfStock, obsAvail!.Availability);
        Assert.Equal(AvailabilityStatus.OutOfStock, offer.Availability);
    }

    [Fact]
    public async Task MaintenanceService_StorageStatsAndDatabaseOptimize_ExecuteCleanly()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal04-maint-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();

            var retailer = new Retailer { Code = "TEST", Name = "Test Retailer" };
            db.Retailers.Add(retailer);
            var product = new ExternalProduct { RetailerId = retailer.Id, ExternalId = "p1", Name = "Arroz" };
            db.ExternalProducts.Add(product);
            await db.SaveChangesAsync();

            var rawStore = new RawStore(paths, db);
            var maint = new MaintenanceService(paths, db, rawStore);

            var stats = await maint.GetStorageStatsAsync();
            Assert.True(stats.DatabaseBytes > 0);
            Assert.Equal(1, stats.ExternalProductsCount);

            var optSuccess = await maint.OptimizeDatabaseAsync();
            Assert.True(optSuccess);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task MaintenanceService_PruneBackups_EnforcesGfsRetention()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal04-gfs-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            Directory.CreateDirectory(paths.Backups);

            // Create fake backup files simulating daily backups for past 20 days
            var now = DateTime.UtcNow;
            var createdFiles = new List<string>();
            for (var d = 1; d <= 20; d++)
            {
                var dt = now.AddDays(-d);
                var filename = $"market-{dt:yyyyMMdd-HHmmss}-000-01234567.db";
                var fullPath = Path.Combine(paths.Backups, filename);
                await File.WriteAllTextAsync(fullPath, "dummy-sqlite-content");
                createdFiles.Add(fullPath);
            }

            // Create an expired pre-restore snapshot (> 7 days) and a fresh one (< 7 days)
            var expiredPreRestore = Path.Combine(paths.Backups, "pre-restore-old.db");
            await File.WriteAllTextAsync(expiredPreRestore, "dummy");
            File.SetLastWriteTimeUtc(expiredPreRestore, now.AddDays(-10));

            var freshPreRestore = Path.Combine(paths.Backups, "pre-restore-new.db");
            await File.WriteAllTextAsync(freshPreRestore, "dummy");
            File.SetLastWriteTimeUtc(freshPreRestore, now.AddDays(-2));

            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            var rawStore = new RawStore(paths, db);
            var maint = new MaintenanceService(paths, db, rawStore);

            var (pruned, retained) = await maint.PruneBackupsAsync(dailyRetention: 7, weeklyRetention: 4, monthlyRetention: 3);

            Assert.True(pruned > 0);
            Assert.True(retained > 0);
            Assert.False(File.Exists(expiredPreRestore));
            Assert.True(File.Exists(freshPreRestore));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SyncEngine_AbortHangingRuns_RecoversHangingState()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal04-sync-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();

            var run1 = new SyncRun { Provider = "D1", State = SyncState.Running, StartedAt = DateTime.UtcNow.AddMinutes(-10) };
            var run2 = new SyncRun { Provider = "EXITO", State = SyncState.Completed, StartedAt = DateTime.UtcNow.AddMinutes(-5), FinishedAt = DateTime.UtcNow };
            db.SyncRuns.AddRange(run1, run2);
            await db.SaveChangesAsync();

            var freshness = new FreshnessService();
            var d1Sync = new D1SearchSync(new HttpClient(), new D1CatalogIngest(db, new RawStore(paths, db)), db);
            var exitoSync = new ExitoSearchSync(new HttpClient(), new ExitoCatalogIngest(db, new RawStore(paths, db)), db);
            var engine = new SyncEngine(db, d1Sync, exitoSync, freshness);

            var abortedCount = await engine.AbortHangingRunsAsync();
            Assert.Equal(1, abortedCount);

            var updatedRun1 = await db.SyncRuns.SingleAsync(r => r.Id == run1.Id);
            Assert.Equal(SyncState.Aborted, updatedRun1.State);
            Assert.Equal("ABORTED_ON_STARTUP", updatedRun1.ErrorCode);
            Assert.NotNull(updatedRun1.FinishedAt);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Api_StorageAndMaintenanceEndpoints_ReturnSuccess()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal04-api-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            // Storage stats
            var storageRes = await client.GetAsync("/api/v1/system/storage");
            Assert.Equal(HttpStatusCode.OK, storageRes.StatusCode);
            var storage = await storageRes.Content.ReadFromJsonAsync<StorageStats>();
            Assert.NotNull(storage);
            Assert.True(storage!.DatabaseBytes > 0);

            // Trigger maintenance
            var maintRes = await client.PostAsync("/api/v1/system/maintenance", null);
            Assert.Equal(HttpStatusCode.OK, maintRes.StatusCode);
            var report = await maintRes.Content.ReadFromJsonAsync<MaintenanceReport>();
            Assert.NotNull(report);
            Assert.True(report!.DatabaseOptimized);

            // Sync runs
            var runsRes = await client.GetAsync("/api/v1/sync/runs");
            Assert.Equal(HttpStatusCode.OK, runsRes.StatusCode);

            // Settings validation
            var invalidSettings = new AppSettings { MaxStores = 10 }; // Invalid: > 5
            var badSettingsRes = await client.PutAsJsonAsync("/api/v1/settings", invalidSettings);
            Assert.Equal(HttpStatusCode.BadRequest, badSettingsRes.StatusCode);

            var validSettings = new AppSettings
            {
                MaxStores = 3,
                PriceTtlHours = 8,
                StockTtlHours = 3,
                RawRetentionDays = 45,
                LogRetentionDays = 15
            };
            var goodSettingsRes = await client.PutAsJsonAsync("/api/v1/settings", validSettings);
            Assert.Equal(HttpStatusCode.OK, goodSettingsRes.StatusCode);
            var updatedSettings = await goodSettingsRes.Content.ReadFromJsonAsync<AppSettings>();
            Assert.NotNull(updatedSettings);
            Assert.Equal(8, updatedSettings!.PriceTtlHours);
            Assert.Equal(45, updatedSettings.RawRetentionDays);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
