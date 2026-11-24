using System.Text.RegularExpressions;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure.Stores.D1;
using MercadoInteligente.Infrastructure.Stores.Exito;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MercadoInteligente.Infrastructure;

public sealed class ProductSearchOrchestrator(
    MarketDb db,
    D1SearchSync d1Sync,
    ExitoSearchSync exitoSync,
    IStoreProviderRegistry? providerRegistry,
    IFreshnessService freshnessService,
    IShoppingContextResolver contextResolver,
    IOfferContextPolicy contextPolicy,
    ILogger<ProductSearchOrchestrator> logger,
    CatalogReconciliationService? reconciler = null
)
{
    public ProductSearchOrchestrator(
        MarketDb db,
        D1SearchSync d1Sync,
        ExitoSearchSync exitoSync,
        IFreshnessService freshnessService,
        IShoppingContextResolver contextResolver,
        IOfferContextPolicy contextPolicy,
        ILogger<ProductSearchOrchestrator> logger
    ) : this(db, d1Sync, exitoSync, null, freshnessService, contextResolver, contextPolicy, logger, null)
    {
    }

    public ProductSearchOrchestrator(
        MarketDb db,
        D1SearchSync d1Sync,
        ExitoSearchSync exitoSync,
        IStoreProviderRegistry? providerRegistry,
        IFreshnessService freshnessService,
        IShoppingContextResolver contextResolver,
        IOfferContextPolicy contextPolicy,
        ILogger<ProductSearchOrchestrator> logger
    ) : this(db, d1Sync, exitoSync, providerRegistry, freshnessService, contextResolver, contextPolicy, logger, null)
    {
    }

    public async Task<ProductSearchResponse> SearchAsync(ProductSearchRequest request, CancellationToken ct = default)
    {
        var warnings = new List<string>();
        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 24 : Math.Min(request.PageSize, 100);

        // 1. Resolve Location and Commercial Context
        decimal? reqLat = null;
        decimal? reqLon = null;
        string? locInput = request.LocationInput;

        if (request.LocationProfileId.HasValue)
        {
            var profile = await db.LocationProfiles.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.LocationProfileId.Value, ct);
            if (profile != null)
            {
                reqLat = profile.Latitude;
                reqLon = profile.Longitude;
                locInput = profile.FormattedAddress ?? profile.Address;
            }
        }
        else if (string.IsNullOrWhiteSpace(locInput))
        {
            var defaultProfile = await db.LocationProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
            if (defaultProfile != null)
            {
                reqLat = defaultProfile.Latitude;
                reqLon = defaultProfile.Longitude;
                locInput = defaultProfile.FormattedAddress ?? defaultProfile.Address;
            }
        }

        var shoppingContext = await contextResolver.ResolveContextAsync(locInput, reqLat, reqLon, ct);

        // 2. Local search check
        var query = request.Query?.Trim();
        var hasQuery = !string.IsNullOrWhiteSpace(query);
        IReadOnlyList<ExternalProduct> localProducts = [];

        var retailers = await db.Retailers.AsNoTracking().ToListAsync(ct);
        var retailerMap = retailers.ToDictionary(r => r.Id);
        var demoRetailerIds = retailers
            .Where(r => string.Equals(r.Code, "DEMO", StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Id)
            .ToHashSet();

        if (hasQuery)
        {
            localProducts = await ProductSearchService.SearchAsync(db, query!, ct: ct);
        }
        else
        {
            localProducts = await db.ExternalProducts.AsNoTracking()
                .Where(p => p.Status == ProductLifecycleState.Active && !demoRetailerIds.Contains(p.RetailerId))
                .OrderByDescending(p => p.UpdatedAt)
                .Take(200)
                .ToListAsync(ct);
        }

        localProducts = localProducts.Where(p => !demoRetailerIds.Contains(p.RetailerId)).ToList();

        SearchSession? session = null;

        // 3. Trigger remote sync if hasQuery and not LocalOnly
        if (hasQuery && !request.LocalOnly)
        {
            session = new SearchSession
            {
                Query = query!,
                StartedAt = DateTime.UtcNow,
                LocalCount = localProducts.Count
            };

            var requestedRetailers = request.SelectedRetailers ?? [];

            var shouldQueryD1 = requestedRetailers.Count == 0 ||
                                requestedRetailers.Any(r => string.Equals(r, "D1", StringComparison.OrdinalIgnoreCase));
            var shouldQueryExito = requestedRetailers.Count == 0 ||
                                  requestedRetailers.Any(r => string.Equals(r, "EXITO", StringComparison.OrdinalIgnoreCase));

            if (shouldQueryD1)
            {
                try
                {
                    var d1Ctx = shoppingContext.GetContextForProvider("D1");
                    var regionId = d1Ctx?.ExternalContextId;
                    await d1Sync.RefreshAsync(query!, regionId, ct);
                    session.RetailerStatus["D1"] = "SUCCESS";
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "D1 live search failed for query '{Query}'", query);
                    session.RetailerStatus["D1"] = "FAILED";
                    session.ErrorCount++;
                    warnings.Add("D1 no respondió en este momento; se muestran resultados disponibles.");
                }
            }

            if (shouldQueryExito)
            {
                try
                {
                    var exitoCtx = shoppingContext.GetContextForProvider("EXITO");
                    var pCtx = exitoCtx != null ? new ProviderContext(exitoCtx.PriceContext) : null;
                    await exitoSync.RefreshAsync(query!, pCtx, ct);
                    session.RetailerStatus["EXITO"] = "SUCCESS";
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Éxito live search failed for query '{Query}'", query);
                    session.RetailerStatus["EXITO"] = "FAILED";
                    session.ErrorCount++;
                    warnings.Add("Éxito no respondió en este momento; se muestran resultados disponibles.");
                }
            }

            // Query modular store providers, excluding DEMO providers
            var otherProviders = (providerRegistry?.GetAllProviders() ?? [])
                .Where(p => !p.IsDemo && !string.Equals(p.Code, "DEMO", StringComparison.OrdinalIgnoreCase))
                .Where(p => !string.Equals(p.Code, "D1", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(p.Code, "EXITO", StringComparison.OrdinalIgnoreCase))
                .Where(p => requestedRetailers.Count == 0 ||
                            requestedRetailers.Any(r => string.Equals(r, p.Code, StringComparison.OrdinalIgnoreCase)))
                .Where(p => p.Capabilities.Search == CapabilityState.Supported)
                .ToList();

            foreach (var p in otherProviders)
            {
                try
                {
                    var provCtx = shoppingContext.GetContextForProvider(p.Code);
                    var pContext = new ProviderContext(provCtx?.PriceContext ?? "fulfillment:medellin-aburra");
                    var provProducts = await p.SearchAsync(query!, pContext, ct);
                    if (reconciler != null && provProducts.Count > 0)
                    {
                        await reconciler.ReconcileRemoteProductsAsync(
                            p.Code,
                            p.Code,
                            provProducts,
                            prod => [], // Never invent synthetic offers!
                            session,
                            ct
                        );
                    }
                    session.RetailerStatus[p.Code] = "SUCCESS";
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Provider {Code} search failed for '{Query}'", p.Code, query);
                    session.RetailerStatus[p.Code] = "FAILED";
                    session.ErrorCount++;
                }
            }

            session.FinishedAt = DateTime.UtcNow;

            // Re-fetch local products after ingestion/reconciliation
            localProducts = await ProductSearchService.SearchAsync(db, query!, ct: ct);
            localProducts = localProducts.Where(p => !demoRetailerIds.Contains(p.RetailerId)).ToList();
        }

        if (localProducts.Count == 0)
        {
            return new ProductSearchResponse(
                Items: [],
                TotalCount: 0,
                Page: page,
                PageSize: pageSize,
                Facets: new ProductSearchFacets([], [], [], null, null),
                Warnings: warnings,
                Session: session,
                TotalPages: 0,
                HasPrevious: false,
                HasNext: false,
                Query: query
            );
        }

        // 4. Build enriched product items with their best compatible offers
        var productIds = localProducts.Select(p => p.Id).ToList();

        var skus = await db.ExternalSkus.AsNoTracking()
            .Where(s => productIds.Contains(s.ExternalProductId))
            .ToListAsync(ct);

        var skuIds = skus.Select(s => s.Id).ToList();

        var rawOffers = await (from offer in db.Offers.AsNoTracking()
                               join seller in db.Sellers.AsNoTracking() on offer.SellerId equals seller.Id
                               where skuIds.Contains(offer.ExternalSkuId)
                               select new
                               {
                                   Offer = offer,
                                   Seller = seller
                               }).ToListAsync(ct);

        var items = new List<ProductSearchResultItem>();

        foreach (var product in localProducts)
        {
            var productSkus = skus.Where(s => s.ExternalProductId == product.Id).ToList();
            if (productSkus.Count == 0) continue;

            if (!retailerMap.TryGetValue(product.RetailerId, out var retailer)) continue;
            if (string.Equals(retailer.Code, "DEMO", StringComparison.OrdinalIgnoreCase)) continue;

            var effectiveCtx = shoppingContext.GetContextForProvider(retailer.Code);

            // Find best compatible offer for this product
            Offer? offerBest = null;
            Seller? sellerBest = null;
            ExternalSku? skuBest = null;
            OfferCompatibilityEvaluation? evalBest = null;

            foreach (var sku in productSkus)
            {
                var skuOffers = rawOffers.Where(o => o.Offer.ExternalSkuId == sku.Id).ToList();
                foreach (var o in skuOffers)
                {
                    var eval = contextPolicy.Evaluate(o.Offer.Scope, o.Offer.Context, retailer.Code, effectiveCtx);
                    if (eval.Compatibility == OfferCompatibility.Incompatible) continue;

                    if (offerBest == null)
                    {
                        offerBest = o.Offer;
                        sellerBest = o.Seller;
                        skuBest = sku;
                        evalBest = eval;
                    }
                    else
                    {
                        if (evalBest!.Compatibility == OfferCompatibility.Fallback &&
                            eval.Compatibility == OfferCompatibility.Compatible)
                        {
                            offerBest = o.Offer;
                            sellerBest = o.Seller;
                            skuBest = sku;
                            evalBest = eval;
                        }
                        else if (evalBest.Compatibility == eval.Compatibility && o.Offer.Price < offerBest.Price)
                        {
                            offerBest = o.Offer;
                            sellerBest = o.Seller;
                            skuBest = sku;
                            evalBest = eval;
                        }
                    }
                }
            }

            if (offerBest == null || skuBest == null || sellerBest == null || evalBest == null)
            {
                continue;
            }

            // Standardize quantity and unit price using UnitComparisonService
            StandardQuantity? stdQty = null;
            if (skuBest.Quantity.HasValue && skuBest.Dimension != Dimension.Unknown)
            {
                stdQty = new StandardQuantity(
                    skuBest.Quantity.Value,
                    skuBest.Dimension,
                    skuBest.PackageCount.GetValueOrDefault(1),
                    skuBest.TotalQuantity ?? skuBest.Quantity.Value,
                    skuBest.Unit ?? skuBest.Dimension.ToString()
                );
            }
            else
            {
                stdQty = QuantityParser.Parse(product.Name) ??
                         (!string.IsNullOrWhiteSpace(skuBest.Name) ? QuantityParser.Parse(skuBest.Name) : null);
            }

            var unitCalc = UnitComparisonService.CalculateUnitPrice(offerBest.Price, stdQty, product.Name, product.CategoryPath);
            decimal? unitPrice = unitCalc?.PricePerBaseUnit;
            string? unitLabel = unitCalc?.UnitPriceUnit;
            string? unitPriceDisplay = unitCalc?.UnitPriceDisplay;
            string? pricingMode = unitCalc?.PricingMode;
            string? priceBasis = unitCalc?.PriceBasis;
            string? priceNotes = unitCalc?.PriceNotes;

            decimal? discountPct = null;
            if (offerBest.ListPrice.HasValue && offerBest.ListPrice.Value > offerBest.Price)
            {
                discountPct = Math.Round((offerBest.ListPrice.Value - offerBest.Price) / offerBest.ListPrice.Value * 100, 0);
            }

            // Presentation text formatted consistently
            var presentation = QuantityDisplayFormatter.Format(
                totalQuantity: skuBest.TotalQuantity ?? skuBest.Quantity,
                unit: skuBest.Unit,
                packageCount: skuBest.PackageCount,
                quantityPerUnit: skuBest.QuantityPerUnit,
                dimension: skuBest.Dimension,
                productName: product.Name
            );
            if (string.IsNullOrWhiteSpace(presentation)) presentation = null;

            var cleanBrand = SanitizeBrand(product.Brand, retailer.Name);

            var priceScopeLabel = offerBest.Scope switch
            {
                PriceScope.FulfillmentRegion => "Precio online para tu zona",
                PriceScope.OnlineGeneric => "Precio online",
                PriceScope.PhysicalVerified => "Precio verificado en tienda",
                _ => "Precio no verificado"
            };

            var availLabel = offerBest.Availability switch
            {
                AvailabilityStatus.InStock => "Disponible",
                AvailabilityStatus.OutOfStock => "Agotado",
                _ => "Disponibilidad no confirmada"
            };

            string liveStatus;
            if (session != null && session.RetailerStatus.TryGetValue(retailer.Code, out var rStatus) && rStatus == "FAILED")
            {
                liveStatus = "No pudimos actualizar esta tienda";
            }
            else if (session != null && offerBest.CheckedAt >= session.StartedAt.AddSeconds(-2))
            {
                liveStatus = "Actualizado ahora";
            }
            else
            {
                liveStatus = "Dato guardado";
            }

            items.Add(new ProductSearchResultItem(
                ProductId: product.Id,
                ExternalSkuId: skuBest.Id,
                Name: product.Name,
                Brand: cleanBrand,
                Presentation: presentation,
                ImageUrl: product.ImageUrl,
                Retailer: retailer.Name,
                RetailerCode: retailer.Code,
                Price: offerBest.Price,
                ListPrice: offerBest.ListPrice,
                DiscountPercentage: discountPct,
                Promotion: offerBest.PromotionConditions,
                UnitPrice: unitPrice,
                UnitLabel: unitLabel,
                Availability: availLabel,
                Freshness: freshnessService.Evaluate(offerBest.CheckedAt, TimeSpan.FromHours(24)).ToString(),
                PriceScope: priceScopeLabel,
                UpdatedAt: product.UpdatedAt,
                ProductUrl: product.ProductUrl,
                CategoryPath: product.CategoryPath,
                IsMarketplace: sellerBest.Kind == SellerKind.Marketplace,
                SellerDisplay: sellerBest.Kind == SellerKind.Marketplace ? sellerBest.Name : null,
                IsFallbackContext: evalBest.IsFallback || evalBest.Compatibility == OfferCompatibility.Fallback,
                ContextWarning: (evalBest.IsFallback || evalBest.Compatibility == OfferCompatibility.Fallback) ? evalBest.Reason : null,
                LiveStatus: liveStatus,
                UnitPriceDisplay: unitPriceDisplay,
                UnitPriceUnit: unitLabel,
                PricingMode: pricingMode,
                PriceBasis: priceBasis,
                PriceNotes: priceNotes
            ));
        }

        // 5. Calculate Facets before user-applied filters
        var facetRetailers = items.GroupBy(i => i.Retailer)
            .Select(g => new FacetCount(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .ToList();

        var facetBrands = items.Where(i => !string.IsNullOrWhiteSpace(i.Brand))
            .GroupBy(i => i.Brand!)
            .Select(g => new FacetCount(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .Take(15)
            .ToList();

        var facetCategories = items.Where(i => !string.IsNullOrWhiteSpace(i.CategoryPath))
            .GroupBy(i => i.CategoryPath!)
            .Select(g => new FacetCount(g.Key, g.Count()))
            .OrderByDescending(f => f.Count)
            .Take(10)
            .ToList();

        var minPrice = items.Count > 0 ? items.Min(i => i.Price) : (decimal?)null;
        var maxPrice = items.Count > 0 ? items.Max(i => i.Price) : (decimal?)null;

        var facets = new ProductSearchFacets(facetRetailers, facetBrands, facetCategories, minPrice, maxPrice);

        // 6. Apply filters
        var filtered = items.AsEnumerable();
        var f = request.Filters;
        if (f != null)
        {
            if (f.Retailers != null && f.Retailers.Count > 0)
            {
                var rSet = f.Retailers.ToHashSet(StringComparer.OrdinalIgnoreCase);
                filtered = filtered.Where(i => rSet.Contains(i.Retailer) || rSet.Contains(i.RetailerCode));
            }
            if (f.Brands != null && f.Brands.Count > 0)
            {
                var bSet = f.Brands.ToHashSet(StringComparer.OrdinalIgnoreCase);
                filtered = filtered.Where(i => i.Brand != null && bSet.Contains(i.Brand));
            }
            if (f.Categories != null && f.Categories.Count > 0)
            {
                var cSet = f.Categories.ToHashSet(StringComparer.OrdinalIgnoreCase);
                filtered = filtered.Where(i => i.CategoryPath != null && cSet.Contains(i.CategoryPath));
            }
            if (f.MinPrice.HasValue)
            {
                filtered = filtered.Where(i => i.Price >= f.MinPrice.Value);
            }
            if (f.MaxPrice.HasValue)
            {
                filtered = filtered.Where(i => i.Price <= f.MaxPrice.Value);
            }
            if (f.InStockOnly.HasValue && f.InStockOnly.Value)
            {
                filtered = filtered.Where(i => i.Availability == "Disponible");
            }
            if (f.PromoOnly.HasValue && f.PromoOnly.Value)
            {
                filtered = filtered.Where(i => i.DiscountPercentage.HasValue || !string.IsNullOrWhiteSpace(i.Promotion));
            }
        }

        // 7. Sort
        filtered = request.Sort?.ToLowerInvariant() switch
        {
            "price_asc" => filtered.OrderBy(i => i.Price),
            "price_desc" => filtered.OrderByDescending(i => i.Price),
            "unit_price_asc" => filtered.OrderBy(i => i.UnitPrice ?? decimal.MaxValue),
            "recent" => filtered.OrderByDescending(i => i.UpdatedAt),
            "discount_desc" => filtered.OrderByDescending(i => i.DiscountPercentage ?? 0),
            _ => filtered // Default relevance order
        };

        var filteredList = filtered.ToList();
        var totalCount = filteredList.Count;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        if (totalPages < 1) totalPages = totalCount > 0 ? 1 : 0;
        var hasPrevious = page > 1;
        var hasNext = page < totalPages;

        // 8. Paging
        var pagedItems = filteredList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new ProductSearchResponse(
            Items: pagedItems,
            TotalCount: totalCount,
            Page: page,
            PageSize: pageSize,
            Facets: facets,
            Warnings: warnings,
            Session: session,
            TotalPages: totalPages,
            HasPrevious: hasPrevious,
            HasNext: hasNext,
            Query: query
        );
    }

    private static string? SanitizeBrand(string? brand, string? retailerName)
    {
        if (string.IsNullOrWhiteSpace(brand)) return null;
        var trimmed = brand.Trim();
        var lower = trimmed.ToLowerInvariant();
        if (lower is "sm" or "sin marca" or "s/m" or "generico" or "genérico" or "no aplica" or "n/a" or "desconocido" or "null" or "none")
            return null;
        if (!string.IsNullOrWhiteSpace(retailerName) && string.Equals(trimmed, retailerName.Trim(), StringComparison.OrdinalIgnoreCase))
            return null;
        return trimmed;
    }
}
