using System.Collections.Concurrent;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MercadoInteligente.Infrastructure;

public sealed class CatalogImportService(
    IServiceScopeFactory scopeFactory,
    ILogger<CatalogImportService> logger
)
{
    private readonly ConcurrentDictionary<Guid, CatalogImportJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellations = new();

    public IReadOnlyList<CatalogImportJob> GetJobs() => _jobs.Values.OrderByDescending(j => j.StartedAt).ToList();

    public CatalogImportJob? GetJob(Guid id) => _jobs.TryGetValue(id, out var job) ? job : null;

    public bool CancelJob(Guid id)
    {
        if (_cancellations.TryGetValue(id, out var cts))
        {
            cts.Cancel();
            if (_jobs.TryGetValue(id, out var job))
            {
                job.Status = CatalogImportStatus.Cancelled;
                job.FinishedAt = DateTime.UtcNow;
            }
            return true;
        }
        return false;
    }

    public async Task<IReadOnlyList<CatalogCategory>> GetCategoriesAsync(string retailerCode, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var providerRegistry = scope.ServiceProvider.GetRequiredService<IStoreProviderRegistry>();
        var provider = providerRegistry.GetProvider(retailerCode);
        if (provider == null) return [];

        var pCtx = new ProviderContext("fulfillment:medellin-aburra");
        return await provider.EnumerateCategoriesAsync(pCtx, ct);
    }

    public CatalogImportJob StartImport(string retailerCode, IReadOnlyList<string> categoryIds, bool selectAll = false)
    {
        using var scope = scopeFactory.CreateScope();
        var providerRegistry = scope.ServiceProvider.GetRequiredService<IStoreProviderRegistry>();
        var metadata = providerRegistry.GetMetadata().FirstOrDefault(m => string.Equals(m.Code, retailerCode, StringComparison.OrdinalIgnoreCase));
        var job = new CatalogImportJob
        {
            RetailerCode = retailerCode.ToUpperInvariant(),
            RetailerName = metadata?.Name ?? retailerCode,
            CategoryIds = categoryIds,
            Status = CatalogImportStatus.Running,
            StartedAt = DateTime.UtcNow
        };

        var cts = new CancellationTokenSource();
        _jobs[job.Id] = job;
        _cancellations[job.Id] = cts;

        _ = Task.Run(() => RunImportAsync(job, selectAll, cts.Token));

        return job;
    }

    private async Task RunImportAsync(CatalogImportJob job, bool selectAll, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MarketDb>();
            var reconciler = scope.ServiceProvider.GetRequiredService<CatalogReconciliationService>();
            var providerRegistry = scope.ServiceProvider.GetRequiredService<IStoreProviderRegistry>();
            var provider = providerRegistry.GetProvider(job.RetailerCode);

            if (provider == null)
            {
                job.Status = CatalogImportStatus.Failed;
                job.ErrorMessage = $"Proveedor no encontrado: {job.RetailerCode}";
                job.FinishedAt = DateTime.UtcNow;
                return;
            }

            var pCtx = new ProviderContext("fulfillment:medellin-aburra");
            var categories = await provider.EnumerateCategoriesAsync(pCtx, ct);

            List<CatalogCategory> targetCategories;
            if (selectAll || job.CategoryIds.Count == 0)
            {
                targetCategories = categories.ToList();
            }
            else
            {
                var targetIdSet = job.CategoryIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
                targetCategories = categories.Where(c => targetIdSet.Contains(c.Id) || targetIdSet.Contains(c.Name)).ToList();
            }

            if (targetCategories.Count == 0 && categories.Count > 0)
            {
                targetCategories = categories.ToList();
            }

            job.TotalItems = targetCategories.Sum(c => c.EstimatedCount ?? 10);

            var session = new SearchSession
            {
                Query = $"Import-{job.RetailerCode}"
            };

            foreach (var cat in targetCategories)
            {
                if (ct.IsCancellationRequested)
                {
                    job.Status = CatalogImportStatus.Cancelled;
                    job.FinishedAt = DateTime.UtcNow;
                    return;
                }

                job.CurrentCategoryName = cat.Name;

                try
                {
                    var products = await provider.EnumerateCatalogByCategoryAsync(cat.Id, pCtx, ct);
                    if (products.Count > 0)
                    {
                        await reconciler.ReconcileRemoteProductsAsync(
                            job.RetailerCode,
                            job.RetailerName,
                            products,
p => [],
                            session,
                            ct
                        );

                        // Category reconciliation: mark missing items
                        var returnedIds = products.Select(p => p.ExternalId).ToList();
                        await reconciler.ReconcileCompletedCategoryAsync(job.RetailerCode, cat.Name, returnedIds, ct);
                    }

                    job.ProcessedItems += products.Count;
                    job.NewItems = session.NewCount;
                    job.UpdatedItems = session.UpdatedCount;
                    job.UnchangedItems = session.UnchangedCount;
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    logger.LogWarning(ex, "Error importing category {Cat} for {Retailer}", cat.Name, job.RetailerCode);
                    job.ErrorCount++;
                }
            }

            if (!ct.IsCancellationRequested)
            {
                job.Status = job.ErrorCount > 0 ? CatalogImportStatus.Partial : CatalogImportStatus.Completed;
                job.FinishedAt = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException)
        {
            job.Status = CatalogImportStatus.Cancelled;
            job.FinishedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fatal error running catalog import for {Retailer}", job.RetailerCode);
            job.Status = CatalogImportStatus.Failed;
            job.ErrorMessage = ex.Message;
            job.FinishedAt = DateTime.UtcNow;
        }
    }
}
