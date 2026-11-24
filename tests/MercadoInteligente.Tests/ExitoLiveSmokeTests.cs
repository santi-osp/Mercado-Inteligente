using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using MercadoInteligente.Infrastructure.Stores.Exito;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

public sealed class ExitoLiveSmokeTests
{
    [EnvironmentVariableFact("MARKET_DUAL_LIVE_SMOKE")]
    public async Task LiveBasketSyncRetainsSeparateD1AndExitoRegionalScopes()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-dual-live-" + Guid.NewGuid().ToString("N"));
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
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            using var d1Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
                { BaseAddress = new Uri("https://www.d1.com.co/"), Timeout = TimeSpan.FromSeconds(15) };
            using var exitoClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
                { BaseAddress = new Uri("https://www.exito.com/"), Timeout = TimeSpan.FromSeconds(15) };
            var raw = new RawStore(paths, db);
            var d1Ingest = new D1CatalogIngest(db, raw);
            var engine = new SyncEngine(db, new D1SearchSync(d1Client, d1Ingest, db),
                new ExitoSearchSync(exitoClient, new ExitoCatalogIngest(db, raw), db),
                new FreshnessService(), null, new ExitoProvider(exitoClient), d1Ingest);
            var result = await engine.SyncBasketAsync(basket.Id, locationInput: "Entrega", ct: timeout.Token);
            Assert.All(result.Results, entry => Assert.Equal("COMPLETED", entry.Status));
            Assert.All(result.ShoppingContext!.Providers, context => Assert.Equal("RESOLVED", context.Status));
            Assert.All(await db.Offers.AsNoTracking().ToListAsync(timeout.Token), offer =>
                Assert.Equal(PriceScope.FulfillmentRegion, offer.Scope));
            Assert.True(await db.Offers.CountAsync(timeout.Token) >= 2);
            var recommendation = BasketOptimizer.Optimize(basket,
                await CatalogOfferLoader.LoadOffersAsync(db, timeout.Token), new AppSettings());
            Assert.True(recommendation.ItemsCoveredCount > 0);
            await using var reopened = new MarketDb(options);
            Assert.True(await reopened.PriceObservations.CountAsync(timeout.Token) >= 2);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [EnvironmentVariableFact("MARKET_EXITO_LIVE_SMOKE")]
    public async Task AnonymousRegionalSearchIntoDisposableDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-live-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
                { BaseAddress = new Uri("https://www.exito.com/"), Timeout = TimeSpan.FromSeconds(15) };
            var context = await new ExitoProvider(client).ResolveRegionAsync(-75.5925m, 6.2425m, timeout.Token);
            Assert.StartsWith("fulfillment:", context.Key);
            var sync = new ExitoSearchSync(client, new ExitoCatalogIngest(db, new RawStore(paths, db)), db);
            var result = await sync.RefreshAsync("leche", context, "LiveSmoke", timeout.Token);
            Assert.Equal("COMPLETED", result.Status);
            Assert.True(result.OffersSeen > 0);
            Assert.All(await db.Offers.AsNoTracking().ToListAsync(timeout.Token), offer =>
            {
                Assert.Equal(PriceScope.FulfillmentRegion, offer.Scope);
                Assert.Equal(context.Key, offer.Context);
                Assert.NotNull(offer.RawCaptureId);
            });
            Assert.NotEmpty(await db.PriceObservations.AsNoTracking().ToListAsync(timeout.Token));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
