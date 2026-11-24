using System.Net.Http.Json;
using MercadoInteligente.Api;
using MercadoInteligente.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MercadoInteligente.Tests;

public sealed class LocalLoggingTests
{
    [Fact]
    public void SinkRotatesPrunesAndNeverPersistsArbitraryLogContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-logging-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, $"market-{DateTime.UtcNow.AddDays(-20):yyyyMMdd}-0000.log"), "old");
            using var provider = new LocalFileLoggerProvider(root, maxBytes: 200, maxFiles: 2);
            var logger = provider.CreateLogger("MercadoInteligente.Api.Operations");
            for (var i = 0; i < 20; i++)
                logger.Log(LogLevel.Error, new EventId(2), "api-key=do-not-write", new InvalidOperationException("token=do-not-write"),
                    (value, error) => value + error!.Message);
            // Even a permitted structured event cannot leak an arbitrary string value.
            logger.LogInformation(new EventId(1), "URL {Url} {StatusCode}", "/path?token=do-not-write", 200);
            var files = Directory.GetFiles(root, "*.log");
            Assert.InRange(files.Length, 1, 2);
            Assert.All(files, file => Assert.InRange(new FileInfo(file).Length, 1, 200));
            Assert.DoesNotContain(files, file => Path.GetFileName(file).Contains(DateTime.UtcNow.AddDays(-20).ToString("yyyyMMdd")));
            Assert.All(files, file =>
            {
                var content = File.ReadAllText(file);
                Assert.DoesNotContain("do-not-write", content);
                Assert.DoesNotContain("/path", content);
                Assert.DoesNotContain("token", content);
                Assert.Contains("event=", content);
            });
            Assert.False(provider.CreateLogger("Microsoft.AspNetCore.Hosting.Diagnostics").IsEnabled(LogLevel.Error));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task HealthDoesNotClaimGoogleReadyWithNullProvidersEvenWhenSecretExists()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-health-logging-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            if (OperatingSystem.IsWindows()) new WindowsSecretStore(paths).Set("google", "test-secret-do-not-write");
            using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseSetting("MARKET_DATA_DIR", root)))
            using (var client = factory.CreateClient())
            {
                var health = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/system/health");
                Assert.Equal("CONFIGURED_UNVERIFIED", health.GetProperty("google").GetString());
                Assert.Equal("CONFIGURED_UNVERIFIED", health.GetProperty("googleGeocoding").GetString());
                Assert.Equal("CONFIGURED_UNVERIFIED", health.GetProperty("googleRoutes").GetString());
                var status = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/system/status");
                Assert.Equal("CONFIGURED_UNVERIFIED", status.GetProperty("google").GetProperty("geocoding").GetString());
                Assert.Equal("CONFIGURED_UNVERIFIED", status.GetProperty("google").GetProperty("routes").GetString());
                Assert.Equal("NOT_CONFIGURED", status.GetProperty("ai").GetProperty("status").GetString());
                Assert.Equal(3, status.GetProperty("providerHealth").GetArrayLength());
                Assert.All(status.GetProperty("providerHealth").EnumerateArray(), provider =>
                    Assert.Equal("NOT_CHECKED", provider.GetProperty("status").GetString()));
                Assert.Equal("NOT_CONFIGURED", health.GetProperty("ai").GetString());
                await client.GetAsync("/api/v1/products/search?q=token%3Dtest-secret-do-not-write");
            }
            var logs = Directory.GetFiles(paths.Logs, "*.log");
            Assert.NotEmpty(logs);
            Assert.All(logs, file =>
            {
                var text = File.ReadAllText(file);
                Assert.DoesNotContain("test-secret-do-not-write", text);
                Assert.DoesNotContain("/api/", text);
            });
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
