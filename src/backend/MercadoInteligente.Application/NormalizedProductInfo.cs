using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public record NormalizedProductInfo(
    string RawName,
    string CleanName,
    string NormalizedBrand,
    string ProductType,
    string VariantType,
    bool IsSugarFree,
    bool IsLactoseFree,
    string? EggGrade,
    string? OilType,
    string? TunaLiquid,
    StandardQuantity? Quantity,
    string? Ean,
    Dictionary<string, string> ExtraAttributes
);
