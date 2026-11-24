using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure.Stores.Common;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.LaVaquita;

public sealed class LaVaquitaProvider(
    MarketDb db,
    GenericStoreCatalogIngest ingest
) : IStoreProvider
{
    public string Code => "LAVAQUITA";

    public ProviderCapabilities Capabilities => new(
        Search: CapabilityState.Supported,
        Details: CapabilityState.Partial,
        Offers: CapabilityState.Supported,
        Stores: CapabilityState.Supported,
        Availability: CapabilityState.Partial,
        CatalogEnumeration: CapabilityState.NotSupported, CategoryEnumeration: CapabilityState.Supported
    );

    private static readonly IReadOnlyList<(ProviderProduct Product, ProviderOffer Offer)> DefaultCatalog = new List<(ProviderProduct, ProviderOffer)>
    {
        (
            new ProviderProduct("LV-CARNE-MOL-500", "Carne Molida Especial La Vaquita 500 g", "La Vaquita",
                new[] { new ProviderSku("LV-SKU-CM-500", "Carne Molida Especial 500 g", null, 0.5m, Dimension.Mass) },
                "https://cavaquita.com/carnes/molida-especial", "Carnes / Res", "https://cavaquita.com/img/carne-molida.jpg"),
            new ProviderOffer("LV-SKU-CM-500", "LAVAQUITA", "La Vaquita", SellerKind.RetailerDirect, 12900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 14500m)
        ),
        (
            new ProviderProduct("LV-PECHUGA-1K", "Pechuga de Pollo Entera La Vaquita 1 kg", "La Vaquita",
                new[] { new ProviderSku("LV-SKU-PP-1K", "Pechuga de Pollo 1 kg", null, 1.0m, Dimension.Mass) },
                "https://cavaquita.com/carnes/pechuga-pollo", "Carnes / Aves", "https://cavaquita.com/img/pechuga.jpg"),
            new ProviderOffer("LV-SKU-PP-1K", "LAVAQUITA", "La Vaquita", SellerKind.RetailerDirect, 15800m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 17200m)
        ),
        (
            new ProviderProduct("LV-TOMATE-1K", "Tomate Chonto Seleccionado 1 kg", "Fruver La Vaquita",
                new[] { new ProviderSku("LV-SKU-TC-1K", "Tomate Chonto 1 kg", null, 1.0m, Dimension.Mass) },
                "https://cavaquita.com/fruver/tomate-chonto", "Fruver / Verduras", "https://cavaquita.com/img/tomate.jpg"),
            new ProviderOffer("LV-SKU-TC-1K", "LAVAQUITA", "La Vaquita", SellerKind.RetailerDirect, 4200m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 4800m)
        ),
        (
            new ProviderProduct("LV-AGUACATE-500", "Aguacate Hass La Vaquita 500 g", "Fruver La Vaquita",
                new[] { new ProviderSku("LV-SKU-AH-500", "Aguacate Hass 500 g", null, 0.5m, Dimension.Mass) },
                "https://cavaquita.com/fruver/aguacate-hass", "Fruver / Frutas", "https://cavaquita.com/img/aguacate.jpg"),
            new ProviderOffer("LV-SKU-AH-500", "LAVAQUITA", "La Vaquita", SellerKind.RetailerDirect, 5500m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 6200m)
        ),
        (
            new ProviderProduct("LV-QUESITO-500", "Quesito Antioqueño Tradicional 500 g", "La Vaquita",
                new[] { new ProviderSku("LV-SKU-QA-500", "Quesito Antioqueño 500 g", null, 0.5m, Dimension.Mass) },
                "https://cavaquita.com/lacteos/quesito-antioqueno", "Lácteos / Quesos", "https://cavaquita.com/img/quesito.jpg"),
            new ProviderOffer("LV-SKU-QA-500", "LAVAQUITA", "La Vaquita", SellerKind.RetailerDirect, 8900m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 9800m)
        ),
        (
            new ProviderProduct("LV-LECHE-1L", "Leche Entera Colanta 1 L", "Colanta",
                new[] { new ProviderSku("LV-SKU-LC-1L", "Leche Entera Colanta 1 L", "7702001000101", 1.0m, Dimension.Volume) },
                "https://cavaquita.com/lacteos/leche-colanta-1l", "Lácteos / Leche", "https://cavaquita.com/img/leche.jpg"),
            new ProviderOffer("LV-SKU-LC-1L", "LAVAQUITA", "La Vaquita", SellerKind.RetailerDirect, 4300m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 4700m)
        ),
        (
            new ProviderProduct("LV-ARROZ-1K", "Arroz Diana Tradicional 1 kg", "Diana",
                new[] { new ProviderSku("LV-SKU-AD-1K", "Arroz Diana 1 kg", "7702511000201", 1.0m, Dimension.Mass) },
                "https://cavaquita.com/abarrotes/arroz-diana-1k", "Abarrotes / Granos", "https://cavaquita.com/img/arroz.jpg"),
            new ProviderOffer("LV-SKU-AD-1K", "LAVAQUITA", "La Vaquita", SellerKind.RetailerDirect, 4600m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 5000m)
        ),
        (
            new ProviderProduct("LV-FRIJOL-500", "Frijol Cargamanto Rojo La Vaquita 500 g", "La Vaquita",
                new[] { new ProviderSku("LV-SKU-FC-500", "Frijol Cargamanto Rojo 500 g", null, 0.5m, Dimension.Mass) },
                "https://cavaquita.com/abarrotes/frijol-cargamanto", "Abarrotes / Granos", "https://cavaquita.com/img/frijol.jpg"),
            new ProviderOffer("LV-SKU-FC-500", "LAVAQUITA", "La Vaquita", SellerKind.RetailerDirect, 6800m,
                PriceScope.FulfillmentRegion, AvailabilityStatus.InStock, "fulfillment:medellin-aburra", null, 7400m)
        )
    };

    public async Task<IReadOnlyList<ProviderProduct>> SearchAsync(string query, ProviderContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(query)) return [];

        var lower = query.Trim().ToLowerInvariant();

        // 1. Check if matching items exist in local database
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
            await ingest.IngestProductsAsync(Code, "Supermercado La Vaquita", prods, p =>
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
            new("LAVAQUITA-LAURELES", "La Vaquita Laureles (Av 33)", 6.2372m, -75.5921m),
            new("LAVAQUITA-POBLADO", "La Vaquita Poblado (Calle 10)", 6.2105m, -75.5702m),
            new("LAVAQUITA-SABANETA", "La Vaquita Sabaneta", 6.1512m, -76.6154m)
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