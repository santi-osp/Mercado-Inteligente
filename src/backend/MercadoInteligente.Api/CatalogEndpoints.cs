using MercadoInteligente.Application;
using MercadoInteligente.Infrastructure;

namespace MercadoInteligente.Api;

public static class CatalogEndpoints
{
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/catalog/categories", async (string retailer, CatalogImportService importService, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(retailer))
            {
                return Results.BadRequest(new { code = "INVALID_REQUEST", message = "Se requiere el parámetro retailer." });
            }

            var categories = await importService.GetCategoriesAsync(retailer, ct);
            return Results.Ok(categories);
        });

        api.MapPost("/catalog/import", (CatalogImportRequest request, CatalogImportService importService) =>
        {
            if (string.IsNullOrWhiteSpace(request.RetailerCode))
            {
                return Results.BadRequest(new { code = "INVALID_REQUEST", message = "Se requiere RetailerCode." });
            }

            var job = importService.StartImport(request.RetailerCode, request.CategoryIds ?? [], request.SelectAll);
            return Results.Ok(job);
        });

        api.MapGet("/catalog/import", (CatalogImportService importService) =>
        {
            return Results.Ok(importService.GetJobs());
        });

        api.MapGet("/catalog/import/{id:guid}", (Guid id, CatalogImportService importService) =>
        {
            var job = importService.GetJob(id);
            return job != null ? Results.Ok(job) : Results.NotFound(new { code = "JOB_NOT_FOUND", message = "Trabajo de importación no encontrado." });
        });

        api.MapPost("/catalog/import/{id:guid}/cancel", (Guid id, CatalogImportService importService) =>
        {
            var cancelled = importService.CancelJob(id);
            return cancelled
                ? Results.Ok(new { success = true, message = "Importación cancelada." })
                : Results.NotFound(new { code = "JOB_NOT_FOUND", message = "Trabajo de importación no encontrado o ya terminado." });
        });

        return api;
    }
}