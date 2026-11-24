using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Xunit;

namespace MercadoInteligente.Tests;

public class StoreRouteOptimizerTests
{
    private readonly NullRoutingProvider _nullRouting = new();

    [Fact]
    public void OptimizeRouteOffline_ZeroStores_ReturnsEmptyPlan()
    {
        var plan = StoreRouteOptimizer.OptimizeRouteOffline(
            6.2425m, -75.5925m, []);

        Assert.Empty(plan.OrderedStores);
        Assert.Empty(plan.Legs);
        Assert.Equal(0m, plan.TotalDistanceKm);
        Assert.Equal("", plan.GoogleMapsNavigationUrl);
    }

    [Fact]
    public void OptimizeRouteOffline_SingleStore_CalculatesRoundTrip()
    {
        var store = new Store
        {
            Id = Guid.NewGuid(),
            Name = "D1 Laureles",
            Latitude = 6.2440m,
            Longitude = -75.5910m
        };

        var plan = StoreRouteOptimizer.OptimizeRouteOffline(
            6.2425m, -75.5925m, [store], returnToOrigin: true);

        Assert.Single(plan.OrderedStores);
        Assert.Equal(2, plan.Legs.Count); // Origin -> Store, Store -> Origin
        Assert.True(plan.TotalDistanceKm > 0m);
        Assert.True(plan.EstimatedTravelCost > 0m);
        Assert.Contains("maps/dir", plan.GoogleMapsNavigationUrl);
    }

    [Fact]
    public void OptimizeRouteOffline_SingleStore_NoReturn_SingleLeg()
    {
        var store = new Store
        {
            Id = Guid.NewGuid(),
            Name = "D1 Laureles",
            Latitude = 6.2440m,
            Longitude = -75.5910m
        };

        var plan = StoreRouteOptimizer.OptimizeRouteOffline(
            6.2425m, -75.5925m, [store], returnToOrigin: false);

        Assert.Single(plan.OrderedStores);
        Assert.Single(plan.Legs);
    }

    [Fact]
    public void OptimizeRouteOffline_ThreeStores_FindsExactMinimalPermutation()
    {
        // Origin: Laureles (6.2425, -75.5925)
        // Store 1: Nearby (6.2430, -75.5920) ~ 0.1 km
        // Store 2: Belén (6.2312, -75.5991) ~ 1.5 km
        // Store 3: Poblado (6.2088, -75.5678) ~ 5.0 km
        var s1 = new Store { Id = Guid.NewGuid(), Name = "Close", Latitude = 6.2430m, Longitude = -75.5920m };
        var s2 = new Store { Id = Guid.NewGuid(), Name = "Medium", Latitude = 6.2312m, Longitude = -75.5991m };
        var s3 = new Store { Id = Guid.NewGuid(), Name = "Far", Latitude = 6.2088m, Longitude = -75.5678m };

        // Pass them in worst order: Far, Close, Medium
        var plan = StoreRouteOptimizer.OptimizeRouteOffline(
            6.2425m, -75.5925m, [s3, s1, s2], returnToOrigin: false);

        // Optimal order should start with Close, then Medium, then Far
        Assert.Equal("Close", plan.OrderedStores[0].Name);
        Assert.Equal("Medium", plan.OrderedStores[1].Name);
        Assert.Equal("Far", plan.OrderedStores[2].Name);
    }

    [Fact]
    public void GenerateGoogleMapsUrl_ProducesZeroKeyNavigationUrl()
    {
        var originLat = 6.2425m;
        var originLon = -75.5925m;
        var stores = new List<Store>
        {
            new() { Id = Guid.NewGuid(), Name = "S1", Latitude = 6.2430m, Longitude = -75.5920m },
            new() { Id = Guid.NewGuid(), Name = "S2", Latitude = 6.2312m, Longitude = -75.5991m }
        };

        var url = StoreRouteOptimizer.GenerateGoogleMapsUrl(originLat, originLon, stores, returnToOrigin: true, TransportMode.Driving);

        Assert.StartsWith("https://www.google.com/maps/dir/?api=1", url);
        Assert.DoesNotContain("key=", url, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("origin=6.2425%2C-75.5925", url);
        Assert.Contains("destination=6.2425%2C-75.5925", url);
        Assert.Contains("travelmode=driving", url);
        Assert.Contains("waypoints=", url);
    }

    [Fact]
    public void FindClosestStore_ReturnsNearestStore()
    {
        var originLat = 6.2425m;
        var originLon = -75.5925m;

        var sClose = new Store { Id = Guid.NewGuid(), Name = "Close", Latitude = 6.2430m, Longitude = -75.5920m };
        var sFar = new Store { Id = Guid.NewGuid(), Name = "Far", Latitude = 6.2088m, Longitude = -75.5678m };

        var closest = StoreRouteOptimizer.FindClosestStore(originLat, originLon, [sFar, sClose]);

        Assert.NotNull(closest);
        Assert.Equal("Close", closest.Name);
    }
}
