using System.Globalization;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public record UnitPriceCalculation(
    decimal AbsolutePrice,
    decimal PricePerBaseUnit, // $/kg, $/L, $/und, $/m
    string BaseUnitSymbol,     // "kg", "L", "und", "m"
    string FormattedPricePerBaseUnit,
    string UnitPriceDisplay = "",
    string UnitPriceUnit = "",
    string PricingMode = "STANDARD",
    string PriceBasis = "",
    string PriceNotes = ""
);

public record OverbuyEvaluation(
    decimal TargetQuantity,
    decimal ItemQuantity,
    int PackagesRequired,
    decimal TotalAcquired,
    decimal TotalCost,
    decimal OverbuyAmount,
    decimal OverbuyPercentage,
    bool IsAcceptable,
    string Explanation
);

public static class UnitComparisonService
{
    private static readonly string[] WeightBasedKeywords =
    [
        "cerdo", "res", "pollo", "carne", "pescado", "filete", "lomo", "lomito",
        "costilla", "pechuga", "tocino", "morrillo", "sobrebarriga", "falda",
        "milanesa", "chuleta", "solomito", "bife", "punta de anca", "carne molida",
        "pernil", "muslo", "alitas", "chicharrón", "chicharron", "salmon", "salmón",
        "tilapia", "trucha", "atún fresco", "atun fresco", "mondongo"
    ];

    public static string FormatCo(decimal amount)
    {
        var rounded = Math.Round(amount, MidpointRounding.AwayFromZero);
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("es-CO").Clone();
        culture.NumberFormat.NumberGroupSeparator = ".";
        culture.NumberFormat.NumberDecimalSeparator = ",";
        return $"${rounded.ToString("N0", culture)}";
    }

    public static bool IsWeightBasedProduct(string? productName, string? category = null)
    {
        var combined = $"{(productName ?? "")} {(category ?? "")}".ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(combined)) return false;

        return WeightBasedKeywords.Any(kw => combined.Contains(kw, StringComparison.OrdinalIgnoreCase));
    }

    public static UnitPriceCalculation? CalculateUnitPrice(
        decimal price,
        StandardQuantity? quantity,
        string? productName = null,
        string? category = null)
    {
        if (price <= 0)
            return null;

        var isWeightBased = IsWeightBasedProduct(productName, category);

        if (quantity == null || quantity.TotalAmount <= 0 || quantity.Dimension == Dimension.Unknown)
        {
            if (isWeightBased)
            {
                return new UnitPriceCalculation(
                    AbsolutePrice: price,
                    PricePerBaseUnit: price,
                    BaseUnitSymbol: "kg",
                    FormattedPricePerBaseUnit: $"${price.ToString("N0", CultureInfo.InvariantCulture)}/kg",
                    UnitPriceDisplay: $"{FormatCo(price)}/kg",
                    UnitPriceUnit: "kg",
                    PricingMode: "WEIGHT_BASED",
                    PriceBasis: "kg",
                    PriceNotes: "El valor final depende del peso."
                );
            }
            return null;
        }

        var pricePerBase = Math.Round(price / quantity.TotalAmount, 2);
        var baseSymbol = quantity.Dimension switch
        {
            Dimension.Mass => "kg",
            Dimension.Volume => "L",
            Dimension.Count => "und",
            Dimension.Length => "m",
            _ => "unidad"
        };

        var unitPriceUnit = quantity.Dimension switch
        {
            Dimension.Mass => "kg",
            Dimension.Volume => "L",
            Dimension.Count => "unidad",
            Dimension.Length => "m",
            _ => "unidad"
        };

        var mode = isWeightBased && quantity.Dimension == Dimension.Mass && quantity.TotalAmount == 1.0m
            ? "WEIGHT_BASED"
            : "STANDARD";

        var basis = mode == "WEIGHT_BASED" ? "kg" : unitPriceUnit;
        var notes = mode == "WEIGHT_BASED" ? "El valor final depende del peso." : "";

        return new UnitPriceCalculation(
            AbsolutePrice: price,
            PricePerBaseUnit: pricePerBase,
            BaseUnitSymbol: baseSymbol,
            FormattedPricePerBaseUnit: $"${pricePerBase.ToString("N0", CultureInfo.InvariantCulture)}/{baseSymbol}",
            UnitPriceDisplay: $"{FormatCo(pricePerBase)}/{unitPriceUnit}",
            UnitPriceUnit: unitPriceUnit,
            PricingMode: mode,
            PriceBasis: basis,
            PriceNotes: notes
        );
    }

    public static OverbuyEvaluation EvaluateOverbuy(
        decimal targetQuantity,
        decimal itemQuantity,
        decimal itemPrice,
        decimal maxOverbuyPercent = 20.0m)
    {
        if (targetQuantity <= 0 || itemQuantity <= 0)
        {
            return new OverbuyEvaluation(
                TargetQuantity: targetQuantity,
                ItemQuantity: itemQuantity,
                PackagesRequired: 1,
                TotalAcquired: itemQuantity,
                TotalCost: itemPrice,
                OverbuyAmount: 0m,
                OverbuyPercentage: 0m,
                IsAcceptable: false,
                Explanation: "Cantidades inválidas para cálculo de sobrecompra."
            );
        }

        var packages = (int)Math.Ceiling(targetQuantity / itemQuantity);
        if (packages <= 0) packages = 1;

        var totalAcquired = packages * itemQuantity;
        var totalCost = packages * itemPrice;
        var overbuyAmount = Math.Max(0m, totalAcquired - targetQuantity);
        var overbuyPercent = (overbuyAmount / targetQuantity) * 100m;
        var isAcceptable = overbuyPercent <= maxOverbuyPercent;

        var explanation = overbuyPercent == 0
            ? "Cubre exactamente la cantidad requerida."
            : isAcceptable
                ? $"Sobrecompra aceptable del {overbuyPercent:N1}% ({overbuyAmount:N2} de más)."
                : $"Sobrecompra excesiva del {overbuyPercent:N1}% (límite permitido: {maxOverbuyPercent:N0}%).";

        return new OverbuyEvaluation(
            TargetQuantity: targetQuantity,
            ItemQuantity: itemQuantity,
            PackagesRequired: packages,
            TotalAcquired: totalAcquired,
            TotalCost: totalCost,
            OverbuyAmount: overbuyAmount,
            OverbuyPercentage: overbuyPercent,
            IsAcceptable: isAcceptable,
            Explanation: explanation
        );
    }
}
