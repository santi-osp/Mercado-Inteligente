using MercadoInteligente.Domain;
namespace MercadoInteligente.Application;
public static class FoundationRules
{
    public static AvailabilityStatus AvailabilityFromStock(int? stock) => stock switch
    {
        null => AvailabilityStatus.Unknown,
        <= 0 => AvailabilityStatus.OutOfStock,
        _ => AvailabilityStatus.InStock
    };
    public static bool Comparable(ParsedQuantity? left, ParsedQuantity? right) => left is not null && right is not null && left.Dimension == right.Dimension;
    public static bool IsPhysicalPrice(PriceScope scope) => scope == PriceScope.PhysicalVerified;
    public static bool IsEligibleSeller(SellerKind kind) => kind == SellerKind.RetailerDirect;
}
