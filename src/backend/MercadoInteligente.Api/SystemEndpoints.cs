using MercadoInteligente.Application;
using MercadoInteligente.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Api;

public static class SystemEndpoints
{
    public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/system/health", async (BackupService backups, MarketDb db, IAiProvider ai,
            IGeocodingProvider geocoding, IRoutingProvider routing, GoogleProviderHealth googleHealth) =>
        {
            var observed = googleHealth.Read();
            var geocodingStatus = geocoding.IsConfigured ? observed.Geocoding : "NOT_CONFIGURED";
            var routesStatus = routing.IsConfigured ? observed.Routes : "NOT_CONFIGURED";
            return new { database = await backups.IntegrityAsync(), foreignKeysValid = await backups.ForeignKeysValidAsync(), products = await db.ExternalProducts.CountAsync(), ai = ai.IsConfigured ? "CONFIGURED_UNVERIFIED" : "NOT_CONFIGURED", google = routesStatus, googleGeocoding = geocodingStatus, googleRoutes = routesStatus, mode = "LOCAL" };
        });
        api.MapGet("/system/status", async (BackupService backups, MarketDb db, IMaintenanceService maintenance,
            IStoreProviderRegistry registry, IAiProvider ai, IGeocodingProvider geocoding,
            IRoutingProvider routing, GoogleProviderHealth googleHealth, CancellationToken ct) =>
        {
            var storage = await maintenance.GetStorageStatsAsync(ct);
            var dbIntegrity = await backups.IntegrityAsync(ct);
            var fkValid = await backups.ForeignKeysValidAsync(ct);
            var allBackups = backups.ListBackups();
            var lastSync = await db.SyncRuns.AsNoTracking().OrderByDescending(s => s.StartedAt).FirstOrDefaultAsync(ct);
            var recentRuns = await db.SyncRuns.AsNoTracking().OrderByDescending(s => s.StartedAt).Take(100).ToListAsync(ct);
            var providerHealth = new[] { "D1", "EXITO", "ARA" }.Select(code =>
            {
                var run = recentRuns.FirstOrDefault(item => item.Provider.Equals(code, StringComparison.OrdinalIgnoreCase));
                return new { provider = code, status = run?.State.ToString() ?? "NOT_CHECKED", checkedAt = run?.FinishedAt ?? run?.StartedAt };
            }).ToArray();
            var pendingActions = new List<string>();
            if (!ai.IsConfigured)
                pendingActions.Add("Configurar clave de OmniRouter en Ajustes para capacidades semánticas opcionales.");
            if (!routing.IsConfigured)
                pendingActions.Add("Configurar clave de Google Maps en Ajustes para geocodificación precisa y rutas en vivo.");
            pendingActions.Add("Validación física en góndola (5-10 productos por cadena D1 y Éxito) para auditar paridad con precios web.");
            var observed = googleHealth.Read();

            return Results.Ok(new
            {
                version = MercadoInteligente.Domain.AppVersionInfo.Version,
                appStatus = "OPERATIONAL",
                backend = "OPERATIONAL",
                database = new
                {
                    integrity = dbIntegrity,
                    foreignKeysValid = fkValid,
                    sizeBytes = storage.DatabaseBytes + storage.DatabaseSidecarsBytes,
                    productsCount = storage.ExternalProductsCount,
                    skusCount = storage.ExternalSkusCount,
                    offersCount = storage.OffersCount,
                    observationsCount = storage.PriceObservationsCount
                },
                raw = new { sizeBytes = storage.RawBytes, filesCount = storage.RawFilesCount },
                backups = new
                {
                    sizeBytes = storage.BackupsBytes,
                    filesCount = storage.BackupFilesCount,
                    lastBackupName = allBackups.FirstOrDefault()?.Name,
                    available = allBackups.Take(10)
                },
                lastSync = lastSync == null ? null : new
                {
                    provider = lastSync.Provider,
                    state = lastSync.State.ToString(),
                    finishedAt = lastSync.FinishedAt,
                    itemsReceived = lastSync.ItemsReceived
                },
                providers = registry.GetMetadata(),
                providerHealth,
                google = new
                {
                    configured = routing.IsConfigured,
                    status = routing.IsConfigured ? observed.Routes : "NOT_CONFIGURED",
                    geocoding = geocoding.IsConfigured ? observed.Geocoding : "NOT_CONFIGURED",
                    routes = routing.IsConfigured ? observed.Routes : "NOT_CONFIGURED"
                },
                ai = new { configured = ai.IsConfigured, status = ai.IsConfigured ? "CONFIGURED_UNVERIFIED" : "NOT_CONFIGURED" },
                pendingExternalActions = pendingActions
            });
        });
        api.MapGet("/system/storage", async (IMaintenanceService maintenance, CancellationToken ct) =>
            Results.Ok(await maintenance.GetStorageStatsAsync(ct)));
        api.MapPost("/system/maintenance", async (IMaintenanceService maintenance, CancellationToken ct) =>
            Results.Ok(await maintenance.ExecuteMaintenanceAsync(ct)));
        return api;
    }
}
