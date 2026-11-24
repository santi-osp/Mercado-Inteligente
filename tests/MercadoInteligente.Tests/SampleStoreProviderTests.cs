using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace MercadoInteligente.Tests;

/// <summary>
/// Prueba de extensibilidad de GOAL-09:
/// Demuestra que una nueva cadena o tienda de barrio (ej: "BODEGA") puede implementarse e integrarse
/// vía Inyección de Dependencias (DI) sin modificar una sola línea del dominio central, de los contratos,
/// ni del optimizador de canastas.
/// </summary>
public class SampleStoreProviderTests
{
    private sealed class MockBodegaProvider : IStoreProvider
    {
        public string Code => "BODEGA";

        public ProviderCapabilities Capabilities => new(
            Search: CapabilityState.Supported,
            Details: CapabilityState.Supported,
            Offers: CapabilityState.Supported,
            Stores: CapabilityState.Supported,
            Availability: CapabilityState.Supported,
            CatalogEnumeration: CapabilityState.Supported
        );

        public Task<IReadOnlyList<ProviderProduct>> SearchAsync(string query, ProviderContext context, CancellationToken cancellationToken)
        {
            var product = new ProviderProduct(
                "bodega-prod-arroz",
                "Arroz Blanco 1000g",
                "Diana",
                [new ProviderSku("bodega-sku-arroz", "Arroz Blanco 1000g", "7701234567890", 1000m, Dimension.Mass)]
            );
            return Task.FromResult<IReadOnlyList<ProviderProduct>>([product]);
        }

        public Task<ProviderProduct?> GetProductAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
        {
            if (externalProductId == "bodega-prod-arroz")
            {
                return Task.FromResult<ProviderProduct?>(new ProviderProduct(
                    "bodega-prod-arroz",
                    "Arroz Blanco 1000g",
                    "Diana",
                    [new ProviderSku("bodega-sku-arroz", "Arroz Blanco 1000g", "7701234567890", 1000m, Dimension.Mass)]
                ));
            }
            return Task.FromResult<ProviderProduct?>(null);
        }

        public Task<IReadOnlyList<ProviderOffer>> OffersAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
        {
            if (externalProductId == "bodega-prod-arroz")
            {
                return Task.FromResult<IReadOnlyList<ProviderOffer>>([
                    new ProviderOffer(
                        "bodega-sku-arroz",
                        "bodega-seller",
                        "Bodega Don Pepe",
                        SellerKind.RetailerDirect,
                        3200m,
                        PriceScope.PhysicalVerified,
                        AvailabilityStatus.InStock,
                        "bodega:laureles"
                    )
                ]);
            }
            return Task.FromResult<IReadOnlyList<ProviderOffer>>([]);
        }

        public Task<IReadOnlyList<ProviderStore>> GetStoresAsync(ProviderContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<ProviderStore>>([
                new ProviderStore("bodega-st-1", "Bodega Don Pepe Laureles", 6.2430m, -75.5930m)
            ]);
        }

        public Task<IReadOnlyList<ProviderProduct>> EnumerateCatalogAsync(ProviderContext context, CancellationToken cancellationToken)
        {
            return SearchAsync("", context, cancellationToken);
        }
    }

    [Fact]
    public void ExtensibilityContract_CanRegisterCustomProviderViaDi()
    {
        var services = new ServiceCollection();

        // 1. Registramos proveedores existentes
        services.AddSingleton<IStoreProvider, DemoStoreProvider>();

        // 2. Registramos el nuevo proveedor sin tocar código de dominio
        services.AddSingleton<IStoreProvider, MockBodegaProvider>();

        // 3. Registramos el registro desacoplado
        services.AddSingleton<IStoreProviderRegistry, StoreProviderRegistry>();

        var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IStoreProviderRegistry>();

        // Verificamos que el registro descubrió dinámicamente a Bodega
        var bodega = registry.GetProvider("BODEGA");
        Assert.NotNull(bodega);
        Assert.Equal("BODEGA", bodega.Code);
        Assert.Equal(CapabilityState.Supported, bodega.Capabilities.Search);
        Assert.Equal(CapabilityState.Supported, bodega.Capabilities.Availability);

        // Verificamos que los metadatos se generan limpiamente
        var metadata = registry.GetMetadata();
        var bodegaMeta = metadata.FirstOrDefault(m => m.Code == "BODEGA");
        Assert.NotNull(bodegaMeta);
        Assert.Equal("ACTIVE_EXTENSIBLE", bodegaMeta.Status);
        Assert.True(bodegaMeta.Enabled);
    }

