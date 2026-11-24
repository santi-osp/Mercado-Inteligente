using System.Globalization;
using System.Text.RegularExpressions;
using MercadoInteligente.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure;

public sealed record StorageStats(
    long DatabaseBytes,
    long DatabaseSidecarsBytes,
    long RawBytes,
    int RawFilesCount,
    long LogsBytes,
    int LogFilesCount,
    long BackupsBytes,
    int BackupFilesCount,
    int ExternalProductsCount,
    int ExternalSkusCount,
    int OffersCount,
    int PriceObservationsCount,
    int RawCapturesCount,
    int SyncRunsCount
);

public sealed record MaintenanceReport(
    bool DatabaseOptimized,
    int BackupsPruned,
    int BackupsRetained,
    int LogsPruned,
    int RawExpiredPruned,
    int RawOrphansPruned,
    string Summary
);

public interface IMaintenanceService
{
    Task<StorageStats> GetStorageStatsAsync(CancellationToken ct = default);
    Task<bool> OptimizeDatabaseAsync(CancellationToken ct = default);
    Task<(int Pruned, int Retained)> PruneBackupsAsync(int dailyRetention = 7, int weeklyRetention = 4, int monthlyRetention = 3, CancellationToken ct = default);
    Task<int> PruneLogsAsync(int retentionDays = 30, CancellationToken ct = default);
    Task<(int ExpiredPruned, int OrphansPruned)> PruneRawAsync(int retentionDays = 60, CancellationToken ct = default);
    Task<MaintenanceReport> ExecuteMaintenanceAsync(CancellationToken ct = default);
}

public sealed class MaintenanceService(LocalPaths paths, MarketDb db, RawStore rawStore) : IMaintenanceService
{
    private static readonly Regex BackupFileRegex = new(
        @"^market-(?<year>\d{4})(?<month>\d{2})(?<day>\d{2})-(?<hour>\d{2})(?<minute>\d{2})(?<second>\d{2})-(?<msec>\d{3})-[0-9a-f]{8}\.(db|zip)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public async Task<StorageStats> GetStorageStatsAsync(CancellationToken ct = default)
    {
        var dbBytes = File.Exists(paths.Database) ? new FileInfo(paths.Database).Length : 0L;
        var sidecarsBytes = 0L;
        foreach (var ext in new[] { "-wal", "-shm" })
        {
            var sidecar = paths.Database + ext;
            if (File.Exists(sidecar)) sidecarsBytes += new FileInfo(sidecar).Length;
        }

        var (rawBytes, rawCount) = GetDirectorySize(paths.Raw);
        var (logsBytes, logsCount) = GetDirectorySize(paths.Logs);
        var (backupsBytes, backupsCount) = GetDirectorySize(paths.Backups);

        var productsCount = await db.ExternalProducts.CountAsync(ct);
        var skusCount = await db.ExternalSkus.CountAsync(ct);
        var offersCount = await db.Offers.CountAsync(ct);
        var obsCount = await db.PriceObservations.CountAsync(ct);
        var rawCountDb = await db.RawCaptures.CountAsync(ct);
        var syncCount = await db.SyncRuns.CountAsync(ct);

        return new StorageStats(
            dbBytes,
            sidecarsBytes,
            rawBytes,
            rawCount,
            logsBytes,
            logsCount,
            backupsBytes,
            backupsCount,
            productsCount,
            skusCount,
            offersCount,
            obsCount,
            rawCountDb,
            syncCount
        );
    }

    public async Task<bool> OptimizeDatabaseAsync(CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State == System.Data.ConnectionState.Closed;
        if (wasClosed) await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA optimize;";
            await command.ExecuteNonQueryAsync(ct);
            return true;
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
    }

    public async Task<(int Pruned, int Retained)> PruneBackupsAsync(
        int dailyRetention = 7,
        int weeklyRetention = 4,
        int monthlyRetention = 3,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(paths.Backups))
            return (0, 0);

        var allFiles = Directory.GetFiles(paths.Backups);
        var parsedBackups = new List<(string FilePath, DateTime Timestamp)>();

        foreach (var file in allFiles)
        {
            var fileName = Path.GetFileName(file);
            var match = BackupFileRegex.Match(fileName);
            if (match.Success)
            {
                if (DateTime.TryParseExact(
                    $"{match.Groups["year"].Value}{match.Groups["month"].Value}{match.Groups["day"].Value}{match.Groups["hour"].Value}{match.Groups["minute"].Value}{match.Groups["second"].Value}",
                    "yyyyMMddHHmmss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var dt))
                {
                    parsedBackups.Add((file, dt));
                }
            }
        }

        var retainedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Daily tier: most recent per day
        var byDay = parsedBackups
            .GroupBy(b => b.Timestamp.Date)
            .OrderByDescending(g => g.Key)
            .Take(dailyRetention);
        foreach (var group in byDay)
        {
            var latest = group.OrderByDescending(b => b.Timestamp).First();
            retainedPaths.Add(latest.FilePath);
        }

        // Weekly tier: most recent per ISO week
        var byWeek = parsedBackups
            .GroupBy(b => ISOWeek.GetYear(b.Timestamp) * 100 + ISOWeek.GetWeekOfYear(b.Timestamp))
            .OrderByDescending(g => g.Key)
            .Take(weeklyRetention);
        foreach (var group in byWeek)
        {
            var latest = group.OrderByDescending(b => b.Timestamp).First();
            retainedPaths.Add(latest.FilePath);
        }

        // Monthly tier: most recent per month
        var byMonth = parsedBackups
            .GroupBy(b => b.Timestamp.Year * 100 + b.Timestamp.Month)
            .OrderByDescending(g => g.Key)
            .Take(monthlyRetention);
        foreach (var group in byMonth)
        {
            var latest = group.OrderByDescending(b => b.Timestamp).First();
            retainedPaths.Add(latest.FilePath);
        }

        var pruned = 0;
        var now = DateTime.UtcNow;

        foreach (var (filePath, timestamp) in parsedBackups)
        {
            ct.ThrowIfCancellationRequested();
            // Preserve very recent backups created today regardless
            if (timestamp >= now.Date)
            {
                retainedPaths.Add(filePath);
                continue;
            }

            if (!retainedPaths.Contains(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    pruned++;
                }
                catch (IOException) { }
            }
        }

        // Clean up older pre-restore/pre-migration temporary snapshots (> 7 days old)
        foreach (var file in allFiles)
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith("pre-restore-", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("pre-migration-", StringComparison.OrdinalIgnoreCase))
            {
                var writeTime = File.GetLastWriteTimeUtc(file);
                if (now - writeTime > TimeSpan.FromDays(7))
                {
                    try
                    {
                        File.Delete(file);
                        pruned++;
                    }
                    catch (IOException) { }
                }
                else
                {
                    retainedPaths.Add(file);
                }
            }
        }

        return (pruned, retainedPaths.Count);
    }

    public Task<int> PruneLogsAsync(int retentionDays = 30, CancellationToken ct = default)
    {
        if (!Directory.Exists(paths.Logs))
            return Task.FromResult(0);

        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var pruned = 0;

        foreach (var file in Directory.EnumerateFiles(paths.Logs, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                    pruned++;
                }
            }
            catch (Exception)
            {
                // Active log file locked by logger; skip safely
            }
        }

        return Task.FromResult(pruned);
    }

