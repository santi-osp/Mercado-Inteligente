using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Xunit;

namespace MercadoInteligente.Tests;

public class Goal05MatchingTests
{
    [Fact]
    public void CocaColaOriginal_Vs_Zero_IsNotExactMatch()
    {
        var prodA = ProductNormalizer.Normalize("Coca-Cola Sabor Original 1.5 L", "Coca-Cola");
        var prodB = ProductNormalizer.Normalize("Coca-Cola Zero Sin Azúcar 1.5 L", "Coca-Cola");

        var result = DeterministicMatcher.Evaluate(prodA, prodB);

        Assert.NotEqual(MatchKind.Exact, result.Kind);
        Assert.True(DeterministicMatcher.HasVariantConflict(prodA, prodB, out _));
    }

    [Fact]
    public void OneLiter_Vs_ThousandMilliliters_IsEquivalentSize()
    {
        var qty1L = QuantityParser.Parse("1 L");
        var qty1000ml = QuantityParser.Parse("1000 ml");

        Assert.NotNull(qty1L);
        Assert.NotNull(qty1000ml);
        Assert.Equal(Dimension.Volume, qty1L.Dimension);
        Assert.Equal(Dimension.Volume, qty1000ml.Dimension);
        Assert.Equal(qty1L.TotalAmount, qty1000ml.TotalAmount);

        var diff = QuantityParser.CalculateSizeDifferenceRatio(qty1L, qty1000ml);
        Assert.Equal(0.0m, diff);
    }

    [Fact]
    public void NineHundredGrams_Vs_OneKilogram_IsSimilarWithinTolerance()
    {
        var qty900g = QuantityParser.Parse("900 g");
        var qty1kg = QuantityParser.Parse("1 kg");

        Assert.NotNull(qty900g);
        Assert.NotNull(qty1kg);
        Assert.Equal(Dimension.Mass, qty900g.Dimension);
        Assert.Equal(Dimension.Mass, qty1kg.Dimension);

        var diff = QuantityParser.CalculateSizeDifferenceRatio(qty900g, qty1kg);
        // (1.0 - 0.9) / 1.0 = 0.10 (10%)
        Assert.Equal(0.10m, diff);
        Assert.True(diff <= 0.15m); // Within standard 15% tolerance
    }

    [Fact]
    public void FiveKilograms_NotAccepted_IfOverbuyExceedsThreshold()
    {
        var targetKg = 1.0m;
        var itemKg = 5.0m;
        var itemPrice = 25000m;
        var maxOverbuyPercent = 20.0m;

        var overbuy = UnitComparisonService.EvaluateOverbuy(targetKg, itemKg, itemPrice, maxOverbuyPercent);

        Assert.False(overbuy.IsAcceptable);
        Assert.Equal(400m, overbuy.OverbuyPercentage); // (5 - 1) / 1 * 100 = 400%
        Assert.Contains("Sobrecompra excesiva", overbuy.Explanation);
    }

    [Fact]
    public void SameEan_WithCriticalVariantConflict_DoesNotAutoMatchExact()
    {
        // EAN recycled or dirty data where one is Zero and one is Regular
        var prodA = ProductNormalizer.Normalize("Coca-Cola Original 1.5 L", "Coca-Cola", ean: "7702535003504");
        var prodB = ProductNormalizer.Normalize("Coca-Cola Zero 1.5 L", "Coca-Cola", ean: "7702535003504");

        var result = DeterministicMatcher.Evaluate(prodA, prodB);

        Assert.NotEqual(MatchKind.Exact, result.Kind);
        Assert.True(result.HasConflict);
        Assert.NotNull(result.ConflictReason);
        Assert.Equal(MatchKind.Candidate, result.Kind);
    }

    [Fact]
    public void HighFuzzySimilarity_WithDifferentVariant_IsNotExact()
    {
        // "Leche Entera Colanta 1L" vs "Leche Deslactosada Colanta 1L" have very high lexical overlap
        var prodA = ProductNormalizer.Normalize("Leche Entera Colanta 1 L", "Colanta");
        var prodB = ProductNormalizer.Normalize("Leche Deslactosada Colanta 1 L", "Colanta");

        var result = DeterministicMatcher.Evaluate(prodA, prodB);

        Assert.NotEqual(MatchKind.Exact, result.Kind);
        Assert.True(DeterministicMatcher.HasVariantConflict(prodA, prodB, out _));
    }