    [Fact]
    public void BasketOptimizer_OperatesUniversallyOnCustomProviderOffers()
    {
        // Demuestra que el BasketOptimizer consume ofertas de la nueva tienda
        // basándose estrictamente en su modelo matemático sin necesidad de casos especiales 'if (store == ...)'
        var arrozBodega = new RawCatalogOffer(
            OfferId: Guid.NewGuid(),
            ExternalSkuId: Guid.NewGuid(),
            ExternalProductId: Guid.NewGuid(),
            RetailerId: Guid.NewGuid(),
            RetailerCode: "BODEGA",
            RetailerName: "Bodega Don Pepe",
            StoreId: Guid.NewGuid(),
            StoreName: "Bodega Don Pepe Laureles",
            SellerId: Guid.NewGuid(),
            SellerName: "Bodega Don Pepe",
            SellerKind: SellerKind.RetailerDirect,
            ProductName: "Arroz 1000g",
            Brand: "Diana",
            SkuName: "Arroz 1000g",
            Ean: null,
            SkuQuantity: null,
            SkuDimension: Dimension.Unknown,
            Price: 3200m,
            ListPrice: null,
            Scope: PriceScope.Store,
            Availability: AvailabilityStatus.InStock,
            Context: "bodega",
            PromotionConditions: null,
            CheckedAt: DateTime.UtcNow,
            StoreKey: "store:bodega"
        );

        var arrozSupermercado = new RawCatalogOffer(
            OfferId: Guid.NewGuid(),
            ExternalSkuId: Guid.NewGuid(),
            ExternalProductId: Guid.NewGuid(),
            RetailerId: Guid.NewGuid(),
            RetailerCode: "SUPER",
            RetailerName: "Supermercado Tradicional",
            StoreId: Guid.NewGuid(),
            StoreName: "Supermercado Tradicional",
            SellerId: Guid.NewGuid(),
            SellerName: "Supermercado Tradicional",
            SellerKind: SellerKind.RetailerDirect,
            ProductName: "Arroz 1000g",
            Brand: "Diana",
            SkuName: "Arroz 1000g",
            Ean: null,
            SkuQuantity: null,
            SkuDimension: Dimension.Unknown,
            Price: 4200m,
            ListPrice: null,
            Scope: PriceScope.Store,
            Availability: AvailabilityStatus.InStock,
            Context: "super",
            PromotionConditions: null,
            CheckedAt: DateTime.UtcNow,
            StoreKey: "store:super"
        );

        var basket = new Basket
        {
            Name = "Canasta Prueba Extensibilidad",
            MaxStores = 1,
            PreferredStrategy = OptimizationStrategy.MaxSavings
        };
        basket.Items.Add(new BasketItem { Query = "Arroz 1000g", Quantity = 1 });

        var catalog = new List<RawCatalogOffer> { arrozBodega, arrozSupermercado };
        var settings = new AppSettings
        {
            MaxStores = 1,
            SizeTolerancePercent = 15,
            AllowSubstitutes = true,
            PriceTtlHours = 24
        };

        var solution = BasketOptimizer.Optimize(basket, catalog, settings);

        // Debe seleccionar Bodega Don Pepe por ser más económica (3200 vs 4200)
        Assert.Equal(1, solution.StoresUsedCount);
        Assert.Equal(1, solution.ItemsCoveredCount);
        Assert.Equal(0, solution.MissingItemsCount);
        Assert.Equal(3200m, solution.TotalCost);
        Assert.Single(solution.Stores);
        Assert.Equal("store:bodega", solution.Stores[0].StoreKey);
        Assert.Equal(3200m, solution.Stores[0].Subtotal);
    }
}
