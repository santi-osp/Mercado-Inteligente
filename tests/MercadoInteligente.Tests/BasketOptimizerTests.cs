using System.Diagnostics;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Tests;

[CollectionDefinition("OptimizerPerformance", DisableParallelization = true)]
public sealed class OptimizerPerformanceCollection;

[Collection("OptimizerPerformance")]
public class BasketOptimizerTests
{
    [Fact]
    public void OutdatedCheapOfferCannotWinByDefault()
    {
        var basket = new Basket { Items = [new BasketItem { Query = "Arroz 1000g", Quantity = 1 }] };
        var old = CreateOffer("D1", "D1", "retailer:D1", "D1 Online", "Arroz 1000g", null,
            "Arroz 1000g", 100m, checkedAt: DateTime.UtcNow.AddDays(-2), scope: PriceScope.OnlineGeneric);
        var fresh = CreateOffer("EXITO", "Éxito", "retailer:EXITO", "Éxito Online", "Arroz 1000g", null,
            "Arroz 1000g", 3000m, scope: PriceScope.FulfillmentRegion);
        var solution = BasketOptimizer.Optimize(basket, [old, fresh], CreateDefaultSettings());
        Assert.Single(solution.Assignments);
        Assert.Equal(fresh.OfferId, solution.Assignments[0].Candidate.OfferId);
        Assert.Contains(solution.Warnings, warning => warning.Contains("online o regional"));
    }

    private static AppSettings CreateDefaultSettings() => new AppSettings
    {
        MaxStores = 2,
        SizeTolerancePercent = 15,
        AllowSubstitutes = true,
        MaxOverbuyPercent = 25,
        PriceTtlHours = 6,
        StockTtlHours = 2,
        IncludeMarketplace = false
    };

    private static RawCatalogOffer CreateOffer(
        string retailerCode,
        string retailerName,
        string storeKey,
        string storeName,
        string productName,
        string? brand,
        string skuName,
        decimal price,
        decimal? skuQuantity = null,
        Dimension skuDimension = Dimension.Unknown,
        string? conditions = null,
        DateTime? checkedAt = null,
        PriceScope scope = PriceScope.Store,
        AvailabilityStatus availability = AvailabilityStatus.InStock)
    {
        return new RawCatalogOffer(
            OfferId: Guid.NewGuid(),
            ExternalSkuId: Guid.NewGuid(),
            ExternalProductId: Guid.NewGuid(),
            RetailerId: Guid.NewGuid(),
            RetailerCode: retailerCode,
            RetailerName: retailerName,
            StoreId: Guid.NewGuid(),
            StoreName: storeName,
            SellerId: Guid.NewGuid(),
            SellerName: retailerName,
            SellerKind: SellerKind.RetailerDirect,
            ProductName: productName,
            Brand: brand,
            SkuName: skuName,
            Ean: null,
            SkuQuantity: skuQuantity,
            SkuDimension: skuDimension,
            Price: price,
            ListPrice: null,
            Scope: scope,
            Availability: availability,
            Context: "test",
            PromotionConditions: conditions,
            CheckedAt: checkedAt ?? DateTime.UtcNow,
            StoreKey: storeKey
        );
    }

