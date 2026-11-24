using System.Text;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.Exito;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

public class ExitoCatalogIngestTests
{
    private static string Payload(string name, int price, bool isMkp = false, string sellerId = "1") =>
        JsonSerializer.Serialize(new
        {
            data = new
            {
                search = new
                {
                    products = new
                    {
                        pageInfo = new { totalCount = 1 },
                        edges = new[]
                        {
                            new
                            {
                                node = new
                                {
                                    name,
                                    isVariantOf = new { productGroupID = "100" },
                                    brand = new { brandName = "Marca" },
                                    productType = new { isMkp },
                                    items = new[]
                                    {
                                        new
                                        {
                                            itemId = "101",
                                            name,
                                            ean = "7700000000001",
                                            sellers = new[]
                                            {
                                                new
                                                {
                                                    sellerId,
                                                    sellerName = "Vendedor",
                                                    commertialOffer = new
                                                    {
                                                        Price = price,
                                                        ListPrice = price + 500,
                                                        AvailableQuantity = 100
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        });

    [Fact]
    public async Task Metrics_distinguish_created_changed_unchanged_and_deduplicated_raw()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-metrics-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();

            var ingest = new ExitoCatalogIngest(db, new RawStore(paths, db));
            var context = new ProviderContext("online-generic");
            var date = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

            var initial = Encoding.UTF8.GetBytes(Payload("Arroz 500g", 2500));
            var first = await ingest.SaveWithMetricsAsync(initial, context, PriceScope.OnlineGeneric, date, false, CancellationToken.None);
            Assert.Equal(new ExitoIngestMetrics(1, 1, 1, 0, 1, 0, 1, 0), first);

            var repeat = await ingest.SaveWithMetricsAsync(initial, context, PriceScope.OnlineGeneric, date.AddMinutes(1), false, CancellationToken.None);
            Assert.Equal(new ExitoIngestMetrics(1, 0, 0, 0, 0, 0, 0, 1), repeat);

            var update = await ingest.SaveWithMetricsAsync(Encoding.UTF8.GetBytes(Payload("Arroz 500g Especial", 2700)), context,
                PriceScope.OnlineGeneric, date.AddMinutes(2), false, CancellationToken.None);
            Assert.Equal(new ExitoIngestMetrics(1, 1, 0, 1, 0, 1, 1, 0), update);

            db.ChangeTracker.Clear();
            Assert.Equal(2, await db.RawCaptures.CountAsync());
            Assert.Equal(2, await db.PriceObservations.CountAsync());
            var retailer = await db.Retailers.SingleAsync(x => x.Code == "EXITO");
            Assert.Equal("Éxito", retailer.Name);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Marketplace_offers_excluded_by_default_and_included_when_enabled()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-mkp-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();

            var ingest = new ExitoCatalogIngest(db, new RawStore(paths, db));
            var context = new ProviderContext("online-generic");
            var date = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

            var mkpPayload = Encoding.UTF8.GetBytes(Payload("Café Especial", 18000, isMkp: true, sellerId: "16377"));

            // When includeMarketplace = false, marketplace offer is excluded
            var defaultResult = await ingest.SaveWithMetricsAsync(mkpPayload, context, PriceScope.OnlineGeneric, date, false, CancellationToken.None);
            Assert.Equal(0, defaultResult.OffersProcessed);

            // When includeMarketplace = true, marketplace offer is saved
            var enabledResult = await ingest.SaveWithMetricsAsync(mkpPayload, context, PriceScope.OnlineGeneric, date.AddMinutes(1), true, CancellationToken.None);
            Assert.Equal(1, enabledResult.OffersProcessed);
            Assert.Equal(1, enabledResult.OffersChanged);

            var offer = await db.Offers.SingleAsync();
            Assert.Equal(18000m, offer.Price);
            var seller = await db.Sellers.SingleAsync(x => x.Id == offer.SellerId);
            Assert.Equal("16377", seller.ExternalId);
            Assert.Equal(SellerKind.Marketplace, seller.Kind);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Older_payload_does_not_replace_newer_offer_checked_at()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-order-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();

            var ingest = new ExitoCatalogIngest(db, new RawStore(paths, db));
            var context = new ProviderContext("online-generic");
            var newerDate = new DateTime(2026, 10, 7, 14, 0, 0, DateTimeKind.Utc);
            var olderDate = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

            await ingest.SaveWithMetricsAsync(Encoding.UTF8.GetBytes(Payload("Leche", 4500)), context,
                PriceScope.OnlineGeneric, newerDate, false, CancellationToken.None);

            var before = await db.Offers.SingleAsync();
            Assert.Equal(4500m, before.Price);
            Assert.Equal(newerDate, before.CheckedAt);

            // Older response arrives later
            await ingest.SaveWithMetricsAsync(Encoding.UTF8.GetBytes(Payload("Leche", 4000)), context,
                PriceScope.OnlineGeneric, olderDate, false, CancellationToken.None);

            db.ChangeTracker.Clear();
            var after = await db.Offers.SingleAsync();
            Assert.Equal(4500m, after.Price); // Preserved newer price!
            Assert.Equal(newerDate, after.CheckedAt);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Context_scope_mismatch_throws_invalid_operation()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-mismatch-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite(new SqliteConnectionStringBuilder
            { DataSource = paths.Database, ForeignKeys = true, Pooling = false, DefaultTimeout = 10 }.ToString()).Options;
            await using var db = new MarketDb(options);
            await db.Database.MigrateAsync();

            var ingest = new ExitoCatalogIngest(db, new RawStore(paths, db));
            var date = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
            var payload = Encoding.UTF8.GetBytes(Payload("Aceite", 12000));

            // Generic context with Regional scope must throw
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ingest.SaveWithMetricsAsync(payload, new ProviderContext("online-generic"), PriceScope.FulfillmentRegion, date, false, CancellationToken.None));

            // Regional context with OnlineGeneric scope must throw
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ingest.SaveWithMetricsAsync(payload, new ProviderContext("fulfillment:reg1"), PriceScope.OnlineGeneric, date, false, CancellationToken.None));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
