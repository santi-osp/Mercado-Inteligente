using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure.Stores.Common;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.Jumbo;

public sealed class JumboProvider(
    MarketDb db,
    GenericStoreCatalogIngest ingest
) : IStoreProvider
{
    public string Code => "JUMBO";

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
            new ProviderProduct("JMB-SALMON-500", "Salmón Noruego Fresco Filete Cuisine & Co 500 g", "Cuisine & Co",
                new[] { new ProviderSku("JMB-SKU-SAL-500", "Filete Salmón Noruego 500 g", "770333200001", 0.5m, Dimension.Mass) },
                "https://tienda.tiendasjumbo.co/pescaderia/salmon-500g", "Pescadería / Salmón", "https://tienda.tiendasjumbo.co/img/salmon.jpg"),
            new ProviderOffer("JMB-SKU-SAL-500", "JUMBO", "Hipermercados Jumbo", SellerKind.RetailerDirect, 38900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Importado calidad premium", 44900m)
        ),
        (
            new ProviderProduct("JMB-QUESO-PARM-200", "Queso Parmesano Reggiano Importado Cuisine & Co 200 g", "Cuisine & Co",
                new[] { new ProviderSku("JMB-SKU-QP-200", "Queso Parmesano 200 g", "770333200002", 0.2m, Dimension.Mass) },
                "https://tienda.tiendasjumbo.co/lacteos/queso-parmesano-200g", "Lácteos / Madurados", "https://tienda.tiendasjumbo.co/img/parmesano.jpg"),
            new ProviderOffer("JMB-SKU-QP-200", "JUMBO", "Hipermercados Jumbo", SellerKind.RetailerDirect, 19500m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "D.O.P. Selección", 22900m)
        ),
        (
            new ProviderProduct("JMB-ACEITE-OLIVA-500", "Aceite de Oliva Extra Virgen Cuisine & Co 500 ml", "Cuisine & Co",
                new[] { new ProviderSku("JMB-SKU-AO-500", "Aceite de Oliva Extra Virgen 500 ml", "770333200003", 0.5m, Dimension.Volume) },
                "https://tienda.tiendasjumbo.co/despensa/aceite-oliva-500ml", "Despensa / Aceites Gourmet", "https://tienda.tiendasjumbo.co/img/oliva.jpg"),
            new ProviderOffer("JMB-SKU-AO-500", "JUMBO", "Hipermercados Jumbo", SellerKind.RetailerDirect, 27900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 31900m)
        ),
        (
            new ProviderProduct("JMB-CAFE-GRANO-454", "Café Premium Grano Juan Valdez 454 g", "Juan Valdez",
                new[] { new ProviderSku("JMB-SKU-JV-454", "Café Grano Juan Valdez 454 g", "770333200004", 0.454m, Dimension.Mass) },
                "https://tienda.tiendasjumbo.co/despensa/cafe-juan-valdez-454g", "Despensa / Café", "https://tienda.tiendasjumbo.co/img/cafe-jv.jpg"),
            new ProviderOffer("JMB-SKU-JV-454", "JUMBO", "Hipermercados Jumbo", SellerKind.RetailerDirect, 31500m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 34900m)
        ),
        (
            new ProviderProduct("JMB-DETERGENTE-ARIEL-4K", "Detergente Líquido Ariel Concentrado 4 L", "Ariel",
                new[] { new ProviderSku("JMB-SKU-AR-4L", "Detergente Líquido Ariel 4 L", "770333200005", 4.0m, Dimension.Volume) },
                "https://tienda.tiendasjumbo.co/aseo/detergente-ariel-4l", "Aseo / Cuidado Ropa", "https://tienda.tiendasjumbo.co/img/ariel-4l.jpg"),
            new ProviderOffer("JMB-SKU-AR-4L", "JUMBO", "Hipermercados Jumbo", SellerKind.RetailerDirect, 49900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Ahorro volumen Jumbo", 56900m)
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
            await ingest.IngestProductsAsync(Code, "Hipermercados Jumbo", prods, p =>
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
            new("JMB-VEGAS", "Jumbo Las Vegas", 6.2034m, -75.5789m),
            new("JMB-SANTAFE", "Jumbo Santafé Medellín", 6.1978m, -75.5734m),
            new("JMB-65", "Jumbo Carrera 65", 6.2601m, -75.5812m)
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