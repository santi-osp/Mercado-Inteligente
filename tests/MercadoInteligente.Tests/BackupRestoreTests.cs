using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

public sealed class BackupRestoreTests
{
    private static DbContextOptions<MarketDb> Options(string file) =>
        new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false, DefaultTimeout = 10 }.ToString()).Options;

    [Fact]
    public async Task RestoresSnapshotAndKeepsPreventiveBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), "restore-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            db.Retailers.Add(new Retailer { Code = "BEFORE", Name = "Before" });
            await db.SaveChangesAsync();
            var service = new BackupService(paths, db);
            var name = await service.CreateAsync();
            db.Retailers.Add(new Retailer { Code = "AFTER", Name = "After" });
            await db.SaveChangesAsync();
            await service.RestoreAsync(name);
            await using var restored = new MarketDb(Options(paths.Database));
            Assert.Equal("BEFORE", (await restored.Retailers.SingleAsync()).Code);
            Assert.Equal("ok", await new BackupService(paths, restored).IntegrityAsync());
            Assert.Single(Directory.GetFiles(paths.Backups, "pre-restore-*.db"));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Missing_raw_rejects_new_snapshot_before_publishing_backup()
    {
        var root = Path.Combine(Path.GetTempPath(), "backup-raw-missing-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            var capture = await new RawStore(paths, db).SaveAsync("backup evidence"u8.ToArray());
            File.Delete(Path.Combine(paths.Raw, capture.RelativePath));
            await Assert.ThrowsAsync<InvalidDataException>(() => new BackupService(paths, db).CreateAsync());
            Assert.Empty(Directory.GetFiles(paths.Backups));
            Assert.Single(await db.RawCaptures.ToListAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Corrupt_raw_rejects_snapshot_and_removes_staging_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "backup-raw-corrupt-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            var capture = await new RawStore(paths, db).SaveAsync("backup evidence"u8.ToArray());
            await File.WriteAllTextAsync(Path.Combine(paths.Raw, capture.RelativePath), "invalid gzip");
            await Assert.ThrowsAsync<InvalidDataException>(() => new BackupService(paths, db).CreateAsync());
            Assert.Empty(Directory.EnumerateFileSystemEntries(paths.Backups));
            Assert.Single(await db.RawCaptures.ToListAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_corrupted_raw_blocks_restore_before_live_database_changes(bool corrupt)
    {
        var root = Path.Combine(Path.GetTempPath(), "restore-raw-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            var capture = await new RawStore(paths, db).SaveAsync("restore evidence"u8.ToArray());
            db.Retailers.Add(new Retailer { Code = "BEFORE", Name = "Before" });
            await db.SaveChangesAsync();
            var service = new BackupService(paths, db);
            var backup = await service.CreateAsync();
            db.Retailers.Add(new Retailer { Code = "AFTER", Name = "After" });
            await db.SaveChangesAsync();
            var raw = Path.Combine(paths.Raw, capture.RelativePath);
            if (corrupt) await File.WriteAllTextAsync(raw, "not gzip");
            else File.Delete(raw);
            await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreAsync(backup));
            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.Retailers.CountAsync());
            Assert.Empty(Directory.GetFiles(paths.Backups, "pre-restore-*.db"));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Matching_raw_allows_restore()
    {
        var root = Path.Combine(Path.GetTempPath(), "restore-raw-valid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            var capture = await new RawStore(paths, db).SaveAsync("restore evidence"u8.ToArray());
            db.Retailers.Add(new Retailer { Code = "BEFORE", Name = "Before" });
            await db.SaveChangesAsync();
            var service = new BackupService(paths, db);
            var backup = await service.CreateAsync();
            db.Retailers.Add(new Retailer { Code = "AFTER", Name = "After" });
            await db.SaveChangesAsync();
            await service.RestoreAsync(backup);
            await using var restored = new MarketDb(Options(paths.Database));
            Assert.Equal("BEFORE", (await restored.Retailers.SingleAsync()).Code);
            Assert.True(File.Exists(Path.Combine(paths.Raw, capture.RelativePath)));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RejectsTraversalAndInvalidBackupWithoutChangingDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), "restore-invalid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            db.Retailers.Add(new Retailer { Code = "SAFE", Name = "Safe" });
            await db.SaveChangesAsync();
            var service = new BackupService(paths, db);
            await Assert.ThrowsAsync<ArgumentException>(() => service.RestoreAsync("../market.db"));
            var badName = "market-20261007-123456-789-abcdef12.db";
            await File.WriteAllTextAsync(Path.Combine(paths.Backups, badName), "not sqlite");
            await Assert.ThrowsAnyAsync<Exception>(() => service.RestoreAsync(badName));
            Assert.Equal("SAFE", (await db.Retailers.SingleAsync()).Code);
            Assert.Empty(Directory.GetFiles(paths.Backups, "pre-restore-*.db"));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
