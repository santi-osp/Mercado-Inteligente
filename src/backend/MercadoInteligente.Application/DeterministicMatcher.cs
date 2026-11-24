using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public record MatchEvaluationResult(
    MatchKind Kind,
    decimal ConfidenceScore,
    string Evidence,
    bool HasConflict = false,
    string? ConflictReason = null
);

public static class DeterministicMatcher
{
    public static MatchEvaluationResult Evaluate(
        NormalizedProductInfo a,
        NormalizedProductInfo b,
        MatchOverride? matchOverride = null,
        decimal sizeToleranceRatio = 0.15m,
        bool rankCandidates = true)
    {
        // 1. User MatchOverride has absolute priority
        if (matchOverride != null)
        {
            return new MatchEvaluationResult(
                matchOverride.Kind,
                1.0m,
                $"User override: {matchOverride.Kind}"
            );
        }

        // 2. EAN/GTIN equality check with Conflict Detection
        if (!string.IsNullOrWhiteSpace(a.Ean) && !string.IsNullOrWhiteSpace(b.Ean) &&
            string.Equals(a.Ean, b.Ean, StringComparison.OrdinalIgnoreCase))
        {
            // Conflict check: incompatible variants
            if (HasVariantConflict(a, b, out var variantConflict))
            {
                return new MatchEvaluationResult(
                    MatchKind.Candidate,
                    0.5m,
                    $"EAN match with critical variant conflict: {variantConflict}",
                    HasConflict: true,
                    ConflictReason: variantConflict
                );
            }

            // Conflict check: incompatible dimensions or massive quantity discrepancy
            if (a.Quantity != null && b.Quantity != null)
            {
                if (a.Quantity.Dimension != b.Quantity.Dimension)
                {
                    return new MatchEvaluationResult(
                        MatchKind.Candidate,
                        0.4m,
                        $"EAN match with dimension conflict ({a.Quantity.Dimension} vs {b.Quantity.Dimension})",
                        HasConflict: true,
                        ConflictReason: "Dimension conflict"
                    );
                }

                var diffRatio = QuantityParser.CalculateSizeDifferenceRatio(a.Quantity, b.Quantity);
                if (diffRatio > 0.25m)
                {
                    return new MatchEvaluationResult(
                        MatchKind.Candidate,
                        0.5m,
                        $"EAN match with size discrepancy ({a.Quantity.TotalAmount} vs {b.Quantity.TotalAmount})",
                        HasConflict: true,
                        ConflictReason: "Size discrepancy > 25%"
                    );
                }
            }

            // Conflict check: clearly distinct brands (neither is generic)
            if (!IsBrandCompatible(a.NormalizedBrand, b.NormalizedBrand))
            {
                return new MatchEvaluationResult(
                    MatchKind.Candidate,
                    0.5m,
                    $"EAN match with brand conflict ({a.NormalizedBrand} vs {b.NormalizedBrand})",
                    HasConflict: true,
                    ConflictReason: "Brand conflict"
                );
            }

            return new MatchEvaluationResult(
                MatchKind.Exact,
                0.99m,
                "EAN identical without conflicts"
            );
        }

        // Check if dimension is fundamentally incompatible
        if (a.Quantity != null && b.Quantity != null && a.Quantity.Dimension != b.Quantity.Dimension)
        {
            return new MatchEvaluationResult(
                MatchKind.NotEquivalent,
                0.0m,
                $"Incompatible dimensions: {a.Quantity.Dimension} vs {b.Quantity.Dimension}"
            );
        }

        // 3. Rule Exact: Brand + ProductType + Variant + Identical Quantity
        var sameBrand = string.Equals(a.NormalizedBrand, b.NormalizedBrand, StringComparison.OrdinalIgnoreCase);
        var sameCategory = string.Equals(a.ProductType, b.ProductType, StringComparison.OrdinalIgnoreCase);
        if (sameCategory && a.ProductType.Equals("Abarrotes", StringComparison.OrdinalIgnoreCase))
        {
            // For unclassified items in the generic "Abarrotes" fallback bucket, require high name similarity
            sameCategory = string.Equals(a.CleanName, b.CleanName, StringComparison.OrdinalIgnoreCase) ||
                           CalculateSimilarity(a.CleanName, b.CleanName) >= 0.7m;
        }
        var variantConflictFound = HasVariantConflict(a, b, out var varConflict);

        if (sameBrand && sameCategory && !variantConflictFound && a.Quantity != null && b.Quantity != null)
        {
            var sizeDiff = QuantityParser.CalculateSizeDifferenceRatio(a.Quantity, b.Quantity);
            if (sizeDiff < 0.02m) // Within 2% (effectively identical)
            {
                return new MatchEvaluationResult(
                    MatchKind.Exact,
                    0.95m,
                    "Rule exact: Brand, category, variant, and quantity identical"
                );
            }
        }

        // If product type is completely different
        if (!sameCategory)
        {
            return new MatchEvaluationResult(
                MatchKind.NotEquivalent,
                0.0m,
                $"Different product categories: {a.ProductType} vs {b.ProductType}"
            );
        }

        // Check for critical variant conflicts that prevent even substitution (e.g. Zero vs Regular, Entera vs Descremada, En Agua vs En Aceite)
        if (HasCriticalVariantConflict(a, b, out var critConflict))
        {
            return new MatchEvaluationResult(
                MatchKind.NotEquivalent,
                0.1m,
                $"Critical variant conflict: {critConflict}"
            );
        }

        // 4. Substitute: Same category, compatible variant, within size tolerance or larger pack
        if (sameCategory && a.Quantity != null && b.Quantity != null)
        {
            var sizeDiff = QuantityParser.CalculateSizeDifferenceRatio(a.Quantity, b.Quantity);
            if (sizeDiff <= sizeToleranceRatio)
            {
                // Egg grade check: AA vs AAA can be substitutes, but NOT exact
                var isEggGradeDiff = a.EggGrade != null && b.EggGrade != null &&
                                     !string.Equals(a.EggGrade, b.EggGrade, StringComparison.OrdinalIgnoreCase);

                var reason = sameBrand
                    ? $"Substitute: Same brand, size difference {sizeDiff * 100:0.#}% within tolerance"
                    : isEggGradeDiff
                        ? $"Substitute: Egg grade difference ({a.EggGrade} vs {b.EggGrade}) within tolerance"
                        : $"Substitute: Alternative brand ({a.NormalizedBrand} vs {b.NormalizedBrand}) within size tolerance {sizeDiff * 100:0.#}%";

                return new MatchEvaluationResult(
                    MatchKind.Substitute,
                    0.80m,
                    reason
                );
            }
            else if (b.Quantity.TotalAmount >= a.Quantity.TotalAmount && !variantConflictFound)
            {
                // Larger presentation of same category: eligible as substitute subject to overbuy rules in optimizer
                return new MatchEvaluationResult(
                    MatchKind.Substitute,
                    0.70m,
                    $"Substitute: Larger presentation of {a.ProductType} ({b.Quantity.TotalAmount} vs {a.Quantity.TotalAmount})"
                );
            }
        }

        // 4b. Same category with unstated quantity (user requested category in general, e.g. "Café Molido")
        if (sameCategory && !variantConflictFound && (a.Quantity == null || b.Quantity == null))
        {
            var isSubstitute = !sameBrand && !a.NormalizedBrand.Equals("Genérico", StringComparison.OrdinalIgnoreCase);
            var matchKind = isSubstitute ? MatchKind.Substitute : MatchKind.Exact;
            return new MatchEvaluationResult(
                matchKind,
                isSubstitute ? 0.80m : 0.90m,
                isSubstitute
                    ? $"Substitute: Alternative brand ({a.NormalizedBrand} vs {b.NormalizedBrand}) for {a.ProductType}"
                    : $"Match: Compatible {a.ProductType} presentation"
            );
        }

        if (!rankCandidates)
        {
            return new MatchEvaluationResult(
                MatchKind.NotEquivalent,
                0.0m,
                "No exact or substitute match found"
            );
        }

        // 5. Candidate Ranking using text similarity (strictly Candidate, NEVER Exact)
        var similarity = CalculateSimilarity(a.CleanName, b.CleanName);
        if (similarity >= 0.4m)
        {
            return new MatchEvaluationResult(
                MatchKind.Candidate,
                similarity * 0.7m, // Capped below substitute confidence
                $"Fuzzy similarity {similarity:P0} (ranking candidate only)"
            );
        }

        return new MatchEvaluationResult(
            MatchKind.NotEquivalent,
            0.0m,
            "No sufficient similarity or compatibility found"
        );
    }

