using System.IO.Compression;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;
namespace MercadoInteligente.Infrastructure;
public class LocalPaths
{
    public string Root { get; }
    public string Database => Path.Combine(Root, "data", "market.db");
    public string Raw => Path.Combine(Root, "data", "raw");
    public string Backups => Path.Combine(Root, "backups");
    public string Logs => Path.Combine(Root, "logs");
    public LocalPaths(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MercadoInteligente");
        foreach (var directory in new[] { Path.GetDirectoryName(Database)!, Raw, Backups, Logs }) Directory.CreateDirectory(directory);
    }
}
internal static class RawBackupGate
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);
    internal static SemaphoreSlim For(LocalPaths paths) => Gates.GetOrAdd(Path.GetFullPath(paths.Root).TrimEnd(Path.DirectorySeparatorChar),
        _ => new SemaphoreSlim(1, 1));
}
public class RawStore(LocalPaths paths, MarketDb db)
{
    internal SemaphoreSlim Gate => RawBackupGate.For(paths);
    public async Task<RawCapture> SaveAsync(byte[] payload, CancellationToken ct = default) =>
        (await SaveWithDedupAsync(payload, ct)).Capture;

    internal async Task<(RawCapture Capture, bool Deduplicated)> SaveWithDedupAsync(byte[] payload, CancellationToken ct = default)
    {
        var gate = RawBackupGate.For(paths);
        await gate.WaitAsync(ct);
        try { return await SaveWithDedupCoreAsync(payload, ct); }
        finally { gate.Release(); }
    }

    // The D1 ingest holds the same gate across its SQLite transaction so backup cannot
    // hold the gate while waiting for that transaction to finish.
    internal Task<(RawCapture Capture, bool Deduplicated)> SaveUnderGateAsync(byte[] payload, CancellationToken ct) =>
        SaveWithDedupCoreAsync(payload, ct);

    private async Task<(RawCapture Capture, bool Deduplicated)> SaveWithDedupCoreAsync(byte[] payload, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        var relative = Path.Combine(hash[..2], hash + ".json.gz");
        var fullPath = Path.Combine(paths.Raw, relative);
        var existing = await db.RawCaptures.FirstOrDefaultAsync(x => x.Sha256 == hash, ct);
        if (existing is not null && File.Exists(fullPath)) return (existing, true);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = File.Create(temporary))
            await using (var gzip = new GZipStream(file, CompressionLevel.Optimal))
                await gzip.WriteAsync(payload, ct);
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        if (existing is not null) return (existing, true);
        var capture = new RawCapture { Sha256 = hash, RelativePath = relative };
        db.RawCaptures.Add(capture);
        await db.SaveChangesAsync(ct);
        return (capture, false);
    }

    // SQLite-only snapshots can still reference RAW absent from the current database.
    // Until backup inventory/RAW companions are coordinated, never prune across one.
    private void RequireNoSqliteBackups()
    {
        // Conservative guard: even an unknown backup file can contain an older RAW index.
        if (Directory.EnumerateFileSystemEntries(paths.Backups, "*", SearchOption.TopDirectoryOnly).Any())
            throw new InvalidOperationException("RAW cleanup is disabled while backup files exist without coordinated RAW retention.");
    }

    // Trim only unreferenced indexed captures; metadata is removed before the file so a failed
    // file deletion leaves a safe orphan for CleanupOrphansAsync, never a dangling DB reference.
    public async Task<int> CleanupExpiredUnreferencedAsync(DateTime olderThanUtc, CancellationToken ct = default)
    {
        if (olderThanUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A UTC cutoff is required.", nameof(olderThanUtc));
        var gate = RawBackupGate.For(paths);
        await gate.WaitAsync(ct);
        try { return await CleanupExpiredCoreAsync(olderThanUtc, ct); }
        finally { gate.Release(); }
    }

    private async Task<int> CleanupExpiredCoreAsync(DateTime olderThanUtc, CancellationToken ct)
    {
        RequireNoSqliteBackups();
        var captures = await db.RawCaptures.AsNoTracking().Where(x => x.CapturedAt < olderThanUtc &&
            !db.Offers.Any(offer => offer.RawCaptureId == x.Id) &&
            !db.PriceObservations.Any(observation => observation.RawCaptureId == x.Id)).ToListAsync(ct);
        var removed = 0;
        foreach (var capture in captures)
        {
            ct.ThrowIfCancellationRequested();
            var hash = capture.Sha256;
            if (hash.Length != 64 || !System.Text.RegularExpressions.Regex.IsMatch(hash, "^[0-9a-f]{64}$") ||
                capture.RelativePath != Path.Combine(hash[..2], hash + ".json.gz")) continue;
            var file = Path.Combine(paths.Raw, capture.RelativePath);
            if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
            var deleted = await db.RawCaptures.Where(x => x.Id == capture.Id && x.CapturedAt < olderThanUtc &&
                !db.Offers.Any(offer => offer.RawCaptureId == x.Id) &&
                !db.PriceObservations.Any(observation => observation.RawCaptureId == x.Id)).ExecuteDeleteAsync(ct);
            if (deleted == 0) continue;
            removed++;
            if (File.Exists(file)) File.Delete(file);
        }
        return removed;
    }

    // Only remove old, unindexed hash-named files; referenced captures remain available to history.
    public async Task<int> CleanupOrphansAsync(DateTime olderThanUtc, CancellationToken ct = default)
    {
        if (olderThanUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A UTC cutoff is required.", nameof(olderThanUtc));
        var gate = RawBackupGate.For(paths);
        await gate.WaitAsync(ct);
        try { return await CleanupOrphansCoreAsync(olderThanUtc, ct); }
        finally { gate.Release(); }
    }

    private async Task<int> CleanupOrphansCoreAsync(DateTime olderThanUtc, CancellationToken ct)
    {
        RequireNoSqliteBackups();
        var hashes = await db.RawCaptures.AsNoTracking().Select(x => x.Sha256).ToHashSetAsync(ct);
        var removed = 0;
        foreach (var folder in Directory.EnumerateDirectories(paths.Raw))
        {
            ct.ThrowIfCancellationRequested();
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0 ||
                !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(folder), "^[0-9a-f]{2}$")) continue;
            foreach (var file in Directory.EnumerateFiles(folder, "*.json.gz", SearchOption.TopDirectoryOnly))
            {
                ct.ThrowIfCancellationRequested();
                var name = Path.GetFileName(file);
                if (name.Length != 72 || !name.EndsWith(".json.gz", StringComparison.Ordinal)) continue;
                var hash = name[..64];
                if (!System.Text.RegularExpressions.Regex.IsMatch(hash, "^[0-9a-f]{64}$") ||
                    !hash.StartsWith(Path.GetFileName(folder), StringComparison.Ordinal) || hashes.Contains(hash) ||
                    (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 ||
                    File.GetLastWriteTimeUtc(file) >= olderThanUtc) continue;
                File.Delete(file);
                removed++;
            }
        }
        return removed;
    }
}
public sealed record BackupFileInfo(
    string Name,
    long Bytes,
    DateTime CreatedAtUtc,
    string Type,
    bool? IntegrityVerified = null,
    string? SchemaVersion = null,
    string? Label = null
);

