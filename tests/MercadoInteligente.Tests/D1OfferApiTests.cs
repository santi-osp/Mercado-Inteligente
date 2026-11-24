using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MercadoInteligente.Tests;

public sealed class D1OfferApiTests
{
    [Fact]
    public async Task Generic_offer_listing_excludes_other_scopes_and_retailers()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-api-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root)))
            {
                using var client = factory.CreateClient();
                using (var scope = factory.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
                    var d1 = new Retailer { Code = "D1", Name = "D1" };
                    var other = new Retailer { Code = "OTHER", Name = "Other" };
                    db.Retailers.AddRange(d1, other);
                    foreach (var (retailer, suffix, priceScope, context) in new[]
                    {
                        (d1, "generic", PriceScope.OnlineGeneric, "online-generic"),
                        (d1, "region", PriceScope.FulfillmentRegion, "medellin"),
                        (d1, "bad-context", PriceScope.OnlineGeneric, "unverified"),
                        (other, "other", PriceScope.OnlineGeneric, "online-generic")
                    })
                    {
                        var product = new ExternalProduct { RetailerId = retailer.Id, ExternalId = suffix, Name = "Arroz " + suffix };
                        var sku = new ExternalSku { ExternalProductId = product.Id, ExternalId = suffix, Name = "Arroz" };
                        var seller = new Seller { RetailerId = retailer.Id, ExternalId = suffix, Name = retailer.Name };
                        db.AddRange(product, sku, seller, new Offer { ExternalSkuId = sku.Id, SellerId = seller.Id,
                            Price = 1990m, Scope = priceScope, Context = context });
                    }
                    await db.SaveChangesAsync();
                }
                var response = await client.GetAsync("/api/v1/providers/d1/offers?q=Arroz");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var rows = await response.Content.ReadFromJsonAsync<JsonElement>();
                var only = Assert.Single(rows.EnumerateArray());
                Assert.Equal("Arroz generic", only.GetProperty("productName").GetString());
                Assert.Equal("online-generic", only.GetProperty("context").GetString());
                Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/providers/d1/offers?q=%20")).StatusCode);
            }
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
