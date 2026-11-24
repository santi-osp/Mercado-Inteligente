using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure;

public static class CatalogOfferLoader
{
    public static Task<IReadOnlyList<RawCatalogOffer>> LoadOffersAsync(
        MarketDb db,
        CancellationToken ct) => LoadOffersAsync(db, null, null, ct);

    public static async Task<IReadOnlyList<RawCatalogOffer>> LoadOffersAsync(
        MarketDb db,
        ShoppingContext? context = null,
        IOfferContextPolicy? policy = null,
        CancellationToken ct = default)
    {
        var rawOffers = await (from offer in db.Offers.AsNoTracking()
                      join sku in db.ExternalSkus.AsNoTracking() on offer.ExternalSkuId equals sku.Id
                      join product in db.ExternalProducts.AsNoTracking() on sku.ExternalProductId equals product.Id
                      join retailer in db.Retailers.AsNoTracking() on product.RetailerId equals retailer.Id
                      join seller in db.Sellers.AsNoTracking() on offer.SellerId equals seller.Id
                      join store in db.Stores.AsNoTracking() on offer.StoreId equals store.Id into storeJoin
                      from store in storeJoin.DefaultIfEmpty()
                      where product.Status == ProductLifecycleState.Active
                      select new RawCatalogOffer(
                          offer.Id,
                          sku.Id,
                          product.Id,
                          retailer.Id,
                          retailer.Code,
                          retailer.Name,
                          offer.StoreId,
                          store != null ? store.Name : null,
                          seller.Id,
                          seller.Name,
                          seller.Kind,
                          product.Name,
                          product.Brand,
                          sku.Name,
                          sku.Ean,
                          sku.Quantity,
                          sku.Dimension,
                          offer.Price,
                          offer.ListPrice,
                          offer.Scope,
                          offer.Availability,
                          offer.Context,
                          offer.PromotionConditions,
                          offer.CheckedAt,
                          null,
                          false,
                          null
                      )).ToListAsync(ct);

        if (context == null || policy == null)
        {
            return rawOffers;
        }

        var result = new List<RawCatalogOffer>(rawOffers.Count);
        foreach (var offer in rawOffers)
        {
            var providerContext = context.GetContextForProvider(offer.RetailerCode)
                                  ?? context.GetContextForRetailer(offer.RetailerId);

            var evaluation = policy.Evaluate(offer, providerContext);
            if (evaluation.Compatibility == OfferCompatibility.Incompatible)
            {
                continue;
            }

            var isFallback = evaluation.IsFallback || evaluation.Compatibility == OfferCompatibility.Fallback;
            result.Add(offer with
            {
                IsFallbackContext = isFallback,
                ContextWarning = isFallback ? evaluation.Reason : null
            });
        }

        return result;
    }
}