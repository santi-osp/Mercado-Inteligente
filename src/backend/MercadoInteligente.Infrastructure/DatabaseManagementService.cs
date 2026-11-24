using System.Data.Common;
using MercadoInteligente.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure;

public sealed record DatabaseStatsResult(
    long DatabaseBytes,
    long RawBytes,
    long BackupsBytes,
    long LogsBytes,
    int ExternalProductsCount,
    int ExternalSkusCount,
    int OffersCount,
    int PriceObservationsCount,
    int BasketsCount,
    int BasketItemsCount,
    int RawCapturesCount,
    Dictionary<string, int> RetailerProductCounts,
    string IntegrityStatus,
    bool ForeignKeysValid,
    DateTime? LastSyncUtc,
    int BackupsCount,
    DateTime? LatestBackupUtc
);

public sealed record DatabaseActionResult(
    bool Success,
    string Message,
    string? PreActionBackup = null
);

public class DatabaseManagementService(LocalPaths paths, MarketDb db, BackupService backupService)
{
    public async Task<DatabaseStatsResult> GetStatsAsync(CancellationToken ct = default)
    {
        var dbBytes = File.Exists(paths.Database) ? new FileInfo(paths.Database).Length : 0;
        var rawBytes = GetDirectorySize(paths.Raw);
        var backupsBytes = GetDirectorySize(paths.Backups);
        var logsBytes = GetDirectorySize(paths.Logs);

        var productsCount = await db.ExternalProducts.CountAsync(ct);
        var skusCount = await db.ExternalSkus.CountAsync(ct);
        var offersCount = await db.Offers.CountAsync(ct);
        var observationsCount = await db.PriceObservations.CountAsync(ct);
        var basketsCount = await db.Baskets.CountAsync(ct);
        var basketItemsCount = await db.BasketItems.CountAsync(ct);
        var rawCapturesCount = await db.RawCaptures.CountAsync(ct);

        var retailerCounts = await (
            from p in db.ExternalProducts.AsNoTracking()
            join r in db.Retailers.AsNoTracking() on p.RetailerId equals r.Id
            group p by r.Name into g
            select new { Retailer = g.Key, Count = g.Count() }
        ).ToDictionaryAsync(x => x.Retailer, x => x.Count, ct);

        var integrity = await backupService.IntegrityAsync(ct);
        var fkValid = await backupService.ForeignKeysValidAsync(ct);

        DateTime? lastSync = null;
        if (await db.SyncRuns.AnyAsync(ct))
        {
            lastSync = await db.SyncRuns.AsNoTracking().MaxAsync(x => (DateTime?)x.FinishedAt, ct);
        }

        var backups = backupService.ListBackups();
        var backupsCount = backups.Count;
        DateTime? latestBackup = backupsCount > 0 ? backups.Max(b => b.CreatedAtUtc) : null;

        return new DatabaseStatsResult(
            DatabaseBytes: dbBytes,
            RawBytes: rawBytes,
            BackupsBytes: backupsBytes,
            LogsBytes: logsBytes,
            ExternalProductsCount: productsCount,
            ExternalSkusCount: skusCount,
            OffersCount: offersCount,
            PriceObservationsCount: observationsCount,
            BasketsCount: basketsCount,
            BasketItemsCount: basketItemsCount,
            RawCapturesCount: rawCapturesCount,
            RetailerProductCounts: retailerCounts,
            IntegrityStatus: integrity,
            ForeignKeysValid: fkValid,
            LastSyncUtc: lastSync,
            BackupsCount: backupsCount,
            LatestBackupUtc: latestBackup
        );
    }

