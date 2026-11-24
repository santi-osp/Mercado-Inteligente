namespace MercadoInteligente.Domain;

public static class PriceObservationFactory
{
    public static PriceObservation FromOfferSnapshot(Offer offer, DateTime? observedAt = null)
    {
        var timestamp = observedAt ?? offer.CheckedAt;
        return new PriceObservation
        {
            Id = Guid.NewGuid(),
            OfferId = offer.Id,
            Price = offer.Price,
            ListPrice = offer.ListPrice,
            Availability = offer.Availability,
            AvailableQuantity = offer.AvailableQuantityRaw,
            PromotionConditions = offer.PromotionConditions,
            PriceScope = offer.Scope,
            PriceContext = offer.Context,
            SellerId = offer.SellerId,
            StoreId = offer.StoreId,
            RawCaptureId = offer.RawCaptureId,
            CheckedAt = timestamp
        };
    }
}
