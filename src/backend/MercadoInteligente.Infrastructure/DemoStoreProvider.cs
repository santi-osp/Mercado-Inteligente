using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure;

/// <summary>Synthetic fixtures for development and tests only; never register in production.</summary>
public sealed class DemoStoreProvider : IStoreProvider
{
    public string Code => "DEMO";
    public bool IsDemo => true;
    public ProviderCapabilities Capabilities { get; } = new(
        CapabilityState.Supported, CapabilityState.Supported, CapabilityState.Supported,
        CapabilityState.NotSupported, CapabilityState.NotSupported, CapabilityState.Supported);

    private static readonly IReadOnlyList<ProviderProduct> Products = Array.AsReadOnly(new[]
    {
        new ProviderProduct("DEMO-P1", "[DEMO] Arroz 1 kg", "DEMO", Array.AsReadOnly(new[]
        {
            new ProviderSku("DEMO-S1", "[DEMO] Arroz 1 kg", null, 1m, Dimension.Mass)
        })),
        new ProviderProduct("DEMO-P2", "[DEMO] Leche 1 L", "DEMO", Array.AsReadOnly(new[]
        {
            new ProviderSku("DEMO-S2", "[DEMO] Leche 1 L", null, 1m, Dimension.Volume)
        }))
    });

    public Task<IReadOnlyList<ProviderProduct>> SearchAsync(string query, ProviderContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<ProviderProduct> matches = string.IsNullOrWhiteSpace(query)
            ? [] : Products.Where(p => p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        return Task.FromResult(matches);
    }

    public Task<ProviderProduct?> GetProductAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(Products.FirstOrDefault(p => p.ExternalId == externalProductId));
    }

    public Task<IReadOnlyList<ProviderOffer>> OffersAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        IReadOnlyList<ProviderOffer> offers = externalProductId switch
        {
            "DEMO-P1" => [new("DEMO-S1", "DEMO-SELLER", "[DEMO] Seller", SellerKind.RetailerDirect,
                5000m, PriceScope.OnlineGeneric, AvailabilityStatus.Unknown, "DEMO")],
            "DEMO-P2" => [new("DEMO-S2", "DEMO-SELLER", "[DEMO] Seller", SellerKind.RetailerDirect,
                4200m, PriceScope.OnlineGeneric, AvailabilityStatus.Unknown, "DEMO")],
            _ => []
        };
        return Task.FromResult(offers);
    }

    public Task<IReadOnlyList<ProviderStore>> GetStoresAsync(ProviderContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult<IReadOnlyList<ProviderStore>>([]);
    }

    public Task<IReadOnlyList<ProviderProduct>> EnumerateCatalogAsync(ProviderContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        return Task.FromResult(Products);
    }
}

