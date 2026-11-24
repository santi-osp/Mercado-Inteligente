using System.Net;
using System.Net.Http.Json;
using MercadoInteligente.Domain;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace MercadoInteligente.Tests;

public sealed class LoopbackRequestSecurityTests
{
    [Fact]
    public async Task HostileOriginsCannotWriteToAnyLocalApiResource()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-origin-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();
            using var localBasket = await client.PostAsync("/api/v1/baskets", null);
            Assert.Equal(HttpStatusCode.Created, localBasket.StatusCode);
            var basket = await localBasket.Content.ReadFromJsonAsync<Basket>();
            Assert.NotNull(basket);

            foreach (var origin in new[] { "https://evil.example", "null", "http://localhost:4200.evil.example", "http://127.0.0.1:4201", "http://evil.example:4200", "http://localhost:4200/path" })
            {
                foreach (var (method, path, body) in new[]
                {
                    (HttpMethod.Post, "/api/v1/baskets", (string?)null),
                    (HttpMethod.Post, $"/api/v1/baskets/{basket.Id}/items", "{\"query\":\"Arroz\",\"quantity\":1}"),
                    (HttpMethod.Delete, $"/api/v1/baskets/{basket.Id}", null),
                    (HttpMethod.Put, "/api/v1/settings", "{\"maxStores\":4}"),
                    (HttpMethod.Post, "/api/v1/backups", null),
                    (HttpMethod.Post, "/api/v1/secrets/google", "{\"value\":\"invalid\"}"),
                    (HttpMethod.Delete, "/api/v1/secrets/google", null)
                })
                {
                    using var request = new HttpRequestMessage(method, path);
                    request.Headers.TryAddWithoutValidation("Origin", origin);
                    request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "localhost:4200");
                    request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "http");
                    if (body is not null) request.Content = new StringContent(body, System.Text.Encoding.UTF8, "text/plain");
                    using var response = await client.SendAsync(request);
                    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                }
            }
            Assert.Empty((await client.GetFromJsonAsync<List<Basket>>("/api/v1/baskets"))!.Single().Items);
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "backups"), "*.db"));
            Assert.Equal(2, (await client.GetFromJsonAsync<AppSettings>("/api/v1/settings"))!.MaxStores);
            Assert.False((await client.GetFromJsonAsync<JsonElement>("/api/v1/secrets/google")).GetProperty("configured").GetBoolean());
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SameOriginAngularProxyAndNoOriginCanWriteWithoutTrustingForwardedHeaders()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-origin-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient(); // TestServer uses the synthetic localhost Host.
            foreach (var origin in new string?[] { null, "http://localhost", "http://localhost:4200", "http://127.0.0.1:4200" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/baskets");
                if (origin is not null) request.Headers.TryAddWithoutValidation("Origin", origin);
                request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "evil.example");
                request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            }
            Assert.Equal(4, (await client.GetFromJsonAsync<List<Basket>>("/api/v1/baskets"))!.Count);

            using var boundHost = new HttpRequestMessage(HttpMethod.Post, "/api/v1/baskets");
            boundHost.Headers.Host = "127.0.0.1:5078";
            boundHost.Headers.TryAddWithoutValidation("Origin", "http://127.0.0.1:5078");
            using var boundResponse = await client.SendAsync(boundHost);
            Assert.Equal(HttpStatusCode.Created, boundResponse.StatusCode);

            using var syntheticHost = new HttpRequestMessage(HttpMethod.Post, "/api/v1/baskets");
            syntheticHost.Headers.Host = "synthetic.test";
            using var result = await client.SendAsync(syntheticHost);
            Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task ForgedForwardedHostCannotTurnCrossOriginIntoSameOrigin()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-origin-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/baskets");
            request.Headers.Host = "localhost:5078";
            request.Headers.TryAddWithoutValidation("Origin", "https://evil.example");
            request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "evil.example");
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
