namespace MercadoInteligente.Application;

public static class HaversineService
{
    private const double EarthRadiusKm = 6371.0;
    private const decimal DefaultRoadWindingFactor = 1.35m;

    public static decimal CalculateDistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        if (lat1 == lat2 && lon1 == lon2)
            return 0m;

        double dLat = ToRadians((double)(lat2 - lat1));
        double dLon = ToRadians((double)(lon2 - lon1));

        double rLat1 = ToRadians((double)lat1);
        double rLat2 = ToRadians((double)lat2);

        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(rLat1) * Math.Cos(rLat2) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);

        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        double distance = EarthRadiusKm * c;

        return Math.Round((decimal)distance, 4);
    }

    public static int CalculateDistanceMeters(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        var km = CalculateDistanceKm(lat1, lon1, lat2, lon2);
        return (int)Math.Round(km * 1000m);
    }

    public static decimal EstimateRoadDistanceKm(decimal straightLineKm, decimal? windingFactor = null)
    {
        var factor = windingFactor ?? DefaultRoadWindingFactor;
        return Math.Round(straightLineKm * factor, 3);
    }

    public static decimal EstimateDurationMinutes(decimal roadDistanceKm, TransportMode mode)
    {
        if (roadDistanceKm <= 0m) return 0m;

        decimal speedKmh = mode switch
        {
            TransportMode.Motorcycle => 30m,
            TransportMode.Walking => 4.5m,
            TransportMode.Bicycling => 15m,
            _ => 25m // Driving in Medellín urban traffic
        };

        decimal hours = roadDistanceKm / speedKmh;
        return Math.Round(hours * 60m, 1);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180.0;
}

public sealed class HaversineDistanceService : IHaversineDistanceService
{
    public decimal CalculateDistanceKm(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        => HaversineService.CalculateDistanceKm(lat1, lon1, lat2, lon2);

    public int CalculateDistanceMeters(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
        => HaversineService.CalculateDistanceMeters(lat1, lon1, lat2, lon2);

    public decimal EstimateRoadDistanceKm(decimal straightLineKm, decimal? windingFactor = null)
        => HaversineService.EstimateRoadDistanceKm(straightLineKm, windingFactor);

    public decimal EstimateDurationMinutes(decimal roadDistanceKm, TransportMode mode)
        => HaversineService.EstimateDurationMinutes(roadDistanceKm, mode);
}