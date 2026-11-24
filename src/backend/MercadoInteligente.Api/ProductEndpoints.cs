using MercadoInteligente.Application;
using MercadoInteligente.Infrastructure;

namespace MercadoInteligente.Api;

public static class ProductEndpoints
{
    public static RouteGroupBuilder MapProductEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/search/products", async (ProductSearchRequest request, ProductSearchOrchestrator orchestrator, CancellationToken ct) =>
        {
            var result = await orchestrator.SearchAsync(request, ct);
            return Results.Ok(result);
        });

        api.MapGet("/search/products", async (
            string? q,
            string? sort,
            int? page,
            int? pageSize,
            string? retailer,
            string? brand,
            string? category,
            decimal? minPrice,
            decimal? maxPrice,
            bool? inStockOnly,
            bool? promoOnly,
            bool? forceRefresh,
            bool? localOnly,
            Guid? locationProfileId,
            string? locationInput,
            ProductSearchOrchestrator orchestrator,
            CancellationToken ct) =>
        {
            var filters = new ProductSearchFilters(
                Retailers: string.IsNullOrWhiteSpace(retailer) ? null : [retailer],
                Brands: string.IsNullOrWhiteSpace(brand) ? null : [brand],
                Categories: string.IsNullOrWhiteSpace(category) ? null : [category],
                MinPrice: minPrice,
                MaxPrice: maxPrice,
                InStockOnly: inStockOnly,
                PromoOnly: promoOnly
            );

            var request = new ProductSearchRequest(
                Query: q,
                SelectedRetailers: string.IsNullOrWhiteSpace(retailer) ? null : [retailer],
                LocationProfileId: locationProfileId,
                LocationInput: locationInput,
                ForceRefresh: forceRefresh ?? false,
                Page: page ?? 1,
                PageSize: pageSize ?? 24,
                Sort: sort ?? "relevance",
                Filters: filters,
                LocalOnly: localOnly ?? false
            );

            var result = await orchestrator.SearchAsync(request, ct);
            return Results.Ok(result);
        });

        return api;
    }
}
