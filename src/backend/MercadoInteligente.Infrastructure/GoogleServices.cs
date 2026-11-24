using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure;

public sealed class GoogleServiceUsageTracker
{
    private readonly string _storagePath;
    private readonly object _lock = new();

    public record DailyUsage(string Date, int GeocodeCount, int RouteCount);

    public GoogleServiceUsageTracker(LocalPaths paths)
    {
        _storagePath = Path.Combine(paths.Root, "data", "google-usage.json");
    }

    public void RecordGeocode() => Increment(isGeocode: true);
    public void RecordRoute() => Increment(isGeocode: false);

    private void Increment(bool isGeocode)
    {
        lock (_lock)
        {
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var usages = LoadUsages();
            var current = usages.FirstOrDefault(u => u.Date == today) ?? new DailyUsage(today, 0, 0);
            usages.RemoveAll(u => u.Date == today);

            var updated = isGeocode
                ? current with { GeocodeCount = current.GeocodeCount + 1 }
                : current with { RouteCount = current.RouteCount + 1 };

            usages.Add(updated);
            SaveUsages(usages);
        }
    }

    public IReadOnlyList<DailyUsage> GetRecentUsage(int days = 30)
    {
        lock (_lock)
        {
            return LoadUsages().OrderByDescending(u => u.Date).Take(days).ToList();
        }
    }