public sealed record BackupVerificationResult(
    string Name,
    bool Valid,
    string Integrity,
    bool ForeignKeysValid,
    string? Message
);

public partial class BackupService(LocalPaths paths, MarketDb db)
{
    private static readonly System.Text.RegularExpressions.Regex DbNamePattern =
        new(@"\Amarket-[0-9]{8}-[0-9]{6}-[0-9]{3}-[0-9a-f]{8}\.db\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public IReadOnlyList<BackupFileInfo> ListBackups()
    {
        if (!Directory.Exists(paths.Backups)) return Array.Empty<BackupFileInfo>();
        var results = new List<BackupFileInfo>();
        foreach (var file in Directory.EnumerateFiles(paths.Backups, "market-*.*", SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) continue;
            var name = Path.GetFileName(file);
            var isZip = ArchiveNamePattern.IsMatch(name);
            var isDb = DbNamePattern.IsMatch(name);
            if (!isZip && !isDb) continue;
            var info = new FileInfo(file);
            string? label = null;
            string? schemaVersion = "1.0.0";
            var metaFile = file + ".meta.json";
            if (File.Exists(metaFile))
            {
                try
                {
                    var metaJson = File.ReadAllText(metaFile);
                    using var doc = System.Text.Json.JsonDocument.Parse(metaJson);
                    if (doc.RootElement.TryGetProperty("label", out var lProp)) label = lProp.GetString();
                    if (doc.RootElement.TryGetProperty("schemaVersion", out var sProp)) schemaVersion = sProp.GetString();
                }
                catch { }
            }
            results.Add(new BackupFileInfo(name, info.Length, info.CreationTimeUtc, isZip ? "Archive" : "Database", null, schemaVersion, label));
        }
        return results.OrderByDescending(x => x.Name).ToList();
    }

    public async Task<string> IntegrityAsync(CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try { await using var command = connection.CreateCommand(); command.CommandText = "PRAGMA integrity_check"; return (string)(await command.ExecuteScalarAsync(ct) ?? "unknown"); }
        finally { await connection.CloseAsync(); }
    }
    public async Task<bool> ForeignKeysValidAsync(CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_key_check";
            await using var reader = await command.ExecuteReaderAsync(ct);
            return !await reader.ReadAsync(ct);
        }
        finally { await connection.CloseAsync(); }
    }
    // Offline operation only: the host and all other users of the database must be stopped.
    // RAW captures are not copied; verify local evidence before replacing the live database.
    public async Task RestoreAsync(string backupName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(backupName))
            throw new ArgumentException("Nombre de copia no especificado.", nameof(backupName));

