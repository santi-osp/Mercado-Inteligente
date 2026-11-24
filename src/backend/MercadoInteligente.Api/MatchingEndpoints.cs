using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Api;

public static class MatchingEndpoints
{
    public static RouteGroupBuilder MapMatchingEndpoints(this RouteGroupBuilder api)
    {
    api.MapGet("/matching/evaluate", () =>
        Results.Ok(MatchingEvaluationService.EvaluateDataset()));
    
    api.MapPost("/matching/compare", (CompareProductsRequest request) =>
    {
        var normA = ProductNormalizer.Normalize(request.NameA, request.BrandA, request.EanA);
        var normB = ProductNormalizer.Normalize(request.NameB, request.BrandB, request.EanB);
        var matchResult = DeterministicMatcher.Evaluate(normA, normB);
    
        UnitPriceCalculation? unitPriceA = request.PriceA.HasValue ? UnitComparisonService.CalculateUnitPrice(request.PriceA.Value, normA.Quantity) : null;
        UnitPriceCalculation? unitPriceB = request.PriceB.HasValue ? UnitComparisonService.CalculateUnitPrice(request.PriceB.Value, normB.Quantity) : null;
    
        OverbuyEvaluation? overbuyA = (request.TargetQuantity.HasValue && request.PriceA.HasValue && normA.Quantity != null)
            ? UnitComparisonService.EvaluateOverbuy(request.TargetQuantity.Value, normA.Quantity.TotalAmount, request.PriceA.Value)
            : null;
    
        OverbuyEvaluation? overbuyB = (request.TargetQuantity.HasValue && request.PriceB.HasValue && normB.Quantity != null)
            ? UnitComparisonService.EvaluateOverbuy(request.TargetQuantity.Value, normB.Quantity.TotalAmount, request.PriceB.Value)
            : null;
    
        return Results.Ok(new
        {
            productA = new { normalized = normA, unitPrice = unitPriceA, overbuy = overbuyA },
            productB = new { normalized = normB, unitPrice = unitPriceB, overbuy = overbuyB },
            match = matchResult
        });
    });
    
    api.MapGet("/matching/pairs", () =>
    {
        var report = MatchingEvaluationService.EvaluateDataset();
        var exactos = report.Details.Where(x => x.Actual == MatchKind.Exact).ToList();
        var similares = report.Details.Where(x => x.Actual == MatchKind.Substitute && x.Description.Contains("tamaño", StringComparison.OrdinalIgnoreCase)).ToList();
        var otrasMarcas = report.Details.Where(x => x.Actual == MatchKind.Substitute && !x.Description.Contains("tamaño", StringComparison.OrdinalIgnoreCase)).ToList();
    
        return Results.Ok(new
        {
            summary = new { report.TotalPairs, report.ExactPrecision, report.ExactRecall, report.ExactF1, report.ExactFalsePositives },
            exactos,
            similares,
            otrasMarcas,
            all = report.Details
        });
    });
    
    api.MapGet("/matching/overrides", async (MarketDb db) =>
        Results.Ok(await db.MatchOverrides.AsNoTracking().ToListAsync()));
    
    api.MapPost("/matching/overrides", async (CreateMatchOverrideRequest input, MarketDb db) =>
    {
        var existing = await db.MatchOverrides.FirstOrDefaultAsync(x => x.ExternalSkuId == input.ExternalSkuId && x.ProductVariantId == input.ProductVariantId);
        if (existing != null)
        {
            existing.Kind = input.Kind;
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            existing = new MatchOverride
            {
                ExternalSkuId = input.ExternalSkuId,
                ProductVariantId = input.ProductVariantId,
                Kind = input.Kind,
                CreatedAt = DateTime.UtcNow
            };
            db.MatchOverrides.Add(existing);
        }
        await db.SaveChangesAsync();
        return Results.Ok(existing);
    });
    
    api.MapDelete("/matching/overrides/{id:guid}", async (Guid id, MarketDb db) =>
    {
        var existing = await db.MatchOverrides.FindAsync(id);
        if (existing == null) return Results.NotFound();
        db.MatchOverrides.Remove(existing);
        await db.SaveChangesAsync();
        return Results.NoContent();
    });

        return api;
    }
}
