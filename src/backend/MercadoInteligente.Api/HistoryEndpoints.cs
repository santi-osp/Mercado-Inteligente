using System.Text;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Api;

public static class HistoryEndpoints
{
    public static RouteGroupBuilder MapHistoryEndpoints(this RouteGroupBuilder api)
    {
    api.MapGet("/history", async (string? query, string? retailer, string? scope, int? limit, MarketDb db, CancellationToken ct) =>
    {
        var maxLimit = Math.Clamp(limit ?? 100, 1, 500);
    
        var baseQuery = from obs in db.PriceObservations.AsNoTracking()
                        join offer in db.Offers.AsNoTracking() on obs.OfferId equals offer.Id
                        join sku in db.ExternalSkus.AsNoTracking() on offer.ExternalSkuId equals sku.Id
                        join product in db.ExternalProducts.AsNoTracking() on sku.ExternalProductId equals product.Id
                        join ret in db.Retailers.AsNoTracking() on product.RetailerId equals ret.Id
                        join seller in db.Sellers.AsNoTracking() on offer.SellerId equals seller.Id
                        select new
                        {
                            ObservationId = obs.Id,
                            OfferId = offer.Id,
                            ProductName = product.Name,
                            Brand = product.Brand,
                            SkuName = sku.Name,
                            Ean = sku.Ean,
                            RetailerCode = ret.Code,
                            RetailerName = ret.Name,
                            SellerName = seller.Name,
                            Price = obs.Price,
                            ListPrice = obs.ListPrice,
                            Availability = obs.Availability,
                            Scope = obs.PriceScope,
                            Context = obs.PriceContext,
                            AvailableQuantity = obs.AvailableQuantity,
                            PromotionConditions = obs.PromotionConditions,
                            SellerId = obs.SellerId,
                            StoreId = obs.StoreId,
                            CheckedAt = obs.CheckedAt
                        };
    
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            baseQuery = baseQuery.Where(x => x.ProductName.Contains(q) || (x.Brand != null && x.Brand.Contains(q)) || x.SkuName.Contains(q));
        }
    
        if (!string.IsNullOrWhiteSpace(retailer))
        {
            var r = retailer.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(x => x.RetailerCode == r || x.RetailerName.Contains(r));
        }
    
        if (!string.IsNullOrWhiteSpace(scope) && Enum.TryParse<PriceScope>(scope.Trim(), true, out var parsedScope))
        {
            baseQuery = baseQuery.Where(x => x.Scope == parsedScope);
        }
    
        var items = await baseQuery
            .OrderByDescending(x => x.CheckedAt)
            .Take(maxLimit)
            .ToListAsync(ct);
    
        var observations = items.Select(x => new
        {
            id = x.ObservationId,
            offerId = x.OfferId,
            productName = x.ProductName,
            brand = x.Brand,
            skuName = x.SkuName,
            ean = x.Ean,
            retailerCode = x.RetailerCode,
            retailerName = x.RetailerName,
            sellerName = x.SellerName,
            price = x.Price,
            listPrice = x.ListPrice,
            availability = x.Availability.ToString(),
            scope = x.Scope?.ToString() ?? "UNKNOWN_HISTORICAL",
            context = x.Context,
            availableQuantity = x.AvailableQuantity,
            promotionConditions = x.PromotionConditions,
            sellerId = x.SellerId,
            storeId = x.StoreId,
            checkedAt = x.CheckedAt
        }).ToList();
    
        var total = observations.Count;
        var minPrice = total > 0 ? observations.Min(x => x.price) : (decimal?)null;
        var maxPrice = total > 0 ? observations.Max(x => x.price) : (decimal?)null;
        var avgPrice = total > 0 ? Math.Round(observations.Average(x => x.price), 2) : (decimal?)null;
        var uniqueProducts = observations.Select(x => x.productName).Distinct().Count();
        var uniqueRetailers = observations.Select(x => x.retailerCode).Distinct().Count();
    
        var productTrends = observations
            .GroupBy(x => new { x.productName, x.skuName, x.retailerCode })
            .Select(g =>
            {
                var obsList = g.OrderBy(x => x.checkedAt).ToList();
                var first = obsList.First().price;
                var latest = obsList.Last().price;
                return new
                {
                    productName = g.Key.productName,
                    skuName = g.Key.skuName,
                    retailerCode = g.Key.retailerCode,
                    count = g.Count(),
                    minPrice = g.Min(x => x.price),
                    maxPrice = g.Max(x => x.price),
                    latestPrice = latest,
                    priceDelta = latest - first,
                    latestCheckedAt = g.Max(x => x.checkedAt)
                };
            })
            .OrderByDescending(x => x.latestCheckedAt)
            .Take(25)
            .ToList();
    
        return Results.Ok(new
        {
            totalObservations = total,
            statistics = new
            {
                totalObservations = total,
                minPrice,
                maxPrice,
                avgPrice,
                uniqueProducts,
                uniqueRetailers
            },
            productTrends,
            observations
        });
    });
    
