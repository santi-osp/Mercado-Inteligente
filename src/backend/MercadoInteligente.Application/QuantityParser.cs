using System.Globalization;
using System.Text.RegularExpressions;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public record StandardQuantity(
    decimal TotalAmount,
    Dimension Dimension,
    int PackageCount = 1,
    decimal? AmountPerUnit = null,
    string OriginalUnit = ""
);

public static partial class QuantityParser
{
    public static StandardQuantity? Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var normalized = Units.Normalize(input).Replace(',', '.');

        // Pattern 1: Multipack with dimension, e.g. "6 x 1 L", "12 x 400 g", "4 x 250 ml", "12 rollos x 40 m"
        var multiMatch = MultiPackPattern().Match(normalized);
        if (multiMatch.Success)
        {
            var packCount = int.Parse(multiMatch.Groups["pack"].Value, CultureInfo.InvariantCulture);
            var itemAmount = decimal.Parse(multiMatch.Groups["amount"].Value, CultureInfo.InvariantCulture);
            var unit = multiMatch.Groups["unit"].Value;
            var (dim, factor) = ResolveUnit(unit);
            if (dim != Dimension.Unknown && packCount > 0 && itemAmount > 0)
            {
                var basePerUnit = itemAmount * factor;
                var total = packCount * basePerUnit;
                return new StandardQuantity(total, dim, packCount, basePerUnit, unit);
            }
        }

        // Pattern 2: Single quantity with unit, e.g. "1000 g", "1.5 kg", "900 ml", "2.5 L", "500 gr", "12 huevos"
        var singleMatch = SingleQuantityPattern().Match(normalized);
        if (singleMatch.Success)
        {
            var amount = decimal.Parse(singleMatch.Groups["amount"].Value, CultureInfo.InvariantCulture);
            var unit = singleMatch.Groups["unit"].Value;
            var (dim, factor) = ResolveUnit(unit);
            if (dim != Dimension.Unknown && amount > 0)
            {
                var total = amount * factor;
                return dim == Dimension.Count
                    ? new StandardQuantity(total, dim, (int)total, 1m, unit)
                    : new StandardQuantity(total, dim, 1, total, unit);
            }
        }

        // Pattern 3: Pack / count only, e.g. "pack x 6", "x 12", "pack 4", "12 unidades", "12 huevos", "pague 2 lleve 3"
        var packOnlyMatch = PackOnlyPattern().Match(normalized);
        if (packOnlyMatch.Success)
        {
            var countStr = packOnlyMatch.Groups["count"].Value;
            if (int.TryParse(countStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) && count > 0)
            {
                return new StandardQuantity(count, Dimension.Count, count, 1m, "und");
            }
        }

        var countMatch = CountOnlyPattern().Match(normalized);
        if (countMatch.Success && int.TryParse(countMatch.Groups["count"].Value, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var units) && units > 0)
            return new StandardQuantity(units, Dimension.Count, units, 1m, "und");

        // Pattern 4: Named packs (sixpack, tripack, duopack)
        if (normalized.Contains("sixpack") || normalized.Contains("six pack"))
            return new StandardQuantity(6, Dimension.Count, 6, 1m, "und");
        if (normalized.Contains("tripack") || normalized.Contains("tri pack") || normalized.Contains("tripak"))
            return new StandardQuantity(3, Dimension.Count, 3, 1m, "und");
        if (normalized.Contains("duopack") || normalized.Contains("duo pack") || normalized.Contains("bipack"))
            return new StandardQuantity(2, Dimension.Count, 2, 1m, "und");

        return null;
    }

    public static (Dimension Dimension, decimal FactorToBase) ResolveUnit(string unit)
    {
        return unit.ToLowerInvariant() switch
        {
            // Mass -> base unit is kg
            "kg" or "kilo" or "kilos" or "kilogramo" or "kilogramos" => (Dimension.Mass, 1.0m),
            "g" or "gr" or "grs" or "gramo" or "gramos" => (Dimension.Mass, 0.001m),
            "lb" or "lbs" or "libra" or "libras" => (Dimension.Mass, 0.5m), // Standard Colombian grocery retail pound = 500g
            "oz" or "onza" or "onzas" => (Dimension.Mass, 0.0283495m),

            // Volume -> base unit is L
            "l" or "lt" or "lts" or "ltr" or "litro" or "litros" => (Dimension.Volume, 1.0m),
            "ml" or "mls" or "mililitro" or "mililitros" or "cc" or "cm3" => (Dimension.Volume, 0.001m),

            // Count -> base unit is count
            "und" or "unds" or "unidad" or "unidades" or "u" or "huevo" or "huevos" => (Dimension.Count, 1.0m),
            "sobre" or "sobres" or "sachet" or "sachets" => (Dimension.Count, 1.0m),
            "rollo" or "rollos" => (Dimension.Count, 1.0m),
            "tableta" or "tabletas" or "pastilla" or "pastillas" or "capsula" or "capsulas" => (Dimension.Count, 1.0m),
            "bolsa" or "bolsas" => (Dimension.Count, 1.0m),
            "paquete" or "paquetes" => (Dimension.Count, 1.0m),

            // Length -> base unit is m
            "m" or "mt" or "mts" or "metro" or "metros" => (Dimension.Length, 1.0m),
            "cm" or "centimetro" or "centimetros" => (Dimension.Length, 0.01m),

            _ => (Dimension.Unknown, 1.0m)
        };
    }

    public static bool AreDimensionsCompatible(Dimension a, Dimension b) =>
        a != Dimension.Unknown && b != Dimension.Unknown && a == b;

    public static decimal CalculateSizeDifferenceRatio(StandardQuantity a, StandardQuantity b)
    {
        if (a.Dimension != b.Dimension || a.TotalAmount <= 0 || b.TotalAmount <= 0)
            return decimal.MaxValue;

        var diff = Math.Abs(a.TotalAmount - b.TotalAmount);
        var max = Math.Max(a.TotalAmount, b.TotalAmount);
        return diff / max;
    }

    [GeneratedRegex(@"(?:(?:pack\s*(?:de|x)?\s*)?|(?:\b))(?<pack>\d+)\s*(?:rollos?|unds?|unidades?|sobres?|bolsas?|piezas?|huevos?)?\s*[x\u00D7*]\s*(?<amount>\d+(?:\.\d+)?)\s*(?<unit>kg|kilos?|kilogramos?|g|grs?|gramos?|lbs?|libras?|oz|onzas?|l|lts?|ltr|litros?|ml|mls?|mililitros?|cc|cm3|m|mts?|metros?|cm|unds?|unidades?|huevos?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MultiPackPattern();

    [GeneratedRegex(@"(?<![x\u00D7*]\s*)\b(?<amount>\d+(?:\.\d+)?)\s*(?<unit>kg|kilos?|kilogramos?|g|grs?|gramos?|lbs?|libras?|oz|onzas?|l|lts?|ltr|litros?|ml|mls?|mililitros?|cc|cm3|m|mts?|metros?|cm|unds?|unidades?|huevos?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SingleQuantityPattern();

    [GeneratedRegex(@"(?:\bpack\s*(?:de|x)?\s*|\bx\s*)(?<count>\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PackOnlyPattern();

    [GeneratedRegex(@"\b(?<count>\d+)\s*(?:unidades?|unds?|piezas?|huevos?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CountOnlyPattern();
}