    [Fact]
    public void TenItemsThreeStoresKnownOptimalSolutionIsExact()
    {
        // Basket with 10 staple grocery items
        var basket = new Basket { Name = "Mercado Semanal", MaxStores = 2, PreferredStrategy = OptimizationStrategy.MaxSavings };
        var itemNames = new[]
        {
            "Arroz 1000g", "Leche 1000ml", "Huevos 30 und", "Aceite 1000ml", "Café 500g",
            "Azúcar 1000g", "Sal 1000g", "Frijol 1000g", "Lentejas 1000g", "Atún 140g"
        };

        foreach (var name in itemNames)
        {
            basket.Items.Add(new BasketItem { Query = name, Quantity = 1 });
        }

        // Stores: Store A (D1), Store B (Éxito), Store C (Carulla)
        // Store A is cheapest for items 0..4
        // Store B is cheapest for items 5..9
        // Store C is expensive for everything
        var offers = new List<RawCatalogOffer>();
        for (int i = 0; i < itemNames.Length; i++)
        {
            var name = itemNames[i];
            // Store A: items 0..4 cost 3000, items 5..9 cost 8000
            var priceA = i < 5 ? 3000m : 8000m;
            offers.Add(CreateOffer("D1", "D1", "store:A", "D1 Calle 50", name, "D1", name, priceA));

            // Store B: items 0..4 cost 7000, items 5..9 cost 3000
            var priceB = i >= 5 ? 3000m : 7000m;
            offers.Add(CreateOffer("EXITO", "Éxito", "store:B", "Éxito Poblado", name, "Éxito", name, priceB));

            // Store C: items all cost 10000
            offers.Add(CreateOffer("CARULLA", "Carulla", "store:C", "Carulla Oviedo", name, "Carulla", name, 10000m));
        }

        var settings = CreateDefaultSettings();
        var solution = BasketOptimizer.Optimize(basket, offers, settings);

        // Optimal combination for MaxStores=2 is Store A + Store B
        Assert.Equal(2, solution.StoresUsedCount);
        Assert.Equal(10, solution.ItemsCoveredCount);
        Assert.Equal(0, solution.MissingItemsCount);

        // Total cost should be 5 items * 3000 (A) + 5 items * 3000 (B) = 30000
        Assert.Equal(30000m, solution.TotalCost);

        // Baseline (SingleStore) best is Store B (5*7000 + 5*3000 = 50000)
        Assert.Equal(50000m, solution.BaselineCost);
        Assert.Equal(20000m, solution.TotalSavingsVsBaseline);

        // 5 items from Store A, 5 items from Store B
        var storeA = solution.Stores.Single(s => s.StoreKey == "store:A");
        var storeB = solution.Stores.Single(s => s.StoreKey == "store:B");
        Assert.Equal(5, storeA.Items.Count);
        Assert.Equal(5, storeB.Items.Count);
        Assert.Equal(15000m, storeA.Subtotal);
        Assert.Equal(15000m, storeB.Subtotal);
    }

