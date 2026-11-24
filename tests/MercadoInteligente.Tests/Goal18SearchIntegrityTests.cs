using System.Net.Http.Json;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MercadoInteligente.Tests;

public class Goal18SearchIntegrityTests
{
    [Fact]
    public void SearchResponse_UsesTotalCountContract()
    {
        var item = new ProductSearchResultItem(
            ProductId: Guid.NewGuid(),
            ExternalSkuId: Guid.NewGuid(),
            Name: "Arroz Diana 1kg",
            Brand: "Diana",
            Presentation: "1 kg",
            ImageUrl: null,
            Retailer: "Éxito",
            RetailerCode: "EXITO",
            Price: 4200m,
            ListPrice: 4500m,
            DiscountPercentage: 7m,
            Promotion: null,
            UnitPrice: 4200m,
            UnitLabel: "kg",
            Availability: "Disponible",
            Freshness: "Fresh",
            PriceScope: "Precio online",
            UpdatedAt: DateTime.UtcNow,
            ProductUrl: "https://exito.com/arroz-diana",
            CategoryPath: "Granos",
            UnitPriceDisplay: "$4.200/kg",
            UnitPriceUnit: "kg",
            PricingMode: "STANDARD",
            PriceBasis: "kg",
            PriceNotes: ""
        );

        var response = new ProductSearchResponse(
            Items: [item],
            TotalCount: 58,
            Page: 1,
            PageSize: 24,
            Facets: new ProductSearchFacets([], [], [], 4200m, 4200m),
            Warnings: [],
            TotalPages: 3,
            HasPrevious: false,
            HasNext: true,
            Query: "arroz"
        );

        // Verify contract properties
        Assert.Equal(58, response.TotalCount);
        Assert.Equal(58, response.Total);
        Assert.Equal(3, response.TotalPages);
        Assert.False(response.HasPrevious);
        Assert.True(response.HasNext);
        Assert.Equal("arroz", response.Query);

        // Verify JSON serialization contract
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("totalCount", out var totalCountProp));
        Assert.Equal(58, totalCountProp.GetInt32());

        Assert.True(root.TryGetProperty("totalPages", out var totalPagesProp));
        Assert.Equal(3, totalPagesProp.GetInt32());

        Assert.True(root.TryGetProperty("hasPrevious", out var hasPrevProp));
        Assert.False(hasPrevProp.GetBoolean());

        Assert.True(root.TryGetProperty("hasNext", out var hasNextProp));
        Assert.True(hasNextProp.GetBoolean());

        Assert.True(root.TryGetProperty("page", out var pageProp));
        Assert.Equal(1, pageProp.GetInt32());

        Assert.True(root.TryGetProperty("pageSize", out var pageSizeProp));
        Assert.Equal(24, pageSizeProp.GetInt32());

