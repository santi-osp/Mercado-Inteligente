using System.Security.Cryptography;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

public class RawLifecycleTests
{
    [Fact]
    public async Task Cleanup_removes_old_orphan_but_keeps_referenced_recent_and_foreign_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-raw-clean-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var store = new RawStore(paths, db);
            var referenced = await store.SaveAsync("good"u8.ToArray());
            var referencedPath = Path.Combine(paths.Raw, referenced.RelativePath);
            File.SetLastWriteTimeUtc(referencedPath, DateTime.UtcNow.AddDays(-100));

            static string RawPath(LocalPaths paths, byte[] bytes)
            {
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                var folder = Path.Combine(paths.Raw, hash[..2]);
                Directory.CreateDirectory(folder);
                return Path.Combine(folder, hash + ".json.gz");
            }
            var old = RawPath(paths, "orphan"u8.ToArray());
            await File.WriteAllTextAsync(old, "orphan bytes");
            File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-90));
            var recent = RawPath(paths, "new orphan"u8.ToArray());
            await File.WriteAllTextAsync(recent, "recent bytes");
            var foreign = Path.Combine(paths.Raw, "notes.txt");
            await File.WriteAllTextAsync(foreign, "unrelated");

            Assert.Equal(1, await store.CleanupOrphansAsync(DateTime.UtcNow.AddDays(-60)));
            Assert.False(File.Exists(old));
            Assert.True(File.Exists(referencedPath));
            Assert.True(File.Exists(recent));
            Assert.True(File.Exists(foreign));
            Assert.Single(await db.RawCaptures.ToListAsync());
            Assert.Equal(0, await store.CleanupOrphansAsync(DateTime.UtcNow.AddDays(-60)));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Expired_unreferenced_capture_is_removed_without_touching_price_evidence()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-raw-retention-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var store = new RawStore(paths, db);
            var orphan = await store.SaveAsync("unreferenced"u8.ToArray());
            var referenced = await store.SaveAsync("referenced"u8.ToArray());
            var recent = await store.SaveAsync("recent"u8.ToArray());
            var retailer = new Retailer { Code = "D1", Name = "D1" };
            var product = new ExternalProduct { RetailerId = retailer.Id, ExternalId = "p", Name = "Product" };
            var sku = new ExternalSku { ExternalProductId = product.Id, ExternalId = "s", Name = "Sku" };
            var seller = new Seller { RetailerId = retailer.Id, ExternalId = "1", Name = "D1" };
            var offer = new Offer { ExternalSkuId = sku.Id, SellerId = seller.Id, Context = "online-generic",
                Scope = PriceScope.OnlineGeneric, Price = 1990m, RawCaptureId = recent.Id };
            db.AddRange(retailer, product, sku, seller, offer);
            db.PriceObservations.Add(new PriceObservation { OfferId = offer.Id, Price = 1990m, RawCaptureId = referenced.Id });
            await db.SaveChangesAsync();
            await db.RawCaptures.Where(x => x.Id == orphan.Id || x.Id == referenced.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.CapturedAt, DateTime.UtcNow.AddDays(-90)));
            db.ChangeTracker.Clear();
            Assert.Equal(1, await store.CleanupExpiredUnreferencedAsync(DateTime.UtcNow.AddDays(-60)));
            Assert.False(File.Exists(Path.Combine(paths.Raw, orphan.RelativePath)));
            Assert.True(File.Exists(Path.Combine(paths.Raw, referenced.RelativePath)));
            Assert.True(File.Exists(Path.Combine(paths.Raw, recent.RelativePath)));
            Assert.Equal(2, await db.RawCaptures.CountAsync());
            Assert.Equal(1, await db.PriceObservations.CountAsync());
            Assert.Equal(0, await store.CleanupExpiredUnreferencedAsync(DateTime.UtcNow.AddDays(-60)));
            await Assert.ThrowsAsync<ArgumentException>(() => store.CleanupExpiredUnreferencedAsync(DateTime.Now));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Sqlite_snapshot_blocks_both_raw_cleanup_paths()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-raw-backup-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var store = new RawStore(paths, db);
            var capture = await store.SaveAsync("backup evidence"u8.ToArray());
            await db.RawCaptures.Where(x => x.Id == capture.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.CapturedAt, DateTime.UtcNow.AddDays(-90)));
            var file = Path.Combine(paths.Raw, capture.RelativePath);
            var backup = await new BackupService(paths, db).CreateAsync();
            Assert.True(File.Exists(Path.Combine(paths.Backups, backup)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.CleanupExpiredUnreferencedAsync(DateTime.UtcNow.AddDays(-60)));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.CleanupOrphansAsync(DateTime.UtcNow.AddDays(-60)));
            Assert.True(File.Exists(file));
            Assert.Single(await db.RawCaptures.ToListAsync());
            File.Move(Path.Combine(paths.Backups, backup), Path.Combine(paths.Backups, "snapshot.unknown"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.CleanupExpiredUnreferencedAsync(DateTime.UtcNow.AddDays(-60)));
            Assert.True(File.Exists(file));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cleanup_rejects_non_utc_cutoff()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-raw-cutoff-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await Assert.ThrowsAsync<ArgumentException>(() => new RawStore(paths, db).CleanupOrphansAsync(DateTime.Now));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
