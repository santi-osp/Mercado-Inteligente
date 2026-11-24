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

namespace MercadoInteligente.Tests;

public sealed class Rc1BasketContextIntegrationTests
{
    [Fact]
    public async Task CompleteAddressCapturesD1AndExitoWithSeparateVerifiedCommercialContexts()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-rc1-regional-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            db.LocationProfiles.Add(new LocationProfile
            {
                Label = "Entrega", Address = "CL 30 A 72-45", Latitude = 6.232177m, Longitude = -75.5932585m,
                PostalCode = "050030284", City = "Medellin", State = "ANTIOQUIA", Street = "CL 30 A",
                Number = "72 - 45", Neighborhood = "ROSALES"
            });
            var basket = new Basket { Items = [new BasketItem { Query = "leche" }] };
            db.Baskets.Add(basket);
            await db.SaveChangesAsync();

            var regionalHandler = new D1RegionalHandler();
            using var regionalClient = new HttpClient(regionalHandler) { BaseAddress = new Uri("https://www.d1.com.co/") };
            using var d1Client = new HttpClient(new D1Handler()) { BaseAddress = new Uri("https://www.d1.com.co/") };
            using var exitoClient = new HttpClient(new ExitoHandler()) { BaseAddress = new Uri("https://www.exito.com/") };
            var raw = new RawStore(paths, db);
            var ingest = new D1CatalogIngest(db, raw);
            var engine = new SyncEngine(db, new D1SearchSync(d1Client, ingest, db),
                new ExitoSearchSync(exitoClient, new ExitoCatalogIngest(db, raw), db),
                new FreshnessService(), null, new ExitoProvider(exitoClient), ingest,
                () => new D1FulfillmentSession(regionalClient));

            var result = await engine.SyncBasketAsync(basket.Id, locationInput: "Entrega");
            Assert.Equal("LOCATION_RESOLVED", result.ShoppingContext!.ValidationStatus);
            Assert.Equal("RESOLVED", result.ShoppingContext.Providers.Single(p => p.Provider == "D1").Status);
            Assert.Equal("fulfillment:medellin-fulfillment", result.ShoppingContext.Providers.Single(p => p.Provider == "D1").Key);
            Assert.Equal("fulfillment:region-med-01", result.ShoppingContext.Providers.Single(p => p.Provider == "EXITO").Key);
            Assert.All(result.Results, entry => Assert.Equal("COMPLETED", entry.Status));
            Assert.Equal(1, regionalHandler.CatalogCalls);
            Assert.Equal(2, await db.Offers.CountAsync());
            Assert.All(await db.Offers.ToListAsync(), offer => Assert.Equal(PriceScope.FulfillmentRegion, offer.Scope));
            var catalog = await CatalogOfferLoader.LoadOffersAsync(db, CancellationToken.None);
            var recommendation = BasketOptimizer.Optimize(basket, catalog, new AppSettings());
            Assert.Equal(1, recommendation.ItemsCoveredCount);
            Assert.Equal(0, recommendation.MissingItemsCount);
            Assert.All(recommendation.Assignments, assignment =>
                Assert.Equal(PriceScope.FulfillmentRegion, assignment.Candidate.Scope));

