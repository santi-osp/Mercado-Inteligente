using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Xunit;
using System.Net;
using System.Text;

namespace MercadoInteligente.Tests;

public class GoogleProvidersTests : IDisposable
{
    private readonly string _tempDir;
    private readonly LocalPaths _paths;
    private readonly FakeSecretStore _secrets;
    private readonly HttpClient _httpClient;
    private readonly GoogleServiceUsageTracker _tracker;

    public GoogleProvidersTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "mercado_google_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "data"));

        _paths = new LocalPaths(_tempDir);
        _secrets = new FakeSecretStore();
        _httpClient = new HttpClient();
        _tracker = new GoogleServiceUsageTracker(_paths);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task RoutingProvider_WithKey_UsesRoutesV2PostAndFieldMask()
    {
        _secrets.Set("google", "test-key");
        using var client = new HttpClient(new RouteHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://routes.googleapis.com/directions/v2:computeRoutes", request.RequestUri!.ToString());
            Assert.Equal("test-key", request.Headers.GetValues("X-Goog-Api-Key").Single());
            Assert.Equal("routes.distanceMeters,routes.duration", request.Headers.GetValues("X-Goog-FieldMask").Single());
            var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            Assert.Contains("\"travelMode\":\"WALK\"", body);
            Assert.DoesNotContain("test-key", body);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"routes\":[{\"distanceMeters\":2500,\"duration\":\"900s\"}]}", Encoding.UTF8, "application/json")
            };
        }));
        var provider = new GoogleRoutingProvider(_secrets, client, _paths, _tracker);
        var route = await provider.GetRouteAsync(6.2m, -75.5m, 6.3m, -75.6m, TransportMode.Walking, CancellationToken.None);
        Assert.Equal(RouteType.RoadRoute, route.Type);
        Assert.Equal(2.5m, route.DistanceKm);
        Assert.Equal(15m, route.DurationMinutes);
        Assert.Equal("HEALTHY", provider.LastProviderStatus);
    }

    [Fact]
    public async Task ProviderHealth_PersistsIndependentOutcomesAcrossInstances()
    {
        var health = new GoogleProviderHealth(_paths);
        _secrets.Set("google", "test-key");
        using var routeClient = new HttpClient(new RouteHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)));
        var routing = new GoogleRoutingProvider(_secrets, routeClient, _paths, _tracker, health);
        await routing.GetRouteAsync(6.2m, -75.5m, 6.3m, -75.6m, TransportMode.Driving, CancellationToken.None);

        Assert.Equal("AUTH_ERROR", new GoogleProviderHealth(_paths).Read().Routes);
        Assert.Equal("CONFIGURED_UNVERIFIED", health.Read().Geocoding);

        using var geocodeClient = new HttpClient(new RouteHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"status\":\"OK\",\"results\":[{\"formatted_address\":\"Medellín\",\"geometry\":{\"location\":{\"lat\":6.25,\"lng\":-75.57}}}]}", Encoding.UTF8, "application/json")
        }));
        var geocoding = new GoogleGeocodingProvider(_secrets, geocodeClient, _paths, _tracker, health);
        await geocoding.GeocodeDetailedAsync("Calle 10 Medellín", CancellationToken.None);
        var restarted = new GoogleProviderHealth(_paths).Read();
        Assert.Equal("HEALTHY", restarted.Geocoding);
        Assert.Equal("AUTH_ERROR", restarted.Routes);
        health.Reset();
        Assert.Equal("CONFIGURED_UNVERIFIED", new GoogleProviderHealth(_paths).Read().Geocoding);
        Assert.Equal("CONFIGURED_UNVERIFIED", new GoogleProviderHealth(_paths).Read().Routes);
    }

    [Fact]
    public async Task MalformedGooglePayload_DoesNotCrashOrClaimHealthy()
    {
        _secrets.Set("google", "test-key");
        using var client = new HttpClient(new RouteHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{invalid", Encoding.UTF8, "application/json")
        }));
        var health = new GoogleProviderHealth(_paths);
        var geocoding = new GoogleGeocodingProvider(_secrets, client, _paths, _tracker, health);
        var routing = new GoogleRoutingProvider(_secrets, client, _paths, _tracker, health);

        Assert.Null(await geocoding.GeocodeDetailedAsync("Calle 10 Medellín", CancellationToken.None));
        var route = await routing.GetRouteAsync(6.2m, -75.5m, 6.3m, -75.6m, TransportMode.Driving, CancellationToken.None);
        Assert.Equal(RouteType.StraightLineEstimate, route.Type);
        Assert.Equal("PROVIDER_ERROR", health.Read().Geocoding);
        Assert.Equal("PROVIDER_ERROR", health.Read().Routes);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "AUTH_ERROR")]
    [InlineData(HttpStatusCode.TooManyRequests, "QUOTA_EXCEEDED")]
    [InlineData(HttpStatusCode.InternalServerError, "PROVIDER_ERROR")]
    public async Task RoutingProvider_ProviderFailure_IsClassifiedAndEstimated(HttpStatusCode code, string expected)
    {
        _secrets.Set("google", "test-key");
        using var client = new HttpClient(new RouteHandler(_ => new HttpResponseMessage(code)));
        var provider = new GoogleRoutingProvider(_secrets, client, _paths, _tracker);
        var route = await provider.GetRouteAsync(6.2m, -75.5m, 6.3m, -75.6m, TransportMode.Driving, CancellationToken.None);
        Assert.Equal(RouteType.StraightLineEstimate, route.Type);
        Assert.Equal(expected, provider.LastProviderStatus);
    }

    private sealed class RouteHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    [Fact]
    public void UsageTracker_RecordsAndPersistsIncrements()
    {
        _tracker.RecordGeocode();
        _tracker.RecordGeocode();
        _tracker.RecordRoute();

        var recent = _tracker.GetRecentUsage(1);
        Assert.Single(recent);
        Assert.Equal(2, recent[0].GeocodeCount);
        Assert.Equal(1, recent[0].RouteCount);

        // Verify loaded from fresh tracker instance reading same file
        var freshTracker = new GoogleServiceUsageTracker(_paths);
        var loaded = freshTracker.GetRecentUsage(1);
        Assert.Single(loaded);
        Assert.Equal(2, loaded[0].GeocodeCount);
        Assert.Equal(1, loaded[0].RouteCount);
    }

    [Fact]
    public async Task GeocodingProvider_WithoutKey_ReturnsUnverifiedMockWithoutCaching()
    {
        var provider = new GoogleGeocodingProvider(_secrets, _httpClient, _paths, _tracker);
        Assert.False(provider.IsConfigured);

        var result = await provider.GeocodeDetailedAsync("Laureles Circular 1", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("MOCK_OFFLINE", result.ProviderStatus);
        Assert.Equal(6.2425m, result.Latitude);
        Assert.Equal(-75.5925m, result.Longitude);

        var cachedResult = await provider.GeocodeDetailedAsync("Laureles Circular 1", CancellationToken.None);
        Assert.NotNull(cachedResult);
        Assert.Equal("MOCK_OFFLINE", cachedResult.ProviderStatus);
        Assert.Equal(6.2425m, cachedResult.Latitude);
        Assert.Equal(-75.5925m, cachedResult.Longitude);
    }

    [Fact]
    public async Task LegacyUnverifiedCacheIsReplacedByLiveGeocodeAndThenReusedAsVerified()
    {
        _secrets.Set("google", "test-key");
        var cache = Path.Combine(_tempDir, "data", "geocoding-cache.json");
        await File.WriteAllTextAsync(cache,
            "[{\"NormalizedAddress\":\"laureles circular 1\",\"Latitude\":6.2425,\"Longitude\":-75.5925,\"FormattedAddress\":\"Mock\",\"CachedAtUtc\":\"2026-10-08T00:00:00Z\"}]");
        var calls = 0;
        using var client = new HttpClient(new RouteHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"status\":\"OK\",\"results\":[{\"formatted_address\":\"Verified\",\"geometry\":{\"location\":{\"lat\":6.24,\"lng\":-75.59}}}]}",
                    Encoding.UTF8, "application/json")
            };
        }));
        var provider = new GoogleGeocodingProvider(_secrets, client, _paths, _tracker);
        var first = await provider.GeocodeDetailedAsync("Laureles Circular 1", CancellationToken.None);
        var second = await provider.GeocodeDetailedAsync("Laureles Circular 1", CancellationToken.None);
        Assert.Equal("LIVE_GOOGLE", first!.ProviderStatus);
        Assert.Equal("CACHED_VERIFIED", second!.ProviderStatus);
        Assert.Equal("Verified", second.FormattedAddress);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RoutingProvider_WithoutKey_ReturnsStraightLineEstimate()
    {
        var provider = new GoogleRoutingProvider(_secrets, _httpClient, _paths, _tracker);
        Assert.False(provider.IsConfigured);

        // Origin Laureles to Poblado
        var route = await provider.GetRouteAsync(6.2425m, -75.5925m, 6.2088m, -75.5678m, TransportMode.Driving, CancellationToken.None);

        Assert.Equal(RouteType.StraightLineEstimate, route.Type);
        Assert.True(route.DistanceKm > 0m);
        Assert.True(route.DurationMinutes > 0m);
        Assert.Equal("HaversineUrbanWinding", route.Provider);
    }

    [Fact]
    public async Task RoutingProvider_ZeroDistance_ReturnsZeroImmediately()
    {
        var provider = new GoogleRoutingProvider(_secrets, _httpClient, _paths, _tracker);
        var route = await provider.GetRouteAsync(6.2425m, -75.5925m, 6.2425m, -75.5925m, TransportMode.Driving, CancellationToken.None);

        Assert.Equal(0m, route.DistanceKm);
        Assert.Equal(0, route.DistanceMeters);
        Assert.Equal(0m, route.DurationMinutes);
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string> _store = new();
        public void Delete(string name) => _store.Remove(name);
        public string? Get(string name) => _store.TryGetValue(name, out var val) ? val : null;
        public void Set(string name, string value) => _store[name] = value;
    }
}