    public async Task<DatabaseActionResult> OptimizeAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = paths.Database,
            Pooling = false
        }.ToString());

        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA optimize; VACUUM; ANALYZE;";
        await cmd.ExecuteNonQueryAsync(ct);

        return new DatabaseActionResult(true, "Base de datos optimizada (VACUUM y ANALYZE completados exitosamente).");
    }

    public async Task<DatabaseActionResult> ClearCatalogAsync(CancellationToken ct = default)
    {
        var backupFile = await backupService.CreateAsync("pre-clear-catalog", ct);

        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = paths.Database,
            Pooling = false
        }.ToString());

        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM PriceObservations;
            DELETE FROM Offers;
            DELETE FROM ProductMatches;
            DELETE FROM ExternalSkus;
            DELETE FROM ExternalProducts;
            DELETE FROM RawCaptures;
            DELETE FROM SyncRuns;
        ";
        await cmd.ExecuteNonQueryAsync(ct);

        try
        {
            cmd.CommandText = "DELETE FROM ProductSearch;";
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        return new DatabaseActionResult(true, "Catálogo comercial eliminado exitosamente. Configuración y listas preservadas.", backupFile);
    }

    public async Task<DatabaseActionResult> ClearHistoryAsync(CancellationToken ct = default)
    {
        var backupFile = await backupService.CreateAsync("pre-clear-history", ct);

        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = paths.Database,
            Pooling = false
        }.ToString());

        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM PriceObservations;";
        await cmd.ExecuteNonQueryAsync(ct);

        return new DatabaseActionResult(true, "Historial de observaciones de precios eliminado exitosamente. Ofertas actuales preservadas.", backupFile);
    }

    public async Task<DatabaseActionResult> ClearBasketsAsync(CancellationToken ct = default)
    {
        var backupFile = await backupService.CreateAsync("pre-clear-baskets", ct);

        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = paths.Database,
            Pooling = false
        }.ToString());

        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM BasketItems; DELETE FROM Baskets;";
        await cmd.ExecuteNonQueryAsync(ct);

        return new DatabaseActionResult(true, "Listas y cestas de mercado eliminadas exitosamente.", backupFile);
    }

    public async Task<DatabaseActionResult> ClearCacheAsync(CancellationToken ct = default)
    {
        int deletedFiles = 0;
        if (Directory.Exists(paths.Backups))
        {
            foreach (var file in Directory.EnumerateFiles(paths.Backups, ".restore-*.*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); deletedFiles++; } catch { }
            }
            foreach (var dir in Directory.EnumerateDirectories(paths.Backups, ".archive-*", SearchOption.AllDirectories))
            {
                try { Directory.Delete(dir, true); deletedFiles++; } catch { }
            }
        }

        var dbDir = Path.GetDirectoryName(paths.Database);
        if (!string.IsNullOrEmpty(dbDir) && Directory.Exists(dbDir))
        {
            foreach (var file in Directory.EnumerateFiles(dbDir, ".restore-*.*", SearchOption.TopDirectoryOnly))
            {
                try { File.Delete(file); deletedFiles++; } catch { }
            }
        }

        // Clean unreferenced RAW files
        if (Directory.Exists(paths.Raw))
        {
            var validShas = await db.RawCaptures.AsNoTracking().Select(r => r.Sha256).ToHashSetAsync(ct);
            foreach (var gzFile in Directory.EnumerateFiles(paths.Raw, "*.json.gz", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(gzFile);
                var sha = fileName.Split('.')[0];
                if (!validShas.Contains(sha))
                {
                    try { File.Delete(gzFile); deletedFiles++; } catch { }
                }
            }
        }

        return new DatabaseActionResult(true, $"Caché y temporales limpiados ({deletedFiles} elementos removidos).");
    }

    public async Task<DatabaseActionResult> ResetDatabaseAsync(CancellationToken ct = default)
    {
        var backupFile = await backupService.CreateArchiveAsync("pre-reset-database", ct);

        await using var conn = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = paths.Database,
            Pooling = false
        }.ToString());

        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM BasketItems;
            DELETE FROM Baskets;
            DELETE FROM PriceObservations;
            DELETE FROM Offers;
            DELETE FROM ProductMatches;
            DELETE FROM MatchOverrides;
            DELETE FROM ExternalSkus;
            DELETE FROM ExternalProducts;
            DELETE FROM ProductVariants;
            DELETE FROM CanonicalProducts;
            DELETE FROM RawCaptures;
            DELETE FROM SyncRuns;
            DELETE FROM ProviderHealth;
            DELETE FROM Stores;
            DELETE FROM Sellers;
            DELETE FROM LocationProfiles;
            DELETE FROM ComparisonProfiles;
        ";
        await cmd.ExecuteNonQueryAsync(ct);

        try
        {
            cmd.CommandText = "DELETE FROM ProductSearch;";
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch { }

        return new DatabaseActionResult(true, "Base de datos restablecida completamente. Copia preventiva creada.", backupFile);
    }

    private static long GetDirectorySize(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return 0;
        try
        {
            return Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories)
                .Select(f => { try { return new FileInfo(f).Length; } catch { return 0L; } })
                .Sum();
        }
        catch { return 0; }
    }
}