    [Fact]
    public void UserOverride_DoNotMatch_WinsOverAnyRuleOrEan()
    {
        var prodA = ProductNormalizer.Normalize("Coca-Cola Sabor Original 1.5 L", "Coca-Cola", "7702535003504");
        var prodB = ProductNormalizer.Normalize("Coca-Cola Sabor Original 1.5 L", "Coca-Cola", "7702535003504");

        var userOverride = new MatchOverride
        {
            ExternalSkuId = Guid.NewGuid(),
            ProductVariantId = Guid.NewGuid(),
            Kind = MatchKind.NotEquivalent
        };

        var result = DeterministicMatcher.Evaluate(prodA, prodB, matchOverride: userOverride);

        Assert.Equal(MatchKind.NotEquivalent, result.Kind);
        Assert.Contains("User override", result.Evidence);
    }

    [Fact]
    public void Multipack_SixByOneLiter_ParsesCorrectlyAndMatchesSixLiters()
    {
        var packQty = QuantityParser.Parse("6 x 1 L");
        var singleQty = QuantityParser.Parse("6 L");

        Assert.NotNull(packQty);
        Assert.NotNull(singleQty);
        Assert.Equal(6, packQty.PackageCount);
        Assert.Equal(1.0m, packQty.AmountPerUnit);
        Assert.Equal(6.0m, packQty.TotalAmount);
        Assert.Equal(Dimension.Volume, packQty.Dimension);

        var diff = QuantityParser.CalculateSizeDifferenceRatio(packQty, singleQty);
        Assert.Equal(0.0m, diff);
    }

    [Fact]
    public void EggGrade_AA_Vs_AAA_IsSubstitute_NotExact()
    {
        var eggsAA = ProductNormalizer.Normalize("Huevos Rojos Tipo AA 30 unidades", "Redondos");
        var eggsAAA = ProductNormalizer.Normalize("Huevos Rojos Tipo AAA 30 unidades", "Santa Reyes");

        var result = DeterministicMatcher.Evaluate(eggsAA, eggsAAA);

        Assert.Equal(MatchKind.Substitute, result.Kind);
        Assert.NotEqual(MatchKind.Exact, result.Kind);
        Assert.Contains("Egg grade", result.Evidence);
    }

    [Fact]
    public void Grams_Vs_Milliliters_CannotBeComparedAcrossBaseUnit()
    {
        var prodGrams = ProductNormalizer.Normalize("Mantequilla Colanta 250 g", "Colanta");
        var prodMl = ProductNormalizer.Normalize("Crema de Leche Colanta 250 ml", "Colanta");

        Assert.NotNull(prodGrams.Quantity);
        Assert.NotNull(prodMl.Quantity);
        Assert.Equal(Dimension.Mass, prodGrams.Quantity.Dimension);
        Assert.Equal(Dimension.Volume, prodMl.Quantity.Dimension);
        Assert.False(QuantityParser.AreDimensionsCompatible(prodGrams.Quantity.Dimension, prodMl.Quantity.Dimension));

        var result = DeterministicMatcher.Evaluate(prodGrams, prodMl);
        Assert.Equal(MatchKind.NotEquivalent, result.Kind);
        Assert.Contains("Incompatible dimensions", result.Evidence);
    }

    [Fact]
    public void UnitComparisonService_CalculatesPricePerUnitCorrectly()
    {
        var qty = QuantityParser.Parse("500 g");
        var price = 6000m; // $6000 for 500g -> $12000 / kg

        var unitPrice = UnitComparisonService.CalculateUnitPrice(price, qty);

        Assert.NotNull(unitPrice);
        Assert.Equal(12000m, unitPrice.PricePerBaseUnit);
        Assert.Equal("kg", unitPrice.BaseUnitSymbol);
        Assert.Equal("$12,000/kg", unitPrice.FormattedPricePerBaseUnit);
    }

    [Fact]
    public void BenchmarkDataset_Evaluation_ProducesZeroFalsePositivesForExact()
    {
        var report = MatchingEvaluationService.EvaluateDataset();

        Assert.True(report.TotalPairs >= 15);
        Assert.Equal(0, report.ExactFalsePositives); // False positive rate for EXACT must be 0!
        Assert.Equal(1.0m, report.ExactPrecision);   // Precision 100%
        Assert.True(report.ExactRecall >= 0.90m);    // High recall
        Assert.True(report.ExactF1 >= 0.90m);
    }

    [Fact]
    public void ToiletPaper_MultipackWithMeters_CalculatesCorrectly()
    {
        var qty = QuantityParser.Parse("12 rollos x 40 m");

        Assert.NotNull(qty);
        Assert.Equal(Dimension.Length, qty.Dimension);
        Assert.Equal(12, qty.PackageCount);
        Assert.Equal(480m, qty.TotalAmount); // 12 * 40m = 480m
    }
}
