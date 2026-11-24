using System.Net;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MercadoInteligente.Tests;

public sealed class StartupMigrationTests
{
    private static DbContextOptions<MarketDb> Options(string file) =>
        new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
        { DataSource = file, ForeignKeys = true, DefaultTimeout = 10 }.ToString()).Options;

    private static async Task<object?> ScalarAsync(string file, string sql)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = file, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    [Fact]
    public async Task FreshStartupEnablesWalAndDoesNotCreateUnneededBackup()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-startup-fresh-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root)))
            using (var client = factory.CreateClient())
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/system/health")).StatusCode);
            Assert.Equal("wal", await ScalarAsync(paths.Database, "PRAGMA journal_mode"));
            Assert.Equal(1L, await ScalarWithForeignKeysAsync(paths.Database));
            Assert.Empty(Directory.GetFiles(paths.Backups, "*.db"));
            await using var db = new MarketDb(Options(paths.Database));
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static async Task<long> ScalarWithForeignKeysAsync(string file)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = file, ForeignKeys = true, Pooling = false }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task OldSchemaIsBackedUpBeforeMigrationAndPreservesRows()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-startup-old-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using (var old = new MarketDb(Options(paths.Database)))
            {
                await old.GetService<IMigrator>().MigrateAsync("20261007042158_Initial");
                old.Retailers.Add(new Retailer { Code = "BEFORE", Name = "Preserved" });
                await old.SaveChangesAsync();
            }
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root)))
            using (var client = factory.CreateClient())
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/system/health")).StatusCode);
            var backup = Assert.Single(Directory.GetFiles(paths.Backups, "*.db"));
            Assert.Equal("wal", await ScalarAsync(paths.Database, "PRAGMA journal_mode"));
            await using var current = new MarketDb(Options(paths.Database));
            await using var snapshot = new MarketDb(Options(backup));
            Assert.Empty(await current.Database.GetPendingMigrationsAsync());
            Assert.Equal(current.Database.GetMigrations().Count() - 1,
                (await snapshot.Database.GetPendingMigrationsAsync()).Count());
            Assert.Equal("BEFORE", (await current.Retailers.SingleAsync()).Code);
            Assert.Equal("BEFORE", (await snapshot.Retailers.SingleAsync()).Code);
            Assert.Equal("ok", await new BackupService(paths, snapshot).IntegrityAsync());
            Assert.Equal(0L, await ScalarAsync(paths.Database,
                "SELECT COUNT(*) FROM pragma_table_info('Settings') WHERE name = 'AiEnabled'"));
            Assert.Equal(1L, await ScalarAsync(backup,
                "SELECT COUNT(*) FROM pragma_table_info('Settings') WHERE name = 'AiEnabled'"));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task InvalidOldRowsStopMigrationAndLeaveLiveDatabaseUntouched()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-startup-invalid-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using (var old = new MarketDb(Options(paths.Database)))
            {
                await old.GetService<IMigrator>().MigrateAsync("20261007042158_Initial");
                var retailer = new Retailer { Code = "OLD", Name = "Older" };
                var product = new ExternalProduct { RetailerId = retailer.Id, ExternalId = "p", Name = "Product" };
                var sku = new ExternalSku { ExternalProductId = product.Id, ExternalId = "s", Name = "Sku" };
                var seller = new Seller { RetailerId = retailer.Id, ExternalId = "v", Name = "Seller" };
                await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Retailers (Id, Code, Name) VALUES ({retailer.Id}, {retailer.Code}, {retailer.Name})");
                await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ExternalProducts (Id, RetailerId, ExternalId, Name, UpdatedAt) VALUES ({product.Id}, {retailer.Id}, {product.ExternalId}, {product.Name}, {DateTime.UtcNow})");
                await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ExternalSkus (Id, ExternalProductId, ExternalId, Name, Dimension) VALUES ({sku.Id}, {product.Id}, {sku.ExternalId}, {sku.Name}, {0})");
                await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Sellers (Id, RetailerId, ExternalId, Name, Kind) VALUES ({seller.Id}, {retailer.Id}, {seller.ExternalId}, {seller.Name}, {0})");
                await old.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Offers (Id, ExternalSkuId, SellerId, Context, Price, Currency, Scope, Availability, CheckedAt) VALUES ({Guid.NewGuid()}, {sku.Id}, {seller.Id}, {"online"}, {"0"}, {"COP"}, {0}, {0}, {DateTime.UtcNow})");
            }
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            var failure = await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                using var client = factory.CreateClient();
                await client.GetAsync("/api/v1/system/health");
            });
            Assert.Contains("Database startup failed", failure.ToString());
            Assert.Contains("NOT been automatically restored", failure.ToString());
            var backup = Assert.Single(Directory.GetFiles(paths.Backups, "*.db"));
            await using var live = new MarketDb(Options(paths.Database));
            await using var copy = new MarketDb(Options(backup));
            Assert.Equal(live.Database.GetMigrations().Count() - 1,
                (await live.Database.GetPendingMigrationsAsync()).Count());
            Assert.Equal("0", await ScalarAsync(paths.Database, "SELECT Price FROM Offers LIMIT 1"));
            Assert.Equal("0", await ScalarAsync(backup, "SELECT Price FROM Offers LIMIT 1"));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task ForeignKeyDamageStopsStartupBeforeBackupOrMigration()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-startup-fk-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using (var old = new MarketDb(Options(paths.Database)))
                await old.GetService<IMigrator>().MigrateAsync("20261007042158_Initial");
            await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, Pooling = false }.ToString()))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "PRAGMA foreign_keys=OFF; INSERT INTO Stores (Id, RetailerId, ExternalId, Name) VALUES ('invalid', 'missing', 'orphan', 'orphan')";
                await command.ExecuteNonQueryAsync();
            }
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            var failure = await Assert.ThrowsAnyAsync<Exception>(async () =>
            {
                using var client = factory.CreateClient();
                await client.GetAsync("/api/v1/system/health");
            });
            Assert.Contains("foreign_key_check failed", failure.ToString());
            Assert.Empty(Directory.GetFiles(paths.Backups, "*.db"));
            await using var live = new MarketDb(Options(paths.Database));
            Assert.Equal(live.Database.GetMigrations().Count() - 1,
                (await live.Database.GetPendingMigrationsAsync()).Count());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

}
