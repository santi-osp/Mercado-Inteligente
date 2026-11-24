using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure;

public sealed class AiSettingsService
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public AiSettingsService(LocalPaths paths)
    {
        _filePath = Path.Combine(paths.Root, "data", "ai-settings.json");
    }

    public AiSettings Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath))
                return new AiSettings();

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<AiSettings>(json) ?? new AiSettings();
            }
            catch
            {
                return new AiSettings();
            }
        }
    }

    public void Save(AiSettings settings)
    {
        lock (_lock)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var tempPath = _filePath + ".tmp";
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
    }
}

public sealed class AiCacheService
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public record CacheEntry(
        string Content,
        int PromptTokens,
        int CompletionTokens,
        string Model,
        string TimestampUtc
    );

    public AiCacheService(LocalPaths paths)
    {
        _filePath = Path.Combine(paths.Root, "data", "ai-cache.json");
    }

    public static string CreateCacheKey(string feature, string promptVersion, string model, string inputHash)
    {
        var raw = $"{feature}:{promptVersion}:{model}:{inputHash}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    public CacheEntry? TryGet(string key)
    {
        lock (_lock)
        {
            var cache = LoadCache();
            return cache.TryGetValue(key, out var entry) ? entry : null;
        }
    }

    public void Set(string key, CacheEntry entry)
    {
        lock (_lock)
        {
            var cache = LoadCache();
            cache[key] = entry;
            SaveCache(cache);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            SaveCache(new Dictionary<string, CacheEntry>());
        }
    }

    private Dictionary<string, CacheEntry> LoadCache()
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Dictionary<string, CacheEntry>>(json)
                   ?? new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, CacheEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveCache(Dictionary<string, CacheEntry> cache)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tempPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(cache, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }
}