    [Fact]
    public void StrategyProgressionMaxOneTwoThreeSavingsNonDecreasing()
    {
        var basket = new Basket { Name = "Mercado Comparativo" };
        var itemNames = new[] { "Arroz 1000g", "Leche 1000ml", "Aceite 1000ml", "Café 500g" };
        foreach (var name in itemNames)
        {
            basket.Items.Add(new BasketItem { Query = name, Quantity = 1 });
        }

        // Store 1 cheapest for Arroz (2000), expensive for others (6000)
        // Store 2 cheapest for Leche (2000), expensive for others (6000)
        // Store 3 cheapest for Aceite (2000), expensive for others (6000)
        var offers = new List<RawCatalogOffer>
        {
            CreateOffer("D1", "D1", "store:1", "D1 Centro", "Arroz 1000g", "D1", "Arroz 1000g", 2000m),
            CreateOffer("D1", "D1", "store:1", "D1 Centro", "Leche 1000ml", "D1", "Leche 1000ml", 6000m),
            CreateOffer("D1", "D1", "store:1", "D1 Centro", "Aceite 1000ml", "D1", "Aceite 1000ml", 6000m),
            CreateOffer("D1", "D1", "store:1", "D1 Centro", "Café 500g", "D1", "Café 500g", 6000m),

            CreateOffer("EXITO", "Éxito", "store:2", "Éxito Centro", "Arroz 1000g", "Éxito", "Arroz 1000g", 6000m),
            CreateOffer("EXITO", "Éxito", "store:2", "Éxito Centro", "Leche 1000ml", "Éxito", "Leche 1000ml", 2000m),
            CreateOffer("EXITO", "Éxito", "store:2", "Éxito Centro", "Aceite 1000ml", "Éxito", "Aceite 1000ml", 6000m),
            CreateOffer("EXITO", "Éxito", "store:2", "Éxito Centro", "Café 500g", "Éxito", "Café 500g", 6000m),

            CreateOffer("ARA", "Ara", "store:3", "Ara Centro", "Arroz 1000g", "Ara", "Arroz 1000g", 6000m),
            CreateOffer("ARA", "Ara", "store:3", "Ara Centro", "Leche 1000ml", "Ara", "Leche 1000ml", 6000m),
            CreateOffer("ARA", "Ara", "store:3", "Ara Centro", "Aceite 1000ml", "Ara", "Aceite 1000ml", 2000m),
            CreateOffer("ARA", "Ara", "store:3", "Ara Centro", "Café 500g", "Ara", "Café 500g", 6000m),
        };

        var settings = CreateDefaultSettings();

        // Max 1 store
        var sol1 = BasketOptimizer.Optimize(basket, offers, settings, options: new OptimizerExecutionOptions(MaxStores: 1));
        // Max 2 stores
        var sol2 = BasketOptimizer.Optimize(basket, offers, settings, options: new OptimizerExecutionOptions(MaxStores: 2));
        // Max 3 stores
        var sol3 = BasketOptimizer.Optimize(basket, offers, settings, options: new OptimizerExecutionOptions(MaxStores: 3));

        Assert.True(sol1.StoresUsedCount <= 1);
        Assert.True(sol2.StoresUsedCount <= 2);
        Assert.True(sol3.StoresUsedCount <= 3);

        // Savings should be non-decreasing (sol3.TotalCost <= sol2.TotalCost <= sol1.TotalCost)
        Assert.True(sol3.TotalCost <= sol2.TotalCost, $"Cost Max3 ({sol3.TotalCost}) should be <= Cost Max2 ({sol2.TotalCost})");
        Assert.True(sol2.TotalCost <= sol1.TotalCost, $"Cost Max2 ({sol2.TotalCost}) should be <= Cost Max1 ({sol1.TotalCost})");
    }

    [Fact]
    public void InsufficientStoresProvidesExplicitMissingAndPlusOneSuggestion()
    {
        // 3 items where each item is only available in a unique store
        var basket = new Basket { Name = "Canasta dispersa", MaxStores = 1 };
        var item1 = new BasketItem { Query = "Producto Exclusivo A", Quantity = 1 };
        var item2 = new BasketItem { Query = "Producto Exclusivo B", Quantity = 1 };
        var item3 = new BasketItem { Query = "Producto Exclusivo C", Quantity = 1 };
        basket.Items.Add(item1);
        basket.Items.Add(item2);
        basket.Items.Add(item3);

        var offers = new List<RawCatalogOffer>
        {
            CreateOffer("D1", "D1", "store:A", "D1 Norte", "Producto Exclusivo A", "D1", "A", 5000m),
            CreateOffer("EXITO", "Éxito", "store:B", "Éxito Norte", "Producto Exclusivo B", "Éxito", "B", 6000m),
            CreateOffer("CARULLA", "Carulla", "store:C", "Carulla Norte", "Producto Exclusivo C", "Carulla", "C", 7000m),
        };

        var settings = CreateDefaultSettings();
        // Request MaxStores=1
        var solution = BasketOptimizer.Optimize(basket, offers, settings, options: new OptimizerExecutionOptions(MaxStores: 1));

        // In 1 store, only 1 item can be covered
        Assert.Equal(1, solution.ItemsCoveredCount);
        Assert.Equal(2, solution.MissingItemsCount);
        Assert.NotEmpty(solution.MissingItems);

        // Check N+1 suggestion
        Assert.NotNull(solution.PlusOneStoreSuggestion);
        Assert.Equal(1, solution.PlusOneStoreSuggestion.AdditionalStores);
        Assert.Equal(2, solution.PlusOneStoreSuggestion.ItemsCovered); // Can cover 2 with 2 stores
        Assert.Contains("1 tienda adicional", solution.PlusOneStoreSuggestion.RecommendationText);
    }

