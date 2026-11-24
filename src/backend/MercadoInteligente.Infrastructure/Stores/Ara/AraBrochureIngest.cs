using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.Ara;

public sealed record AraBrochureItem(
    string ExternalId,
    string Name,
    string? Brand,
    decimal Quantity,
    string Unit,
    decimal Price,
    decimal? ListPrice = null,
    string? Conditions = null,
    string? Category = null
);

public sealed record AraBrochure(
    string CampaignName,
    DateTime ValidFrom,
    DateTime ValidTo,
    IReadOnlyList<AraBrochureItem> Items,
    string? Source = "PUBLIC_PROMO"
);

public sealed record AraBrochureIngestResult(
    string CampaignName,
    int ItemsReceived,
    int OffersProcessed,
    int OffersChanged,
    int ProductsCreated,
    int ProductsUpdated,
    bool IsActive,
    DateTime ValidFrom,
    DateTime ValidTo
);

public sealed class AraBrochureIngest(MarketDb db, RawStore rawStore)
{
    private const int MaxPayloadBytes = 2 * 1024 * 1024; // 2MB limit

    public async Task<AraBrochureIngestResult> IngestAsync(byte[] rawPayload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rawPayload);
        if (rawPayload.Length == 0) throw new ArgumentException("El payload del folleto no puede estar vacío.", nameof(rawPayload));
        if (rawPayload.Length > MaxPayloadBytes) throw new ArgumentException($"El payload excede el límite de {MaxPayloadBytes} bytes.", nameof(rawPayload));

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        AraBrochure? brochure;
        try
        {
            brochure = JsonSerializer.Deserialize<AraBrochure>(rawPayload, options);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Formato JSON inválido para el folleto de Ara.", ex);
        }

        if (brochure == null || string.IsNullOrWhiteSpace(brochure.CampaignName))
            throw new InvalidOperationException("El folleto debe especificar un nombre de campaña válido.");

        if (brochure.ValidTo < brochure.ValidFrom)
            throw new InvalidOperationException("La fecha de vencimiento del folleto no puede ser anterior a la fecha de inicio.");

        if (brochure.Items == null || brochure.Items.Count == 0)
            throw new InvalidOperationException("El folleto no contiene productos o promociones.");