    private List<DailyUsage> LoadUsages()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                var json = File.ReadAllText(_storagePath);
                return JsonSerializer.Deserialize<List<DailyUsage>>(json) ?? [];
            }
        }
        catch { }
        return [];
    }

    private void SaveUsages(List<DailyUsage> list)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
            File.WriteAllText(_storagePath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

public sealed class GoogleProviderHealth(LocalPaths paths)
{
    private readonly string _filePath = Path.Combine(paths.Root, "data", "google-health.json");
    private readonly object _gate = new();
    private sealed record State(string Geocoding, string Routes, DateTime CheckedAtUtc);

    public (string Geocoding, string Routes) Read()
    {
        lock (_gate)
        {
            try
            {
                return File.Exists(_filePath)
                    ? JsonSerializer.Deserialize<State>(File.ReadAllText(_filePath)) is { } state &&
                      DateTime.UtcNow - state.CheckedAtUtc < TimeSpan.FromHours(24)
                        ? (state.Geocoding, state.Routes) : ("CONFIGURED_UNVERIFIED", "CONFIGURED_UNVERIFIED")
                    : ("CONFIGURED_UNVERIFIED", "CONFIGURED_UNVERIFIED");
            }
            catch { return ("CONFIGURED_UNVERIFIED", "CONFIGURED_UNVERIFIED"); }
        }
    }

    public void RecordGeocoding(string status) => Record(status, null);
    public void RecordRoutes(string status) => Record(null, status);
    public void Reset() => Record("CONFIGURED_UNVERIFIED", "CONFIGURED_UNVERIFIED");

    private void Record(string? geocoding, string? routes)
    {
        lock (_gate)
        {
            try
            {
                var previous = Read();
                Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
                var temporary = _filePath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(new State(
                    geocoding ?? previous.Geocoding, routes ?? previous.Routes, DateTime.UtcNow)));
                File.Move(temporary, _filePath, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}

public sealed class GoogleGeocodingProvider : IGeocodingProvider
{
    public string LastProviderStatus { get; private set; } = "NOT_CONFIGURED";
    private readonly ISecretStore _secretStore;
    private readonly HttpClient _httpClient;
    private readonly GoogleServiceUsageTracker _tracker;
    private readonly GoogleProviderHealth? _health;
    private readonly string _cachePath;
    private readonly object _cacheLock = new();

    private sealed record CachedGeocodeEntry(
        string NormalizedAddress,
        decimal Latitude,
        decimal Longitude,
        string FormattedAddress,
        DateTime CachedAtUtc,
        string? Source = null,
        AddressComponents? Components = null
    );

    public bool IsConfigured
    {
        get
        {
            var key = _secretStore.Get("google");
            return !string.IsNullOrWhiteSpace(key);
        }
    }

    public GoogleGeocodingProvider(
        ISecretStore secretStore,
        HttpClient httpClient,
        LocalPaths paths,
        GoogleServiceUsageTracker tracker,
        GoogleProviderHealth? health = null)
    {
        _secretStore = secretStore;
        _httpClient = httpClient;
        _tracker = tracker;
        _health = health;
        _cachePath = Path.Combine(paths.Root, "data", "geocoding-cache.json");
    }

    public async Task<(decimal Latitude, decimal Longitude)?> GeocodeAsync(string address, CancellationToken cancellationToken)
    {
        var result = await GeocodeDetailedAsync(address, cancellationToken);
        if (result != null)
        {
            return (result.Latitude, result.Longitude);
        }
        return null;
    }

    public async Task<GeocodingResult?> ReverseGeocodeAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken)
    {
        var normalized = $"{latitude:F5},{longitude:F5}";
        var cached = GetFromCache(normalized);
        if (cached is { Source: "LIVE_GOOGLE" or "CACHED_VERIFIED" })
        {
            return new GeocodingResult(cached.Latitude, cached.Longitude, cached.FormattedAddress, "CACHED_VERIFIED", cached.Components);
        }

        var apiKey = _secretStore.Get("google");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LastProviderStatus = "NOT_CONFIGURED";
            _health?.RecordGeocoding(LastProviderStatus);
            var mock = GetMedellinMockGeocode(normalized);
            if (mock != null) return mock with { ProviderStatus = "MOCK_OFFLINE" };
            return null;
        }

        try
        {
            _tracker.RecordGeocode();
            var uri = $"https://maps.googleapis.com/maps/api/geocode/json?latlng={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&region=co";
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-Goog-Api-Key", apiKey);
            using var httpResponse = await _httpClient.SendAsync(request, cancellationToken);
            if (!httpResponse.IsSuccessStatusCode)
            {
                LastProviderStatus = httpResponse.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "AUTH_ERROR",
                    System.Net.HttpStatusCode.TooManyRequests => "QUOTA_EXCEEDED",
                    _ => "PROVIDER_ERROR"
                };
                _health?.RecordGeocoding(LastProviderStatus);
                return null;
            }

            var response = await httpResponse.Content.ReadFromJsonAsync<GoogleGeocodeApiResponse>(cancellationToken);
            if (response != null && response.Status == "OK" && response.Results.Count > 0)
            {
                var first = response.Results[0];
                var lat = Math.Round((decimal)first.Geometry.Location.Lat, 5);
                var lng = Math.Round((decimal)first.Geometry.Location.Lng, 5);
                var formatted = first.FormattedAddress;
                var components = ParseAddressComponents(first.AddressComponents);

                SaveToCache(normalized, lat, lng, formatted, components);
                LastProviderStatus = "HEALTHY";
                _health?.RecordGeocoding(LastProviderStatus);
                return new GeocodingResult(lat, lng, formatted, "LIVE_GOOGLE", components);
            }

            LastProviderStatus = response?.Status switch
            {
                "REQUEST_DENIED" => "AUTH_ERROR",
                "OVER_QUERY_LIMIT" => "QUOTA_EXCEEDED",
                _ => "PROVIDER_ERROR"
            };
            _health?.RecordGeocoding(LastProviderStatus);
            return null;
        }
        catch (HttpRequestException)
        {
            LastProviderStatus = "NETWORK_ERROR";
            _health?.RecordGeocoding(LastProviderStatus);
            var mock = GetMedellinMockGeocode(normalized);
            if (mock != null) return mock with { ProviderStatus = "OFFLINE_FALLBACK" };
            return null;
        }
        catch (Exception)
        {
            LastProviderStatus = "PROVIDER_ERROR";
            _health?.RecordGeocoding(LastProviderStatus);
            return null;
        }
    }

    public async Task<GeocodingResult?> GeocodeDetailedAsync(string address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;

        var normalized = LocationInputParser.NormalizeAddress(address);

        // 1. Check persistent cache
        var cached = GetFromCache(normalized);
        if (cached is { Source: "LIVE_GOOGLE" })
        {
            return new GeocodingResult(
                cached.Latitude,
                cached.Longitude,
                cached.FormattedAddress,
                "CACHED_VERIFIED",
                cached.Components
            );
        }

        // 2. Check if API key is configured
        var apiKey = _secretStore.Get("google");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LastProviderStatus = "NOT_CONFIGURED";
            _health?.RecordGeocoding(LastProviderStatus);
            // Offline mock lookup for common Medellín reference points
            var mock = GetMedellinMockGeocode(normalized);
            if (mock != null)
            {
                return mock with { ProviderStatus = "MOCK_OFFLINE" };
            }

            return null;
        }

        // 3. Query Google Geocoding API
        try
        {
            _tracker.RecordGeocode();
            var uri = $"https://maps.googleapis.com/maps/api/geocode/json?address={Uri.EscapeDataString(normalized)}&region=co";
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("X-Goog-Api-Key", apiKey);
            using var httpResponse = await _httpClient.SendAsync(request, cancellationToken);
            if (!httpResponse.IsSuccessStatusCode)
            {
                LastProviderStatus = httpResponse.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "AUTH_ERROR",
                    System.Net.HttpStatusCode.TooManyRequests => "QUOTA_EXCEEDED",
                    _ => "PROVIDER_ERROR"
                };
                _health?.RecordGeocoding(LastProviderStatus);
                return null;
            }
            var response = await httpResponse.Content.ReadFromJsonAsync<GoogleGeocodeApiResponse>(cancellationToken);

            if (response != null && response.Status == "OK" && response.Results.Count > 0)
            {
                var first = response.Results[0];
                var lat = Math.Round((decimal)first.Geometry.Location.Lat, 5);
                var lng = Math.Round((decimal)first.Geometry.Location.Lng, 5);
                var formatted = first.FormattedAddress;
                var components = ParseAddressComponents(first.AddressComponents);

                SaveToCache(normalized, lat, lng, formatted, components);
                LastProviderStatus = "HEALTHY";
                _health?.RecordGeocoding(LastProviderStatus);
                return new GeocodingResult(lat, lng, formatted, "LIVE_GOOGLE", components);
            }
            LastProviderStatus = response?.Status switch
            {
                "REQUEST_DENIED" => "AUTH_ERROR",
                "OVER_QUERY_LIMIT" => "QUOTA_EXCEEDED",
                _ => "PROVIDER_ERROR"
            };
            _health?.RecordGeocoding(LastProviderStatus);
            return null;
        }
        catch (HttpRequestException)
        {
            LastProviderStatus = "NETWORK_ERROR";
            _health?.RecordGeocoding(LastProviderStatus);
            var mock = GetMedellinMockGeocode(normalized);
            if (mock != null) return mock with { ProviderStatus = "OFFLINE_FALLBACK" };
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LastProviderStatus = "NETWORK_ERROR";
            _health?.RecordGeocoding(LastProviderStatus);
            // Fallback to offline mock on network / timeout errors
            var mock = GetMedellinMockGeocode(normalized);
            if (mock != null)
            {
                return mock with { ProviderStatus = "OFFLINE_FALLBACK" };
            }
            return null;
        }
        catch (JsonException)
        {
            LastProviderStatus = "PROVIDER_ERROR";
            _health?.RecordGeocoding(LastProviderStatus);
            return null;
        }
    }

    private CachedGeocodeEntry? GetFromCache(string normalized)
    {
        lock (_cacheLock)
        {
            var cache = LoadCache();
            return cache.FirstOrDefault(c => string.Equals(c.NormalizedAddress, normalized, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void SaveToCache(string normalized, decimal lat, decimal lng, string formatted, AddressComponents? components = null)
    {
        lock (_cacheLock)
        {
            var cache = LoadCache();
            cache.RemoveAll(c => string.Equals(c.NormalizedAddress, normalized, StringComparison.OrdinalIgnoreCase));
            cache.Add(new CachedGeocodeEntry(normalized, lat, lng, formatted, DateTime.UtcNow, "LIVE_GOOGLE", components));
            SaveCache(cache);
        }
    }

    private List<CachedGeocodeEntry> LoadCache()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                var json = File.ReadAllText(_cachePath);
                return JsonSerializer.Deserialize<List<CachedGeocodeEntry>>(json) ?? [];
            }
        }
        catch { }
        return [];
    }

    private void SaveCache(List<CachedGeocodeEntry> cache)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static GeocodingResult? GetMedellinMockGeocode(string normalized)
    {
        var lower = normalized.ToLowerInvariant();
        if (lower.Contains("poblado") || lower.Contains("carrera 43a"))
            return new GeocodingResult(6.2088m, -75.5678m, "El Poblado, Medellín, Antioquia, Colombia", "MOCK_OFFLINE",
                new AddressComponents("Colombia", "CO", "Antioquia", "ANT", "Medellín", "050021", "El Poblado", "Carrera 43A", "1-50"));
        if (lower.Contains("laureles") || lower.Contains("circular 1") || lower.Contains("transversal 39"))
            return new GeocodingResult(6.2425m, -75.5925m, "Laureles, Medellín, Antioquia, Colombia", "MOCK_OFFLINE",
                new AddressComponents("Colombia", "CO", "Antioquia", "ANT", "Medellín", "050031", "Laureles", "Circular 1", "70-10"));
        if (lower.Contains("belen") || lower.Contains("calle 30"))
            return new GeocodingResult(6.2312m, -75.5991m, "Belén, Medellín, Antioquia, Colombia", "MOCK_OFFLINE",
                new AddressComponents("Colombia", "CO", "Antioquia", "ANT", "Medellín", "050025", "Belén", "Calle 30", "75-20"));
        if (lower.Contains("centro") || lower.Contains("calle 50") || lower.Contains("colombia"))
            return new GeocodingResult(6.2518m, -75.5636m, "Centro, Medellín, Antioquia, Colombia", "MOCK_OFFLINE",
                new AddressComponents("Colombia", "CO", "Antioquia", "ANT", "Medellín", "050015", "La Candelaria", "Calle 50", "50-10"));
        if (lower.Contains("envigado"))
            return new GeocodingResult(6.1685m, -75.5861m, "Envigado, Antioquia, Colombia", "MOCK_OFFLINE",
                new AddressComponents("Colombia", "CO", "Antioquia", "ANT", "Envigado", "055422", "Zona Centro", "Carrera 43", "38 Sur"));
        if (lower.Contains("itagui"))
            return new GeocodingResult(6.1725m, -75.6095m, "Itagüí, Antioquia, Colombia", "MOCK_OFFLINE",
                new AddressComponents("Colombia", "CO", "Antioquia", "ANT", "Itagüí", "055412", "Centro", "Calle 50", "50-20"));
        if (lower.Contains("bello"))
            return new GeocodingResult(6.3333m, -75.5583m, "Bello, Antioquia, Colombia", "MOCK_OFFLINE",
                new AddressComponents("Colombia", "CO", "Antioquia", "ANT", "Bello", "051052", "Centro", "Calle 50", "50-20"));
        if (lower.Contains("sabaneta"))
            return new GeocodingResult(6.1517m, -75.6156m, "Sabaneta, Antioquia, Colombia", "MOCK_OFFLINE",
                new AddressComponents("Colombia", "CO", "Antioquia", "ANT", "Sabaneta", "055450", "Centro", "Calle 75 Sur", "45-10"));
        if (lower.Contains("bogota") || lower.Contains("chapinero") || lower.Contains("usaquen"))
            return new GeocodingResult(4.6097m, -74.0817m, "Bogotá D.C., Cundinamarca, Colombia", "MOCK_OFFLINE",
                new AddressComponents("Colombia", "CO", "Bogotá D.C.", "BOG", "Bogotá", "110111", "Chapinero", "Carrera 7", "72-01"));

        // Generic Medellín central reference point
        return new GeocodingResult(6.2442m, -75.5812m, "Medellín, Antioquia, Colombia", "MOCK_OFFLINE",
            new AddressComponents("Colombia", "CO", "Antioquia", "ANT", "Medellín", "050001", "La Candelaria", "Calle 50", "50-01"));
    }

    public static AddressComponents ParseAddressComponents(List<GoogleAddressComponent> components)
    {
        if (components == null || components.Count == 0) return new AddressComponents();

        string? country = null;
        string? countryCode = null;
        string? state = null;
        string? stateCode = null;
        string? city = null;
        string? postalCode = null;
        string? neighborhood = null;
        string? street = null;
        string? streetNumber = null;

        foreach (var comp in components)
        {
            if (comp.Types.Contains("country"))
            {
                country = comp.LongName;
                countryCode = comp.ShortName;
            }
            else if (comp.Types.Contains("administrative_area_level_1"))
            {
                state = comp.LongName;
                stateCode = comp.ShortName;
            }
            else if (comp.Types.Contains("locality"))
            {
                city ??= comp.LongName;
            }
            else if (comp.Types.Contains("administrative_area_level_2") && city == null)
            {
                city = comp.LongName;
            }
            else if (comp.Types.Contains("postal_code"))
            {
                postalCode = comp.LongName;
            }
            else if (comp.Types.Contains("neighborhood") || comp.Types.Contains("sublocality") || comp.Types.Contains("sublocality_level_1"))
            {
                neighborhood ??= comp.LongName;
            }
            else if (comp.Types.Contains("route"))
            {
                street = comp.LongName;
            }
            else if (comp.Types.Contains("street_number"))
            {
                streetNumber = comp.LongName;
            }
        }

        return new AddressComponents(
            Country: country,
            CountryCode: countryCode,
            State: state,
            StateCode: stateCode,
            City: city,
            PostalCode: postalCode,
            Neighborhood: neighborhood,
            Street: street,
            StreetNumber: streetNumber
        );
    }

    private sealed class GoogleGeocodeApiResponse
    {
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("results")] public List<GoogleGeocodeResult> Results { get; set; } = [];
    }

    private sealed class GoogleGeocodeResult
    {
        [JsonPropertyName("formatted_address")] public string FormattedAddress { get; set; } = "";
        [JsonPropertyName("geometry")] public GoogleGeometry Geometry { get; set; } = new();
        [JsonPropertyName("address_components")] public List<GoogleAddressComponent> AddressComponents { get; set; } = [];
    }

    public sealed class GoogleAddressComponent
    {
        [JsonPropertyName("long_name")] public string LongName { get; set; } = "";
        [JsonPropertyName("short_name")] public string ShortName { get; set; } = "";
        [JsonPropertyName("types")] public List<string> Types { get; set; } = [];
    }

    private sealed class GoogleGeometry
    {
        [JsonPropertyName("location")] public GoogleLatLng Location { get; set; } = new();
    }

    private sealed class GoogleLatLng
    {
        [JsonPropertyName("lat")] public double Lat { get; set; }
        [JsonPropertyName("lng")] public double Lng { get; set; }
    }
}