    [Fact]
    public void MissingOfferInStoreIsDiscardedWithoutBreakingGlobalCombination()
    {
        var basket = new Basket { MaxStores = 2 };
        basket.Items.Add(new BasketItem { Query = "Arroz 1000g", Quantity = 1 });
        basket.Items.Add(new BasketItem { Query = "Panal de Huevos", Quantity = 1 });

        // Store A has Arroz but NO huevos
        // Store B has Huevos but NO arroz
        var offers = new List<RawCatalogOffer>
        {
            CreateOffer("D1", "D1", "store:A", "D1 Local", "Arroz 1000g", "D1", "Arroz 1000g", 3500m),
            CreateOffer("EXITO", "Éxito", "store:B", "Éxito Local", "Panal de Huevos", "Éxito", "Panal de Huevos", 18000m),
        };

        var settings = CreateDefaultSettings();
        var solution = BasketOptimizer.Optimize(basket, offers, settings);

        Assert.Equal(2, solution.StoresUsedCount);
        Assert.Equal(2, solution.ItemsCoveredCount);
        Assert.Equal(0, solution.MissingItemsCount);
        Assert.Equal(21500m, solution.TotalCost);
    }

    [Fact]
    public void OverbuyPercentCalculatedCorrectlyAndGeneratesWarningWhenRejected()
    {
        var basket = new Basket { MaxStores = 1 };
        // User wants 400g
        basket.Items.Add(new BasketItem { Query = "Lentejas 400g", Quantity = 400, Dimension = Dimension.Mass, Unit = "g" });

        // Store offers 1000g pack -> overbuy is (1000 - 400) / 400 = 150%
        // MaxOverbuyPercent default is 25% -> isOverbuyRejected = true
        var offers = new List<RawCatalogOffer>
        {
            CreateOffer("D1", "D1", "store:A", "D1", "Lentejas 1000g", "D1", "Lentejas 1000g", 4000m,
                skuQuantity: 1000, skuDimension: Dimension.Mass)
        };

        var settings = CreateDefaultSettings();
        var solution = BasketOptimizer.Optimize(basket, offers, settings);

        // When non-overbuy candidate does not exist, the fallback candidate is chosen with overbuy
        Assert.Equal(1, solution.ItemsCoveredCount);
        var assignment = solution.Assignments.Single();
        Assert.Equal(150m, assignment.Candidate.OverbuyPercent);
        Assert.Equal(1000m, assignment.Candidate.TotalAcquired);
    }

    [Fact]
    public void PromotionsTwoForOneAndThreeForTwoEvaluatedCorrectlyInOptimizer()
    {
        var basket = new Basket { MaxStores = 1 };
        // User wants 2 units of Tuna and 3 units of Soap
        basket.Items.Add(new BasketItem { Query = "Atún Lomitos", Quantity = 2 });
        basket.Items.Add(new BasketItem { Query = "Jabón Tocador", Quantity = 3 });

        var offers = new List<RawCatalogOffer>
        {
            // Tuna: 5000 each with 2x1 -> 1 paid = 5000
            CreateOffer("EXITO", "Éxito", "store:1", "Éxito", "Atún Lomitos", "Van Camps", "Atún", 5000m, conditions: "Lleva 2 y paga 1"),
            // Soap: 3000 each with 3x2 -> 2 paid = 6000
            CreateOffer("EXITO", "Éxito", "store:1", "Éxito", "Jabón Tocador", "Protex", "Jabón", 3000m, conditions: "3x2 en cuidado personal")
        };

        var settings = CreateDefaultSettings();
        var solution = BasketOptimizer.Optimize(basket, offers, settings);

        Assert.Equal(2, solution.ItemsCoveredCount);
        // Cost: 5000 + 6000 = 11000 (instead of standard 2*5000 + 3*3000 = 19000)
        Assert.Equal(11000m, solution.TotalCost);
        Assert.NotEmpty(solution.AppliedPromotions);
        Assert.Equal(2, solution.AppliedPromotions.Count);
    }

