using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure.Stores.Common;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.Olimpica;

public sealed class OlimpicaProvider(
    MarketDb db,
    GenericStoreCatalogIngest ingest
) : IStoreProvider
{
    public string Code => "OLIMPICA";

    public ProviderCapabilities Capabilities => new(
        Search: CapabilityState.Supported,
        Details: CapabilityState.Partial,
        Offers: CapabilityState.Supported,
        Stores: CapabilityState.Supported,
        Availability: CapabilityState.Supported,
        CatalogEnumeration: CapabilityState.NotSupported, CategoryEnumeration: CapabilityState.Supported
    );

    private static readonly IReadOnlyList<(ProviderProduct Product, ProviderOffer Offer)> DefaultCatalog = new List<(ProviderProduct, ProviderOffer)>
    {
        (
            new ProviderProduct("OLI-LECHE-DES-1L", "Leche Deslactosada Medalla de Oro Olímpica 1 L", "Medalla de Oro",
                new[] { new ProviderSku("OLI-SKU-LD-1L", "Leche Deslactosada 1 L", "770212345001", 1.0m, Dimension.Volume) },
                "https://olimpica.com/lacteos/leche-deslactosada-1l", "Lácteos / Leche", "https://olimpica.com/img/leche-des-1l.jpg"),
            new ProviderOffer("OLI-SKU-LD-1L", "OLIMPICA", "Supertiendas Olímpica", SellerKind.RetailerDirect, 3990m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Marca Propia Olímpica", 4400m)
        ),
        (
            new ProviderProduct("OLI-ARROZ-1K", "Arroz Blanco Medalla de Oro 1 kg", "Medalla de Oro",
                new[] { new ProviderSku("OLI-SKU-AR-1K", "Arroz Blanco 1 kg", "770212345002", 1.0m, Dimension.Mass) },
                "https://olimpica.com/despensa/arroz-1k", "Despensa / Granos", "https://olimpica.com/img/arroz-1k.jpg"),
            new ProviderOffer("OLI-SKU-AR-1K", "OLIMPICA", "Supertiendas Olímpica", SellerKind.RetailerDirect, 4100m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 4500m)
        ),
        (
            new ProviderProduct("OLI-ACEITE-900", "Aceite Vegetal Medalla de Oro 900 ml", "Medalla de Oro",
                new[] { new ProviderSku("OLI-SKU-AC-900", "Aceite Vegetal 900 ml", "770212345003", 0.9m, Dimension.Volume) },
                "https://olimpica.com/despensa/aceite-900ml", "Despensa / Aceites", "https://olimpica.com/img/aceite-900ml.jpg"),
            new ProviderOffer("OLI-SKU-AC-900", "OLIMPICA", "Supertiendas Olímpica", SellerKind.RetailerDirect, 7800m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 8900m)
        ),
        (
            new ProviderProduct("OLI-ATUN-140", "Atún Lomitos en Agua Medalla de Oro 140 g", "Medalla de Oro",
                new[] { new ProviderSku("OLI-SKU-AT-140", "Atún en Agua 140 g", "770212345004", 0.14m, Dimension.Mass) },
                "https://olimpica.com/enlatados/atun-agua-140g", "Enlatados / Pescados", "https://olimpica.com/img/atun-140g.jpg"),
            new ProviderOffer("OLI-SKU-AT-140", "OLIMPICA", "Supertiendas Olímpica", SellerKind.RetailerDirect, 5200m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 5900m)
        ),
        (
            new ProviderProduct("OLI-PAPEL-12", "Papel Higiénico Medalla de Oro x 12 Rollos", "Medalla de Oro",
                new[] { new ProviderSku("OLI-SKU-PH-12", "Papel Higiénico x 12 rollos", "770212345005", 12m, Dimension.Count) },
                "https://olimpica.com/aseo/papel-higienico-12r", "Aseo Hogar / Papeles", "https://olimpica.com/img/papel-12r.jpg"),
            new ProviderOffer("OLI-SKU-PH-12", "OLIMPICA", "Supertiendas Olímpica", SellerKind.RetailerDirect, 19900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Ahorra Pack x 12", 22500m)
        ),
        (
            new ProviderProduct("OLI-DETERGENTE-3K", "Detergente en Polvo Medalla de Oro 3 kg", "Medalla de Oro",
                new[] { new ProviderSku("OLI-SKU-DET-3K", "Detergente en Polvo 3 kg", "770212345006", 3.0m, Dimension.Mass) },
                "https://olimpica.com/aseo/detergente-3k", "Aseo Hogar / Lavado", "https://olimpica.com/img/detergente-3k.jpg"),
            new ProviderOffer("OLI-SKU-DET-3K", "OLIMPICA", "Supertiendas Olímpica", SellerKind.RetailerDirect, 21900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Rendimiento familiar", 24900m)
        )
    };

    public async Task<IReadOnlyList<ProviderProduct>> SearchAsync(string query, ProviderContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(query)) return [];

        var lower = query.Trim().ToLowerInvariant();

        // 1. Check local database
        var retailer = await db.Retailers.AsNoTracking().FirstOrDefaultAsync(r => r.Code == Code, cancellationToken);
        if (retailer != null)
        {
            var dbProducts = await db.ExternalProducts.AsNoTracking()
                .Where(p => p.RetailerId == retailer.Id && (p.Name.ToLower().Contains(lower) || (p.Brand != null && p.Brand.ToLower().Contains(lower))))
                .Take(25)
                .ToListAsync(cancellationToken);

            if (dbProducts.Count > 0)
            {
                var prodIds = dbProducts.Select(p => p.Id).ToList();
                var dbSkus = await db.ExternalSkus.AsNoTracking()
                    .Where(s => prodIds.Contains(s.ExternalProductId))
                    .ToListAsync(cancellationToken);

                return dbProducts.Select(p =>
                {
                    var skus = dbSkus.Where(s => s.ExternalProductId == p.Id).ToList();
                    return new ProviderProduct(
                        p.ExternalId, p.Name, p.Brand,
                        skus.Select(s => new ProviderSku(s.ExternalId, s.Name, s.Ean, s.Quantity, s.Dimension)).ToList(),
                        p.ProductUrl, p.CategoryPath, p.ImageUrl
                    );
                }).ToList();
            }
        }

        // 2. Fallback to catalog seed
        var tokens = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var matched = DefaultCatalog
            .Where(x =>
            {
                var name = x.Product.Name.ToLowerInvariant();
                var brand = x.Product.Brand?.ToLowerInvariant() ?? "";
                var cat = x.Product.CategoryPath?.ToLowerInvariant() ?? "";
                return tokens.Any(t =>
                    name.Contains(t) ||
                    brand.Contains(t) ||
                    cat.Contains(t) ||
                    (t.Length > 4 && t.EndsWith('s') && (name.Contains(t[..^1]) || cat.Contains(t[..^1])))
                );
            })
            .ToList();

        if (matched.Count > 0)
        {
            var prods = matched.Select(x => x.Product).ToList();
            await ingest.IngestProductsAsync(Code, "Supertiendas Olímpica", prods, p =>
            {
                var pair = matched.FirstOrDefault(m => m.Product.ExternalId == p.ExternalId);
                return pair.Offer != null ? new[] { pair.Offer } : Array.Empty<ProviderOffer>();
            }, cancellationToken);
            return prods;
        }

        return [];
    }

    public Task<ProviderProduct?> GetProductAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
    {
        var item = DefaultCatalog.FirstOrDefault(x => x.Product.ExternalId == externalProductId);
        return Task.FromResult<ProviderProduct?>(item.Product);
    }

    public Task<IReadOnlyList<ProviderOffer>> OffersAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
    {
        var item = DefaultCatalog.FirstOrDefault(x => x.Product.ExternalId == externalProductId);
        IReadOnlyList<ProviderOffer> offers = item.Offer != null ? new[] { item.Offer } : [];
        return Task.FromResult(offers);
    }

    public Task<IReadOnlyList<ProviderStore>> GetStoresAsync(ProviderContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProviderStore> stores = new List<ProviderStore>
        {
            new("OLI-CENTRO", "Olímpica Junín Centro", 6.2514m, -75.5681m),
            new("OLI-BELEN", "Olímpica Belén Los Alpes", 6.2321m, -75.6012m),
            new("OLI-ENVIGADO", "Olímpica Envigado Principal", 6.1689m, -75.5843m)
        };
        return Task.FromResult(stores);
    }

    public Task<IReadOnlyList<CatalogCategory>> EnumerateCategoriesAsync(ProviderContext context, CancellationToken cancellationToken)
    {
        var categories = DefaultCatalog
            .Select(x => x.Product.CategoryPath)
            .Where(cat => !string.IsNullOrWhiteSpace(cat))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(cat => new CatalogCategory(
                cat!.ToLowerInvariant().Replace(" ", "-").Replace("/", "-"),
                cat!,
                null,
                DefaultCatalog.Count(x => string.Equals(x.Product.CategoryPath, cat, StringComparison.OrdinalIgnoreCase))
            ))
            .ToList();
        return Task.FromResult<IReadOnlyList<CatalogCategory>>(categories);
    }

    public Task<IReadOnlyList<ProviderProduct>> EnumerateCatalogByCategoryAsync(string categoryId, ProviderContext context, CancellationToken cancellationToken)
    {
        var matched = DefaultCatalog
            .Where(x => x.Product.CategoryPath != null &&
                       (x.Product.CategoryPath.Equals(categoryId, StringComparison.OrdinalIgnoreCase) ||
                        x.Product.CategoryPath.ToLowerInvariant().Replace(" ", "-").Replace("/", "-") == categoryId ||
                        x.Product.CategoryPath.Contains(categoryId, StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.Product)
            .ToList();
        return Task.FromResult<IReadOnlyList<ProviderProduct>>(matched);
    }

    public Task<IReadOnlyList<ProviderProduct>> EnumerateCatalogAsync(ProviderContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"La enumeración exhaustiva no está soportada para {Code}.");
}