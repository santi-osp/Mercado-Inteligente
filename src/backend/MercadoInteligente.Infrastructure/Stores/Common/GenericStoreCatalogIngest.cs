using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.Common;

public sealed class GenericStoreCatalogIngest(MarketDb db, RawStore rawStore)
{
    public async Task<int> IngestProductsAsync(
        string retailerCode,
        string retailerName,
        IReadOnlyList<ProviderProduct> products,
        Func<ProviderProduct, IReadOnlyList<ProviderOffer>> offersFactory,
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

            var seller = await db.Sellers.FirstOrDefaultAsync(s => s.RetailerId == retailer.Id && s.Name == retailerName, ct);
            if (seller == null)
            {
                seller = new Seller
                {
                    RetailerId = retailer.Id,
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
                var existingProd = await db.ExternalProducts
                    .FirstOrDefaultAsync(ep => ep.RetailerId == retailer.Id && ep.ExternalId == p.ExternalId, ct);

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
                        LastSeenAt = now,
                        UpdatedAt = now
                    };
                    db.ExternalProducts.Add(existingProd);
                    await db.SaveChangesAsync(ct);
                    savedCount++;
                }
                else
                {
                    existingProd.LastSeenAt = now;
                    existingProd.ProductUrl ??= p.ProductUrl;
                    existingProd.ImageUrl ??= p.ImageUrl;
                    existingProd.CategoryPath ??= p.CategoryPath;
                    existingProd.Brand ??= p.Brand;
                    existingProd.UpdatedAt = now;
                    await db.SaveChangesAsync(ct);
                }

                var prodOffers = offersFactory(p);

                var existingSkus = await db.ExternalSkus
                    .Where(s => s.ExternalProductId == existingProd.Id)
                    .ToListAsync(ct);

                foreach (var sku in p.Skus)
                {
                    var existingSku = existingSkus.FirstOrDefault(s => s.ExternalId == sku.ExternalId);
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
                            .FirstOrDefaultAsync(o => o.ExternalSkuId == existingSku.Id && o.SellerId == seller.Id, ct);

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
                        }
                        else
                        {
                            existingOffer.Price = skuOffer.Price;
                            existingOffer.ListPrice = skuOffer.ListPrice;
                            existingOffer.Availability = skuOffer.Availability;
                            existingOffer.Scope = skuOffer.Scope;
                            existingOffer.Context = skuOffer.Context;
                            existingOffer.PromotionConditions = skuOffer.Conditions;
                            existingOffer.CheckedAt = now;
                            await db.SaveChangesAsync(ct);
                        }

                        // Save price observation snapshot
                        db.PriceObservations.Add(new PriceObservation
                        {
                            OfferId = existingOffer.Id,
                            Price = existingOffer.Price,
                            ListPrice = existingOffer.ListPrice,
                            Availability = existingOffer.Availability,
                            PromotionConditions = existingOffer.PromotionConditions,
                            PriceScope = existingOffer.Scope,
                            PriceContext = existingOffer.Context,
                            SellerId = existingOffer.SellerId,
                            RawCaptureId = raw.Id,
                            ObservedAt = now
                        });
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
}
