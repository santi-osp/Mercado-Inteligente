using System.Globalization;
using System.Text.RegularExpressions;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public static partial class LocationInputParser
{
    private static readonly Regex CoordinatesRegex = new(
        @"^\s*([+-]?\d+(?:\.\d+)?)\s*(?:°\s*)?([NS])?\s*[,;\s]\s*([+-]?\d+(?:\.\d+)?)\s*(?:°\s*)?([EW])?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GoogleMapsUrlRegex = new(
        @"https?://(?:www\.)?(?:google\.[a-z.]+/maps|maps\.google\.[a-z.]+(?:/maps|\?|/|$)).*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GoogleMapsShortUrlRegex = new(
        @"https?://(?:maps\.app\.goo\.gl|goo\.gl/maps)/[A-Za-z0-9_-]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AtCoordsRegex = new(
        @"@(-?\d+\.\d+),(-?\d+\.\d+)",
        RegexOptions.Compiled);

    private static readonly Regex QueryCoordsRegex = new(
        @"[?&](?:q|query|ll)=(-?\d+\.\d+),(-?\d+\.\d+)",
        RegexOptions.Compiled);

    private static readonly Regex DataCoordsRegex = new(
        @"!3d(-?\d+\.\d+)!4d(-?\d+\.\d+)",
        RegexOptions.Compiled);

    private static readonly Regex QueryAddressRegex = new(
        @"[?&](?:q|query)=([^&]+)",
        RegexOptions.Compiled);

    public static LocationParseResult Parse(string? input, IReadOnlyList<LocationProfile>? savedProfiles = null)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new LocationParseResult(
                OriginalInput: input ?? "",
                NormalizedAddress: null,
                Latitude: null,
                Longitude: null,
                Source: LocationInputSource.Address,
                Status: LocationInputStatus.Invalid,
                ErrorMessage: "Indique una dirección, coordenadas o enlace de Google Maps."
            );
        }

        var trimmed = input.Trim();

        // 1. Check Saved Profiles if provided
        if (savedProfiles != null && savedProfiles.Count > 0)
        {
            var matchedProfile = savedProfiles.FirstOrDefault(p =>
                string.Equals(p.Label.Trim(), trimmed, StringComparison.OrdinalIgnoreCase));

            if (matchedProfile != null)
            {
                if (matchedProfile.Latitude.HasValue && matchedProfile.Longitude.HasValue)
                {
                    return new LocationParseResult(
                        OriginalInput: trimmed,
                        NormalizedAddress: string.IsNullOrWhiteSpace(matchedProfile.Address) ? matchedProfile.Label : matchedProfile.Address,
                        Latitude: matchedProfile.Latitude.Value,
                        Longitude: matchedProfile.Longitude.Value,
                        Source: LocationInputSource.Profile,
                        Status: LocationInputStatus.Success
                    );
                }

                return new LocationParseResult(
                    OriginalInput: trimmed,
                    NormalizedAddress: matchedProfile.Address,
                    Latitude: null,
                    Longitude: null,
                    Source: LocationInputSource.Profile,
                    Status: LocationInputStatus.NeedsGeocoding
                );
            }
        }

        // 2. Check Raw Coordinates
        var coordMatch = CoordinatesRegex.Match(trimmed);
        if (coordMatch.Success)
        {
            if (TryParseCoordinates(coordMatch, out var lat, out var lon, out var error))
            {
                return new LocationParseResult(
                    OriginalInput: trimmed,
                    NormalizedAddress: $"{lat:F5}, {lon:F5}",
                    Latitude: lat,
                    Longitude: lon,
                    Source: LocationInputSource.Coordinates,
                    Status: LocationInputStatus.Success
                );
            }

            return new LocationParseResult(
                OriginalInput: trimmed,
                NormalizedAddress: null,
                Latitude: null,
                Longitude: null,
                Source: LocationInputSource.Coordinates,
                Status: LocationInputStatus.Invalid,
                ErrorMessage: error
            );
        }

        // 3. Check Google Maps URLs
        if (GoogleMapsUrlRegex.IsMatch(trimmed))
        {
            return ParseGoogleMapsLongUrl(trimmed);
        }

        if (GoogleMapsShortUrlRegex.IsMatch(trimmed))
        {
            return new LocationParseResult(
                OriginalInput: trimmed,
                NormalizedAddress: null,
                Latitude: null,
                Longitude: null,
                Source: LocationInputSource.GoogleMapsShortUrl,
                Status: LocationInputStatus.NeedsGeocoding
            );
        }

        // Check if user entered an invalid URL format
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new LocationParseResult(
                OriginalInput: trimmed,
                NormalizedAddress: null,
                Latitude: null,
                Longitude: null,
                Source: LocationInputSource.GoogleMapsUrl,
                Status: LocationInputStatus.Invalid,
                ErrorMessage: "El enlace proporcionado no corresponde a un formato reconocido de Google Maps."
            );
        }

        // 4. Plain Text Address
        var normalizedAddress = NormalizeAddress(trimmed);
        return new LocationParseResult(
            OriginalInput: trimmed,
            NormalizedAddress: normalizedAddress,
            Latitude: null,
            Longitude: null,
            Source: LocationInputSource.Address,
            Status: LocationInputStatus.NeedsGeocoding
        );
    }

    private static LocationParseResult ParseGoogleMapsLongUrl(string url)
    {
        // Check for coordinates in @lat,lon
        var atMatch = AtCoordsRegex.Match(url);
        if (atMatch.Success &&
            decimal.TryParse(atMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var atLat) &&
            decimal.TryParse(atMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var atLon))
        {
            if (ValidateLatLon(atLat, atLon, out var err))
            {
                return new LocationParseResult(
                    OriginalInput: url,
                    NormalizedAddress: $"{atLat:F5}, {atLon:F5}",
                    Latitude: atLat,
                    Longitude: atLon,
                    Source: LocationInputSource.GoogleMapsUrl,
                    Status: LocationInputStatus.Success
                );
            }
            return new LocationParseResult(url, null, null, null, LocationInputSource.GoogleMapsUrl, LocationInputStatus.Invalid, err);
        }

        // Check for coordinates in !3d<lat>!4d<lon>
        var dataMatch = DataCoordsRegex.Match(url);
        if (dataMatch.Success &&
            decimal.TryParse(dataMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dLat) &&
            decimal.TryParse(dataMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var dLon))
        {
            if (ValidateLatLon(dLat, dLon, out var err))
            {
                return new LocationParseResult(
                    OriginalInput: url,
                    NormalizedAddress: $"{dLat:F5}, {dLon:F5}",
                    Latitude: dLat,
                    Longitude: dLon,
                    Source: LocationInputSource.GoogleMapsUrl,
                    Status: LocationInputStatus.Success
                );
            }
            return new LocationParseResult(url, null, null, null, LocationInputSource.GoogleMapsUrl, LocationInputStatus.Invalid, err);
        }

        // Check for coordinates in ?q=lat,lon or ?query=lat,lon or ?ll=lat,lon
        var qMatch = QueryCoordsRegex.Match(url);
        if (qMatch.Success &&
            decimal.TryParse(qMatch.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var qLat) &&
            decimal.TryParse(qMatch.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var qLon))
        {
            if (ValidateLatLon(qLat, qLon, out var err))
            {
                return new LocationParseResult(
                    OriginalInput: url,
                    NormalizedAddress: $"{qLat:F5}, {qLon:F5}",
                    Latitude: qLat,
                    Longitude: qLon,
                    Source: LocationInputSource.GoogleMapsUrl,
                    Status: LocationInputStatus.Success
                );
            }
            return new LocationParseResult(url, null, null, null, LocationInputSource.GoogleMapsUrl, LocationInputStatus.Invalid, err);
        }

        // Check for textual query in ?q=address or ?query=address
        var addressMatch = QueryAddressRegex.Match(url);
        if (addressMatch.Success)
        {
            var rawAddress = Uri.UnescapeDataString(addressMatch.Groups[1].Value).Replace('+', ' ').Trim();
            if (!string.IsNullOrWhiteSpace(rawAddress))
            {
                return new LocationParseResult(
                    OriginalInput: url,
                    NormalizedAddress: NormalizeAddress(rawAddress),
                    Latitude: null,
                    Longitude: null,
                    Source: LocationInputSource.GoogleMapsUrl,
                    Status: LocationInputStatus.NeedsGeocoding
                );
            }
        }

        return new LocationParseResult(
            OriginalInput: url,
            NormalizedAddress: null,
            Latitude: null,
            Longitude: null,
            Source: LocationInputSource.GoogleMapsUrl,
            Status: LocationInputStatus.Invalid,
            ErrorMessage: "No se pudieron extraer coordenadas ni dirección del enlace de Google Maps."
        );
    }

    private static bool TryParseCoordinates(Match match, out decimal lat, out decimal lon, out string? error)
    {
        lat = 0m;
        lon = 0m;
        error = null;

        var latStr = match.Groups[1].Value;
        var latDir = match.Groups[2].Value.ToUpperInvariant();
        var lonStr = match.Groups[3].Value;
        var lonDir = match.Groups[4].Value.ToUpperInvariant();

        if (!decimal.TryParse(latStr, NumberStyles.Float, CultureInfo.InvariantCulture, out lat))
        {
            error = "Latitud inválida.";
            return false;
        }

        if (!decimal.TryParse(lonStr, NumberStyles.Float, CultureInfo.InvariantCulture, out lon))
        {
            error = "Longitud inválida.";
            return false;
        }

        if (latDir == "S" && lat > 0) lat = -lat;
        if (latDir == "N" && lat < 0) lat = Math.Abs(lat);
        if (lonDir == "W" && lon > 0) lon = -lon;
        if (lonDir == "E" && lon < 0) lon = Math.Abs(lon);

        return ValidateLatLon(lat, lon, out error);
    }

    private static bool ValidateLatLon(decimal lat, decimal lon, out string? error)
    {
        if (lat < -90m || lat > 90m)
        {
            error = "La latitud debe estar comprendida entre -90 y 90 grados.";
            return false;
        }

        if (lon < -180m || lon > 180m)
        {
            error = "La longitud debe estar comprendida entre -180 y 180 grados.";
            return false;
        }

        error = null;
        return true;
    }

    public static string NormalizeAddress(string raw)
    {
        var cleaned = raw.Trim();
        cleaned = Regex.Replace(cleaned, @"\b(cra|cr|carrera)\.?\b", "Carrera", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\b(cll|cl|calle)\.?\b", "Calle", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\b(diag|dg|diagonal)\.?\b", "Diagonal", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\b(tv|trans|transversal)\.?\b", "Transversal", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\b(av|ave|avenida)\.?\b", "Avenida", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\s{2,}", " ");

        // Append Medellín if no city is mentioned
        if (!cleaned.Contains("medellin", StringComparison.OrdinalIgnoreCase) &&
            !cleaned.Contains("envigado", StringComparison.OrdinalIgnoreCase) &&
            !cleaned.Contains("itagui", StringComparison.OrdinalIgnoreCase) &&
            !cleaned.Contains("bello", StringComparison.OrdinalIgnoreCase) &&
            !cleaned.Contains("sabaneta", StringComparison.OrdinalIgnoreCase) &&
            !cleaned.Contains("colombia", StringComparison.OrdinalIgnoreCase))
        {
            cleaned += ", Medellín, Colombia";
        }

        return cleaned;
    }

    public static async Task<LocationParseResult> ResolveGoogleMapsShortUrlAsync(
        string shortUrl,
        System.Net.Http.HttpClient httpClient,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(shortUrl))
        {
            return new LocationParseResult(shortUrl ?? "", null, null, null, LocationInputSource.GoogleMapsShortUrl, LocationInputStatus.Invalid, "Enlace corto vacío.");
        }

        if (!GoogleMapsShortUrlRegex.IsMatch(shortUrl.Trim()))
        {
            return new LocationParseResult(shortUrl, null, null, null, LocationInputSource.GoogleMapsShortUrl, LocationInputStatus.Invalid, "Enlace no reconocido como Google Maps corto.");
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var currentUri = new Uri(shortUrl.Trim());
            for (int hop = 0; hop < 3; hop++)
            {
                if (!IsAllowedGoogleHost(currentUri.Host))
                {
                    return new LocationParseResult(shortUrl, null, null, null, LocationInputSource.GoogleMapsShortUrl, LocationInputStatus.Invalid, "Redirección a host no autorizado.");
                }

                using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, currentUri);
                req.Headers.Add("User-Agent", "MercadoInteligente/1.0");

                using var resp = await httpClient.SendAsync(req, System.Net.Http.HttpCompletionOption.ResponseHeadersRead, cts.Token);
                if (resp.StatusCode is System.Net.HttpStatusCode.MovedPermanently or System.Net.HttpStatusCode.Found or System.Net.HttpStatusCode.SeeOther or System.Net.HttpStatusCode.TemporaryRedirect or (System.Net.HttpStatusCode)308)
                {
                    var location = resp.Headers.Location;
                    if (location == null) break;

                    currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                    var resolvedUrl = currentUri.ToString();

                    var parsedTarget = ParseGoogleMapsLongUrl(resolvedUrl);
                    if (parsedTarget.Status == LocationInputStatus.Success && parsedTarget.Latitude.HasValue && parsedTarget.Longitude.HasValue)
                    {
                        return parsedTarget with { OriginalInput = shortUrl, Source = LocationInputSource.GoogleMapsShortUrl };
                    }
                }
                else
                {
                    var resolvedUrl = currentUri.ToString();
                    var parsedTarget = ParseGoogleMapsLongUrl(resolvedUrl);
                    if (parsedTarget.Status == LocationInputStatus.Success && parsedTarget.Latitude.HasValue && parsedTarget.Longitude.HasValue)
                    {
                        return parsedTarget with { OriginalInput = shortUrl, Source = LocationInputSource.GoogleMapsShortUrl };
                    }
                    break;
                }
            }

            var finalParsed = ParseGoogleMapsLongUrl(currentUri.ToString());
            if (finalParsed.Status == LocationInputStatus.Success && finalParsed.Latitude.HasValue && finalParsed.Longitude.HasValue)
            {
                return finalParsed with { OriginalInput = shortUrl, Source = LocationInputSource.GoogleMapsShortUrl };
            }

            return new LocationParseResult(shortUrl, null, null, null, LocationInputSource.GoogleMapsShortUrl, LocationInputStatus.Invalid, "No fue posible obtener coordenadas de este enlace.");
        }
        catch (OperationCanceledException)
        {
            return new LocationParseResult(shortUrl, null, null, null, LocationInputSource.GoogleMapsShortUrl, LocationInputStatus.Invalid, "Tiempo de espera agotado al resolver el enlace.");
        }
        catch (Exception ex)
        {
            return new LocationParseResult(shortUrl, null, null, null, LocationInputSource.GoogleMapsShortUrl, LocationInputStatus.Invalid, $"Error al resolver enlace: {ex.Message}");
        }
    }

    private static bool IsAllowedGoogleHost(string host)
    {
        var h = host.ToLowerInvariant();
        return h == "maps.app.goo.gl" ||
               h == "goo.gl" ||
               h == "google.com" ||
               h.EndsWith(".google.com", StringComparison.Ordinal) ||
               h == "google.co" ||
               h.EndsWith(".google.co", StringComparison.Ordinal);
    }
}