public sealed class AiUsageTracker
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private const decimal UsdToCopRate = 4100m;

    private class PersistedUsage
    {
        public int TotalCalls { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public decimal EstimatedCostUsd { get; set; }
        public int CacheHits { get; set; }
        public int ErrorsCount { get; set; }
        public List<AiDailyUsage> DailyUsages { get; set; } = new();
    }

    public AiUsageTracker(LocalPaths paths)
    {
        _filePath = Path.Combine(paths.Root, "data", "ai-usage.json");
    }

    public void RecordCall(int promptTokens, int completionTokens, decimal costUsd, bool isCacheHit, bool isError)
    {
        lock (_lock)
        {
            var usage = Load();
            var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

            if (isCacheHit)
            {
                usage.CacheHits++;
            }
            else
            {
                usage.TotalCalls++;
                usage.PromptTokens += promptTokens;
                usage.CompletionTokens += completionTokens;
                usage.EstimatedCostUsd += costUsd;

                if (isError)
                    usage.ErrorsCount++;

                var daily = usage.DailyUsages.FirstOrDefault(d => d.Date == today);
                if (daily == null)
                {
                    daily = new AiDailyUsage(today, 1, promptTokens, completionTokens, costUsd);
                    usage.DailyUsages.Add(daily);
                }
                else
                {
                    usage.DailyUsages.Remove(daily);
                    usage.DailyUsages.Add(new AiDailyUsage(
                        today,
                        daily.Calls + 1,
                        daily.PromptTokens + promptTokens,
                        daily.CompletionTokens + completionTokens,
                        daily.CostUsd + costUsd
                    ));
                }
            }

            Save(usage);
        }
    }

    public AiUsageStats GetStats()
    {
        lock (_lock)
        {
            var usage = Load();
            var recent = usage.DailyUsages.OrderByDescending(d => d.Date).Take(14).ToList();
            return new AiUsageStats(
                TotalCalls: usage.TotalCalls,
                PromptTokens: usage.PromptTokens,
                CompletionTokens: usage.CompletionTokens,
                EstimatedCostUsd: Math.Round(usage.DailyUsages.Sum(day => day.CostUsd), 4),
                EstimatedCostCop: Math.Round(usage.DailyUsages.Sum(day => day.CostUsd) * UsdToCopRate, 0),
                CacheHits: usage.CacheHits,
                ErrorsCount: usage.ErrorsCount,
                RecentDailyUsage: recent
            );
        }
    }

    public decimal GetMonthlyCostUsd()
    {
        lock (_lock)
        {
            var prefix = DateTime.UtcNow.ToString("yyyy-MM");
            return Load().DailyUsages.Where(day => day.Date.StartsWith(prefix, StringComparison.Ordinal))
                .Sum(day => day.CostUsd);
        }
    }

    public bool TryReserveMonthlyBudget(decimal budgetUsd, decimal estimatedMaximumCostUsd)
    {
        lock (_lock)
        {
            var usage = Load();
            var month = DateTime.UtcNow.ToString("yyyy-MM");
            var spent = usage.DailyUsages.Where(day => day.Date.StartsWith(month, StringComparison.Ordinal))
                .Sum(day => day.CostUsd);
            if (spent + estimatedMaximumCostUsd > budgetUsd) return false;
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var existing = usage.DailyUsages.FirstOrDefault(day => day.Date == date);
            if (existing is not null) usage.DailyUsages.Remove(existing);
            usage.DailyUsages.Add(new AiDailyUsage(date, existing?.Calls ?? 0,
                existing?.PromptTokens ?? 0, existing?.CompletionTokens ?? 0,
                (existing?.CostUsd ?? 0) + estimatedMaximumCostUsd));
            Save(usage);
            return true;
        }
    }

    public void SettleMonthlyBudget(decimal reservedUsd, decimal actualUsd)
    {
        lock (_lock)
        {
            var usage = Load();
            var date = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var existing = usage.DailyUsages.FirstOrDefault(day => day.Date == date);
            if (existing is null) return;
            usage.DailyUsages.Remove(existing);
            usage.DailyUsages.Add(existing with { CostUsd = Math.Max(0, existing.CostUsd - reservedUsd + actualUsd) });
            Save(usage);
        }
    }

    private PersistedUsage Load()
    {
        if (!File.Exists(_filePath))
            return new PersistedUsage();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<PersistedUsage>(json) ?? new PersistedUsage();
        }
        catch
        {
            return new PersistedUsage();
        }
    }

    private void Save(PersistedUsage usage)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tempPath = _filePath + ".tmp";
        var json = JsonSerializer.Serialize(usage, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _filePath, overwrite: true);
    }
}

