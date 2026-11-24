using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.Exito;

// Accepts an already fetched FastStore SearchQuery catalog response; callers must not label an unverified context regional.
public sealed class ExitoCatalogIngest(MarketDb db, RawStore rawStore)
{
    public async Task<int> SaveAsync(byte[] payload, ProviderContext context, PriceScope scope, DateTime checkedAt,
        bool includeMarketplace = false, CancellationToken ct = default) =>
        (await SaveWithMetricsAsync(payload, context, scope, checkedAt, includeMarketplace, ct)).OffersProcessed;

    internal Task<ExitoIngestMetrics> SaveWithMetricsAsync(byte[] payload, ProviderContext context, PriceScope scope,
        DateTime checkedAt, bool includeMarketplace, CancellationToken ct)
    {
        if (context.Key == "online-generic" && scope != PriceScope.OnlineGeneric)
            throw new InvalidOperationException("Generic online context requires PriceScope.OnlineGeneric.");
        if (context.Key.StartsWith("fulfillment:", StringComparison.Ordinal) && scope != PriceScope.FulfillmentRegion)
            throw new InvalidOperationException("Fulfillment region context requires PriceScope.FulfillmentRegion.");
        return SaveValidatedAsync(payload, context, scope, checkedAt, includeMarketplace, ct);
    }

