using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace MercadoInteligente.Infrastructure;

// Creation and offline restore for portable archives (.zip).
public partial class BackupService
{
    private const int MaxCaptures = 100_000;
    private const int MaxManifest = 16 * 1024 * 1024;
    private const long MaxRaw = 64L * 1024 * 1024;
    private const long MaxDatabase = 8L * 1024 * 1024 * 1024;
    private static readonly Regex HashPattern = new(@"\A[0-9a-f]{64}\z", RegexOptions.CultureInvariant);
    private static readonly Regex ArchiveNamePattern = new(@"\Amarket-[0-9]{8}-[0-9]{6}-[0-9]{3}-[0-9a-f]{8}\.zip\z", RegexOptions.CultureInvariant);
    internal sealed record RawEntry(string Sha256, string Path, long Bytes, string FileSha256);
    internal sealed record ArchiveManifest(int Version, string DatabasePath, long DatabaseBytes, string DatabaseSha256, RawEntry[] Raw);

    public async Task<string> CreateArchiveAsync(CancellationToken ct = default) => await CreateArchiveAsync(null, ct);

    public async Task<string> CreateArchiveAsync(string? label, CancellationToken ct = default)
    {
        var gate = RawBackupGate.For(paths);
        await gate.WaitAsync(ct);
        try
        {
            var name = await CreateArchiveCoreAsync(ct);
            if (!string.IsNullOrWhiteSpace(label))
            {
                var metaFile = Path.Combine(paths.Backups, name + ".meta.json");
                var meta = new { label = label.Trim(), createdAtUtc = DateTime.UtcNow, schemaVersion = "1.0.0" };
                await File.WriteAllTextAsync(metaFile, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }), ct);
            }
            return name;
        }
        finally { gate.Release(); }
    }

    public async Task RestoreArchiveAsync(string archiveName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(archiveName) || !ArchiveNamePattern.IsMatch(archiveName))
            throw new ArgumentException("Invalid archive file name", nameof(archiveName));

        var gate = RawBackupGate.For(paths);
        await gate.WaitAsync(ct);
        try { await RestoreArchiveCoreAsync(archiveName, ct); }
        finally { gate.Release(); }
    }

    private async Task RestoreArchiveCoreAsync(string archiveName, CancellationToken ct)
    {
        var archivePath = Path.Combine(paths.Backups, archiveName);
        if (!File.Exists(archivePath) || (File.GetAttributes(archivePath) & FileAttributes.ReparsePoint) != 0)
            throw new FileNotFoundException("Archive not found or is a link", archivePath);

        // 1. Validate the archive boundedly before touching any live paths.
        var manifest = await ValidateArchiveAsync(archivePath, ct);

        // 2. Stage database.sqlite extraction in the database folder (same volume as target).
        var dbDirectory = Path.GetDirectoryName(paths.Database)!;
        Directory.CreateDirectory(dbDirectory);
        var stagedDb = Path.Combine(dbDirectory, ".restore-" + Guid.NewGuid().ToString("N") + ".db");
        var stagedRawFiles = new List<(string StagePath, string TargetPath)>();

        try
        {
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                var dbEntry = archive.GetEntry("database.sqlite") ?? throw new InvalidDataException("Missing SQLite entry in archive.");
                await using (var output = new FileStream(stagedDb, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await using (var input = dbEntry.Open())
                {
                    await input.CopyToAsync(output, ct);
                }
            }

            // Verify the staged database offline.
            await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = stagedDb, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            {
                await connection.OpenAsync(ct);
                await CheckDatabaseAsync(connection, ct);
            }
            SqliteConnection.ClearAllPools();

            // 3. Verify existing local RAW files and stage missing RAW entries.
            if (Directory.Exists(paths.Raw) && (File.GetAttributes(paths.Raw) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("RAW root directory is a link.");

            using (var archive = ZipFile.OpenRead(archivePath))
            {
                foreach (var item in manifest.Raw)
                {
                    ct.ThrowIfCancellationRequested();
                    var prefix = item.Sha256[..2];
                    var folder = Path.Combine(paths.Raw, prefix);
                    if (Directory.Exists(folder) && (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException($"RAW directory {prefix} is a link.");

                    var localTarget = Path.Combine(folder, item.Sha256 + ".json.gz");
                    if (File.Exists(localTarget))
                    {
                        if ((File.GetAttributes(localTarget) & FileAttributes.ReparsePoint) != 0)
                            throw new InvalidDataException($"Existing RAW file {item.Sha256} is a link.");

                        // Verify existing local file content: fail closed on corrupt or mismatched local RAW.
                        var isCorrupt = false;
                        try
                        {
                            await using var localStream = File.OpenRead(localTarget);
                            await using var localGzip = new GZipStream(localStream, CompressionMode.Decompress);
                            using var localDigest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                            var buffer = new byte[16384];
                            long decompressedLength = 0;
                            int read;
                            while ((read = await localGzip.ReadAsync(buffer, ct)) != 0)
                            {
                                decompressedLength += read;
                                if (decompressedLength > 16 * 1024 * 1024) { isCorrupt = true; break; }
                                localDigest.AppendData(buffer, 0, read);
                            }
                            if (isCorrupt || Convert.ToHexString(localDigest.GetHashAndReset()).ToLowerInvariant() != item.Sha256)
                                isCorrupt = true;
                        }
                        catch
                        {
                            isCorrupt = true;
                        }

                        if (isCorrupt)
                            throw new InvalidDataException($"Existing local RAW file for {item.Sha256} is corrupt; cannot overwrite (fail closed).");

                        // Valid existing local RAW: do not overwrite.
                        continue;
                    }

                    // Missing RAW file: stage in target folder.
                    Directory.CreateDirectory(folder);
                    var stageFile = localTarget + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    var rawEntry = archive.GetEntry(item.Path) ?? throw new InvalidDataException($"Missing RAW entry {item.Path} in archive.");
                    await using (var output = new FileStream(stageFile, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await using (var input = rawEntry.Open())
                    {
                        await input.CopyToAsync(output, ct);
                    }

                    // Verify staged file hash.
                    await using (var checkStream = File.OpenRead(stageFile))
                    {
                        var digest = Convert.ToHexString(await SHA256.HashDataAsync(checkStream, ct)).ToLowerInvariant();
                        if (digest != item.FileSha256)
                            throw new InvalidDataException($"Staged RAW file hash mismatch for {item.Sha256}.");
                    }

                    stagedRawFiles.Add((stageFile, localTarget));
                }
            }

            // 4. Preexisting live DB: take verified preventive backup and checkpoint WAL.
            ct.ThrowIfCancellationRequested();
            if (File.Exists(paths.Database))
            {
                var rollback = Path.Combine(paths.Backups, "pre-restore-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8] + ".db");
                await using (var live = new SqliteConnection(new SqliteConnectionStringBuilder
                    { DataSource = paths.Database, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString()))
                {
                    await live.OpenAsync(ct);
                    await using var checkpoint = live.CreateCommand();
                    checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE)";
                    await using var result = await checkpoint.ExecuteReaderAsync(ct);
                    if (await result.ReadAsync(ct) && result.GetInt64(0) != 0)
                        throw new IOException("Live database WAL could not be checkpointed; stop the app first");
                    live.BackupDatabase(new SqliteConnection(new SqliteConnectionStringBuilder
                        { DataSource = rollback, Pooling = false }.ToString()));
                }
                SqliteConnection.ClearAllPools();

                await using (var verifyRollback = new SqliteConnection(new SqliteConnectionStringBuilder
                    { DataSource = rollback, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
                {
                    await verifyRollback.OpenAsync(ct);
                    await CheckDatabaseAsync(verifyRollback, ct);
                }
                SqliteConnection.ClearAllPools();

                var wal = paths.Database + "-wal";
                if (File.Exists(wal) && new FileInfo(wal).Length != 0)
                    throw new IOException("Live WAL still exists; stop all database users before restoring");
            }

            // 5. Publish missing RAW files before DB swap.
            ct.ThrowIfCancellationRequested();
            foreach (var (stagePath, targetPath) in stagedRawFiles)
            {
                File.Move(stagePath, targetPath, overwrite: false);
            }

            // 6. Swap SQLite database atomically on the same filesystem volume.
            if (File.Exists(paths.Database))
            {
                RemoveEmptySidecars(paths.Database);
                File.Replace(stagedDb, paths.Database, null);
            }
            else
            {
                RemoveEmptySidecars(paths.Database);
                File.Move(stagedDb, paths.Database);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(stagedDb)) File.Delete(stagedDb);
            foreach (var suffix in new[] { "-wal", "-shm" })
                if (File.Exists(stagedDb + suffix)) File.Delete(stagedDb + suffix);
            foreach (var (stagePath, _) in stagedRawFiles)
                if (File.Exists(stagePath)) File.Delete(stagePath);
        }
    }

    private async Task<string> CreateArchiveCoreAsync(CancellationToken ct)
    {
        var name = "market-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N")[..8] + ".zip";
        var destination = Path.Combine(paths.Backups, name);
        var snapshot = Path.Combine(paths.Backups, ".archive-" + Guid.NewGuid().ToString("N") + ".db");
        var stage = Path.Combine(paths.Backups, ".archive-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = paths.Database, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            await using (var target = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = snapshot, Pooling = false }.ToString()))
            {
                await source.OpenAsync(ct);
                await target.OpenAsync(ct);
                source.BackupDatabase(target);
                await CheckDatabaseAsync(target, ct);
                await CheckRawEvidenceAsync(target, ct);
            }
            SqliteConnection.ClearAllPools();
            var size = new FileInfo(snapshot).Length;
            if (size > MaxDatabase) throw new InvalidDataException("Archive SQLite exceeds size limit.");
            var raw = new List<RawEntry>();
            await using (var snapshotDb = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = snapshot, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            {
                await snapshotDb.OpenAsync(ct);
                await using var command = snapshotDb.CreateCommand();
                command.CommandText = "SELECT Sha256, RelativePath FROM RawCaptures ORDER BY Sha256";
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    if (raw.Count >= MaxCaptures) throw new InvalidDataException("Too many RAW entries.");
                    var hash = reader.GetString(0);
                    if (!HashPattern.IsMatch(hash) || reader.GetString(1) != Path.Combine(hash[..2], hash + ".json.gz") ||
                        (raw.Count > 0 && string.CompareOrdinal(raw[^1].Sha256, hash) >= 0))
                        throw new InvalidDataException("Invalid RAW index.");
                    var folder = Path.Combine(paths.Raw, hash[..2]);
                    var file = Path.Combine(folder, hash + ".json.gz");
                    if (!File.Exists(file) || (File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0 ||
                        (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("RAW missing or linked.");
                    var bytes = new FileInfo(file).Length;
                    if (bytes > MaxRaw) throw new InvalidDataException("RAW exceeds size limit.");
                    raw.Add(new RawEntry(hash, "raw/" + hash[..2] + "/" + hash + ".json.gz", bytes, ""));
                }
            }
            await using (var output = new FileStream(stage, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                var dbHash = await AddFileAsync(archive, snapshot, "database.sqlite", ct);
                for (var i = 0; i < raw.Count; i++)
                {
                    var item = raw[i];
                    var file = Path.Combine(paths.Raw, item.Sha256[..2], item.Sha256 + ".json.gz");
                    raw[i] = item with { FileSha256 = await AddFileAsync(archive, file, item.Path, ct) };
                }
                var manifest = JsonSerializer.SerializeToUtf8Bytes(new ArchiveManifest(1, "database.sqlite", size, dbHash, raw.ToArray()));
                if (manifest.Length > MaxManifest) throw new InvalidDataException("Manifest exceeds size limit.");
                await using var entry = archive.CreateEntry("manifest.json", CompressionLevel.NoCompression).Open();
                await entry.WriteAsync(manifest, ct);
            }
            await ValidateArchiveAsync(stage, ct);
            ct.ThrowIfCancellationRequested();
            File.Move(stage, destination);
            return name;
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(snapshot)) File.Delete(snapshot);
            foreach (var suffix in new[] { "-wal", "-shm" }) if (File.Exists(snapshot + suffix)) File.Delete(snapshot + suffix);
            foreach (var suffix in new[] { "-wal", "-shm" }) if (File.Exists(stage + ".verify.db" + suffix)) File.Delete(stage + ".verify.db" + suffix);
            if (File.Exists(stage)) File.Delete(stage);
        }
    }

    private static async Task<string> AddFileAsync(ZipArchive archive, string file, string name, CancellationToken ct)
    {
        await using var source = File.OpenRead(file);
        await using var target = archive.CreateEntry(name, CompressionLevel.NoCompression).Open();
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) != 0)
        {
            digest.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        return Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
    }

    internal static async Task<ArchiveManifest> ValidateArchiveAsync(string stage, CancellationToken ct)
    {
        using var archive = ZipFile.OpenRead(stage);
        var entry = archive.GetEntry("manifest.json");
        if (entry is null || entry.Length > MaxManifest) throw new InvalidDataException("Invalid manifest entry.");
        await using var manifestStream = entry.Open();
        using var manifestBuffer = new MemoryStream();
        await manifestStream.CopyToAsync(manifestBuffer, ct);
        if (manifestBuffer.Length > MaxManifest) throw new InvalidDataException("Manifest exceeds limit.");
        var manifest = JsonSerializer.Deserialize<ArchiveManifest>(manifestBuffer.ToArray()) ?? throw new InvalidDataException("Invalid manifest.");
        if (manifest.Version != 1 || manifest.DatabasePath != "database.sqlite" || manifest.Raw is null ||
            manifest.Raw.Length > MaxCaptures || archive.Entries.Count != manifest.Raw.Length + 2 ||
            archive.Entries.Select(x => x.FullName).Distinct(StringComparer.Ordinal).Count() != archive.Entries.Count)
            throw new InvalidDataException("Invalid archive layout.");
        if (archive.Entries.Any(e => e.FullName.Contains('\\') || e.FullName.StartsWith('/') || e.FullName.Contains("..")))
            throw new InvalidDataException("Unsafe entry path in archive.");
        var dbEntry = archive.GetEntry("database.sqlite") ?? throw new InvalidDataException("Missing SQLite.");
        await VerifyEntryAsync(dbEntry, manifest.DatabaseBytes, manifest.DatabaseSha256, MaxDatabase, ct);
        var prior = "";
        foreach (var item in manifest.Raw)
        {
            if (!HashPattern.IsMatch(item.Sha256) || string.CompareOrdinal(prior, item.Sha256) >= 0 ||
                item.Path != "raw/" + item.Sha256[..2] + "/" + item.Sha256 + ".json.gz")
                throw new InvalidDataException("Invalid RAW path.");
            prior = item.Sha256;
            var rawEntry = archive.GetEntry(item.Path) ?? throw new InvalidDataException("Missing RAW.");
            await VerifyEntryAsync(rawEntry, item.Bytes, item.FileSha256, MaxRaw, ct);
            await using var gzip = new GZipStream(rawEntry.Open(), CompressionMode.Decompress);
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[16384];
            long length = 0;
            int read;
            while ((read = await gzip.ReadAsync(buffer, ct)) != 0)
            {
                length += read;
                if (length > 16 * 1024 * 1024) throw new InvalidDataException("RAW verification limit exceeded.");
                digest.AppendData(buffer, 0, read);
            }
            if (Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant() != item.Sha256)
                throw new InvalidDataException("RAW content hash mismatch.");
        }
        var verify = Path.Combine(Path.GetDirectoryName(stage)!, ".verify-" + Guid.NewGuid().ToString("N") + ".db");
        try
        {
            await using (var output = new FileStream(verify, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            await using (var input = dbEntry.Open()) await input.CopyToAsync(output, ct);
            await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                { DataSource = verify, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            await connection.OpenAsync(ct);
            await CheckDatabaseAsync(connection, ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT Sha256, RelativePath FROM RawCaptures ORDER BY Sha256";
            await using var reader = await command.ExecuteReaderAsync(ct);
            foreach (var item in manifest.Raw)
                if (!await reader.ReadAsync(ct) || reader.GetString(0) != item.Sha256 ||
                    reader.GetString(1) != Path.Combine(item.Sha256[..2], item.Sha256 + ".json.gz"))
                    throw new InvalidDataException("SQLite RAW index mismatch.");
            if (await reader.ReadAsync(ct)) throw new InvalidDataException("Extra SQLite RAW index row.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(verify)) File.Delete(verify);
            foreach (var suffix in new[] { "-wal", "-shm" })
                if (File.Exists(verify + suffix)) File.Delete(verify + suffix);
        }
        return manifest;
    }

    private static async Task VerifyEntryAsync(ZipArchiveEntry entry, long bytes, string hash, long limit, CancellationToken ct)
    {
        if (bytes < 0 || bytes > limit || entry.Length != bytes || !HashPattern.IsMatch(hash))
            throw new InvalidDataException("Invalid archive entry metadata.");
        await using var stream = entry.Open();
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        long length = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, ct)) != 0)
        {
            length += read;
            if (length > bytes) throw new InvalidDataException("Archive entry exceeds limit.");
            digest.AppendData(buffer, 0, read);
        }
        if (length != bytes || Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant() != hash)
            throw new InvalidDataException("Archive entry hash mismatch.");
    }
}
