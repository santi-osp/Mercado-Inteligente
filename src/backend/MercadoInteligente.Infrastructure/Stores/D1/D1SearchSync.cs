using System.Net;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Infrastructure.Stores.D1;

// Manual, bounded generic-online refresh. No regional claim or background traffic.
public sealed class D1RefreshGate
{
    public SemaphoreSlim Semaphore { get; } = new(1, 1);
    public DateTime LastAttempt { get; set; } = DateTime.MinValue;
}

public sealed class D1SearchSync(HttpClient client, D1CatalogIngest ingest, MarketDb db, D1RefreshGate? sharedGate = null)
{
    private readonly D1RefreshGate gate = sharedGate ?? new D1RefreshGate();
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

    public Task<D1SyncResult> RefreshAsync(string query, CancellationToken ct) =>
        RefreshAsync(query, null, ct);

    public async Task<D1SyncResult> RefreshAsync(string query, string? trigger, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > 200)
            throw new ArgumentException("Search must contain 1–200 characters.", nameof(query));
        if (!await gate.Semaphore.WaitAsync(0, ct)) return new D1SyncResult("BUSY", 0);
        try
        {
            var now = DateTime.UtcNow;
            if (now - gate.LastAttempt < TimeSpan.FromSeconds(30)) return new D1SyncResult("COOLDOWN", 0);
            gate.LastAttempt = now;
            var run = new SyncRun { Provider = "D1", Trigger = trigger ?? "OnDemand", Context = "online-generic" };
            db.SyncRuns.Add(run);
            await db.SaveChangesAsync(ct);
            try
            {
                var count = 0;
                var itemsReceived = 0;
                var offersChanged = 0;
                for (var page = 0; page < 3; page++)
                {
                    var from = page * 20;
                    using var response = await FetchPageAsync($"api/catalog_system/pub/products/search?ft={Uri.EscapeDataString(query.Trim())}&_from={from}&_to={from + 19}", ct);
                    if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
                        throw new HttpRequestException("D1 declined this request.", null, response.StatusCode);
                    response.EnsureSuccessStatusCode();
                    if (response.Content.Headers.ContentLength > MaxPayloadBytes)
                        throw new System.Text.Json.JsonException("D1 page exceeded the size limit.");
                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    using var memory = new MemoryStream();
                    var buffer = new byte[16 * 1024];
                    int read;
                    while ((read = await stream.ReadAsync(buffer, ct)) != 0)
                    {
                        if (memory.Length + read > MaxPayloadBytes)
                            throw new System.Text.Json.JsonException("D1 page exceeded the size limit.");
                        memory.Write(buffer, 0, read);
                    }
                    var payload = memory.ToArray();
                    using var document = System.Text.Json.JsonDocument.Parse(payload);
                    if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
                        throw new System.Text.Json.JsonException("D1 catalog is not an array.");
                    if (document.RootElement.GetArrayLength() == 0) break;
                    if (document.RootElement.GetArrayLength() > 20)
                        throw new System.Text.Json.JsonException("D1 page exceeded the requested limit.");
                    var metrics = await ingest.SaveWithMetricsAsync(payload, new ProviderContext("online-generic"), PriceScope.OnlineGeneric, DateTime.UtcNow, ct);
                    count += metrics.OffersProcessed;
                    offersChanged += metrics.OffersChanged;
                    itemsReceived += document.RootElement.GetArrayLength();
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
                    if (document.RootElement.GetArrayLength() < 20) break;
                }
                var completed = await db.SyncRuns.SingleAsync(x => x.Id == run.Id, ct);
                completed.State = SyncState.Completed;
                completed.FinishedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return new D1SyncResult("COMPLETED", count);
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
                    System.Text.Json.JsonException => "SCHEMA_DRIFT",
                    _ => "SYNC_ERROR"
                };
                await db.SaveChangesAsync(CancellationToken.None);
                throw;
            }
        }
        finally { gate.Semaphore.Release(); }
    }
}

public sealed record D1SyncResult(string Status, int OffersSeen);
