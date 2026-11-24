namespace MercadoInteligente.Application;

public enum PriceAnomalyStatus
{
    Valid,
    Suspicious,
    Invalid
}

public sealed record PriceValidationResult(
    PriceAnomalyStatus Status,
    string? Reason,
    decimal? PreviousPrice = null,
    decimal? Ratio = null
);

public interface IPriceAnomalyDetector
{
    PriceValidationResult ValidateOfferPrice(decimal price, decimal? listPrice = null, decimal? previousPrice = null);
}

public sealed class PriceAnomalyDetector : IPriceAnomalyDetector
{
    // Thresholds: Drop of > 80% (ratio < 0.20) or surge of > 500% (ratio > 5.0)
    public const decimal ExtremeDropRatio = 0.20m;
    public const decimal ExtremeSurgeRatio = 5.0m;

    public PriceValidationResult ValidateOfferPrice(decimal price, decimal? listPrice = null, decimal? previousPrice = null)
    {
        if (price <= 0)
            return new PriceValidationResult(PriceAnomalyStatus.Invalid, "El precio debe ser estrictamente positivo.");

        if (listPrice.HasValue && listPrice.Value <= 0)
            return new PriceValidationResult(PriceAnomalyStatus.Invalid, "El precio de lista debe ser mayor a 0 si se especifica.");

        if (listPrice.HasValue && price > listPrice.Value * 1.5m)
        {
            var markupRatio = price / listPrice.Value;
            return new PriceValidationResult(
                PriceAnomalyStatus.Suspicious,
                $"El precio de oferta supera el precio de lista en {(markupRatio - 1) * 100:F0}%. Requiere verificación.",
                listPrice.Value,
                markupRatio
            );
        }

        if (previousPrice.HasValue && previousPrice.Value > 0)
        {
            var ratio = price / previousPrice.Value;
            if (ratio < ExtremeDropRatio)
            {
                var dropPercent = (1m - ratio) * 100m;
                return new PriceValidationResult(
                    PriceAnomalyStatus.Suspicious,
                    $"Caída extrema de precio de {dropPercent:F0}% respecto a la última observación conocida (${previousPrice.Value:N0}).",
                    previousPrice.Value,
                    ratio
                );
            }

            if (ratio > ExtremeSurgeRatio)
            {
                return new PriceValidationResult(
                    PriceAnomalyStatus.Suspicious,
                    $"Aumento extremo de precio de {ratio:F1}x respecto a la última observación conocida (${previousPrice.Value:N0}).",
                    previousPrice.Value,
                    ratio
                );
            }
        }

        return new PriceValidationResult(PriceAnomalyStatus.Valid, null, previousPrice);
    }
}
