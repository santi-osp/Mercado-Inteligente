using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public enum LocationInputSource
{
    Coordinates,
    GoogleMapsUrl,
    GoogleMapsShortUrl,
    Address,
    Profile
}

public enum LocationInputStatus
{
    Success,
    NeedsGeocoding,
    Invalid
}

public sealed record LocationParseResult(
    string OriginalInput,
    string? NormalizedAddress,
    decimal? Latitude,
    decimal? Longitude,
    LocationInputSource Source,
    LocationInputStatus Status,
    string? ErrorMessage = null
);

public sealed record AddressComponents(
    string? Country = null,
    string? CountryCode = null,
    string? State = null,
    string? StateCode = null,
    string? City = null,
    string? PostalCode = null,
    string? Neighborhood = null,
    string? Street = null,
    string? StreetNumber = null
);

public sealed record GeocodingResult(
    decimal Latitude,
    decimal Longitude,
    string FormattedAddress,
    string ProviderStatus,
    AddressComponents? Components = null
);

public enum TransportMode
{
    Driving,
    Motorcycle,
    Walking,
    Bicycling
}

public enum RouteType
{
    RoadRoute,
    StraightLineEstimate,
    CachedRoute
}

public sealed record RouteInfo(
    decimal DistanceKm,
    int DistanceMeters,
    decimal DurationMinutes,
    RouteType Type,
    string Provider,
    bool RoutingFallback = false,
    string? RoutingModeRequested = null,
    string? RoutingModeUsed = null,
    string? FallbackMessage = null,
    string DistanceSource = "GOOGLE_ROUTES",
    bool IsApproximate = false
);

public sealed record RouteLeg(
    string FromLabel,
    string ToLabel,
    decimal DistanceKm,
    decimal DurationMinutes,
    RouteType Type
);

public sealed record StoreRoutePlan(
    decimal OriginLatitude,
    decimal OriginLongitude,
    bool ReturnToOrigin,
    TransportMode TransportMode,
    decimal CostPerKm,
    decimal? MaxRouteKm,
    decimal TotalDistanceKm,
    decimal TotalDurationMinutes,
    decimal EstimatedTravelCost,
    RouteType RouteType,
    IReadOnlyList<Store> OrderedStores,
    IReadOnlyList<RouteLeg> Legs,
    string GoogleMapsNavigationUrl,
    bool RoutingFallback = false,
    string? RoutingModeRequested = null,
    string? RoutingModeUsed = null,
    string? FallbackMessage = null,
    string DistanceSource = "GOOGLE_ROUTES",
    bool IsApproximate = false
);

public sealed record OptimizerLocationContext(
    decimal OriginLatitude,
    decimal OriginLongitude,
    bool ReturnToOrigin = true,
    TransportMode TransportMode = TransportMode.Driving,
    decimal CostPerKm = 2500m,
    decimal PriceWeight = 0.7m,
    decimal DistanceWeight = 0.2m,
    decimal ConvenienceWeight = 0.1m
);
