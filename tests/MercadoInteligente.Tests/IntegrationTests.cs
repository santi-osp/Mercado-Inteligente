using System.Net;
using System.Net.Http.Json;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

public sealed class DatabaseIntegrationTests
{
    private static DbContextOptions<MarketDb> Options(string file) =>
        new DbContextOptionsBuilder<MarketDb>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = file, ForeignKeys = true, DefaultTimeout = 10 }.ToString()).Options;

    [Fact]
    public async Task MigratedDatabaseEnforcesForeignKeysAndRecordsPriceHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-fk-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(Options(paths.Database));
            await db.Database.MigrateAsync();
            await db.Database.OpenConnectionAsync();
            await using (var pragma = db.Database.GetDbConnection().CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys";
                Assert.Equal(1L, await pragma.ExecuteScalarAsync());
            }
            await db.Database.CloseConnectionAsync();
            db.Stores.Add(new Store { RetailerId = Guid.NewGuid(), ExternalId = "orphan" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();

            var retailer = new Retailer { Code = "TEST", Name = "Test" };
            var product = new ExternalProduct { RetailerId = retailer.Id, ExternalId = "p1", Name = "Arroz" };
            var sku = new ExternalSku { ExternalProductId = product.Id, ExternalId = "sku1", Name = "Arroz 1 kg" };
            var seller = new Seller { RetailerId = retailer.Id, ExternalId = "directo", Name = "Directo" };
            var offer = new Offer { ExternalSkuId = sku.Id, SellerId = seller.Id, Price = 5000m, Context = "online" };
            db.AddRange(retailer, product, sku, seller, offer);
            await db.SaveChangesAsync();
            var first = new PriceObservation { OfferId = offer.Id, Price = 5000m, Availability = AvailabilityStatus.InStock, CheckedAt = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc) };
            var second = new PriceObservation { OfferId = offer.Id, Price = 4800m, Availability = AvailabilityStatus.OutOfStock, CheckedAt = first.CheckedAt.AddHours(1) };
            db.PriceObservations.AddRange(first, second);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var history = await db.PriceObservations.Where(x => x.OfferId == offer.Id).OrderBy(x => x.CheckedAt).ToListAsync();
            Assert.Equal(new[] { 5000m, 4800m }, history.Select(x => x.Price));
            Assert.Equal(AvailabilityStatus.OutOfStock, history[1].Availability);
            Assert.Equal(5000m, (await db.Offers.SingleAsync()).Price); // history does not silently overwrite the current offer
            db.PriceObservations.Add(new PriceObservation { OfferId = Guid.NewGuid(), Price = 100m });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            db.Offers.Add(new Offer { ExternalSkuId = sku.Id, SellerId = seller.Id, Price = 0m, Context = "invalid" });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            db.PriceObservations.Add(new PriceObservation { OfferId = offer.Id, Price = -1m });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task BackupRestoresToIndependentTemporaryDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-backup-" + Guid.NewGuid().ToString("N"));
        var restore = Path.Combine(Path.GetTempPath(), "market-restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using (var db = new MarketDb(Options(paths.Database)))
            {
                await db.Database.MigrateAsync();
                db.Settings.Add(new AppSettings { MaxStores = 4 });
                db.Retailers.Add(new Retailer { Code = "RESTORE", Name = "Before backup" });
                await db.SaveChangesAsync();
                var backup = new BackupService(paths, db);
                Assert.Equal("ok", await backup.IntegrityAsync());
                var name = await backup.CreateAsync();
                Assert.Equal(Path.GetFileName(name), name);
                Directory.CreateDirectory(restore);
                var restoredFile = Path.Combine(restore, "restored.db");
                File.Copy(Path.Combine(paths.Backups, name), restoredFile);
                db.Retailers.Add(new Retailer { Code = "LATER", Name = "After backup" });
                await db.SaveChangesAsync();
                await using var restored = new MarketDb(Options(restoredFile));
                Assert.Equal("ok", await new BackupService(new LocalPaths(restore), restored).IntegrityAsync());
                Assert.Equal(4, (await restored.Settings.SingleAsync()).MaxStores);
                Assert.Equal("RESTORE", (await restored.Retailers.SingleAsync()).Code);
                Assert.Equal(1, await restored.Retailers.CountAsync());
                Assert.Empty(await restored.Database.GetPendingMigrationsAsync());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(restore)) Directory.Delete(restore, true);
        }
    }
}

public sealed class ApiIntegrationTests
{
    [Fact]
    public async Task BasketOptimizationAcceptsBrowserTransportModeName()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-optimize-json-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();
            var basketResponse = await client.PostAsync("/api/v1/baskets", null);
            var basket = await basketResponse.Content.ReadFromJsonAsync<Basket>();
            Assert.NotNull(basket);
            var itemResponse = await client.PostAsJsonAsync($"/api/v1/baskets/{basket.Id}/items", new { query = "leche", quantity = 1 });
            Assert.Equal(HttpStatusCode.Created, itemResponse.StatusCode);

