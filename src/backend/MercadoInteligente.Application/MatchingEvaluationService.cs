using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public record LabeledProductPair(
    string Id,
    string NameA,
    string? BrandA,
    string? EanA,
    string NameB,
    string? BrandB,
    string? EanB,
    MatchKind ExpectedMatch,
    string Description
);

public record MatchingEvaluationReport(
    int TotalPairs,
    int ExactTruePositives,
    int ExactFalsePositives,
    int ExactFalseNegatives,
    int ExactTrueNegatives,
    decimal ExactPrecision,
    decimal ExactRecall,
    decimal ExactF1,
    int SubstituteMatches,
    int NotEquivalentMatches,
    List<PairEvaluationDetail> Details
);

public record PairEvaluationDetail(
    string PairId,
    string Description,
    MatchKind Expected,
    MatchKind Actual,
    decimal Confidence,
    string Evidence,
    bool IsCorrect
);

public static class MatchingEvaluationService
{
    public static readonly List<LabeledProductPair> BenchmarkDataset =
    [
        // 1. EXACT matches (Identical product across retailers)
        new("EX-01", "Coca-Cola Sabor Original 1.5 L", "Coca-Cola", "7702535003504",
                     "Gaseosa Coca-Cola Original 1500 ml", "Coca-Cola", "7702535003504",
                     MatchKind.Exact, "Coca-Cola Original 1.5L con mismo EAN y volumen equivalente"),

        new("EX-02", "Leche Entera Alquería 1 L", "Alquería", "7702177000109",
                     "Leche Entera Larga Vida Alquería 1000 ml", "Alquería", "7702177000109",
                     MatchKind.Exact, "Leche Entera Alquería 1L vs 1000ml"),

        new("EX-03", "Arroz Blanco Diana 1000 g", "Diana", "7702511000215",
                     "Arroz Diana Blanco 1 kg", "Diana", "7702511000215",
                     MatchKind.Exact, "Arroz Diana 1000g vs 1kg con mismo EAN"),

        new("EX-04", "Atún Lomitos en Agua Van Camp's 160 g", "Van Camp's", "7702084000302",
                     "Lomitos de Atún en Agua Van Camps 160g", "Van Camp's", "7702084000302",
                     MatchKind.Exact, "Atún Van Camps en agua 160g"),

        new("EX-05", "Aceite Vegetal Premier 1000 ml", "Premier", "7702057045019",
                     "Aceite Vegetal Premier 1 L", "Premier", "7702057045019",
                     MatchKind.Exact, "Aceite vegetal 1L vs 1000ml misma marca"),

        // 2. SUBSTITUTE matches (Alternative brands or similar sizes)
        new("SUB-01", "Leche Entera Latti 1000 ml", "Latti", "7709998000111",
                      "Leche Entera Alquería 1 L", "Alquería", "7702177000109",
                      MatchKind.Substitute, "Leche Entera D1 (Latti) vs Éxito (Alquería) misma categoría y volumen"),

        new("SUB-02", "Arroz Blanco De La Cuesta 1000 g", "De La Cuesta", "7709998000222",
                      "Arroz Blanco Diana 1 kg", "Diana", "7702511000215",
                      MatchKind.Substitute, "Arroz D1 (De La Cuesta) vs Éxito (Diana) misma categoría y masa"),

        new("SUB-03", "Huevos Rojos Tipo AA 30 und", "Redondos", "7709998000333",
                      "Huevos Rojos Tipo AAA 30 unidades", "Santa Reyes", "7702000000333",
                      MatchKind.Substitute, "Huevos AA vs AAA sustitutos de categoría con grado distinto"),

        new("SUB-04", "Aceite de Girasol Kythos 900 ml", "Kythos", "7709998000444",
                      "Aceite de Girasol Diana 1000 ml", "Diana", "7702511000444",
                      MatchKind.Substitute, "Aceite girasol 900ml vs 1000ml (dentro de tolerancia 15%)"),

        new("SUB-05", "Atún en Trozos en Agua Bruma 140 g", "Bruma", "7709998000555",
                      "Atún en Lomitos en Agua Isabel 160 g", "Isabel", "7702000000555",
                      MatchKind.Substitute, "Atún en agua marcas alternativas tamaño cercano (140g vs 160g: diff 12.5% <= 15%)"),

        // 3. NOT EQUIVALENT matches (Critical variant/category/dimension conflicts)
        new("NOT-01", "Coca-Cola Original 1.5 L", "Coca-Cola", null,
                      "Coca-Cola Sin Azúcar Zero 1.5 L", "Coca-Cola", null,
                      MatchKind.NotEquivalent, "Coca-Cola Original vs Zero (conflicto crítico de azúcar)"),

        new("NOT-02", "Leche Entera Colanta 1 L", "Colanta", null,
                      "Leche Deslactosada Colanta 1 L", "Colanta", null,
                      MatchKind.NotEquivalent, "Leche Entera vs Deslactosada (conflicto crítico de lactosa)"),

        new("NOT-03", "Atún en Agua Van Camp's 160 g", "Van Camp's", null,
                      "Atún en Aceite Van Camp's 160 g", "Van Camp's", null,
                      MatchKind.NotEquivalent, "Atún en Agua vs Atún en Aceite (conflicto de líquido)"),

        new("NOT-04", "Arroz Blanco Diana 1 kg", "Diana", null,
                      "Azúcar Blanco Manuelita 1 kg", "Manuelita", null,
                      MatchKind.NotEquivalent, "Arroz vs Azúcar (categorías funcionales completamente distintas)"),

        new("NOT-05", "Leche Entera Alquería 1 L", "Alquería", null,
                      "Arroz Blanco Diana 1 kg", "Diana", null,
                      MatchKind.NotEquivalent, "Dimensión y categoría incompatibles (Volumen vs Masa)"),

        new("NOT-06", "Coca-Cola Original 1.5 L", "Coca-Cola", "7702535003504",
                      "Coca-Cola Zero 1.5 L", "Coca-Cola", "7702535003504",
                      MatchKind.Candidate, "EAN idéntico erróneo pero con conflicto de variante (debe ir a revisión/Candidate, nunca Exact)")
    ];