    private async Task<ExitoIngestMetrics> SaveValidatedAsync(byte[] payload, ProviderContext context, PriceScope scope,
        DateTime checkedAt, bool includeMarketplace, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(context);
        if (checkedAt.Kind != DateTimeKind.Utc) throw new ArgumentException("A UTC capture time is required.", nameof(checkedAt));

        using var document = JsonDocument.Parse(payload);
        var nodes = ExitoMapper.Nodes(document.RootElement);
        if (nodes.Count == 0) return new ExitoIngestMetrics(0, 0, 0, 0, 0, 0, 0, 0);

        var products = ExitoMapper.Products(nodes);
        if (products.Count == 0) throw new JsonException("Exito catalog has no valid products.");

        var offers = products.ToDictionary(
            x => x.ExternalId,
            x => ExitoMapper.Offers(nodes, x.ExternalId, context.Key, scope, includeMarketplace),
            StringComparer.Ordinal);

        foreach (var product in products)
        {
            var ids = product.Skus.Select(x => x.ExternalId).ToHashSet(StringComparer.Ordinal);
            if (offers[product.ExternalId].Any(x => !ids.Contains(x.SkuId)))
                throw new JsonException("Exito offer references a missing SKU.");
        }

        var gate = rawStore.Gate;
        await gate.WaitAsync(ct);
        try
        {
            return await SaveInsideGateAsync(payload, products, offers, context, scope, checkedAt, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<ExitoIngestMetrics> SaveInsideGateAsync(byte[] payload, IReadOnlyList<ProviderProduct> products,
        Dictionary<string, IReadOnlyList<ProviderOffer>> offers, ProviderContext context, PriceScope scope, DateTime checkedAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (raw, rawExisting) = await rawStore.SaveUnderGateAsync(payload, ct);
        var retailer = await db.Retailers.SingleOrDefaultAsync(x => x.Code == "EXITO", ct);
        if (retailer is null)
        {
            retailer = new Retailer { Code = "EXITO", Name = "Éxito" };
            db.Retailers.Add(retailer);
            await db.SaveChangesAsync(ct);
        }

        var count = 0;
        var changedOffers = 0;
        var productsCreated = 0;
        var productsUpdated = 0;
        var skusCreated = 0;
        var skusUpdated = 0;

        foreach (var incoming in products)
        {
            var product = await db.ExternalProducts.SingleOrDefaultAsync(x => x.RetailerId == retailer.Id && x.ExternalId == incoming.ExternalId, ct);
            var updateMetadata = product is null || product.UpdatedAt <= checkedAt;
            if (product is null)
            {
                product = new ExternalProduct { RetailerId = retailer.Id, ExternalId = incoming.ExternalId };
                db.ExternalProducts.Add(product);
                productsCreated++;
            }
            else if (updateMetadata && (product.Name != incoming.Name ||
                (incoming.Brand is not null && product.Brand != incoming.Brand) ||
                (incoming.ImageUrl is not null && product.ImageUrl != incoming.ImageUrl) ||
                (incoming.ProductUrl is not null && product.ProductUrl != incoming.ProductUrl) ||
                (incoming.CategoryPath is not null && product.CategoryPath != incoming.CategoryPath)))
            {
                productsUpdated++;
            }

            if (updateMetadata)
            {
                product.Name = incoming.Name;
                product.Brand = incoming.Brand ?? product.Brand;
                product.ImageUrl = incoming.ImageUrl ?? product.ImageUrl;
                product.ProductUrl = incoming.ProductUrl ?? product.ProductUrl;
                product.CategoryPath = incoming.CategoryPath ?? product.CategoryPath;
                product.UpdatedAt = checkedAt;
            }

            foreach (var sourceSku in incoming.Skus)
            {
                var sku = await db.ExternalSkus.SingleOrDefaultAsync(x => x.ExternalProductId == product.Id && x.ExternalId == sourceSku.ExternalId, ct);
                if (sku is null)
                {
                    sku = new ExternalSku { ExternalProductId = product.Id, ExternalId = sourceSku.ExternalId };
                    db.ExternalSkus.Add(sku);
                    skusCreated++;
                }
                else if (updateMetadata && (sku.Name != sourceSku.Name || sku.Ean != sourceSku.Ean ||
                    sku.Quantity != sourceSku.Quantity || sku.Dimension != sourceSku.Dimension))
                {
                    skusUpdated++;
                }

                if (updateMetadata || db.Entry(sku).State == EntityState.Added)
                {
                    sku.Name = sourceSku.Name;
                    sku.Ean = sourceSku.Ean;
                    var pack = QuantityParser.Parse(sourceSku.Name);
                    sku.Quantity = pack?.TotalAmount ?? sourceSku.Quantity;
                    sku.Dimension = pack?.Dimension ?? sourceSku.Dimension;
                    sku.PackageCount = pack?.PackageCount;
                    sku.QuantityPerUnit = pack?.AmountPerUnit;
                    sku.Unit = pack?.OriginalUnit;
                    sku.TotalQuantity = pack?.TotalAmount;
                }
            }

            await db.SaveChangesAsync(ct);

            foreach (var sourceOffer in offers[incoming.ExternalId])
            {
                var seller = await db.Sellers.SingleOrDefaultAsync(x => x.RetailerId == retailer.Id && x.ExternalId == sourceOffer.SellerId, ct);
                if (seller is null)
                {
                    seller = new Seller { RetailerId = retailer.Id, ExternalId = sourceOffer.SellerId };
                    db.Sellers.Add(seller);
                }
                seller.Name = sourceOffer.SellerName;
                seller.Kind = sourceOffer.SellerKind;

                var sku = await db.ExternalSkus.SingleAsync(x => x.ExternalProductId == product.Id && x.ExternalId == sourceOffer.SkuId, ct);
                var offer = await db.Offers.SingleOrDefaultAsync(x => x.ExternalSkuId == sku.Id && x.SellerId == seller.Id && x.Context == context.Key, ct);
                if (offer is not null && offer.CheckedAt > checkedAt)
                    continue; // A late response must not replace newer evidence.

                if (offer is null)
                {
                    offer = new Offer { ExternalSkuId = sku.Id, SellerId = seller.Id, Context = context.Key };
                    db.Offers.Add(offer);
                }

                var changed = offer.Price != sourceOffer.Price || offer.Availability != sourceOffer.Availability || offer.Scope != scope ||
                    offer.ListPrice != sourceOffer.ListPrice || offer.PromotionConditions != sourceOffer.Conditions ||
                    offer.AvailableQuantityRaw != sourceOffer.AvailableQuantityRaw;

                offer.ListPrice = sourceOffer.ListPrice;
                offer.AvailableQuantityRaw = sourceOffer.AvailableQuantityRaw;
                offer.PromotionConditions = sourceOffer.Conditions;
                offer.Price = sourceOffer.Price;
                offer.Scope = scope;
                offer.Availability = sourceOffer.Availability;
                offer.CheckedAt = checkedAt;
                offer.RawCaptureId = raw.Id;

                if (changed)
                {
                    changedOffers++;
                    db.PriceObservations.Add(PriceObservationFactory.FromOfferSnapshot(offer, checkedAt));
                }
                count++;
                await db.SaveChangesAsync(ct);
            }
        }

        await transaction.CommitAsync(ct);
        return new ExitoIngestMetrics(count, changedOffers, productsCreated, productsUpdated, skusCreated, skusUpdated,
            rawExisting ? 0 : 1, rawExisting ? 1 : 0);
    }
}

public sealed record ExitoIngestMetrics(int OffersProcessed, int OffersChanged, int ProductsCreated, int ProductsUpdated,
    int SkusCreated, int SkusUpdated, int RawNew, int RawDeduplicated);