            db.ChangeTracker.Clear();
            await using var reopened = new MarketDb(options);
            Assert.Equal(2, await reopened.Offers.CountAsync());
            Assert.Equal(2, await reopened.PriceObservations.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task CoordinatesResolveExitoRegionWhileD1RemainsExplicitlyOnlineGeneric()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-rc1-context-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            var basket = new Basket { Items = [new BasketItem { Query = "leche" }] };
            db.Baskets.Add(basket);
            await db.SaveChangesAsync();

            var d1Handler = new D1Handler();
            var exitoHandler = new ExitoHandler();
            using var d1Client = new HttpClient(d1Handler) { BaseAddress = new Uri("https://www.d1.com.co/") };
            using var exitoClient = new HttpClient(exitoHandler) { BaseAddress = new Uri("https://www.exito.com/") };
            var raw = new RawStore(paths, db);
            var engine = new SyncEngine(db,
                new D1SearchSync(d1Client, new D1CatalogIngest(db, raw), db),
                new ExitoSearchSync(exitoClient, new ExitoCatalogIngest(db, raw), db),
                new FreshnessService(), null, new ExitoProvider(exitoClient));

            var result = await engine.SyncBasketAsync(basket.Id, locationInput: "6.24250, -75.59250");
            Assert.Equal("LOCATION_RESOLVED", result.ShoppingContext!.ValidationStatus);
            Assert.Equal("ONLINE_GENERIC", result.ShoppingContext.Providers.Single(p => p.Provider == "D1").Status);
            Assert.Equal("fulfillment:region-med-01", result.ShoppingContext.Providers.Single(p => p.Provider == "EXITO").Key);
            Assert.All(result.Results, entry => Assert.Equal("COMPLETED", entry.Status));
            Assert.True(exitoHandler.SawRegion);
            Assert.Equal(2, await db.Offers.CountAsync());
            var offers = await db.Offers.Join(db.ExternalSkus, offer => offer.ExternalSkuId, sku => sku.Id,
                (offer, sku) => new { offer.Scope, offer.Context, sku.ExternalProductId })
                .Join(db.ExternalProducts, item => item.ExternalProductId, product => product.Id,
                    (item, product) => new { item.Scope, item.Context, product.RetailerId })
                .Join(db.Retailers, item => item.RetailerId, retailer => retailer.Id,
                    (item, retailer) => new { item.Scope, item.Context, Code = retailer.Code }).ToListAsync();
            Assert.Contains(offers, offer => offer.Code == "D1" && offer.Scope == PriceScope.OnlineGeneric && offer.Context == "online-generic");
            Assert.Contains(offers, offer => offer.Code == "EXITO" && offer.Scope == PriceScope.FulfillmentRegion && offer.Context == "fulfillment:region-med-01");
            Assert.All(await db.PriceObservations.ToListAsync(), observation => Assert.NotNull(observation.PriceScope));

            db.ChangeTracker.Clear();
            await using var reopened = new MarketDb(options);
            Assert.Equal(2, await reopened.Offers.CountAsync());
            Assert.Equal(2, await reopened.PriceObservations.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RegionalFailuresUseGenericOffersWithoutClaimingRegionalPrices()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-rc1-fallback-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            db.LocationProfiles.Add(new LocationProfile
            {
                Label = "Entrega", Address = "CL 30 A 72-45", Latitude = 6.232177m, Longitude = -75.5932585m,
                PostalCode = "050030284", City = "Medellin", State = "ANTIOQUIA", Street = "CL 30 A",
                Number = "72 - 45", Neighborhood = "ROSALES"
            });
            var basket = new Basket { Items = [new BasketItem { Query = "leche" }] };
            db.Baskets.Add(basket);
            await db.SaveChangesAsync();

            using var regionalClient = new HttpClient(new D1RegionalHandler { FailRegional = true })
            { BaseAddress = new Uri("https://www.d1.com.co/") };
            using var d1Client = new HttpClient(new D1Handler()) { BaseAddress = new Uri("https://www.d1.com.co/") };
            using var exitoClient = new HttpClient(new ExitoHandler { FailRegional = true })
            { BaseAddress = new Uri("https://www.exito.com/") };
            var raw = new RawStore(paths, db);
            var ingest = new D1CatalogIngest(db, raw);
            var engine = new SyncEngine(db, new D1SearchSync(d1Client, ingest, db),
                new ExitoSearchSync(exitoClient, new ExitoCatalogIngest(db, raw), db),
                new FreshnessService(), null, new ExitoProvider(exitoClient), ingest,
                () => new D1FulfillmentSession(regionalClient));

            var result = await engine.SyncBasketAsync(basket.Id, locationInput: "Entrega");
            Assert.All(result.Results, entry => Assert.Equal("COMPLETED", entry.Status));
            Assert.All(result.ShoppingContext!.Providers, context =>
            {
                Assert.Equal("ONLINE_GENERIC_FALLBACK", context.Status);
                Assert.Equal(PriceScope.OnlineGeneric, context.Scope);
            });
            Assert.Equal(2, await db.Offers.CountAsync());
            Assert.All(await db.Offers.ToListAsync(), offer =>
            {
                Assert.Equal(PriceScope.OnlineGeneric, offer.Scope);
                Assert.Equal("online-generic", offer.Context);
            });
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private sealed class D1Handler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var firstPage = request.RequestUri!.Query.Contains("_from=0", StringComparison.Ordinal);
            var json = firstPage
                ? "[{\"productId\":\"d1-milk\",\"productName\":\"Leche D1\",\"items\":[{\"itemId\":\"d1-milk-1\",\"name\":\"Leche 1 L\",\"sellers\":[{\"sellerId\":\"1\",\"sellerName\":\"D1\",\"commertialOffer\":{\"Price\":2900,\"IsAvailable\":true,\"AvailableQuantity\":20}}]}]}]"
                : "[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class D1RegionalHandler : HttpMessageHandler
    {
        public int CatalogCalls { get; private set; }
        public bool FailRegional { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.PathAndQuery;
            if (FailRegional && path.StartsWith("/api/segments", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
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
                json = "{\"channel\":\"1\",\"regionId\":\"medellin-fulfillment\",\"facets\":\"coordinates=-75.5932585,6.232177;\"}";
            else if (path.StartsWith("/api/catalog_system/pub/products/search?ft=leche", StringComparison.Ordinal))
            {
                CatalogCalls++;
                json = "[{\"productId\":\"d1-milk\",\"productName\":\"Leche D1\",\"items\":[{\"itemId\":\"d1-milk-1\",\"name\":\"Leche 1 L\",\"sellers\":[{\"sellerId\":\"1\",\"sellerName\":\"D1\",\"commertialOffer\":{\"Price\":2900,\"IsAvailable\":true,\"AvailableQuantity\":20}}]}]}]";
            }
            else throw new InvalidOperationException("Unexpected D1 endpoint: " + path);
            return Task.FromResult(new HttpResponseMessage(status)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class ExitoHandler : HttpMessageHandler
    {
        public bool SawRegion { get; private set; }
        public bool FailRegional { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath == "/api/sessionByCoords")
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent("{\"regionId\":\"region-med-01\"}", Encoding.UTF8, "application/json") });

            var query = Uri.UnescapeDataString(request.RequestUri.Query);
            if (FailRegional && query.Contains("region-med-01", StringComparison.Ordinal))
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
            SawRegion |= query.Contains("region-med-01", StringComparison.Ordinal);
            var json = query.Contains("\"after\":\"0\"", StringComparison.Ordinal)
                ? JsonSerializer.Serialize(new { data = new { search = new { products = new
                {
                    pageInfo = new { totalCount = 1 }, edges = new[] { new { node = new
                    {
                        name = "Leche Éxito", isVariantOf = new { productGroupID = "G1" }, brand = new { brandName = "Marca" },
                        productType = new { isMkp = false }, items = new[] { new
                        {
                            itemId = "exito-milk-1", name = "Leche 1L", ean = "7701234567899",
                            sellers = new[] { new { sellerId = "1", sellerName = "Éxito", commertialOffer = new
                            { Price = 3200, ListPrice = 3500, AvailableQuantity = 20 } } }
                        } }
                    } } } } } } })
                : "{\"data\":{\"search\":{\"products\":{\"pageInfo\":{\"totalCount\":0},\"edges\":[]}}}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
