using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public interface IFreshnessService
{
    Freshness Evaluate(DateTime? checkedAtUtc, TimeSpan ttl, TimeSpan? outdatedThreshold = null, DateTime? nowUtc = null);
    Freshness GetPriceFreshness(DateTime? checkedAtUtc, AppSettings settings, DateTime? nowUtc = null);
    Freshness GetStockFreshness(DateTime? checkedAtUtc, AppSettings settings, DateTime? nowUtc = null);
    Freshness GetProductFreshness(DateTime? updatedAtUtc, AppSettings settings, DateTime? nowUtc = null);
    Freshness GetStoreFreshness(DateTime? checkedAtUtc, AppSettings settings, DateTime? nowUtc = null);
    Freshness GetRouteFreshness(DateTime? checkedAtUtc, AppSettings settings, DateTime? nowUtc = null);
    Freshness GetPromoFreshness(DateTime? checkedAtUtc, AppSettings settings, DateTime? nowUtc = null);
    Freshness GetOfferFreshness(Offer offer, AppSettings settings, DateTime? nowUtc = null);
    bool IsStaleOrOutdated(DateTime? checkedAtUtc, TimeSpan ttl, DateTime? nowUtc = null);
}

public sealed class FreshnessService : IFreshnessService
{
    public Freshness Evaluate(DateTime? checkedAtUtc, TimeSpan ttl, TimeSpan? outdatedThreshold = null, DateTime? nowUtc = null)
    {
        if (!checkedAtUtc.HasValue || checkedAtUtc.Value == default)
            return Freshness.Unknown;

        var now = nowUtc ?? DateTime.UtcNow;
        var checkedUtc = checkedAtUtc.Value.Kind == DateTimeKind.Utc
            ? checkedAtUtc.Value
            : DateTime.SpecifyKind(checkedAtUtc.Value, DateTimeKind.Utc);

        var age = now - checkedUtc;
        if (age < TimeSpan.Zero)
            age = TimeSpan.Zero; // Future clock skew safe guard

        if (age <= ttl)
            return Freshness.Fresh;

        var outdatedSpan = outdatedThreshold ?? TimeSpan.FromTicks(ttl.Ticks * 3);
        if (age <= outdatedSpan)
            return Freshness.Stale;

        return Freshness.Outdated;
    }

    public Freshness GetPriceFreshness(DateTime? checkedAtUtc, AppSettings settings, DateTime? nowUtc = null) =>
        Evaluate(checkedAtUtc, TimeSpan.FromHours(settings.PriceTtlHours > 0 ? settings.PriceTtlHours : 6), null, nowUtc);

    public Freshness GetStockFreshness(DateTime? checkedAtUtc, AppSettings settings, DateTime? nowUtc = null) =>
        Evaluate(checkedAtUtc, TimeSpan.FromHours(settings.StockTtlHours > 0 ? settings.StockTtlHours : 2), null, nowUtc);

    public Freshness GetProductFreshness(DateTime? updatedAtUtc, AppSettings settings, DateTime? nowUtc = null) =>
        Evaluate(updatedAtUtc, TimeSpan.FromDays(settings.ProductTtlDays > 0 ? settings.ProductTtlDays : 7), null, nowUtc);

    public Freshness GetStoreFreshness(DateTime? checkedAtUtc, AppSettings settings, DateTime? nowUtc = null) =>
        Evaluate(checkedAtUtc, TimeSpan.FromDays(settings.StoresTtlDays > 0 ? settings.StoresTtlDays : 7), null, nowUtc);

    public Freshness GetRouteFreshness(DateTime? checkedAtUtc, AppSettings settings, DateTime? nowUtc = null) =>
        Evaluate(checkedAtUtc, TimeSpan.FromHours(settings.RouteTtlHours > 0 ? settings.RouteTtlHours : 24), null, nowUtc);

    public Freshness GetPromoFreshness(DateTime? checkedAtUtc, AppSettings settings, DateTime? nowUtc = null) =>
        Evaluate(checkedAtUtc, TimeSpan.FromHours(settings.PromoTtlHours > 0 ? settings.PromoTtlHours : 4), null, nowUtc);

    public Freshness GetOfferFreshness(Offer offer, AppSettings settings, DateTime? nowUtc = null) =>
        GetPriceFreshness(offer.CheckedAt, settings, nowUtc);

    public bool IsStaleOrOutdated(DateTime? checkedAtUtc, TimeSpan ttl, DateTime? nowUtc = null)
    {
        var freshness = Evaluate(checkedAtUtc, ttl, null, nowUtc);
        return freshness is Freshness.Stale or Freshness.Outdated or Freshness.Unknown;
    }
}
