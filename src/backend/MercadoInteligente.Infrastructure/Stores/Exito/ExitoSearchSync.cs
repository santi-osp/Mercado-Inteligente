using System.Globalization;
using System.Net;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.Exito;

public sealed class ExitoRefreshGate
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public DateTime LastAttempt { get; set; } = DateTime.MinValue;
}

public sealed class ExitoSearchSync(HttpClient client, ExitoCatalogIngest ingest, MarketDb db, ExitoRefreshGate? sharedGate = null)
{
    private readonly ExitoRefreshGate gate = sharedGate ?? new ExitoRefreshGate();
    private const int MaxPayloadBytes = 2 * 1024 * 1024;

    private async Task<HttpResponseMessage> FetchPageAsync(string path, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);
                if (attempt == 0 && response.StatusCode is HttpStatusCode.InternalServerError or
                    HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
                {
                    response.Dispose();
                    await Task.Delay(TimeSpan.FromMilliseconds(150), ct);
                    continue;
                }
                return response;
            }
            catch (HttpRequestException) when (attempt == 0 && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), ct);
            }
        }
    }

    public Task<ExitoSyncResult> RefreshAsync(string query, ProviderContext? context = null, CancellationToken ct = default) =>
        RefreshAsync(query, context, null, ct);

    public async Task<ExitoSyncResult> RefreshAsync(string query, ProviderContext? context, string? trigger, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
            throw new ArgumentException("Search must contain 1–200 characters.", nameof(query));

        if (!await gate.Semaphore.WaitAsync(0, ct)) return new ExitoSyncResult("BUSY", 0);
        try
        {
            var now = DateTime.UtcNow;
            if (trigger != "BasketFallback" && now - gate.LastAttempt < TimeSpan.FromSeconds(30)) return new ExitoSyncResult("COOLDOWN", 0);
            gate.LastAttempt = now;

            var effectiveContext = context ?? ExitoProvider.OnlineGeneric;
            var scope = effectiveContext.Key == "online-generic" ? PriceScope.OnlineGeneric : PriceScope.FulfillmentRegion;

            var settings = await db.Settings.AsNoTracking().FirstOrDefaultAsync(ct);
            var includeMarketplace = settings?.IncludeMarketplace ?? false;

            var run = new SyncRun { Provider = "EXITO", Trigger = trigger ?? "OnDemand", Context = effectiveContext.Key };
            db.SyncRuns.Add(run);
            await db.SaveChangesAsync(ct);

            try
            {
                var count = 0;
                var itemsReceived = 0;
                var offersChanged = 0;

                var region = effectiveContext.Key switch
                {
                    "online-generic" => null,
                    _ when effectiveContext.Key.StartsWith("fulfillment:", StringComparison.Ordinal) && effectiveContext.Key.Length > 12
                        => effectiveContext.Key[12..],
                    _ => throw new ArgumentException("Unverified Exito context.", nameof(context))
                };

                for (var page = 0; page < 3; page++)
                {
                    var channel = JsonSerializer.Serialize(new { salesChannel = "1", regionId = region });
                    var facets = new[]
                    {
                        new { key = "vendido-por", value = "exito" },
                        new { key = "channel", value = channel },
                        new { key = "locale", value = "es-CO" }
                    };
                    var variables = JsonSerializer.Serialize(new
                    {
                        first = 20,
                        after = (page * 20).ToString(CultureInfo.InvariantCulture),
                        sort = "score_desc",
                        term = query.Trim(),
                        selectedFacets = facets
                    });
                    var path = "api/graphql?operationName=SearchQuery&variables=" + Uri.EscapeDataString(variables);

                    using var response = await FetchPageAsync(path, ct);
                    if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                        throw new HttpRequestException("Exito declined this request.", null, response.StatusCode);
                    response.EnsureSuccessStatusCode();

                    if (response.Content.Headers.ContentLength > MaxPayloadBytes)
                        throw new JsonException("Exito page exceeded the size limit.");

                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var memory = new MemoryStream();
                    var buffer = new byte[16 * 1024];
                    int read;
                    while ((read = await stream.ReadAsync(buffer, ct)) != 0)
                    {
                        if (memory.Length + read > MaxPayloadBytes)
                            throw new JsonException("Exito page exceeded the size limit.");
                        memory.Write(buffer, 0, read);
                    }

                    var payload = memory.ToArray();
                    using var document = JsonDocument.Parse(payload);
                    var nodes = ExitoMapper.Nodes(document.RootElement);
                    if (nodes.Count == 0) break;

                    var metrics = await ingest.SaveWithMetricsAsync(payload, effectiveContext, scope, DateTime.UtcNow, includeMarketplace, ct);
                    count += metrics.OffersProcessed;
                    offersChanged += metrics.OffersChanged;
                    itemsReceived += nodes.Count;

                    run.ItemsReceived = itemsReceived;
                    run.OffersProcessed = count;
                    run.OffersChanged = offersChanged;
                    run.ProductsCreated += metrics.ProductsCreated;
                    run.ProductsUpdated += metrics.ProductsUpdated;
                    run.SkusCreated += metrics.SkusCreated;
                    run.SkusUpdated += metrics.SkusUpdated;
                    run.RawNew += metrics.RawNew;
                    run.RawDeduplicated += metrics.RawDeduplicated;
                    await db.SaveChangesAsync(ct);

                    if (nodes.Count < 20) break;
                }

                var completed = await db.SyncRuns.SingleAsync(x => x.Id == run.Id, ct);
                completed.State = SyncState.Completed;
                completed.FinishedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return new ExitoSyncResult("COMPLETED", count);
            }
            catch (Exception exception)
            {
                db.ChangeTracker.Clear();
                var failed = await db.SyncRuns.SingleAsync(x => x.Id == run.Id, CancellationToken.None);
                failed.State = SyncState.Failed;
                failed.FinishedAt = DateTime.UtcNow;
                failed.ErrorCode = exception switch
                {
                    OperationCanceledException when ct.IsCancellationRequested => "CANCELLED",
                    OperationCanceledException => "TIMEOUT",
                    HttpRequestException { StatusCode: HttpStatusCode.Forbidden } => "FORBIDDEN",
                    HttpRequestException { StatusCode: HttpStatusCode.TooManyRequests } => "RATE_LIMITED",
                    HttpRequestException => "HTTP_ERROR",
                    JsonException => "SCHEMA_DRIFT",
                    _ => "SYNC_ERROR"
                };
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
        }
        finally
        {
            gate.Semaphore.Release();
        }
    }
}

public sealed record ExitoSyncResult(string Status, int OffersSeen);
