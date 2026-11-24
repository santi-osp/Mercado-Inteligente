using System.Collections.Concurrent;
using MercadoInteligente.Application;

namespace MercadoInteligente.Infrastructure;

public sealed class CommercialContextCache : ICommercialContextCache
{
    private sealed record CacheEntry(EffectiveProviderContext Context, DateTime ExpiresAt);

    private readonly ConcurrentDictionary<string, CacheEntry> entries = new(StringComparer.OrdinalIgnoreCase);

    public EffectiveProviderContext? Get(string providerCode, Guid? locationProfileId, string? addressOrCoordinateKey)
    {
        var key = BuildKey(providerCode, locationProfileId, addressOrCoordinateKey);
        if (string.IsNullOrWhiteSpace(key)) return null;

        if (entries.TryGetValue(key, out var entry))
        {
            if (DateTime.UtcNow <= entry.ExpiresAt)
            {
                return entry.Context;
            }
            entries.TryRemove(key, out _);
        }

        return null;
    }

    public void Set(string providerCode, Guid? locationProfileId, string? addressOrCoordinateKey, EffectiveProviderContext context, TimeSpan? ttl = null)
    {
        var key = BuildKey(providerCode, locationProfileId, addressOrCoordinateKey);
        if (string.IsNullOrWhiteSpace(key)) return;

        var lifetime = ttl ?? TimeSpan.FromHours(2);
        var entry = new CacheEntry(context, DateTime.UtcNow.Add(lifetime));
        entries[key] = entry;

        // Also index by profile ID if present so lookups by profile ID always hit
        if (locationProfileId.HasValue)
        {
            var profileKey = BuildKey(providerCode, locationProfileId, null);
            if (!string.Equals(key, profileKey, StringComparison.OrdinalIgnoreCase))
            {
                entries[profileKey] = entry;
            }
        }
    }

    public void Invalidate(Guid? locationProfileId = null)
    {
        if (!locationProfileId.HasValue)
        {
            entries.Clear();
            return;
        }

        var prefix = $"{locationProfileId.Value:D}";
        foreach (var key in entries.Keys)
        {
            if (key.Contains(prefix, StringComparison.OrdinalIgnoreCase))
            {
                entries.TryRemove(key, out _);
            }
        }
    }

    public void Clear() => entries.Clear();

    private static string BuildKey(string providerCode, Guid? locationProfileId, string? addressOrCoordinateKey)
    {
        var prov = (providerCode ?? "").Trim().ToUpperInvariant();
        if (locationProfileId.HasValue)
        {
            return $"{prov}:profile:{locationProfileId.Value:D}";
        }

        if (!string.IsNullOrWhiteSpace(addressOrCoordinateKey))
        {
            return $"{prov}:addr:{addressOrCoordinateKey.Trim().ToLowerInvariant()}";
        }

        return string.Empty;
    }
}