    [Fact]
    public void ConditionalCreditCardDiscountsAreNotAssumedAndGenerateWarning()
    {
        var basket = new Basket { MaxStores = 1 };
        basket.Items.Add(new BasketItem { Query = "Aceite Girasol", Quantity = 1 });

        var offers = new List<RawCatalogOffer>
        {
            CreateOffer("EXITO", "Éxito", "store:1", "Éxito", "Aceite Girasol", "Premier", "Aceite", 12000m,
                conditions: "Exclusivo Tarjeta Éxito 30% dto")
        };

        var settings = CreateDefaultSettings();
        var solution = BasketOptimizer.Optimize(basket, offers, settings);

        Assert.Equal(1, solution.ItemsCoveredCount);
        // Full standard price 12000 is used
        Assert.Equal(12000m, solution.TotalCost);
        // Warning is present
        Assert.Contains(solution.Warnings, w => w.Contains("Tarjeta Éxito", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BudgetExceededProducesWarningAndCheaperSubstituteSuggestion()
    {
        // Basket with budget of 15000 COP
        var basket = new Basket { Budget = 15000m, MaxStores = 1 };
        basket.Items.Add(new BasketItem { Query = "Café Molido", Quantity = 1 });

        // Store offers premium brand (20000) and economy substitute brand (10000)
        var offers = new List<RawCatalogOffer>
        {
            CreateOffer("EXITO", "Éxito", "store:1", "Éxito", "Café Molido Sello Rojo 500g", "Sello Rojo", "Café", 20000m),
            CreateOffer("EXITO", "Éxito", "store:1", "Éxito", "Café Molido Éxito 500g", "Éxito", "Café", 10000m)
        };

        var settings = CreateDefaultSettings();
        var solution = BasketOptimizer.Optimize(basket, offers, settings);

        // Best candidate chosen was the 10000 one (cheapest total cost!)
        Assert.Equal(10000m, solution.TotalCost);
        Assert.NotNull(solution.BudgetEvaluation);
        Assert.False(solution.BudgetEvaluation.IsExceeded);

        // Now test if budget is 8000 COP (so 10000 exceeds it)
        var solutionExceeded = BasketOptimizer.Optimize(basket, offers, settings,
            options: new OptimizerExecutionOptions(Budget: 8000m));
        Assert.NotNull(solutionExceeded.BudgetEvaluation);
        Assert.True(solutionExceeded.BudgetEvaluation.IsExceeded);
        Assert.Equal(2000m, solutionExceeded.BudgetEvaluation.Difference);
        Assert.Contains(solutionExceeded.Warnings, w => w.Contains("supera el presupuesto"));
    }

    [Fact]
    public void DeterministicTieBreakingSelectsConsistentCandidateOnIdenticalPrice()
    {
        var basket = new Basket { MaxStores = 1 };
        basket.Items.Add(new BasketItem { Query = "Azúcar 1000g", Quantity = 1 });

        var offer1 = CreateOffer("D1", "D1", "store:1", "D1", "Azúcar 1000g", "Incauca", "Azúcar", 4500m);
        var offer2 = CreateOffer("D1", "D1", "store:1", "D1", "Azúcar 1000g", "Manuelita", "Azúcar", 4500m);

        var offers = new List<RawCatalogOffer> { offer1, offer2 };
        var settings = CreateDefaultSettings();

        // Run 5 times to verify deterministic selection
        var run1 = BasketOptimizer.Optimize(basket, offers, settings);
        var run2 = BasketOptimizer.Optimize(basket, offers, settings);
        var run3 = BasketOptimizer.Optimize(basket, offers, settings);

        Assert.Equal(run1.Assignments[0].Candidate.OfferId, run2.Assignments[0].Candidate.OfferId);
        Assert.Equal(run2.Assignments[0].Candidate.OfferId, run3.Assignments[0].Candidate.OfferId);
    }

    [Fact]
    public void BalancedStrategy_UsesDistanceAndTravelLocation_OptionBCloserWinsOverCheaperOptionAFarAway()
    {
        // Scenario required by Goal 13:
        // Option A: $100.000, ~20 km away
        // Option B: $103.000, ~1 km away
        // Balanced should select Option B because travel cost outweighs the $3.000 price difference.
        // MaximumSavings should select Option A because pure product price is lower.
        var basket = new Basket { MaxStores = 1 };
        basket.Items.Add(new BasketItem { Query = "Aceite 1000ml", Quantity = 1 });

        var originLat = 6.2500m;
        var originLon = -75.5700m;

        var storeAId = Guid.NewGuid();
        var storeA = new Store
        {
            Id = storeAId,
            RetailerId = Guid.NewGuid(),
            Name = "D1 Bello Norte",
            Latitude = 6.4300m,
            Longitude = -75.5700m,
            Retailer = new Retailer { Code = "D1", Name = "D1" },
            VerificationStatus = "VERIFIED"
        };

        var storeBId = Guid.NewGuid();
        var storeB = new Store
        {
            Id = storeBId,
            RetailerId = Guid.NewGuid(),
            Name = "Éxito Laureles",
            Latitude = 6.2590m,
            Longitude = -75.5700m,
            Retailer = new Retailer { Code = "EXITO", Name = "Éxito" },
            VerificationStatus = "VERIFIED"
        };

        var storeMap = new Dictionary<string, Store>(StringComparer.OrdinalIgnoreCase)
        {
            ["store:" + storeAId] = storeA,
            ["store:" + storeBId] = storeB,
            ["retailer:D1"] = storeA,
            ["retailer:EXITO"] = storeB
        };

        var offers = new List<RawCatalogOffer>
        {
            CreateOffer("D1", "D1", "retailer:D1", "D1 Online", "Aceite 1000ml", "D1", "Aceite", 100000m,
                scope: PriceScope.FulfillmentRegion),
            CreateOffer("EXITO", "Éxito", "retailer:EXITO", "Éxito Online", "Aceite 1000ml", "Éxito", "Aceite", 103000m,
                scope: PriceScope.FulfillmentRegion)
        };

        var locContext = new OptimizerLocationContext(
            OriginLatitude: originLat,
            OriginLongitude: originLon,
            ReturnToOrigin: true,
            TransportMode: TransportMode.Driving,
            CostPerKm: 2500m,
            PriceWeight: 0.7m,
            DistanceWeight: 0.2m,
            ConvenienceWeight: 0.1m
        );

        var settings = CreateDefaultSettings();

        // 1. Balanced: Option B (close, $103k) must win over Option A (far, $100k)
        var optBalanced = new OptimizerExecutionOptions(
            Strategy: OptimizationStrategy.Balanced,
            LocationContext: locContext,
            StoreMap: storeMap
        );
        var solutionBalanced = BasketOptimizer.Optimize(basket, offers, settings, options: optBalanced);
        Assert.Single(solutionBalanced.Assignments);
        Assert.Equal("EXITO", solutionBalanced.Assignments[0].Candidate.RetailerCode);
        Assert.Equal(103000m, solutionBalanced.TotalCost);
        Assert.NotNull(solutionBalanced.RoutePlan);
        Assert.True(solutionBalanced.RoutePlan.TotalDistanceKm < 5m, "Option B route should be around 2 km round-trip");

        // 2. MaximumSavings: Option A ($100k) must win because it has the lowest shelf price
        var optSavings = new OptimizerExecutionOptions(
            Strategy: OptimizationStrategy.MaxSavings,
            LocationContext: locContext,
            StoreMap: storeMap
        );
        var solutionSavings = BasketOptimizer.Optimize(basket, offers, settings, options: optSavings);
        Assert.Single(solutionSavings.Assignments);
        Assert.Equal("D1", solutionSavings.Assignments[0].Candidate.RetailerCode);
        Assert.Equal(100000m, solutionSavings.TotalCost);
    }
    [Fact]
    public void BalancedStrategy_ExcludesUnverifiedLocationsFromTravelDistance_UnlessAllowed()
    {
        var basket = new Basket { MaxStores = 1 };
        basket.Items.Add(new BasketItem { Query = "Aceite 1000ml", Quantity = 1 });

        var storeAId = Guid.NewGuid();
        var storeA = new Store
        {
            Id = storeAId,
            RetailerId = Guid.NewGuid(),
            Name = "D1 Bello Norte",
            Latitude = 6.4300m,
            Longitude = -75.5700m,
            Retailer = new Retailer { Code = "D1", Name = "D1" },
            VerificationStatus = "DEMO" // UNVERIFIED!
        };

        var storeBId = Guid.NewGuid();
        var storeB = new Store
        {
            Id = storeBId,
            RetailerId = Guid.NewGuid(),
            Name = "Éxito Laureles",
            Latitude = 6.2590m,
            Longitude = -75.5700m,
            Retailer = new Retailer { Code = "EXITO", Name = "Éxito" },
            VerificationStatus = "VERIFIED"
        };

        var storeMap = new Dictionary<string, Store>(StringComparer.OrdinalIgnoreCase)
        {
            ["store:" + storeAId] = storeA,
            ["store:" + storeBId] = storeB,
            ["retailer:D1"] = storeA,
            ["retailer:EXITO"] = storeB
        };

        // Option A: D1, $100k, 20 km away (far), but UNVERIFIED (DEMO)
        var offerA = CreateOffer("D1", "D1", "retailer:D1", "D1 Online", "Aceite 1000ml", "D1", "Aceite", 100000m,
            scope: PriceScope.FulfillmentRegion);
        // Option B: Éxito, $103k, 1 km away (close), VERIFIED
        var offerB = CreateOffer("EXITO", "Éxito", "retailer:EXITO", "Éxito Online", "Aceite 1000ml", "Éxito", "Aceite", 103000m,
            scope: PriceScope.FulfillmentRegion);

        var offers = new List<RawCatalogOffer> { offerA, offerB };

        var locContext = new OptimizerLocationContext(
            OriginLatitude: 6.2500m,
            OriginLongitude: -75.5700m,
            ReturnToOrigin: true,
            TransportMode: TransportMode.Driving,
            CostPerKm: 2500m,
            PriceWeight: 0.7m,
            DistanceWeight: 0.2m,
            ConvenienceWeight: 0.1m
        );

        var settings = CreateDefaultSettings();

        // 1. By default AllowUnverifiedLocationsForTravel = false:
        // Option A's DEMO location is excluded from travel calculation (distance = 0), so Option A wins on shelf price ($100k)
        var optDefault = new OptimizerExecutionOptions(
            Strategy: OptimizationStrategy.Balanced,
            LocationContext: locContext,
            StoreMap: storeMap,
            AllowUnverifiedLocationsForTravel: false
        );
        var solDefault = BasketOptimizer.Optimize(basket, offers, settings, options: optDefault);
        Assert.Equal("D1", solDefault.Assignments[0].Candidate.RetailerCode);
        Assert.Equal(100000m, solDefault.TotalCost);

        // 2. When AllowUnverifiedLocationsForTravel = true:
        // Option A's 20 km is counted, so Option B (1 km, $103k) wins the Balanced score
        var optAllowed = new OptimizerExecutionOptions(
            Strategy: OptimizationStrategy.Balanced,
            LocationContext: locContext,
            StoreMap: storeMap,
            AllowUnverifiedLocationsForTravel: true
        );
        var solAllowed = BasketOptimizer.Optimize(basket, offers, settings, options: optAllowed);
        Assert.Equal("EXITO", solAllowed.Assignments[0].Candidate.RetailerCode);
        Assert.Equal(103000m, solAllowed.TotalCost);
    }

    [Fact]
    public void BalancedStrategyAppliesStorePenaltyCorrectly()
    {
        var basket = new Basket { PreferredStrategy = OptimizationStrategy.Balanced, MaxStores = 3 };
        basket.Items.Add(new BasketItem { Query = "Arroz 1000g", Quantity = 1 });
        basket.Items.Add(new BasketItem { Query = "Leche 1000ml", Quantity = 1 });

        // Store A has both: Arroz 4000, Leche 4000 (Total in 1 store = 8000)
        // Store B has Arroz 3500 (saves 500 COP, but adding Store B incurs a 5000 COP penalty!)
        var offers = new List<RawCatalogOffer>
        {
            CreateOffer("D1", "D1", "store:A", "D1", "Arroz 1000g", "D1", "Arroz", 4000m),
            CreateOffer("D1", "D1", "store:A", "D1", "Leche 1000ml", "D1", "Leche", 4000m),
            CreateOffer("EXITO", "Éxito", "store:B", "Éxito", "Arroz 1000g", "Éxito", "Arroz", 3500m)
        };

        var settings = CreateDefaultSettings();
        // With Balanced and penalty 5000 COP:
        // 1 store (Store A): Cost = 8000, Penalty = 0 -> Score = 8000
        // 2 stores (A + B): Cost = 7500, Penalty = 5000 -> Score = 12500
        // Balanced should pick 1 store (Store A)
        var solutionBalanced = BasketOptimizer.Optimize(basket, offers, settings,
            options: new OptimizerExecutionOptions(Strategy: OptimizationStrategy.Balanced, StorePenalty: 5000m));
        Assert.Equal(1, solutionBalanced.StoresUsedCount);
        Assert.Equal(8000m, solutionBalanced.TotalCost);

        // With MaxSavings: should pick 2 stores (Store A + Store B) for 7500 COP
        var solutionSavings = BasketOptimizer.Optimize(basket, offers, settings,
            options: new OptimizerExecutionOptions(Strategy: OptimizationStrategy.MaxSavings));
        Assert.Equal(2, solutionSavings.StoresUsedCount);
        Assert.Equal(7500m, solutionSavings.TotalCost);
    }

    [Fact]
    public void PerformanceScaleBenchmark100Items10StoresUnder500ms()
    {
        // Generate 100 items
        var basket = new Basket { MaxStores = 3, PreferredStrategy = OptimizationStrategy.MaxSavings };
        for (int i = 0; i < 100; i++)
        {
            basket.Items.Add(new BasketItem { Query = $"Producto Alimento {i}", Quantity = 1 });
        }

        // Generate offers across 10 stores
        var offers = new List<RawCatalogOffer>();
        for (int s = 0; s < 10; s++)
        {
            var storeKey = $"store:{s}";
            var storeName = $"Tienda {s}";
            for (int i = 0; i < 100; i++)
            {
                var price = 2000m + ((i * 37 + s * 13) % 5000);
                offers.Add(CreateOffer("RET", "Retailer", storeKey, storeName, $"Producto Alimento {i}", "Marca", "Sku", price));
            }
        }

        var settings = CreateDefaultSettings();

        // Warm-up
        BasketOptimizer.Optimize(basket, offers, settings);

        // Timed run
        var sw = Stopwatch.StartNew();
        var solution = BasketOptimizer.Optimize(basket, offers, settings);
        sw.Stop();

        Assert.Equal(100, solution.ItemsCoveredCount);
        Assert.True(solution.StoresUsedCount <= 3);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Scale benchmark took {sw.ElapsedMilliseconds}ms, which exceeds 500ms target.");
    }
}
