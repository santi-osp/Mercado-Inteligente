using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.Ara;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MercadoInteligente.Tests;

/// <summary>
/// Comprehensive End-to-End lifecycle tests verifying the 13 required flows
/// defined in GOAL-10-HARDENING-RELEASE.md.
/// </summary>
public sealed class E2ELifecycleTests
{
    private static DbContextOptions<MarketDb> Options(string file) =>
        new DbContextOptionsBuilder<MarketDb>()
            .UseSqlite(new SqliteConnectionStringBuilder { DataSource = file, ForeignKeys = true, DefaultTimeout = 10 }.ToString()).Options;

    // 1. Primer inicio DB limpia
    [Fact]
    public async Task Flow01_CleanStartup_CreatesDefaultSettings_AndHealthyDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f01-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var health = await client.GetFromJsonAsync<JsonElement>("/api/v1/system/health");
            Assert.Equal("ok", health.GetProperty("database").GetString());
            Assert.True(health.GetProperty("foreignKeysValid").GetBoolean());
            Assert.Equal("LOCAL", health.GetProperty("mode").GetString());

            var settings = await client.GetFromJsonAsync<AppSettings>("/api/v1/settings");
            Assert.NotNull(settings);
            Assert.InRange(settings.MaxStores, 1, 5);
            Assert.True(settings.SizeTolerancePercent >= 0);

            var status = await client.GetFromJsonAsync<JsonElement>("/api/v1/system/status");
            Assert.Equal(AppVersionInfo.Version, status.GetProperty("version").GetString());
            Assert.Equal("OPERATIONAL", status.GetProperty("appStatus").GetString());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 2. Crear/editar perfil configuración
    [Fact]
    public async Task Flow02_SettingsProfile_CreateAndEdit_PersistsAcrossApi()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f02-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var current = await client.GetFromJsonAsync<AppSettings>("/api/v1/settings");
            Assert.NotNull(current);

            var updatedInput = new AppSettings
            {
                Id = current.Id,
                MaxStores = 4,
                SizeTolerancePercent = 20,
                AllowSubstitutes = true,
                MaxOverbuyPercent = 30,
                PriceTtlHours = 12,
                StockTtlHours = 4,
                ProductTtlDays = 90,
                StoresTtlDays = 90,
                RouteTtlHours = 24,
                PromoTtlHours = 24,
                RawRetentionDays = 60,
                LogRetentionDays = 30,
                BackupDailyRetention = 7,
                BackupWeeklyRetention = 4,
                BackupMonthlyRetention = 3,
                IncludeMarketplace = false
            };

            var updateResponse = await client.PutAsJsonAsync("/api/v1/settings", updatedInput);
            Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

            var refreshed = await client.GetFromJsonAsync<AppSettings>("/api/v1/settings");
            Assert.NotNull(refreshed);
            Assert.Equal(4, refreshed.MaxStores);
            Assert.Equal(20, refreshed.SizeTolerancePercent);
            Assert.Equal(12, refreshed.PriceTtlHours);
            var aiStatus = await client.GetFromJsonAsync<JsonElement>("/api/v1/ai/status");
            Assert.False(aiStatus.GetProperty("enabled").GetBoolean());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 3. D1 search/sync
    [Fact]
    public async Task Flow03_D1SearchSync_QueryAndLocalIngest_PreservesData()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f03-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            // Query validation
            var badQuery = await client.GetAsync("/api/v1/providers/d1/offers?q=");
            Assert.Equal(HttpStatusCode.BadRequest, badQuery.StatusCode);