public sealed class GoogleRoutingProvider : IRoutingProvider
{
    private readonly ISecretStore _secretStore;
    private readonly HttpClient _httpClient;
    private readonly GoogleServiceUsageTracker _tracker;
    private readonly GoogleProviderHealth? _health;
    private readonly string _cachePath;
    private readonly object _cacheLock = new();
    public static readonly TimeSpan RouteTtl = TimeSpan.FromHours(24);
    public string LastProviderStatus { get; private set; } = "NOT_CONFIGURED";

    private sealed record CachedRouteEntry(
        string RouteKey,
        decimal DistanceKm,
        int DistanceMeters,
        decimal DurationMinutes,
        DateTime CachedAtUtc,
        bool RoutingFallback = false,
        string? RoutingModeRequested = null,
        string? RoutingModeUsed = null,
        string? FallbackMessage = null,
        string DistanceSource = "GOOGLE_ROUTES",
        bool IsApproximate = false
    );

    public bool IsConfigured
    {
        get
        {
            var key = _secretStore.Get("google");
            return !string.IsNullOrWhiteSpace(key);
        }
    }

    public GoogleRoutingProvider(
        ISecretStore secretStore,
        HttpClient httpClient,
        LocalPaths paths,
        GoogleServiceUsageTracker tracker,
        GoogleProviderHealth? health = null)
    {
        _secretStore = secretStore;
        _httpClient = httpClient;
        _tracker = tracker;
        _health = health;
        _cachePath = Path.Combine(paths.Root, "data", "routing-cache.json");
    }

