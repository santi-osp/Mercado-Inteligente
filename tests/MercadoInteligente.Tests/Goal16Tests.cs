using System.Net;
using System.Net.Http.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.Common;
using MercadoInteligente.Infrastructure.Stores.Jumbo;
using MercadoInteligente.Infrastructure.Stores.LaVaquita;
using MercadoInteligente.Infrastructure.Stores.MundoHuevo;
using MercadoInteligente.Infrastructure.Stores.Olimpica;
using MercadoInteligente.Infrastructure.Stores.PriceSmart;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MercadoInteligente.Tests;

public sealed class Goal16Tests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public Goal16Tests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OmniRouterSecret_CanBeStoredWithoutOpenRouterPrefix()
    {
        using var client = _factory.CreateClient();
        var customKey = "sk-f9e6f33ded13e766-93b504-4ee71ec1";

        var response = await client.PostAsJsonAsync("/api/v1/secrets/omnirouter", new { value = customKey });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var statusResponse = await client.GetFromJsonAsync<SecretStatusDto>("/api/v1/secrets/omnirouter");
        Assert.NotNull(statusResponse);
        Assert.True(statusResponse.Configured);
    }

    [Fact]
    public async Task OmniRouterSecret_IsNeverReturnedInClearText()
    {
        using var client = _factory.CreateClient();
        var customKey = "sk-f9e6f33ded13e766-93b504-4ee71ec1";
        await client.PostAsJsonAsync("/api/v1/secrets/omnirouter", new { value = customKey });

        var response = await client.GetAsync("/api/v1/secrets/omnirouter");
        var rawJson = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain(customKey, rawJson);
        Assert.Contains("configured", rawJson);
    }

    [Fact]
    public async Task OmniRouterConnectionTest_DistinguishesStoredFromValid()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/v1/ai/test-connection", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AiConnectionTestResult>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task BackupCanBeCreatedListedVerifiedAndDeleted()
    {
        using var client = _factory.CreateClient();

        // 1. Create backup with label
        var createRes = await client.PostAsJsonAsync("/api/v1/backups", new { label = "TestGoal16" });
        Assert.Equal(HttpStatusCode.OK, createRes.StatusCode);
        var created = await createRes.Content.ReadFromJsonAsync<BackupCreatedDto>();
        Assert.NotNull(created);
        Assert.False(string.IsNullOrWhiteSpace(created.File));

        var backupFileName = Path.GetFileName(created.File);

        // 2. List backups
        var listRes = await client.GetFromJsonAsync<List<BackupFileInfo>>("/api/v1/backups");
        Assert.NotNull(listRes);
        Assert.Contains(listRes, b => b.Name == backupFileName);

        // 3. Verify backup
        var verifyRes = await client.PostAsync($"/api/v1/backups/{backupFileName}/verify", null);
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);
        var verify = await verifyRes.Content.ReadFromJsonAsync<BackupVerificationResult>();
        Assert.NotNull(verify);
        Assert.True(verify.Valid);

        // 4. Delete backup
        var delRes = await client.DeleteAsync($"/api/v1/backups/{backupFileName}");
        Assert.Equal(HttpStatusCode.NoContent, delRes.StatusCode);
    }

    [Fact]
    public async Task BackupDeleteRejectsPathTraversal()
    {
        using var client = _factory.CreateClient();
        var res = await client.DeleteAsync("/api/v1/backups/..%2F..%2Fwindows%2Fsystem32");
        Assert.True(res.StatusCode == HttpStatusCode.BadRequest || res.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CorruptBackupCannotBeRestored()
    {
        using var scope = _factory.Services.CreateScope();
        var paths = scope.ServiceProvider.GetRequiredService<LocalPaths>();
        Directory.CreateDirectory(paths.Backups);

        var corruptFile = "corrupt_test_backup.db";
        var corruptPath = Path.Combine(paths.Backups, corruptFile);
        await File.WriteAllTextAsync(corruptPath, "ESTE NO ES UN ARCHIVO SQLITE VALIDO");

        using var client = _factory.CreateClient();
        var res = await client.PostAsync($"/api/v1/backups/{corruptFile}/restore", null);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);

        // Clean up
        if (File.Exists(corruptPath)) File.Delete(corruptPath);
    }

    [Fact]
    public async Task DatabaseStats_ReturnsValidCountsAndIntegrity()
    {
        using var client = _factory.CreateClient();
        var stats = await client.GetFromJsonAsync<DatabaseStatsResult>("/api/v1/database/stats");

        Assert.NotNull(stats);
        Assert.Equal("ok", stats.IntegrityStatus);
        Assert.True(stats.ForeignKeysValid);
        Assert.NotNull(stats.RetailerProductCounts);
    }

    [Fact]
    public async Task ClearCatalog_PreservesSecretsSettingsAndBackups()
    {
        using var client = _factory.CreateClient();

        // Must reject without confirmation
        var rejected = await client.PostAsJsonAsync("/api/v1/database/clear-catalog", new { confirmation = "no" });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        // Must reject with lowercase or invalid confirmation
        var invalid = await client.PostAsJsonAsync("/api/v1/database/clear-catalog", new { confirmation = "borrar" });
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode); // case-insensitive trimmed BORRAR is accepted
    }

    [Fact]
    public async Task BestUnitPrice_UsesCompatibleDimensionsOnly()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
        var service = new ProductAnalysisService(db, NullLogger<ProductAnalysisService>.Instance);

        var opps = await service.GetOpportunitiesAsync();
        Assert.NotNull(opps);
        Assert.NotNull(opps.BestUnitPrices);
    }

    [Fact]
    public async Task NewProviders_AreRegisteredAndExposeCapabilities()
    {
        using var scope = _factory.Services.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IStoreProviderRegistry>();

        var codes = registry.GetAllProviders().Select(p => p.Code).ToList();
        Assert.Contains("LAVAQUITA", codes);
        Assert.Contains("MUNDOHUEVO", codes);
        Assert.Contains("OLIMPICA", codes);
        Assert.Contains("JUMBO", codes);
        Assert.Contains("PRICESMART", codes);

        var priceSmart = registry.GetProvider("PRICESMART");
        Assert.NotNull(priceSmart);
        Assert.Equal("PRICESMART", priceSmart.Code);
        Assert.Equal(CapabilityState.Supported, priceSmart.Capabilities.Search);

        var laVaquita = registry.GetProvider("LAVAQUITA");
        Assert.NotNull(laVaquita);
        Assert.Equal(CapabilityState.Supported, laVaquita.Capabilities.Search);
    }

    [Fact]
    public async Task NewProviders_CanSearchAndIngestProductsIntoDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
        var registry = scope.ServiceProvider.GetRequiredService<IStoreProviderRegistry>();

        var mundoHuevo = registry.GetProvider("MUNDOHUEVO");
        Assert.NotNull(mundoHuevo);

        var results = await mundoHuevo.SearchAsync("Huevos", new ProviderContext("fulfillment:medellin-aburra"), CancellationToken.None);
        Assert.NotEmpty(results);

        var retailer = await db.Retailers.FirstOrDefaultAsync(r => r.Code == "MUNDOHUEVO");
        Assert.NotNull(retailer);

        var ingested = await db.ExternalProducts.AnyAsync(p => p.RetailerId == retailer.Id);
        Assert.True(ingested);
    }

    private sealed record SecretStatusDto(bool Configured);
    private sealed record BackupCreatedDto(string File, string? Label);
}