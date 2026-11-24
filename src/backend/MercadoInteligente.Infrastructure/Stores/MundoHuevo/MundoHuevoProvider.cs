using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure.Stores.Common;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.MundoHuevo;

public sealed class MundoHuevoProvider(
    MarketDb db,
    GenericStoreCatalogIngest ingest
) : IStoreProvider
{
    public string Code => "MUNDOHUEVO";

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
            new ProviderProduct("MH-HUEVO-AA-30", "Huevo Rojo AA Cubeta x 30 Mundo Huevo", "Mundo Huevo",
                new[] { new ProviderSku("MH-SKU-AA-30", "Huevo Rojo Calibre AA x 30 unidades", "770999100001", 30m, Dimension.Count) },
                "https://mundohuevo.com/productos/huevo-aa-30", "Huevos / Rojos", "https://mundohuevo.com/img/cubeta-aa-30.jpg"),
            new ProviderOffer("MH-SKU-AA-30", "MUNDOHUEVO", "Mundo Huevo Especialista", SellerKind.RetailerDirect, 16500m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Calibre AA seleccionado", 18000m)
        ),
        (
            new ProviderProduct("MH-HUEVO-AAA-30", "Huevo Rojo AAA Cubeta x 30 Mundo Huevo", "Mundo Huevo",
                new[] { new ProviderSku("MH-SKU-AAA-30", "Huevo Rojo Calibre AAA x 30 unidades", "770999100002", 30m, Dimension.Count) },
                "https://mundohuevo.com/productos/huevo-aaa-30", "Huevos / Rojos", "https://mundohuevo.com/img/cubeta-aaa-30.jpg"),
            new ProviderOffer("MH-SKU-AAA-30", "MUNDOHUEVO", "Mundo Huevo Especialista", SellerKind.RetailerDirect, 18900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Calibre AAA extra grande", 20500m)
        ),
        (
            new ProviderProduct("MH-HUEVO-JUMBO-30", "Huevo Jumbo Especial Cubeta x 30 Mundo Huevo", "Mundo Huevo",
                new[] { new ProviderSku("MH-SKU-JB-30", "Huevo Calibre Jumbo x 30 unidades", "770999100003", 30m, Dimension.Count) },
                "https://mundohuevo.com/productos/huevo-jumbo-30", "Huevos / Especiales", "https://mundohuevo.com/img/cubeta-jumbo-30.jpg"),
            new ProviderOffer("MH-SKU-JB-30", "MUNDOHUEVO", "Mundo Huevo Especialista", SellerKind.RetailerDirect, 21900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Calibre Jumbo peso superior", 23500m)
        ),
        (
            new ProviderProduct("MH-HUEVO-CAMP-30", "Huevo Gallina Feliz / Campesino x 30 Mundo Huevo", "Mundo Huevo",
                new[] { new ProviderSku("MH-SKU-CAMP-30", "Huevo Campesino Gallina Feliz x 30 unidades", "770999100004", 30m, Dimension.Count) },
                "https://mundohuevo.com/productos/huevo-campesino-30", "Huevos / Libres de Jaula", "https://mundohuevo.com/img/cubeta-campesino-30.jpg"),
            new ProviderOffer("MH-SKU-CAMP-30", "MUNDOHUEVO", "Mundo Huevo Especialista", SellerKind.RetailerDirect, 23500m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Pastoreo / Gallina Feliz", 25900m)
        ),
        (
            new ProviderProduct("MH-HUEVO-A-30", "Huevo Rojo Calibre A Cubeta x 30 Mundo Huevo", "Mundo Huevo",
                new[] { new ProviderSku("MH-SKU-A-30", "Huevo Rojo Calibre A x 30 unidades", "770999100005", 30m, Dimension.Count) },
                "https://mundohuevo.com/productos/huevo-a-30", "Huevos / Económicos", "https://mundohuevo.com/img/cubeta-a-30.jpg"),
            new ProviderOffer("MH-SKU-A-30", "MUNDOHUEVO", "Mundo Huevo Especialista", SellerKind.RetailerDirect, 14200m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Calibre A estándar", 15500m)
        ),
        (
            new ProviderProduct("MH-HUEVO-CODORNIZ-24", "Huevo de Codorniz Bandeja x 24 Mundo Huevo", "Mundo Huevo",
                new[] { new ProviderSku("MH-SKU-COD-24", "Huevo de Codorniz x 24 unidades", "770999100006", 24m, Dimension.Count) },
                "https://mundohuevo.com/productos/huevo-codorniz-24", "Huevos / Codorniz", "https://mundohuevo.com/img/bandeja-codorniz.jpg"),
            new ProviderOffer("MH-SKU-COD-24", "MUNDOHUEVO", "Mundo Huevo Especialista", SellerKind.RetailerDirect, 5800m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 6400m)
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
            await ingest.IngestProductsAsync(Code, "Mundo Huevo Especialista", prods, p =>
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
            new("MH-LAURELES", "Mundo Huevo Laureles", 6.2415m, -75.5942m),
            new("MH-BELEN", "Mundo Huevo Belén", 6.2301m, -75.5998m),
            new("MH-POBLADO", "Mundo Huevo Poblado", 6.2089m, -75.5714m)
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