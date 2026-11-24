using MercadoInteligente.Infrastructure;
using Microsoft.AspNetCore.Http;

namespace MercadoInteligente.Api;

public static class BackupEndpoints
{
    public sealed record CreateBackupRequest(string? Label);
    public sealed record RestoreBackupRequest(string? Confirmation);

    public static RouteGroupBuilder MapBackupEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/backups");

        group.MapGet("", (BackupService backups) => 
            Results.Ok(backups.ListBackups()));

        group.MapPost("", async (HttpRequest httpReq, BackupService backups, CancellationToken ct) =>
        {
            string? label = null;
            if (httpReq.HasJsonContentType())
            {
                try
                {
                    var req = await httpReq.ReadFromJsonAsync<CreateBackupRequest>(ct);
                    label = req?.Label;
                }
                catch { }
            }
            var file = await backups.CreateAsync(label, ct);
            return Results.Ok(new { file, label });
        });

        group.MapPost("/archive", async (HttpRequest httpReq, BackupService backups, CancellationToken ct) =>
        {
            string? label = null;
            if (httpReq.HasJsonContentType())
            {
                try
                {
                    var req = await httpReq.ReadFromJsonAsync<CreateBackupRequest>(ct);
                    label = req?.Label;
                }
                catch { }
            }
            var file = await backups.CreateArchiveAsync(label, ct);
            return Results.Ok(new { file, label });
        });

        group.MapGet("/{name}/verify", async (string name, BackupService backups, CancellationToken ct) =>
        {
            var result = await backups.VerifyBackupAsync(name, ct);
            return Results.Ok(result);
        });

        group.MapPost("/{name}/verify", async (string name, BackupService backups, CancellationToken ct) =>
        {
            var result = await backups.VerifyBackupAsync(name, ct);
            return Results.Ok(result);
        });

        group.MapPost("/{name}/restore", async (string name, HttpRequest httpReq, BackupService backups, CancellationToken ct) =>
        {
            try
            {
                var verify = await backups.VerifyBackupAsync(name, ct);
                if (!verify.Valid)
                {
                    return Results.BadRequest(new 
                    { 
                        code = "BACKUP_CORRUPT", 
                        message = $"Esta copia parece estar dañada y no se restaurará. Detalle de integridad: {verify.Integrity}" 
                    });
                }

                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    await backups.RestoreArchiveAsync(name, ct);
                }
                else
                {
                    await backups.RestoreAsync(name, ct);
                }

                return Results.Ok(new { message = "Copia restaurada exitosamente.", backup = name });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { code = "INVALID_BACKUP_NAME", message = ex.Message });
            }
            catch (FileNotFoundException ex)
            {
                return Results.NotFound(new { code = "BACKUP_NOT_FOUND", message = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { code = "RESTORE_FAILED", message = ex.Message });
            }
        });

        group.MapDelete("/{name}", (string name, BackupService backups) =>
        {
            try
            {
                var deleted = backups.DeleteBackup(name);
                return deleted ? Results.NoContent() : Results.NotFound(new { code = "BACKUP_NOT_FOUND", message = "La copia especificada no existe." });
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { code = "INVALID_BACKUP_PATH", message = ex.Message });
            }
        });

        group.MapPost("/open-folder", (BackupService backups) =>
        {
            var opened = backups.OpenBackupsFolder();
            return Results.Ok(new { opened });
        });

        return api;
    }
}