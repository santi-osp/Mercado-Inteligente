using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MercadoInteligente.Domain;
namespace MercadoInteligente.Application;
public record ParsedQuantity(decimal Amount, Dimension Dimension, int PackCount);
public static partial class Units
{
    public static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var clean = new string(decomposed.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return Regex.Replace(clean.ToLowerInvariant().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
    }
    public static ParsedQuantity? Parse(string input)
    {
        var normalized = Normalize(input).Replace(',', '.');
        var match = QuantityPattern().Match(normalized);
        if (match.Success && match.Groups["unit"].Success) return ParseQuantity(match);
        var packOnly = PackPattern().Match(normalized);
        if (packOnly.Success) return new ParsedQuantity(int.Parse(packOnly.Groups["count"].Value, CultureInfo.InvariantCulture), Dimension.Count, int.Parse(packOnly.Groups["count"].Value, CultureInfo.InvariantCulture));
        return null;
    }
    private static ParsedQuantity? ParseQuantity(Match match)
    {
        var count = match.Groups["count"].Success ? int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture) : 1;
        var amount = decimal.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture);
        var unit = match.Groups["unit"].Value;
        var dimension = unit switch { "kg" or "g" or "gr" or "grs" => Dimension.Mass, "l" or "lt" or "ltr" or "ml" => Dimension.Volume, "und" or "unidad" or "unidades" => Dimension.Count, "m" or "cm" => Dimension.Length, _ => Dimension.Unknown };
        if (dimension == Dimension.Unknown || amount <= 0 || count <= 0) return null;
        var factor = unit is "g" or "gr" or "grs" or "ml" ? 0.001m : unit == "cm" ? 0.01m : 1m;
        return new ParsedQuantity(count * amount * factor, dimension, count);
    }
    public static decimal? UnitPrice(decimal price, ParsedQuantity? quantity) => price > 0 && quantity?.Amount > 0 ? price / quantity.Amount : null;
    [GeneratedRegex(@"\b[x\u00D7]\s*(?<count>\d+)\b")]
    private static partial Regex PackPattern();
    [GeneratedRegex(@"(?:(?<count>\d+)\s*[x×]\s*)?(?<amount>\d+(?:\.\d+)?)\s*(?<unit>kg|grs|gr|g|ltr|lt|l|ml|unidades|unidad|und|cm|m)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QuantityPattern();
}
