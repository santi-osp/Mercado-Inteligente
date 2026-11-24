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

public sealed class ExitoApiTests
{
    [Fact]
    public async Task Providers_list_includes_exito_faststore_status()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-api-providers-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var response = await client.GetAsync("/api/v1/providers");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var array = await response.Content.ReadFromJsonAsync<JsonElement>();
            var exito = array.EnumerateArray().FirstOrDefault(p => p.GetProperty("code").GetString() == "EXITO");
            Assert.Equal("PARTIAL_FASTSTORE_MANUAL_REFRESH", exito.GetProperty("status").GetString());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Exito_health_reports_offline_state_and_last_sync_run()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-health-api-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var initial = await client.GetFromJsonAsync<JsonElement>("/api/v1/providers/exito/health");
            Assert.Equal("PARTIAL_FASTSTORE_MANUAL_REFRESH", initial.GetProperty("status").GetString());
            Assert.Equal(JsonValueKind.Null, initial.GetProperty("lastRun").ValueKind);

            var capabilities = initial.GetProperty("capabilities");
            Assert.Equal((int)CapabilityState.Partial, capabilities.GetProperty("search").GetInt32());
            Assert.Equal((int)CapabilityState.NotSupported, capabilities.GetProperty("stores").GetInt32());
            Assert.Equal((int)CapabilityState.NotSupported, capabilities.GetProperty("catalogEnumeration").GetInt32());

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
                db.SyncRuns.Add(new SyncRun
                {
                    Provider = "EXITO",
                    State = SyncState.Completed,
                    ItemsReceived = 15,
                    OffersProcessed = 12,
                    OffersChanged = 3,
                    ProductsCreated = 5,
                    SkusCreated = 5,
                    RawNew = 1,
                    FinishedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/providers/exito/health");
            Assert.Equal("PARTIAL_FASTSTORE_MANUAL_REFRESH", after.GetProperty("status").GetString());
            var lastRun = after.GetProperty("lastRun");
            Assert.Equal((int)SyncState.Completed, lastRun.GetProperty("state").GetInt32());
            Assert.Equal(15, lastRun.GetProperty("itemsReceived").GetInt32());
            Assert.Equal(12, lastRun.GetProperty("offersProcessed").GetInt32());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Exito_offers_endpoint_filters_by_query_scope_and_retailer()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-offers-api-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            using (var scope = factory.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
                var exito = new Retailer { Code = "EXITO", Name = "Éxito" };
                var d1 = new Retailer { Code = "D1", Name = "D1" };
                db.Retailers.AddRange(exito, d1);

                // EXITO generic offer
                var p1 = new ExternalProduct { RetailerId = exito.Id, ExternalId = "ex-1", Name = "Café Juan Valdez" };
                var sku1 = new ExternalSku { ExternalProductId = p1.Id, ExternalId = "sku-1", Name = "Café 500g" };
                var seller1 = new Seller { RetailerId = exito.Id, ExternalId = "1", Name = "Éxito Directo", Kind = SellerKind.RetailerDirect };
                var offer1 = new Offer { ExternalSkuId = sku1.Id, SellerId = seller1.Id, Context = "online-generic", Price = 18500m, Scope = PriceScope.OnlineGeneric };

                // EXITO regional offer
                var p2 = new ExternalProduct { RetailerId = exito.Id, ExternalId = "ex-2", Name = "Café Matiz" };
                var sku2 = new ExternalSku { ExternalProductId = p2.Id, ExternalId = "sku-2", Name = "Café Matiz 500g" };
                var offer2 = new Offer { ExternalSkuId = sku2.Id, SellerId = seller1.Id, Context = "fulfillment:reg1", Price = 19500m, Scope = PriceScope.FulfillmentRegion };

                // D1 offer with matching name
                var p3 = new ExternalProduct { RetailerId = d1.Id, ExternalId = "d1-1", Name = "Café D1" };
                var sku3 = new ExternalSku { ExternalProductId = p3.Id, ExternalId = "sku-3", Name = "Café D1" };
                var seller3 = new Seller { RetailerId = d1.Id, ExternalId = "1", Name = "D1", Kind = SellerKind.RetailerDirect };
                var offer3 = new Offer { ExternalSkuId = sku3.Id, SellerId = seller3.Id, Context = "online-generic", Price = 12000m, Scope = PriceScope.OnlineGeneric };

                db.AddRange(p1, sku1, seller1, offer1, p2, sku2, offer2, p3, sku3, seller3, offer3);
                await db.SaveChangesAsync();
            }

            // Generic search: only offer1 should return
            var response = await client.GetAsync("/api/v1/providers/exito/offers?q=Café");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var items = await response.Content.ReadFromJsonAsync<JsonElement>();
            var array = items.EnumerateArray().ToList();
            Assert.Single(array);
            Assert.Equal("Café Juan Valdez", array[0].GetProperty("productName").GetString());
            Assert.Equal("online-generic", array[0].GetProperty("context").GetString());
            Assert.Equal(18500m, array[0].GetProperty("price").GetDecimal());
            Assert.Equal((int)SellerKind.RetailerDirect, array[0].GetProperty("sellerKind").GetInt32());

            // Regional search: only offer2 should return
            var regResponse = await client.GetAsync("/api/v1/providers/exito/offers?q=Café&context=fulfillment:reg1");
            Assert.Equal(HttpStatusCode.OK, regResponse.StatusCode);
            var regArray = (await regResponse.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToList();
            Assert.Single(regArray);
            Assert.Equal("Café Matiz", regArray[0].GetProperty("productName").GetString());
            Assert.Equal("fulfillment:reg1", regArray[0].GetProperty("context").GetString());

            // Invalid query rejects
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/providers/exito/offers?q=")).StatusCode);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Settings_endpoint_updates_and_reads_include_marketplace()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-exito-settings-api-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            // Default settings
            var getInitial = await client.GetFromJsonAsync<JsonElement>("/api/v1/settings");
            Assert.False(getInitial.GetProperty("includeMarketplace").GetBoolean());

            // Update includeMarketplace to true
            var putResponse = await client.PutAsJsonAsync("/api/v1/settings", new { includeMarketplace = true });
            Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);
            var putBody = await putResponse.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(putBody.GetProperty("includeMarketplace").GetBoolean());

            // Verify persistence
            var getAfter = await client.GetFromJsonAsync<JsonElement>("/api/v1/settings");
            Assert.True(getAfter.GetProperty("includeMarketplace").GetBoolean());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
