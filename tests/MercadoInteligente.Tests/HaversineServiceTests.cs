using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Xunit;

namespace MercadoInteligente.Tests;

public class HaversineServiceTests
{
    [Fact]
    public void CalculateDistanceKm_SamePoint_ReturnsZero()
    {
        var dist = HaversineService.CalculateDistanceKm(6.2425m, -75.5925m, 6.2425m, -75.5925m);
        Assert.Equal(0m, dist);
    }

    [Fact]
    public void CalculateDistanceKm_ParqueBerrioToParquePoblado_AccurateDistance()
    {
        // Parque Berrío: 6.2518, -75.5636
        // Parque El Poblado: 6.2088, -75.5678
        // Straight line distance is approx 4.8 km to 5.2 km
        var dist = HaversineService.CalculateDistanceKm(6.2518m, -75.5636m, 6.2088m, -75.5678m);

        Assert.InRange(dist, 4.5m, 5.5m);
    }

    [Fact]
    public void EstimateRoadDistanceKm_AppliesUrbanWindingFactor()
    {
        var straightKm = 10.0m;
        var roadKm = HaversineService.EstimateRoadDistanceKm(straightKm);

        Assert.Equal(13.5m, roadKm); // 10 * 1.35
    }

    [Theory]
    [InlineData(10.0, TransportMode.Driving, 24.0)]      // 10 km / 25 km/h * 60 = 24 min
    [InlineData(10.0, TransportMode.Motorcycle, 20.0)]   // 10 km / 30 km/h * 60 = 20 min
    [InlineData(10.0, TransportMode.Bicycling, 40.0)]    // 10 km / 15 km/h * 60 = 40 min
    [InlineData(4.5, TransportMode.Walking, 60.0)]       // 4.5 km / 4.5 km/h * 60 = 60 min
    public void EstimateDurationMinutes_CalculatesExpectedTime(double km, TransportMode mode, double expectedMinutes)
    {
        var duration = HaversineService.EstimateDurationMinutes((decimal)km, mode);
        Assert.Equal((decimal)expectedMinutes, duration);
    }
}
