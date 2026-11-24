using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.Ara;

public sealed record CreateAraManualOfferRequest(
    string ProductName,
    string? Brand,
    decimal Quantity,
    string Unit,
    decimal Price,
    decimal? ListPrice = null,
    string? StoreName = null,
    string? StoreAddress = null,
    DateTime? ValidUntil = null,
    string? Notes = null
);

public sealed record AraManualOfferDto(
    Guid OfferId,
    Guid ProductId,
    string ProductName,
    string? Brand,
    decimal Quantity,
    string Unit,
    decimal Price,
    decimal? ListPrice,
    string? StoreName,
    string? StoreAddress,
    DateTime? ValidUntil,
    string? Notes,
    DateTime CreatedAt
);

public sealed class AraManualOfferService(MarketDb db, RawStore rawStore)
{
    public async Task<AraManualOfferDto> AddManualOfferAsync(CreateAraManualOfferRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ProductName))
            throw new ArgumentException("El nombre del producto es obligatorio.", nameof(request.ProductName));
        if (request.Price <= 0)
            throw new ArgumentException("El precio debe ser estrictamente mayor a 0.", nameof(request.Price));
        if (request.Quantity <= 0)
            throw new ArgumentException("La cantidad debe ser mayor a 0.", nameof(request.Quantity));
        if (string.IsNullOrWhiteSpace(request.Unit))
            throw new ArgumentException("La unidad de medida es obligatoria (ej: g, ml, und).", nameof(request.Unit));

        var now = DateTime.UtcNow;

        // Gate for raw preservation
        var gate = rawStore.Gate;
        await gate.WaitAsync(ct);
        RawCapture raw;
        try
        {
            var auditPayload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                source = "USER_MANUAL",
                retailer = "ARA",
                timestamp = now,
                entry = request
            });
            (raw, _) = await rawStore.SaveUnderGateAsync(auditPayload, ct);
        }
        finally
        {
            gate.Release();
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var retailer = await db.Retailers.FirstOrDefaultAsync(r => r.Code == "ARA", ct);
        if (retailer == null)
        {
            retailer = new Retailer { Code = "ARA", Name = "Tiendas Ara" };
            db.Retailers.Add(retailer);
            await db.SaveChangesAsync(ct);
        }

        var seller = await db.Sellers.FirstOrDefaultAsync(s => s.RetailerId == retailer.Id && s.ExternalId == "ARA_MANUAL", ct);
        if (seller == null)
        {
            seller = new Seller
            {
                RetailerId = retailer.Id,
                ExternalId = "ARA_MANUAL",
                Name = "Tiendas Ara (Aporte Usuario)",
                Kind = SellerKind.RetailerDirect
            };
            db.Sellers.Add(seller);
            await db.SaveChangesAsync(ct);
        }

        Store? store = null;
        if (!string.IsNullOrWhiteSpace(request.StoreName))
        {
            var cleanStoreName = request.StoreName.Trim();
            store = await db.Stores.FirstOrDefaultAsync(s => s.RetailerId == retailer.Id && s.Name == cleanStoreName, ct);
            if (store == null)
            {
                var storeExtId = "ara-store-" + Guid.NewGuid().ToString("N")[..8];
                store = new Store
                {
                    RetailerId = retailer.Id,
                    ExternalId = storeExtId,
                    Name = cleanStoreName
                };
                db.Stores.Add(store);
                await db.SaveChangesAsync(ct);
            }
        }

        var externalId = "ara-manual-" + Guid.NewGuid().ToString("N")[..10];
        var product = new ExternalProduct
        {
            RetailerId = retailer.Id,
            ExternalId = externalId,
            Name = request.ProductName.Trim(),
            Brand = request.Brand?.Trim(),
            CategoryPath = "Aportes Usuario/Ara",
            Status = ProductLifecycleState.Active,
            LastSeenAt = now,
            UpdatedAt = now
        };
        db.ExternalProducts.Add(product);
        await db.SaveChangesAsync(ct);

        var dimension = ResolveDimension(request.Unit);
        var sku = new ExternalSku
        {
            ExternalProductId = product.Id,
            ExternalId = externalId + "-sku",
            Name = product.Name,
            Quantity = request.Quantity,
            Dimension = dimension
        };
        db.ExternalSkus.Add(sku);
        await db.SaveChangesAsync(ct);

        var scope = store != null ? PriceScope.PhysicalVerified : PriceScope.Unknown;
        var conditions = request.ValidUntil.HasValue
            ? $"Vigente hasta: {request.ValidUntil.Value:yyyy-MM-dd}. {request.Notes?.Trim()}"
            : request.Notes?.Trim();

        var offer = new Offer
        {
            ExternalSkuId = sku.Id,
            SellerId = seller.Id,
            StoreId = store?.Id,
            Price = request.Price,
            ListPrice = request.ListPrice,
            Currency = "COP",
            Scope = scope,
            Availability = AvailabilityStatus.InStock,
            Context = "source:USER_MANUAL",
            PromotionConditions = conditions,
            CheckedAt = now,
            RawCaptureId = raw.Id
        };
        db.Offers.Add(offer);
        await db.SaveChangesAsync(ct);

        db.PriceObservations.Add(PriceObservationFactory.FromOfferSnapshot(offer, now));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new AraManualOfferDto(
            OfferId: offer.Id,
            ProductId: product.Id,
            ProductName: product.Name,
            Brand: product.Brand,
            Quantity: sku.Quantity ?? request.Quantity,
            Unit: request.Unit.Trim(),
            Price: offer.Price,
            ListPrice: offer.ListPrice,
            StoreName: store?.Name,
            StoreAddress: null,
            ValidUntil: request.ValidUntil,
            Notes: request.Notes,
            CreatedAt: now
        );
    }

    public async Task<IReadOnlyList<AraManualOfferDto>> GetManualOffersAsync(CancellationToken ct = default)
    {
        var retailer = await db.Retailers.AsNoTracking().FirstOrDefaultAsync(r => r.Code == "ARA", ct);
        if (retailer == null) return [];

        var offers = await (from o in db.Offers.AsNoTracking()
                            join s in db.ExternalSkus.AsNoTracking() on o.ExternalSkuId equals s.Id
                            join p in db.ExternalProducts.AsNoTracking() on s.ExternalProductId equals p.Id
                            join st in db.Stores.AsNoTracking() on o.StoreId equals st.Id into storeJoin
                            from st in storeJoin.DefaultIfEmpty()
                            where p.RetailerId == retailer.Id && o.Context == "source:USER_MANUAL"
                            orderby o.CheckedAt descending
                            select new
                            {
                                Offer = o,
                                Sku = s,
                                Product = p,
                                Store = st
                            }).ToListAsync(ct);

        return offers.Select(item => new AraManualOfferDto(
            OfferId: item.Offer.Id,
            ProductId: item.Product.Id,
            ProductName: item.Product.Name,
            Brand: item.Product.Brand,
            Quantity: item.Sku.Quantity ?? 1,
            Unit: item.Sku.Dimension.ToString().ToLowerInvariant(),
            Price: item.Offer.Price,
            ListPrice: item.Offer.ListPrice,
            StoreName: item.Store != null ? item.Store.Name : null,
            StoreAddress: null,
            ValidUntil: null,
            Notes: item.Offer.PromotionConditions,
            CreatedAt: item.Offer.CheckedAt
        )).ToList();
    }

    public async Task<bool> DeleteManualOfferAsync(Guid offerId, CancellationToken ct = default)
    {
        var offer = await db.Offers.FirstOrDefaultAsync(o => o.Id == offerId && o.Context == "source:USER_MANUAL", ct);
        if (offer == null) return false;

        var observations = await db.PriceObservations.Where(po => po.OfferId == offer.Id).ToListAsync(ct);
        db.PriceObservations.RemoveRange(observations);
        db.Offers.Remove(offer);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static Dimension ResolveDimension(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit)) return Dimension.Unknown;
        var u = unit.Trim().ToLowerInvariant();
        return u switch
        {
            "g" or "kg" or "gr" or "gramos" or "kilo" or "kilos" or "kilogramos" or "lb" or "libras" => Dimension.Mass,
            "ml" or "l" or "lt" or "litro" or "litros" or "mililitros" or "cm3" or "cc" => Dimension.Volume,
            "und" or "unid" or "unidad" or "unidades" or "piezas" or "pz" => Dimension.Count,
            "m" or "cm" or "metros" => Dimension.Length,
            _ => Dimension.Unknown
        };
    }
}
