using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MercadoInteligente.Infrastructure;

public sealed class CatalogReconciliationService(
    MarketDb db,
    RawStore rawStore,
    IProductLifecycleService lifecycleService,
    ILogger<CatalogReconciliationService> logger
)
{
    public async Task<int> ReconcileRemoteProductsAsync(
        string retailerCode,
        string retailerName,
        IReadOnlyList<ProviderProduct> products,
        Func<ProviderProduct, IReadOnlyList<ProviderOffer>> offersFactory,
        SearchSession? session = null,
        CancellationToken ct = default)
    {
        if (products.Count == 0) return 0;

        var gate = rawStore.Gate;
        await gate.WaitAsync(ct);
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var retailer = await db.Retailers.FirstOrDefaultAsync(r => r.Code == retailerCode, ct);
            if (retailer == null)
            {
                retailer = new Retailer { Code = retailerCode, Name = retailerName };
                db.Retailers.Add(retailer);
                await db.SaveChangesAsync(ct);
            }

            var seller = await db.Sellers.FirstOrDefaultAsync(s => s.RetailerId == retailer.Id, ct);
            if (seller == null)
            {
                seller = new Seller
                {
                    RetailerId = retailer.Id,
                    ExternalId = retailerCode,
                    Name = retailerName,
                    Kind = SellerKind.RetailerDirect
                };
                db.Sellers.Add(seller);
                await db.SaveChangesAsync(ct);
            }

            // Save Raw JSON audit
            var rawBytes = JsonSerializer.SerializeToUtf8Bytes(products);
            var (raw, _) = await rawStore.SaveUnderGateAsync(rawBytes, ct);

            int savedCount = 0;
            var now = DateTime.UtcNow;

            foreach (var p in products)
            {
                // Match strategy: 1. Retailer + ExternalId, 2. EAN within retailer
                var existingProd = await db.ExternalProducts
                    .FirstOrDefaultAsync(ep => ep.RetailerId == retailer.Id && ep.ExternalId == p.ExternalId, ct);

                if (existingProd == null && p.Skus.Count > 0)
                {
                    var eans = p.Skus.Where(s => !string.IsNullOrWhiteSpace(s.Ean)).Select(s => s.Ean!).ToList();
                    if (eans.Count > 0)
                    {
                        var matchingProd = await (from s in db.ExternalSkus
                                                  join ep in db.ExternalProducts on s.ExternalProductId equals ep.Id
                                                  where ep.RetailerId == retailer.Id && eans.Contains(s.Ean!)
                                                  select ep).FirstOrDefaultAsync(ct);
                        if (matchingProd != null)
                        {
                            existingProd = matchingProd;
                        }
                    }
                }

                if (existingProd == null)
                {
                    existingProd = new ExternalProduct
                    {
                        RetailerId = retailer.Id,
                        ExternalId = p.ExternalId,
                        Name = p.Name,
                        Brand = p.Brand,
                        CategoryPath = p.CategoryPath,
                        ProductUrl = p.ProductUrl,
                        ImageUrl = p.ImageUrl,
                        Status = ProductLifecycleState.Active,
                        MissingCount = 0,
                        LastSeenAt = now,
                        UpdatedAt = now
                    };
                    db.ExternalProducts.Add(existingProd);
                    await db.SaveChangesAsync(ct);
                    savedCount++;

                    if (session != null) session.NewCount++;
                }
                else
                {
                    lifecycleService.MarkProductSeen(existingProd, now);
                    existingProd.ProductUrl ??= p.ProductUrl;
                    existingProd.ImageUrl ??= p.ImageUrl;
                    existingProd.CategoryPath ??= p.CategoryPath;
                    existingProd.Brand ??= p.Brand;
                    if (!string.IsNullOrWhiteSpace(p.Name)) existingProd.Name = p.Name;
                    await db.SaveChangesAsync(ct);
                }

                var prodOffers = offersFactory(p);

                var existingSkus = await db.ExternalSkus
                    .Where(s => s.ExternalProductId == existingProd.Id)
                    .ToListAsync(ct);

                foreach (var sku in p.Skus)
                {
                    var existingSku = existingSkus.FirstOrDefault(s => s.ExternalId == sku.ExternalId)
                                      ?? existingSkus.FirstOrDefault(s => !string.IsNullOrEmpty(s.Ean) && s.Ean == sku.Ean);

                    if (existingSku == null)
                    {
                        var parsedQty = QuantityParser.Parse(sku.Name);
                        existingSku = new ExternalSku
                        {
                            ExternalProductId = existingProd.Id,
                            ExternalId = sku.ExternalId,
                            Name = sku.Name,
                            Ean = sku.Ean,
                            Quantity = sku.Quantity ?? parsedQty?.TotalAmount,
                            Dimension = sku.Dimension != Dimension.Unknown ? sku.Dimension : (parsedQty?.Dimension ?? Dimension.Unknown),
                            Unit = parsedQty?.OriginalUnit,
                            PackageCount = parsedQty?.PackageCount ?? 1,
                            QuantityPerUnit = parsedQty?.AmountPerUnit,
                            TotalQuantity = parsedQty?.TotalAmount
                        };
                        db.ExternalSkus.Add(existingSku);
                        await db.SaveChangesAsync(ct);
                        existingSkus.Add(existingSku);
                    }

                    var skuOffer = prodOffers.FirstOrDefault(o => o.SkuId == sku.ExternalId)
                                  ?? prodOffers.FirstOrDefault();

                    if (skuOffer != null)
                    {
                        var existingOffer = await db.Offers
                            .FirstOrDefaultAsync(o => o.ExternalSkuId == existingSku.Id && o.SellerId == seller.Id && o.Context == skuOffer.Context, ct);

                        if (existingOffer == null)
                        {
                            existingOffer = new Offer
                            {
                                ExternalSkuId = existingSku.Id,
                                SellerId = seller.Id,
                                Price = skuOffer.Price,
                                ListPrice = skuOffer.ListPrice,
                                Scope = skuOffer.Scope,
                                Availability = skuOffer.Availability,
                                Context = skuOffer.Context,
                                PromotionConditions = skuOffer.Conditions,
                                CheckedAt = now,
                                RawCaptureId = raw.Id
                            };
                            db.Offers.Add(existingOffer);
                            await db.SaveChangesAsync(ct);

                            db.PriceObservations.Add(PriceObservationFactory.FromOfferSnapshot(existingOffer, now));
                            if (session != null) session.NewCount++;
                        }
                        else
                        {
                            bool priceChanged = existingOffer.Price != skuOffer.Price || existingOffer.ListPrice != skuOffer.ListPrice;
                            bool promoChanged = existingOffer.PromotionConditions != skuOffer.Conditions;
                            bool availChanged = existingOffer.Availability != skuOffer.Availability;
                            bool scopeChanged = existingOffer.Scope != skuOffer.Scope;
                            bool isSignificantChange = priceChanged || promoChanged || availChanged || scopeChanged;

                            existingOffer.Price = skuOffer.Price;
                            existingOffer.ListPrice = skuOffer.ListPrice;
                            existingOffer.Availability = skuOffer.Availability;
                            existingOffer.Scope = skuOffer.Scope;
                            existingOffer.Context = skuOffer.Context;
                            existingOffer.PromotionConditions = skuOffer.Conditions;
                            existingOffer.CheckedAt = now;
                            await db.SaveChangesAsync(ct);

                            if (isSignificantChange)
                            {
                                db.PriceObservations.Add(PriceObservationFactory.FromOfferSnapshot(existingOffer, now));
                                if (session != null) session.UpdatedCount++;
                            }
                            else
                            {
                                if (session != null) session.UnchangedCount++;
                            }
                        }
                    }
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return savedCount;
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<bool> MarkExplicitUnavailableAsync(
        string retailerCode,
        string externalSkuId,
        string reason,
        CancellationToken ct = default)
    {
        var retailer = await db.Retailers.FirstOrDefaultAsync(r => r.Code == retailerCode, ct);
        if (retailer == null) return false;

        var sku = await (from s in db.ExternalSkus
                         join ep in db.ExternalProducts on s.ExternalProductId equals ep.Id
                         where s.ExternalId == externalSkuId && ep.RetailerId == retailer.Id
                         select s).FirstOrDefaultAsync(ct);
        if (sku == null) return false;

        var offer = await db.Offers.FirstOrDefaultAsync(o => o.ExternalSkuId == sku.Id, ct);
        if (offer == null) return false;

        var now = DateTime.UtcNow;
        offer.Availability = AvailabilityStatus.OutOfStock;
        offer.CheckedAt = now;

        db.PriceObservations.Add(PriceObservationFactory.FromOfferSnapshot(offer, now));
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> ReconcileCompletedCategoryAsync(
        string retailerCode,
        string categoryPath,
        IReadOnlyCollection<string> returnedExternalProductIds,
        CancellationToken ct = default)
    {
        var retailer = await db.Retailers.FirstOrDefaultAsync(r => r.Code == retailerCode, ct);
        if (retailer == null) return 0;

        var now = DateTime.UtcNow;
        var existingProducts = await db.ExternalProducts
            .Where(p => p.RetailerId == retailer.Id && p.CategoryPath != null &&
                       (p.CategoryPath == categoryPath || p.CategoryPath.StartsWith(categoryPath)))
            .ToListAsync(ct);

        int missingMarked = 0;
        foreach (var p in existingProducts)
        {
            if (!returnedExternalProductIds.Contains(p.ExternalId))
            {
                p.MissingCount++;
                if (p.MissingCount >= 3)
                {
                    p.Status = ProductLifecycleState.Inactive;
                }
                else if (p.MissingCount >= 1)
                {
                    p.Status = ProductLifecycleState.Missing;
                }
                p.UpdatedAt = now;
                missingMarked++;
            }
            else
            {
                lifecycleService.MarkProductSeen(p, now);
            }
        }

        await db.SaveChangesAsync(ct);
        return missingMarked;
    }
}