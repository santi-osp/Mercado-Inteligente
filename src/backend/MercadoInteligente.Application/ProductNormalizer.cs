using System.Text.RegularExpressions;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public static partial class ProductNormalizer
{
    private static readonly string[] KnownBrands =
    [
        "coca-cola", "coca cola", "pepsi", "postobon", "colombiana", "sprite", "quatro",
        "alpina", "colanta", "alqueria", "parmalat", "latti", "san fernando",
        "diana", "roa", "florhuila", "castellano",
        "doria", "comarrico", "la muneca", "barilla",
        "san jorge", "fruco", "bary", "la constancia",
        "van camps", "vancamps", "isabel", "alamar", "bruma", "delfin",
        "familia", "scott", "suave y dulce", "elite", "rosal",
        "ariel", "fab", "dersa", "rey", "blancox", "clorox", "limpido", "rindex",
        "sello rojo", "aguila roja", "juan valdez", "nescafe", "colcafe", "aroma",
        "bimbo", "comapan", "guadalupe", "taeq", "ekono", "exito", "frescampo",
        "de la cuesta", "kythos", "redondos", "corona", "luker", "chocolisto", "milo"
    ];

    private static readonly string[] KnownBrandsSorted = KnownBrands.OrderByDescending(x => x.Length).ToArray();

    private static readonly Dictionary<string, string> BrandCanonicalMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["coca-cola"] = "Coca-Cola",
        ["coca cola"] = "Coca-Cola",
        ["pepsi"] = "Pepsi",
        ["postobon"] = "Postobón",
        ["colombiana"] = "Colombiana",
        ["alpina"] = "Alpina",
        ["colanta"] = "Colanta",
        ["alqueria"] = "Alquería",
        ["parmalat"] = "Parmalat",
        ["latti"] = "Latti",
        ["diana"] = "Diana",
        ["roa"] = "Roa",
        ["florhuila"] = "Florhuila",
        ["doria"] = "Doria",
        ["comarrico"] = "Comarrico",
        ["san jorge"] = "San Jorge",
        ["fruco"] = "Fruco",
        ["van camps"] = "Van Camp's",
        ["vancamps"] = "Van Camp's",
        ["isabel"] = "Isabel",
        ["bruma"] = "Bruma",
        ["familia"] = "Familia",
        ["scott"] = "Scott",
        ["suave y dulce"] = "Suave y Dulce",
        ["ariel"] = "Ariel",
        ["fab"] = "Fab",
        ["rey"] = "Rey",
        ["blancox"] = "Blancox",
        ["sello rojo"] = "Sello Rojo",
        ["aguila roja"] = "Águila Roja",
        ["juan valdez"] = "Juan Valdez",
        ["nescafe"] = "Nescafé",
        ["colcafe"] = "Colcafé",
        ["aroma"] = "Aroma",
        ["bimbo"] = "Bimbo",
        ["comapan"] = "Comapan",
        ["taeq"] = "Taeq",
        ["ekono"] = "Ekono",
        ["exito"] = "Éxito",
        ["frescampo"] = "Frescampo",
        ["de la cuesta"] = "De La Cuesta",
        ["kythos"] = "Kythos",
        ["redondos"] = "Redondos",
        ["corona"] = "Corona",
        ["luker"] = "Luker",
        ["chocolisto"] = "Chocolisto",
        ["milo"] = "Milo"
    };

    public static NormalizedProductInfo Normalize(string? rawName, string? brand = null, string? ean = null, decimal? quantity = null, Dimension dimension = Dimension.Unknown)
    {
        var raw = rawName?.Trim() ?? "";
        var normalizedText = Units.Normalize(raw).ToLowerInvariant();

        // 1. Detect Brand
        var detectedBrand = DetectBrand(raw, brand, normalizedText);

        // 2. Detect Product Type / Functional Category
        var productType = DetectProductType(normalizedText);

        // 3. Detect Variant Attributes
        var isSugarFree = DetectSugarFree(normalizedText);
        var isLactoseFree = DetectLactoseFree(normalizedText);
        var eggGrade = DetectEggGrade(normalizedText);
        var oilType = DetectOilType(normalizedText);
        var tunaLiquid = DetectTunaLiquid(normalizedText);
        var variantType = DetectVariantType(normalizedText, isSugarFree, isLactoseFree, eggGrade, oilType, tunaLiquid);

        // 4. Quantity Parsing
        var standardQty = QuantityParser.Parse(raw);
        if (standardQty == null && quantity.HasValue && quantity.Value > 0 && dimension != Dimension.Unknown)
        {
            standardQty = new StandardQuantity(quantity.Value, dimension, 1, quantity.Value, "");
        }

        // 5. Clean Name (strip promo phrases)
        var cleanName = CleanPromoPhrases(raw);

        // 6. Clean EAN
        var cleanEan = CleanEan(ean);

        var extraAttributes = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(eggGrade)) extraAttributes["egg_grade"] = eggGrade;
        if (!string.IsNullOrEmpty(oilType)) extraAttributes["oil_type"] = oilType;
        if (!string.IsNullOrEmpty(tunaLiquid)) extraAttributes["tuna_liquid"] = tunaLiquid;
        if (isSugarFree) extraAttributes["sugar_free"] = "true";
        if (isLactoseFree) extraAttributes["lactose_free"] = "true";

        return new NormalizedProductInfo(
            RawName: raw,
            CleanName: cleanName,
            NormalizedBrand: detectedBrand,
            ProductType: productType,
            VariantType: variantType,
            IsSugarFree: isSugarFree,
            IsLactoseFree: isLactoseFree,
            EggGrade: eggGrade,
            OilType: oilType,
            TunaLiquid: tunaLiquid,
            Quantity: standardQty,
            Ean: cleanEan,
            ExtraAttributes: extraAttributes
        );
    }

    private static string DetectBrand(string raw, string? brand, string normalizedText)
    {
        if (!string.IsNullOrWhiteSpace(brand))
        {
            var brandNorm = Units.Normalize(brand).ToLowerInvariant();
            if (BrandCanonicalMap.TryGetValue(brandNorm, out var mapped))
                return mapped;
            return brand.Trim();
        }

        // Search in text
        foreach (var b in KnownBrandsSorted)
        {
            if (!normalizedText.Contains(b)) continue;
            if (Regex.IsMatch(normalizedText, $@"\b{Regex.Escape(b)}\b", RegexOptions.IgnoreCase))
            {
                if (BrandCanonicalMap.TryGetValue(b, out var mapped))
                    return mapped;
                return b;
            }
        }

        return "Genérico";
    }

    private static string DetectProductType(string text)
    {
        if (text.Contains("leche") && Regex.IsMatch(text, @"\bleche\b")) return "Leche";
        if (text.Contains("arroz") && Regex.IsMatch(text, @"\barroz\b")) return "Arroz";
        if (text.Contains("huevo") && Regex.IsMatch(text, @"\bhuevo(s)?\b")) return "Huevos";
        if (text.Contains("aceite") && Regex.IsMatch(text, @"\baceite\b")) return "Aceite";
        if ((text.Contains("pasta") || text.Contains("espagueti") || text.Contains("spaghetti") || text.Contains("macarron") || text.Contains("tornillo") || text.Contains("fideo")) &&
            Regex.IsMatch(text, @"\b(pasta|espagueti|spaghetti|macarron(es)?|tornillo(s)?|fideos?)\b")) return "Pasta";
        if (text.Contains("atun") && Regex.IsMatch(text, @"\batun\b")) return "Atún";
        if (text.Contains("papel") && Regex.IsMatch(text, @"\bpapel\s*higienico\b")) return "Papel Higiénico";
        if (text.Contains("cafe") && Regex.IsMatch(text, @"\bcafe\b")) return "Café";
        if (text.Contains("jabon") && Regex.IsMatch(text, @"\bjabon\b")) return "Jabón";
        if (text.Contains("detergente") && Regex.IsMatch(text, @"\bdetergente\b")) return "Detergente";
        if (text.Contains("pan") && Regex.IsMatch(text, @"\bpan\b")) return "Pan";
        if (text.Contains("gaseosa") || text.Contains("coca-cola") || text.Contains("pepsi") || text.Contains("postobon")) return "Gaseosa";
        if (text.Contains("azucar") && Regex.IsMatch(text, @"\bazucar\b")) return "Azúcar";
        if (text.Contains("sal") && Regex.IsMatch(text, @"\bsal\b")) return "Sal";
        if (text.Contains("frijol") && Regex.IsMatch(text, @"\bfrijol(es)?\b")) return "Fríjol";
        if (text.Contains("lenteja") && Regex.IsMatch(text, @"\blenteja(s)?\b")) return "Lenteja";
        if (text.Contains("harina") && Regex.IsMatch(text, @"\bharina\b")) return "Harina";
        if (text.Contains("chocolate") && Regex.IsMatch(text, @"\bchocolate\b")) return "Chocolate";

        return "Abarrotes";
    }

    private static bool DetectSugarFree(string text)
    {
        return Regex.IsMatch(text, @"\b(zero|cero|sin\s*azucar|sugar\s*free|light|diet)\b");
    }

    private static bool DetectLactoseFree(string text)
    {
        return Regex.IsMatch(text, @"\b(deslactosada|sin\s*lactosa|lactose\s*free)\b");
    }

    private static string? DetectEggGrade(string text)
    {
        if (Regex.IsMatch(text, @"\b(tipo\s*)?aaa\b")) return "AAA";
        if (Regex.IsMatch(text, @"\b(tipo\s*)?aa\b")) return "AA";
        if (Regex.IsMatch(text, @"\b(tipo\s*)?a\b")) return "A";
        if (Regex.IsMatch(text, @"\b(tipo\s*)?b\b")) return "B";
        return null;
    }

    private static string? DetectOilType(string text)
    {
        if (Regex.IsMatch(text, @"\bgirasol\b")) return "Girasol";
        if (Regex.IsMatch(text, @"\boliva\b")) return "Oliva";
        if (Regex.IsMatch(text, @"\bcanola\b")) return "Canola";
        if (Regex.IsMatch(text, @"\bsoya\b")) return "Soya";
        if (Regex.IsMatch(text, @"\bmaiz\b")) return "Maíz";
        if (Regex.IsMatch(text, @"\bvegetal\b")) return "Vegetal";
        return null;
    }

    private static string? DetectTunaLiquid(string text)
    {
        if (Regex.IsMatch(text, @"\b(en\s*)?agua\b")) return "Agua";
        if (Regex.IsMatch(text, @"\b(en\s*)?aceite\b")) return "Aceite";
        return null;
    }

    private static string DetectVariantType(string text, bool isSugarFree, bool isLactoseFree, string? eggGrade, string? oilType, string? tunaLiquid)
    {
        var variants = new List<string>();

        if (text.Contains("leche"))
        {
            if (isLactoseFree)
            {
                variants.Add(text.Contains("semidescremada") ? "Deslactosada Semidescremada" : "Deslactosada");
            }
            else if (text.Contains("semidescremada"))
            {
                variants.Add("Semidescremada");
            }
            else if (text.Contains("descremada"))
            {
                variants.Add("Descremada");
            }
            else if (text.Contains("entera"))
            {
                variants.Add("Entera");
            }
        }

        if (isSugarFree) variants.Add("Zero/Sin Azúcar");
        else if (Regex.IsMatch(text, @"\b(original|clasica|tradicional)\b")) variants.Add("Original");

        if (!string.IsNullOrEmpty(eggGrade)) variants.Add($"Grado {eggGrade}");
        if (!string.IsNullOrEmpty(oilType)) variants.Add(oilType);
        if (!string.IsNullOrEmpty(tunaLiquid)) variants.Add($"En {tunaLiquid}");

        if (text.Contains("integral")) variants.Add("Integral");
        else if (text.Contains("blanco") || text.Contains("blanca")) variants.Add("Blanco");

        if (variants.Count == 0) return "Estándar";
        return string.Join(" - ", variants.Distinct());
    }

    private static string CleanPromoPhrases(string raw)
    {
        var clean = Regex.Replace(raw, @"\b(pague\s*\d+\s*lleve\s*\d+|oferta|gratis|\d+%\s*dcto|descuento|promocion|combo)\b", "", RegexOptions.IgnoreCase);
        return Regex.Replace(clean, @"\s{2,}", " ").Trim();
    }

    public static string? CleanEan(string? ean)
    {
        if (string.IsNullOrWhiteSpace(ean)) return null;
        var digitsOnly = Regex.Replace(ean, @"[^\d]", "");
        if (digitsOnly.Length is 8 or 12 or 13 or 14)
            return digitsOnly;
        return null;
    }
}
