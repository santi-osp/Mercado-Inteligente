using MercadoInteligente.Application;
using MercadoInteligente.Infrastructure;

namespace MercadoInteligente.Api;

public static class AiEndpoints
{
    public static void MapAiEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/ai/status", (OpenRouterAiProvider provider, AiSettingsService settingsService, AiUsageTracker tracker) =>
        {
            var settings = settingsService.Load();
            var stats = tracker.GetStats();
            return Results.Ok(new
            {
                providerName = provider.ProviderDisplayName,
                isConfigured = provider.IsConfigured,
                enabled = settings.Enabled,
                model = settings.Model,
                baseUrl = settings.BaseUrl,
                timeoutSeconds = settings.TimeoutSeconds,
                maxTokens = settings.MaxTokens,
                temperature = settings.Temperature,
                features = new
                {
                    normalization = settings.NormalizationEnabled,
                    matching = settings.MatchingEnabled,
                    substitutes = settings.SubstituteEnabled,
                    explanation = settings.ExplanationEnabled
                },
                monthlyBudgetUsd = settings.MonthlyBudgetUsd,
                monthlyCostUsd = tracker.GetMonthlyCostUsd(),
                budgetWarning = settings.WarningBudgetUsd is > 0 && tracker.GetMonthlyCostUsd() >= settings.WarningBudgetUsd,
                hardBudgetLimit = settings.HardBudgetLimit,
                usage = stats
            });
        });

        api.MapPut("/ai/settings", (AiSettings newSettings, AiSettingsService settingsService) =>
        {
            if (newSettings.MonthlyBudgetUsd is < 0 || newSettings.WarningBudgetUsd is < 0)
                return Results.BadRequest(new { code = "INVALID_BUDGET" });
            settingsService.Save(newSettings);
            return Results.Ok(newSettings);
        });

        api.MapGet("/ai/models", async (OpenRouterAiProvider provider, CancellationToken ct) =>
            Results.Ok(await provider.ListModelsAsync(ct)));

        api.MapPost("/ai/test-connection", async (OpenRouterAiProvider provider, CancellationToken ct) =>
            Results.Ok(await provider.TestConnectionAsync(ct)));

        api.MapGet("/ai/usage", (AiUsageTracker tracker) => Results.Ok(tracker.GetStats()));

        api.MapPost("/ai/cache/clear", (AiCacheService cache) =>
        {
            cache.Clear();
            return Results.Ok(new { cleared = true });
        });

        api.MapPost("/ai/normalize", async (AiProductNormalizationRequest request, ProductNormalizationAiService service, CancellationToken ct) =>
            Results.Ok(await service.NormalizeAsync(request, ct)));

        api.MapPost("/ai/adjudicate", async (AiMatchAdjudicationRequest request, MatchAdjudicationAiService service, CancellationToken ct) =>
            Results.Ok(await service.AdjudicateAsync(request, ct)));

        api.MapPost("/ai/substitute", async (AiSubstituteSuggestionRequest request, SubstituteSuggestionAiService service, CancellationToken ct) =>
            Results.Ok(await service.SuggestSubstitutesAsync(request, ct)));

                api.MapPost("/ai/shopping-assistant", async (ShoppingAssistantRequest request, ShoppingAssistantAiService service, CancellationToken ct) =>
            Results.Ok(await service.AssistAsync(request, ct)));

        api.MapPost("/ai/explain", async (AiBasketExplanationRequest request, BasketExplanationAiService service, CancellationToken ct) =>
            Results.Ok(await service.ExplainBasketAsync(request, ct)));
    }
}