        if (!DbNamePattern.IsMatch(backupName))
            throw new ArgumentException("Nombre de copia no valido.", nameof(backupName));

        var sourceFile = Path.Combine(paths.Backups, backupName);
        var fullSourcePath = Path.GetFullPath(sourceFile);
        if (!string.Equals(Path.GetDirectoryName(fullSourcePath), Path.GetFullPath(paths.Backups), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Ruta no permitida.", nameof(backupName));

        if (!File.Exists(sourceFile) || (File.GetAttributes(sourceFile) & FileAttributes.ReparsePoint) != 0)
            throw new FileNotFoundException("Backup not found or is a link", sourceFile);

        var verify = await VerifyBackupAsync(backupName, ct);
        if (!verify.Valid)
            throw new InvalidOperationException($"La copia de seguridad '{backupName}' falló la verificación de integridad ({verify.Integrity}). Restauración cancelada por seguridad.");

        var stage = Path.Combine(Path.GetDirectoryName(paths.Database)!, ".restore-" + Guid.NewGuid().ToString("N") + ".db");
        var rollback = Path.Combine(paths.Backups, "pre-restore-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8] + ".db");
        try
        {
            // An SQLite snapshot folds a backup's WAL into the staging database, without trusting
            // its sidecars or copying any paths recorded in its rows.
            await using (var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sourceFile, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            await using (var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = stage, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
            {
                await source.OpenAsync(ct);
                await target.OpenAsync(ct);
                await CheckDatabaseAsync(source, ct);
                await CheckRawEvidenceAsync(source, ct);
                source.BackupDatabase(target);
                await CheckDatabaseAsync(target, ct);
            }
            ct.ThrowIfCancellationRequested();
            SqliteConnection.ClearAllPools();
            // Checkpoint live WAL before touching files; a failure leaves the live DB unchanged.
            if (File.Exists(paths.Database))
            {
                await using (var live = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = paths.Database, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
                {
                    await live.OpenAsync(ct);
                    await using var checkpoint = live.CreateCommand();
                    checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                    await using var result = await checkpoint.ExecuteReaderAsync(ct);
                    if (await result.ReadAsync(ct) && result.GetInt64(0) != 0)
                        throw new IOException("Live database WAL could not be checkpointed; stop the app first");
                    await using var rollbackConn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = rollback, Pooling = false, DefaultTimeout = 5 }.ToString());
                    live.BackupDatabase(rollbackConn);
                }
                SqliteConnection.ClearAllPools();
                // A lingering sidecar could replay old transactions into the restored database.
                // Reject rather than discard any nonempty WAL (possible concurrent writer).
                var wal = paths.Database + "-wal";
                if (File.Exists(wal) && new FileInfo(wal).Length != 0)
                    throw new IOException("Live WAL still exists; stop all database users before restoring");
                // Remove only empty sidecars before the swap, so cleanup failures cannot
                // report an error after replacement has already happened.
                RemoveEmptySidecars(paths.Database);
                // File.Replace is atomic on the same volume and leaves the preventive snapshot intact.
                File.Replace(stage, paths.Database, null);
            }
            else
            {
                RemoveEmptySidecars(paths.Database);
                File.Move(stage, paths.Database);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(stage)) File.Delete(stage);
        }
    }

    private async Task CheckRawEvidenceAsync(SqliteConnection source, CancellationToken ct)
    {
        await using var command = source.CreateCommand();
        command.CommandText = "SELECT Sha256, RelativePath FROM RawCaptures";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var hash = reader.GetString(0);
            var relative = reader.GetString(1);
            if (hash.Length != 64 || !System.Text.RegularExpressions.Regex.IsMatch(hash, "^[0-9a-f]{64}$") ||
                relative != Path.Combine(hash[..2], hash + ".json.gz"))
                throw new InvalidDataException("Backup has invalid RAW metadata.");
            var directory = Path.Combine(paths.Raw, hash[..2]);
            var file = Path.Combine(directory, hash + ".json.gz");
            try
            {
                if (!File.Exists(file) || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
                    (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Backup RAW evidence is missing or linked.");
                await using var compressed = File.OpenRead(file);
                await using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
                using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[16 * 1024];
                var length = 0L;
                int read;
                while ((read = await gzip.ReadAsync(buffer, ct)) != 0)
                {
                    length += read;
                    if (length > 16 * 1024 * 1024)
                        throw new InvalidDataException("Backup RAW evidence exceeds the verification limit.");
                    digest.AppendData(buffer, 0, read);
                }
                if (!Convert.ToHexString(digest.GetHashAndReset()).Equals(hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Backup RAW evidence hash does not match.");
            }
            catch (InvalidDataException) { throw; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            { throw new InvalidDataException("Backup RAW evidence cannot be read.", exception); }
        }
    }

    private static void RemoveEmptySidecars(string database)
    {
        var wal = database + "-wal";
        if (File.Exists(wal) && new FileInfo(wal).Length != 0)
            throw new IOException("Live WAL still exists; stop all database users before restoring");
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var sidecar = database + suffix;
            if (File.Exists(sidecar)) File.Delete(sidecar);
        }
    }

    private static async Task CheckDatabaseAsync(SqliteConnection connection, CancellationToken ct)
    {
        foreach (var pragma in new[] { "PRAGMA integrity_check", "PRAGMA foreign_key_check" })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = pragma;
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct) && (pragma.Contains("foreign_key") || reader.GetString(0) != "ok"))
                throw new InvalidDataException($"Backup failed {pragma}");
        }
    }
    public async Task<string> CreateAsync(CancellationToken ct = default) => await CreateAsync(null, ct);

    public async Task<string> CreateAsync(string? label, CancellationToken ct = default)
    {
        var gate = RawBackupGate.For(paths);
        await gate.WaitAsync(ct);
        try
        {
            var name = await CreateCoreAsync(ct);
            if (!string.IsNullOrWhiteSpace(label))
            {
                var metaFile = Path.Combine(paths.Backups, name + ".meta.json");
                var meta = new { label = label.Trim(), createdAtUtc = DateTime.UtcNow, schemaVersion = "1.0.0" };
                await File.WriteAllTextAsync(metaFile, System.Text.Json.JsonSerializer.Serialize(meta, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), ct);
            }
            return name;
        }
        finally { gate.Release(); }
    }

    public async Task<BackupVerificationResult> VerifyBackupAsync(string backupName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(backupName))
            throw new ArgumentException("Nombre de copia no especificado.", nameof(backupName));

        var isDb = DbNamePattern.IsMatch(backupName);
        var isZip = ArchiveNamePattern.IsMatch(backupName);
        if (!isDb && !isZip)
            throw new ArgumentException("Nombre de copia no válido.", nameof(backupName));

        var fullPath = Path.GetFullPath(Path.Combine(paths.Backups, backupName));
        if (!string.Equals(Path.GetDirectoryName(fullPath), Path.GetFullPath(paths.Backups), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Ruta no permitida.", nameof(backupName));

        if (!File.Exists(fullPath) || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
            throw new FileNotFoundException("Copia de seguridad no encontrada.", fullPath);

        if (isZip)
        {
            try
            {
                var manifest = await ValidateArchiveAsync(fullPath, ct);
                return new BackupVerificationResult(backupName, true, "ok", true, $"Archivo .zip íntegro. Versión de manifiesto: {manifest.Version}, RAW ítems: {manifest.Raw.Length}");
            }
            catch (Exception ex)
            {
                return new BackupVerificationResult(backupName, false, "corrupt", false, $"Archivo corrupto o incompleto: {ex.Message}");
            }
        }

        try
        {
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = fullPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            }.ToString());

            await connection.OpenAsync(ct);
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "PRAGMA quick_check";
            var quickResult = (string)(await cmd.ExecuteScalarAsync(ct) ?? "error");

            var integrity = quickResult;
            if (quickResult == "ok")
            {
                cmd.CommandText = "PRAGMA integrity_check";
                integrity = (string)(await cmd.ExecuteScalarAsync(ct) ?? "error");
            }

            cmd.CommandText = "PRAGMA foreign_key_check";
            await using var fkReader = await cmd.ExecuteReaderAsync(ct);
            var fkValid = !await fkReader.ReadAsync(ct);

            var valid = integrity == "ok" && fkValid;
            var message = valid
                ? "Base de datos íntegra y verificada (PRAGMA integrity_check: ok, claves foráneas correctas)."
                : $"Error de integridad: {integrity}. Claves foráneas válidas: {fkValid}.";

            return new BackupVerificationResult(backupName, valid, integrity, fkValid, message);
        }
        catch (Exception ex)
        {
            return new BackupVerificationResult(backupName, false, "error", false, $"Fallo al examinar la base de datos: {ex.Message}");
        }
    }

    public bool DeleteBackup(string backupName)
    {
        if (string.IsNullOrWhiteSpace(backupName))
            throw new ArgumentException("Nombre de copia no especificado.", nameof(backupName));

        var isDb = DbNamePattern.IsMatch(backupName);
        var isZip = ArchiveNamePattern.IsMatch(backupName);
        if (!isDb && !isZip)
            throw new ArgumentException("Nombre de copia no válido.", nameof(backupName));

        var fullPath = Path.GetFullPath(Path.Combine(paths.Backups, backupName));
        if (!string.Equals(Path.GetDirectoryName(fullPath), Path.GetFullPath(paths.Backups), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Ruta no permitida.", nameof(backupName));

        if (!File.Exists(fullPath)) return false;

        File.Delete(fullPath);
        var metaFile = fullPath + ".meta.json";
        if (File.Exists(metaFile)) File.Delete(metaFile);

        return true;
    }

    public bool OpenBackupsFolder()
    {
        Directory.CreateDirectory(paths.Backups);
        if (OperatingSystem.IsWindows())
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = paths.Backups,
                UseShellExecute = true
            });
            return true;
        }
        return false;
    }

    private async Task<string> CreateCoreAsync(CancellationToken ct)
    {
        var name = "market-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8] + ".db";
        var destination = Path.Combine(paths.Backups, name);
        var stage = Path.Combine(paths.Backups, ".backup-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            // Validate the exact SQLite snapshot that will be published, not a potentially
            // different live index read before SQLite's backup operation.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    await using var source = new SqliteConnection(new SqliteConnectionStringBuilder
                        { DataSource = paths.Database, Mode = SqliteOpenMode.ReadOnly, Pooling = false, DefaultTimeout = 5 }.ToString());
                    await using var target = new SqliteConnection(new SqliteConnectionStringBuilder
                        { DataSource = stage, Pooling = false, DefaultTimeout = 5 }.ToString());
                    await source.OpenAsync(ct);
                    await target.OpenAsync(ct);
                    source.BackupDatabase(target);
                    await CheckDatabaseAsync(target, ct);
                    await CheckRawEvidenceAsync(target, ct);
                    break;
                }
                catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 && attempt < 2)
                {
                    if (File.Exists(stage)) File.Delete(stage);
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1)), ct);
                }
            }
            ct.ThrowIfCancellationRequested();
            File.Move(stage, destination);
            return name;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(stage)) File.Delete(stage);
        }
    }

}
public class WindowsSecretStore(LocalPaths paths) : ISecretStore
{
    private string FileFor(string name) => Path.Combine(paths.Root, "secrets", name + ".bin");
    private static void Validate(string name) { if (name is not ("openrouter" or "google" or "omnirouter")) throw new ArgumentException("Unknown secret type"); }
    public void Set(string name, string value)
    {
        Validate(name);
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI requires Windows");
        Directory.CreateDirectory(Path.GetDirectoryName(FileFor(name))!);
        File.WriteAllBytes(FileFor(name), ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    }
    public string? Get(string name)
    {
        Validate(name);
        if (!File.Exists(FileFor(name))) return null;
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DPAPI requires Windows");
        return System.Text.Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(FileFor(name)), null, DataProtectionScope.CurrentUser));
    }
    public void Delete(string name) { Validate(name); File.Delete(FileFor(name)); }
}
public class NullAiProvider : IAiProvider { public bool IsConfigured => false; public Task<string?> ExplainAsync(string context, CancellationToken cancellationToken) => Task.FromResult<string?>(null); }
public class NullGeocodingProvider : IGeocodingProvider { public Task<(decimal Latitude, decimal Longitude)?> GeocodeAsync(string address, CancellationToken cancellationToken) => Task.FromResult<(decimal, decimal)?>(null); }
public class NullRoutingProvider : IRoutingProvider { public Task<decimal?> DistanceKmAsync(decimal a, decimal b, decimal c, decimal d, CancellationToken cancellationToken) => Task.FromResult<decimal?>(null); }
public class SyncCoordinator(MarketDb db) : ISyncCoordinator
{
    public async Task<SyncRun> StartAsync(string provider, CancellationToken ct)
    {
        var run = new SyncRun { Provider = provider }; db.SyncRuns.Add(run); await db.SaveChangesAsync(ct); return run;
    }
}
