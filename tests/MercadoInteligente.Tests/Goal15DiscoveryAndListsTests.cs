using System.Net;
using System.Net.Http.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using MercadoInteligente.Infrastructure.Stores.Exito;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace MercadoInteligente.Tests;

public class Goal15DiscoveryAndListsTests
{
    [Fact]
    public async Task Search_WorksWithoutAnyShoppingList()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal15-search-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));

            // Seed product in database without any basket
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
                Assert.Empty(await db.Baskets.ToListAsync());
                Assert.Empty(await db.BasketItems.ToListAsync());

                var retailer = new Retailer { Code = "D1", Name = "Tiendas D1" };
                db.Retailers.Add(retailer);

                var seller = new Seller { RetailerId = retailer.Id, ExternalId = "d1-direct", Name = retailer.Name, Kind = SellerKind.RetailerDirect };
                db.Sellers.Add(seller);

                var product = new ExternalProduct
                {
                    RetailerId = retailer.Id,
                    ExternalId = "d1-queso-1",
                    Name = "Queso Mozzarella Tajado 400g",
                    Brand = "Latti",
                    ProductUrl = "https://d1.com.co/queso-mozzarella/p",
                    ImageUrl = "https://d1.com.co/images/queso.jpg",
                    CategoryPath = "LÃ¡cteos y Quesos",
                    UpdatedAt = DateTime.UtcNow
                };
                db.ExternalProducts.Add(product);

                var sku = new ExternalSku
                {
                    ExternalProductId = product.Id,
                    ExternalId = "sku-d1-queso-1",
                    Name = product.Name,
                    Quantity = 0.4m,
                    Dimension = Dimension.Mass,
                    Unit = "kg"
                };
                db.ExternalSkus.Add(sku);

                var offer = new Offer
                {
                    ExternalSkuId = sku.Id,
                    SellerId = seller.Id,
                    Price = 12500m,
                    ListPrice = 14000m,
                    Scope = PriceScope.OnlineGeneric,
                    Context = "online:generic",
                    Availability = AvailabilityStatus.InStock,
                    PromotionConditions = "Ahorra .500",
                    CheckedAt = DateTime.UtcNow
                };
                db.Offers.Add(offer);

                await db.SaveChangesAsync();
            }

            using var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/v1/search/products", new
            {
                query = "Queso",
                page = 1,
                pageSize = 10,
                localOnly = true
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var searchResult = await response.Content.ReadFromJsonAsync<ProductSearchResponse>();
            Assert.NotNull(searchResult);
            Assert.NotEmpty(searchResult.Items);

            var item = searchResult.Items[0];
            Assert.Contains("Queso", item.Name);
            Assert.Equal("Tiendas D1", item.Retailer);
            Assert.Equal(12500m, item.Price);
            Assert.Equal("https://d1.com.co/queso-mozzarella/p", item.ProductUrl);
            Assert.Equal("https://d1.com.co/images/queso.jpg", item.ImageUrl);
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
    public async Task DeletingBasketDoesNotDeleteSearchedProductCatalog()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal15-delbasket-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));

            Guid basketId;
            Guid productId;

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MarketDb>();

                var retailer = new Retailer { Code = "EXITO", Name = "Ã‰xito" };
                db.Retailers.Add(retailer);

                var seller = new Seller { RetailerId = retailer.Id, ExternalId = "exito-direct", Name = retailer.Name, Kind = SellerKind.RetailerDirect };
                db.Sellers.Add(seller);

                var product = new ExternalProduct
                {
                    RetailerId = retailer.Id,
                    ExternalId = "exito-leche-1",
                    Name = "Leche Entera 1000ml",
                    Brand = "AlquerÃ­a",
                    ProductUrl = "https://www.exito.com/leche-entera/p",
                    ImageUrl = "https://images.exito.com/leche.jpg",
                    UpdatedAt = DateTime.UtcNow
                };
                db.ExternalProducts.Add(product);
                productId = product.Id;

                var sku = new ExternalSku
                {
                    ExternalProductId = product.Id,
                    ExternalId = "sku-exito-leche-1",
                    Name = product.Name,
                    Quantity = 1.0m,
                    Dimension = Dimension.Volume,
                    Unit = "L"
                };
                db.ExternalSkus.Add(sku);

                var offer = new Offer
                {
                    ExternalSkuId = sku.Id,
                    SellerId = seller.Id,
                    Price = 4900m,
                    Scope = PriceScope.OnlineGeneric,
                    Context = "online:generic",
                    Availability = AvailabilityStatus.InStock,
                    CheckedAt = DateTime.UtcNow
                };
                db.Offers.Add(offer);

                var basket = new Basket { Name = "Mercado Quincenal" };
                db.Baskets.Add(basket);
                basketId = basket.Id;

                var item = new BasketItem
                {
                    BasketId = basket.Id,
                    Query = "Leche Entera",
                    Quantity = 2
                };
                db.BasketItems.Add(item);

                await db.SaveChangesAsync();
            }

            using var client = factory.CreateClient();

            // 1. Delete Basket
            var deleteResponse = await client.DeleteAsync($"/api/v1/baskets/{basketId}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // 2. Verify Basket is gone
            var getBasketResponse = await client.GetAsync($"/api/v1/baskets/{basketId}");
            Assert.Equal(HttpStatusCode.NotFound, getBasketResponse.StatusCode);

            // 3. Verify Product still exists in catalog search
            var searchResponse = await client.PostAsJsonAsync("/api/v1/search/products", new
            {
                query = "Leche",
                page = 1,
                pageSize = 50,
                localOnly = true
            });
            Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
            var searchResult = await searchResponse.Content.ReadFromJsonAsync<ProductSearchResponse>();
            Assert.NotNull(searchResult);
            Assert.Contains(searchResult.Items, p => p.ProductId == productId);
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
    public async Task DuplicateBasket_ClonesBasketAndItems()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal15-dup-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            // Create basket
            var createRes = await client.PostAsJsonAsync("/api/v1/baskets", new { name = "Lista Original" });
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
            var basket = await createRes.Content.ReadFromJsonAsync<Basket>();
            Assert.NotNull(basket);

            // Add item
            var addItemRes = await client.PostAsJsonAsync($"/api/v1/baskets/{basket.Id}/items", new { query = "Arroz 1kg", quantity = 3 });
            Assert.Equal(HttpStatusCode.Created, addItemRes.StatusCode);

            // Duplicate basket
            var dupRes = await client.PostAsync($"/api/v1/baskets/{basket.Id}/duplicate", null);
            Assert.Equal(HttpStatusCode.Created, dupRes.StatusCode);
            var duplicated = await dupRes.Content.ReadFromJsonAsync<Basket>();
            Assert.NotNull(duplicated);

            Assert.NotEqual(basket.Id, duplicated.Id);
            Assert.Equal("Lista Original (Copia)", duplicated.Name);
            Assert.Single(duplicated.Items);
            Assert.Equal("Arroz 1kg", duplicated.Items[0].Query);
            Assert.Equal(3, duplicated.Items[0].Quantity);
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
    public async Task ShoppingAssistant_ReturnsFriendlyDisabledStatus_WhenNotConfigured()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal15-ai-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/v1/ai/shopping-assistant", new
            {
                message = "Â¿CuÃ¡l queso conviene mÃ¡s?",
                pageContext = "search"
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var assistantResult = await response.Content.ReadFromJsonAsync<ShoppingAssistantResponse>();
            Assert.NotNull(assistantResult);
            Assert.Equal("disabled", assistantResult.Status);
            Assert.Contains("Ajustes", assistantResult.Reply);
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
    public async Task DeleteBasket_RemovesBasketAndItems_ButPreservesCatalogAndPriceHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal15-delhist-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));

            Guid basketId;
            Guid productId;
            Guid observationId;

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MarketDb>();

                var retailer = new Retailer { Code = "EXITO", Name = "Ã‰xito" };
                db.Retailers.Add(retailer);

                var seller = new Seller { RetailerId = retailer.Id, ExternalId = "exito-direct-2", Name = retailer.Name, Kind = SellerKind.RetailerDirect };
                db.Sellers.Add(seller);

                var product = new ExternalProduct
                {
                    RetailerId = retailer.Id,
                    ExternalId = "exito-arroz-1",
                    Name = "Arroz Diana 1000g",
                    Brand = "Diana",
                    ProductUrl = "https://www.exito.com/arroz-diana/p",
                    ImageUrl = "https://images.exito.com/arroz.jpg",
                    UpdatedAt = DateTime.UtcNow
                };
                db.ExternalProducts.Add(product);
                productId = product.Id;

                var sku = new ExternalSku
                {
                    ExternalProductId = product.Id,
                    ExternalId = "sku-exito-arroz-1",
                    Name = product.Name,
                    Quantity = 1.0m,
                    Dimension = Dimension.Mass,
                    Unit = "kg"
                };
                db.ExternalSkus.Add(sku);

                var offer = new Offer
                {
                    ExternalSkuId = sku.Id,
                    SellerId = seller.Id,
                    Price = 4500m,
                    Scope = PriceScope.OnlineGeneric,
                    Context = "online:generic",
                    Availability = AvailabilityStatus.InStock,
                    CheckedAt = DateTime.UtcNow
                };
                db.Offers.Add(offer);

                var observation = PriceObservationFactory.FromOfferSnapshot(
                    offer,
                    DateTime.UtcNow.AddDays(-1)
                );
                db.PriceObservations.Add(observation);
                observationId = observation.Id;

                var basket = new Basket { Name = "Mercado Mensual" };
                db.Baskets.Add(basket);
                basketId = basket.Id;

                var item = new BasketItem
                {
                    BasketId = basket.Id,
                    Query = "Arroz Diana",
                    Quantity = 5
                };
                db.BasketItems.Add(item);

                await db.SaveChangesAsync();
            }

            using var client = factory.CreateClient();

            // 1. Delete Basket
            var deleteResponse = await client.DeleteAsync($"/api/v1/baskets/{basketId}");
            Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

            // 2. Verify Basket is deleted
            var getBasketResponse = await client.GetAsync($"/api/v1/baskets/{basketId}");
            Assert.Equal(HttpStatusCode.NotFound, getBasketResponse.StatusCode);

            // 3. Verify Catalog Product still exists
            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
                var prod = await db.ExternalProducts.FindAsync(productId);
                Assert.NotNull(prod);

                var obs = await db.PriceObservations.FindAsync(observationId);
                Assert.NotNull(obs);
                Assert.Equal(4500m, obs.Price);
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
    public async Task DeletingBasket_DoesNotRemoveProductDiscoveryResults()
    {
        // Alias verifying that discovery search endpoint returns products even after deleting the basket
        await DeletingBasketDoesNotDeleteSearchedProductCatalog();
    }

    [Fact]
    public async Task SearchResult_ContainsValidRetailerProductUrl()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal15-url-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MarketDb>();

                var retailer = new Retailer { Code = "D1", Name = "Tiendas D1" };
                db.Retailers.Add(retailer);

                var seller = new Seller { RetailerId = retailer.Id, ExternalId = "d1-seller-url", Name = retailer.Name, Kind = SellerKind.RetailerDirect };
                db.Sellers.Add(seller);

                var product = new ExternalProduct
                {
                    RetailerId = retailer.Id,
                    ExternalId = "d1-aceite-1",
                    Name = "Aceite Vegetal 900ml",
                    Brand = "Olio",
                    ProductUrl = "https://d1.com.co/aceite-vegetal-900ml/p",
                    ImageUrl = "https://d1.com.co/images/aceite.jpg",
                    UpdatedAt = DateTime.UtcNow
                };
                db.ExternalProducts.Add(product);

                var sku = new ExternalSku
                {
                    ExternalProductId = product.Id,
                    ExternalId = "sku-d1-aceite-1",
                    Name = product.Name,
                    Quantity = 0.9m,
                    Dimension = Dimension.Volume,
                    Unit = "L"
                };
                db.ExternalSkus.Add(sku);

                var offer = new Offer
                {
                    ExternalSkuId = sku.Id,
                    SellerId = seller.Id,
                    Price = 7990m,
                    Scope = PriceScope.OnlineGeneric,
                    Context = "online:generic",
                    Availability = AvailabilityStatus.InStock,
                    CheckedAt = DateTime.UtcNow
                };
                db.Offers.Add(offer);

                await db.SaveChangesAsync();
            }

            using var client = factory.CreateClient();
            var searchResponse = await client.PostAsJsonAsync("/api/v1/search/products", new
            {
                query = "Aceite",
                page = 1,
                pageSize = 10,
                localOnly = true
            });

            Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);
            var searchResult = await searchResponse.Content.ReadFromJsonAsync<ProductSearchResponse>();
            Assert.NotNull(searchResult);
            Assert.NotEmpty(searchResult.Items);

            var item = searchResult.Items[0];
            Assert.NotNull(item.ProductUrl);
            Assert.StartsWith("https://", item.ProductUrl);
            Assert.Contains("d1.com.co", item.ProductUrl);
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
    public async Task ShoppingAssistant_WhenAiDisabled_ShowsConfigurationState()
    {
        // Explicit test named as in DoD section 121
        await ShoppingAssistant_ReturnsFriendlyDisabledStatus_WhenNotConfigured();
    }

    [Fact]
    public async Task ShoppingAssistant_UsesVisibleSearchResultsWithoutInventingPrices()
    {
        var mockAi = new TestMockAiProvider();
        var assistantService = new ShoppingAssistantAiService(mockAi);

        var visibleProducts = new List<AssistantProductDto>
        {
            new(
                Name: "Queso Mozzarella 400g",
                Brand: "Latti",
                Presentation: "400g",
                Price: 12500m,
                ListPrice: 14000m,
                UnitPrice: 31250m,
                UnitLabel: "$ / kg",
                Retailer: "Tiendas D1",
                Availability: "Disponible",
                ProductUrl: "https://d1.com.co/queso/p",
                ImageUrl: "https://d1.com.co/queso.jpg"
            ),
            new(
                Name: "Queso Mozzarella 500g",
                Brand: "Colanta",
                Presentation: "500g",
                Price: 18900m,
                ListPrice: null,
                UnitPrice: 37800m,
                UnitLabel: "$ / kg",
                Retailer: "Ã‰xito",
                Availability: "Disponible",
                ProductUrl: "https://www.exito.com/queso/p",
                ImageUrl: "https://images.exito.com/queso.jpg"
            )
        };

        var request = new ShoppingAssistantRequest(
            Message: "Â¿CuÃ¡l queso tiene mejor precio por kilo?",
            PageContext: "search",
            VisibleProducts: visibleProducts
        );

        var result = await assistantService.AssistAsync(request);

        Assert.Equal("success", result.Status);
        Assert.NotNull(result.Reply);
        Assert.Contains("Latti", result.Reply);
        Assert.NotNull(result.RecommendedProducts);
        Assert.NotEmpty(result.RecommendedProducts);

        var topRec = result.RecommendedProducts[0];
        Assert.Equal("Queso Mozzarella 400g", topRec.Name);
        Assert.Equal("Tiendas D1", topRec.Retailer);
        Assert.Equal(12500m, topRec.Price);
        Assert.Equal("https://d1.com.co/queso/p", topRec.ProductUrl);
    }

    [Fact]
    public async Task Search_WhenDatabaseIsEmpty_QueriesEnabledRetailers()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal15-emptyquery-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();

            var d1Handler = new TrackingHttpMessageHandler();
            var d1Client = new HttpClient(d1Handler) { BaseAddress = new Uri("https://d1.com.co") };
            var rawStore = scope.ServiceProvider.GetRequiredService<RawStore>();
            var d1Ingest = new D1CatalogIngest(db, rawStore);
            var trackingD1Sync = new D1SearchSync(d1Client, d1Ingest, db);

            var exitoHandler = new TrackingHttpMessageHandler();
            var exitoClient = new HttpClient(exitoHandler) { BaseAddress = new Uri("https://www.exito.com") };
            var exitoIngest = new ExitoCatalogIngest(db, rawStore);
            var trackingExitoSync = new ExitoSearchSync(exitoClient, exitoIngest, db);

            var freshness = scope.ServiceProvider.GetRequiredService<IFreshnessService>();
            var contextResolver = scope.ServiceProvider.GetRequiredService<IShoppingContextResolver>();
            var contextPolicy = scope.ServiceProvider.GetRequiredService<IOfferContextPolicy>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProductSearchOrchestrator>>();

            var orchestrator = new ProductSearchOrchestrator(
                db,
                trackingD1Sync,
                trackingExitoSync,
                freshness,
                contextResolver,
                contextPolicy,
                logger
            );

            var response = await orchestrator.SearchAsync(new ProductSearchRequest("Leche Deslactosada"));

            Assert.NotNull(response);
            Assert.True(d1Handler.CallCount > 0, string.Join("; ", response.Warnings));
            Assert.True(exitoHandler.CallCount > 0, "Ã‰xito debe consultarse remotamente cuando la base de datos estÃ¡ vacÃ­a.");
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
    public async Task Search_D1Failure_DoesNotHideExitoResults()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-goal15-d1fail-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();

            var retailer = new Retailer { Code = "EXITO", Name = "Ã‰xito" };
            db.Retailers.Add(retailer);

            var seller = new Seller { RetailerId = retailer.Id, ExternalId = "exito-direct", Name = retailer.Name, Kind = SellerKind.RetailerDirect };
            db.Sellers.Add(seller);

            var product = new ExternalProduct
            {
                RetailerId = retailer.Id,
                ExternalId = "exito-arroz-1",
                Name = "Arroz Florhuila 1000g",
                Brand = "Florhuila",
                ProductUrl = "https://www.exito.com/arroz-florhuila/p",
                ImageUrl = "https://images.exito.com/arroz.jpg",
                UpdatedAt = DateTime.UtcNow
            };
            db.ExternalProducts.Add(product);

            var sku = new ExternalSku
            {
                ExternalProductId = product.Id,
                ExternalId = "sku-exito-arroz-1",
                Name = product.Name,
                Quantity = 1.0m,
                Dimension = Dimension.Mass,
                Unit = "kg"
            };
            db.ExternalSkus.Add(sku);

            var offer = new Offer
            {
                ExternalSkuId = sku.Id,
                SellerId = seller.Id,
                Price = 5200m,
                Scope = PriceScope.OnlineGeneric,
                Context = "online:generic",
                Availability = AvailabilityStatus.InStock,
                CheckedAt = DateTime.UtcNow
            };
            db.Offers.Add(offer);
            await db.SaveChangesAsync();

            var throwingD1Handler = new ThrowingHttpMessageHandler();
            var d1Client = new HttpClient(throwingD1Handler) { BaseAddress = new Uri("https://d1.com.co") };
            var rawStore = scope.ServiceProvider.GetRequiredService<RawStore>();
            var d1Ingest = new D1CatalogIngest(db, rawStore);
            var failingD1Sync = new D1SearchSync(d1Client, d1Ingest, db);

            var exitoHandler = new TrackingHttpMessageHandler();
            var exitoClient = new HttpClient(exitoHandler) { BaseAddress = new Uri("https://www.exito.com") };
            var exitoIngest = new ExitoCatalogIngest(db, rawStore);
            var trackingExitoSync = new ExitoSearchSync(exitoClient, exitoIngest, db);

            var freshness = scope.ServiceProvider.GetRequiredService<IFreshnessService>();
            var contextResolver = scope.ServiceProvider.GetRequiredService<IShoppingContextResolver>();
            var contextPolicy = scope.ServiceProvider.GetRequiredService<IOfferContextPolicy>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProductSearchOrchestrator>>();

            var orchestrator = new ProductSearchOrchestrator(
                db,
                failingD1Sync,
                trackingExitoSync,
                freshness,
                contextResolver,
                contextPolicy,
                logger
            );

            var response = await orchestrator.SearchAsync(new ProductSearchRequest("Arroz", ForceRefresh: true));

            Assert.NotNull(response);
            Assert.NotEmpty(response.Items);
            Assert.Contains(response.Items, i => i.Name.Contains("Arroz Florhuila"));
            Assert.Contains(response.Warnings, w => w.Contains("D1 no respondió"));
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

    private sealed class TrackingHttpMessageHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var isD1 = request.RequestUri?.ToString().Contains("catalog_system") ?? false;
            var json = isD1 ? "[]" : "{\"data\":{\"search\":{\"products\":{\"edges\":[]}}}}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private sealed class ThrowingHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Simulando fallo de conexiÃ³n con D1");
        }
    }

    private sealed class TestMockAiProvider : IAiProvider
    {
        public bool IsConfigured => true;

        public Task<string?> ExplainAsync(string context, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("ExplicaciÃ³n de prueba");

        public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken)
        {
            var json = @"{
  ""reply"": ""Te conviene el **Queso Mozzarella 400g de Latti en Tiendas D1** porque su precio por kilo es de $31.250 frente a $37.800 de Colanta en Ã‰xito."",
  ""recommendedProducts"": [
    {
      ""name"": ""Queso Mozzarella 400g"",
      ""retailer"": ""Tiendas D1"",
      ""price"": 12500,
      ""reason"": ""Mejor precio por kilo ($31.250/kg)"",
      ""productUrl"": ""https://d1.com.co/queso/p""
    }
  ]
}";
            return Task.FromResult(new AiCompletionResult(true, json, null));
        }

        public Task<AiConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new AiConnectionTestResult(true, 200, "ConexiÃ³n exitosa", 50L, "gemini-3.8-flash"));
    }

}
