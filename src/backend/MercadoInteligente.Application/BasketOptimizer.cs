using MercadoInteligente.Domain;

namespace MercadoInteligente.Application;

public sealed record OptimizerItemAssignment(
    Guid BasketItemId,
    string ItemQuery,
    decimal RequestedQuantity,
    OptimizerCandidate Candidate,
    bool IsSubstitute,
    string AssignmentReason
);

public sealed record OptimizerStoreBucket(
    string StoreKey,
    string StoreName,
    string RetailerCode,
    decimal Subtotal,
    IReadOnlyList<OptimizerItemAssignment> Items,
    TravelLocation? TravelLocation = null
);

public sealed record OptimizerMissingItem(
    Guid BasketItemId,
    string ItemQuery,
    decimal RequestedQuantity,
    string Reason
);

public sealed record OptimizerSolution(
    OptimizationStrategy Strategy,
    int MaxStoresAllowed,
    int StoresUsedCount,
    decimal TotalCost,
    decimal BaselineCost,
    decimal TotalSavingsVsBaseline,
    int TotalItemsRequested,
    int ItemsCoveredCount,
    int MissingItemsCount,
    IReadOnlyList<OptimizerStoreBucket> Stores,
    IReadOnlyList<OptimizerItemAssignment> Assignments,
    IReadOnlyList<OptimizerMissingItem> MissingItems,
    IReadOnlyList<string> AppliedPromotions,
    IReadOnlyList<string> Warnings,
    OptimizerSuggestion? PlusOneStoreSuggestion = null,
    BudgetEvaluation? BudgetEvaluation = null,
    StoreRoutePlan? RoutePlan = null,
    ShoppingContext? ShoppingContext = null
);

public sealed record OptimizerSuggestion(
    int AdditionalStores,
    int StoresCount,
    int ItemsCovered,
    decimal TotalCost,
    string RecommendationText
);

public sealed record BudgetEvaluation(
    decimal Budget,
    decimal TotalCost,
    decimal Difference,
    bool IsExceeded,
    IReadOnlyList<string> Suggestions
);

public sealed record OptimizerExecutionOptions(
    OptimizationStrategy? Strategy = null,
    int? MaxStores = null,
    decimal? Budget = null,
    bool? AllowSubstitutes = null,
    int? SizeTolerancePercent = null,
    int? MaxOverbuyPercent = null,
    decimal StorePenalty = 5000m, // COP
    OptimizerLocationContext? LocationContext = null,
    IReadOnlyDictionary<string, Store>? StoreMap = null,
    ShoppingContext? ShoppingContext = null,
    bool AllowUnverifiedLocationsForTravel = false
);

