using MercadoInteligente.Infrastructure;

namespace MercadoInteligente.Api;

public static class DatabaseEndpoints
{
    public sealed record DangerZoneConfirmationRequest(string? Confirmation);

    public static RouteGroupBuilder MapDatabaseEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/database");

        group.MapGet("/stats", async (DatabaseManagementService service, CancellationToken ct) =>
        {
            var stats = await service.GetStatsAsync(ct);
            return Results.Ok(stats);
        });

        group.MapPost("/optimize", async (DatabaseManagementService service, CancellationToken ct) =>
        {
            var result = await service.OptimizeAsync(ct);
            return Results.Ok(result);
        });

        group.MapPost("/clear-catalog", async (DangerZoneConfirmationRequest? request, DatabaseManagementService service, CancellationToken ct) =>
        {
            if (!string.Equals(request?.Confirmation?.Trim(), "BORRAR", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new
                {
                    code = "CONFIRMATION_REQUIRED",
                    message = "Esta acción es destructiva. Debes escribir 'BORRAR' para confirmar."
                });
            }

            var result = await service.ClearCatalogAsync(ct);
            return Results.Ok(result);
        });

        group.MapPost("/clear-history", async (DangerZoneConfirmationRequest? request, DatabaseManagementService service, CancellationToken ct) =>
        {
            if (!string.Equals(request?.Confirmation?.Trim(), "BORRAR", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new
                {
                    code = "CONFIRMATION_REQUIRED",
                    message = "Esta acción es destructiva. Debes escribir 'BORRAR' para confirmar."
                });
            }

            var result = await service.ClearHistoryAsync(ct);
            return Results.Ok(result);
        });

        group.MapPost("/clear-cache", async (DatabaseManagementService service, CancellationToken ct) =>
        {
            var result = await service.ClearCacheAsync(ct);
            return Results.Ok(result);
        });

        group.MapPost("/clear-baskets", async (DangerZoneConfirmationRequest? request, DatabaseManagementService service, CancellationToken ct) =>
        {
            if (!string.Equals(request?.Confirmation?.Trim(), "BORRAR", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new
                {
                    code = "CONFIRMATION_REQUIRED",
                    message = "Esta acción es destructiva. Debes escribir 'BORRAR' para confirmar."
                });
            }

            var result = await service.ClearBasketsAsync(ct);
            return Results.Ok(result);
        });

        group.MapPost("/reset", async (DangerZoneConfirmationRequest? request, DatabaseManagementService service, CancellationToken ct) =>
        {
            if (!string.Equals(request?.Confirmation?.Trim(), "BORRAR", StringComparison.OrdinalIgnoreCase))
            {
                return Results.BadRequest(new
                {
                    code = "CONFIRMATION_REQUIRED",
                    message = "Esta acción es destructiva. Debes escribir 'BORRAR' para confirmar."
                });
            }

            var result = await service.ResetDatabaseAsync(ct);
            return Results.Ok(result);
        });

        return api;
    }
}