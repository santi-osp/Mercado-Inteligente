using MercadoInteligente.Application;
using MercadoInteligente.Api;
using Microsoft.Extensions.FileProviders;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using MercadoInteligente.Infrastructure.Stores.D1;
using MercadoInteligente.Infrastructure.Stores.Exito;
using MercadoInteligente.Infrastructure.Stores.Ara;
using MercadoInteligente.Infrastructure.Stores.Common;
using MercadoInteligente.Infrastructure.Stores.LaVaquita;
using MercadoInteligente.Infrastructure.Stores.MundoHuevo;
using MercadoInteligente.Infrastructure.Stores.Olimpica;
using MercadoInteligente.Infrastructure.Stores.Jumbo;
using MercadoInteligente.Infrastructure.Stores.PriceSmart;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using System.Diagnostics;
using System.Net;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["MARKET_URL"] ?? "http://127.0.0.1:5000");
var paths = new LocalPaths(builder.Configuration["MARKET_DATA_DIR"]);
builder.Services.AddSingleton(paths);
builder.Logging.AddProvider(new LocalFileLoggerProvider(paths.Logs));
builder.Services.AddDbContext<MarketDb>(options => options.UseSqlite($"Data Source={paths.Database};Foreign Keys=True;Default Timeout=10;Pooling=False"));
builder.Services.AddScoped<RawStore>();
builder.Services.AddScoped<D1CatalogIngest>();
builder.Services.AddSingleton<D1RefreshGate>();
builder.Services.AddHttpClient<D1SearchSync>(client =>
{
    client.BaseAddress = new Uri("https://www.d1.com.co/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddHttpClient<D1HealthProbe>(client =>
{
    client.BaseAddress = new Uri("https://www.d1.com.co/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddTransient<D1RegionalCapabilityProbe>();
builder.Services.AddScoped<ExitoCatalogIngest>();
builder.Services.AddSingleton<ExitoRefreshGate>();
builder.Services.AddHttpClient<ExitoProvider>(client =>
{
    client.BaseAddress = new Uri("https://www.exito.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddHttpClient<ExitoSearchSync>(client =>
{
    client.BaseAddress = new Uri("https://www.exito.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddHttpClient<ExitoHealthProbe>(client =>
{
    client.BaseAddress = new Uri("https://www.exito.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddHttpClient<ExitoRegionalCapabilityProbe>(client =>
{
    client.BaseAddress = new Uri("https://www.exito.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });

// Provider Infrastructure and Registry
builder.Services.AddHttpClient<D1Provider>(client =>
{
    client.BaseAddress = new Uri("https://www.d1.com.co/");
    client.Timeout = TimeSpan.FromSeconds(15);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddScoped<IStoreProvider>(sp => sp.GetRequiredService<D1Provider>());
builder.Services.AddScoped<IStoreProvider>(sp => sp.GetRequiredService<ExitoProvider>());
builder.Services.AddScoped<AraProvider>();
builder.Services.AddScoped<IStoreProvider>(sp => sp.GetRequiredService<AraProvider>());
builder.Services.AddScoped<AraBrochureIngest>();
builder.Services.AddScoped<AraManualOfferService>();
builder.Services.AddScoped<AraHealthProbe>();
builder.Services.AddScoped<IStoreProviderRegistry, StoreProviderRegistry>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddScoped<DatabaseManagementService>();
builder.Services.AddScoped<GenericStoreCatalogIngest>();
builder.Services.AddScoped<IProductAnalysisService, ProductAnalysisService>();

builder.Services.AddScoped<LaVaquitaProvider>();
builder.Services.AddScoped<IStoreProvider>(sp => sp.GetRequiredService<LaVaquitaProvider>());

builder.Services.AddScoped<MundoHuevoProvider>();
builder.Services.AddScoped<IStoreProvider>(sp => sp.GetRequiredService<MundoHuevoProvider>());

builder.Services.AddScoped<OlimpicaProvider>();
builder.Services.AddScoped<IStoreProvider>(sp => sp.GetRequiredService<OlimpicaProvider>());

builder.Services.AddScoped<JumboProvider>();
builder.Services.AddScoped<IStoreProvider>(sp => sp.GetRequiredService<JumboProvider>());

builder.Services.AddScoped<PriceSmartProvider>();
builder.Services.AddScoped<IStoreProvider>(sp => sp.GetRequiredService<PriceSmartProvider>());
builder.Services.AddScoped<ISyncCoordinator, SyncCoordinator>();
builder.Services.AddSingleton<IFreshnessService, FreshnessService>();
builder.Services.AddSingleton<IPriceAnomalyDetector, PriceAnomalyDetector>();
builder.Services.AddSingleton<IProductLifecycleService, ProductLifecycleService>();
builder.Services.AddSingleton<ISmartPriceHistoryService, SmartPriceHistoryService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddScoped<ISyncEngine, SyncEngine>();
builder.Services.AddSingleton<ISecretStore, WindowsSecretStore>();
builder.Services.AddSingleton<AiSettingsService>();
builder.Services.AddSingleton<AiCacheService>();
builder.Services.AddSingleton<AiUsageTracker>();
builder.Services.AddSingleton<IHaversineDistanceService, HaversineDistanceService>();
builder.Services.AddHttpClient<OmniRouterAiProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(25);
});
builder.Services.AddSingleton<OmniRouterAiProvider>();
builder.Services.AddSingleton<OpenRouterAiProvider>(sp => sp.GetRequiredService<OmniRouterAiProvider>());
builder.Services.AddSingleton<IAiProvider>(sp => sp.GetRequiredService<OmniRouterAiProvider>());
builder.Services.AddTransient<ProductNormalizationAiService>();
builder.Services.AddTransient<MatchAdjudicationAiService>();
builder.Services.AddTransient<SubstituteSuggestionAiService>();
builder.Services.AddTransient<BasketExplanationAiService>();
builder.Services.AddSingleton<GoogleServiceUsageTracker>();
builder.Services.AddSingleton<GoogleProviderHealth>();
builder.Services.AddHttpClient<GoogleGeocodingProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient<GoogleRoutingProvider>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddTransient<IGeocodingProvider>(sp => sp.GetRequiredService<GoogleGeocodingProvider>());
builder.Services.AddTransient<IRoutingProvider>(sp => sp.GetRequiredService<GoogleRoutingProvider>());
builder.Services.AddTransient<StoreRouteOptimizer>();
builder.Services.AddSingleton<IOfferContextPolicy, OfferContextCompatibilityService>();
builder.Services.AddSingleton<ICommercialContextCache, CommercialContextCache>();
builder.Services.AddTransient<Func<D1FulfillmentSession>>(sp => () => new D1FulfillmentSession());
builder.Services.AddScoped<IShoppingContextResolver, ShoppingContextService>();
builder.Services.AddScoped<CatalogReconciliationService>();
builder.Services.AddSingleton<CatalogImportService>();
builder.Services.AddScoped<ProductSearchOrchestrator>();
builder.Services.AddTransient<ShoppingAssistantAiService>();
var app = builder.Build();
var operations = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("MercadoInteligente.Api.Operations");
// Check the actual HTTP Host and Origin, never X-Forwarded-Host/Proto/For from a caller.
app.Use(async (context, next) =>
{
    var request = context.Request;
    if (!IsLoopbackHost(request.Host.Host))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }

    if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase) &&
        !HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method) &&
        !HttpMethods.IsOptions(request.Method) && request.Headers.ContainsKey("Origin"))
    {
        var origins = request.Headers.Origin;
        if (origins.Count != 1 || !Uri.TryCreate(origins[0], UriKind.Absolute, out var origin) ||
            !string.Equals(origins[0], origin.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase) ||
            origin.UserInfo.Length != 0 ||
            !(SameOrigin(origin, request) || IsAngularDevOrigin(origin, request)))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
    }
    await next();
});
app.Use(async (context, next) =>
{
    var start = Stopwatch.GetTimestamp();
    try { await next(); }
    catch (Exception exception)
    {
        operations.LogError(new EventId(2, "RequestFailure"), exception, "Request failed");
        if (!context.Response.HasStarted) { context.Response.StatusCode = 500; await context.Response.WriteAsJsonAsync(new { code = "INTERNAL_ERROR", message = "La solicitud no pudo completarse." }); }
    }
    finally
    {
        operations.LogInformation(new EventId(1, "RequestComplete"), "Request completed: {StatusCode} in {DurationMs} ms",
            context.Response.StatusCode, (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    }
});
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
    var backups = scope.ServiceProvider.GetRequiredService<BackupService>();
    string? migrationBackup = null;
    try
    {
        // Never inspect migration history or change journal mode before checking an existing file.
        // ReadWrite mode prevents a mistyped path from silently creating a database here.
        if (File.Exists(paths.Database))
            await ValidateDatabaseAsync(paths.Database);

        var pending = (await db.Database.GetPendingMigrationsAsync()).Any();
        if (pending && File.Exists(paths.Database))
        {
            migrationBackup = await backups.CreateAsync();
            await ValidateDatabaseAsync(Path.Combine(paths.Backups, migrationBackup));
        }

        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = paths.Database, ForeignKeys = true, DefaultTimeout = 10, Pooling = false
        }.ToString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA journal_mode=WAL";
            if (!string.Equals((string?)await command.ExecuteScalarAsync(), "wal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("SQLite could not enable WAL mode.");
        }

        await db.Database.MigrateAsync();
        await ValidateDatabaseAsync(paths.Database);
        if (!db.Settings.Any()) { db.Settings.Add(new AppSettings()); await db.SaveChangesAsync(); }
        foreach (var run in db.SyncRuns.Where(x => x.State == SyncState.Running))
        { run.State = SyncState.Aborted; run.FinishedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        await ValidateDatabaseAsync(paths.Database);
    }
    catch (Exception exception)
    {
        operations.LogError(new EventId(2, "StartupDatabaseFailure"), exception, "Database startup failed");
        var recovery = migrationBackup is null
            ? "No migration backup was created during this startup."
            : $"Pre-migration backup: {Path.Combine(paths.Backups, migrationBackup)}.";
        throw new InvalidOperationException(
            $"Database startup failed for {paths.Database}. The API was not started. {recovery} " +
            "Stop all database users and inspect the database and backups. To recover, use the offline " +
            "scripts/restore-backup.ps1 procedure with a verified backup. The live database has NOT been automatically restored; " +
            "preserve it and its WAL/SHM sidecars for diagnosis before any manual restore.", exception);
    }
}
if (args.Contains("--prepare-database", StringComparer.Ordinal))
{
    Console.WriteLine("SQLite preparada: migraciones e integridad verificadas.");
    return;
}

static async Task ValidateDatabaseAsync(string file)
{
    await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = file, Mode = SqliteOpenMode.ReadWrite, ForeignKeys = true,
        DefaultTimeout = 10, Pooling = false
    }.ToString());
    await connection.OpenAsync();
    await using (var integrity = connection.CreateCommand())
    {
        integrity.CommandText = "PRAGMA integrity_check";
        if (!string.Equals((string?)await integrity.ExecuteScalarAsync(), "ok", StringComparison.Ordinal))
            throw new InvalidDataException("SQLite integrity_check failed.");
    }
    await using (var foreignKeys = connection.CreateCommand())
    {
        foreignKeys.CommandText = "PRAGMA foreign_key_check";
        await using var rows = await foreignKeys.ExecuteReaderAsync();
        if (await rows.ReadAsync())
            throw new InvalidDataException("SQLite foreign_key_check failed.");
    }
}


var uiDirectory = UiHosting.FindBuiltUi(app.Environment.ContentRootPath);
if (uiDirectory is not null)
{
    var files = new PhysicalFileProvider(uiDirectory);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { code = "NOT_FOUND", message = "Recurso no encontrado." });
            return;
        }
        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(Path.Combine(uiDirectory, "index.html"));
    });
}
var api = app.MapGroup("/api/v1");
api.MapSystemEndpoints();
// Secret endpoints intentionally return status only: the value never leaves the backend.
var secrets = api.MapGroup("/secrets/{type}");
secrets.AddEndpointFilter(async (context, next) =>
{
    context.HttpContext.Response.Headers.CacheControl = "no-store";
    return await next(context);
});
secrets.MapGet("", (string type, ISecretStore store) =>
{
    if (!IsSupportedSecretType(type)) return Results.NotFound();
    var isConfigured = type switch
    {
        "omnirouter" => store.Get("omnirouter") != null || store.Get("openrouter") != null,
        "openrouter" => store.Get("openrouter") != null || store.Get("omnirouter") != null,
        _ => store.Get(type) != null
    };
    return Results.Ok(new { configured = isConfigured });
});
secrets.MapPost("", (string type, SecretRequest payload, ISecretStore store) =>
{
    if (!IsSupportedSecretType(type)) return Results.NotFound();
    if (!IsValidSecret(type, payload.Value))
        return Results.BadRequest(new { code = "INVALID_SECRET", message = "Formato de clave inv�lido." });

    if (type == "omnirouter")
    {
        store.Set("omnirouter", payload.Value!);
        store.Set("openrouter", payload.Value!);
    }
    else if (type == "openrouter")
    {
        store.Set("openrouter", payload.Value!);
        store.Set("omnirouter", payload.Value!);
    }
    else
    {
        store.Set(type, payload.Value!);
    }

    return Results.Ok(new { configured = true });
});
secrets.MapDelete("", (string type, ISecretStore store) =>
{
    if (!IsSupportedSecretType(type)) return Results.NotFound();
    if (type is "omnirouter" or "openrouter")
    {
        store.Delete("omnirouter");
        store.Delete("openrouter");
    }
    else
    {
        store.Delete(type);
    }

    return Results.NoContent();
});
api.MapGet("/settings", async (MarketDb db) => await db.Settings.SingleAsync());
api.MapPut("/settings", async (AppSettings input, MarketDb db) =>
{
    if (input.MaxStores is < 1 or > 5 || input.SizeTolerancePercent is < 0 or > 100 || input.MaxOverbuyPercent is < 0 or > 100 ||
        input.PriceTtlHours is < 1 or > 168 || input.StockTtlHours is < 1 or > 72 ||
        input.ProductTtlDays is < 1 or > 365 || input.StoresTtlDays is < 1 or > 365 ||
        input.RouteTtlHours is < 1 or > 168 || input.PromoTtlHours is < 1 or > 168 ||
        input.RawRetentionDays is < 1 or > 365 || input.LogRetentionDays is < 1 or > 365 ||
        input.BackupDailyRetention is < 1 or > 30 || input.BackupWeeklyRetention is < 1 or > 52 || input.BackupMonthlyRetention is < 1 or > 24)
        return Results.BadRequest(new { code = "INVALID_SETTINGS", message = "Revise los límites de configuración y retención." });
    var settings = await db.Settings.SingleAsync();
    settings.MaxStores = input.MaxStores; settings.SizeTolerancePercent = input.SizeTolerancePercent;
    settings.MaxOverbuyPercent = input.MaxOverbuyPercent; settings.AllowSubstitutes = input.AllowSubstitutes;
    settings.IncludeMarketplace = input.IncludeMarketplace;
    settings.PriceTtlHours = input.PriceTtlHours;
    settings.StockTtlHours = input.StockTtlHours;
    settings.ProductTtlDays = input.ProductTtlDays;
    settings.StoresTtlDays = input.StoresTtlDays;
    settings.RouteTtlHours = input.RouteTtlHours;
    settings.PromoTtlHours = input.PromoTtlHours;
    settings.RawRetentionDays = input.RawRetentionDays;
    settings.LogRetentionDays = input.LogRetentionDays;
    settings.BackupDailyRetention = input.BackupDailyRetention;
    settings.BackupWeeklyRetention = input.BackupWeeklyRetention;
    settings.BackupMonthlyRetention = input.BackupMonthlyRetention;
    await db.SaveChangesAsync(); return Results.Ok(settings);
});
api.MapGet("/retailers", async (MarketDb db) => await db.Retailers.AsNoTracking().ToListAsync());
api.MapGet("/stores", async (MarketDb db) => await db.Stores.AsNoTracking().ToListAsync());
api.MapGet("/products/search", async (string? q, MarketDb db) =>
{
    if (string.IsNullOrWhiteSpace(q)) return Results.Ok(Array.Empty<object>());
    return Results.Ok(await ProductSearchService.SearchAsync(db, q));
});
api.MapProductEndpoints();
api.MapCatalogEndpoints();
api.MapBasketEndpoints();
api.MapHistoryEndpoints();
api.MapBackupEndpoints();
api.MapDatabaseEndpoints();
api.MapProviderEndpoints();
api.MapMatchingEndpoints();
api.MapAnalysisEndpoints();

api.MapLocationEndpoints();

api.MapAiEndpoints();

api.MapFallback(() => Results.NotFound(new { code = "NOT_FOUND", message = "Recurso no encontrado." }));
app.Run();
static bool IsLoopbackHost(string host) =>
    string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
    (IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address));

static bool SameOrigin(Uri origin, HttpRequest request) =>
    string.Equals(origin.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase) &&
    string.Equals(origin.IdnHost, request.Host.Host, StringComparison.OrdinalIgnoreCase) &&
    origin.Port == (request.Host.Port ?? (request.IsHttps ? 443 : 80));

// The CLI serves Angular at :4200 and proxies /api to this loopback-only backend.
static bool IsAngularDevOrigin(Uri origin, HttpRequest request) =>
    string.Equals(origin.Scheme, "http", StringComparison.OrdinalIgnoreCase) &&
    IsLoopbackHost(request.Host.Host) &&
    (string.Equals(origin.IdnHost, "localhost", StringComparison.OrdinalIgnoreCase) ||
     string.Equals(origin.IdnHost, "127.0.0.1", StringComparison.Ordinal)) &&
    origin.Port == 4200;

static bool IsSupportedSecretType(string type) => type is "google" or "openrouter" or "omnirouter";

static bool IsValidSecret(string type, string? value)
{
    if (value is null || value.Length > 512 || value.Any(c => c < '!' || c > '~')) return false;
    // Shape checks only; storing a key does not verify that an external provider accepts it.
    return type switch
    {
        "google" => value.Length is >= 35 and <= 128 && value.StartsWith("AIza", StringComparison.Ordinal),
        "openrouter" => (value.Length is >= 24 and <= 512 && value.StartsWith("sk-or-", StringComparison.Ordinal)) || (value.Length is >= 10 and <= 512),
        "omnirouter" => value.Length is >= 10 and <= 512,
        _ => false
    };
}



public partial class Program { }

public sealed record AddBasketItemRequest(
    string? Query,
    decimal Quantity,
    Guid? ProductVariantId = null,
    string? Notes = null,
    Dimension Dimension = Dimension.Unknown,
    string? Unit = null,
    bool? AllowSubstitutesOverride = null,
    int? SizeTolerancePercentOverride = null,
    string? PreferredBrand = null,
    string? ExcludedBrand = null
);

public sealed record CreateBasketRequest(
    string? Name = null,
    decimal? Budget = null,
    int? MaxStores = null,
    OptimizationStrategy? PreferredStrategy = null
);

public sealed record AddProductToBasketRequest(
    string Name,
    decimal Quantity = 1,
    string? Brand = null,
    string? Unit = null,
    string? Notes = null
);

public sealed record UpdateBasketRequest(
    string? Name,
    decimal? Budget,
    int? MaxStores,
    OptimizationStrategy? PreferredStrategy
);

public sealed record UpdateBasketItemRequest(
    string? Query,
    decimal? Quantity,
    string? Notes,
    Dimension? Dimension,
    string? Unit,
    bool? AllowSubstitutesOverride,
    int? SizeTolerancePercentOverride,
    string? PreferredBrand,
    string? ExcludedBrand
);

public sealed record OptimizeBasketRequest(
    OptimizationStrategy? Strategy = null,
    int? MaxStores = null,
    decimal? Budget = null,
    bool? AllowSubstitutes = null,
    int? SizeTolerancePercent = null,
    int? MaxOverbuyPercent = null,
    decimal? StorePenalty = null,
    decimal? OriginLatitude = null,
    decimal? OriginLongitude = null,
    string? LocationInput = null,
    bool ReturnToOrigin = true,
    [property: JsonConverter(typeof(JsonStringEnumConverter<TransportMode>))] TransportMode TransportMode = TransportMode.Driving,
    decimal CostPerKm = 2500m,
    decimal? MaxRouteKm = null,
    decimal PriceWeight = 1.0m,
    decimal DistanceWeight = 1.0m,
    decimal ConvenienceWeight = 1.0m
);

public sealed record SimulateOptimizationRequest(
    List<SimulateBasketItemInput> Items,
    decimal? Budget = null,
    int? MaxStores = null,
    OptimizationStrategy? Strategy = null,
    bool? AllowSubstitutes = null,
    int? SizeTolerancePercent = null,
    int? MaxOverbuyPercent = null,
    decimal? StorePenalty = null,
    decimal? OriginLatitude = null,
    decimal? OriginLongitude = null,
    string? LocationInput = null,
    bool ReturnToOrigin = true,
    [property: JsonConverter(typeof(JsonStringEnumConverter<TransportMode>))] TransportMode TransportMode = TransportMode.Driving,
    decimal CostPerKm = 2500m,
    decimal? MaxRouteKm = null,
    decimal PriceWeight = 1.0m,
    decimal DistanceWeight = 1.0m,
    decimal ConvenienceWeight = 1.0m
);

public sealed record LocationParseRequest(string? Input);
public sealed record LocationGeocodeRequest(string? Address);
public sealed record SaveLocationProfileRequest(
    string Label,
    string Address,
    decimal? Latitude = null,
    decimal? Longitude = null,
    string? FormattedAddress = null,
    string? Country = null,
    string? CountryCode = null,
    string? State = null,
    string? StateCode = null,
    string? City = null,
    string? PostalCode = null,
    string? Neighborhood = null,
    string? Street = null,
    string? Number = null,
    string? StreetNumber = null
);
public sealed record OptimizeRouteRequest(
    decimal OriginLatitude,
    decimal OriginLongitude,
    List<Guid> StoreIds,
    bool ReturnToOrigin = true,
    [property: JsonConverter(typeof(JsonStringEnumConverter<TransportMode>))] TransportMode TransportMode = TransportMode.Driving,
    decimal CostPerKm = 2500m,
    decimal? MaxRouteKm = null
);

public sealed record SimulateBasketItemInput(
    string Query,
    decimal Quantity = 1,
    Dimension Dimension = Dimension.Unknown,
    string? Unit = null,
    bool? AllowSubstitutes = null,
    int? SizeTolerancePercent = null,
    string? PreferredBrand = null,
    string? ExcludedBrand = null,
    string? Notes = null
);

public sealed record SecretInput(string? Value);

public sealed record CompareProductsRequest(
    string? NameA,
    string? BrandA,
    string? EanA,
    decimal? PriceA,
    string? NameB,
    string? BrandB,
    string? EanB,
    decimal? PriceB,
    decimal? TargetQuantity = null
);

public sealed record CreateMatchOverrideRequest(
    Guid ExternalSkuId,
    Guid ProductVariantId,
    MatchKind Kind
);





public sealed record SecretRequest(string? Value);