    api.MapGet("/export/prices.csv", async (string? retailer, string? scope, MarketDb db, CancellationToken ct) =>
    {
        var query = from obs in db.PriceObservations.AsNoTracking()
                    join offer in db.Offers.AsNoTracking() on obs.OfferId equals offer.Id
                    join sku in db.ExternalSkus.AsNoTracking() on offer.ExternalSkuId equals sku.Id
                    join product in db.ExternalProducts.AsNoTracking() on sku.ExternalProductId equals product.Id
                    join ret in db.Retailers.AsNoTracking() on product.RetailerId equals ret.Id
                    join seller in db.Sellers.AsNoTracking() on (obs.SellerId ?? offer.SellerId) equals seller.Id
                    select new
                    {
                        obs.CheckedAt,
                        RetailerCode = ret.Code,
                        SellerName = seller.Name,
                        SellerKind = seller.Kind,
                        ProductName = product.Name,
                        Brand = product.Brand,
                        sku.Ean,
                        obs.Price,
                        ListPrice = obs.ListPrice,
                        Availability = obs.Availability,
                        Scope = obs.PriceScope,
                        Context = obs.PriceContext,
                        PromotionConditions = obs.PromotionConditions,
                        AvailableQuantity = obs.AvailableQuantity,
                        StoreId = obs.StoreId,
                        SellerId = obs.SellerId
                    };

        if (!string.IsNullOrWhiteSpace(retailer))
        {
            var r = retailer.Trim().ToUpperInvariant();
            query = query.Where(x => x.RetailerCode == r);
        }

        if (!string.IsNullOrWhiteSpace(scope) && Enum.TryParse<PriceScope>(scope.Trim(), true, out var parsedScope))
        {
            query = query.Where(x => x.Scope == parsedScope);
        }

        var rows = await query.OrderByDescending(x => x.CheckedAt).Take(5000).ToListAsync(ct);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Fecha,Cadena,Vendedor,TipoVendedor,Producto,Marca,EAN,Precio,PrecioLista,Disponibilidad,Alcance,Contexto,CondicionesPromo,CantidadDisponible,StoreId,SellerId");

        foreach (var r in rows)
        {
            sb.Append(r.CheckedAt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(r.RetailerCode)).Append(',');
            sb.Append(EscapeCsv(r.SellerName)).Append(',');
            sb.Append(EscapeCsv(r.SellerKind.ToString())).Append(',');
            sb.Append(EscapeCsv(r.ProductName)).Append(',');
            sb.Append(EscapeCsv(r.Brand ?? "")).Append(',');
            sb.Append(EscapeCsv(r.Ean ?? "")).Append(',');
            sb.Append(r.Price.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(',');
            sb.Append(r.ListPrice.HasValue ? r.ListPrice.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) : "").Append(',');
            sb.Append(EscapeCsv(r.Availability.ToString())).Append(',');
            sb.Append(EscapeCsv(r.Scope?.ToString() ?? "UNKNOWN_HISTORICAL")).Append(',');
            sb.Append(EscapeCsv(r.Context ?? "")).Append(',');
            sb.Append(EscapeCsv(r.PromotionConditions ?? "")).Append(',');
            sb.Append(EscapeCsv(r.AvailableQuantity.HasValue ? r.AvailableQuantity.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "")).Append(',');
            sb.Append(EscapeCsv(r.StoreId?.ToString() ?? "")).Append(',');
            sb.AppendLine(EscapeCsv(r.SellerId?.ToString() ?? ""));
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var filename = $"mercado-precios-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return Results.File(bytes, "text/csv; charset=utf-8", filename);
    });

        return api;
    }

    static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}

