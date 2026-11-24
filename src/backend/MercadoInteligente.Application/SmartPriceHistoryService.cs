using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public interface ISmartPriceHistoryService
{
    bool HasSignificantChange(Offer currentOffer, decimal newPrice, decimal? newListPrice, AvailabilityStatus newAvailability, string? newPromo);
    PriceObservation? RecordObservationIfChanged(Offer currentOffer, decimal newPrice, decimal? newListPrice, AvailabilityStatus newAvailability, string? newPromo, Guid? rawCaptureId, DateTime nowUtc);
}

public sealed class SmartPriceHistoryService : ISmartPriceHistoryService
{
    public bool HasSignificantChange(Offer currentOffer, decimal newPrice, decimal? newListPrice, AvailabilityStatus newAvailability, string? newPromo)
    {
        if (currentOffer.Price != newPrice) return true;
        if (currentOffer.ListPrice != newListPrice) return true;
        if (currentOffer.Availability != newAvailability) return true;
        if (!string.Equals(currentOffer.PromotionConditions, newPromo, StringComparison.Ordinal)) return true;
        return false;
    }

    public PriceObservation? RecordObservationIfChanged(
        Offer currentOffer,
        decimal newPrice,
        decimal? newListPrice,
        AvailabilityStatus newAvailability,
        string? newPromo,
        Guid? rawCaptureId,
        DateTime nowUtc)
    {
        var changed = HasSignificantChange(currentOffer, newPrice, newListPrice, newAvailability, newPromo);

        currentOffer.Price = newPrice;
        currentOffer.ListPrice = newListPrice;
        currentOffer.Availability = newAvailability;
        currentOffer.PromotionConditions = newPromo;
        currentOffer.CheckedAt = nowUtc;
        if (rawCaptureId.HasValue)
            currentOffer.RawCaptureId = rawCaptureId.Value;

        if (changed)
        {
            return PriceObservationFactory.FromOfferSnapshot(currentOffer, nowUtc);
        }

        // Price and attributes are unchanged: CheckedAt was updated, but no duplicate row is added to history.
        return null;
    }
}