public static class BasketOptimizer
{
    public static OptimizerSolution Optimize(
        Basket basket,
        IReadOnlyList<RawCatalogOffer> offers,
        AppSettings settings,
        IReadOnlyList<MatchOverride>? overrides = null,
        OptimizerExecutionOptions? options = null)
    {
        var effectiveStrategy = options?.Strategy ?? basket.PreferredStrategy;
        var maxStores = Math.Clamp(options?.MaxStores ?? basket.MaxStores ?? settings.MaxStores, 1, 5);
        var budget = options?.Budget ?? basket.Budget;
        var storePenalty = options?.StorePenalty ?? 5000m;

        var candOptions = new CandidateGenerationOptions(
            DefaultSizeTolerancePercent: options?.SizeTolerancePercent ?? settings.SizeTolerancePercent,
            DefaultAllowSubstitutes: options?.AllowSubstitutes ?? settings.AllowSubstitutes,
            DefaultMaxOverbuyPercent: options?.MaxOverbuyPercent ?? settings.MaxOverbuyPercent,
            PriceTtlHours: settings.PriceTtlHours,
            StockTtlHours: settings.StockTtlHours,
            IncludeMarketplace: settings.IncludeMarketplace,
            AllowOutdatedOffers: settings.AllowOutdatedOffers
        );

        // 0. Preprocess offers once for high-throughput scaling
        var preprocessedOffers = new PreprocessedOffer[offers.Count];
        if (offers.Count > 50)
        {
            Parallel.For(0, offers.Count, i =>
            {
                preprocessedOffers[i] = CandidateGenerationService.PreprocessOffer(offers[i], settings.PriceTtlHours);
            });
        }
        else
        {
            for (int i = 0; i < offers.Count; i++)
            {
                preprocessedOffers[i] = CandidateGenerationService.PreprocessOffer(offers[i], settings.PriceTtlHours);
            }
        }
        var catalogIndex = new CatalogIndex(preprocessedOffers);

        // 1. Generate candidates for each basket item
        var itemCandidatesMap = new Dictionary<Guid, IReadOnlyList<OptimizerCandidate>>(basket.Items.Count);
        var basketItemsList = basket.Items.ToList();
        var candidatesArray = new IReadOnlyList<OptimizerCandidate>[basketItemsList.Count];
        if (basketItemsList.Count > 10)
        {
            Parallel.For(0, basketItemsList.Count, i =>
            {
                candidatesArray[i] = CandidateGenerationService.GenerateCandidatesForItem(basketItemsList[i], catalogIndex, candOptions, overrides);
            });
        }
        else
        {
            for (int i = 0; i < basketItemsList.Count; i++)
            {
                candidatesArray[i] = CandidateGenerationService.GenerateCandidatesForItem(basketItemsList[i], catalogIndex, candOptions, overrides);
            }
        }
        for (int i = 0; i < basketItemsList.Count; i++)
        {
            itemCandidatesMap[basketItemsList[i].Id] = candidatesArray[i];
        }

        // 2. Pre-index best candidate per store for each item
        var itemStoreBestMap = new Dictionary<Guid, Dictionary<string, OptimizerCandidate>>(basket.Items.Count);
        var itemStoreOverbuyMap = new Dictionary<Guid, Dictionary<string, OptimizerCandidate>>(basket.Items.Count);

        foreach (var item in basket.Items)
        {
            var candidates = itemCandidatesMap[item.Id];
            var storeBest = new Dictionary<string, OptimizerCandidate>();
            var storeOverbuy = new Dictionary<string, OptimizerCandidate>();

            foreach (var cand in candidates)
            {
                if (!cand.IsOverbuyRejected)
                {
                    if (!storeBest.TryGetValue(cand.StoreKey, out var current) || CompareCandidates(cand, current) < 0)
                    {
                        storeBest[cand.StoreKey] = cand;
                    }
                }
                else
                {
                    if (!storeOverbuy.TryGetValue(cand.StoreKey, out var current) || CompareOverbuyCandidates(cand, current) < 0)
                    {
                        storeOverbuy[cand.StoreKey] = cand;
                    }
                }
            }

            itemStoreBestMap[item.Id] = storeBest;
            itemStoreOverbuyMap[item.Id] = storeOverbuy;
        }

        // 3. Identify all unique store keys
        var allStoreKeys = itemCandidatesMap.Values
            .SelectMany(cands => cands.Select(c => c.StoreKey))
            .Distinct()
            .OrderBy(k => k)
            .ToList();

        // 3.5 Pre-resolve store proxies for routing & distance calculation (TravelLocation before subset scoring)
        var resolvedStoreMap = BuildResolvedStoreMap(allStoreKeys, options?.StoreMap, options?.LocationContext);
        bool allowUnverified = options?.AllowUnverifiedLocationsForTravel ?? settings.AllowUnverifiedLocationsForTravel;

        // 4. Find Best Single-Store Solution (Baseline)
        var baseline = EvaluateBestSubset(basket.Items, itemStoreBestMap, itemStoreOverbuyMap, allStoreKeys, maxStores: 1, OptimizationStrategy.SingleStore, storePenalty, options?.LocationContext, resolvedStoreMap, allowUnverified);

        // 5. Find Best Solution for requested Strategy with <= MaxStores
        var targetStrategy = effectiveStrategy;
        var bestSolution = EvaluateBestSubset(basket.Items, itemStoreBestMap, itemStoreOverbuyMap, allStoreKeys, maxStores, targetStrategy, storePenalty, options?.LocationContext, resolvedStoreMap, allowUnverified);

        // 6. Evaluate N+1 Suggestion if coverage < 100% and more stores exist
        OptimizerSuggestion? plusOneSuggestion = null;
        if (bestSolution.ItemsCoveredCount < basket.Items.Count && maxStores < allStoreKeys.Count && maxStores < 5)
        {
            var nextSubsetSolution = EvaluateBestSubset(basket.Items, itemStoreBestMap, itemStoreOverbuyMap, allStoreKeys, maxStores + 1, targetStrategy, storePenalty, options?.LocationContext, resolvedStoreMap, allowUnverified);
            if (nextSubsetSolution.ItemsCoveredCount > bestSolution.ItemsCoveredCount)
            {
                var extraCovered = nextSubsetSolution.ItemsCoveredCount - bestSolution.ItemsCoveredCount;
                var extraCost = nextSubsetSolution.TotalCost - bestSolution.TotalCost;
                var costDiffText = extraCost >= 0 ? $"por ${extraCost:N0} adicionales" : $"ahorrando ${Math.Abs(extraCost):N0}";
                plusOneSuggestion = new OptimizerSuggestion(
                    AdditionalStores: 1,
                    StoresCount: nextSubsetSolution.StoresUsedCount,
                    ItemsCovered: nextSubsetSolution.ItemsCoveredCount,
                    TotalCost: nextSubsetSolution.TotalCost,
                    RecommendationText: $"Con 1 tienda adicional ({nextSubsetSolution.StoresUsedCount} en total) puedes cubrir {extraCovered} producto(s) más {costDiffText}."
                );
            }
        }

        // 7. Savings Calculation vs Baseline
        decimal baselineCost = baseline.TotalCost;
        decimal savings = 0m;
        if (baseline.ItemsCoveredCount == bestSolution.ItemsCoveredCount && baselineCost > 0)
        {
            savings = Math.Max(0m, baselineCost - bestSolution.TotalCost);
        }
        else if (baselineCost > 0)
        {
            savings = Math.Max(0m, baselineCost - bestSolution.TotalCost);
        }

        // 8. Budget Analysis
        BudgetEvaluation? budgetEval = null;
        if (budget.HasValue && budget.Value > 0)
        {
            var diff = bestSolution.TotalCost - budget.Value;
            var isExceeded = diff > 0;
            var budgetSuggestions = new List<string>();

            if (isExceeded)
            {
                budgetSuggestions.Add($"El costo total excede tu presupuesto por ${diff:N0} COP.");

                // Check if any items have cheaper substitute options that were not picked
                foreach (var assign in bestSolution.Assignments)
                {
                    var cands = itemCandidatesMap[assign.BasketItemId];
                    var cheaper = cands
                        .Where(c => !c.IsOverbuyRejected && c.TotalCost < assign.Candidate.TotalCost)
                        .OrderBy(c => c.TotalCost)
                        .FirstOrDefault();

                    if (cheaper != null)
                    {
                        var potentialSaving = assign.Candidate.TotalCost - cheaper.TotalCost;
                        budgetSuggestions.Add($"En '{assign.ItemQuery}', cambiar a '{cheaper.ProductName}' podría ahorrar ${potentialSaving:N0} COP.");
                    }
                }
            }

            budgetEval = new BudgetEvaluation(
                Budget: budget.Value,
                TotalCost: bestSolution.TotalCost,
                Difference: diff,
                IsExceeded: isExceeded,
                Suggestions: budgetSuggestions
            );
        }

        // 9. Warnings aggregation
        var warnings = new List<string>();
        if (bestSolution.MissingItems.Count > 0)
        {
            warnings.Add($"No se encontraron ofertas disponibles en el límite de {maxStores} tienda(s) para {bestSolution.MissingItems.Count} producto(s).");
        }

        var staleCount = bestSolution.Assignments.Count(a => a.Candidate.Freshness != Freshness.Fresh);
        if (staleCount > 0)
        {
            warnings.Add($"{staleCount} producto(s) usan precios o inventarios que podrían estar desactualizados.");
        }

        var onlineCount = bestSolution.Assignments.Count(a => a.Candidate.Scope is PriceScope.OnlineGeneric or PriceScope.FulfillmentRegion);
        if (onlineCount > 0)
        {
            warnings.Add($"{onlineCount} oferta(s) corresponden a canal online o regional, no verificadas en góndola física.");
        }

        foreach (var assign in bestSolution.Assignments)
        {
            if (assign.Candidate.PromotionWarning != null && !warnings.Contains(assign.Candidate.PromotionWarning))
            {
                warnings.Add(assign.Candidate.PromotionWarning);
            }
        }

        if (budgetEval?.IsExceeded == true)
        {
            warnings.Add($"Atención: la cesta supera el presupuesto configurado en ${budgetEval.Difference:N0} COP.");
        }

        // 10. Enrich store buckets with TravelLocation and audit transparency notices
        var enrichedStores = new List<OptimizerStoreBucket>();
        bool hasProxyRouteNotice = false;
        bool hasFallbackNotice = false;

        foreach (var bucket in bestSolution.Stores)
        {
            TravelLocation? travelLocation = null;
            bool isPhysical = bucket.Items.All(i => i.Candidate.Scope is PriceScope.Store or PriceScope.PhysicalVerified);
            bool isFallback = bucket.Items.Any(i => i.Candidate.IsFallbackContext);
            if (isFallback) hasFallbackNotice = true;

            if (resolvedStoreMap != null && options?.LocationContext != null)
            {
                Store? matchedStore = null;
                if (resolvedStoreMap.TryGetValue(bucket.StoreKey, out var store) && store.Latitude.HasValue && store.Longitude.HasValue)
                {
                    matchedStore = store;
                }
                else
                {
                    var retailerStores = resolvedStoreMap.Values
                        .Where(s => (string.Equals(s.Retailer?.Code, bucket.RetailerCode, StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(s.Name, bucket.StoreName, StringComparison.OrdinalIgnoreCase) ||
                                     (!string.IsNullOrEmpty(s.ExternalId) && s.ExternalId.StartsWith(bucket.RetailerCode, StringComparison.OrdinalIgnoreCase)))
                                     && s.Latitude.HasValue && s.Longitude.HasValue)
                        .ToList();

                    if (retailerStores.Count > 0)
                    {
                        matchedStore = retailerStores
                            .OrderBy(s => HaversineService.CalculateDistanceKm(
                                options.LocationContext.OriginLatitude,
                                options.LocationContext.OriginLongitude,
                                s.Latitude!.Value,
                                s.Longitude!.Value))
                            .FirstOrDefault();
                    }
                }

                if (matchedStore != null && matchedStore.Latitude.HasValue && matchedStore.Longitude.HasValue)
                {
                    var dist = HaversineService.CalculateDistanceKm(
                        options.LocationContext.OriginLatitude,
                        options.LocationContext.OriginLongitude,
                        matchedStore.Latitude.Value,
                        matchedStore.Longitude.Value);

                    bool isProxy = !isPhysical;
                    if (isProxy) hasProxyRouteNotice = true;

                    travelLocation = new TravelLocation(
                        StoreId: matchedStore.Id,
                        StoreName: matchedStore.Name,
                        RetailerCode: bucket.RetailerCode,
                        Latitude: matchedStore.Latitude.Value,
                        Longitude: matchedStore.Longitude.Value,
                        DistanceKm: dist,
                        IsPhysicalStoreProxy: isProxy,
                        VerificationStatus: isProxy ? "UNCONFIRMED_ONLINE_REGIONAL" : "PHYSICAL_STORE",
                        TransparencyNotice: isProxy ? "Precio online/regional; precio físico no confirmado" : "Precio en tienda física"
                    );
                }
            }

            enrichedStores.Add(bucket with { TravelLocation = travelLocation });
        }

        if (hasProxyRouteNotice && !warnings.Contains("Ruta hacia sucursal cercana; precio físico no confirmado."))
        {
            warnings.Add("Ruta hacia sucursal cercana; precio físico no confirmado.");
        }
        if (hasFallbackNotice && !warnings.Contains("No se encontró precio regional; se utilizó precio online genérico como fallback."))
        {
            warnings.Add("No se encontró precio regional; se utilizó precio online genérico como fallback.");
        }

        // 11. Applied promotions aggregation
        var appliedPromos = bestSolution.Assignments
            .Where(a => !string.IsNullOrWhiteSpace(a.Candidate.PromotionApplied))
            .Select(a => $"{a.Candidate.ProductName}: {a.Candidate.PromotionApplied}")
            .ToList();

        // 12. Route Plan generation if location context is provided
        StoreRoutePlan? routePlan = null;
        var locationCtx = options?.LocationContext;
        if (locationCtx != null && enrichedStores.Count > 0)
        {
            var storesToRoute = new List<Store>();
            foreach (var bucket in enrichedStores)
            {
                if (bucket.TravelLocation != null && bucket.TravelLocation.StoreId.HasValue && resolvedStoreMap != null &&
                    resolvedStoreMap.TryGetValue(bucket.TravelLocation.StoreId.Value.ToString(), out var physicalStore) &&
                    physicalStore.Latitude.HasValue && physicalStore.Longitude.HasValue)
                {
                    storesToRoute.Add(physicalStore);
                }
                else if (resolvedStoreMap != null && resolvedStoreMap.TryGetValue(bucket.StoreKey, out var store) && store.Latitude.HasValue && store.Longitude.HasValue)
                {
                    storesToRoute.Add(store);
                }
                else if (bucket.TravelLocation != null)
                {
                    storesToRoute.Add(new Store
                    {
                        Id = bucket.TravelLocation.StoreId ?? Guid.NewGuid(),
                        Name = bucket.TravelLocation.StoreName,
                        Latitude = bucket.TravelLocation.Latitude,
                        Longitude = bucket.TravelLocation.Longitude,
                        VerificationStatus = bucket.TravelLocation.VerificationStatus
                    });
                }
            }

            if (storesToRoute.Count > 0)
            {
                routePlan = StoreRouteOptimizer.OptimizeRouteOffline(
                    locationCtx.OriginLatitude,
                    locationCtx.OriginLongitude,
                    storesToRoute,
                    returnToOrigin: locationCtx.ReturnToOrigin,
                    transportMode: locationCtx.TransportMode,
                    costPerKm: locationCtx.CostPerKm
                );
            }
        }

        return new OptimizerSolution(
            Strategy: targetStrategy,
            MaxStoresAllowed: maxStores,
            StoresUsedCount: bestSolution.StoresUsedCount,
            TotalCost: bestSolution.TotalCost,
            BaselineCost: baselineCost,
            TotalSavingsVsBaseline: savings,
            TotalItemsRequested: basket.Items.Count,
            ItemsCoveredCount: bestSolution.ItemsCoveredCount,
            MissingItemsCount: bestSolution.MissingItems.Count,
            Stores: enrichedStores,
            Assignments: bestSolution.Assignments,
            MissingItems: bestSolution.MissingItems,
            AppliedPromotions: appliedPromos,
            Warnings: warnings,
            PlusOneStoreSuggestion: plusOneSuggestion,
            BudgetEvaluation: budgetEval,
            RoutePlan: routePlan,
            ShoppingContext: options?.ShoppingContext
        );
    }

    private static IReadOnlyDictionary<string, Store> BuildResolvedStoreMap(
        IEnumerable<string> allStoreKeys,
        IReadOnlyDictionary<string, Store>? storeMap,
        OptimizerLocationContext? locationContext)
    {
        var resolved = new Dictionary<string, Store>(StringComparer.OrdinalIgnoreCase);
        if (storeMap == null) return resolved;

        foreach (var kvp in storeMap)
        {
            resolved[kvp.Key] = kvp.Value;
        }

        foreach (var key in allStoreKeys)
        {
            if (resolved.TryGetValue(key, out var existing) && existing.Latitude.HasValue && existing.Longitude.HasValue)
            {
                continue;
            }

            if (key.StartsWith("store:", StringComparison.OrdinalIgnoreCase))
            {
                var rawId = key["store:".Length..];
                if (resolved.TryGetValue(rawId, out var s) && s.Latitude.HasValue && s.Longitude.HasValue)
                {
                    resolved[key] = s;
                    continue;
                }
            }

            if (key.StartsWith("retailer:", StringComparison.OrdinalIgnoreCase))
            {
                var retCode = key["retailer:".Length..];
                var candidateStores = storeMap.Values
                    .Where(s => (string.Equals(s.Retailer?.Code, retCode, StringComparison.OrdinalIgnoreCase) ||
                                 (!string.IsNullOrEmpty(s.ExternalId) && s.ExternalId.StartsWith(retCode, StringComparison.OrdinalIgnoreCase))) &&
                                s.Latitude.HasValue && s.Longitude.HasValue)
                    .ToList();

                if (candidateStores.Count > 0)
                {
                    var closest = locationContext != null
                        ? candidateStores
                            .OrderBy(s => HaversineService.CalculateDistanceKm(
                                locationContext.OriginLatitude, locationContext.OriginLongitude,
                                s.Latitude!.Value, s.Longitude!.Value))
                            .First()
                        : candidateStores[0];
                    resolved[key] = closest;
                }
            }
        }

        return resolved;
    }

    private static EvaluatedSubset EvaluateBestSubset(
        IReadOnlyList<BasketItem> items,
        Dictionary<Guid, Dictionary<string, OptimizerCandidate>> itemStoreBestMap,
        Dictionary<Guid, Dictionary<string, OptimizerCandidate>> itemStoreOverbuyMap,
        List<string> allStoreKeys,
        int maxStores,
        OptimizationStrategy strategy,
        decimal storePenalty,
        OptimizerLocationContext? locationContext = null,
        IReadOnlyDictionary<string, Store>? storeMap = null,
        bool allowUnverified = false)
    {
        if (items.Count == 0 || allStoreKeys.Count == 0)
        {
            return new EvaluatedSubset(0, 0m, 0, [], [], []);
        }

        var kMax = Math.Min(maxStores, allStoreKeys.Count);
        var subsets = GenerateSubsets(allStoreKeys, kMax);

        if (strategy == OptimizationStrategy.SingleStore)
        {
            subsets = subsets.Where(s => s.Count == 1).ToList();
        }

        var storeKeyToIndex = new Dictionary<string, int>(allStoreKeys.Count, StringComparer.Ordinal);
        for (int i = 0; i < allStoreKeys.Count; i++)
        {
            storeKeyToIndex[allStoreKeys[i]] = i;
        }

        var storeCount = allStoreKeys.Count;
        var itemsCount = items.Count;
        var indexedBest = new OptimizerCandidate?[itemsCount * storeCount];
        var indexedOverbuy = new OptimizerCandidate?[itemsCount * storeCount];

        for (int i = 0; i < itemsCount; i++)
        {
            var itemId = items[i].Id;
            var bestDict = itemStoreBestMap[itemId];
            var overbuyDict = itemStoreOverbuyMap[itemId];

            for (int s = 0; s < storeCount; s++)
            {
                var key = allStoreKeys[s];
                if (bestDict.TryGetValue(key, out var cand))
                {
                    indexedBest[i * storeCount + s] = cand;
                }
                if (overbuyDict.TryGetValue(key, out var ob))
                {
                    indexedOverbuy[i * storeCount + s] = ob;
                }
            }
        }

        var indexedSubsets = new List<(List<string> Original, int[] Indices)>(subsets.Count);
        foreach (var subset in subsets)
        {
            var arr = new int[subset.Count];
            for (int s = 0; s < subset.Count; s++)
            {
                arr[s] = storeKeyToIndex[subset[s]];
            }
            indexedSubsets.Add((subset, arr));
        }

        List<string>? bestSubset = null;
        int bestCovered = -1;
        decimal bestCost = decimal.MaxValue;
        int bestStoresUsed = int.MaxValue;
        decimal bestScore = decimal.MaxValue;

        foreach (var item in indexedSubsets)
        {
            var subset = item.Original;
            var subIndices = item.Indices;

            ScoreIndexedSubset(itemsCount, storeCount, indexedBest, indexedOverbuy, subIndices,
                out var covered, out var cost, out var storesUsed, out var usedMask);

            decimal strategyCost = cost;
            if (strategy == OptimizationStrategy.Balanced)
            {
                if (locationContext != null && storeMap != null && storesUsed > 0)
                {
                    var usedStores = new List<Store>(storesUsed);
                    for (int s = 0; s < subIndices.Length; s++)
                    {
                        if ((usedMask & (1 << s)) != 0)
                        {
                            var key = subset[s];
                            if (storeMap.TryGetValue(key, out var st) && st.Latitude.HasValue && st.Longitude.HasValue)
                            {
                                bool isVerified = string.Equals(st.VerificationStatus, "VERIFIED", StringComparison.OrdinalIgnoreCase);
                                if (isVerified || allowUnverified)
                                {
                                    usedStores.Add(st);
                                }
                            }
                        }
                    }

                    decimal travelCost = 0m;
                    if (usedStores.Count > 0)
                    {
                        var route = StoreRouteOptimizer.OptimizeRouteOffline(
                            locationContext.OriginLatitude,
                            locationContext.OriginLongitude,
                            usedStores,
                            returnToOrigin: locationContext.ReturnToOrigin,
                            transportMode: locationContext.TransportMode,
                            costPerKm: locationContext.CostPerKm
                        );
                        travelCost = route.EstimatedTravelCost;
                    }

                    var penaltyCost = Math.Max(0, storesUsed - 1) * storePenalty;
                    strategyCost = (cost * locationContext.PriceWeight) +
                                   (travelCost * locationContext.DistanceWeight) +
                                   (penaltyCost * locationContext.ConvenienceWeight);
                }
                else
                {
                    strategyCost += Math.Max(0, storesUsed - 1) * storePenalty;
                }
            }

            bool isBetter = false;
            if (bestSubset == null)
            {
                isBetter = true;
            }
            else if (covered > bestCovered)
            {
                isBetter = true;
            }
            else if (covered == bestCovered)
            {
                if (strategy == OptimizationStrategy.MinComplexity)
                {
                    if (storesUsed < bestStoresUsed)
                        isBetter = true;
                    else if (storesUsed == bestStoresUsed && cost < bestCost)
                        isBetter = true;
                }
                else if (strategy == OptimizationStrategy.Balanced)
                {
                    if (strategyCost < bestScore)
                        isBetter = true;
                    else if (strategyCost == bestScore && storesUsed < bestStoresUsed)
                        isBetter = true;
                }
                else // MaxSavings or SingleStore
                {
                    if (cost < bestCost)
                        isBetter = true;
                    else if (cost == bestCost && storesUsed < bestStoresUsed)
                        isBetter = true;
                }
            }

            if (isBetter)
            {
                bestSubset = subset;
                bestCovered = covered;
                bestCost = cost;
                bestStoresUsed = storesUsed;
                bestScore = strategyCost;
            }
        }

        if (bestSubset == null)
        {
            return new EvaluatedSubset(0, 0m, 0, [], [], []);
        }

        return MaterializeSubset(items, itemStoreBestMap, itemStoreOverbuyMap, bestSubset);
    }

    private static void ScoreIndexedSubset(
        int itemsCount,
        int storeCount,
        OptimizerCandidate?[] indexedBest,
        OptimizerCandidate?[] indexedOverbuy,
        int[] subIndices,
        out int itemsCovered,
        out decimal totalCost,
        out int storesUsedCount,
        out int usedMask)
    {
        itemsCovered = 0;
        totalCost = 0m;
        usedMask = 0;
        int subLen = subIndices.Length;

        for (int i = 0; i < itemsCount; i++)
        {
            int rowOffset = i * storeCount;
            OptimizerCandidate? picked = null;
            int pickedStoreIdx = -1;

            for (int s = 0; s < subLen; s++)
            {
                var cand = indexedBest[rowOffset + subIndices[s]];
                if (cand != null)
                {
                    if (picked == null || CompareCandidates(cand, picked) < 0)
                    {
                        picked = cand;
                        pickedStoreIdx = s;
                    }
                }
            }

            if (picked == null)
            {
                for (int s = 0; s < subLen; s++)
                {
                    var cand = indexedOverbuy[rowOffset + subIndices[s]];
                    if (cand != null)
                    {
                        if (picked == null || CompareOverbuyCandidates(cand, picked) < 0)
                        {
                            picked = cand;
                            pickedStoreIdx = s;
                        }
                    }
                }
            }

            if (picked != null)
            {
                itemsCovered++;
                totalCost += picked.TotalCost;
                if (pickedStoreIdx >= 0 && pickedStoreIdx < 32)
                {
                    usedMask |= (1 << pickedStoreIdx);
                }
            }
        }

        storesUsedCount = System.Numerics.BitOperations.PopCount((uint)usedMask);
    }

    private static EvaluatedSubset MaterializeSubset(
        IReadOnlyList<BasketItem> items,
        Dictionary<Guid, Dictionary<string, OptimizerCandidate>> itemStoreBestMap,
        Dictionary<Guid, Dictionary<string, OptimizerCandidate>> itemStoreOverbuyMap,
        List<string> storeSubset)
    {
        var assignments = new List<OptimizerItemAssignment>(items.Count);
        var missing = new List<OptimizerMissingItem>();
        decimal totalCost = 0m;
        var usedStores = new HashSet<string>(storeSubset.Count);

        foreach (var item in items)
        {
            var storeBest = itemStoreBestMap[item.Id];
            OptimizerCandidate? picked = null;

            foreach (var storeKey in storeSubset)
            {
                if (storeBest.TryGetValue(storeKey, out var cand))
                {
                    if (picked == null || CompareCandidates(cand, picked) < 0)
                    {
                        picked = cand;
                    }
                }
            }

            if (picked == null)
            {
                var storeOverbuy = itemStoreOverbuyMap[item.Id];
                foreach (var storeKey in storeSubset)
                {
                    if (storeOverbuy.TryGetValue(storeKey, out var cand))
                    {
                        if (picked == null || CompareOverbuyCandidates(cand, picked) < 0)
                        {
                            picked = cand;
                        }
                    }
                }
            }

            if (picked == null)
            {
                missing.Add(new OptimizerMissingItem(
                    BasketItemId: item.Id,
                    ItemQuery: item.Query,
                    RequestedQuantity: item.Quantity,
                    Reason: "No hay oferta disponible en las tiendas de esta combinación."
                ));
                continue;
            }

            usedStores.Add(picked.StoreKey);
            totalCost += picked.TotalCost;

            var isSubstitute = picked.MatchKind == MatchKind.Substitute;
            var reason = isSubstitute
                ? $"Sustituto aceptado ({picked.Brand}): {picked.Explanation}"
                : "Coincidencia exacta con la necesidad solicitada.";

            assignments.Add(new OptimizerItemAssignment(
                BasketItemId: item.Id,
                ItemQuery: item.Query,
                RequestedQuantity: item.Quantity,
                Candidate: picked,
                IsSubstitute: isSubstitute,
                AssignmentReason: reason
            ));
        }

        var buckets = assignments
            .GroupBy(a => a.Candidate.StoreKey)
            .Select(g =>
            {
                var first = g.First().Candidate;
                return new OptimizerStoreBucket(
                    StoreKey: g.Key,
                    StoreName: first.StoreName,
                    RetailerCode: first.RetailerCode,
                    Subtotal: g.Sum(x => x.Candidate.TotalCost),
                    Items: g.ToList()
                );
            })
            .OrderByDescending(b => b.Subtotal)
            .ToList();

        return new EvaluatedSubset(
            StoresUsedCount: usedStores.Count,
            TotalCost: totalCost,
            ItemsCoveredCount: assignments.Count,
            Stores: buckets,
            Assignments: assignments,
            MissingItems: missing
        );
    }

    private static int CompareCandidates(OptimizerCandidate a, OptimizerCandidate b)
    {
        int c = a.IsFallbackContext.CompareTo(b.IsFallbackContext);
        if (c != 0) return c;

        c = b.IsPreferredBrand.CompareTo(a.IsPreferredBrand);
        if (c != 0) return c;

        c = a.TotalCost.CompareTo(b.TotalCost);
        if (c != 0) return c;

        bool aExact = a.MatchKind == MatchKind.Exact;
        bool bExact = b.MatchKind == MatchKind.Exact;
        c = bExact.CompareTo(aExact);
        if (c != 0) return c;

        c = a.OverbuyPercent.CompareTo(b.OverbuyPercent);
        if (c != 0) return c;

        bool aFresh = a.Freshness == Freshness.Fresh;
        bool bFresh = b.Freshness == Freshness.Fresh;
        c = bFresh.CompareTo(aFresh);
        if (c != 0) return c;

        return a.OfferId.CompareTo(b.OfferId);
    }

    private static int CompareOverbuyCandidates(OptimizerCandidate a, OptimizerCandidate b)
    {
        int c = a.IsFallbackContext.CompareTo(b.IsFallbackContext);
        if (c != 0) return c;

        c = a.OverbuyPercent.CompareTo(b.OverbuyPercent);
        if (c != 0) return c;

        c = a.TotalCost.CompareTo(b.TotalCost);
        if (c != 0) return c;

        return a.OfferId.CompareTo(b.OfferId);
    }

    private static List<List<string>> GenerateSubsets(List<string> stores, int maxK)
    {
        var result = new List<List<string>>();
        for (int k = 1; k <= maxK; k++)
        {
            GenerateCombinations(stores, k, 0, [], result);
        }
        return result;
    }

    private static void GenerateCombinations(
        List<string> stores,
        int k,
        int startIndex,
        List<string> current,
        List<List<string>> result)
    {
        if (current.Count == k)
        {
            result.Add([.. current]);
            return;
        }

        for (int i = startIndex; i < stores.Count; i++)
        {
            current.Add(stores[i]);
            GenerateCombinations(stores, k, i + 1, current, result);
            current.RemoveAt(current.Count - 1);
        }
    }

    private sealed record EvaluatedSubset(
        int StoresUsedCount,
        decimal TotalCost,
        int ItemsCoveredCount,
        IReadOnlyList<OptimizerStoreBucket> Stores,
        IReadOnlyList<OptimizerItemAssignment> Assignments,
        IReadOnlyList<OptimizerMissingItem> MissingItems
    );
}
