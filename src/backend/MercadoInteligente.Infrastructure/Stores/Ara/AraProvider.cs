using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.Ara;

/// <summary>
/// Proveedor honesto para Tiendas Ara.
/// Clasificación de diagnóstico D: no realiza scraping invasivo ni reverse engineering no autorizado.
/// Opera estrictamente bajo modelo de cobertura parcial sustentado en:
/// 1. Ingesta de folletos públicos y rebajas de temporada (campañas quincenales).
/// 2. Registro manual o asistido por el usuario (ofertas observadas físicamente).
///
/// Garantía de honestidad:
/// - NO afirma catálogo completo (CatalogEnumeration = NotSupported).
/// - NO afirma stock en tiempo real (Availability = NotSupported).
/// - NO afirma precios específicos por sucursal a menos que el usuario lo indique expresamente en una entrada manual.
/// </summary>
public sealed class AraProvider(MarketDb db) : IStoreProvider
{
    public string Code => "ARA";

    public ProviderCapabilities Capabilities => DeclaredCapabilities;

    public static ProviderCapabilities DeclaredCapabilities { get; } = new(
        Search: CapabilityState.Partial,
        Details: CapabilityState.Partial,
        Offers: CapabilityState.Partial,
        Stores: CapabilityState.Partial,
        Availability: CapabilityState.NotSupported,
        CatalogEnumeration: CapabilityState.NotSupported, CategoryEnumeration: CapabilityState.Partial
    );

    public async Task<IReadOnlyList<ProviderProduct>> SearchAsync(string query, ProviderContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(query)) return [];

        var retailer = await db.Retailers.AsNoTracking().FirstOrDefaultAsync(r => r.Code == "ARA", cancellationToken);
        if (retailer == null) return [];

        var lower = query.Trim().ToLowerInvariant();

        var products = await db.ExternalProducts
            .AsNoTracking()
            .Where(p => p.RetailerId == retailer.Id && (p.Name.ToLower().Contains(lower) || (p.Brand != null && p.Brand.ToLower().Contains(lower))))
            .Take(30)
            .ToListAsync(cancellationToken);

        var productIds = products.Select(p => p.Id).ToList();

        var skus = await db.ExternalSkus
            .AsNoTracking()
            .Where(s => productIds.Contains(s.ExternalProductId))
            .ToListAsync(cancellationToken);

        var skuMap = skus.GroupBy(s => s.ExternalProductId)
            .ToDictionary(g => g.Key, g => g.Select(s => new ProviderSku(s.ExternalId, s.Name, s.Ean, s.Quantity, s.Dimension)).ToList());

        return products.Select(p => new ProviderProduct(
            ExternalId: p.ExternalId,
            Name: p.Name,
            Brand: p.Brand,
            Skus: skuMap.TryGetValue(p.Id, out var pSkus) ? pSkus : [],
            ProductUrl: p.ProductUrl,
            CategoryPath: p.CategoryPath
        )).ToList();
    }

    public async Task<ProviderProduct?> GetProductAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(externalProductId)) return null;

        var retailer = await db.Retailers.AsNoTracking().FirstOrDefaultAsync(r => r.Code == "ARA", cancellationToken);
        if (retailer == null) return null;

        var product = await db.ExternalProducts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.RetailerId == retailer.Id && p.ExternalId == externalProductId, cancellationToken);

        if (product == null) return null;

        var skus = await db.ExternalSkus
            .AsNoTracking()
            .Where(s => s.ExternalProductId == product.Id)
            .Select(s => new ProviderSku(s.ExternalId, s.Name, s.Ean, s.Quantity, s.Dimension))
            .ToListAsync(cancellationToken);

        return new ProviderProduct(
            ExternalId: product.ExternalId,
            Name: product.Name,
            Brand: product.Brand,
            Skus: skus,
            ProductUrl: product.ProductUrl,
            CategoryPath: product.CategoryPath
        );
    }

    public async Task<IReadOnlyList<ProviderOffer>> OffersAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (string.IsNullOrWhiteSpace(externalProductId)) return [];

        var retailer = await db.Retailers.AsNoTracking().FirstOrDefaultAsync(r => r.Code == "ARA", cancellationToken);
        if (retailer == null) return [];

        var product = await db.ExternalProducts
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.RetailerId == retailer.Id && p.ExternalId == externalProductId, cancellationToken);

        if (product == null) return [];

        var skus = await db.ExternalSkus
            .AsNoTracking()
            .Where(s => s.ExternalProductId == product.Id)
            .ToListAsync(cancellationToken);

        var skuIds = skus.Select(s => s.Id).ToList();

        var offers = await (from o in db.Offers.AsNoTracking()
                            join s in db.Sellers.AsNoTracking() on o.SellerId equals s.Id
                            where skuIds.Contains(o.ExternalSkuId)
                            select new { Offer = o, Seller = s })
                            .ToListAsync(cancellationToken);

        var skuExtIdMap = skus.ToDictionary(s => s.Id, s => s.ExternalId);

        return offers.Select(item => new ProviderOffer(
            SkuId: skuExtIdMap[item.Offer.ExternalSkuId],
            SellerId: item.Seller.ExternalId,
            SellerName: item.Seller.Name,
            SellerKind: item.Seller.Kind,
            Price: item.Offer.Price,
            Scope: item.Offer.Scope,
            Availability: item.Offer.Availability,
            Context: item.Offer.Context,
            Conditions: item.Offer.PromotionConditions,
            ListPrice: item.Offer.ListPrice,
            AvailableQuantityRaw: item.Offer.AvailableQuantityRaw
        )).ToList();
    }

    public async Task<IReadOnlyList<ProviderStore>> GetStoresAsync(ProviderContext context, CancellationToken cancellationToken)
    {
        var retailer = await db.Retailers.AsNoTracking().FirstOrDefaultAsync(r => r.Code == "ARA", cancellationToken);
        if (retailer == null) return [];

        var stores = await db.Stores
            .AsNoTracking()
            .Where(s => s.RetailerId == retailer.Id)
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);

        return stores.Select(s => new ProviderStore(s.ExternalId, s.Name, s.Latitude, s.Longitude)).ToList();
    }

    public Task<IReadOnlyList<ProviderProduct>> EnumerateCatalogAsync(ProviderContext context, CancellationToken cancellationToken)
    {
        throw new NotSupportedException(
            "El catálogo completo no está disponible para Tiendas Ara. " +
            "El proveedor opera honestamente sobre folletos públicos promocionales y entradas manuales de la comunidad."
        );
    }
}