    public async Task<(int ExpiredPruned, int OrphansPruned)> PruneRawAsync(int retentionDays = 60, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retentionDays);
        var expiredPruned = 0;
        var orphansPruned = 0;

        try
        {
            expiredPruned = await rawStore.CleanupExpiredUnreferencedAsync(cutoff, ct);
        }
        catch (InvalidOperationException)
        {
            // Expected when backup snapshots exist under RawStore guard
        }

        try
        {
            orphansPruned = await rawStore.CleanupOrphansAsync(cutoff, ct);
        }
        catch (InvalidOperationException)
        {
            // Expected when backup snapshots exist under RawStore guard
        }

        return (expiredPruned, orphansPruned);
    }

    public async Task<MaintenanceReport> ExecuteMaintenanceAsync(CancellationToken ct = default)
    {
        var settings = await db.Settings.FirstOrDefaultAsync(ct) ?? new AppSettings();

        var dbOptimized = await OptimizeDatabaseAsync(ct);
        var (backupsPruned, backupsRetained) = await PruneBackupsAsync(
            settings.BackupDailyRetention > 0 ? settings.BackupDailyRetention : 7,
            settings.BackupWeeklyRetention > 0 ? settings.BackupWeeklyRetention : 4,
            settings.BackupMonthlyRetention > 0 ? settings.BackupMonthlyRetention : 3,
            ct);

        var logsPruned = await PruneLogsAsync(settings.LogRetentionDays > 0 ? settings.LogRetentionDays : 30, ct);
        var (rawExpired, rawOrphans) = await PruneRawAsync(settings.RawRetentionDays > 0 ? settings.RawRetentionDays : 60, ct);

        var summary = $"Mantenimiento completado: SQLite optimizado={dbOptimized}, Backups eliminados/retenidos={backupsPruned}/{backupsRetained}, Logs eliminados={logsPruned}, RAW eliminados={rawExpired + rawOrphans}.";

        return new MaintenanceReport(dbOptimized, backupsPruned, backupsRetained, logsPruned, rawExpired, rawOrphans, summary);
    }

    private static (long TotalBytes, int FileCount) GetDirectorySize(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return (0, 0);
        var total = 0L;
        var count = 0;
        foreach (var file in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            try
            {
                total += new FileInfo(file).Length;
                count++;
            }
            catch (IOException) { }
        }
        return (total, count);
    }
}
