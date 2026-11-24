using System.Globalization;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public sealed class StoreRouteOptimizer
{
    private readonly IRoutingProvider _routingProvider;

    public StoreRouteOptimizer(IRoutingProvider routingProvider)
    {
        _routingProvider = routingProvider;
    }

    public static StoreRoutePlan OptimizeRouteOffline(
        decimal originLatitude,
        decimal originLongitude,
        IReadOnlyList<Store> stores,
        bool returnToOrigin = true,
        TransportMode transportMode = TransportMode.Driving,
        decimal costPerKm = 2500m,
        decimal? maxRouteKm = null)
    {
        var validStores = stores.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToList();
        if (validStores.Count == 0)
        {
            return new StoreRoutePlan(
                OriginLatitude: originLatitude,
                OriginLongitude: originLongitude,
                ReturnToOrigin: returnToOrigin,
                TransportMode: transportMode,
                CostPerKm: costPerKm,
                MaxRouteKm: maxRouteKm,
                TotalDistanceKm: 0m,
                TotalDurationMinutes: 0m,
                EstimatedTravelCost: 0m,
                RouteType: RouteType.StraightLineEstimate,
                OrderedStores: [],
                Legs: [],
                GoogleMapsNavigationUrl: ""
            );
        }

        IReadOnlyList<Store> bestOrder;
        if (validStores.Count <= 4)
        {
            bestOrder = FindExactOptimalPermutationOffline(originLatitude, originLongitude, validStores, returnToOrigin);
        }
        else
        {
            bestOrder = FindGreedyNearestNeighborOffline(originLatitude, originLongitude, validStores);
        }

        var (legs, totalKm, totalDuration) = BuildRouteLegsOffline(
            originLatitude, originLongitude, bestOrder, returnToOrigin, transportMode);

        var travelCost = Math.Round(totalKm * costPerKm, 0);
        var navUrl = GenerateGoogleMapsUrl(originLatitude, originLongitude, bestOrder, returnToOrigin, transportMode);

        return new StoreRoutePlan(
            OriginLatitude: originLatitude,
            OriginLongitude: originLongitude,
            ReturnToOrigin: returnToOrigin,
            TransportMode: transportMode,
            CostPerKm: costPerKm,
            MaxRouteKm: maxRouteKm,
            TotalDistanceKm: totalKm,
            TotalDurationMinutes: totalDuration,
            EstimatedTravelCost: travelCost,
            RouteType: RouteType.StraightLineEstimate,
            OrderedStores: bestOrder,
            Legs: legs,
            GoogleMapsNavigationUrl: navUrl,
            RoutingFallback: false,
            RoutingModeRequested: transportMode == TransportMode.Motorcycle ? "MOTORCYCLE" : transportMode.ToString().ToUpperInvariant(),
            RoutingModeUsed: transportMode == TransportMode.Motorcycle ? "OFFLINE_MOTORCYCLE" : transportMode.ToString().ToUpperInvariant(),
            FallbackMessage: null,
            DistanceSource: "HAVERSINE",
            IsApproximate: true
        );
    }

    private static IReadOnlyList<Store> FindExactOptimalPermutationOffline(
        decimal originLat, decimal originLon, List<Store> stores, bool returnToOrigin)
    {
        var permutations = GetPermutations(stores).ToList();
        if (permutations.Count == 1) return permutations[0];

        List<Store>? best = null;
        decimal minDistance = decimal.MaxValue;

        foreach (var perm in permutations)
        {
            decimal dist = 0m;
            decimal curLat = originLat;
            decimal curLon = originLon;

            foreach (var store in perm)
            {
                var straight = HaversineService.CalculateDistanceKm(curLat, curLon, store.Latitude!.Value, store.Longitude!.Value);
                dist += HaversineService.EstimateRoadDistanceKm(straight);
                curLat = store.Latitude!.Value;
                curLon = store.Longitude!.Value;
            }

            if (returnToOrigin)
            {
                var straightReturn = HaversineService.CalculateDistanceKm(curLat, curLon, originLat, originLon);
                dist += HaversineService.EstimateRoadDistanceKm(straightReturn);
            }

            if (dist < minDistance)
            {
                minDistance = dist;
                best = perm;
            }
        }

        return best ?? stores;
    }

    private static IReadOnlyList<Store> FindGreedyNearestNeighborOffline(
        decimal originLat, decimal originLon, List<Store> stores)
    {
        var remaining = new List<Store>(stores);
        var ordered = new List<Store>();
        decimal curLat = originLat;
        decimal curLon = originLon;

        while (remaining.Count > 0)
        {
            Store? closest = null;
            decimal closestDist = decimal.MaxValue;

            foreach (var s in remaining)
            {
                var straight = HaversineService.CalculateDistanceKm(curLat, curLon, s.Latitude!.Value, s.Longitude!.Value);
                var d = HaversineService.EstimateRoadDistanceKm(straight);
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = s;
                }
            }

            if (closest != null)
            {
                ordered.Add(closest);
                remaining.Remove(closest);
                curLat = closest.Latitude!.Value;
                curLon = closest.Longitude!.Value;
            }
        }

        return ordered;
    }

    private static (List<RouteLeg> Legs, decimal TotalKm, decimal TotalDuration) BuildRouteLegsOffline(
        decimal originLat, decimal originLon, IReadOnlyList<Store> orderedStores, bool returnToOrigin, TransportMode mode)
    {
        var legs = new List<RouteLeg>();
        decimal totalKm = 0m;
        decimal totalDuration = 0m;

        decimal curLat = originLat;
        decimal curLon = originLon;
        string curLabel = "Origen (Usuario)";

        foreach (var store in orderedStores)
        {
            var nextLat = store.Latitude!.Value;
            var nextLon = store.Longitude!.Value;
            var nextLabel = $"{store.Name}";

            var straight = HaversineService.CalculateDistanceKm(curLat, curLon, nextLat, nextLon);
            var roadKm = HaversineService.EstimateRoadDistanceKm(straight);
            var duration = HaversineService.EstimateDurationMinutes(roadKm, mode);

            legs.Add(new RouteLeg(curLabel, nextLabel, roadKm, duration, RouteType.StraightLineEstimate));
            totalKm += roadKm;
            totalDuration += duration;

            curLat = nextLat;
            curLon = nextLon;
            curLabel = nextLabel;
        }

        if (returnToOrigin)
        {
            var straight = HaversineService.CalculateDistanceKm(curLat, curLon, originLat, originLon);
            var roadKm = HaversineService.EstimateRoadDistanceKm(straight);
            var duration = HaversineService.EstimateDurationMinutes(roadKm, mode);

            legs.Add(new RouteLeg(curLabel, "Regreso a Origen", roadKm, duration, RouteType.StraightLineEstimate));
            totalKm += roadKm;
            totalDuration += duration;
        }

        return (legs, Math.Round(totalKm, 2), Math.Round(totalDuration, 1));
    }

    public async Task<StoreRoutePlan> OptimizeRouteAsync(
        decimal originLatitude,
        decimal originLongitude,
        IReadOnlyList<Store> stores,
        bool returnToOrigin = true,
        TransportMode transportMode = TransportMode.Driving,
        decimal costPerKm = 2500m,
        decimal? maxRouteKm = null,
        CancellationToken cancellationToken = default)
    {
        // Filter out stores without coordinates
        var validStores = stores.Where(s => s.Latitude.HasValue && s.Longitude.HasValue).ToList();

        if (validStores.Count == 0)
        {
            return new StoreRoutePlan(
                OriginLatitude: originLatitude,
                OriginLongitude: originLongitude,
                ReturnToOrigin: returnToOrigin,
                TransportMode: transportMode,
                CostPerKm: costPerKm,
                MaxRouteKm: maxRouteKm,
                TotalDistanceKm: 0m,
                TotalDurationMinutes: 0m,
                EstimatedTravelCost: 0m,
                RouteType: RouteType.RoadRoute,
                OrderedStores: [],
                Legs: [],
                GoogleMapsNavigationUrl: ""
            );
        }

        // Find optimal store permutation
        IReadOnlyList<Store> bestOrder;
        if (validStores.Count <= 4)
        {
            bestOrder = await FindExactOptimalPermutationAsync(originLatitude, originLongitude, validStores, returnToOrigin, transportMode, cancellationToken);
        }
        else
        {
            bestOrder = await FindGreedyNearestNeighborAsync(originLatitude, originLongitude, validStores, returnToOrigin, transportMode, cancellationToken);
        }

        // Build detailed legs and metrics for the best order
        var (legs, totalKm, totalDuration, combinedType, fallback, reqMode, usedMode, fbMsg) = await BuildRouteLegsAsync(
            originLatitude, originLongitude, bestOrder, returnToOrigin, transportMode, cancellationToken);

        var travelCost = Math.Round(totalKm * costPerKm, 0);

        // Generate zero-key public Google Maps URL
        var navUrl = GenerateGoogleMapsUrl(originLatitude, originLongitude, bestOrder, returnToOrigin, transportMode);

        return new StoreRoutePlan(
            OriginLatitude: originLatitude,
            OriginLongitude: originLongitude,
            ReturnToOrigin: returnToOrigin,
            TransportMode: transportMode,
            CostPerKm: costPerKm,
            MaxRouteKm: maxRouteKm,
            TotalDistanceKm: totalKm,
            TotalDurationMinutes: totalDuration,
            EstimatedTravelCost: travelCost,
            RouteType: combinedType,
            OrderedStores: bestOrder,
            Legs: legs,
            GoogleMapsNavigationUrl: navUrl,
            RoutingFallback: fallback,
            RoutingModeRequested: reqMode ?? transportMode.ToString().ToUpperInvariant(),
            RoutingModeUsed: usedMode ?? transportMode.ToString().ToUpperInvariant(),
            FallbackMessage: fbMsg,
            DistanceSource: combinedType == RouteType.StraightLineEstimate ? "HAVERSINE" : "GOOGLE_ROUTES",
            IsApproximate: combinedType == RouteType.StraightLineEstimate
        );
    }

    private async Task<IReadOnlyList<Store>> FindExactOptimalPermutationAsync(
        decimal originLat,
        decimal originLon,
        List<Store> stores,
        bool returnToOrigin,
        TransportMode mode,
        CancellationToken cancellationToken)
    {
        var permutations = GetPermutations(stores).ToList();
        if (permutations.Count == 1)
        {
            return permutations[0];
        }

        List<Store>? best = null;
        decimal minDistance = decimal.MaxValue;

        foreach (var perm in permutations)
        {
            decimal dist = 0m;
            decimal currentLat = originLat;
            decimal currentLon = originLon;

            foreach (var store in perm)
            {
                var d = await GetLegDistanceAsync(currentLat, currentLon, store.Latitude!.Value, store.Longitude!.Value, mode, cancellationToken);
                dist += d;
                currentLat = store.Latitude!.Value;
                currentLon = store.Longitude!.Value;
            }

            if (returnToOrigin)
            {
                dist += await GetLegDistanceAsync(currentLat, currentLon, originLat, originLon, mode, cancellationToken);
            }

            if (dist < minDistance)
            {
                minDistance = dist;
                best = perm;
            }
        }

        return best ?? stores;
    }

    private async Task<IReadOnlyList<Store>> FindGreedyNearestNeighborAsync(
        decimal originLat,
        decimal originLon,
        List<Store> stores,
        bool returnToOrigin,
        TransportMode mode,
        CancellationToken cancellationToken)
    {
        var remaining = new List<Store>(stores);
        var ordered = new List<Store>();

        decimal currentLat = originLat;
        decimal currentLon = originLon;

        while (remaining.Count > 0)
        {
            Store? closest = null;
            decimal closestDist = decimal.MaxValue;

            foreach (var s in remaining)
            {
                var d = await GetLegDistanceAsync(currentLat, currentLon, s.Latitude!.Value, s.Longitude!.Value, mode, cancellationToken);
                if (d < closestDist)
                {
                    closestDist = d;
                    closest = s;
                }
            }

            if (closest != null)
            {
                ordered.Add(closest);
                remaining.Remove(closest);
                currentLat = closest.Latitude!.Value;
                currentLon = closest.Longitude!.Value;
            }
        }

        return ordered;
    }

    private async Task<decimal> GetLegDistanceAsync(
        decimal lat1, decimal lon1, decimal lat2, decimal lon2, TransportMode mode, CancellationToken cancellationToken)
    {
        var route = await _routingProvider.GetRouteAsync(lat1, lon1, lat2, lon2, mode, cancellationToken);
        return route.DistanceKm;
    }

    private async Task<(List<RouteLeg> Legs, decimal TotalKm, decimal TotalDuration, RouteType Type, bool RoutingFallback, string? RoutingModeRequested, string? RoutingModeUsed, string? FallbackMessage)> BuildRouteLegsAsync(
        decimal originLat,
        decimal originLon,
        IReadOnlyList<Store> orderedStores,
        bool returnToOrigin,
        TransportMode mode,
        CancellationToken cancellationToken)
    {
        var legs = new List<RouteLeg>();
        decimal totalKm = 0m;
        decimal totalDuration = 0m;
        bool hasEstimate = false;
        bool hasCached = false;
        bool hasFallback = false;
        string? reqMode = null;
        string? usedMode = null;
        string? fallbackMsg = null;

        decimal currentLat = originLat;
        decimal currentLon = originLon;
        string currentLabel = "Origen (Usuario)";

        foreach (var store in orderedStores)
        {
            var nextLat = store.Latitude!.Value;
            var nextLon = store.Longitude!.Value;
            var nextLabel = store.Name;

            var route = await _routingProvider.GetRouteAsync(currentLat, currentLon, nextLat, nextLon, mode, cancellationToken);
            legs.Add(new RouteLeg(currentLabel, nextLabel, route.DistanceKm, route.DurationMinutes, route.Type));

            totalKm += route.DistanceKm;
            totalDuration += route.DurationMinutes;

            if (route.Type == RouteType.StraightLineEstimate) hasEstimate = true;
            if (route.Type == RouteType.CachedRoute) hasCached = true;
            if (route.RoutingFallback)
            {
                hasFallback = true;
                reqMode ??= route.RoutingModeRequested;
                usedMode ??= route.RoutingModeUsed;
                fallbackMsg ??= route.FallbackMessage;
            }

            currentLat = nextLat;
            currentLon = nextLon;
            currentLabel = nextLabel;
        }

        if (returnToOrigin)
        {
            var returnRoute = await _routingProvider.GetRouteAsync(currentLat, currentLon, originLat, originLon, mode, cancellationToken);
            legs.Add(new RouteLeg(currentLabel, "Regreso a Origen", returnRoute.DistanceKm, returnRoute.DurationMinutes, returnRoute.Type));

            totalKm += returnRoute.DistanceKm;
            totalDuration += returnRoute.DurationMinutes;

            if (returnRoute.Type == RouteType.StraightLineEstimate) hasEstimate = true;
            if (returnRoute.Type == RouteType.CachedRoute) hasCached = true;
            if (returnRoute.RoutingFallback)
            {
                hasFallback = true;
                reqMode ??= returnRoute.RoutingModeRequested;
                usedMode ??= returnRoute.RoutingModeUsed;
                fallbackMsg ??= returnRoute.FallbackMessage;
            }
        }

        RouteType combinedType = RouteType.RoadRoute;
        if (hasEstimate) combinedType = RouteType.StraightLineEstimate;
        else if (hasCached) combinedType = RouteType.CachedRoute;

        return (legs, Math.Round(totalKm, 2), Math.Round(totalDuration, 1), combinedType, hasFallback, reqMode, usedMode, fallbackMsg);
    }

    public static string GenerateGoogleMapsUrl(
        decimal originLat,
        decimal originLon,
        IReadOnlyList<Store> stores,
        bool returnToOrigin,
        TransportMode mode)
    {
        if (stores.Count == 0) return "";

        var gMode = mode switch
        {
            TransportMode.Walking => "walking",
            TransportMode.Bicycling => "bicycling",
            _ => "driving"
        };

        var originStr = $"{originLat.ToString(CultureInfo.InvariantCulture)},{originLon.ToString(CultureInfo.InvariantCulture)}";

        string destStr;
        List<string> waypointCoords = [];

        if (returnToOrigin)
        {
            destStr = originStr;
            waypointCoords.AddRange(stores.Select(s => $"{s.Latitude!.Value.ToString(CultureInfo.InvariantCulture)},{s.Longitude!.Value.ToString(CultureInfo.InvariantCulture)}"));
        }
        else
        {
            var last = stores[^1];
            destStr = $"{last.Latitude!.Value.ToString(CultureInfo.InvariantCulture)},{last.Longitude!.Value.ToString(CultureInfo.InvariantCulture)}";
            waypointCoords.AddRange(stores.Take(stores.Count - 1).Select(s => $"{s.Latitude!.Value.ToString(CultureInfo.InvariantCulture)},{s.Longitude!.Value.ToString(CultureInfo.InvariantCulture)}"));
        }

        var url = $"https://www.google.com/maps/dir/?api=1&origin={Uri.EscapeDataString(originStr)}&destination={Uri.EscapeDataString(destStr)}&travelmode={gMode}";

        if (waypointCoords.Count > 0)
        {
            var waypoints = string.Join("|", waypointCoords);
            url += $"&waypoints={Uri.EscapeDataString(waypoints)}";
        }

        return url;
    }

    public static Store? FindClosestStore(decimal originLat, decimal originLon, IEnumerable<Store> stores)
    {
        Store? closest = null;
        decimal minDist = decimal.MaxValue;
        foreach (var s in stores)
        {
            if (s.Latitude.HasValue && s.Longitude.HasValue)
            {
                var d = HaversineService.CalculateDistanceKm(originLat, originLon, s.Latitude.Value, s.Longitude.Value);
                if (d < minDist)
                {
                    minDist = d;
                    closest = s;
                }
            }
        }
        return closest;
    }

    private static IEnumerable<List<T>> GetPermutations<T>(IReadOnlyList<T> list)
    {
        if (list.Count == 0)
        {
            yield return new List<T>();
            yield break;
        }

        if (list.Count == 1)
        {
            yield return new List<T> { list[0] };
            yield break;
        }

        for (int i = 0; i < list.Count; i++)
        {
            var current = list[i];
            var sublist = new List<T>(list);
            sublist.RemoveAt(i);

            foreach (var perm in GetPermutations(sublist))
            {
                perm.Insert(0, current);
                yield return perm;
            }
        }
    }
}
