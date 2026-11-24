using MercadoInteligente.Application;

namespace MercadoInteligente.Api;

public static class AnalysisEndpoints
{
    public static RouteGroupBuilder MapAnalysisEndpoints(this RouteGroupBuilder api)
    {
        var group = api.MapGroup("/analysis");

        group.MapGet("/product/{id:guid}", async (Guid id, IProductAnalysisService service, CancellationToken ct) =>
        {
            var result = await service.AnalyzeProductAsync(id, ct);
            return result != null ? Results.Ok(result) : Results.NotFound(new { code = "PRODUCT_NOT_FOUND", message = "Producto no encontrado." });
        });

        group.MapGet("/opportunities", async (IProductAnalysisService service, CancellationToken ct) =>
        {
            var result = await service.GetOpportunitiesAsync(ct);
            return Results.Ok(result);
        });

        group.MapGet("/basket/{id:guid}", async (Guid id, IProductAnalysisService service, CancellationToken ct) =>
        {
            var result = await service.AnalyzeBasketAsync(id, ct);
            return result != null ? Results.Ok(result) : Results.NotFound(new { code = "BASKET_NOT_FOUND", message = "Lista no encontrada." });
        });

        return api;
    }
}