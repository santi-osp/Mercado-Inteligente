using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MercadoInteligente.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

public sealed class BackupArchiveTests
{
    private static DbContextOptions<MarketDb> Options(string file) =>
        new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder { DataSource = file, Pooling = false }.ToString()).Options;

    [Fact]
    public async Task Creates_self_contained_verified_archive_without_changing_legacy_backup()
    {
        var root = Path.Combine(Path.GetTempPath(), "archive-create-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            var payload = "archived RAW evidence"u8.ToArray();
            var capture = await new RawStore(paths, db).SaveAsync(payload);
            var service = new BackupService(paths, db);
            var legacy = await service.CreateAsync();
            var name = await service.CreateArchiveAsync();
            Assert.EndsWith(".zip", name);
            Assert.True(File.Exists(Path.Combine(paths.Backups, legacy)));
            using var zip = ZipFile.OpenRead(Path.Combine(paths.Backups, name));
            Assert.Equal(3, zip.Entries.Count);
            using var manifest = JsonDocument.Parse(zip.GetEntry("manifest.json")!.Open());
            Assert.Equal(1, manifest.RootElement.GetProperty("Version").GetInt32());
            Assert.Equal("database.sqlite", manifest.RootElement.GetProperty("DatabasePath").GetString());
            var record = Assert.Single(manifest.RootElement.GetProperty("Raw").EnumerateArray());
            var path = "raw/" + capture.Sha256[..2] + "/" + capture.Sha256 + ".json.gz";
            Assert.Equal(path, record.GetProperty("Path").GetString());
            var raw = zip.GetEntry(path)!;
            await using (var stream = raw.Open())
            await using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
            {
                using var content = new MemoryStream();
                await gzip.CopyToAsync(content);
                Assert.Equal(payload, content.ToArray());
            }
            await using (var stream = raw.Open())
            {
                var digest = await SHA256.HashDataAsync(stream);
                Assert.Equal(Convert.ToHexString(digest).ToLowerInvariant(), record.GetProperty("FileSha256").GetString());
            }
            File.Delete(Path.Combine(paths.Raw, capture.RelativePath));
            await Assert.ThrowsAsync<ArgumentException>(() => service.RestoreAsync(name)); // no archive restore on legacy method
            Assert.True(File.Exists(Path.Combine(paths.Backups, name)));
            Assert.True(!Directory.GetFiles(paths.Backups, ".archive-*").Any(), string.Join(", ", Directory.GetFiles(paths.Backups, ".archive-*")));

            // Separate RestoreArchiveAsync restores the deleted RAW and restores the database
            await service.RestoreArchiveAsync(name);
            Assert.True(File.Exists(Path.Combine(paths.Raw, capture.RelativePath)));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RestoreArchive_restores_cleanly_into_fresh_root()
    {
        var rootA = Path.Combine(Path.GetTempPath(), "archive-rootA-" + Guid.NewGuid().ToString("N"));
        var rootB = Path.Combine(Path.GetTempPath(), "archive-rootB-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pathsA = new LocalPaths(rootA);
            await using var dbA = new MarketDb(Options(pathsA.Database));
            await dbA.Database.MigrateAsync();
            var payload = "fresh root evidence"u8.ToArray();
            var capture = await new RawStore(pathsA, dbA).SaveAsync(payload);
            var serviceA = new BackupService(pathsA, dbA);
            var name = await serviceA.CreateArchiveAsync();

            var pathsB = new LocalPaths(rootB);
            File.Copy(Path.Combine(pathsA.Backups, name), Path.Combine(pathsB.Backups, name));
            Assert.False(File.Exists(pathsB.Database));
            Assert.Empty(Directory.EnumerateFiles(pathsB.Raw, "*", SearchOption.AllDirectories));

            await using var dbB = new MarketDb(Options(pathsB.Database));
            var serviceB = new BackupService(pathsB, dbB);
            await serviceB.RestoreArchiveAsync(name);

            Assert.True(File.Exists(pathsB.Database));
            var rawFile = Path.Combine(pathsB.Raw, capture.RelativePath);
            Assert.True(File.Exists(rawFile));
            await using (var stream = File.OpenRead(rawFile))
            await using (var gzip = new GZipStream(stream, CompressionMode.Decompress))
            {
                using var content = new MemoryStream();
                await gzip.CopyToAsync(content);
                Assert.Equal(payload, content.ToArray());
            }

            await using var checkDb = new MarketDb(Options(pathsB.Database));
            var restoredCaptures = await checkDb.RawCaptures.ToListAsync();
            Assert.Single(restoredCaptures);
            Assert.Equal(capture.Sha256, restoredCaptures[0].Sha256);
            Assert.Empty(Directory.GetFiles(pathsB.Backups, "pre-restore-*"));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(rootA)) Directory.Delete(rootA, true);
            if (Directory.Exists(rootB)) Directory.Delete(rootB, true);
        }
    }

    [Fact]
    public async Task RestoreArchive_creates_preventive_backup_when_live_database_exists()
    {
        var root = Path.Combine(Path.GetTempPath(), "archive-preventive-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            var capture = await new RawStore(paths, db).SaveAsync("initial evidence"u8.ToArray());
            var service = new BackupService(paths, db);
            var name = await service.CreateArchiveAsync();

            // Mutate live database
            db.Settings.Add(new MercadoInteligente.Domain.AppSettings { MaxStores = 4 });
            await db.SaveChangesAsync();

            await service.RestoreArchiveAsync(name);

            // A pre-restore-*.db must have been created
            var rollbacks = Directory.GetFiles(paths.Backups, "pre-restore-*.db");
            Assert.Single(rollbacks);

            // Check restored database state
            await using var checkDb = new MarketDb(Options(paths.Database));
            var settings = await checkDb.Settings.FirstOrDefaultAsync();
            Assert.True(settings is null || settings.MaxStores != 4);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RestoreArchive_fails_closed_and_leaves_live_db_intact_if_local_raw_is_corrupt()
    {
        var root = Path.Combine(Path.GetTempPath(), "archive-corrupt-local-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            var capture = await new RawStore(paths, db).SaveAsync("evidence before corruption"u8.ToArray());
            var service = new BackupService(paths, db);
            var name = await service.CreateArchiveAsync();

            // Corrupt the local RAW file
            var localRaw = Path.Combine(paths.Raw, capture.RelativePath);
            await File.WriteAllTextAsync(localRaw, "corrupted content");

            // Add marker to live database
            db.Settings.Add(new MercadoInteligente.Domain.AppSettings { MaxStores = 3 });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreArchiveAsync(name));

            // Verify live DB is intact and has our marker
            await using var checkDb = new MarketDb(Options(paths.Database));
            var settings = await checkDb.Settings.SingleOrDefaultAsync(x => x.MaxStores == 3);
            Assert.NotNull(settings);

            // Archive was preserved in backups
            Assert.True(File.Exists(Path.Combine(paths.Backups, name)));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task RestoreArchive_fails_on_corrupt_archive_and_preserves_live_db()
    {
        var root = Path.Combine(Path.GetTempPath(), "archive-corrupt-file-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            db.Settings.Add(new MercadoInteligente.Domain.AppSettings { MaxStores = 2 });
            await db.SaveChangesAsync();

            var badName = "market-20261007-120000-000-deadbeef.zip";
            await File.WriteAllTextAsync(Path.Combine(paths.Backups, badName), "not a zip");

            var service = new BackupService(paths, db);
            await Assert.ThrowsAnyAsync<Exception>(() => service.RestoreArchiveAsync(badName));

            // Verify live DB is untouched
            await using var checkDb = new MarketDb(Options(paths.Database));
            var settings = await checkDb.Settings.SingleOrDefaultAsync(x => x.MaxStores == 2);
            Assert.NotNull(settings);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_or_corrupted_raw_never_publishes_archive(bool corrupt)
    {
        var root = Path.Combine(Path.GetTempPath(), "archive-invalid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            var capture = await new RawStore(paths, db).SaveAsync("evidence"u8.ToArray());
            var raw = Path.Combine(paths.Raw, capture.RelativePath);
            if (corrupt) await File.WriteAllTextAsync(raw, "invalid gzip");
            else File.Delete(raw);
            await Assert.ThrowsAsync<InvalidDataException>(() => new BackupService(paths, db).CreateArchiveAsync());
            Assert.Empty(Directory.EnumerateFileSystemEntries(paths.Backups));
            Assert.Single(await db.RawCaptures.ToListAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task Cancellation_does_not_publish_or_leave_staging()
    {
        var root = Path.Combine(Path.GetTempPath(), "archive-cancel-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new BackupService(paths, db).CreateArchiveAsync(cancellation.Token));
            Assert.Empty(Directory.EnumerateFileSystemEntries(paths.Backups));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
