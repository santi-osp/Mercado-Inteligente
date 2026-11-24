using System.Net;
using System.Text;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using MercadoInteligente.Infrastructure.Stores.Exito;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MercadoInteligente.Tests;

public sealed class Goal13IntegrationCertificationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalPaths _paths;

    public Goal13IntegrationCertificationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "market_goal13_cert_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "data"));
        _paths = new LocalPaths(_tempDir);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task SyncAndOptimize_UseSameD1FulfillmentContext()
    {
        // 1. Arrange DB and LocationProfile
        var options = new DbContextOptionsBuilder<MarketDb>()
            .UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = _paths.Database,
                ForeignKeys = true,
                Pooling = false
            }.ToString()).Options;

        await using var db = new MarketDb(options);
        await db.Database.MigrateAsync();

        var profile = new LocationProfile
        {
            Label = "Casa Medellin",
            Address = "Carrera 43A # 1-50, Medellin",
            Latitude = 6.232177m,
            Longitude = -75.5932585m,
            PostalCode = "050030284",
            City = "Medellin",
            State = "ANTIOQUIA",
            Street = "Carrera 43A",
            Number = "1-50",
            Neighborhood = "El Poblado"
        };
        db.LocationProfiles.Add(profile);

        var basket = new Basket { Items = [new BasketItem { Query = "leche" }] };
        db.Baskets.Add(basket);
        await db.SaveChangesAsync();

        var contextCache = new CommercialContextCache();
        var regionalHandler = new MockD1RegionalHandler("medellin-fulfillment-12109");
        using var regionalClient = new HttpClient(regionalHandler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var d1GenericClient = new HttpClient(new MockD1GenericHandler()) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var exitoClient = new HttpClient(new MockExitoHandler()) { BaseAddress = new Uri("https://www.exito.com/") };

        var raw = new RawStore(_paths, db);
        var ingest = new D1CatalogIngest(db, raw);
        var engine = new SyncEngine(
            db,
            new D1SearchSync(d1GenericClient, ingest, db),
            new ExitoSearchSync(exitoClient, new ExitoCatalogIngest(db, raw), db),
            new FreshnessService(),
            null,
            new ExitoProvider(exitoClient),
            ingest,
            () => new D1FulfillmentSession(regionalClient),
            contextCache
        );

        // 2. Act: Sync Basket
        var syncResult = await engine.SyncBasketAsync(basket.Id, locationInput: "Casa Medellin");

        Assert.Equal("LOCATION_RESOLVED", syncResult.ShoppingContext!.ValidationStatus);
        var d1SyncContext = syncResult.ShoppingContext.ProviderContexts.Single(p => p.ProviderCode == "D1");
        Assert.Equal(PriceScope.FulfillmentRegion, d1SyncContext.PriceScope);
        Assert.Equal("fulfillment:medellin-fulfillment-12109", d1SyncContext.PriceContext);
        Assert.False(d1SyncContext.IsFallback);

        // 3. Act: Resolve context for Optimize using real ShoppingContextService
        var shoppingContextService = new ShoppingContextService(
            db,
            null,
            new ExitoProvider(exitoClient),
            () => new D1FulfillmentSession(regionalClient),
            contextCache
        );

        var optimizeShoppingContext = await shoppingContextService.ResolveContextAsync(locationInput: "Casa Medellin");
        var d1OptimizeContext = optimizeShoppingContext.ProviderContexts.Single(p => p.ProviderCode == "D1");

        // 4. Assert: Exact same D1 context is reused
        Assert.Equal(d1SyncContext.PriceScope, d1OptimizeContext.PriceScope);
        Assert.Equal(d1SyncContext.PriceContext, d1OptimizeContext.PriceContext);
        Assert.Equal("fulfillment:medellin-fulfillment-12109", d1OptimizeContext.PriceContext);
        Assert.False(d1OptimizeContext.IsFallback);

        // 5. Act: Optimize Basket
        var policy = new OfferContextCompatibilityService();
        var catalog = await CatalogOfferLoader.LoadOffersAsync(db, optimizeShoppingContext, policy);
        var settings = new AppSettings();
        var recommendation = BasketOptimizer.Optimize(basket, catalog, settings);

        // 6. Assert: Assignment uses the regional D1 offer
        Assert.True(recommendation.ItemsCoveredCount > 0);
        var d1Assignment = recommendation.Assignments.FirstOrDefault(a => a.Candidate.RetailerCode == "D1");
        Assert.NotNull(d1Assignment);
        Assert.Equal(PriceScope.FulfillmentRegion, d1Assignment.Candidate.Scope);
        Assert.Equal("medellin-fulfillment-12109", d1Assignment.Candidate.Context);
    }

    [Fact]
    public async Task FullFlow_SyncAndOptimize_PreservesProviderContext()
    {
        var options = new DbContextOptionsBuilder<MarketDb>()
            .UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = _paths.Database,
                ForeignKeys = true,
                Pooling = false
            }.ToString()).Options;

        await using var db = new MarketDb(options);
        await db.Database.MigrateAsync();

        var profile = new LocationProfile
        {
            Label = "Casa",
            Address = "Calle 10 # 40-20, Medellin",
            Latitude = 6.2088m,
            Longitude = -75.5678m,
            PostalCode = "050021",
            City = "Medellin",
            State = "Antioquia",
            Street = "Calle 10",
            Number = "40-20",
            Neighborhood = "Poblado"
        };
        db.LocationProfiles.Add(profile);

        var basket = new Basket { Items = [new BasketItem { Query = "arroz" }] };
        db.Baskets.Add(basket);
        await db.SaveChangesAsync();

        var contextCache = new CommercialContextCache();
        var d1Handler = new MockD1RegionalHandler("medellin-fulfillment-12109", "arroz", 3500m, -75.5678m, 6.2088m);
        using var regionalClient = new HttpClient(d1Handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var d1GenericClient = new HttpClient(new MockD1GenericHandler("arroz", 3200m)) { BaseAddress = new Uri("https://www.d1.com.co/") };
        using var exitoClient = new HttpClient(new MockExitoHandler("arroz", 3800m)) { BaseAddress = new Uri("https://www.exito.com/") };

        var raw = new RawStore(_paths, db);
        var ingest = new D1CatalogIngest(db, raw);
        var engine = new SyncEngine(
            db,
            new D1SearchSync(d1GenericClient, ingest, db),
            new ExitoSearchSync(exitoClient, new ExitoCatalogIngest(db, raw), db),
            new FreshnessService(),
            null,
            new ExitoProvider(exitoClient),
            ingest,
            () => new D1FulfillmentSession(regionalClient),
            contextCache
        );

        // Run sync
        var syncResult = await engine.SyncBasketAsync(basket.Id, locationInput: "Casa");
        Assert.Equal("LOCATION_RESOLVED", syncResult.ShoppingContext!.ValidationStatus);

        // Inject a cheaper Bogotá offer into DB to test wrong-region isolation
        var d1Retailer = await db.Retailers.FirstAsync(r => r.Code == "D1");
        var d1Sku = await db.ExternalSkus.FirstAsync();
        var seller = await db.Sellers.FirstAsync(s => s.RetailerId == d1Retailer.Id);

        var cheapBogotaOffer = new Offer
        {
            ExternalSkuId = d1Sku.Id,
            SellerId = seller.Id,
            Price = 1500m, // cheaper than 3500 Medellin
            Currency = "COP",
            Scope = PriceScope.FulfillmentRegion,
            Context = "fulfillment:bogota-region-9999",
            Availability = AvailabilityStatus.InStock,
            CheckedAt = DateTime.UtcNow
        };
        db.Offers.Add(cheapBogotaOffer);
        await db.SaveChangesAsync();

        // Optimize
        var shoppingContextService = new ShoppingContextService(
            db,
            null,
            new ExitoProvider(exitoClient),
            () => new D1FulfillmentSession(regionalClient),
            contextCache
        );

        var optContext = await shoppingContextService.ResolveContextAsync(locationInput: "Casa");
        var policy = new OfferContextCompatibilityService();
        var catalog = await CatalogOfferLoader.LoadOffersAsync(db, optContext, policy);
        var recommendation = BasketOptimizer.Optimize(basket, catalog, new AppSettings());

        // Assert: Bogotá offer () was rejected despite being cheaper; Medellin regional offer () was chosen!
        var assignment = recommendation.Assignments.First();
        Assert.Equal("medellin-fulfillment-12109", assignment.Candidate.Context);
        Assert.Equal(3500m, assignment.Candidate.PackPrice);
        Assert.NotEqual(1500m, assignment.Candidate.PackPrice);
    }

    [Fact]
    public async Task ContextChange_InvalidatesEffectiveProviderContext_EndToEnd()
    {
        var options = new DbContextOptionsBuilder<MarketDb>()
            .UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = _paths.Database,
                ForeignKeys = true,
                Pooling = false
            }.ToString()).Options;

        await using var db = new MarketDb(options);
        await db.Database.MigrateAsync();

        var profile = new LocationProfile
        {
            Label = "Casa",
            Address = "Calle 10, Medellin",
            Latitude = 6.2088m,
            Longitude = -75.5678m,
            PostalCode = "050021",
            City = "Medellin",
            State = "Antioquia",
            Street = "Calle 10",
            Number = "40-20",
            Neighborhood = "Poblado"
        };
        db.LocationProfiles.Add(profile);
        await db.SaveChangesAsync();

        var cache = new CommercialContextCache();
        var medellinContext = new EffectiveProviderContext(
            RetailerId: Guid.NewGuid(),
            ProviderCode: "D1",
            RequestedLocationProfileId: profile.Id,
            PriceScope: PriceScope.FulfillmentRegion,
            PriceContext: "fulfillment:medellin-12109",
            FulfillmentRegion: "12109",
            ResolutionStatus: ContextResolutionStatus.Resolved,
            IsFallback: false,
            ResolvedAt: DateTime.UtcNow,
            StatusOverride: "RESOLVED"
        );

        cache.Set("D1", profile.Id, null, medellinContext);
        Assert.NotNull(cache.Get("D1", profile.Id, null));

        // Simulate update to profile (or profile invalidation)
        cache.Invalidate(profile.Id);

        // Assert: cache hit is null after invalidation
        Assert.Null(cache.Get("D1", profile.Id, null));
    }

    private sealed class MockD1GenericHandler(string queryTerm = "leche", decimal price = 2900m) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var firstPage = request.RequestUri!.Query.Contains("_from=0", StringComparison.Ordinal);
            var json = firstPage
                ? $"[{{\"productId\":\"d1-{queryTerm}\",\"productName\":\"{queryTerm.ToUpper()} D1\",\"items\":[{{\"itemId\":\"d1-{queryTerm}-1\",\"name\":\"{queryTerm} 1L\",\"sellers\":[{{\"sellerId\":\"1\",\"sellerName\":\"D1\",\"commertialOffer\":{{\"Price\":{price},\"IsAvailable\":true,\"AvailableQuantity\":20}}}}]}}]}}]"
                : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class MockD1RegionalHandler(string regionId, string queryTerm = "leche", decimal price = 2900m, decimal lon = -75.5932585m, decimal lat = 6.232177m) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            var status = HttpStatusCode.OK;
            string json;
            if (path.StartsWith("/api/sessions?", StringComparison.Ordinal))
            {
                status = HttpStatusCode.BadRequest;
                json = "{}";
            }
            else if (path == "/api/checkout/pub/orderForm")
                json = "{\"orderFormId\":\"e43e5fd1-70a3-4026-8e7b-710ac73539ab\"}";
            else if (path.StartsWith("/_v/private/segment/", StringComparison.Ordinal))
                json = "{}";
            else if (path == "/api/segments")
            {
                var lonStr = lon.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var latStr = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
                json = $"{{\"channel\":\"1\",\"regionId\":\"{regionId}\",\"facets\":\"coordinates={lonStr},{latStr};\"}}";
            }
            else if (path.StartsWith("/api/catalog_system/pub/products/search", StringComparison.Ordinal))
            {
                json = $"[{{\"productId\":\"d1-{queryTerm}\",\"productName\":\"{queryTerm.ToUpper()} D1\",\"items\":[{{\"itemId\":\"d1-{queryTerm}-1\",\"name\":\"{queryTerm} 1L\",\"sellers\":[{{\"sellerId\":\"1\",\"sellerName\":\"D1\",\"commertialOffer\":{{\"Price\":{price},\"IsAvailable\":true,\"AvailableQuantity\":20}}}}]}}]}}]";
            }
            else throw new InvalidOperationException("Unexpected D1 endpoint: " + path);

            return Task.FromResult(new HttpResponseMessage(status)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class MockExitoHandler(string queryTerm = "leche", decimal price = 3200m) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/sessionByCoords")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{\"regionId\":\"region-med-01\"}", Encoding.UTF8, "application/json") });

            var query = Uri.UnescapeDataString(request.RequestUri.Query);
            var json = query.Contains("\"after\":\"0\"", StringComparison.Ordinal)
                ? JsonSerializer.Serialize(new { data = new { search = new { products = new
                {
                    pageInfo = new { totalCount = 1 }, edges = new[] { new { node = new
                    {
                        name = $"{queryTerm} Éxito", isVariantOf = new { productGroupID = "G1" }, brand = new { brandName = "Marca" },
                        productType = new { isMkp = false }, items = new[] { new
                        {
                            itemId = "exito-item-1", name = $"{queryTerm} 1L", ean = "7701234567899",
                            sellers = new[] { new { sellerId = "1", sellerName = "Éxito", commertialOffer = new
                            { Price = price, ListPrice = price + 300, AvailableQuantity = 20 } } }
                        } }
                    } } } } } } })
                : "{\"data\":{\"search\":{\"products\":{\"pageInfo\":{\"totalCount\":0},\"edges\":[]}}}}";

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}