    public static MatchingEvaluationReport EvaluateDataset(IEnumerable<LabeledProductPair>? dataset = null)
    {
        var pairs = (dataset ?? BenchmarkDataset).ToList();
        var details = new List<PairEvaluationDetail>();

        int exactTp = 0, exactFp = 0, exactFn = 0, exactTn = 0;
        int subMatches = 0, notEqMatches = 0;

        foreach (var pair in pairs)
        {
            var normA = ProductNormalizer.Normalize(pair.NameA, pair.BrandA, pair.EanA);
            var normB = ProductNormalizer.Normalize(pair.NameB, pair.BrandB, pair.EanB);

            var result = DeterministicMatcher.Evaluate(normA, normB);

            var isExactActual = result.Kind == MatchKind.Exact;
            var isExactExpected = pair.ExpectedMatch == MatchKind.Exact;

            if (isExactActual && isExactExpected) exactTp++;
            else if (isExactActual && !isExactExpected) exactFp++;
            else if (!isExactActual && isExactExpected) exactFn++;
            else exactTn++;

            if (result.Kind == MatchKind.Substitute) subMatches++;
            if (result.Kind == MatchKind.NotEquivalent) notEqMatches++;

            var isCorrect = result.Kind == pair.ExpectedMatch;

            details.Add(new PairEvaluationDetail(
                PairId: pair.Id,
                Description: pair.Description,
                Expected: pair.ExpectedMatch,
                Actual: result.Kind,
                Confidence: result.ConfidenceScore,
                Evidence: result.Evidence,
                IsCorrect: isCorrect
            ));
        }

        var precision = (exactTp + exactFp) > 0 ? (decimal)exactTp / (exactTp + exactFp) : 1.0m;
        var recall = (exactTp + exactFn) > 0 ? (decimal)exactTp / (exactTp + exactFn) : 0.0m;
        var f1 = (precision + recall) > 0 ? (2 * precision * recall) / (precision + recall) : 0.0m;

        return new MatchingEvaluationReport(
            TotalPairs: pairs.Count,
            ExactTruePositives: exactTp,
            ExactFalsePositives: exactFp,
            ExactFalseNegatives: exactFn,
            ExactTrueNegatives: exactTn,
            ExactPrecision: precision,
            ExactRecall: recall,
            ExactF1: f1,
            SubstituteMatches: subMatches,
            NotEquivalentMatches: notEqMatches,
            Details: details
        );
    }
}