    public async Task<decimal?> DistanceKmAsync(
        decimal originLatitude,
        decimal originLongitude,
        decimal destinationLatitude,
        decimal destinationLongitude,
        CancellationToken cancellationToken)
    {
        var route = await GetRouteAsync(originLatitude, originLongitude, destinationLatitude, destinationLongitude, TransportMode.Driving, cancellationToken);
        return route.DistanceKm;
    }

    public async Task<RouteInfo> GetRouteAsync(
        decimal originLatitude,
        decimal originLongitude,
        decimal destinationLatitude,
        decimal destinationLongitude,
        TransportMode mode,
        CancellationToken cancellationToken)
    {
        if (originLatitude == destinationLatitude && originLongitude == destinationLongitude)
        {
            return new RouteInfo(0m, 0, 0m, RouteType.RoadRoute, "ZeroDistance",
                RoutingFallback: false,
                RoutingModeRequested: mode.ToString().ToUpperInvariant(),
                RoutingModeUsed: mode.ToString().ToUpperInvariant());
        }

        var key = $"{originLatitude:F4},{originLongitude:F4}->{destinationLatitude:F4},{destinationLongitude:F4}_{mode}";

        // 1. Check 24-hour persistent cache
        var cached = GetFromCache(key);
        if (cached != null)
        {
            return new RouteInfo(
                cached.DistanceKm,
                cached.DistanceMeters,
                cached.DurationMinutes,
                RouteType.CachedRoute,
                "Cache(24h)",
                cached.RoutingFallback,
                cached.RoutingModeRequested,
                cached.RoutingModeUsed,
                cached.FallbackMessage,
                cached.DistanceSource,
                cached.IsApproximate
            );
        }

        // 2. Check if API key is configured
        var apiKey = _secretStore.Get("google");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LastProviderStatus = "NOT_CONFIGURED";
            _health?.RecordRoutes(LastProviderStatus);
            // Offline estimation using Haversine with winding factor
            return CalculateOfflineEstimate(originLatitude, originLongitude, destinationLatitude, destinationLongitude, mode);
        }