    public static bool HasVariantConflict(NormalizedProductInfo a, NormalizedProductInfo b, out string conflict)
    {
        // Sugar / Zero vs Regular conflict
        if (a.IsSugarFree != b.IsSugarFree)
        {
            conflict = $"Sugar conflict: {(a.IsSugarFree ? "Zero/Sin Azúcar" : "Regular")} vs {(b.IsSugarFree ? "Zero/Sin Azúcar" : "Regular")}";
            return true;
        }

        // Lactose free vs regular milk conflict
        if (a.IsLactoseFree != b.IsLactoseFree)
        {
            conflict = $"Lactose conflict: {(a.IsLactoseFree ? "Deslactosada" : "Regular")} vs {(b.IsLactoseFree ? "Deslactosada" : "Regular")}";
            return true;
        }

        // Milk type conflict (Entera vs Descremada / Semidescremada)
        if (a.ProductType == "Leche" && b.ProductType == "Leche")
        {
            var aEntera = a.VariantType.Contains("Entera", StringComparison.OrdinalIgnoreCase);
            var bEntera = b.VariantType.Contains("Entera", StringComparison.OrdinalIgnoreCase);
            var aDescremada = a.VariantType.Contains("Descremada", StringComparison.OrdinalIgnoreCase);
            var bDescremada = b.VariantType.Contains("Descremada", StringComparison.OrdinalIgnoreCase);

            if ((aEntera && bDescremada) || (bEntera && aDescremada))
            {
                conflict = $"Milk type conflict: {a.VariantType} vs {b.VariantType}";
                return true;
            }
        }

        // Egg grade conflict
        if (a.EggGrade != null && b.EggGrade != null &&
            !string.Equals(a.EggGrade, b.EggGrade, StringComparison.OrdinalIgnoreCase))
        {
            conflict = $"Egg grade conflict: {a.EggGrade} vs {b.EggGrade}";
            return true;
        }

        // Oil type conflict (Oliva vs Girasol vs Vegetal)
        if (a.OilType != null && b.OilType != null &&
            !string.Equals(a.OilType, b.OilType, StringComparison.OrdinalIgnoreCase))
        {
            conflict = $"Oil type conflict: {a.OilType} vs {b.OilType}";
            return true;
        }

        // Tuna liquid conflict (Agua vs Aceite)
        if (a.TunaLiquid != null && b.TunaLiquid != null &&
            !string.Equals(a.TunaLiquid, b.TunaLiquid, StringComparison.OrdinalIgnoreCase))
        {
            conflict = $"Tuna liquid conflict: {a.TunaLiquid} vs {b.TunaLiquid}";
            return true;
        }

        conflict = "";
        return false;
    }