            var response = await client.PostAsJsonAsync($"/api/v1/baskets/{basket.Id}/optimize", new
            {
                originLatitude = 6.232177m,
                originLongitude = -75.5932585m,
                transportMode = "Driving",
                returnToOrigin = true,
                costPerKm = 2500m
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task BasketApiCreatesListsAndDeletesItemsWithValidation()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-basket-api-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root)))
            using (var client = factory.CreateClient())
            {
                var created = await client.PostAsync("/api/v1/baskets", null);
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
                var basket = await created.Content.ReadFromJsonAsync<Basket>();
                Assert.NotNull(basket);
                Assert.Equal($"/api/v1/baskets/{basket.Id}", created.Headers.Location?.ToString());

                var missingId = Guid.NewGuid();
                Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/baskets/{missingId}")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/v1/baskets/{missingId}/items", new { query = "Arroz", quantity = 1m })).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/baskets/{basket.Id}/items", new { query = "  ", quantity = 1m })).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/baskets/{basket.Id}/items", new { query = "Arroz", quantity = 0m })).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/baskets/{basket.Id}/items", new { query = "Arroz", quantity = -2m })).StatusCode);
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/baskets/{basket.Id}/items", new { query = "Arroz", quantity = 1m, productVariantId = Guid.NewGuid() })).StatusCode);

                var added = await client.PostAsJsonAsync($"/api/v1/baskets/{basket.Id}/items", new { query = "  Arroz  ", quantity = 2.5m, notes = "Integral" });
                Assert.Equal(HttpStatusCode.Created, added.StatusCode);
                var item = await added.Content.ReadFromJsonAsync<BasketItem>();
                Assert.NotNull(item);
                Assert.Equal(basket.Id, item.BasketId);
                Assert.Equal("Arroz", item.Query);
                Assert.Equal(2.5m, item.Quantity);
                Assert.Equal("Integral", item.Notes);
                Assert.Equal($"/api/v1/baskets/{basket.Id}/items/{item.Id}", added.Headers.Location?.ToString());
                var list = await client.GetFromJsonAsync<List<Basket>>("/api/v1/baskets");
                Assert.NotNull(list);
                Assert.Equal(item.Id, Assert.Single(Assert.Single(list).Items).Id);
                var fetched = await client.GetFromJsonAsync<Basket>($"/api/v1/baskets/{basket.Id}");
                Assert.Equal(item.Id, Assert.Single(fetched!.Items).Id);
                Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/baskets/{missingId}/items/{item.Id}")).StatusCode);
                Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/baskets/{basket.Id}/items/{item.Id}")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/baskets/{basket.Id}/items/{item.Id}")).StatusCode);
                Assert.Empty((await client.GetFromJsonAsync<Basket>($"/api/v1/baskets/{basket.Id}"))!.Items);
                Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/baskets/{basket.Id}")).StatusCode);
                Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/v1/baskets/{basket.Id}")).StatusCode);
                Assert.Empty((await client.GetFromJsonAsync<List<Basket>>("/api/v1/baskets"))!);
            }
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task HealthSettingsAndManualBackupUseIsolatedDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-api-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root)))
            using (var client = factory.CreateClient())
            {
                var health = await client.GetAsync("/api/v1/system/health");
                Assert.Equal(HttpStatusCode.OK, health.StatusCode);
                var healthBody = await health.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                Assert.Equal("ok", healthBody.GetProperty("database").GetString());
                Assert.True(healthBody.GetProperty("foreignKeysValid").GetBoolean());
                var unknown = await client.GetAsync("/api/v1/does-not-exist");
                Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
                Assert.Equal("NOT_FOUND", (await unknown.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("code").GetString());
                var invalid = await client.PutAsJsonAsync("/api/v1/settings", new AppSettings { MaxStores = 0 });
                Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
                var update = await client.PutAsJsonAsync("/api/v1/settings", new AppSettings { MaxStores = 4, AllowSubstitutes = false });
                Assert.Equal(HttpStatusCode.OK, update.StatusCode);
                var backup = await client.PostAsync("/api/v1/backups", null);
                Assert.Equal(HttpStatusCode.OK, backup.StatusCode);
                var file = (await backup.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("file").GetString();
                Assert.True(File.Exists(Path.Combine(root, "backups", file!)));
                var archiveResponse = await client.PostAsync("/api/v1/backups/archive", null);
                Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);
                var archiveName = (await archiveResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>()).GetProperty("file").GetString();
                Assert.Matches(@"\Amarket-[0-9]{8}-[0-9]{6}-[0-9]{3}-[0-9a-f]{8}\.zip\z", archiveName!);
                using var zip = System.IO.Compression.ZipFile.OpenRead(Path.Combine(root, "backups", archiveName!));
                Assert.Equal(new[] { "database.sqlite", "manifest.json" }, zip.Entries.Select(entry => entry.FullName).OrderBy(name => name));
            }
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root)))
            using (var client = factory.CreateClient())
            {
                var settings = await client.GetFromJsonAsync<AppSettings>("/api/v1/settings");
                Assert.NotNull(settings);
                Assert.Equal(4, settings.MaxStores);
                Assert.False(settings.AllowSubstitutes);
                Assert.Equal(4, settings.MaxStores);
            }
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
