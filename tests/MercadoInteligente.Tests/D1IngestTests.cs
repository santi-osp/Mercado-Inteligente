using System.Text;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

public class D1IngestTests
{
    [Fact]
    public async Task Metrics_distinguish_created_changed_unchanged_and_deduplicated_raw()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-metrics-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var ingest = new D1CatalogIngest(db, new RawStore(paths, db));
            var context = new ProviderContext("online-generic");
            var date = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
            static byte[] Catalog(string name, int price) => Encoding.UTF8.GetBytes(
                "[{\"productId\":\"1092\",\"productName\":\"" + name + "\",\"items\":[{\"itemId\":\"1092\",\"name\":\"" + name +
                "\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"Price\":" + price + "}}]}]}]");
            var initial = Catalog("Arroz", 1990);
            var first = await ingest.SaveWithMetricsAsync(initial, context, PriceScope.OnlineGeneric, date, CancellationToken.None);
            Assert.Equal(new D1IngestMetrics(1, 1, 1, 0, 1, 0, 1, 0), first);
            var repeat = await ingest.SaveWithMetricsAsync(initial, context, PriceScope.OnlineGeneric, date.AddMinutes(1), CancellationToken.None);
            Assert.Equal(new D1IngestMetrics(1, 0, 0, 0, 0, 0, 0, 1), repeat);
            var update = await ingest.SaveWithMetricsAsync(Catalog("Arroz largo", 2000), context,
                PriceScope.OnlineGeneric, date.AddMinutes(2), CancellationToken.None);
            Assert.Equal(new D1IngestMetrics(1, 1, 0, 1, 0, 1, 1, 0), update);
            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.RawCaptures.CountAsync());
            Assert.Equal(2, await db.PriceObservations.CountAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Missing_indexed_raw_file_is_repaired_without_new_index()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-raw-repair-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var ingest = new D1CatalogIngest(db, new RawStore(paths, db));
            var payload = Encoding.UTF8.GetBytes("[{\"productId\":\"1\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"Price\":1990}}]}]}]");
            var date = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
            var context = new ProviderContext("online-generic");
            Assert.Equal(1, (await ingest.SaveWithMetricsAsync(payload, context, PriceScope.OnlineGeneric,
                date, CancellationToken.None)).RawNew);
            var file = Path.Combine(paths.Raw, (await db.RawCaptures.SingleAsync()).RelativePath);
            File.Delete(file);
            var repaired = await ingest.SaveWithMetricsAsync(payload, context, PriceScope.OnlineGeneric,
                date.AddMinutes(1), CancellationToken.None);
            Assert.Equal(0, repaired.RawNew);
            Assert.Equal(1, repaired.RawDeduplicated);
            Assert.True(File.Exists(file));
            Assert.Single(await db.RawCaptures.ToListAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Product_url_and_category_survive_omission_and_older_capture()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-product-meta-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var ingest = new D1CatalogIngest(db, new RawStore(paths, db));
            var context = new ProviderContext("online-generic");
            var date = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
            static byte[] Catalog(string name, string metadata) => Encoding.UTF8.GetBytes(
                "[{\"productId\":\"1092\",\"productName\":\"" + name + "\"," + metadata +
                "\"items\":[{\"itemId\":\"1092\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"Price\":1990}}]}]}]");
            await ingest.SaveAsync(Catalog("Inicial", "\"link\":\"https://www.d1.com.co/arroz/p\",\"categories\":[\"/Granos/Arroz/\"],"),
                context, PriceScope.OnlineGeneric, date, CancellationToken.None);
            db.ChangeTracker.Clear();
            Assert.Equal("https://www.d1.com.co/arroz/p", (await db.ExternalProducts.SingleAsync()).ProductUrl);
            Assert.Equal("/Granos/Arroz/", (await db.ExternalProducts.SingleAsync()).CategoryPath);
            await ingest.SaveAsync(Catalog("Actualizado", ""), context, PriceScope.OnlineGeneric, date.AddHours(2), CancellationToken.None);
            await ingest.SaveAsync(Catalog("Antiguo", "\"link\":\"https://www.d1.com.co/old/p\",\"categories\":[\"/Anterior/\"],"),
                context, PriceScope.OnlineGeneric, date.AddHours(1), CancellationToken.None);
            db.ChangeTracker.Clear();
            var product = await db.ExternalProducts.SingleAsync();
            Assert.Equal("Actualizado", product.Name);
            Assert.Equal(date.AddHours(2), product.UpdatedAt);
            Assert.Equal("https://www.d1.com.co/arroz/p", product.ProductUrl);
            Assert.Equal("/Granos/Arroz/", product.CategoryPath);
            Assert.Single(await db.Offers.ToListAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Older_product_metadata_does_not_block_a_newer_offer_for_another_context()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-older-product-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var ingest = new D1CatalogIngest(db, new RawStore(paths, db));
            var context = new ProviderContext("online-generic");
            var date = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
            static byte[] Catalog(string name, int price) => Encoding.UTF8.GetBytes(
                "[{\"productId\":\"1092\",\"productName\":\"" + name + "\",\"items\":[{\"itemId\":\"1092\",\"name\":\"" + name + "\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"Price\":" + price + "}}]}]}]");
            await ingest.SaveAsync(Catalog("Nuevo", 1990), context, PriceScope.OnlineGeneric, date, CancellationToken.None);
            db.ChangeTracker.Clear();
            var product = await db.ExternalProducts.SingleAsync();
            product.UpdatedAt = date.AddHours(2); // A different context refreshed product metadata later.
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await ingest.SaveAsync(Catalog("Viejo", 2000), context, PriceScope.OnlineGeneric, date.AddHours(1), CancellationToken.None);
            db.ChangeTracker.Clear();
            Assert.Equal("Nuevo", (await db.ExternalProducts.SingleAsync()).Name);
            Assert.Equal("Nuevo", (await db.ExternalSkus.SingleAsync()).Name);
            Assert.Equal(date.AddHours(2), (await db.ExternalProducts.SingleAsync()).UpdatedAt);
            Assert.Equal(2000m, (await db.Offers.SingleAsync()).Price);
            Assert.Equal(2, await db.PriceObservations.CountAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task List_price_and_raw_quantity_are_persisted_without_claiming_physical_inventory()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-metadata-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var ingest = new D1CatalogIngest(db, new RawStore(paths, db));
            var checkedAt = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
            static byte[] Catalog(int listPrice, int quantity) => Encoding.UTF8.GetBytes(
                "[{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1092\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"Price\":1990,\"ListPrice\":" + listPrice + ",\"AvailableQuantity\":" + quantity + ",\"IsAvailable\":true}}]}]}]");
            var context = new ProviderContext("online-generic");
            await ingest.SaveAsync(Catalog(2200, 99999), context, PriceScope.OnlineGeneric, checkedAt, CancellationToken.None);
            db.ChangeTracker.Clear();
            var first = await db.Offers.SingleAsync();
            Assert.Equal(2200m, first.ListPrice);
            Assert.Equal(99999m, first.AvailableQuantityRaw);
            Assert.Equal(AvailabilityStatus.InStock, first.Availability);
            Assert.Equal(PriceScope.OnlineGeneric, first.Scope);
            await ingest.SaveAsync(Catalog(2100, 0), context, PriceScope.OnlineGeneric, checkedAt.AddMinutes(5), CancellationToken.None);
            db.ChangeTracker.Clear();
            var changed = await db.Offers.SingleAsync();
            Assert.Equal(1990m, changed.Price);
            Assert.Equal(2100m, changed.ListPrice);
            Assert.Equal(0m, changed.AvailableQuantityRaw);
            Assert.Equal(AvailabilityStatus.Unknown, changed.Availability);
            Assert.Equal(2, await db.PriceObservations.CountAsync());
            await ingest.SaveAsync(Catalog(2100, 99999), context, PriceScope.OnlineGeneric, checkedAt.AddMinutes(10), CancellationToken.None);
            db.ChangeTracker.Clear();
            Assert.Equal(3, await db.PriceObservations.CountAsync()); // availability changed from UNKNOWN to IN_STOCK.
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Duplicate_product_rows_keep_distinct_skus_and_offers_together()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-duplicate-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var payload = Encoding.UTF8.GetBytes("""
                [{"productId":"1092","productName":"Arroz","items":[{"itemId":"small","name":"Arroz 500 G","sellers":[{"sellerId":"1","commertialOffer":{"Price":1990}}]}]},
                 {"productId":"1092","productName":"Arroz otra categoría","items":[{"itemId":"large","name":"Arroz 1 KG","sellers":[{"sellerId":"1","commertialOffer":{"Price":3800}}]}]}]
                """);
            var ingest = new D1CatalogIngest(db, new RawStore(paths, db));
            Assert.Equal(2, await ingest.SaveAsync(payload, new ProviderContext("online-generic"),
                PriceScope.OnlineGeneric, DateTime.UtcNow, CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Single(await db.ExternalProducts.ToListAsync());
            Assert.Equal(2, await db.ExternalSkus.CountAsync());
            Assert.Equal(2, await db.Offers.CountAsync());
            Assert.Equal(2, await db.PriceObservations.CountAsync());
            Assert.Equal(new[] { "large", "small" }, await db.ExternalSkus.OrderBy(x => x.ExternalId)
                .Select(x => x.ExternalId).ToArrayAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Repeated_capture_preserves_history_and_failed_refresh_preserves_good_data()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var ingest = new D1CatalogIngest(db, new RawStore(paths, db));
            var context = new ProviderContext("online-generic");
            var now = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
            static byte[] Catalog(int price) => Encoding.UTF8.GetBytes("[{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1092\",\"name\":\"Arroz 500 G\",\"sellers\":[{\"sellerId\":\"1\",\"sellerName\":\"D1\",\"commertialOffer\":{\"Price\":" + price + ",\"IsAvailable\":true,\"AvailableQuantity\":99999}}]}]}]");
            Assert.Equal(1, await ingest.SaveAsync(Catalog(1990), context, PriceScope.OnlineGeneric, now, CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Equal(1, await ingest.SaveAsync(Catalog(1990), context, PriceScope.OnlineGeneric, now.AddHours(1), CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Equal(1, await ingest.SaveAsync(Catalog(2000), context, PriceScope.OnlineGeneric, now.AddHours(2), CancellationToken.None));
            db.ChangeTracker.Clear();
            await Assert.ThrowsAnyAsync<System.Text.Json.JsonException>(() => ingest.SaveAsync(Catalog(2100).AsSpan(0, 20).ToArray(), context, PriceScope.OnlineGeneric, now.AddHours(3), CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => ingest.SaveAsync(Catalog(2100), new ProviderContext("region"), PriceScope.Region, now, CancellationToken.None));
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => ingest.SaveAsync(Encoding.UTF8.GetBytes("[]"), context, PriceScope.OnlineGeneric, now.AddHours(4), CancellationToken.None));
            var drift = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Catalog(2100)).Replace("\"Price\":2100", "\"MissingPrice\":2100"));
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => ingest.SaveAsync(drift, context, PriceScope.OnlineGeneric, now.AddHours(4), CancellationToken.None));
            var malformedOffer = Encoding.UTF8.GetBytes("[{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1092\",\"name\":\"Arroz 500 G\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"Price\":2100}}]},{\"itemId\":\"other\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"Price\":2200}}]}]}]");
            // Both offers parse before transaction; a missing required price anywhere must preserve prior state.
            var brokenSecond = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(malformedOffer).Replace("\"Price\":2200", "\"MissingPrice\":2200"));
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => ingest.SaveAsync(brokenSecond, context, PriceScope.OnlineGeneric, now.AddHours(5), CancellationToken.None));
            var missingProductId = Encoding.UTF8.GetBytes("[{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1092\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"Price\":2100}}]}]},{\"productName\":\"Unknown\",\"items\":[]}]");
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => ingest.SaveAsync(missingProductId, context, PriceScope.OnlineGeneric, now.AddHours(5), CancellationToken.None));
            var missingSkuId = Encoding.UTF8.GetBytes("[{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1092\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"Price\":2100}}]},{\"name\":\"Unknown\"}]}]");
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => ingest.SaveAsync(missingSkuId, context, PriceScope.OnlineGeneric, now.AddHours(5), CancellationToken.None));
            var missingSellerId = Encoding.UTF8.GetBytes("[{\"productId\":\"1092\",\"productName\":\"Arroz\",\"items\":[{\"itemId\":\"1092\",\"sellers\":[{\"sellerId\":\"1\",\"commertialOffer\":{\"Price\":2100}},{\"commertialOffer\":{\"Price\":2000}}]}]}]");
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => ingest.SaveAsync(missingSellerId, context, PriceScope.OnlineGeneric, now.AddHours(5), CancellationToken.None));
            db.ChangeTracker.Clear();
            Assert.Equal(2000m, (await db.Offers.SingleAsync()).Price);
            Assert.Equal(now.AddHours(2), (await db.Offers.SingleAsync()).CheckedAt);
            Assert.Equal(new[] { 1990m, 2000m }, await db.PriceObservations.OrderBy(x => x.CheckedAt).Select(x => x.Price).ToArrayAsync());
            Assert.Equal(2, await db.RawCaptures.CountAsync());
            await ingest.SaveAsync(Catalog(1800), context, PriceScope.OnlineGeneric, now.AddMinutes(30), CancellationToken.None);
            db.ChangeTracker.Clear();
            Assert.Equal(2000m, (await db.Offers.SingleAsync()).Price);
            Assert.Equal(2, await db.PriceObservations.CountAsync());
            Assert.Equal(99999m, (await db.Offers.SingleAsync()).AvailableQuantityRaw);
            Assert.Equal(now.AddHours(2), (await db.ExternalProducts.SingleAsync()).UpdatedAt);
            Assert.Single(await db.ExternalSkus.ToListAsync());
            Assert.Single(await db.Sellers.ToListAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
