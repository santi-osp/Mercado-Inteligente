using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public record AiSettings(
    bool Enabled = false,
    string BaseUrl = "https://omnirouter.li/v1",
    string Model = "gemini-3.8-flash",
    int TimeoutSeconds = 15,
    int MaxTokens = 1000,
    double Temperature = 0.1,
    bool NormalizationEnabled = true,
    bool MatchingEnabled = true,
    bool SubstituteEnabled = true,
    bool ExplanationEnabled = true,
    decimal? MonthlyBudgetUsd = 10.0m,
    bool AskBeforeAi = false,
    decimal? WarningBudgetUsd = null,
    bool HardBudgetLimit = false
);

public record AiDailyUsage(
    string Date,
    int Calls,
    int PromptTokens,
    int CompletionTokens,
    decimal CostUsd
);

public record AiUsageStats(
    int TotalCalls,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd,
    decimal EstimatedCostCop,
    int CacheHits,
    int ErrorsCount,
    IReadOnlyList<AiDailyUsage> RecentDailyUsage
);

public record AiCompletionRequest(
    string Feature,
    string PromptVersion,
    string SystemPrompt,
    string UserPrompt,
    string? InputHash = null,
    int? MaxTokens = null,
    double? Temperature = null
);

public record AiCompletionResult(
    bool Success,
    string? RawContent,
    string? ErrorCode = null,
    int PromptTokens = 0,
    int CompletionTokens = 0,
    long LatencyMs = 0,
    bool IsFromCache = false,
    string ModelUsed = "",
    string PromptVersion = ""
);

public record AiConnectionTestResult(
    bool Success,
    int StatusCode,
    string Message,
    long LatencyMs,
    string? ModelTested
);

// 1. Ambiguous Product Normalization
public record AiProductNormalizationRequest(
    string Name,
    string? Brand = null,
    string? RawQuantity = null,
    string? Ean = null
);

public record AiProductNormalizationResult(
    string NormalizedType,
    string? BrandCandidate,
    decimal? Quantity,
    string? Unit,
    string? Package,
    IReadOnlyList<string> Attributes,
    double Confidence,
    string ShortExplanation,
    bool NeedsReview,
    string Source = "AI",
    string? ModelUsed = null,
    string? PromptVersion = null
);

// 2. Match Adjudication
public record AiMatchAdjudicationRequest(
    string NameA,
    string? BrandA,
    string? EanA,
    decimal? QuantityA,
    string? UnitA,
    string NameB,
    string? BrandB,
    string? EanB,
    decimal? QuantityB,
    string? UnitB,
    string DeterministicKind,
    string DeterministicEvidence
);

public record AiMatchAdjudicationResult(
    string Decision, // EXACT | SUBSTITUTE | NOT_EQUIVALENT | UNSURE
    double Confidence,
    string ConciseReason,
    IReadOnlyList<string> AttributesInCommon,
    IReadOnlyList<string> AttributesDiffering,
    string Source = "AI",
    string? ModelUsed = null,
    string? PromptVersion = null
);

// 3. Functional Substitute Suggestion
public record AiSubstituteCandidate(
    string ProductId,
    string Name,
    string? Brand,
    decimal Price,
    string? PackageSize,
    string? Store
);

public record AiSubstituteSuggestionRequest(
    string TargetName,
    string? TargetBrand,
    string? TargetUnit,
    decimal? TargetQuantity,
    IReadOnlyList<AiSubstituteCandidate> Candidates
);

public record AiSubstituteItem(
    string ProductId,
    string Name,
    double Confidence,
    string FunctionalEquivalence, // DirectAlternative | BudgetOption | PremiumOption
    string Reason
);

public record AiSubstituteSuggestionResult(
    IReadOnlyList<AiSubstituteItem> Suggestions,
    string Source = "AI",
    string? ModelUsed = null,
    string? PromptVersion = null
);

// 4. Basket Optimization Explanation
public record AiBasketExplanationRequest(
    string Strategy,
    decimal TotalCost,
    decimal ProductCost,
    decimal TravelCost,
    decimal? DistanceKm,
    int? DurationMinutes,
    int StoreCount,
    IReadOnlyList<string> Stores,
    decimal? SingleStoreCost,
    decimal? EstimatedSavings,
    int ItemCount,
    IReadOnlyList<string> Warnings
);

public record AiBasketExplanationResult(
    string Summary,
    string WhyRecommended,
    string StoreTradeoffs,
    IReadOnlyList<string> KeySavingsSources,
    IReadOnlyList<string> Warnings,
    string Source = "AI",
    string? ModelUsed = null,
    string? PromptVersion = null
);