public class OpenRouterAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly ISecretStore _secretStore;
    private readonly AiSettingsService _settingsService;
    private readonly AiCacheService _cacheService;
    private readonly AiUsageTracker _usageTracker;
    private readonly Dictionary<string, (DateTime ExpiresAt, decimal Prompt, decimal Completion)> _modelPrices = new();
    private readonly object _modelPricesLock = new();

    public const string SecretKeyName = "openrouter";

    public OpenRouterAiProvider(
        HttpClient httpClient,
        ISecretStore secretStore,
        AiSettingsService settingsService,
        AiCacheService cacheService,
        AiUsageTracker usageTracker)
    {
        _httpClient = httpClient;
        _secretStore = secretStore;
        _settingsService = settingsService;
        _cacheService = cacheService;
        _usageTracker = usageTracker;
    }

    public virtual string ProviderDisplayName => "OmniRouter";

    public string? GetApiKey()
    {
        return _secretStore.Get("omnirouter") ??
               _secretStore.Get("openrouter") ??
               Environment.GetEnvironmentVariable("OMNIROUTER_API_KEY") ??
               Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
    }

    public string GetEffectiveBaseUrl(AiSettings settings)
    {
        return Environment.GetEnvironmentVariable("OMNIROUTER_BASE_URL") ??
               Environment.GetEnvironmentVariable("OPENROUTER_BASE_URL") ??
               settings.BaseUrl;
    }

    public string GetEffectiveModel(AiSettings settings)
    {
        return Environment.GetEnvironmentVariable("OMNIROUTER_MODEL") ??
               Environment.GetEnvironmentVariable("OPENROUTER_MODEL") ??
               settings.Model;
    }

    public bool IsConfigured
    {
        get
        {
            var settings = _settingsService.Load();
            if (!settings.Enabled) return false;
            var key = GetApiKey();
            return !string.IsNullOrWhiteSpace(key);
        }
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken)
    {
        var settings = _settingsService.Load();
        var baseUrl = GetEffectiveBaseUrl(settings);
        var apiKey = GetApiKey();

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, CombineUrl(baseUrl, "models"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.Add("HTTP-Referer", "https://mercado-inteligente.local");
                request.Headers.Add("X-Title", "Mercado Inteligente");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var m in data.EnumerateArray())
                        {
                            if (m.TryGetProperty("id", out var idElem))
                            {
                                var idStr = idElem.GetString();
                                if (!string.IsNullOrWhiteSpace(idStr)) list.Add(idStr);
                            }
                        }
                        if (list.Count > 0)
                        {
                            return list.OrderByDescending(x => x.Contains("gemini", StringComparison.OrdinalIgnoreCase)).ToList();
                        }
                    }
                }
            }
            catch { }
        }

        return [
            "gemini-3.8-flash",
            "gemini-2.5-flash",
            "anthropic/claude-3.5-haiku",
            "openai/gpt-4o-mini"
        ];
    }

    public async Task<string?> ExplainAsync(string context, CancellationToken cancellationToken)
    {
        var req = new AiCompletionRequest(
            Feature: "general_explain",
            PromptVersion: "v1.0",
            SystemPrompt: "You are a grocery shopping assistant. Explain the following context clearly in Spanish.",
            UserPrompt: context,
            InputHash: AiPromptSafety.ComputeSha256(context)
        );

        var result = await CompleteAsync(req, cancellationToken);
        return result.Success ? result.RawContent : null;
    }

    public async Task<AiConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiConnectionTestResult(
                Success: false,
                StatusCode: 400,
                Message: "API key de OmniRouter no está configurada en SecretStore o entorno.",
                LatencyMs: 0,
                ModelTested: null
            );
        }

        var settings = _settingsService.Load();
        var effectiveBase = GetEffectiveBaseUrl(settings);
        var effectiveModel = GetEffectiveModel(settings);
        var sw = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, CombineUrl(effectiveBase, "chat/completions"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Add("HTTP-Referer", "https://mercado-inteligente.local");
            request.Headers.Add("X-Title", "Mercado Inteligente");

            var payload = new
            {
                model = effectiveModel,
                messages = new object[]
                {
                    new { role = "user", content = "ping" }
                },
                max_tokens = 5,
                temperature = 0.0
            };

            request.Content = JsonContent.Create(payload);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Min(10, settings.TimeoutSeconds)));

            var response = await _httpClient.SendAsync(request, cts.Token);
            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                return new AiConnectionTestResult(
                    Success: true,
                    StatusCode: (int)response.StatusCode,
                    Message: "Conexión a OmniRouter exitosa. Modelo responde correctamente.",
                    LatencyMs: sw.ElapsedMilliseconds,
                    ModelTested: effectiveModel
                );
            }

            var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var errMessage = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Error 401: API key inválida o revocada.",
                HttpStatusCode.PaymentRequired => "Error 402: Crédito insuficiente en OmniRouter.",
                HttpStatusCode.TooManyRequests => "Error 429: Límite de tasa (Rate limit) excedido.",
                _ => $"Error HTTP {(int)response.StatusCode}: {Truncate(errBody, 200)}"
            };

            return new AiConnectionTestResult(
                Success: false,
                StatusCode: (int)response.StatusCode,
                Message: errMessage,
                LatencyMs: sw.ElapsedMilliseconds,
                ModelTested: effectiveModel
            );
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            return new AiConnectionTestResult(
                Success: false,
                StatusCode: 408,
                Message: "Tiempo de espera agotado al conectar con OmniRouter.",
                LatencyMs: sw.ElapsedMilliseconds,
                ModelTested: effectiveModel
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new AiConnectionTestResult(
                Success: false,
                StatusCode: 500,
                Message: $"Excepción de red: {ex.Message}",
                LatencyMs: sw.ElapsedMilliseconds,
                ModelTested: effectiveModel
            );
        }
    }

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken)
    {
        var settings = _settingsService.Load();
        var effectiveModel = GetEffectiveModel(settings);
        var effectiveBase = GetEffectiveBaseUrl(settings);
        if (!settings.Enabled)
        {
            return new AiCompletionResult(
                Success: false,
                RawContent: null,
                ErrorCode: "AI_DISABLED",
                ModelUsed: effectiveModel,
                PromptVersion: request.PromptVersion
            );
        }

        // Check feature flag
        if (!IsFeatureEnabled(request.Feature, settings))
        {
            return new AiCompletionResult(
                Success: false,
                RawContent: null,
                ErrorCode: "FEATURE_DISABLED",
                ModelUsed: effectiveModel,
                PromptVersion: request.PromptVersion
            );
        }

        var apiKey = GetApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new AiCompletionResult(
                Success: false,
                RawContent: null,
                ErrorCode: "NO_API_KEY",
                ModelUsed: effectiveModel,
                PromptVersion: request.PromptVersion
            );
        }

        // Check Cache
        var inputHash = request.InputHash ?? AiPromptSafety.ComputeSha256(request.UserPrompt);
        var cacheKey = AiCacheService.CreateCacheKey(request.Feature, request.PromptVersion, settings.Model, inputHash);
        var cached = _cacheService.TryGet(cacheKey);
        if (cached != null)
        {
            _usageTracker.RecordCall(cached.PromptTokens, cached.CompletionTokens, 0m, isCacheHit: true, isError: false);
            return new AiCompletionResult(
                Success: true,
                RawContent: cached.Content,
                ErrorCode: null,
                PromptTokens: cached.PromptTokens,
                CompletionTokens: cached.CompletionTokens,
                LatencyMs: 0,
                IsFromCache: true,
                ModelUsed: cached.Model,
                PromptVersion: request.PromptVersion
            );
        }

        decimal reservedUsd = 0m;
        if (settings.HardBudgetLimit && settings.MonthlyBudgetUsd is > 0)
        {
            var pricing = await GetModelPricingAsync(settings, apiKey, cancellationToken);
            if (pricing is null)
                return new AiCompletionResult(false, null, "MODEL_PRICING_UNAVAILABLE", ModelUsed: effectiveModel,
                    PromptVersion: request.PromptVersion);
            var maximumPromptTokens = Encoding.UTF8.GetByteCount(request.SystemPrompt) +
                Encoding.UTF8.GetByteCount(request.UserPrompt) + 100;
            var maximumCompletionTokens = request.MaxTokens ?? settings.MaxTokens;
            reservedUsd = maximumPromptTokens * pricing.Value.Prompt +
                maximumCompletionTokens * pricing.Value.Completion;
            if (reservedUsd <= 0) reservedUsd = 0.00000001m;
        }
        if (reservedUsd > 0 && !_usageTracker.TryReserveMonthlyBudget(settings.MonthlyBudgetUsd!.Value, reservedUsd))
            return new AiCompletionResult(false, null, "BUDGET_EXCEEDED", ModelUsed: effectiveModel,
                PromptVersion: request.PromptVersion);

        var sw = Stopwatch.StartNew();

        try
        {
            using var httpReq = new HttpRequestMessage(HttpMethod.Post, CombineUrl(effectiveBase, "chat/completions"));
            httpReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpReq.Headers.Add("HTTP-Referer", "https://mercado-inteligente.local");
            httpReq.Headers.Add("X-Title", "Mercado Inteligente");

            var payload = new
            {
                model = effectiveModel,
                messages = new object[]
                {
                    new { role = "system", content = request.SystemPrompt },
                    new { role = "user", content = request.UserPrompt }
                },
                max_tokens = request.MaxTokens ?? settings.MaxTokens,
                temperature = request.Temperature ?? settings.Temperature
            };

            httpReq.Content = JsonContent.Create(payload);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(settings.TimeoutSeconds));

            var response = await _httpClient.SendAsync(httpReq, cts.Token);
            sw.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var code = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized => "UNAUTHORIZED_401",
                    HttpStatusCode.PaymentRequired => "INSUFFICIENT_CREDITS_402",
                    HttpStatusCode.TooManyRequests => "RATE_LIMIT_429",
                    _ => $"HTTP_{(int)response.StatusCode}"
                };

                if (reservedUsd > 0) _usageTracker.SettleMonthlyBudget(reservedUsd, 0);
                _usageTracker.RecordCall(0, 0, 0m, isCacheHit: false, isError: true);
                return new AiCompletionResult(
                    Success: false,
                    RawContent: null,
                    ErrorCode: code,
                    LatencyMs: sw.ElapsedMilliseconds,
                    ModelUsed: effectiveModel,
                    PromptVersion: request.PromptVersion
                );
            }

            var respJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(respJson);
            var root = doc.RootElement;

            string? content = null;
            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var msg) && msg.TryGetProperty("content", out var cnt))
                {
                    content = cnt.GetString();
                }
            }

            int promptTokens = 0;
            int completionTokens = 0;
            if (root.TryGetProperty("usage", out var usageElem))
            {
                if (usageElem.TryGetProperty("prompt_tokens", out var pt)) promptTokens = pt.GetInt32();
                if (usageElem.TryGetProperty("completion_tokens", out var ct)) completionTokens = ct.GetInt32();
            }

            var estimatedCost = root.TryGetProperty("usage", out var usageCost) &&
                usageCost.TryGetProperty("cost", out var costValue) && costValue.TryGetDecimal(out var providerCost)
                ? providerCost : await EstimateWithModelMetadataAsync(promptTokens, completionTokens, settings, apiKey, cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                if (reservedUsd > 0) _usageTracker.SettleMonthlyBudget(reservedUsd, estimatedCost);
                _usageTracker.RecordCall(promptTokens, completionTokens, reservedUsd > 0 ? 0 : estimatedCost, isCacheHit: false, isError: true);
                return new AiCompletionResult(
                    Success: false,
                    RawContent: null,
                    ErrorCode: "EMPTY_COMPLETION",
                    PromptTokens: promptTokens,
                    CompletionTokens: completionTokens,
                    LatencyMs: sw.ElapsedMilliseconds,
                    ModelUsed: effectiveModel,
                    PromptVersion: request.PromptVersion
                );
            }

            // Save to Cache
            _cacheService.Set(cacheKey, new AiCacheService.CacheEntry(
                Content: content,
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                Model: settings.Model,
                TimestampUtc: DateTime.UtcNow.ToString("o")
            ));

            if (reservedUsd > 0) _usageTracker.SettleMonthlyBudget(reservedUsd, estimatedCost);
            _usageTracker.RecordCall(promptTokens, completionTokens, reservedUsd > 0 ? 0 : estimatedCost, isCacheHit: false, isError: false);

            return new AiCompletionResult(
                Success: true,
                RawContent: content,
                ErrorCode: null,
                PromptTokens: promptTokens,
                CompletionTokens: completionTokens,
                LatencyMs: sw.ElapsedMilliseconds,
                IsFromCache: false,
                ModelUsed: effectiveModel,
                PromptVersion: request.PromptVersion
            );
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            if (reservedUsd > 0) _usageTracker.SettleMonthlyBudget(reservedUsd, 0);
            _usageTracker.RecordCall(0, 0, 0m, isCacheHit: false, isError: true);
            return new AiCompletionResult(
                Success: false,
                RawContent: null,
                ErrorCode: "TIMEOUT",
                LatencyMs: sw.ElapsedMilliseconds,
                ModelUsed: effectiveModel,
                PromptVersion: request.PromptVersion
            );
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (reservedUsd > 0) _usageTracker.SettleMonthlyBudget(reservedUsd, 0);
            _usageTracker.RecordCall(0, 0, 0m, isCacheHit: false, isError: true);
            return new AiCompletionResult(
                Success: false,
                RawContent: null,
                ErrorCode: $"EXCEPTION_{ex.GetType().Name}",
                LatencyMs: sw.ElapsedMilliseconds,
                ModelUsed: effectiveModel,
                PromptVersion: request.PromptVersion
            );
        }
    }

    private static bool IsFeatureEnabled(string feature, AiSettings settings) => feature switch
    {
        "product_normalization" => settings.NormalizationEnabled,
        "match_adjudication" => settings.MatchingEnabled,
        "substitute_suggestion" => settings.SubstituteEnabled,
        "basket_explanation" => settings.ExplanationEnabled,
        _ => true
    };

    private static decimal EstimateCost(int promptTokens, int completionTokens, string model)
    {
        // Standard rate estimate: ~$1.00/1M input tokens, ~$5.00/1M output tokens for mini/haiku models
        decimal inputRate = 0.000001m;
        decimal outputRate = 0.000005m;

        if (model.Contains("gpt-4o-mini", StringComparison.OrdinalIgnoreCase))
        {
            inputRate = 0.00000015m;
            outputRate = 0.00000060m;
        }

        return (promptTokens * inputRate) + (completionTokens * outputRate);
    }

    private async Task<decimal> EstimateWithModelMetadataAsync(int promptTokens, int completionTokens,
        AiSettings settings, string apiKey, CancellationToken cancellationToken)
    {
        var pricing = await GetModelPricingAsync(settings, apiKey, cancellationToken);
        return pricing is null ? EstimateCost(promptTokens, completionTokens, settings.Model) :
            pricing.Value.Prompt * promptTokens + pricing.Value.Completion * completionTokens;
    }

    private async Task<(decimal Prompt, decimal Completion)?> GetModelPricingAsync(
        AiSettings settings, string apiKey, CancellationToken cancellationToken)
    {
        lock (_modelPricesLock)
        {
            if (_modelPrices.TryGetValue(settings.Model, out var cached) && cached.ExpiresAt > DateTime.UtcNow)
                return (cached.Prompt, cached.Completion);
        }
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                CombineUrl(settings.BaseUrl, "models/" + Uri.EscapeDataString(settings.Model)));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var data = document.RootElement.GetProperty("data");
            var pricing = data.GetProperty("pricing");
            var prompt = decimal.Parse(pricing.GetProperty("prompt").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            var completion = decimal.Parse(pricing.GetProperty("completion").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
            if (prompt < 0 || completion < 0) throw new FormatException("Invalid model price.");
            lock (_modelPricesLock)
                _modelPrices[settings.Model] = (DateTime.UtcNow.AddHours(24), prompt, completion);
            return (prompt, completion);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or FormatException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
    }

    private static string Truncate(string str, int maxLen)
    {
        return str.Length <= maxLen ? str : str[..maxLen] + "...";
    }
}


public class OmniRouterAiProvider : OpenRouterAiProvider
{
    public OmniRouterAiProvider(
        HttpClient httpClient,
        ISecretStore secretStore,
        AiSettingsService settingsService,
        AiCacheService cacheService,
        AiUsageTracker usageTracker)
        : base(httpClient, secretStore, settingsService, cacheService, usageTracker)
    {
    }

    public override string ProviderDisplayName => "OmniRouter";
}
