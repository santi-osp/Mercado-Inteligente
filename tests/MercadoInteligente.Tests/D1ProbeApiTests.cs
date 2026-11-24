using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MercadoInteligente.Tests;

public sealed class D1ProbeApiTests
{
    [Theory]
    [InlineData(HttpStatusCode.PartialContent, "VALID", "VALID_GENERIC_ONLINE")]
    [InlineData(HttpStatusCode.TooManyRequests, "DECLINED", "NOT_CHECKED")]
    public async Task Health_is_local_and_only_explicit_probe_requests_public_search(
        HttpStatusCode providerStatus, string search, string price)
    {
        var root = Path.Combine(Path.GetTempPath(), "market-d1-probe-api-" + Guid.NewGuid().ToString("N"));
        var handler = new CountingHandler(providerStatus);
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseSetting("MARKET_DATA_DIR", root);
                builder.ConfigureTestServices(services => services.AddTransient(_ =>
                    new D1HealthProbe(new HttpClient(handler, disposeHandler: false)
                        { BaseAddress = new Uri("https://www.d1.com.co/") })));
            });
            using var client = factory.CreateClient();
            var health = await client.GetFromJsonAsync<JsonElement>("/api/v1/providers/d1/health");
            Assert.Equal(JsonValueKind.Null, health.GetProperty("lastRun").ValueKind);
            Assert.Equal(0, handler.Calls);

            using var response = await client.PostAsync("/api/v1/providers/d1/probe", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var diagnostic = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(search, diagnostic.GetProperty("search").GetString());
            Assert.Equal(price, diagnostic.GetProperty("genericOnlinePrice").GetString());
            Assert.Equal(1, handler.Calls);
            Assert.Contains("ft=arroz", handler.Path);
            Assert.Contains("_to=0", handler.Path);

            health = await client.GetFromJsonAsync<JsonElement>("/api/v1/providers/d1/health");
            Assert.Equal(JsonValueKind.Null, health.GetProperty("lastRun").ValueKind);
            Assert.Equal(1, handler.Calls);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
            Assert.Equal(0, await db.SyncRuns.CountAsync());
            Assert.Equal(0, await db.RawCaptures.CountAsync());
            Assert.Equal(0, await db.Offers.CountAsync());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private sealed class CountingHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string Path { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            Path = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent("""[{"productId":"1092","productName":"Arroz","items":[{"itemId":"1092","sellers":[{"sellerId":"1","commertialOffer":{"Price":1990}}]}]}]""",
                    Encoding.UTF8, "application/json")
            });
        }
    }
}