        // 3. Query Google Routes API v2
        try
        {
            _tracker.RecordRoute();
            if (mode == TransportMode.Motorcycle)
            {
                // Try TWO_WHEELER first
                var twoWheeler = await QueryRoutesV2Async(apiKey, originLatitude, originLongitude, destinationLatitude, destinationLongitude, "TWO_WHEELER", cancellationToken);
                if (twoWheeler != null)
                {
                    SaveToCache(key, twoWheeler.DistanceKm, twoWheeler.DistanceMeters, twoWheeler.DurationMinutes,
                        false, "MOTORCYCLE", "TWO_WHEELER", null);
                    LastProviderStatus = "HEALTHY";
                    _health?.RecordRoutes(LastProviderStatus);
                    return twoWheeler with
                    {
                        RoutingFallback = false,
                        RoutingModeRequested = "MOTORCYCLE",
                        RoutingModeUsed = "TWO_WHEELER",
                        FallbackMessage = null
                    };
                }

                // If TWO_WHEELER fails or unsupported, explicit fallback to DRIVE
                var driveFallback = await QueryRoutesV2Async(apiKey, originLatitude, originLongitude, destinationLatitude, destinationLongitude, "DRIVE", cancellationToken);
                if (driveFallback != null)
                {
                    const string fallbackMsg = "Ruta aproximada usando modo automóvil.";
                    SaveToCache(key, driveFallback.DistanceKm, driveFallback.DistanceMeters, driveFallback.DurationMinutes,
                        true, "MOTORCYCLE", "DRIVE", fallbackMsg);
                    LastProviderStatus = "HEALTHY";
                    _health?.RecordRoutes(LastProviderStatus);
                    return driveFallback with
                    {
                        RoutingFallback = true,
                        RoutingModeRequested = "MOTORCYCLE",
                        RoutingModeUsed = "DRIVE",
                        FallbackMessage = fallbackMsg
                    };
                }

                return CalculateOfflineEstimate(originLatitude, originLongitude, destinationLatitude, destinationLongitude, mode);
            }

            var googleMode = mode switch
            {
                TransportMode.Walking => "WALK",
                TransportMode.Bicycling => "BICYCLE",
                _ => "DRIVE"
            };

            var route = await QueryRoutesV2Async(apiKey, originLatitude, originLongitude, destinationLatitude, destinationLongitude, googleMode, cancellationToken);
            if (route != null)
            {
                SaveToCache(key, route.DistanceKm, route.DistanceMeters, route.DurationMinutes,
                    false, mode.ToString().ToUpperInvariant(), googleMode, null);
                LastProviderStatus = "HEALTHY";
                _health?.RecordRoutes(LastProviderStatus);
                return route with
                {
                    RoutingFallback = false,
                    RoutingModeRequested = mode.ToString().ToUpperInvariant(),
                    RoutingModeUsed = googleMode,
                    FallbackMessage = null
                };
            }

            return CalculateOfflineEstimate(originLatitude, originLongitude, destinationLatitude, destinationLongitude, mode);
        }
        catch (HttpRequestException)
        {
            LastProviderStatus = "NETWORK_ERROR";
            _health?.RecordRoutes(LastProviderStatus);
            return CalculateOfflineEstimate(originLatitude, originLongitude, destinationLatitude, destinationLongitude, mode);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LastProviderStatus = "NETWORK_ERROR";
            _health?.RecordRoutes(LastProviderStatus);
            return CalculateOfflineEstimate(originLatitude, originLongitude, destinationLatitude, destinationLongitude, mode);
        }
        catch (JsonException)
        {
            LastProviderStatus = "PROVIDER_ERROR";
            _health?.RecordRoutes(LastProviderStatus);
            return CalculateOfflineEstimate(originLatitude, originLongitude, destinationLatitude, destinationLongitude, mode);
        }
    }

    private async Task<RouteInfo?> QueryRoutesV2Async(
        string apiKey,
        decimal originLatitude,
        decimal originLongitude,
        decimal destinationLatitude,
        decimal destinationLongitude,
        string travelMode,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://routes.googleapis.com/directions/v2:computeRoutes");
        request.Headers.Add("X-Goog-Api-Key", apiKey);
        request.Headers.Add("X-Goog-FieldMask", "routes.distanceMeters,routes.duration");
        request.Content = JsonContent.Create(new
        {
            origin = new { location = new { latLng = new { latitude = originLatitude, longitude = originLongitude } } },
            destination = new { location = new { latLng = new { latitude = destinationLatitude, longitude = destinationLongitude } } },
            travelMode
        });

        using var httpResponse = await _httpClient.SendAsync(request, cancellationToken);
        if (!httpResponse.IsSuccessStatusCode)
        {
            LastProviderStatus = httpResponse.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden => "AUTH_ERROR",
                System.Net.HttpStatusCode.TooManyRequests => "QUOTA_EXCEEDED",
                _ => "PROVIDER_ERROR"
            };
            _health?.RecordRoutes(LastProviderStatus);
            return null;
        }

        var response = await httpResponse.Content.ReadFromJsonAsync<GoogleRoutesApiResponse>(cancellationToken);
        if (response?.Routes.FirstOrDefault() is { DistanceMeters: > 0 } route &&
            decimal.TryParse(route.Duration.TrimEnd('s'), System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds))
        {
            var distanceMeters = route.DistanceMeters;
            var distanceKm = Math.Round((decimal)distanceMeters / 1000m, 3);
            var durationMinutes = Math.Round(seconds / 60m, 1);
            return new RouteInfo(distanceKm, distanceMeters, durationMinutes, RouteType.RoadRoute, "GoogleRoutesV2",
                DistanceSource: "GOOGLE_ROUTES", IsApproximate: false);
        }

        return null;
    }

    private static RouteInfo CalculateOfflineEstimate(decimal lat1, decimal lon1, decimal lat2, decimal lon2, TransportMode mode)
    {
        var straightLineKm = HaversineService.CalculateDistanceKm(lat1, lon1, lat2, lon2);
        var roadKm = HaversineService.EstimateRoadDistanceKm(straightLineKm);
        var duration = HaversineService.EstimateDurationMinutes(roadKm, mode);
        var meters = (int)Math.Round(roadKm * 1000m);

        return new RouteInfo(
            DistanceKm: roadKm,
            DistanceMeters: meters,
            DurationMinutes: duration,
            Type: RouteType.StraightLineEstimate,
            Provider: "HaversineUrbanWinding",
            RoutingFallback: mode == TransportMode.Motorcycle,
            RoutingModeRequested: mode == TransportMode.Motorcycle ? "MOTORCYCLE" : mode.ToString().ToUpperInvariant(),
            RoutingModeUsed: mode == TransportMode.Motorcycle ? "DRIVE_ESTIMATE" : mode.ToString().ToUpperInvariant(),
            FallbackMessage: mode == TransportMode.Motorcycle ? "Ruta aproximada usando modo automóvil (offline)." : null,
            DistanceSource: "HAVERSINE",
            IsApproximate: true
        );
    }

    private CachedRouteEntry? GetFromCache(string routeKey)
    {
        lock (_cacheLock)
        {
            var cache = LoadCache();
            var entry = cache.FirstOrDefault(c => c.RouteKey == routeKey);
            if (entry != null)
            {
                if (DateTime.UtcNow - entry.CachedAtUtc <= RouteTtl)
                {
                    return entry;
                }
                cache.Remove(entry);
                SaveCache(cache);
            }
            return null;
        }
    }

    private void SaveToCache(string routeKey, decimal km, int meters, decimal duration,
        bool routingFallback = false, string? routingModeRequested = null, string? routingModeUsed = null, string? fallbackMessage = null,
        string distanceSource = "GOOGLE_ROUTES", bool isApproximate = false)
    {
        lock (_cacheLock)
        {
            var cache = LoadCache();
            cache.RemoveAll(c => c.RouteKey == routeKey);
            cache.Add(new CachedRouteEntry(routeKey, km, meters, duration, DateTime.UtcNow,
                routingFallback, routingModeRequested, routingModeUsed, fallbackMessage, distanceSource, isApproximate));
            SaveCache(cache);
        }
    }

    private List<CachedRouteEntry> LoadCache()
    {
        try
        {
            if (File.Exists(_cachePath))
            {
                var json = File.ReadAllText(_cachePath);
                return JsonSerializer.Deserialize<List<CachedRouteEntry>>(json) ?? [];
            }
        }
        catch { }
        return [];
    }

    private void SaveCache(List<CachedRouteEntry> cache)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
            File.WriteAllText(_cachePath, JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private sealed class GoogleRoutesApiResponse
    {
        [JsonPropertyName("routes")] public List<GoogleRoute> Routes { get; set; } = [];
    }

    private sealed class GoogleRoute
    {
        [JsonPropertyName("distanceMeters")] public int DistanceMeters { get; set; }
        [JsonPropertyName("duration")] public string Duration { get; set; } = "";
    }
}
