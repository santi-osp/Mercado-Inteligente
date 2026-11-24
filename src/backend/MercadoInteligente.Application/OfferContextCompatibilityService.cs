using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public sealed class OfferContextCompatibilityService : IOfferContextPolicy
{
    public OfferCompatibilityEvaluation Evaluate(
        PriceScope offerScope,
        string offerContext,
        string providerCode,
        EffectiveProviderContext? providerContext,
        bool allowGenericFallbackIfRegionalMissing = true)
    {
        // 1. Ara / Manual catalog: no regional fulfillment requirements
        if (string.Equals(providerCode, "ARA", StringComparison.OrdinalIgnoreCase))
        {
            return new OfferCompatibilityEvaluation(
                OfferCompatibility.Compatible,
                "Ara opera con precios de catalogo / tienda sin segmentacion por fulfillment regional.",
                IsFallback: false);
        }

        // 2. If providerContext is null -> no localized shopping context was requested/resolved
        if (providerContext is null)
        {
            if (offerScope == PriceScope.OnlineGeneric)
            {
                return new OfferCompatibilityEvaluation(
                    OfferCompatibility.Compatible,
                    "Precio online generico aceptado (sin contexto regional especificado).",
                    IsFallback: false);
            }

            // Offers tied to a specific fulfillment region are incompatible if we don't know the user's location
            if (offerScope is PriceScope.FulfillmentRegion or PriceScope.Region or PriceScope.City)
            {
                return new OfferCompatibilityEvaluation(
                    OfferCompatibility.Incompatible,
                    $"Oferta regional ({offerContext}) rechazada: no hay ubicacion o contexto de compra activo.",
                    IsFallback: false);
            }

            return new OfferCompatibilityEvaluation(
                OfferCompatibility.Compatible,
                "Oferta aceptada en contexto no delimitado.",
                IsFallback: false);
        }

        // 3. Provider context was resolved as Fallback or OnlineGeneric (regional resolution could not be performed or failed)
        if (providerContext.IsFallback || providerContext.ResolutionStatus == ContextResolutionStatus.Fallback ||
            providerContext.PriceScope == PriceScope.OnlineGeneric)
        {
            if (offerScope == PriceScope.OnlineGeneric)
            {
                return new OfferCompatibilityEvaluation(
                    OfferCompatibility.Fallback,
                    "Contexto del proveedor no pudo resolverse regionalmente; utilizando precio online generico.",
                    IsFallback: true);
            }

            // Any regional offer is incompatible because we cannot guarantee the user is in that region
            return new OfferCompatibilityEvaluation(
                OfferCompatibility.Incompatible,
                $"Oferta regional ({offerContext}) rechazada: el contexto del proveedor esta en fallback generico.",
                IsFallback: false);
        }

        // 4. Provider context is resolved for a localized region (e.g. FulfillmentRegion, Region, City)
        var targetContext = providerContext.PriceContext?.Trim() ?? "";

        // Check if offer matches the localized regional context
        var isRegionalScope = offerScope is PriceScope.FulfillmentRegion or PriceScope.Region or PriceScope.City;
        if (isRegionalScope)
        {
            if (IsContextMatch(offerContext, targetContext))
            {
                return new OfferCompatibilityEvaluation(
                    OfferCompatibility.Compatible,
                    $"Oferta coincide con el contexto regional activo ({targetContext}).",
                    IsFallback: false);
            }

            // Distinct region: strictly INCOMPATIBLE
            return new OfferCompatibilityEvaluation(
                OfferCompatibility.Incompatible,
                $"Oferta regional ({offerContext}) rechazada: incompatible con el contexto regional activo ({targetContext}).",
                IsFallback: false);
        }

        // 5. Store / PhysicalVerified scopes
        if (offerScope is PriceScope.Store or PriceScope.PhysicalVerified)
        {
            return new OfferCompatibilityEvaluation(
                OfferCompatibility.Compatible,
                "Oferta de tienda fisica / verificada.",
                IsFallback: false);
        }

        // 6. OnlineGeneric offer when regional context was requested
        if (offerScope == PriceScope.OnlineGeneric)
        {
            if (allowGenericFallbackIfRegionalMissing)
            {
                return new OfferCompatibilityEvaluation(
                    OfferCompatibility.Fallback,
                    "No se encontro precio regional; se utilizo precio online generico como fallback.",
                    IsFallback: true);
            }

            return new OfferCompatibilityEvaluation(
                OfferCompatibility.Incompatible,
                "Precio online generico rechazado porque se requiere precio regional verificado.",
                IsFallback: true);
        }

        return new OfferCompatibilityEvaluation(
            OfferCompatibility.Incompatible,
            $"Alcance de precio no reconocido ({offerScope}).",
            IsFallback: false);
    }

    public OfferCompatibilityEvaluation Evaluate(
        RawCatalogOffer offer,
        EffectiveProviderContext? providerContext,
        bool allowGenericFallbackIfRegionalMissing = true)
    {
        return Evaluate(
            offer.Scope,
            offer.Context,
            offer.RetailerCode,
            providerContext,
            allowGenericFallbackIfRegionalMissing);
    }

    private static bool IsContextMatch(string offerContext, string targetContext)
    {
        if (string.IsNullOrWhiteSpace(offerContext) || string.IsNullOrWhiteSpace(targetContext))
            return false;

        var cleanOffer = offerContext.Trim();
        var cleanTarget = targetContext.Trim();

        if (string.Equals(cleanOffer, cleanTarget, StringComparison.OrdinalIgnoreCase))
            return true;

        var offerKey = cleanOffer.StartsWith("fulfillment:", StringComparison.OrdinalIgnoreCase)
            ? cleanOffer["fulfillment:".Length..]
            : cleanOffer.StartsWith("region:", StringComparison.OrdinalIgnoreCase)
                ? cleanOffer["region:".Length..]
                : cleanOffer;

        var targetKey = cleanTarget.StartsWith("fulfillment:", StringComparison.OrdinalIgnoreCase)
            ? cleanTarget["fulfillment:".Length..]
            : cleanTarget.StartsWith("region:", StringComparison.OrdinalIgnoreCase)
                ? cleanTarget["region:".Length..]
                : cleanTarget;

        return string.Equals(offerKey, targetKey, StringComparison.OrdinalIgnoreCase);
    }
}
