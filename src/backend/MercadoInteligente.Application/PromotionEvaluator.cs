using System.Text.RegularExpressions;

namespace MercadoInteligente.Application;

public sealed record PromotionEvaluationResult(
    decimal TotalCost,
    decimal StandardCost,
    decimal Savings,
    string? AppliedPromotion,
    string? ConditionWarning
);

public static partial class PromotionEvaluator
{
    [GeneratedRegex(@"(?:lleva|compra)?\s*2\s*(?:y\s*)?(?:paga|lleve)\s*1|2\s*x\s*1", RegexOptions.IgnoreCase)]
    private static partial Regex TwoForOneRegex();

    [GeneratedRegex(@"(?:lleva|compra)?\s*3\s*(?:y\s*)?(?:paga|lleve)\s*2|3\s*x\s*2", RegexOptions.IgnoreCase)]
    private static partial Regex ThreeForTwoRegex();

    [GeneratedRegex(@"(?:segunda|2da|2da\s*unidad)\s*(?:con|al)?\s*(\d{1,2})%\s*(?:dto|descuento)?", RegexOptions.IgnoreCase)]
    private static partial Regex SecondUnitPercentRegex();

    [GeneratedRegex(@"(\d+)\s*(?:unidades|und|x)?\s*(?:por|\$)\s*\$?([0-9.,]+)", RegexOptions.IgnoreCase)]
    private static partial Regex NForPriceRegex();

    [GeneratedRegex(@"(?:tarjeta\s+(?:exito|cencosud|olimpica|bancolombia)|puntos\s+colombia|club\s+privilegio|exclusivo\s+app)", RegexOptions.IgnoreCase)]
    private static partial Regex ConditionalDiscountRegex();

    public static PromotionEvaluationResult Evaluate(decimal unitPrice, int packsNeeded, string? conditions = null)
    {
        if (packsNeeded <= 0)
            return new PromotionEvaluationResult(0, 0, 0, null, null);

        var standardCost = unitPrice * packsNeeded;
        if (string.IsNullOrWhiteSpace(conditions))
            return new PromotionEvaluationResult(standardCost, standardCost, 0, null, null);

        var cond = conditions.Trim();

        // 1. Conditional card/loyalty program discounts:
        // If discount requires a specific payment method or loyalty card, do NOT automatically assume it.
        // Retain condition text and return standard cost with an explicit warning.
        if (ConditionalDiscountRegex().IsMatch(cond))
        {
            return new PromotionEvaluationResult(
                TotalCost: standardCost,
                StandardCost: standardCost,
                Savings: 0,
                AppliedPromotion: null,
                ConditionWarning: $"Precio condicionado detectado ('{cond}'). Se calcula precio estándar por seguridad."
            );
        }

        // 2. Buy 2 Pay 1 (2x1)
        if (TwoForOneRegex().IsMatch(cond))
        {
            var pairs = packsNeeded / 2;
            var remainder = packsNeeded % 2;
            var paidPacks = pairs + remainder;
            var totalCost = paidPacks * unitPrice;
            var savings = standardCost - totalCost;

            return new PromotionEvaluationResult(
                TotalCost: totalCost,
                StandardCost: standardCost,
                Savings: savings,
                AppliedPromotion: $"2x1 aplicado ({pairs} gratis)",
                ConditionWarning: null
            );
        }

        // 3. Buy 3 Pay 2 (3x2)
        if (ThreeForTwoRegex().IsMatch(cond))
        {
            var triplets = packsNeeded / 3;
            var remainder = packsNeeded % 3;
            var paidPacks = (triplets * 2) + remainder;
            var totalCost = paidPacks * unitPrice;
            var savings = standardCost - totalCost;

            return new PromotionEvaluationResult(
                TotalCost: totalCost,
                StandardCost: standardCost,
                Savings: savings,
                AppliedPromotion: $"3x2 aplicado ({triplets} gratis)",
                ConditionWarning: null
            );
        }

        // 4. Second unit with % discount (e.g. 2da al 50% de descuento)
        var secondMatch = SecondUnitPercentRegex().Match(cond);
        if (secondMatch.Success && decimal.TryParse(secondMatch.Groups[1].Value, out var discountPct) && discountPct is > 0 and < 100)
        {
            var pairs = packsNeeded / 2;
            var remainder = packsNeeded % 2;
            var secondUnitPrice = unitPrice * (1.0m - (discountPct / 100.0m));
            var totalCost = (pairs * (unitPrice + secondUnitPrice)) + (remainder * unitPrice);
            var savings = standardCost - totalCost;

            return new PromotionEvaluationResult(
                TotalCost: totalCost,
                StandardCost: standardCost,
                Savings: savings,
                AppliedPromotion: $"Segunda unidad al {discountPct}% dto ({pairs} unidades con descuento)",
                ConditionWarning: null
            );
        }

        // 5. N units for $X (e.g. 3 por $12000)
        var nForPriceMatch = NForPriceRegex().Match(cond);
        if (nForPriceMatch.Success &&
            int.TryParse(nForPriceMatch.Groups[1].Value, out var bundleQty) && bundleQty > 1 &&
            decimal.TryParse(nForPriceMatch.Groups[2].Value.Replace(".", "").Replace(",", "."), System.Globalization.CultureInfo.InvariantCulture, out var bundlePrice) && bundlePrice > 0)
        {
            var bundles = packsNeeded / bundleQty;
            var remainder = packsNeeded % bundleQty;
            var totalCost = (bundles * bundlePrice) + (remainder * unitPrice);
            var savings = standardCost - totalCost;

            if (savings > 0)
            {
                return new PromotionEvaluationResult(
                    TotalCost: totalCost,
                    StandardCost: standardCost,
                    Savings: savings,
                    AppliedPromotion: $"{bundleQty} por ${bundlePrice:N0} ({bundles} paquetes)",
                    ConditionWarning: null
                );
            }
        }

        // Default: No applicable mathematical promo pattern recognized
        return new PromotionEvaluationResult(standardCost, standardCost, 0, null, $"Condición informativa: '{cond}'");
    }
}