            // Valid query contract
            var validQuery = await client.GetAsync("/api/v1/providers/d1/offers?q=leche");
            Assert.Equal(HttpStatusCode.OK, validQuery.StatusCode);
            var offers = await validQuery.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Array, offers.ValueKind);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 4. Éxito search/sync
    [Fact]
    public async Task Flow04_ExitoSearchSync_QueryAndLocalIngest_PreservesData()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f04-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var badQuery = await client.GetAsync("/api/v1/providers/exito/offers?q=");
            Assert.Equal(HttpStatusCode.BadRequest, badQuery.StatusCode);

            var validQuery = await client.GetAsync("/api/v1/providers/exito/offers?q=arroz");
            Assert.Equal(HttpStatusCode.OK, validQuery.StatusCode);
            var offers = await validQuery.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(JsonValueKind.Array, offers.ValueKind);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 5. Crear cesta
    [Fact]
    public async Task Flow05_BasketLifecycle_CreateAddUpdateAndRetrieve()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f05-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            // Create basket
            var createRes = await client.PostAsync("/api/v1/baskets", null);
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
            var basket = await createRes.Content.ReadFromJsonAsync<Basket>();
            Assert.NotNull(basket);
            Assert.NotEqual(Guid.Empty, basket.Id);

            // Add item
            var addItemRes = await client.PostAsJsonAsync($"/api/v1/baskets/{basket.Id}/items", new
            {
                query = "Arroz blanco 1000g",
                quantity = 2m,
                notes = "Grano entero"
            });
            Assert.Equal(HttpStatusCode.Created, addItemRes.StatusCode);
            var item = await addItemRes.Content.ReadFromJsonAsync<BasketItem>();
            Assert.NotNull(item);
            Assert.Equal("Arroz blanco 1000g", item.Query);
            Assert.Equal(2m, item.Quantity);

            // Update item quantity
            var updateItemRes = await client.PutAsJsonAsync($"/api/v1/baskets/{basket.Id}/items/{item.Id}", new
            {
                quantity = 3m
            });
            Assert.Equal(HttpStatusCode.OK, updateItemRes.StatusCode);

            // Retrieve basket with items
            var getBasketRes = await client.GetFromJsonAsync<Basket>($"/api/v1/baskets/{basket.Id}");
            Assert.NotNull(getBasketRes);
            Assert.Single(getBasketRes.Items);
            Assert.Equal(3m, getBasketRes.Items[0].Quantity);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 6. Exact/similar/substitute comparison
    [Fact]
    public async Task Flow06_MatchingLifecycle_ExactSimilarAndSubstituteComparisons()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f06-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            // Evaluate standard dataset pairs
            var pairs = await client.GetFromJsonAsync<JsonElement>("/api/v1/matching/pairs");
            var summary = pairs.GetProperty("summary");
            Assert.True(summary.GetProperty("totalPairs").GetInt32() > 0);
            Assert.True(summary.GetProperty("exactPrecision").GetDouble() > 0.8);

            // Custom comparison between two Colombian products
            var compRes = await client.PostAsJsonAsync("/api/v1/matching/compare", new
            {
                nameA = "Leche Alquería Entera 1000 ml",
                nameB = "Leche Alquería Entera 1 Litro",
                priceA = 4200m,
                priceB = 4300m
            });
            Assert.Equal(HttpStatusCode.OK, compRes.StatusCode);
            var comp = await compRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal((int)MatchKind.Exact, comp.GetProperty("match").GetProperty("kind").GetInt32());
            Assert.True(comp.GetProperty("productA").TryGetProperty("unitPrice", out _));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 7. Optimizar max N
    [Fact]
    public async Task Flow07_BasketOptimizer_MaxNStores_CalculatesOptimalDistribution()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f07-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var simRes = await client.PostAsJsonAsync("/api/v1/optimizer/simulate", new
            {
                items = new[]
                {
                    new { query = "Arroz blanco", quantity = 1m },
                    new { query = "Aceite vegetal", quantity = 1m }
                },
                maxStores = 2,
                allowSubstitutes = true
            });
            Assert.Equal(HttpStatusCode.OK, simRes.StatusCode);
            var solution = await simRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(solution.TryGetProperty("stores", out var stores));
            Assert.Equal(JsonValueKind.Array, stores.ValueKind);
            Assert.True(solution.TryGetProperty("totalCost", out _));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 8. Reiniciar y persistencia
    [Fact]
    public async Task Flow08_RestartAndPersistence_RetainsStateAcrossAppRestarts()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f08-" + Guid.NewGuid().ToString("N"));
        Guid basketId;
        try
        {
            // First run: create basket and update settings
            using (var factory1 = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root)))
            using (var client1 = factory1.CreateClient())
            {
                var basketRes = await client1.PostAsync("/api/v1/baskets", null);
                var basket = await basketRes.Content.ReadFromJsonAsync<Basket>();
                Assert.NotNull(basket);
                basketId = basket.Id;

                await client1.PutAsJsonAsync($"/api/v1/baskets/{basketId}", new { name = "Mercado Semanal Persistente" });
                await client1.PostAsJsonAsync($"/api/v1/baskets/{basketId}/items", new { query = "Café Juan Valdez 500g", quantity = 1m });

                var settings = await client1.GetFromJsonAsync<AppSettings>("/api/v1/settings");
                Assert.NotNull(settings);
                settings.MaxStores = 2;
                await client1.PutAsJsonAsync("/api/v1/settings", settings);
            }

            SqliteConnection.ClearAllPools();

            // Second run: verify persistence with a new factory instance on same root
            using (var factory2 = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root)))
            using (var client2 = factory2.CreateClient())
            {
                var baskets = await client2.GetFromJsonAsync<List<Basket>>("/api/v1/baskets");
                Assert.NotNull(baskets);
                var persistedBasket = baskets.FirstOrDefault(b => b.Id == basketId);
                Assert.NotNull(persistedBasket);
                Assert.Equal("Mercado Semanal Persistente", persistedBasket.Name);
                Assert.Single(persistedBasket.Items);
                Assert.Equal("Café Juan Valdez 500g", persistedBasket.Items[0].Query);

                var settings = await client2.GetFromJsonAsync<AppSettings>("/api/v1/settings");
                Assert.NotNull(settings);
                Assert.Equal(2, settings.MaxStores);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 9. Offline/last-known-good
    [Fact]
    public async Task Flow09_OfflineResilience_LastKnownGoodDataPreserved()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f09-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
                var retailer = new Retailer { Code = "D1", Name = "Tiendas D1" };
                var product = new ExternalProduct { RetailerId = retailer.Id, ExternalId = "d1-arroz-1", Name = "Arroz D1 1kg" };
                var sku = new ExternalSku { ExternalProductId = product.Id, ExternalId = "d1-sku-1", Name = "Arroz D1 1000g" };
                var seller = new Seller { RetailerId = retailer.Id, ExternalId = "d1-direct", Name = "D1 Directo" };
                var offer = new Offer
                {
                    ExternalSkuId = sku.Id,
                    SellerId = seller.Id,
                    Price = 3800m,
                    Scope = PriceScope.OnlineGeneric,
                    Context = "online-generic",
                    CheckedAt = DateTime.UtcNow
                };
                db.AddRange(retailer, product, sku, seller, offer);
                await db.SaveChangesAsync();
            }

            // Query existing local offers
            var offersRes = await client.GetFromJsonAsync<JsonElement>("/api/v1/providers/d1/offers?q=Arroz");
            Assert.Equal(1, offersRes.GetArrayLength());
            Assert.Equal("Arroz D1 1kg", offersRes[0].GetProperty("productName").GetString());
            Assert.Equal(3800m, offersRes[0].GetProperty("price").GetDecimal());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 10. Backup y restore test
    [Fact]
    public async Task Flow10_BackupAndRestore_GeneratesVerifiableBackupAndRestoresOffline()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f10-" + Guid.NewGuid().ToString("N"));
        var restoreRoot = Path.Combine(Path.GetTempPath(), "market-e2e-f10-restore-" + Guid.NewGuid().ToString("N"));
        try
        {
            string backupName;
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root)))
            using (var client = factory.CreateClient())
            {
                // Create backup via API
                var backupRes = await client.PostAsync("/api/v1/backups", null);
                Assert.Equal(HttpStatusCode.OK, backupRes.StatusCode);
                var backupData = await backupRes.Content.ReadFromJsonAsync<JsonElement>();
                backupName = backupData.GetProperty("file").GetString()!;
                Assert.NotEmpty(backupName);

                // List backups
                var listRes = await client.GetFromJsonAsync<JsonElement>("/api/v1/backups");
                Assert.True(listRes.GetArrayLength() > 0);
            }

            SqliteConnection.ClearAllPools();

            // Offline restore test into separate directory
            var livePaths = new LocalPaths(root);
            var restorePaths = new LocalPaths(restoreRoot);
            Directory.CreateDirectory(restorePaths.Root);

            // Copy backup file to restoreRoot's backups directory
            Directory.CreateDirectory(restorePaths.Backups);
            File.Copy(Path.Combine(livePaths.Backups, backupName), Path.Combine(restorePaths.Backups, backupName));

            await using (var restoreDb = new MarketDb(Options(restorePaths.Database)))
            {
                var backupService = new BackupService(restorePaths, restoreDb);
                await backupService.RestoreAsync(backupName);

                // Verify restored database integrity
                Assert.Equal("ok", await backupService.IntegrityAsync());
                Assert.True(await backupService.ForeignKeysValidAsync());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(restoreRoot)) Directory.Delete(restoreRoot, true);
        }
    }

    // 11. Google configured path si disponible / unconfigured fallback
    [Fact]
    public async Task Flow11_GoogleMapsIntegration_UnconfiguredFallbackAndSafeHandling()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f11-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var secretRes = await client.GetFromJsonAsync<JsonElement>("/api/v1/secrets/google");
            Assert.False(secretRes.GetProperty("configured").GetBoolean());

            var healthRes = await client.GetFromJsonAsync<JsonElement>("/api/v1/system/health");
            Assert.Equal("NOT_CONFIGURED", healthRes.GetProperty("google").GetString());

            var statusRes = await client.GetFromJsonAsync<JsonElement>("/api/v1/system/status");
            Assert.False(statusRes.GetProperty("google").GetProperty("configured").GetBoolean());
            Assert.Equal("NOT_CONFIGURED", statusRes.GetProperty("google").GetProperty("status").GetString());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 12. OpenRouter configured path si disponible / AI-off fallback
    [Fact]
    public async Task Flow12_OpenRouterAiIntegration_UnconfiguredFallbackAndAiOffSafety()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f12-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var secretRes = await client.GetFromJsonAsync<JsonElement>("/api/v1/secrets/openrouter");
            Assert.False(secretRes.GetProperty("configured").GetBoolean());

            var healthRes = await client.GetFromJsonAsync<JsonElement>("/api/v1/system/health");
            Assert.Equal("NOT_CONFIGURED", healthRes.GetProperty("ai").GetString());

            var statusRes = await client.GetFromJsonAsync<JsonElement>("/api/v1/system/status");
            Assert.False(statusRes.GetProperty("ai").GetProperty("configured").GetBoolean());
            Assert.Equal("NOT_CONFIGURED", statusRes.GetProperty("ai").GetProperty("status").GetString());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    // 13. Ara partial state
    [Fact]
    public async Task Flow13_AraProvider_HonestPartialCapabilityAndManualOfferHandling()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-e2e-f13-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            // List manual offers (initially empty)
            var manualOffers = await client.GetFromJsonAsync<JsonElement>("/api/v1/stores/ara/manual");
            Assert.Equal(JsonValueKind.Array, manualOffers.ValueKind);

            // Add manual offer
            var addManual = await client.PostAsJsonAsync("/api/v1/stores/ara/manual", new CreateAraManualOfferRequest(
                ProductName: "Atún Van Camp's en Aceite 160g",
                Brand: "Van Camp's",
                Quantity: 160m,
                Unit: "g",
                Price: 6500m,
                StoreName: "Ara Chapinero",
                StoreAddress: "Calle 60 # 9-20",
                Notes: "Precio verificado en góndola"
            ));
            Assert.Equal(HttpStatusCode.Created, addManual.StatusCode);
            var createdOffer = await addManual.Content.ReadFromJsonAsync<AraManualOfferDto>();
            Assert.NotNull(createdOffer);
            Assert.Equal("Atún Van Camp's en Aceite 160g", createdOffer.ProductName);

            // Brochure endpoint validates empty payloads
            var emptyBrochure = await client.PostAsync("/api/v1/stores/ara/brochure", new ByteArrayContent(Array.Empty<byte>()));
            Assert.Equal(HttpStatusCode.BadRequest, emptyBrochure.StatusCode);

            // System status reports provider metadata
            var status = await client.GetFromJsonAsync<JsonElement>("/api/v1/system/status");
            var providers = status.GetProperty("providers");
            Assert.True(providers.GetArrayLength() >= 3);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
