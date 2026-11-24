using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MercadoInteligente.Tests;

public sealed class LocationApiIntegrationTests
{
    [Fact]
    public async Task BasketSync_DoesNotTreatOfflineReferenceGeocodeAsLocalizedCommercialContext()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-context-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();
            var response = await client.PostAsync("/api/v1/sync/basket?locationInput=Laureles%20Circular%201", null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var context = result.GetProperty("shoppingContext");
            Assert.Equal("LOCATION_UNVERIFIED", context.GetProperty("validationStatus").GetString());
            Assert.Equal(JsonValueKind.Null, context.GetProperty("latitude").ValueKind);
            Assert.All(context.GetProperty("providers").EnumerateArray(), provider =>
                Assert.Equal("ONLINE_GENERIC", provider.GetProperty("status").GetString()));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SavingAddressWithoutGoogleDoesNotPersistMockCoordinates()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-profile-unverified-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();
            var response = await client.PostAsJsonAsync("/api/v1/location/profiles", new
            { label = "Sin geocodificar", address = "Laureles Circular 1" });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var profile = await response.Content.ReadFromJsonAsync<LocationProfile>();
            Assert.NotNull(profile);
            Assert.Null(profile.Latitude);
            Assert.Null(profile.Longitude);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ParseLocationEndpoint_ReturnsParsedCoordinates()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-loc-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/v1/location/parse", new { input = "6.2425, -75.5925" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var payload = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Success", payload.RootElement.GetProperty("status").GetString());
            Assert.Equal("Coordinates", payload.RootElement.GetProperty("source").GetString());
            Assert.Equal(6.2425m, payload.RootElement.GetProperty("latitude").GetDecimal());
            Assert.Equal(-75.5925m, payload.RootElement.GetProperty("longitude").GetDecimal());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task LocationProfilesCrud_PersistsAndDeletesCorrectly()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-loc-profiles-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            // 1. Create a profile
            var createRes = await client.PostAsJsonAsync("/api/v1/location/profiles", new
            {
                label = "Mi Casa",
                address = "Circular 1 # 70-15",
                latitude = 6.2425m,
                longitude = -75.5925m
            });
            Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
            var created = await createRes.Content.ReadFromJsonAsync<LocationProfile>();
            Assert.NotNull(created);
            Assert.Equal("Mi Casa", created.Label);

            // 2. List profiles
            var listRes = await client.GetAsync("/api/v1/location/profiles");
            Assert.Equal(HttpStatusCode.OK, listRes.StatusCode);
            var profiles = await listRes.Content.ReadFromJsonAsync<List<LocationProfile>>();
            Assert.NotNull(profiles);
            Assert.Contains(profiles, p => p.Label == "Mi Casa");

            // 3. Delete profile
            var deleteRes = await client.DeleteAsync($"/api/v1/location/profiles/{created.Id}");
            Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

            // 4. Verify deleted
            var afterListRes = await client.GetAsync("/api/v1/location/profiles");
            var afterProfiles = await afterListRes.Content.ReadFromJsonAsync<List<LocationProfile>>();
            Assert.NotNull(afterProfiles);
            Assert.DoesNotContain(afterProfiles, p => p.Id == created.Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task SeedAndGetStores_ReturnsStoresWithDistances()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-loc-stores-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            // Call seed endpoint
            var seedRes = await client.PostAsync("/api/v1/location/stores/seed", null);
            Assert.Equal(HttpStatusCode.OK, seedRes.StatusCode);

            // Query stores with origin in Laureles
            var storesRes = await client.GetAsync("/api/v1/location/stores?originLat=6.2425&originLon=-75.5925");
            Assert.Equal(HttpStatusCode.OK, storesRes.StatusCode);

            var stores = await storesRes.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(stores.GetArrayLength() > 0);

            var firstStore = stores[0];
            Assert.True(firstStore.TryGetProperty("distanceKm", out var distProp));
            Assert.True(distProp.GetDecimal() >= 0m);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task RouteOptimizeEndpoint_EmptyStores_ReturnsBadRequest()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-routes-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var req = new
            {
                originLatitude = 6.2425m,
                originLongitude = -75.5925m,
                storeIds = new List<Guid>(),
                returnToOrigin = true,
                transportMode = "Walking"
            };

            var res = await client.PostAsJsonAsync("/api/v1/routes/optimize", req);
            Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
            var error = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("EMPTY_STORES", error.GetProperty("code").GetString());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task UsageEndpoint_ReturnsStats()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-usage-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("MARKET_DATA_DIR", root));
            using var client = factory.CreateClient();

            var res = await client.GetAsync("/api/v1/location/usage");
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.TryGetProperty("geocodingConfigured", out _));
            Assert.True(json.TryGetProperty("routingConfigured", out _));
            Assert.True(json.TryGetProperty("todayGeocodeCalls", out _));
            Assert.True(json.TryGetProperty("todayRouteCalls", out _));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