        var gate = rawStore.Gate;
        await gate.WaitAsync(ct);
        try
        {
            return await IngestUnderGateAsync(rawPayload, brochure, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<AraBrochureIngestResult> IngestUnderGateAsync(byte[] rawPayload, AraBrochure brochure, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var (raw, _) = await rawStore.SaveUnderGateAsync(rawPayload, ct);

        var retailer = await db.Retailers.FirstOrDefaultAsync(r => r.Code == "ARA", ct);
        if (retailer == null)
        {
            retailer = new Retailer { Code = "ARA", Name = "Tiendas Ara" };
            db.Retailers.Add(retailer);
            await db.SaveChangesAsync(ct);
        }

        var seller = await db.Sellers.FirstOrDefaultAsync(s => s.RetailerId == retailer.Id && s.ExternalId == "ARA_BROCHURE", ct);
        if (seller == null)
        {
            seller = new Seller
            {
                RetailerId = retailer.Id,
                ExternalId = "ARA_BROCHURE",
                Name = "Tiendas Ara (Folleto Público)",
                Kind = SellerKind.RetailerDirect
            };
            db.Sellers.Add(seller);
            await db.SaveChangesAsync(ct);
        }

        var now = DateTime.UtcNow;
        var isActive = now >= brochure.ValidFrom && now <= brochure.ValidTo;
        var context = $"brochure:{brochure.CampaignName.Trim()}";

        var offersProcessed = 0;
        var offersChanged = 0;
        var productsCreated = 0;
        var productsUpdated = 0;

        foreach (var item in brochure.Items)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalId) || string.IsNullOrWhiteSpace(item.Name) || item.Price <= 0)
                continue;

            var extId = item.ExternalId.Trim();
            var product = await db.ExternalProducts.FirstOrDefaultAsync(p => p.RetailerId == retailer.Id && p.ExternalId == extId, ct);

            if (product == null)
            {
                product = new ExternalProduct
                {
                    RetailerId = retailer.Id,
                    ExternalId = extId,
                    Name = item.Name.Trim(),
                    Brand = item.Brand?.Trim(),
                    CategoryPath = item.Category?.Trim(),
                    Status = ProductLifecycleState.Active,
                    LastSeenAt = now,
                    UpdatedAt = now
                };
                db.ExternalProducts.Add(product);
                productsCreated++;
            }
            else
            {
                product.Name = item.Name.Trim();
                if (!string.IsNullOrWhiteSpace(item.Brand)) product.Brand = item.Brand.Trim();
                if (!string.IsNullOrWhiteSpace(item.Category)) product.CategoryPath = item.Category.Trim();
                product.Status = ProductLifecycleState.Active;
                product.LastSeenAt = now;
                product.UpdatedAt = now;
                productsUpdated++;
            }
            await db.SaveChangesAsync(ct);

            // SKU
            var skuExtId = $"{extId}-sku";
            var sku = await db.ExternalSkus.FirstOrDefaultAsync(s => s.ExternalProductId == product.Id && s.ExternalId == skuExtId, ct);

            var unitDimension = ResolveDimension(item.Unit);
            if (sku == null)
            {
                sku = new ExternalSku
                {
                    ExternalProductId = product.Id,
                    ExternalId = skuExtId,
                    Name = product.Name,
                    Quantity = item.Quantity > 0 ? item.Quantity : 1,
                    Dimension = unitDimension
                };
                db.ExternalSkus.Add(sku);
            }
            else
            {
                sku.Quantity = item.Quantity > 0 ? item.Quantity : sku.Quantity;
                sku.Dimension = unitDimension;
            }
            await db.SaveChangesAsync(ct);

            // Offer
            var conditions = $"Vigencia: {brochure.ValidFrom:yyyy-MM-dd} a {brochure.ValidTo:yyyy-MM-dd}";
            if (!string.IsNullOrWhiteSpace(item.Conditions))
                conditions = $"{item.Conditions.Trim()} | {conditions}";

            var offer = await db.Offers.FirstOrDefaultAsync(o => o.ExternalSkuId == sku.Id && o.SellerId == seller.Id && o.Context == context, ct);
            var availability = isActive ? AvailabilityStatus.InStock : AvailabilityStatus.Unknown;

            if (offer == null)
            {
                offer = new Offer
                {
                    ExternalSkuId = sku.Id,
                    SellerId = seller.Id,
                    Price = item.Price,
                    ListPrice = item.ListPrice,
                    Currency = "COP",
                    Scope = PriceScope.OnlineGeneric,
                    Availability = availability,
                    Context = context,
                    PromotionConditions = conditions,
                    CheckedAt = now,
                    RawCaptureId = raw.Id
                };
                db.Offers.Add(offer);
                await db.SaveChangesAsync(ct);

                db.PriceObservations.Add(PriceObservationFactory.FromOfferSnapshot(offer, now));
                offersChanged++;
            }
            else
            {
                var priceChanged = offer.Price != item.Price || offer.Availability != availability;
                offer.Price = item.Price;
                offer.ListPrice = item.ListPrice;
                offer.Availability = availability;
                offer.PromotionConditions = conditions;
                offer.CheckedAt = now;
                offer.RawCaptureId = raw.Id;

                if (priceChanged)
                {
                    db.PriceObservations.Add(PriceObservationFactory.FromOfferSnapshot(offer, now));
                    offersChanged++;
                }
            }

            offersProcessed++;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new AraBrochureIngestResult(
            CampaignName: brochure.CampaignName,
            ItemsReceived: brochure.Items.Count,
            OffersProcessed: offersProcessed,
            OffersChanged: offersChanged,
            ProductsCreated: productsCreated,
            ProductsUpdated: productsUpdated,
            IsActive: isActive,
            ValidFrom: brochure.ValidFrom,
            ValidTo: brochure.ValidTo
        );
    }

    private static Dimension ResolveDimension(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return Dimension.Unknown;
        var u = unit.Trim().ToLowerInvariant();
        return u switch
        {
            "g" or "kg" or "gr" or "gramos" or "kilo" or "kilos" or "kilogramos" or "lb" or "libras" or "libra" => Dimension.Mass,
            "ml" or "l" or "lt" or "litro" or "litros" or "mililitros" or "cm3" or "cc" => Dimension.Volume,
            "und" or "unid" or "unidad" or "unidades" or "piezas" or "pz" or "rollos" or "bolsas" => Dimension.Count,
            "m" or "cm" or "metros" => Dimension.Length,
            _ => Dimension.Unknown
        };
    }
}
