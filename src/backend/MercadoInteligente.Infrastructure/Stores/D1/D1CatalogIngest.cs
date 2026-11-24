using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.D1;

// Accepts an already fetched catalog response; callers must not label an unverified context regional.
public sealed class D1CatalogIngest(MarketDb db, RawStore rawStore)
{
    public async Task<int> SaveAsync(byte[] payload, ProviderContext context, PriceScope scope, DateTime checkedAt, CancellationToken ct) =>
        (await SaveWithMetricsAsync(payload, context, scope, checkedAt, ct)).OffersProcessed;

    internal Task<D1IngestMetrics> SaveWithMetricsAsync(byte[] payload, ProviderContext context, PriceScope scope, DateTime checkedAt, CancellationToken ct)
    {
        if (scope != PriceScope.OnlineGeneric || context.Key != "online-generic")
            throw new InvalidOperationException("Regional capture requires a verified session and a dedicated ingestion path.");
        return SaveValidatedAsync(payload, context, scope, checkedAt, ct);
    }

    internal async Task<int> SaveRegionalAsync(byte[] payload, string regionId, DateTime checkedAt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(regionId)) throw new InvalidOperationException("D1 region was not verified.");
        return (await SaveValidatedAsync(payload, new ProviderContext(regionId), PriceScope.FulfillmentRegion, checkedAt, ct)).OffersProcessed;
    }

    private async Task<D1IngestMetrics> SaveValidatedAsync(byte[] payload, ProviderContext context, PriceScope scope, DateTime checkedAt, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(context);
        if (checkedAt.Kind != DateTimeKind.Utc) throw new ArgumentException("A UTC capture time is required.", nameof(checkedAt));
        using var document = JsonDocument.Parse(payload);
        var products = D1Mapper.Products(document.RootElement);
        if (products.Count == 0) throw new JsonException("D1 catalog has no valid products.");
        var offers = products.ToDictionary(x => x.ExternalId,
            x => D1Mapper.Offers(document.RootElement, x.ExternalId, context), StringComparer.Ordinal);
        foreach (var product in products)
        {
            var ids = product.Skus.Select(x => x.ExternalId).ToHashSet(StringComparer.Ordinal);
            if (offers[product.ExternalId].Any(x => !ids.Contains(x.SkuId)))
                throw new JsonException("D1 offer references a missing SKU.");
        }
        var gate = rawStore.Gate;
        await gate.WaitAsync(ct);
        try { return await SaveInsideGateAsync(payload, products, offers, context, scope, checkedAt, ct); }
        finally { gate.Release(); }
    }

    private async Task<D1IngestMetrics> SaveInsideGateAsync(byte[] payload, IReadOnlyList<ProviderProduct> products,
        Dictionary<string, IReadOnlyList<ProviderOffer>> offers, ProviderContext context, PriceScope scope, DateTime checkedAt, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var (raw, rawExisting) = await rawStore.SaveUnderGateAsync(payload, ct);
        var retailer = await db.Retailers.SingleOrDefaultAsync(x => x.Code == "D1", ct);
        if (retailer is null)
        {
            retailer = new Retailer { Code = "D1", Name = "D1" };
            db.Retailers.Add(retailer);
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
                (incoming.CategoryPath is not null && product.CategoryPath != incoming.CategoryPath))) productsUpdated++;
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
                    sku.Quantity != sourceSku.Quantity || sku.Dimension != sourceSku.Dimension)) skusUpdated++;
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
        return new D1IngestMetrics(count, changedOffers, productsCreated, productsUpdated, skusCreated, skusUpdated,
            rawExisting ? 0 : 1, rawExisting ? 1 : 0);
    }
}

internal sealed record D1IngestMetrics(int OffersProcessed, int OffersChanged, int ProductsCreated, int ProductsUpdated,
    int SkusCreated, int SkusUpdated, int RawNew, int RawDeduplicated);
