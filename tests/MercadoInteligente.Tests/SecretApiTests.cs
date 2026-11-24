using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MercadoInteligente.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace MercadoInteligente.Tests;

public sealed class SecretApiTests
{
    [Fact]
    public async Task StatusAndUnsupportedTypesNeverExposeSecret()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-secrets-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();
            foreach (var type in new[] { "google", "openrouter" })
            {
                var status = await client.GetAsync($"/api/v1/secrets/{type}");
                Assert.Equal(HttpStatusCode.OK, status.StatusCode);
                Assert.Contains("no-store", status.Headers.CacheControl!.ToString());
                Assert.False((await status.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("configured").GetBoolean());
                Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/v1/secrets/{type}", new { value = "invalid" })).StatusCode);
            }
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/secrets/other")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/v1/secrets/other", new { value = "anything" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/v1/secrets/other")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/secrets/google", new { value = "AIza" + new string('a', 30) + "\n" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/secrets/openrouter", new { value = "sk-or-" + new string('a', 600) })).StatusCode);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task StoredKeysAreEncryptedAndHealthRemainsNotConfigured()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "market-secrets-" + Guid.NewGuid().ToString("N"));
        var values = new Dictionary<string, string> { ["google"] = "AIza" + new string('G', 35), ["openrouter"] = "sk-or-v1-" + new string('O', 30) };
        try
        {
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root)))
            using (var client = factory.CreateClient())
            {
                foreach (var (type, value) in values)
                {
                    var saved = await client.PostAsJsonAsync($"/api/v1/secrets/{type}", new { value });
                    Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
                    Assert.DoesNotContain(value, await saved.Content.ReadAsStringAsync());
                    Assert.Contains("no-store", saved.Headers.CacheControl!.ToString());
                    var ciphertext = File.ReadAllBytes(Path.Combine(root, "secrets", type + ".bin"));
                    Assert.DoesNotContain(value, System.Text.Encoding.Latin1.GetString(ciphertext));
                    Assert.Equal(value, new WindowsSecretStore(new LocalPaths(root)).Get(type));
                }
                var health = await client.GetFromJsonAsync<JsonElement>("/api/v1/system/health");
                Assert.Equal("NOT_CONFIGURED", health.GetProperty("ai").GetString());
                Assert.Equal("CONFIGURED_UNVERIFIED", health.GetProperty("google").GetString());
            }
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root)))
            using (var client = factory.CreateClient())
            {
                foreach (var (type, value) in values)
                {
                    var status = await client.GetAsync($"/api/v1/secrets/{type}");
                    Assert.True((await status.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("configured").GetBoolean());
                    Assert.DoesNotContain(value, await status.Content.ReadAsStringAsync());
                    var invalid = await client.PostAsJsonAsync($"/api/v1/secrets/{type}", new { value = "bad" });
                    Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
                    Assert.Equal(value, new WindowsSecretStore(new LocalPaths(root)).Get(type));
                    var removed = await client.DeleteAsync($"/api/v1/secrets/{type}");
                    Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
                    Assert.Contains("no-store", removed.Headers.CacheControl!.ToString());
                    Assert.False((await client.GetFromJsonAsync<JsonElement>($"/api/v1/secrets/{type}")).GetProperty("configured").GetBoolean());
                    Assert.False(File.Exists(Path.Combine(root, "secrets", type + ".bin")));
                }
            }
            foreach (var value in values.Values)
                foreach (var file in Directory.GetFiles(Path.Combine(root, "logs")))
                    Assert.DoesNotContain(value, await File.ReadAllTextAsync(file));
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
[Fact]
    public async Task OmniRouterSecret_CanBeStoredWithoutOpenRouterPrefix()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-omni-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            const string testKey = "sk-f9e6f33ded13e766-93b504-4ee71ec1";
            var saveResponse = await client.PostAsJsonAsync("/api/v1/secrets/omnirouter", new { value = testKey });
            Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);

            var status = await client.GetAsync("/api/v1/secrets/omnirouter");
            Assert.Equal(HttpStatusCode.OK, status.StatusCode);
            var json = await status.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.GetProperty("configured").GetBoolean());

            if (OperatingSystem.IsWindows())
            {
                var store = new WindowsSecretStore(new LocalPaths(root));
                Assert.Equal(testKey, store.Get("omnirouter"));
                Assert.Equal(testKey, store.Get("openrouter"));
            }
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task OmniRouterSecret_IsNeverReturned()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-omni-leak-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            const string secretKey = "sk-f9e6f33ded13e766-93b504-4ee71ec1";
            await client.PostAsJsonAsync("/api/v1/secrets/omnirouter", new { value = secretKey });

            var statusResponse = await client.GetAsync("/api/v1/secrets/omnirouter");
            var body = await statusResponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain(secretKey, body);
            Assert.Contains("\"configured\":true", body);

            var healthResponse = await client.GetAsync("/api/v1/system/health");
            var healthBody = await healthResponse.Content.ReadAsStringAsync();
            Assert.DoesNotContain(secretKey, healthBody);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
