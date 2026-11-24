using System.Globalization;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public static class QuantityDisplayFormatter
{
    private static readonly CultureInfo CoCulture = new("es-CO");

    public static string Format(StandardQuantity? quantity)
    {
        if (quantity == null || quantity.TotalAmount <= 0) return "";

        if (quantity.PackageCount > 1)
        {
            var singleAmount = quantity.AmountPerUnit ?? (quantity.TotalAmount / quantity.PackageCount);
            var singleFormatted = FormatSingle(singleAmount, quantity.Dimension, quantity.OriginalUnit);
            return $"{quantity.PackageCount} × {singleFormatted}";
        }

        return FormatSingle(quantity.TotalAmount, quantity.Dimension, quantity.OriginalUnit);
    }

    public static string Format(
        decimal? totalQuantity,
        string? unit,
        int? packageCount = null,
        decimal? quantityPerUnit = null,
        Dimension dimension = Dimension.Unknown,
        string? productName = null)
    {
        // If we can parse a structured StandardQuantity from productName or unit, prefer it if totalQuantity is missing
        if ((!totalQuantity.HasValue || totalQuantity.Value <= 0) && !string.IsNullOrWhiteSpace(productName))
        {
            var parsedFromProduct = QuantityParser.Parse(productName);
            if (parsedFromProduct != null)
                return Format(parsedFromProduct);
        }

        if ((!totalQuantity.HasValue || totalQuantity.Value <= 0) && !string.IsNullOrWhiteSpace(unit))
        {
            var parsedFromUnit = QuantityParser.Parse(unit);
            if (parsedFromUnit != null)
                return Format(parsedFromUnit);
        }

        if (!totalQuantity.HasValue || totalQuantity.Value <= 0)
        {
            return !string.IsNullOrWhiteSpace(unit) ? unit.Trim() : "";
        }

        // Detect dimension if unknown
        if (dimension == Dimension.Unknown && !string.IsNullOrWhiteSpace(unit))
        {
            var (resolvedDim, _) = QuantityParser.ResolveUnit(unit);
            dimension = resolvedDim;
        }

        var count = packageCount.GetValueOrDefault(1);
        if (count > 1)
        {
            decimal singleAmount;
            if (quantityPerUnit.HasValue && quantityPerUnit.Value > 0)
            {
                singleAmount = quantityPerUnit.Value;
            }
            else
            {
                singleAmount = totalQuantity.Value / count;
            }

            var singleFormatted = FormatSingle(singleAmount, dimension, unit);
            return $"{count} × {singleFormatted}";
        }

        return FormatSingle(totalQuantity.Value, dimension, unit);
    }

    public static string FormatSingle(decimal amount, Dimension dimension, string? unitHint = null)
    {
        if (amount <= 0) return "";

        var normUnit = (unitHint ?? "").Trim().ToLowerInvariant();

        // Handle mass (base is kg)
        if (dimension == Dimension.Mass || normUnit is "kg" or "kilo" or "kilos" or "kilogramo" or "g" or "gr" or "grs" or "gramos" or "lb" or "libras")
        {
            // Bug fix: if amount < 1 (e.g. 0.5) and unit is "g", the amount was stored as kg (0.5 kg).
            // A grocery item of 0.5 g is impossible (e.g. 500g of sugar/meat stored as 0.5).
            // Any mass in kg < 1 is converted to grams.
            if (amount < 1.0m)
            {
                var grams = amount * 1000m;
                // If it was somehow already in grams (< 1 gram), grams would be < 1, but in grocery that does not happen
                return $"{FormatNumber(grams)} g";
            }

            // Exactly or >= 1 kg
            return $"{FormatNumber(amount)} kg";
        }

        // Handle volume (base is L)
        if (dimension == Dimension.Volume || normUnit is "l" or "lt" or "lts" or "ltr" or "litro" or "litros" or "ml" or "mls" or "mililitros" or "cc" or "cm3")
        {
            if (amount < 1.0m)
            {
                var ml = amount * 1000m;
                return $"{FormatNumber(ml)} ml";
            }

            return $"{FormatNumber(amount)} L";
        }

        // Handle length (base is m)
        if (dimension == Dimension.Length || normUnit is "m" or "mt" or "mts" or "metro" or "cm")
        {
            if (amount < 1.0m)
            {
                var cm = amount * 100m;
                return $"{FormatNumber(cm)} cm";
            }

            return $"{FormatNumber(amount)} m";
        }

        // Handle count
        if (dimension == Dimension.Count || normUnit is "und" or "unds" or "unidad" or "unidades" or "u")
        {
            var intCount = (int)Math.Round(amount, MidpointRounding.AwayFromZero);
            return intCount == 1 ? "1 unidad" : $"{intCount} unidades";
        }

        // Generic fallback
        if (!string.IsNullOrWhiteSpace(unitHint))
        {
            return $"{FormatNumber(amount)} {unitHint}";
        }

        return FormatNumber(amount);
    }

    private static string FormatNumber(decimal value)
    {
        // Format with up to 2 decimal places, trimming trailing zeros
        if (value == Math.Truncate(value))
        {
            return value.ToString("0", CoCulture);
        }

        return value.ToString("0.##", CoCulture);
    }
}