using Microsoft.EntityFrameworkCore;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MercadoInteligente.Tests;

public class SearchRelevanceTests
{
    [Theory]
    [InlineData("leche", "Leche Entera Alquería 1L")]
    [InlineData("queso", "Queso Campesino Colanta 500g")]
    [InlineData("arroz", "Arroz Blanco Diana 1kg")]
    [InlineData("detergente", "Detergente en Polvo Ariel 1kg")]
    [InlineData("huevos", "Huevos Rojos Santa Anita 30 und")]
    [InlineData("cafe", "Café Molido Sello Rojo 500g")]
    public async Task SearchReturnsMostRelevantProductAtTop(string query, string expectedTopProductName)
    {
        var root = Path.Combine(Path.GetTempPath(), "test-relevance-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
                builder.UseSetting("MARKET_DATA_DIR", root));

            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
            await db.Database.MigrateAsync();

            var retailer = new Retailer { Code = "TEST", Name = "Supermercado Test" };
            db.Retailers.Add(retailer);

            var seller = new Seller { RetailerId = retailer.Id, Name = "Vendedor Test", Kind = SellerKind.RetailerDirect };
            db.Sellers.Add(seller);

            void AddProduct(string extId, string name, string brand, string category, decimal price, string unit, decimal qty)
            {
                var product = new ExternalProduct
                {
                    RetailerId = retailer.Id,
                    ExternalId = extId,
                    Name = name,
                    Brand = brand,
                    Status = ProductLifecycleState.Active,
                    CategoryPath = category
                };
                db.ExternalProducts.Add(product);

                var sku = new ExternalSku
                {
                    ExternalProductId = product.Id,
                    ExternalId = "SKU-" + extId,
                    Name = name,
                    Quantity = qty,
                    Unit = unit
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

            AddProduct("1", "Leche Entera Alquería 1L", "Alquería", "Lácteos", 4500m, "L", 1m);
            AddProduct("2", "Queso Campesino Colanta 500g", "Colanta", "Lácteos", 12000m, "g", 500m);
            AddProduct("3", "Arroz Blanco Diana 1kg", "Diana", "Granos", 4200m, "kg", 1m);
            AddProduct("4", "Detergente en Polvo Ariel 1kg", "Ariel", "Aseo", 14500m, "kg", 1m);
            AddProduct("5", "Huevos Rojos Santa Anita 30 und", "Santa Anita", "Huevos", 18500m, "und", 30m);
            AddProduct("6", "Café Molido Sello Rojo 500g", "Sello Rojo", "Café", 16000m, "g", 500m);
            AddProduct("7", "Galletas con Leche Dulce", "Noel", "Snacks", 3500m, "g", 200m);

            await db.SaveChangesAsync();

            var orchestrator = scope.ServiceProvider.GetRequiredService<ProductSearchOrchestrator>();
            var response = await orchestrator.SearchAsync(new ProductSearchRequest(query, LocalOnly: true));

            Assert.NotNull(response);
            Assert.NotEmpty(response.Items);
            Assert.Equal(expectedTopProductName, response.Items[0].Name);
            Assert.False(response.Items[0].UnitLabel?.StartsWith(" / "), "UnitLabel should not contain leading ' / '");
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
