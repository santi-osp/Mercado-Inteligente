using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MercadoInteligente.Tests;

public sealed class Goal12ContextAndRegressionTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalPaths _paths;

    public Goal12ContextAndRegressionTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mercado_goal12_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "data"));
        _paths = new LocalPaths(_tempDir);
    }

    public void Dispose()
    {
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
    public async Task Optimizer_DoesNotUseCheaperOfferFromWrongContext()
    {
        // ARRANGE:
        // ShoppingContext: Medellin (D1 fulfillment:medellin-01)
        // Offers:
        //  1. D1 Bogota: $7.000 (FulfillmentRegion, fulfillment:bogota-01) - CHEAPER but INCOMPATIBLE
        //  2. D1 OnlineGeneric: $8.000 (OnlineGeneric) - CHEAPER than Medellin, but FALLBACK
        //  3. D1 Medellin: $9.000 (FulfillmentRegion, fulfillment:medellin-01) - TARGET COMPATIBLE
        var options = new DbContextOptionsBuilder<MarketDb>()
            .UseSqlite(new SqliteConnectionStringBuilder
            {
                DataSource = _paths.Database,
                ForeignKeys = true,
                Pooling = false
            }.ToString()).Options;

        await using var db = new MarketDb(options);
        await db.Database.MigrateAsync();

        var retailer = new Retailer { Code = "D1", Name = "Tiendas D1" };
        var seller = new Seller { Name = "D1 Directo", Kind = SellerKind.RetailerDirect, RetailerId = retailer.Id };
        var product = new ExternalProduct
        {
            RetailerId = retailer.Id,
            ExternalId = "D1-LECHE-1L",
            Name = "Leche Entera 1L",
            Brand = "Latti",
            Status = ProductLifecycleState.Active
        };
        var sku = new ExternalSku
        {
            ExternalProductId = product.Id,
            ExternalId = "SKU-D1-LECHE",
            Name = "Leche Entera 1000ml",
            Ean = "7701234567890",
            Quantity = 1000m,
            Dimension = Dimension.Volume
        };

        db.Retailers.Add(retailer);
        db.Sellers.Add(seller);
        db.ExternalProducts.Add(product);
        db.ExternalSkus.Add(sku);

        var offerBogota = new Offer
        {
            ExternalSkuId = sku.Id,
            SellerId = seller.Id,
            Price = 7000m,
            ListPrice = 7500m,
            Scope = PriceScope.FulfillmentRegion,
            Context = "fulfillment:bogota-01",
            Availability = AvailabilityStatus.InStock,
            CheckedAt = DateTime.UtcNow
        };
        var offerGeneric = new Offer
        {
            ExternalSkuId = sku.Id,
            SellerId = seller.Id,
            Price = 8000m,
            ListPrice = 8500m,
            Scope = PriceScope.OnlineGeneric,
            Context = "",
            Availability = AvailabilityStatus.InStock,
            CheckedAt = DateTime.UtcNow
        };
        var offerMedellin = new Offer
        {
            ExternalSkuId = sku.Id,
            SellerId = seller.Id,
            Price = 9000m,
            ListPrice = 9500m,
            Scope = PriceScope.FulfillmentRegion,
            Context = "fulfillment:medellin-01",
            Availability = AvailabilityStatus.InStock,
            CheckedAt = DateTime.UtcNow
        };

        db.Offers.AddRange(offerBogota, offerGeneric, offerMedellin);

        var basket = new Basket
        {
            Items = [new BasketItem { Query = "Leche Entera", Quantity = 1m }]
        };
        db.Baskets.Add(basket);
        await db.SaveChangesAsync();

        var d1Context = new EffectiveProviderContext(
            RetailerId: retailer.Id,
            ProviderCode: "D1",
            RequestedLocationProfileId: Guid.NewGuid(),
            PriceScope: PriceScope.FulfillmentRegion,
            PriceContext: "fulfillment:medellin-01",
            ExternalContextId: "medellin-01",
            RegionId: "MED",
            FulfillmentRegion: "medellin-01",
            SalesChannel: "1",
            ResolutionStatus: ContextResolutionStatus.Resolved,
            IsFallback: false,
            ResolvedAt: DateTime.UtcNow
        );

        var shoppingContext = new ShoppingContext(
            LocationProfileId: d1Context.RequestedLocationProfileId,
            NormalizedAddress: "Medellin",
            Latitude: 6.24m,
            Longitude: -75.59m,
            ProviderContexts: [d1Context],
            ResolvedAt: DateTime.UtcNow,
            ValidationStatus: "LOCATION_RESOLVED",
            HasFallbackProviders: false
        );

        var policy = new OfferContextCompatibilityService();

        // ACT:
        var loadedOffers = await CatalogOfferLoader.LoadOffersAsync(db, shoppingContext, policy, CancellationToken.None);

        // ASSERT loaded offers filtering:
        // 1. Bogota offer MUST be filtered out completely
        Assert.DoesNotContain(loadedOffers, o => o.OfferId == offerBogota.Id);

        // 2. Medellin offer is loaded with IsFallbackContext = false
        var medLoaded = loadedOffers.Single(o => o.OfferId == offerMedellin.Id);
        Assert.False(medLoaded.IsFallbackContext);

        // 3. Generic offer is loaded as Fallback
        var genLoaded = loadedOffers.Single(o => o.OfferId == offerGeneric.Id);
        Assert.True(genLoaded.IsFallbackContext);

        // OPTIMIZE:
        var solution = BasketOptimizer.Optimize(
            basket,
            loadedOffers,
            new AppSettings(),
            options: new OptimizerExecutionOptions { ShoppingContext = shoppingContext });

        // ASSERT: Optimizer MUST select the $9.000 Medellin offer, NOT the cheaper generic $8.000 or Bogota $7.000
        Assert.Equal(1, solution.ItemsCoveredCount);
        var assignment = solution.Assignments.Single();
        Assert.Equal(offerMedellin.Id, assignment.Candidate.OfferId);
        Assert.Equal(9000m, assignment.Candidate.TotalCost);
        Assert.Equal(PriceScope.FulfillmentRegion, assignment.Candidate.Scope);
        Assert.Equal("fulfillment:medellin-01", assignment.Candidate.Context);
        Assert.False(assignment.Candidate.IsFallbackContext);
    }

    [Fact]
    public async Task Optimizer_UsesGenericOnlineOnlyWhenRegionalOfferMissing()
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

        var retailer = new Retailer { Code = "D1", Name = "Tiendas D1" };
        var seller = new Seller { Name = "D1 Directo", Kind = SellerKind.RetailerDirect, RetailerId = retailer.Id };
        var product = new ExternalProduct
        {
            RetailerId = retailer.Id,
            ExternalId = "D1-ARROZ-1K",
            Name = "Arroz Diana 1kg",
            Brand = "Diana",
            Status = ProductLifecycleState.Active
        };
        var sku = new ExternalSku
        {
            ExternalProductId = product.Id,
            ExternalId = "SKU-D1-ARROZ",
            Name = "Arroz 1000g",
            Ean = "7709999999999",
            Quantity = 1000m,
            Dimension = Dimension.Mass
        };

        db.Retailers.Add(retailer);
        db.Sellers.Add(seller);
        db.ExternalProducts.Add(product);
        db.ExternalSkus.Add(sku);

        // Only OnlineGeneric is available for this product
        var offerGeneric = new Offer
        {
            ExternalSkuId = sku.Id,
            SellerId = seller.Id,
            Price = 4200m,
            ListPrice = 4500m,
            Scope = PriceScope.OnlineGeneric,
            Context = "",
            Availability = AvailabilityStatus.InStock,
            CheckedAt = DateTime.UtcNow
        };
        db.Offers.Add(offerGeneric);

        var basket = new Basket
        {
            Items = [new BasketItem { Query = "Arroz Diana", Quantity = 1m }]
        };
        db.Baskets.Add(basket);
        await db.SaveChangesAsync();

        var d1Context = new EffectiveProviderContext(
            RetailerId: retailer.Id,
            ProviderCode: "D1",
            RequestedLocationProfileId: Guid.NewGuid(),
            PriceScope: PriceScope.FulfillmentRegion,
            PriceContext: "fulfillment:medellin-01",
            ExternalContextId: "medellin-01",
            RegionId: "MED",
            FulfillmentRegion: "medellin-01",
            SalesChannel: "1",
            ResolutionStatus: ContextResolutionStatus.Resolved,
            IsFallback: false,
            ResolvedAt: DateTime.UtcNow
        );

        var shoppingContext = new ShoppingContext(
            LocationProfileId: d1Context.RequestedLocationProfileId,
            NormalizedAddress: "Medellin",
            Latitude: 6.24m,
            Longitude: -75.59m,
            ProviderContexts: [d1Context],
            ResolvedAt: DateTime.UtcNow,
            ValidationStatus: "LOCATION_RESOLVED",
            HasFallbackProviders: false
        );

        var policy = new OfferContextCompatibilityService();
        var loadedOffers = await CatalogOfferLoader.LoadOffersAsync(db, shoppingContext, policy, CancellationToken.None);

        var genLoaded = loadedOffers.Single();
        Assert.True(genLoaded.IsFallbackContext);
        Assert.NotNull(genLoaded.ContextWarning);

        var solution = BasketOptimizer.Optimize(
            basket,
            loadedOffers,
            new AppSettings(),
            options: new OptimizerExecutionOptions { ShoppingContext = shoppingContext });

        Assert.Equal(1, solution.ItemsCoveredCount);
        var assignment = solution.Assignments.Single();
        Assert.Equal(4200m, assignment.Candidate.TotalCost);
        Assert.True(assignment.Candidate.IsFallbackContext);
    }

    [Fact]
    public async Task SavedLocationProfile_FromUiContainsStructuredAddressComponents()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("MARKET_DATA_DIR", _tempDir));

        var client = factory.CreateClient();

        var payload = new
        {
            label = "Casa San Diego",
            address = "Calle 33 # 42-15",
            formattedAddress = "Cl. 33 #42-15, La Candelaria, Medellín, Antioquia, Colombia",
            latitude = 6.2361m,
            longitude = -75.5682m,
            country = "Colombia",
            countryCode = "CO",
            state = "Antioquia",
            stateCode = "ANT",
            city = "Medellín",
            postalCode = "050016",
            neighborhood = "San Diego",
            street = "Calle 33",
            number = "42-15"
        };

        var response = await client.PostAsync("/api/v1/location/profiles",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var createdProfile = await response.Content.ReadFromJsonAsync<LocationProfile>();
        Assert.NotNull(createdProfile);
        Assert.Equal("Casa San Diego", createdProfile.Label);
        Assert.Equal("Calle 33", createdProfile.Street);
        Assert.Equal("42-15", createdProfile.Number);
        Assert.Equal("San Diego", createdProfile.Neighborhood);
        Assert.Equal("050016", createdProfile.PostalCode);
        Assert.Equal("Medellín", createdProfile.City);
        Assert.Equal("Antioquia", createdProfile.State);
        Assert.Equal("Colombia", createdProfile.Country);

        // Verify direct DB persistence
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
        var dbProfile = await db.LocationProfiles.SingleOrDefaultAsync(p => p.Id == createdProfile.Id);

        Assert.NotNull(dbProfile);
        Assert.Equal("Calle 33", dbProfile.Street);
        Assert.Equal("42-15", dbProfile.Number);
        Assert.Equal("San Diego", dbProfile.Neighborhood);
        Assert.Equal("050016", dbProfile.PostalCode);
        Assert.Equal("Medellín", dbProfile.City);
        Assert.Equal("Antioquia", dbProfile.State);
        Assert.Equal("Colombia", dbProfile.Country);
        Assert.Equal("Cl. 33 #42-15, La Candelaria, Medellín, Antioquia, Colombia", dbProfile.FormattedAddress);

        // Test PUT updates fields and timestamps
        var updatePayload = new
        {
            label = "Casa San Diego Actualizada",
            address = "Calle 33 # 42-15 Apto 402",
            formattedAddress = dbProfile.FormattedAddress,
            latitude = dbProfile.Latitude,
            longitude = dbProfile.Longitude,
            country = "Colombia",
            countryCode = "CO",
            state = "Antioquia",
            stateCode = "ANT",
            city = "Medellín",
            postalCode = "050016",
            neighborhood = "San Diego Central",
            street = "Calle 33",
            number = "42-15 Int 402"
        };

        var putResponse = await client.PutAsync($"/api/v1/location/profiles/{dbProfile.Id}",
            new StringContent(JsonSerializer.Serialize(updatePayload), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var updatedFromDb = await db.LocationProfiles.AsNoTracking().SingleAsync(p => p.Id == dbProfile.Id);
        Assert.Equal("Casa San Diego Actualizada", updatedFromDb.Label);
        Assert.Equal("San Diego Central", updatedFromDb.Neighborhood);
        Assert.Equal("42-15 Int 402", updatedFromDb.Number);
        Assert.True(updatedFromDb.UpdatedAt >= updatedFromDb.CreatedAt);
    }

    [Fact]
    public async Task HistoricalCsv_UsesObservationSnapshot_NotCurrentOffer()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("MARKET_DATA_DIR", _tempDir));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();

            var retailer = new Retailer { Code = "D1", Name = "Tiendas D1" };
            var seller = new Seller { Name = "D1 Central", Kind = SellerKind.RetailerDirect, RetailerId = retailer.Id };
            var product = new ExternalProduct
            {
                RetailerId = retailer.Id,
                ExternalId = "D1-CAFE-500",
                Name = "Café Molido 500g",
                Brand = "Aroma",
                Status = ProductLifecycleState.Active
            };
            var sku = new ExternalSku
            {
                ExternalProductId = product.Id,
                ExternalId = "SKU-D1-CAFE",
                Name = "Café Molido 500g",
                Ean = "7708888888888",
                Quantity = 500m,
                Dimension = Dimension.Mass
            };

            db.Retailers.Add(retailer);
            db.Sellers.Add(seller);
            db.ExternalProducts.Add(product);
            db.ExternalSkus.Add(sku);

            // T1: Initial offer state
            var t1Time = DateTime.UtcNow.AddDays(-2);
            var offer = new Offer
            {
                ExternalSkuId = sku.Id,
                SellerId = seller.Id,
                Price = 10000m,
                ListPrice = 12000m,
                Scope = PriceScope.FulfillmentRegion,
                Context = "fulfillment:medellin-1",
                PromotionConditions = null,
                AvailableQuantityRaw = 10m,
                Availability = AvailabilityStatus.InStock,
                CheckedAt = t1Time
            };
            db.Offers.Add(offer);
            await db.SaveChangesAsync();

            var obs1 = PriceObservationFactory.FromOfferSnapshot(offer, t1Time);
            db.PriceObservations.Add(obs1);
            await db.SaveChangesAsync();

            // T2: Mutated offer state with promotion
            var t2Time = DateTime.UtcNow.AddDays(-1);
            offer.Price = 8000m;
            offer.ListPrice = 10000m;
            offer.PromotionConditions = "2x1 Promoción Especial";
            offer.AvailableQuantityRaw = 5m;
            offer.CheckedAt = t2Time;
            await db.SaveChangesAsync();

            var obs2 = PriceObservationFactory.FromOfferSnapshot(offer, t2Time);
            db.PriceObservations.Add(obs2);
            await db.SaveChangesAsync();

            // T3: Final current offer mutation (price $15,000, 3x2, OnlineGeneric)
            offer.Price = 15000m;
            offer.ListPrice = 18000m;
            offer.PromotionConditions = "3x2 Promo T3";
            offer.Scope = PriceScope.OnlineGeneric;
            offer.Context = "online-general";
            offer.AvailableQuantityRaw = 2m;
            offer.CheckedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var client = factory.CreateClient();
        var csvResponse = await client.GetAsync("/api/v1/export/prices.csv");
        Assert.Equal(HttpStatusCode.OK, csvResponse.StatusCode);

        var csvContent = await csvResponse.Content.ReadAsStringAsync();
        var lines = csvContent.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

        // Header + 2 observation rows
        Assert.True(lines.Length >= 3, $"Expected header + 2 observations, got: {csvContent}");
        Assert.Contains("Fecha,Cadena,Vendedor,TipoVendedor,Producto,Marca,EAN,Precio,PrecioLista,Disponibilidad,Alcance,Contexto,CondicionesPromo,CantidadDisponible,StoreId,SellerId", lines[0]);

        // Find T1 observation row (price 10000.00)
        var t1Row = lines.FirstOrDefault(l => l.Contains(",10000.00,12000.00,"));
        Assert.NotNull(t1Row);
        // T1 row MUST NOT have promotion condition from T2 or T3
        Assert.DoesNotContain("2x1", t1Row);
        Assert.DoesNotContain("3x2", t1Row);
        Assert.Contains("12000.00", t1Row);
        Assert.Contains("FulfillmentRegion", t1Row);

        // Find T2 observation row (price 8000.00)
        var t2Row = lines.FirstOrDefault(l => l.Contains(",8000.00,10000.00,"));
        Assert.NotNull(t2Row);
        // T2 row MUST contain its own promotion ("2x1"), but NOT T3 promo ("3x2") or T3 price ("15000")
        Assert.Contains("2x1 Promoción Especial", t2Row);
        Assert.DoesNotContain("3x2 Promo T3", t2Row);
        Assert.Contains("10000.00", t2Row);
        Assert.Contains("FulfillmentRegion", t2Row);

        // Verify that current mutated price (15000.00) does NOT appear in historical observations export
        Assert.DoesNotContain("15000.00", csvContent);
        Assert.DoesNotContain("3x2 Promo T3", csvContent);
    }

    [Fact]
    public async Task MotorcycleRouting_UsesTwoWheeler_WhenSupported()
    {
        var secrets = new FakeSecretStore();
        secrets.Set("google", "test-api-key");
        var tracker = new GoogleServiceUsageTracker(_paths);

        // Case A: TWO_WHEELER succeeds directly
        using var clientOk = new HttpClient(new MockHttpHandler(req =>
        {
            var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("\"travelMode\":\"TWO_WHEELER\"", body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"routes\":[{\"distanceMeters\":4200,\"duration\":\"480s\"}]}", Encoding.UTF8, "application/json")
            };
        }));

        var provider = new GoogleRoutingProvider(secrets, clientOk, _paths, tracker);
        var route = await provider.GetRouteAsync(6.24m, -75.59m, 6.21m, -75.57m, TransportMode.Motorcycle, CancellationToken.None);

        Assert.Equal(RouteType.RoadRoute, route.Type);
        Assert.Equal(4.2m, route.DistanceKm);
        Assert.Equal(8.0m, route.DurationMinutes);
        Assert.Equal("MOTORCYCLE", route.RoutingModeRequested);
        Assert.Equal("TWO_WHEELER", route.RoutingModeUsed);
        Assert.False(route.RoutingFallback);
        Assert.Null(route.FallbackMessage);
    }

    [Fact]
    public async Task MotorcycleRouting_FallsBackToDrive_WhenTwoWheelerFailsOrUnsupported()
    {
        var secrets = new FakeSecretStore();
        secrets.Set("google", "test-api-key");
        var tracker = new GoogleServiceUsageTracker(_paths);

        // Case B: TWO_WHEELER fails (400 or empty), second call DRIVE succeeds
        var callCount = 0;
        using var clientFallback = new HttpClient(new MockHttpHandler(req =>
        {
            callCount++;
            var body = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            if (body.Contains("\"travelMode\":\"TWO_WHEELER\""))
            {
                // Simulate unsupported mode in region
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("{\"error\":{\"code\":400,\"message\":\"TWO_WHEELER not supported in this region\"}}", Encoding.UTF8, "application/json")
                };
            }
            if (body.Contains("\"travelMode\":\"DRIVE\""))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"routes\":[{\"distanceMeters\":4500,\"duration\":\"600s\"}]}", Encoding.UTF8, "application/json")
                };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var provider = new GoogleRoutingProvider(secrets, clientFallback, _paths, tracker);
        var route = await provider.GetRouteAsync(6.24m, -75.59m, 6.21m, -75.57m, TransportMode.Motorcycle, CancellationToken.None);

        Assert.Equal(2, callCount);
        Assert.Equal(RouteType.RoadRoute, route.Type);
        Assert.Equal(4.5m, route.DistanceKm);
        Assert.Equal(10.0m, route.DurationMinutes);
        Assert.Equal("MOTORCYCLE", route.RoutingModeRequested);
        Assert.Equal("DRIVE", route.RoutingModeUsed);
        Assert.True(route.RoutingFallback);
        Assert.Equal("Ruta aproximada usando modo automóvil.", route.FallbackMessage);
    }

    [Fact]
    public void ContextChange_InvalidatesEffectiveProviderContext()
    {
        var policy = new OfferContextCompatibilityService();

        var medellinContext = new EffectiveProviderContext(
            RetailerId: Guid.NewGuid(),
            ProviderCode: "D1",
            RequestedLocationProfileId: Guid.NewGuid(),
            PriceScope: PriceScope.FulfillmentRegion,
            PriceContext: "fulfillment:medellin-1",
            ExternalContextId: "medellin-1",
            RegionId: "MED",
            FulfillmentRegion: "medellin-1",
            SalesChannel: "1",
            ResolutionStatus: ContextResolutionStatus.Resolved,
            IsFallback: false,
            ResolvedAt: DateTime.UtcNow
        );

        var bogotaContext = new EffectiveProviderContext(
            RetailerId: medellinContext.RetailerId,
            ProviderCode: "D1",
            RequestedLocationProfileId: Guid.NewGuid(),
            PriceScope: PriceScope.FulfillmentRegion,
            PriceContext: "fulfillment:bogota-1",
            ExternalContextId: "bogota-1",
            RegionId: "BOG",
            FulfillmentRegion: "bogota-1",
            SalesChannel: "1",
            ResolutionStatus: ContextResolutionStatus.Resolved,
            IsFallback: false,
            ResolvedAt: DateTime.UtcNow
        );

        // Evaluation under Medellin context
        var evalMedUnderMed = policy.Evaluate(PriceScope.FulfillmentRegion, "fulfillment:medellin-1", "D1", medellinContext);
        var evalBogUnderMed = policy.Evaluate(PriceScope.FulfillmentRegion, "fulfillment:bogota-1", "D1", medellinContext);

        Assert.Equal(OfferCompatibility.Compatible, evalMedUnderMed.Compatibility);
        Assert.False(evalMedUnderMed.IsFallback);
        Assert.Equal(OfferCompatibility.Incompatible, evalBogUnderMed.Compatibility);

        // Changing location to Bogota: evaluation is strictly reversed
        var evalMedUnderBog = policy.Evaluate(PriceScope.FulfillmentRegion, "fulfillment:medellin-1", "D1", bogotaContext);
        var evalBogUnderBog = policy.Evaluate(PriceScope.FulfillmentRegion, "fulfillment:bogota-1", "D1", bogotaContext);

        Assert.Equal(OfferCompatibility.Incompatible, evalMedUnderBog.Compatibility);
        Assert.Equal(OfferCompatibility.Compatible, evalBogUnderBog.Compatibility);
        Assert.False(evalBogUnderBog.IsFallback);
    }

    [Fact]
    public void TravelLocation_SeparatedFromPriceScope_DoesNotMutateStoreIdOrScope()
    {
        var basket = new Basket
        {
            Items = [new BasketItem { Query = "Aceite", Quantity = 1m }]
        };

        var offer = new RawCatalogOffer(
            OfferId: Guid.NewGuid(),
            ExternalSkuId: Guid.NewGuid(),
            ExternalProductId: Guid.NewGuid(),
            RetailerId: Guid.NewGuid(),
            RetailerCode: "D1",
            RetailerName: "D1",
            StoreId: null, // Online regional offer has NO store id
            StoreName: null,
            SellerId: Guid.NewGuid(),
            SellerName: "D1",
            SellerKind: SellerKind.RetailerDirect,
            ProductName: "Aceite Vegetal 900ml",
            Brand: "Oleocali",
            SkuName: "Aceite 900ml",
            Ean: "7701111111111",
            SkuQuantity: 900m,
            SkuDimension: Dimension.Volume,
            Price: 7500m,
            ListPrice: 8000m,
            Scope: PriceScope.FulfillmentRegion, // Online regional scope
            Availability: AvailabilityStatus.InStock,
            Context: "fulfillment:medellin-1",
            PromotionConditions: null,
            CheckedAt: DateTime.UtcNow,
            IsFallbackContext: false,
            ContextWarning: null
        );

        var physicalStore = new Store
        {
            Id = Guid.NewGuid(),
            RetailerId = offer.RetailerId,
            ExternalId = "D1-LAURELES",
            Name = "D1 Laureles",
            Latitude = 6.2425m,
            Longitude = -75.5925m,
            VerificationStatus = "DEMO"
        };

        var storeMap = new Dictionary<string, Store>
        {
            ["D1"] = physicalStore,
            [physicalStore.Id.ToString()] = physicalStore
        };

        var locationContext = new OptimizerLocationContext(
            OriginLatitude: 6.2400m,
            OriginLongitude: -75.5900m,
            ReturnToOrigin: true,
            TransportMode: TransportMode.Driving
        );

        var solution = BasketOptimizer.Optimize(
            basket,
            [offer],
            new AppSettings(),
            options: new OptimizerExecutionOptions
            {
                LocationContext = locationContext,
                StoreMap = storeMap
            });

        Assert.Equal(1, solution.ItemsCoveredCount);
        var storeBucket = solution.Stores.Single();

        // 1. TravelLocation exists and provides physical store proxy for routing
        Assert.NotNull(storeBucket.TravelLocation);
        Assert.True(storeBucket.TravelLocation.IsPhysicalStoreProxy);
        Assert.Equal("Precio online/regional; precio físico no confirmado", storeBucket.TravelLocation.TransparencyNotice);
        Assert.Equal("UNCONFIRMED_ONLINE_REGIONAL", storeBucket.TravelLocation.VerificationStatus);

        // 2. Candidate inside bucket still has StoreId == null and Scope == FulfillmentRegion
        var itemAssignment = storeBucket.Items.Single();
        Assert.Null(itemAssignment.Candidate.StoreId);
        Assert.Equal(PriceScope.FulfillmentRegion, itemAssignment.Candidate.Scope);
        Assert.Equal("fulfillment:medellin-1", itemAssignment.Candidate.Context);
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _store = new();
        public void Delete(string name) => _store.Remove(name);
        public string? Get(string name) => _store.TryGetValue(name, out var val) ? val : null;
        public void Set(string name, string value) => _store[name] = value;
    }

    private sealed class MockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
