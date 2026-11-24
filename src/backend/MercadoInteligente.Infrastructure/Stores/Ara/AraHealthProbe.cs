using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.Ara;

public sealed record AraHealthReport(
    string Provider,
    string Status,
    string Coverage,
    int TotalActiveOffers,
    int BrochureOffersCount,
    int ManualOffersCount,
    DateTime? LastOfferUpdate,
    string Summary
);

public sealed class AraHealthProbe(MarketDb db)
{
    public async Task<AraHealthReport> CheckHealthAsync(CancellationToken ct = default)
    {
        var retailer = await db.Retailers.AsNoTracking().FirstOrDefaultAsync(r => r.Code == "ARA", ct);
        if (retailer == null)
        {
            return new AraHealthReport(
                Provider: "ARA",
                Status: "PARTIAL_HONEST_BROCHURE_AND_MANUAL",
                Coverage: "Parcial (Folletos y entradas manuales)",
                TotalActiveOffers: 0,
                BrochureOffersCount: 0,
                ManualOffersCount: 0,
                LastOfferUpdate: null,
                Summary: "Proveedor Ara inicializado sin ofertas registradas aún."
            );
        }

        var offers = await (from o in db.Offers.AsNoTracking()
                            join s in db.ExternalSkus.AsNoTracking() on o.ExternalSkuId equals s.Id
                            join p in db.ExternalProducts.AsNoTracking() on s.ExternalProductId equals p.Id
                            where p.RetailerId == retailer.Id
                            select new { o.Id, o.Context, o.CheckedAt })
                            .ToListAsync(ct);

        var brochureCount = offers.Count(o => o.Context.StartsWith("brochure:", StringComparison.OrdinalIgnoreCase));
        var manualCount = offers.Count(o => o.Context.Equals("source:USER_MANUAL", StringComparison.OrdinalIgnoreCase));
        var lastUpdate = offers.Count > 0 ? offers.Max(o => o.CheckedAt) : (DateTime?)null;

        var summary = $"Operando bajo modelo honesto de cobertura parcial con {offers.Count} ofertas activas ({brochureCount} de folletos públicos, {manualCount} de aportes de usuario).";

        return new AraHealthReport(
            Provider: "ARA",
            Status: "PARTIAL_HONEST_BROCHURE_AND_MANUAL",
            Coverage: "Parcial (Folletos públicos y aportes comunitarios)",
            TotalActiveOffers: offers.Count,
            BrochureOffersCount: brochureCount,
            ManualOffersCount: manualCount,
            LastOfferUpdate: lastUpdate,
            Summary: summary
        );
    }
}
