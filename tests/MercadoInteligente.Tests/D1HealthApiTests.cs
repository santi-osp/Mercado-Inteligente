using System.Net.Http.Json;
using System.Text.Json;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace MercadoInteligente.Tests;

public sealed class D1HealthApiTests
{
    [Fact]
    public async Task Declared_capabilities_and_local_failure_do_not_claim_live_readiness()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-health-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root)))
            using (var client = factory.CreateClient())
            {
                var initial = await client.GetFromJsonAsync<JsonElement>("/api/v1/providers/d1/health");
                Assert.Equal("PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH", initial.GetProperty("status").GetString());
                Assert.Equal(JsonValueKind.Null, initial.GetProperty("lastRun").ValueKind);
                var capabilities = initial.GetProperty("capabilities");
                Assert.Equal((int)CapabilityState.Partial, capabilities.GetProperty("search").GetInt32());
                Assert.Equal((int)CapabilityState.NotSupported, capabilities.GetProperty("stores").GetInt32());
                Assert.Equal((int)CapabilityState.NotSupported, capabilities.GetProperty("catalogEnumeration").GetInt32());
                using (var scope = factory.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
                    db.SyncRuns.Add(new SyncRun { Provider = "D1", State = SyncState.Failed,
                        ErrorCode = "RATE_LIMITED", ItemsReceived = 20, OffersProcessed = 18,
                        ProductsCreated = 8, SkusCreated = 11, RawNew = 1, FinishedAt = DateTime.UtcNow });
                    await db.SaveChangesAsync();
                }
                var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/providers/d1/health");
                Assert.Equal("PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH", after.GetProperty("status").GetString());
                Assert.Equal((int)SyncState.Failed, after.GetProperty("lastRun").GetProperty("state").GetInt32());
                Assert.Equal("RATE_LIMITED", after.GetProperty("lastRun").GetProperty("errorCode").GetString());
                Assert.Equal(20, after.GetProperty("lastRun").GetProperty("itemsReceived").GetInt32());
                Assert.Equal(18, after.GetProperty("lastRun").GetProperty("offersProcessed").GetInt32());
                Assert.Equal(8, after.GetProperty("lastRun").GetProperty("productsCreated").GetInt32());
                Assert.Equal(11, after.GetProperty("lastRun").GetProperty("skusCreated").GetInt32());
                Assert.Equal(1, after.GetProperty("lastRun").GetProperty("rawNew").GetInt32());
                Assert.False(after.GetRawText().Contains("READY", StringComparison.Ordinal));
            }
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
