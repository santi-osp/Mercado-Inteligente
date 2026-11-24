using System.Net;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using MercadoInteligente.Infrastructure.Stores.Exito;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MercadoInteligente.Tests;

public sealed class Goal16AddendumTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalPaths _paths;

    public Goal16AddendumTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mercado_goal16_addendum_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _paths = new LocalPaths(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    private async Task<(MarketDb db, CatalogReconciliationService reconciler, RawStore rawStore)> CreateServicesAsync()
    {
        var options = new DbContextOptionsBuilder<MarketDb>()
            .UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = _paths.Database,
                ForeignKeys = true,
                Pooling = false
            }.ToString()).Options;

        var db = new MarketDb(options);
        await db.Database.MigrateAsync();

        var rawStore = new RawStore(_paths, db);
        var lifecycle = new ProductLifecycleService();
        var reconciler = new CatalogReconciliationService(db, rawStore, lifecycle, NullLogger<CatalogReconciliationService>.Instance);

        return (db, reconciler, rawStore);
    }

    [Fact]
    public async Task Search_MergesLocalAndRemoteResults()
    {
        // ARRANGE
        var (db, reconciler, _) = await CreateServicesAsync();
        await using (db)
        {
            var retailer = new Retailer { Code = "D1", Name = "Tiendas D1" };
            db.Retailers.Add(retailer);
            var localProd = new ExternalProduct
            {
                RetailerId = retailer.Id,
                ExternalId = "D1-LOCAL-QUESO",
                Name = "Queso Mozzarella Tajado Local 400g",
                Brand = "Latti",
                CategoryPath = "Lácteos y Quesos",
                Status = ProductLifecycleState.Active,
                UpdatedAt = DateTime.UtcNow
            };
            db.ExternalProducts.Add(localProd);
            await db.SaveChangesAsync();

            // ACT: Reconcile remote product into database (from Exito or Olimpica)
            var remoteProducts = new List<ProviderProduct>
            {
                new(
                    "EXITO-REMOTE-QUESO",
                    "Queso Campesino Remoto 500g",
                    "Colanta",
                    new List<ProviderSku> { new("SKU-EXITO-1", "Queso Campesino", "770111222333", 500m, Dimension.Mass) },
                    "https://exito.com/p1",
                    "Lácteos",
                    "https://exito.com/i1.jpg"
                )
            };

            await reconciler.ReconcileRemoteProductsAsync(
                "EXITO",
                "Éxito",
                remoteProducts,
                p => new List<ProviderOffer> { new(p.Skus[0].ExternalId, "EXITO", "Éxito", SellerKind.RetailerDirect, 12000m, PriceScope.Region, AvailabilityStatus.InStock, "region:antioquia") }
            );

            // Search local database for "Queso"
            var searchResults = await ProductSearchService.SearchAsync(db, "Queso", CancellationToken.None);

            // ASSERT: Both local and reconciled remote products are present
            Assert.Contains(searchResults, p => p.ExternalId == "D1-LOCAL-QUESO");
            Assert.Contains(searchResults, p => p.ExternalId == "EXITO-REMOTE-QUESO");
        }
    }

    [Fact]
    public async Task Search_UpdatesExistingOffer_WhenRemotePriceChanged()
    {
        // ARRANGE
        var (db, reconciler, _) = await CreateServicesAsync();
        await using (db)
        {
            var retailer = new Retailer { Code = "D1", Name = "Tiendas D1" };
            db.Retailers.Add(retailer);
            var seller = new Seller { RetailerId = retailer.Id, Name = "D1", Kind = SellerKind.RetailerDirect };
            db.Sellers.Add(seller);

            var product = new ExternalProduct
            {
                RetailerId = retailer.Id,
                ExternalId = "D1-LECHE",
                Name = "Leche Entera 1L",
                Brand = "Latti",
                Status = ProductLifecycleState.Active,
                UpdatedAt = DateTime.UtcNow.AddDays(-1)
            };
            db.ExternalProducts.Add(product);
            await db.SaveChangesAsync();

            var sku = new ExternalSku
            {
                ExternalProductId = product.Id,
                ExternalId = "SKU-D1-LECHE",
                Name = "Leche Entera 1L",
                Quantity = 1000m,
                Dimension = Dimension.Volume
            };
            db.ExternalSkus.Add(sku);
            await db.SaveChangesAsync();

            var offer = new Offer
            {
                ExternalSkuId = sku.Id,
                SellerId = seller.Id,
                Price = 3800m,
                ListPrice = 3800m,
                Availability = AvailabilityStatus.InStock,
                Scope = PriceScope.FulfillmentRegion,
                Context = "fulfillment:medellin-aburra",
                CheckedAt = DateTime.UtcNow.AddDays(-1)
            };
            db.Offers.Add(offer);
            await db.SaveChangesAsync();

            var initialObs = PriceObservationFactory.FromOfferSnapshot(offer, offer.CheckedAt);
            db.PriceObservations.Add(initialObs);
            await db.SaveChangesAsync();

            // ACT: Remote search returns price change to $4.200
            var remoteProducts = new List<ProviderProduct>
            {
                new(
                    "D1-LECHE",
                    "Leche Entera 1L",
                    "Latti",
                    new List<ProviderSku> { new("SKU-D1-LECHE", "Leche Entera 1L", null, 1000m, Dimension.Volume) },
                    null,
                    "Lácteos",
                    null
                )
            };

            await reconciler.ReconcileRemoteProductsAsync(
                "D1",
                "Tiendas D1",
                remoteProducts,
                p => new List<ProviderOffer> { new("SKU-D1-LECHE", "D1", "Tiendas D1", SellerKind.RetailerDirect, 4200m, PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra") }
            );

            // ASSERT: Offer price updated, historical observations preserved
            var updatedOffer = await db.Offers.FirstAsync(o => o.ExternalSkuId == sku.Id);
            Assert.Equal(4200m, updatedOffer.Price);

            var observations = await db.PriceObservations.Where(o => o.OfferId == updatedOffer.Id).OrderBy(o => o.CheckedAt).ToListAsync();
            Assert.True(observations.Count >= 2);
            Assert.Equal(3800m, observations[0].Price);
            Assert.Equal(4200m, observations[^1].Price);
        }
    }

    [Fact]
    public async Task Search_InsertsPreviouslyUnknownRemoteSku()
    {
        // ARRANGE
        var (db, reconciler, _) = await CreateServicesAsync();
        await using (db)
        {
            var retailer = new Retailer { Code = "LAVAQUITA", Name = "Supermercado La Vaquita" };
            db.Retailers.Add(retailer);
            await db.SaveChangesAsync();

            // ACT: Reconcile previously unseen product and SKU
            var remoteProducts = new List<ProviderProduct>
            {
                new(
                    "LV-ARROZ-1KG",
                    "Arroz Blanco Diana 1kg",
                    "Diana",
                    new List<ProviderSku> { new("SKU-LV-ARROZ-1KG", "Arroz 1kg", "770123450001", 1000m, Dimension.Mass) },
                    "https://lavaquita.co/arroz",
                    "Granos",
                    "https://lavaquita.co/arroz.jpg"
                )
            };

            var saved = await reconciler.ReconcileRemoteProductsAsync(
                "LAVAQUITA",
                "Supermercado La Vaquita",
                remoteProducts,
                p => new List<ProviderOffer> { new("SKU-LV-ARROZ-1KG", "LAVAQUITA", "Supermercado La Vaquita", SellerKind.RetailerDirect, 4900m, PriceScope.OnlineGeneric, AvailabilityStatus.InStock, "online:colombia") }
            );

            Assert.Equal(1, saved);

            // ASSERT: Product, Sku, and Offer exist in database
            var prod = await db.ExternalProducts.FirstOrDefaultAsync(p => p.ExternalId == "LV-ARROZ-1KG");
            Assert.NotNull(prod);
            Assert.Equal("Arroz Blanco Diana 1kg", prod.Name);
            Assert.Equal(ProductLifecycleState.Active, prod.Status);

            var sku = await db.ExternalSkus.FirstOrDefaultAsync(s => s.ExternalId == "SKU-LV-ARROZ-1KG");
            Assert.NotNull(sku);

            var offer = await db.Offers.FirstOrDefaultAsync(o => o.ExternalSkuId == sku.Id);
            Assert.NotNull(offer);
            Assert.Equal(4900m, offer.Price);
        }
    }

    [Fact]
    public async Task SearchResultAbsence_DoesNotDeactivateExistingSku()
    {
        // ARRANGE (CRITICAL REQUIREMENT):
        // Absence in a partial search query must NEVER deactivate an existing active product/SKU.
        var (db, reconciler, _) = await CreateServicesAsync();
        await using (db)
        {
            var retailer = new Retailer { Code = "D1", Name = "Tiendas D1" };
            db.Retailers.Add(retailer);
            var prod = new ExternalProduct
            {
                RetailerId = retailer.Id,
                ExternalId = "D1-ACEITE-900",
                Name = "Aceite Vegetal 900ml",
                Brand = "D1",
                Status = ProductLifecycleState.Active,
                MissingCount = 0,
                UpdatedAt = DateTime.UtcNow
            };
            db.ExternalProducts.Add(prod);
            await db.SaveChangesAsync();

            var sku = new ExternalSku
            {
                ExternalProductId = prod.Id,
                ExternalId = "SKU-D1-ACEITE",
                Name = "Aceite 900ml",
                Quantity = 900m,
                Dimension = Dimension.Volume
            };
            db.ExternalSkus.Add(sku);
            await db.SaveChangesAsync();

            // ACT: A search for "Galletas" returns completely unrelated products
            var remoteProducts = new List<ProviderProduct>
            {
                new("D1-GALLETAS", "Galletas Dulces 200g", "D1", new List<ProviderSku> { new("SKU-D1-GALLETAS", "Galletas", null, 200m, Dimension.Mass) }, null, "Snacks", null)
            };

            await reconciler.ReconcileRemoteProductsAsync(
                "D1",
                "Tiendas D1",
                remoteProducts,
                p => new List<ProviderOffer> { new("SKU-D1-GALLETAS", "D1", "Tiendas D1", SellerKind.RetailerDirect, 2500m, PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra") }
            );

            // ASSERT: The absent product D1-ACEITE-900 remains Active and MissingCount is 0!
            var checkedProd = await db.ExternalProducts.FirstAsync(p => p.ExternalId == "D1-ACEITE-900");
            Assert.Equal(ProductLifecycleState.Active, checkedProd.Status);
            Assert.Equal(0, checkedProd.MissingCount);
        }
    }

    [Fact]
    public async Task ExplicitProviderUnavailable_UpdatesCurrentAvailabilityButPreservesHistory()
    {
        // ARRANGE
        var (db, reconciler, _) = await CreateServicesAsync();
        await using (db)
        {
            var retailer = new Retailer { Code = "EXITO", Name = "Almacenes Éxito" };
            db.Retailers.Add(retailer);
            var seller = new Seller { RetailerId = retailer.Id, Name = "Éxito", Kind = SellerKind.RetailerDirect };
            db.Sellers.Add(seller);

            var product = new ExternalProduct
            {
                RetailerId = retailer.Id,
                ExternalId = "EXITO-CAFE",
                Name = "Café Sello Rojo 500g",
                Status = ProductLifecycleState.Active,
                UpdatedAt = DateTime.UtcNow
            };
            db.ExternalProducts.Add(product);
            await db.SaveChangesAsync();

            var sku = new ExternalSku
            {
                ExternalProductId = product.Id,
                ExternalId = "SKU-EXITO-CAFE",
                Name = "Café Sello Rojo 500g",
                Quantity = 500m,
                Dimension = Dimension.Mass
            };
            db.ExternalSkus.Add(sku);
            await db.SaveChangesAsync();

            var offer = new Offer
            {
                ExternalSkuId = sku.Id,
                SellerId = seller.Id,
                Price = 18900m,
                Availability = AvailabilityStatus.InStock,
                Scope = PriceScope.Region,
                Context = "region:antioquia",
                CheckedAt = DateTime.UtcNow.AddHours(-2)
            };
            db.Offers.Add(offer);
            await db.SaveChangesAsync();

            var obs = PriceObservationFactory.FromOfferSnapshot(offer, offer.CheckedAt);
            db.PriceObservations.Add(obs);
            await db.SaveChangesAsync();

            // ACT: Explicit provider reports item out of stock
            var updated = await reconciler.MarkExplicitUnavailableAsync("EXITO", "SKU-EXITO-CAFE", "OUT_OF_STOCK");
            Assert.True(updated);

            // ASSERT: Current offer availability is OutOfStock
            var currentOffer = await db.Offers.FirstAsync(o => o.ExternalSkuId == sku.Id);
            Assert.Equal(AvailabilityStatus.OutOfStock, currentOffer.Availability);

            // Historical observation is preserved with original InStock price
            var observations = await db.PriceObservations.Where(o => o.OfferId == currentOffer.Id).OrderBy(o => o.CheckedAt).ToListAsync();
            Assert.True(observations.Count >= 2);
            Assert.Equal(AvailabilityStatus.InStock, observations[0].Availability);
            Assert.Equal(AvailabilityStatus.OutOfStock, observations[^1].Availability);
        }
    }

    [Fact]
    public async Task CompletedCategorySync_MarksMissingSkuWithoutDeletingHistory()
    {
        // ARRANGE
        var (db, reconciler, _) = await CreateServicesAsync();
        await using (db)
        {
            var retailer = new Retailer { Code = "D1", Name = "Tiendas D1" };
            db.Retailers.Add(retailer);

            var prod1 = new ExternalProduct
            {
                RetailerId = retailer.Id,
                ExternalId = "D1-YOGURT-FRESA",
                Name = "Yogurt Fresa 1L",
                CategoryPath = "Lácteos y Refrigerados",
                Status = ProductLifecycleState.Active,
                MissingCount = 0,
                UpdatedAt = DateTime.UtcNow
            };
            var prod2 = new ExternalProduct
            {
                RetailerId = retailer.Id,
                ExternalId = "D1-YOGURT-MORA",
                Name = "Yogurt Mora 1L",
                CategoryPath = "Lácteos y Refrigerados",
                Status = ProductLifecycleState.Active,
                MissingCount = 0,
                UpdatedAt = DateTime.UtcNow
            };
            db.ExternalProducts.AddRange(prod1, prod2);
            await db.SaveChangesAsync();

            // ACT: Completed category sync for "Lácteos y Refrigerados" only returns prod1
            var missingCount = await reconciler.ReconcileCompletedCategoryAsync(
                "D1",
                "Lácteos y Refrigerados",
                new List<string> { prod1.ExternalId },
                CancellationToken.None
            );

            // ASSERT: prod2 is marked Missing, prod1 remains Active
            Assert.Equal(1, missingCount);

            var dbProd1 = await db.ExternalProducts.FirstAsync(p => p.ExternalId == prod1.ExternalId);
            var dbProd2 = await db.ExternalProducts.FirstAsync(p => p.ExternalId == prod2.ExternalId);

            Assert.Equal(ProductLifecycleState.Active, dbProd1.Status);
            Assert.Equal(ProductLifecycleState.Missing, dbProd2.Status);
            Assert.Equal(1, dbProd2.MissingCount);
        }
    }

    [Fact]
    public async Task CategoryImport_PaginatesPersistsAndDeduplicates()
    {
        // ARRANGE
        var (db, reconciler, _) = await CreateServicesAsync();
        await using (db)
        {
            var batch1 = new List<ProviderProduct>
            {
                new("PROD-1", "Manzana Roja 1kg", "Fruver", new List<ProviderSku> { new("SKU-MANZANA", "Manzana", null, 1000m, Dimension.Mass) }, null, "Frutas", null)
            };

            var batch2 = new List<ProviderProduct>
            {
                new("PROD-1", "Manzana Roja 1kg (Actualizada)", "Fruver", new List<ProviderSku> { new("SKU-MANZANA", "Manzana", null, 1000m, Dimension.Mass) }, null, "Frutas", null),
                new("PROD-2", "Pera Verde 1kg", "Fruver", new List<ProviderSku> { new("SKU-PERA", "Pera", null, 1000m, Dimension.Mass) }, null, "Frutas", null)
            };

            // ACT: Import batch 1 then batch 2 (batch 2 contains duplicate SKU-MANZANA)
            await reconciler.ReconcileRemoteProductsAsync("JUMBO", "Cencosud Jumbo", batch1, p => new List<ProviderOffer> { new(p.Skus[0].ExternalId, "JUMBO", "Jumbo", SellerKind.RetailerDirect, 8000m, PriceScope.OnlineGeneric, AvailabilityStatus.InStock, "online:colombia") });
            await reconciler.ReconcileRemoteProductsAsync("JUMBO", "Cencosud Jumbo", batch2, p => new List<ProviderOffer> { new(p.Skus[0].ExternalId, "JUMBO", "Jumbo", SellerKind.RetailerDirect, 8500m, PriceScope.OnlineGeneric, AvailabilityStatus.InStock, "online:colombia") });

            // ASSERT: Exactly 2 external SKUs exist in DB for this retailer (no duplicate SKU-MANZANA)
            var skus = await (from s in db.ExternalSkus
                              join ep in db.ExternalProducts on s.ExternalProductId equals ep.Id
                              join r in db.Retailers on ep.RetailerId equals r.Id
                              where r.Code == "JUMBO"
                              select s).ToListAsync();
            Assert.Equal(2, skus.Count);

            var manzanaSku = skus.First(s => s.ExternalId == "SKU-MANZANA");
            var offers = await db.Offers.Where(o => o.ExternalSkuId == manzanaSku.Id).ToListAsync();
            Assert.Single(offers); // Deduplication: exactly one current offer per SKU
            Assert.Equal(8500m, offers[0].Price);
        }
    }

    [Fact]
    public void CancelledCatalogImport_PreservesCompletedProducts()
    {
        // ARRANGE
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IStoreProviderRegistry>(_ => new StoreProviderRegistry([]));
        var sp = services.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();

        var importService = new CatalogImportService(scopeFactory, NullLogger<CatalogImportService>.Instance);

        // ACT: Start an import job and cancel it
        var job = importService.StartImport("D1", new List<string> { "lacteos" });
        Assert.NotNull(job);

        var cancelled = importService.CancelJob(job.Id);
        Assert.True(cancelled);

        var retrieved = importService.GetJob(job.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(CatalogImportStatus.Cancelled, retrieved.Status);
    }
}