        // Verify JSON deserialization preserves values
        var deserialized = JsonSerializer.Deserialize<ProductSearchResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(deserialized);
        Assert.Equal(58, deserialized.TotalCount);
        Assert.Equal(3, deserialized.TotalPages);
        Assert.False(deserialized.HasPrevious);
        Assert.True(deserialized.HasNext);
    }

    [Fact]
    public void UnitPrice_500g_DisplaysPerKg()
    {
        // 500g at $10.000 -> $20.000/kg
        var qty = QuantityParser.Parse("500 g");
        var calc = UnitComparisonService.CalculateUnitPrice(10000m, qty, "Queso Campesino 500g", "Lácteos");

        Assert.NotNull(calc);
        Assert.Equal(20000m, calc.PricePerBaseUnit);
        Assert.Equal("kg", calc.UnitPriceUnit);
        Assert.Equal("$20.000/kg", calc.UnitPriceDisplay);
        Assert.Equal("STANDARD", calc.PricingMode);
    }

    [Fact]
    public void UnitPrice_900ml_DisplaysPerLiter()
    {
        // 900ml at $4.500 -> $5.000/L
        var qty = QuantityParser.Parse("900 ml");
        var calc = UnitComparisonService.CalculateUnitPrice(4500m, qty, "Leche Entera 900ml", "Lácteos");

        Assert.NotNull(calc);
        Assert.Equal(5000m, calc.PricePerBaseUnit);
        Assert.Equal("L", calc.UnitPriceUnit);
        Assert.Equal("$5.000/L", calc.UnitPriceDisplay);
        Assert.Equal("STANDARD", calc.PricingMode);
    }

    [Fact]
    public void UnitPrice_Multipack_2x900g()
    {
        // 2x900g at $30.000 -> 1.8 kg total -> $16.666,67 / kg -> rounded: $16.667/kg
        var qty = QuantityParser.Parse("2x900g");
        var calc = UnitComparisonService.CalculateUnitPrice(30000m, qty, "Café 2x900g", "Café");

        Assert.NotNull(calc);
        Assert.Equal(16666.67m, calc.PricePerBaseUnit);
        Assert.Equal("kg", calc.UnitPriceUnit);
        Assert.Equal("$16.667/kg", calc.UnitPriceDisplay);
    }

    [Fact]
    public void UnitPrice_12Units()
    {
        // 12 units at $18.000 -> $1.500/unidad
        var qty = new StandardQuantity(12, Dimension.Count, 1, 12m, "und");
        var calc = UnitComparisonService.CalculateUnitPrice(18000m, qty, "Huevos AA 12 und", "Huevos");

        Assert.NotNull(calc);
        Assert.Equal(1500m, calc.PricePerBaseUnit);
        Assert.Equal("unidad", calc.UnitPriceUnit);
        Assert.Equal("$1.500/unidad", calc.UnitPriceDisplay);
        Assert.Equal("STANDARD", calc.PricingMode);
    }

    [Fact]
    public void UnknownQuantity_DoesNotInventUnitPrice()
    {
        // Product without discernible quantity must not invent a unit price
        var calcNull = UnitComparisonService.CalculateUnitPrice(12000m, null, "Servilletas decoradas", "Hogar");
        Assert.Null(calcNull);

        var unknownQty = new StandardQuantity(0, Dimension.Unknown, 1, 0, "");
        var calcUnknown = UnitComparisonService.CalculateUnitPrice(12000m, unknownQty, "Servilletas decoradas", "Hogar");
        Assert.Null(calcUnknown);
    }

    [Fact]
    public void WeightBasedProduct_DisplaysPricePerKg()
    {
        // "Lomito de cerdo" at $24.480 without quantity -> $24.480/kg, WEIGHT_BASED
        var calc = UnitComparisonService.CalculateUnitPrice(24480m, null, "Lomito de cerdo", "Carnes");

        Assert.NotNull(calc);
        Assert.Equal(24480m, calc.PricePerBaseUnit);
        Assert.Equal("kg", calc.UnitPriceUnit);
        Assert.Equal("$24.480/kg", calc.UnitPriceDisplay);
        Assert.Equal("WEIGHT_BASED", calc.PricingMode);
        Assert.Equal("kg", calc.PriceBasis);
        Assert.Equal("El valor final depende del peso.", calc.PriceNotes);
    }

    [Fact]
    public void Presentation_500g_DoesNotDisplayPoint5g()
    {
        // A product of 500g stored in kg as 0.5 must format as "500 g", never "0.5 g"
        var formatted = QuantityDisplayFormatter.FormatSingle(0.5m, Dimension.Mass, "g");
        Assert.Equal("500 g", formatted);
        Assert.DoesNotContain("0.5 g", formatted);

        var formattedKgHint = QuantityDisplayFormatter.FormatSingle(0.5m, Dimension.Mass, "kg");
        Assert.Equal("500 g", formattedKgHint);
        Assert.DoesNotContain("0.5 g", formattedKgHint);

        var fullFormat = QuantityDisplayFormatter.Format(0.5m, "g", 1, null, Dimension.Mass, "Azúcar 500g");
        Assert.Equal("500 g", fullFormat);
    }

    [Fact]
    public async Task SearchPagination_And_Counters_MatchAccurately()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal18-page-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
            await db.Database.MigrateAsync();

            var retailer = new Retailer { Code = "EXITO", Name = "Éxito" };
            db.Retailers.Add(retailer);

            var seller = new Seller { RetailerId = retailer.Id, Name = "Éxito Directo", Kind = SellerKind.RetailerDirect };
            db.Sellers.Add(seller);

            // Seed 58 products
            for (int i = 1; i <= 58; i++)
            {
                var brand = i <= 26 ? "MarcaA" : (i <= 46 ? "MarcaB" : "MarcaC");
                var price = 2000m + (i * 100m);
                var product = new ExternalProduct
                {
                    RetailerId = retailer.Id,
                    ExternalId = $"prod-arroz-{i:D3}",
                    Name = $"Arroz Especial Calidad {i:D3} 1kg",
                    Brand = brand,
                    CategoryPath = "Granos y Pastas",
                    Status = ProductLifecycleState.Active,
                    UpdatedAt = DateTime.UtcNow.AddMinutes(-i)
                };
                db.ExternalProducts.Add(product);

                var sku = new ExternalSku
                {
                    ExternalProductId = product.Id,
                    ExternalId = $"sku-arroz-{i:D3}",
                    Name = product.Name,
                    Quantity = 1.0m,
                    Unit = "kg",
                    Dimension = Dimension.Mass,
                    TotalQuantity = 1.0m
                };
                db.ExternalSkus.Add(sku);

                var offer = new Offer
                {
                    SellerId = seller.Id,
                    ExternalSkuId = sku.Id,
                    Price = price,
                    Scope = PriceScope.OnlineGeneric,
                    Context = "generic",
                    Availability = AvailabilityStatus.InStock,
                    CheckedAt = DateTime.UtcNow
                };
                db.Offers.Add(offer);
            }

            await db.SaveChangesAsync();

            var orchestrator = scope.ServiceProvider.GetRequiredService<ProductSearchOrchestrator>();

            // Test 1: SearchCounter_MatchesItemsAndTotal (Page 1)
            var req1 = new ProductSearchRequest("arroz", Page: 1, PageSize: 24, LocalOnly: true);
            var res1 = await orchestrator.SearchAsync(req1);

            Assert.Equal(58, res1.TotalCount);
            Assert.Equal(58, res1.Total);
            Assert.Equal(3, res1.TotalPages);
            Assert.Equal(1, res1.Page);
            Assert.Equal(24, res1.Items.Count);
            Assert.False(res1.HasPrevious);
            Assert.True(res1.HasNext);

            // Test 2: SearchPagination_ReturnsCorrectPage (Page 2)
            var req2 = new ProductSearchRequest("arroz", Page: 2, PageSize: 24, LocalOnly: true);
            var res2 = await orchestrator.SearchAsync(req2);

            Assert.Equal(58, res2.TotalCount);
            Assert.Equal(3, res2.TotalPages);
            Assert.Equal(2, res2.Page);
            Assert.Equal(24, res2.Items.Count);
            Assert.True(res2.HasPrevious);
            Assert.True(res2.HasNext);

            // Verify items on Page 2 are distinct from Page 1
            var page1Ids = res1.Items.Select(x => x.ProductId).ToHashSet();
            var page2Ids = res2.Items.Select(x => x.ProductId).ToHashSet();
            Assert.Empty(page1Ids.Intersect(page2Ids));

            // Test 3: SearchPagination_CanNavigateBeyondFiftyMatches (Page 3)
            var req3 = new ProductSearchRequest("arroz", Page: 3, PageSize: 24, LocalOnly: true);
            var res3 = await orchestrator.SearchAsync(req3);

            Assert.Equal(58, res3.TotalCount);
            Assert.Equal(3, res3.TotalPages);
            Assert.Equal(3, res3.Page);
            Assert.Equal(10, res3.Items.Count); // 58 - 48 = 10
            Assert.True(res3.HasPrevious);
            Assert.False(res3.HasNext);

            // Test 4: FiltersPreservePaginationMetadata
            var filterReq = new ProductSearchRequest(
                "arroz",
                Page: 1,
                PageSize: 24,
                Filters: new ProductSearchFilters(Brands: ["MarcaA"]),
                LocalOnly: true
            );
            var filterRes = await orchestrator.SearchAsync(filterReq);

            Assert.Equal(26, filterRes.TotalCount);
            Assert.Equal(2, filterRes.TotalPages);
            Assert.Equal(24, filterRes.Items.Count);
            Assert.All(filterRes.Items, item => Assert.Equal("MarcaA", item.Brand));
            Assert.True(filterRes.HasNext);

            var filterReqPage2 = new ProductSearchRequest(
                "arroz",
                Page: 2,
                PageSize: 24,
                Filters: new ProductSearchFilters(Brands: ["MarcaA"]),
                LocalOnly: true
            );
            var filterResPage2 = await orchestrator.SearchAsync(filterReqPage2);
            Assert.Equal(26, filterResPage2.TotalCount);
            Assert.Equal(2, filterResPage2.Items.Count);
            Assert.True(filterResPage2.HasPrevious);
            Assert.False(filterResPage2.HasNext);

            // Test 5: SortingPreservesQueryAndFilters
            var sortAscReq = new ProductSearchRequest(
                "arroz",
                Sort: "price_asc",
                Page: 1,
                PageSize: 24,
                Filters: new ProductSearchFilters(Brands: ["MarcaA"]),
                LocalOnly: true
            );
            var sortAscRes = await orchestrator.SearchAsync(sortAscReq);
            Assert.Equal(26, sortAscRes.TotalCount);
            Assert.Equal("arroz", sortAscRes.Query);
            for (int j = 0; j < sortAscRes.Items.Count - 1; j++)
            {
                Assert.True(sortAscRes.Items[j].Price <= sortAscRes.Items[j + 1].Price);
            }

            var sortDescReq = new ProductSearchRequest(
                "arroz",
                Sort: "price_desc",
                Page: 1,
                PageSize: 24,
                Filters: new ProductSearchFilters(Brands: ["MarcaA"]),
                LocalOnly: true
            );
            var sortDescRes = await orchestrator.SearchAsync(sortDescReq);
            Assert.Equal(26, sortDescRes.TotalCount);
            for (int j = 0; j < sortDescRes.Items.Count - 1; j++)
            {
                Assert.True(sortDescRes.Items[j].Price >= sortDescRes.Items[j + 1].Price);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ProviderWithoutOffers_DoesNotInventPrice()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal18-no-offers-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
            await db.Database.MigrateAsync();

            var retailer = new Retailer { Code = "EXITO", Name = "Éxito" };
            db.Retailers.Add(retailer);

            // Product with SKU but WITHOUT any Offer
            var product = new ExternalProduct
            {
                RetailerId = retailer.Id,
                ExternalId = "prod-no-offer",
                Name = "Aceite de Oliva 500ml",
                Brand = "Carbonell",
                CategoryPath = "Aceites",
                Status = ProductLifecycleState.Active
            };
            db.ExternalProducts.Add(product);

            var sku = new ExternalSku
            {
                ExternalProductId = product.Id,
                ExternalId = "sku-no-offer",
                Name = product.Name,
                Quantity = 0.5m,
                Unit = "L",
                Dimension = Dimension.Volume
            };
            db.ExternalSkus.Add(sku);

            await db.SaveChangesAsync();

            var orchestrator = scope.ServiceProvider.GetRequiredService<ProductSearchOrchestrator>();
            var response = await orchestrator.SearchAsync(new ProductSearchRequest("Aceite", LocalOnly: true));

            // Product without offers must NEVER be returned in search results with an invented price
            Assert.Empty(response.Items);
            Assert.Equal(0, response.TotalCount);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task DemoCatalog_IsExcludedFromProductionAnalysis()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal18-demo-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
            await db.Database.MigrateAsync();

            // Create DEMO retailer
            var demoRetailer = new Retailer { Code = "DEMO", Name = "Supermercado Demo" };
            db.Retailers.Add(demoRetailer);

            var demoSeller = new Seller { RetailerId = demoRetailer.Id, Name = "Demo Seller", Kind = SellerKind.RetailerDirect };
            db.Sellers.Add(demoSeller);

            var demoProduct = new ExternalProduct
            {
                RetailerId = demoRetailer.Id,
                ExternalId = "demo-prod-1",
                Name = "Arroz Demo Barato 1kg",
                Brand = "DemoBrand",
                CategoryPath = "Granos",
                Status = ProductLifecycleState.Active
            };
            db.ExternalProducts.Add(demoProduct);

            var demoSku = new ExternalSku
            {
                ExternalProductId = demoProduct.Id,
                ExternalId = "demo-sku-1",
                Name = demoProduct.Name,
                Quantity = 1.0m,
                Unit = "kg",
                Dimension = Dimension.Mass
            };
            db.ExternalSkus.Add(demoSku);

            var demoOffer = new Offer
            {
                SellerId = demoSeller.Id,
                ExternalSkuId = demoSku.Id,
                Price = 990m,
                Scope = PriceScope.OnlineGeneric,
                Context = "generic",
                Availability = AvailabilityStatus.InStock,
                CheckedAt = DateTime.UtcNow
            };
            db.Offers.Add(demoOffer);

            // Also create a real retailer product
            var realRetailer = new Retailer { Code = "EXITO", Name = "Éxito" };
            db.Retailers.Add(realRetailer);

            var realSeller = new Seller { RetailerId = realRetailer.Id, Name = "Éxito Real", Kind = SellerKind.RetailerDirect };
            db.Sellers.Add(realSeller);

            var realProduct = new ExternalProduct
            {
                RetailerId = realRetailer.Id,
                ExternalId = "real-prod-1",
                Name = "Arroz Real Premium 1kg",
                Brand = "Diana",
                CategoryPath = "Granos",
                Status = ProductLifecycleState.Active
            };
            db.ExternalProducts.Add(realProduct);

            var realSku = new ExternalSku
            {
                ExternalProductId = realProduct.Id,
                ExternalId = "real-sku-1",
                Name = realProduct.Name,
                Quantity = 1.0m,
                Unit = "kg",
                Dimension = Dimension.Mass
            };
            db.ExternalSkus.Add(realSku);

            var realOffer = new Offer
            {
                SellerId = realSeller.Id,
                ExternalSkuId = realSku.Id,
                Price = 4100m,
                Scope = PriceScope.OnlineGeneric,
                Context = "generic",
                Availability = AvailabilityStatus.InStock,
                CheckedAt = DateTime.UtcNow
            };
            db.Offers.Add(realOffer);

            await db.SaveChangesAsync();

            var orchestrator = scope.ServiceProvider.GetRequiredService<ProductSearchOrchestrator>();
            var response = await orchestrator.SearchAsync(new ProductSearchRequest("Arroz", LocalOnly: true));

            // Demo product must be excluded from search results
            Assert.Single(response.Items);
            Assert.Equal("Arroz Real Premium 1kg", response.Items[0].Name);
            Assert.DoesNotContain(response.Items, i => i.RetailerCode == "DEMO");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }

    [Fact]
    public async Task ModularProviderSearch_NeverCreatesPlaceholderPrice()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal18-modular-price-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
            await db.Database.MigrateAsync();

            var reconciler = scope.ServiceProvider.GetRequiredService<CatalogReconciliationService>();

            var remoteProducts = new List<ProviderProduct>
            {
                new(
                    ExternalId: "ext-prod-no-price",
                    Name: "Atún Van Camps en Agua 160g",
                    Brand: "Van Camps",
                    Skus: [new ProviderSku("ext-sku-no-price", "Atún Van Camps en Agua 160g", null, 0.16m, Dimension.Mass)],
                    ProductUrl: "https://example.com/atun",
                    CategoryPath: "Enlatados",
                    ImageUrl: null
                )
            };

            // Reconciliation with empty offersFactory must never invent placeholder offers ($0 or -1)
            var count = await reconciler.ReconcileRemoteProductsAsync(
                retailerCode: "LAVAQUITA",
                retailerName: "La Vaquita",
                products: remoteProducts,
                offersFactory: p => [], // No offers available
                session: null
            );

            Assert.Equal(1, count);

            // Verify ExternalProduct and SKU were saved, but NO Offer was created
            var savedProduct = await db.ExternalProducts.FirstOrDefaultAsync(p => p.ExternalId == "ext-prod-no-price");
            Assert.NotNull(savedProduct);

            var savedOffers = await db.Offers.ToListAsync();
            Assert.Empty(savedOffers);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
            {
                try { Directory.Delete(root, true); } catch { }
            }
        }
    }
}
