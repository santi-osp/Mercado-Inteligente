using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure.Stores.Common;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.PriceSmart;

/// <summary>
/// PriceSmart Club de Compras (identificado inequívocamente para el requerimiento 'Price Mark / PriceSmart').
/// Opera con modelo de membresía y formatos de gran volumen / institucionales (Member's Selection).
/// </summary>
public sealed class PriceSmartProvider(
    MarketDb db,
    GenericStoreCatalogIngest ingest
) : IStoreProvider
{
    public string Code => "PRICESMART";

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
            new ProviderProduct("PS-PAPEL-32R", "Papel Higiénico Ultra Suave Member's Selection x 32 Rollos", "Member's Selection",
                new[] { new ProviderSku("PS-SKU-PH-32R", "Papel Higiénico 32 rollos", "088712345001", 32m, Dimension.Count) },
                "https://pricesmart.com/colombia/aseo/papel-higienico-32", "Club / Cuidado Hogar", "https://pricesmart.com/img/papel-32.jpg"),
            new ProviderOffer("PS-SKU-PH-32R", "PRICESMART", "PriceSmart Club de Membresía", SellerKind.RetailerDirect, 64900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Requiere membresía PriceSmart activa. Formato bulk.", 72000m)
        ),
        (
            new ProviderProduct("PS-DETERGENTE-9K", "Detergente Líquido Plus Member's Selection 9 L (150 lavadas)", "Member's Selection",
                new[] { new ProviderSku("PS-SKU-DET-9L", "Detergente Líquido 9 L", "088712345002", 9.0m, Dimension.Volume) },
                "https://pricesmart.com/colombia/aseo/detergente-9l", "Club / Lavado", "https://pricesmart.com/img/detergente-9l.jpg"),
            new ProviderOffer("PS-SKU-DET-9L", "PRICESMART", "PriceSmart Club de Membresía", SellerKind.RetailerDirect, 79900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Requiere membresía PriceSmart activa. Rinde 150 lavadas.", 89000m)
        ),
        (
            new ProviderProduct("PS-ACEITE-CANOLA-5L", "Aceite de Canola Puro Member's Selection 5 L", "Member's Selection",
                new[] { new ProviderSku("PS-SKU-AC-5L", "Aceite de Canola 5 L", "088712345003", 5.0m, Dimension.Volume) },
                "https://pricesmart.com/colombia/despensa/aceite-canola-5l", "Club / Despensa Familiar", "https://pricesmart.com/img/aceite-canola-5l.jpg"),
            new ProviderOffer("PS-SKU-AC-5L", "PRICESMART", "PriceSmart Club de Membresía", SellerKind.RetailerDirect, 52900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Requiere membresía PriceSmart activa.", 58900m)
        ),
        (
            new ProviderProduct("PS-ATUN-8L", "Atún Sólido en Agua Member's Selection Pack x 8 Latas (140 g c/u)", "Member's Selection",
                new[] { new ProviderSku("PS-SKU-AT-8L", "Atún en Agua Pack x 8 (1.12 kg total)", "088712345004", 1.12m, Dimension.Mass) },
                "https://pricesmart.com/colombia/despensa/atun-pack-8", "Club / Enlatados", "https://pricesmart.com/img/atun-pack-8.jpg"),
            new ProviderOffer("PS-SKU-AT-8L", "PRICESMART", "PriceSmart Club de Membresía", SellerKind.RetailerDirect, 36900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Requiere membresía PriceSmart activa. Pack 8 latas.", 41900m)
        ),
        (
            new ProviderProduct("PS-ARROZ-5K", "Arroz Extra Selección Especial Member's Selection 5 kg", "Member's Selection",
                new[] { new ProviderSku("PS-SKU-AR-5K", "Arroz Extra Grano Largo 5 kg", "088712345005", 5.0m, Dimension.Mass) },
                "https://pricesmart.com/colombia/despensa/arroz-5k", "Club / Granos", "https://pricesmart.com/img/arroz-5k.jpg"),
            new ProviderOffer("PS-SKU-AR-5K", "PRICESMART", "PriceSmart Club de Membresía", SellerKind.RetailerDirect, 22900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", "Requiere membresía PriceSmart activa.", 25500m)
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
            await ingest.IngestProductsAsync(Code, "PriceSmart Club de Compras", prods, p =>
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
            new("PS-MEDELLIN", "PriceSmart Medellín (El Poblado)", 6.2045m, -75.5768m)
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