    public static bool HasCriticalVariantConflict(NormalizedProductInfo a, NormalizedProductInfo b, out string conflict)
    {
        // Sugar / Zero vs Regular conflict
        if (a.IsSugarFree != b.IsSugarFree)
        {
            conflict = $"Sugar conflict: {(a.IsSugarFree ? "Zero/Sin Azúcar" : "Regular")} vs {(b.IsSugarFree ? "Zero/Sin Azúcar" : "Regular")}";
            return true;
        }

        // Lactose free vs regular milk conflict
        if (a.IsLactoseFree != b.IsLactoseFree)
        {
            conflict = $"Lactose conflict: {(a.IsLactoseFree ? "Deslactosada" : "Regular")} vs {(b.IsLactoseFree ? "Deslactosada" : "Regular")}";
            return true;
        }

        // Milk type conflict (Entera vs Descremada / Semidescremada)
        if (a.ProductType == "Leche" && b.ProductType == "Leche")
        {
            var aEntera = a.VariantType.Contains("Entera", StringComparison.OrdinalIgnoreCase);
            var bEntera = b.VariantType.Contains("Entera", StringComparison.OrdinalIgnoreCase);
            var aDescremada = a.VariantType.Contains("Descremada", StringComparison.OrdinalIgnoreCase);
            var bDescremada = b.VariantType.Contains("Descremada", StringComparison.OrdinalIgnoreCase);

            if ((aEntera && bDescremada) || (bEntera && aDescremada))
            {
                conflict = $"Milk type conflict: {a.VariantType} vs {b.VariantType}";
                return true;
            }
        }

        // Oil type conflict (Oliva vs Girasol vs Vegetal)
        if (a.OilType != null && b.OilType != null &&
            !string.Equals(a.OilType, b.OilType, StringComparison.OrdinalIgnoreCase))
        {
            conflict = $"Oil type conflict: {a.OilType} vs {b.OilType}";
            return true;
        }

        // Tuna liquid conflict (Agua vs Aceite)
        if (a.TunaLiquid != null && b.TunaLiquid != null &&
            !string.Equals(a.TunaLiquid, b.TunaLiquid, StringComparison.OrdinalIgnoreCase))
        {
            conflict = $"Tuna liquid conflict: {a.TunaLiquid} vs {b.TunaLiquid}";
            return true;
        }

        conflict = "";
        return false;
    }

    private static bool IsBrandCompatible(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;
        if (a.Equals("Genérico", StringComparison.OrdinalIgnoreCase) || b.Equals("Genérico", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public static decimal CalculateSimilarity(string s1, string s2)
    {
        if (string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2)) return 0m;
        if (string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase)) return 1.0m;

        var t1 = s1.ToLowerInvariant().Split([' ', '-', ',', '.'], StringSplitOptions.RemoveEmptyEntries);
        var t2 = s2.ToLowerInvariant().Split([' ', '-', ',', '.'], StringSplitOptions.RemoveEmptyEntries);

        if (t1.Length == 0 || t2.Length == 0) return 0m;

        int matches = 0;
        for (int i = 0; i < t1.Length; i++)
        {
            for (int j = 0; j < t2.Length; j++)
            {
                if (t1[i] == t2[j])
                {
                    matches++;
                    break;
                }
            }
        }

        int union = t1.Length + t2.Length - matches;
        return union == 0 ? 0m : (decimal)matches / union;
    }
}
