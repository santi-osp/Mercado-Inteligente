using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.Ara;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MercadoInteligente.Tests;

public class AraProviderTests
{
    private static (MarketDb db, RawStore raw, string tempDir) CreateTestContext()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ara-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var dbPath = Path.Combine(tempDir, "test.db");
        var rawPath = Path.Combine(tempDir, "raw");
        Directory.CreateDirectory(rawPath);

        var options = new DbContextOptionsBuilder<MarketDb>()
            .UseSqlite($"Data Source={dbPath};Foreign Keys=True")
            .Options;

        var db = new MarketDb(options);
        db.Database.EnsureCreated();
        var paths = new LocalPaths(tempDir);
        var rawStore = new RawStore(paths, db);
        return (db, rawStore, tempDir);
    }

    private static void Cleanup(string tempDir)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(tempDir))
        {
            try { Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    [Fact]
    public void DeclaredCapabilities_AreHonestAndExplicit()
    {
        var caps = AraProvider.DeclaredCapabilities;
        Assert.Equal(CapabilityState.Partial, caps.Search);
        Assert.Equal(CapabilityState.Partial, caps.Details);
        Assert.Equal(CapabilityState.Partial, caps.Offers);
        Assert.Equal(CapabilityState.Partial, caps.Stores);
        Assert.Equal(CapabilityState.NotSupported, caps.Availability);
        Assert.Equal(CapabilityState.NotSupported, caps.CatalogEnumeration);
    }

    [Fact]
    public async Task EnumerateCatalogAsync_ThrowsNotSupportedException()
    {
        var (db, _, tempDir) = CreateTestContext();
        try
        {
            var provider = new AraProvider(db);
            var ctx = new ProviderContext("general");
            await Assert.ThrowsAsync<NotSupportedException>(() => provider.EnumerateCatalogAsync(ctx, CancellationToken.None));
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public async Task BrochureIngest_PersistsOffersAndRawAudit()
    {
        var (db, rawStore, tempDir) = CreateTestContext();
        try
        {
            var ingest = new AraBrochureIngest(db, rawStore);
            var brochure = new AraBrochure(
                CampaignName: "Rebajas de la Semana - Octubre 2026",
                ValidFrom: DateTime.UtcNow.AddDays(-1),
                ValidTo: DateTime.UtcNow.AddDays(6),
                Items:
                [
                    new AraBrochureItem(
                        ExternalId: "ara-promo-arroz-1k",
                        Name: "Arroz Diana Tradicional 1000g",
                        Brand: "Diana",
                        Quantity: 1000,
                        Unit: "g",
                        Price: 4390m,
                        ListPrice: 4990m,
                        Conditions: "Máximo 3 unidades por cliente",
                        Category: "Abarrotes"
                    ),
                    new AraBrochureItem(
                        ExternalId: "ara-promo-leche-1l",
                        Name: "Leche Entera Del Vecchio 1000ml",
                        Brand: "Del Vecchio",
                        Quantity: 1000,
                        Unit: "ml",
                        Price: 3200m,
                        ListPrice: null,
                        Conditions: null,
                        Category: "Lácteos"
                    )
                ]
            );

            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(brochure);
            var result = await ingest.IngestAsync(jsonBytes, CancellationToken.None);

            Assert.Equal("Rebajas de la Semana - Octubre 2026", result.CampaignName);
            Assert.Equal(2, result.ItemsReceived);
            Assert.Equal(2, result.OffersProcessed);
            Assert.Equal(2, result.ProductsCreated);
            Assert.True(result.IsActive);

            // Verify in DB
            var offers = await db.Offers.ToListAsync();
            Assert.Equal(2, offers.Count);
            Assert.All(offers, o =>
            {
                Assert.StartsWith("brochure:", o.Context);
                Assert.Equal(PriceScope.OnlineGeneric, o.Scope);
                Assert.Equal(AvailabilityStatus.InStock, o.Availability);
            });

            // Verify RawStore has the capture
            var captures = await db.RawCaptures.ToListAsync();
            Assert.NotEmpty(captures);
            Assert.All(captures, c =>
            {
                Assert.False(string.IsNullOrWhiteSpace(c.Sha256));
            });
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public async Task BrochureIngest_RejectsOversizedPayload()
    {
        var (db, rawStore, tempDir) = CreateTestContext();
        try
        {
            var ingest = new AraBrochureIngest(db, rawStore);
            var oversizedBytes = new byte[2 * 1024 * 1024 + 10]; // Exceeds 2 MB limit
            await Assert.ThrowsAsync<ArgumentException>(() => ingest.IngestAsync(oversizedBytes, CancellationToken.None));
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public async Task ManualOffer_CreatesVerifiedPhysicalOffer_WhenStoreProvided()
    {
        var (db, rawStore, tempDir) = CreateTestContext();
        try
        {
            var service = new AraManualOfferService(db, rawStore);
            var request = new CreateAraManualOfferRequest(
                ProductName: "Aceite Premier Girasol 1000ml",
                Brand: "Premier",
                Quantity: 1000,
                Unit: "ml",
                Price: 9500m,
                ListPrice: 10500m,
                StoreName: "Ara Belén Parque",
                ValidUntil: DateTime.UtcNow.AddDays(3),
                Notes: "Observado en góndola de entrada"
            );

            var dto = await service.AddManualOfferAsync(request, CancellationToken.None);
            Assert.NotEqual(Guid.Empty, dto.OfferId);
            Assert.Equal("Aceite Premier Girasol 1000ml", dto.ProductName);
            Assert.Equal("Ara Belén Parque", dto.StoreName);
            Assert.Equal(9500m, dto.Price);

            // Check offer in DB
            var dbOffer = await db.Offers.FirstOrDefaultAsync(o => o.Id == dto.OfferId);
            Assert.NotNull(dbOffer);
            Assert.Equal(PriceScope.PhysicalVerified, dbOffer.Scope);
            Assert.Equal("source:USER_MANUAL", dbOffer.Context);
            Assert.NotNull(dbOffer.StoreId);

            var dbStore = await db.Stores.FirstOrDefaultAsync(s => s.Id == dbOffer.StoreId);
            Assert.NotNull(dbStore);
            Assert.Equal("Ara Belén Parque", dbStore.Name);

            // Check PriceObservation was created
            var obs = await db.PriceObservations.FirstOrDefaultAsync(po => po.OfferId == dto.OfferId);
            Assert.NotNull(obs);
            Assert.Equal(9500m, obs.Price);
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public async Task ManualOffer_SetsScopeUnknown_WhenNoStoreProvided()
    {
        var (db, rawStore, tempDir) = CreateTestContext();
        try
        {
            var service = new AraManualOfferService(db, rawStore);
            var request = new CreateAraManualOfferRequest(
                ProductName: "Huevos AA x 30",
                Brand: "Santa Anita",
                Quantity: 30,
                Unit: "und",
                Price: 17500m
            );

            var dto = await service.AddManualOfferAsync(request, CancellationToken.None);
            var dbOffer = await db.Offers.FirstOrDefaultAsync(o => o.Id == dto.OfferId);
            Assert.NotNull(dbOffer);
            Assert.Equal(PriceScope.Unknown, dbOffer.Scope);
            Assert.Null(dbOffer.StoreId);
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public async Task ManualOffer_DeleteRemovesOfferAndObservations()
    {
        var (db, rawStore, tempDir) = CreateTestContext();
        try
        {
            var service = new AraManualOfferService(db, rawStore);
            var request = new CreateAraManualOfferRequest(
                ProductName: "Pan Tajado Bimbo Blanco",
                Brand: "Bimbo",
                Quantity: 450,
                Unit: "g",
                Price: 5800m
            );

            var created = await service.AddManualOfferAsync(request, CancellationToken.None);
            var listBefore = await service.GetManualOffersAsync(CancellationToken.None);
            Assert.Single(listBefore);

            var deleted = await service.DeleteManualOfferAsync(created.OfferId, CancellationToken.None);
            Assert.True(deleted);

            var listAfter = await service.GetManualOffersAsync(CancellationToken.None);
            Assert.Empty(listAfter);

            var observations = await db.PriceObservations.Where(po => po.OfferId == created.OfferId).ToListAsync();
            Assert.Empty(observations);
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public async Task AraHealthProbe_ReflectsAccurateCounts()
    {
        var (db, rawStore, tempDir) = CreateTestContext();
        try
        {
            var probe = new AraHealthProbe(db);
            var reportInit = await probe.CheckHealthAsync(CancellationToken.None);
            Assert.Equal("ARA", reportInit.Provider);
            Assert.Equal("PARTIAL_HONEST_BROCHURE_AND_MANUAL", reportInit.Status);
            Assert.Equal(0, reportInit.TotalActiveOffers);

            // Add 1 manual offer
            var manualService = new AraManualOfferService(db, rawStore);
            await manualService.AddManualOfferAsync(new CreateAraManualOfferRequest(
                ProductName: "Atún Van Camp's Agua",
                Brand: "Van Camp's",
                Quantity: 160,
                Unit: "g",
                Price: 6200m
            ), CancellationToken.None);

            var reportAfter = await probe.CheckHealthAsync(CancellationToken.None);
            Assert.Equal(1, reportAfter.TotalActiveOffers);
            Assert.Equal(1, reportAfter.ManualOffersCount);
            Assert.Equal(0, reportAfter.BrochureOffersCount);
            Assert.NotNull(reportAfter.LastOfferUpdate);
        }
        finally
        {
            Cleanup(tempDir);
        }
    }

    [Fact]
    public async Task ApiEndpoints_ExposeRegistryAndAraCapabilities()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-ara-api-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            // 1. GET /api/v1/providers
            var providersResp = await client.GetAsync("/api/v1/providers");
            Assert.Equal(HttpStatusCode.OK, providersResp.StatusCode);
            var providers = await providersResp.Content.ReadFromJsonAsync<List<ProviderMetadata>>();
            Assert.NotNull(providers);
            var ara = providers.FirstOrDefault(p => p.Code == "ARA");
            Assert.NotNull(ara);
            Assert.Equal("Tiendas Ara", ara.Name);
            Assert.True(ara.SupportsManualOffers);
            Assert.Equal("PARTIAL_HONEST_BROCHURE_AND_MANUAL", ara.Status);

            // 2. GET /api/v1/stores/ara/health
            var healthResp = await client.GetAsync("/api/v1/stores/ara/health");
            Assert.Equal(HttpStatusCode.OK, healthResp.StatusCode);

            // 3. POST and GET /api/v1/stores/ara/manual
            var newOffer = new CreateAraManualOfferRequest(
                ProductName: "Café Sello Rojo 500g",
                Brand: "Sello Rojo",
                Quantity: 500,
                Unit: "g",
                Price: 12500m,
                StoreName: "Ara Laureles"
            );
            var postResp = await client.PostAsJsonAsync("/api/v1/stores/ara/manual", newOffer);
            Assert.Equal(HttpStatusCode.Created, postResp.StatusCode);
            var created = await postResp.Content.ReadFromJsonAsync<AraManualOfferDto>();
            Assert.NotNull(created);
            Assert.Equal("Café Sello Rojo 500g", created.ProductName);

            var getManualResp = await client.GetAsync("/api/v1/stores/ara/manual");
            Assert.Equal(HttpStatusCode.OK, getManualResp.StatusCode);
            var manuals = await getManualResp.Content.ReadFromJsonAsync<List<AraManualOfferDto>>();
            Assert.NotNull(manuals);
            Assert.Contains(manuals, m => m.OfferId == created.OfferId);

            // 4. DELETE /api/v1/stores/ara/manual/{id}
            var delResp = await client.DeleteAsync($"/api/v1/stores/ara/manual/{created.OfferId}");
            Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
