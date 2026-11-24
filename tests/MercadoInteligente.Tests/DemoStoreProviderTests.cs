using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

namespace MercadoInteligente.Tests;

public class DemoStoreProviderTests
{
    private readonly IStoreProvider _provider = new DemoStoreProvider();
    private readonly ProviderContext _context = new("demo-region");

    [Fact]
    public async Task CapabilitiesMatchSupportedOperationsAndSyntheticScope()
    {
        Assert.Equal("DEMO", _provider.Code);
        Assert.Equal(CapabilityState.Supported, _provider.Capabilities.Search);
        Assert.Equal(CapabilityState.Supported, _provider.Capabilities.Details);
        Assert.Equal(CapabilityState.Supported, _provider.Capabilities.Offers);
        Assert.Equal(CapabilityState.Supported, _provider.Capabilities.CatalogEnumeration);
        Assert.Equal(CapabilityState.NotSupported, _provider.Capabilities.Stores);
        Assert.Equal(CapabilityState.NotSupported, _provider.Capabilities.Availability);

        var products = await _provider.EnumerateCatalogAsync(_context, CancellationToken.None);
        Assert.NotEmpty(products);
        Assert.All(products, p =>
        {
            Assert.StartsWith("DEMO-", p.ExternalId);
            Assert.StartsWith("[DEMO]", p.Name);
            Assert.All(p.Skus, sku => { Assert.StartsWith("DEMO-", sku.ExternalId); Assert.Null(sku.Ean); });
        });
        Assert.Empty(await _provider.GetStoresAsync(_context, CancellationToken.None));
        foreach (var product in products)
        {
            Assert.Equal(product, await _provider.GetProductAsync(product.ExternalId, _context, CancellationToken.None));
            var offers = await _provider.OffersAsync(product.ExternalId, _context, CancellationToken.None);
            Assert.All(offers, offer =>
            {
                Assert.Contains(product.Skus, sku => sku.ExternalId == offer.SkuId);
                Assert.Equal(PriceScope.OnlineGeneric, offer.Scope);
                Assert.Equal(AvailabilityStatus.Unknown, offer.Availability);
                Assert.Equal("DEMO", offer.Context);
                Assert.True(offer.Price > 0);
            });
        }
    }

    [Fact]
    public async Task SearchDetailsAndUnknownIdsAreDeterministic()
    {
        var found = await _provider.SearchAsync("ARROZ", _context, CancellationToken.None);
        Assert.Single(found);
        Assert.Equal("DEMO-P1", found[0].ExternalId);
        Assert.Empty(await _provider.SearchAsync(" ", _context, CancellationToken.None));
        Assert.Null(await _provider.GetProductAsync("missing", _context, CancellationToken.None));
        Assert.Empty(await _provider.OffersAsync("missing", _context, CancellationToken.None));
    }

    [Fact]
    public async Task EveryOperationHonorsCancellation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _provider.SearchAsync("arroz", _context, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _provider.GetProductAsync("DEMO-P1", _context, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _provider.OffersAsync("DEMO-P1", _context, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _provider.GetStoresAsync(_context, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _provider.EnumerateCatalogAsync(_context, source.Token));
    }

    [Fact]
    public void ProductionServiceProviderDoesNotRegisterDemo()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-demo-registration-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder => builder.UseSetting("MARKET_DATA_DIR", root));
            using var scope = factory.Services.CreateScope();
            var providers = scope.ServiceProvider.GetServices<IStoreProvider>().ToList();
            Assert.DoesNotContain(providers, p => p.Code == "DEMO");
            Assert.Contains(providers, p => p.Code == "ARA");
            Assert.Contains(providers, p => p.Code == "D1");
            Assert.Contains(providers, p => p.Code == "EXITO");
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
