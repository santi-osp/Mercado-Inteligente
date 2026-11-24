using System.Text.Json;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

// Explicit opt-in only: dotnet test --filter FullyQualifiedName~D1LiveSmokeTests with MARKET_D1_LIVE_SMOKE=1.
public sealed class D1LiveSmokeTests
{
    [EnvironmentVariableFact("MARKET_D1_LIVE_SMOKE")]
    public async Task Anonymous_medellin_capture_into_disposable_database()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-live-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, ForeignKeys = true, Pooling = false }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            using var generic = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
                { BaseAddress = new Uri("https://www.d1.com.co/"), Timeout = TimeSpan.FromSeconds(15) };
            using var response = await generic.GetAsync("api/catalog_system/pub/products/search?ft=arroz&_from=0&_to=0", timeout.Token);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token);
            Assert.True(bytes.Length is > 0 and <= 2 * 1024 * 1024);
            using var document = JsonDocument.Parse(bytes);
            var product = Assert.Single(D1Mapper.Products(document.RootElement));
            Assert.NotEmpty(product.Skus);
            using var session = new D1FulfillmentSession();
            var address = new D1DeliveryAddress("050030284", "Medellin", "ANTIOQUIA", "CL 30 A", "72 - 45",
                "ROSALES", -75.5932585m, 6.232177m);
            var capture = new D1RegionalCapture(session, new D1CatalogIngest(db, new RawStore(paths, db)));
            Assert.True(await capture.CaptureProductAsync(address, product.ExternalId, timeout.Token) > 0);
            var offer = await db.Offers.AsNoTracking().SingleAsync(timeout.Token);
            Assert.True(offer.Price > 0);
            Assert.Equal(PriceScope.FulfillmentRegion, offer.Scope);
            Assert.NotNull(offer.RawCaptureId);
            Assert.NotEmpty(await db.PriceObservations.ToListAsync(timeout.Token));
            await session.ConfirmStillValidAsync(timeout.Token);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
