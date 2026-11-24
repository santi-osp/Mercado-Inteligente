// Domain and DTO models for Mercado Inteligente Frontend

export type AddressComponents = {
  country?: string | null;
  countryCode?: string | null;
  state?: string | null;
  stateCode?: string | null;
  city?: string | null;
  postalCode?: string | null;
  neighborhood?: string | null;
  street?: string | null;
  streetNumber?: string | null;
};

export type GeocodingResultDto = {
  latitude: number;
  longitude: number;
  formattedAddress: string;
  providerStatus: string;
  components?: AddressComponents | null;
};

export type TravelLocationDto = {
  storeId: string | null;
  storeName: string;
  retailerCode: string;
  latitude: number;
  longitude: number;
  distanceKm: number | null;
  isPhysicalStoreProxy: boolean;
  verificationStatus: string;
  transparencyNotice: string;
};
export type Settings = { maxStores: number; sizeTolerancePercent: number; allowSubstitutes: boolean; maxOverbuyPercent: number; priceTtlHours?: number; includeMarketplace?: boolean; allowUnverifiedLocationsForTravel?: boolean };
export type Health = { database: string; products: number; ai: string; google: string; mode: string };
export type ProviderCapabilityState = 'Supported' | 'Partial' | 'NotSupported' | 'NotConfigured';
export type ProviderCapabilitiesDto = {
  search?: ProviderCapabilityState;
  details?: ProviderCapabilityState;
  offers?: ProviderCapabilityState;
  stores?: ProviderCapabilityState;
  availability?: ProviderCapabilityState;
  catalogEnumeration?: ProviderCapabilityState;
};
export type Provider = {
  code: string;
  name?: string;
  retailerType?: string;
  catalogCoverage?: string;
  capabilities?: ProviderCapabilitiesDto;
  status: string;
  enabled?: boolean;
  priceTtlHours?: number;
  supportsManualOffers?: boolean;
  description?: string;
  websiteUrl?: string | null;
};
export type AraHealthReport = {
  provider: string;
  status: string;
  coverage: string;
  totalActiveOffers: number;
  brochureOffersCount: number;
  manualOffersCount: number;
  lastOfferUpdate: string | null;
  summary: string;
};
export type AraManualOffer = {
  offerId: string;
  productId: string;
  productName: string;
  brand: string | null;
  skuQuantity: number | null;
  skuDimension: string;
  price: number;
  listPrice: number | null;
  storeName: string | null;
  scope: string;
  checkedAt: string;
  validUntil: string | null;
  notes: string | null;
};
export type CreateAraManualOfferRequest = {
  productName: string;
  brand?: string;
  quantity?: number;
  unit?: string;
  price: number;
  listPrice?: number;
  storeName?: string;
  validUntil?: string;
  notes?: string;
};
export type D1Probe = { checkedAt: string; search: string; genericOnlinePrice: string };
export type D1Health = { status: string; lastRun: { state: number | string; finishedAt: string | null; errorCode: string | null; itemsReceived: number; offersProcessed: number; offersChanged: number; productsCreated: number; productsUpdated: number; skusCreated: number; skusUpdated: number; rawNew: number; rawDeduplicated: number } | null };
export type ExitoProbe = { checkedAt: string; search: string; genericOnlinePrice: string };
export type ExitoRegionalProbe = { checkedAt: string; regionContext: string; regionalCatalog: string; regionalOnlinePrice: string; errorCode: string | null };
export type ExitoHealth = { status: string; lastRun: { state: number | string; finishedAt: string | null; errorCode: string | null; itemsReceived: number; offersProcessed: number; offersChanged: number; productsCreated: number; productsUpdated: number; skusCreated: number; skusUpdated: number; rawNew: number; rawDeduplicated: number } | null };
export type SecretType = 'google' | 'openrouter' | 'omnirouter';
export type SecretEntry = { type: SecretType; label: string; configured: boolean | null; loading: boolean; busy: boolean; error: string; notice: string; draft: string };
export type D1Offer = { id: string; productName: string; skuName: string; ean: string | null; price: number; listPrice: number | null; scope: string | number; availability: string | number; context: string; checkedAt: string; sellerName: string; sellerKind: string | number };
export type ExitoOffer = { id: string; productName: string; skuName: string; ean: string | null; price: number; listPrice: number | null; scope: string | number; availability: string | number; context: string; checkedAt: string; sellerName: string; sellerKind: string | number };
export type LocalProduct = { id: string; retailerId: string; externalId: string; name: string; brand: string | null; updatedAt: string };
export type BasketItem = { id: string; basketId: string; query: string; quantity: number; productVariantId: string | null; notes: string | null };
export type Basket = { id: string; name: string; createdAt: string; items: BasketItem[] };
export type StorageStats = { databaseBytes: number; sidecarsBytes: number; rawBytes: number; logsBytes: number; backupsBytes: number; externalProductsCount: number; externalSkusCount: number; offersCount: number; priceObservationsCount: number };
export type BackupFileDto = {
  name: string;
  bytes: number;
  createdAtUtc: string;
  type: string;
  integrityVerified?: boolean | null;
  schemaVersion?: string | null;
  label?: string | null;
};

export type BackupVerificationResultDto = {
  name: string;
  valid: boolean;
  integrity: string;
  foreignKeysValid: boolean;
  message?: string | null;
};

export type DatabaseStatsDto = {
  databaseBytes: number;
  rawBytes: number;
  backupsBytes: number;
  logsBytes: number;
  externalProductsCount: number;
  externalSkusCount: number;
  offersCount: number;
  priceObservationsCount: number;
  basketsCount: number;
  basketItemsCount: number;
  rawCapturesCount: number;
  retailerProductCounts: Record<string, number>;
  integrityStatus: string;
  foreignKeysValid: boolean;
  lastSyncUtc: string | null;
  backupsCount: number;
  latestBackupUtc: string | null;
};

export type DatabaseActionResultDto = {
  success: boolean;
  message: string;
  preActionBackup?: string | null;
};

export type UnitPriceInfoDto = {
  pricePerBaseUnit: number;
  baseUnit: string;
  dimension: string;
  totalAmount: number;
  originalPackage: string;
};

export type PresentationComparisonItemDto = {
  skuId: string;
  skuName: string;
  package: string;
  price: number;
  unitPrice: number | null;
  unitLabel: string | null;
  savingsPercentVsCheapestUnit: number | null;
};

export type RetailerPriceItemDto = {
  retailerCode: string;
  retailerName: string;
  price: number;
  listPrice: number | null;
  discountPercent: number | null;
  priceScope: string;
  isVerified: boolean;
  diffVsCheapest: number | null;
};

export type ProductAnalysisResultDto = {
  productId: string;
  productName: string;
  brand: string | null;
  category: string | null;
  currentBestPrice: number;
  cheapestRetailer: string;
  bestUnitPrice: UnitPriceInfoDto | null;
  minHistoricalPrice: number | null;
  maxHistoricalPrice: number | null;
  avgHistoricalPrice: number | null;
  priceTrend: string;
  trendPercentVsAvg: number | null;
  isGoodTimeToBuy: boolean | null;
  goodTimeToBuyReason: string | null;
  valueScore: number;
  confidence: string;
  valueExplanation: string;
  presentations: PresentationComparisonItemDto[];
  retailerPrices: RetailerPriceItemDto[];
  observationsCount: number;
};

export type OpportunityItemDto = {
  productId: string;
  productName: string;
  brand: string | null;
  retailerCode: string;
  retailerName: string;
  price: number;
  listPrice: number | null;
  discountPercent: number | null;
  unitPriceText: string | null;
  valueScore: number;
  highlight: string;
  imageUrl: string | null;
};

export type MarketOpportunitiesResultDto = {
  bestUnitPrices: OpportunityItemDto[];
  highestDiscounts: OpportunityItemDto[];
  bestValueItems: OpportunityItemDto[];
};

export type BasketItemAnalysisDto = {
  itemId: string;
  query: string;
  selectedProductName: string | null;
  price: number | null;
  valueScore: number | null;
  suggestion: string | null;
};

export type BasketAnalysisResultDto = {
  basketId: string;
  totalItems: number;
  estimatedTotal: number;
  potentialSavings: number;
  recommendations: string[];
  itemBreakdowns: BasketItemAnalysisDto[];
};
export type PriceObservationDto = {
  id: string;
  offerId: string;
  productName: string;
  brand: string | null;
  skuName: string;
  ean: string | null;
  retailerCode: string;
  retailerName: string;
  sellerName: string;
  price: number;
  listPrice: number | null;
  availability: string;
  scope: string;
  context: string;
  checkedAt: string;
};
export type ProductTrendDto = {
  productName: string;
  skuName: string;
  retailerCode: string;
  count: number;
  minPrice: number;
  maxPrice: number;
  latestPrice: number;
  priceDelta: number;
  latestCheckedAt: string;
};
export type HistoryResponse = {
  totalObservations: number;
  statistics: {
    totalObservations: number;
    minPrice: number | null;
    maxPrice: number | null;
    avgPrice: number | null;
    uniqueProducts: number;
    uniqueRetailers: number;
  };
  productTrends: ProductTrendDto[];
  observations: PriceObservationDto[];
};
export type SystemStatusDto = {
  version: string;
  appStatus: string;
  database: {
    integrity: string;
    foreignKeysValid: boolean;
    sizeBytes: number;
    productsCount: number;
    skusCount: number;
    offersCount: number;
    observationsCount: number;
  };
  raw: {
    sizeBytes: number;
    filesCount: number;
  };
  backups: {
    sizeBytes: number;
    filesCount: number;
    lastBackupName: string | null;
    available: BackupFileDto[];
  };
  lastSync: {
    provider: string;
    state: string;
    finishedAt: string | null;
    itemsReceived: number;
  } | null;
  providers: Provider[];
  google: {
    configured: boolean;
    status: string;
  };
  ai: {
    configured: boolean;
    status: string;
  };
  pendingExternalActions: string[];
};
export type MaintenanceReport = { databaseOptimized: boolean; foreignKeysValid: boolean; backupsPruned: number; backupsRetained: number; logsPruned: number; rawPruned: number };
export type BasketSyncResult = { basketId: string; totalItems: number; queriesExecuted: number; results: Array<{ provider: string; status: string; itemsReceived: number; offersProcessed: number; offersChanged: number; errorCode: string | null }>; shoppingContext?: { validationStatus: string; providers: Array<{ provider: string; key: string; scope: string; status: string }> } };
export type ComparisonTab = 'exactos' | 'similares' | 'otrasMarcas';
export type PairDetail = { pairId: string; description: string; expected: number | string; actual: number | string; confidence: number; evidence: string; isCorrect: boolean };
export type ComparisonPairsResponse = { summary: { totalPairs: number; exactPrecision: number; exactRecall: number; exactF1: number; exactFalsePositives: number }; exactos: PairDetail[]; similares: PairDetail[]; otrasMarcas: PairDetail[]; all: PairDetail[] };
export type CustomComparisonResult = {
  productA: { normalized: { cleanName: string; normalizedBrand: string; productType: string; variantType: string; quantity: { totalAmount: number; originalUnit: string } | null }; unitPrice: { absolutePrice: number; pricePerBaseUnit: number; formattedPricePerBaseUnit: string } | null; overbuy: { targetQuantity: number; itemQuantity: number; packagesRequired: number; totalCost: number; overbuyPercentage: number; isAcceptable: boolean; explanation: string } | null };
  productB: { normalized: { cleanName: string; normalizedBrand: string; productType: string; variantType: string; quantity: { totalAmount: number; originalUnit: string } | null }; unitPrice: { absolutePrice: number; pricePerBaseUnit: number; formattedPricePerBaseUnit: string } | null; overbuy: { targetQuantity: number; itemQuantity: number; packagesRequired: number; totalCost: number; overbuyPercentage: number; isAcceptable: boolean; explanation: string } | null };
  match: { kind: number | string; confidenceScore: number; evidence: string; hasConflict: boolean; conflictReason: string | null };
};
export type LocationInputSource = 'Coordinates' | 'GoogleMapsUrl' | 'GoogleMapsShortUrl' | 'Address' | 'Profile';
export type LocationInputStatus = 'Success' | 'NeedsGeocoding' | 'Invalid';
export type LocationParseResult = {
  originalInput: string;
  normalizedAddress: string | null;
  latitude: number | null;
  longitude: number | null;
  source: LocationInputSource;
  status: LocationInputStatus;
  errorMessage: string | null;
};
export type LocationProfile = {
  id: string;
  label: string;
  address: string | null;
  formattedAddress?: string | null;
  latitude: number | null;
  longitude: number | null;
  country?: string | null;
  countryCode?: string | null;
  state?: string | null;
  stateCode?: string | null;
  city?: string | null;
  postalCode?: string | null;
  neighborhood?: string | null;
  street?: string | null;
  number?: string | null;
  streetNumber?: string | null;
  createdAt: string;
  updatedAt?: string;
};
export type StoreWithDistance = {
  id: string;
  name: string;
  brand: string;
  address: string;
  latitude: number;
  longitude: number;
  distanceKm: number | null;
};
export type RouteType = 'RoadRoute' | 'StraightLineEstimate' | 'CachedRoute';
export type RouteLeg = {
  fromName: string;
  toName: string;
  distanceKm: number;
  durationMinutes: number;
  type: RouteType;
  provider: string;
};
export type StoreRoutePlan = {
  orderedStores: Array<{ id: string; name: string; brand: string; address: string; latitude: number; longitude: number }>;
  legs: RouteLeg[];
  totalDistanceKm: number;
  totalDurationMinutes: number;
  estimatedTravelCost: number;
  googleMapsNavigationUrl: string;
  costPerKm: number;
  distanceSource?: string;
  isApproximate?: boolean;
};
export type GoogleUsageStats = {
  geocodingConfigured: boolean;
  routingConfigured: boolean;
  todayGeocodeCalls: number;
  todayRouteCalls: number;
  history?: Array<{ date: string; geocodeCount: number; routeCount: number }>;
};
export type AiStatusResponse = {
  isConfigured: boolean;
  enabled: boolean;
  model: string;
  baseUrl: string;
  timeoutSeconds: number;
  maxTokens: number;
  temperature: number;
  features: {
    normalization: boolean;
    matching: boolean;
    substitutes: boolean;
    explanation: boolean;
  };
  monthlyBudgetUsd: number | null;
  warningBudgetUsd?: number | null;
  hardBudgetLimit?: boolean;
  budgetWarning?: boolean;
  monthlyCostUsd?: number;
  usage: {
    totalCalls: number;
    promptTokens: number;
    completionTokens: number;
    estimatedCostUsd: number;
    estimatedCostCop: number;
    cacheHits: number;
    errorsCount: number;
    recentDailyUsage: Array<{
      date: string;
      calls: number;
      promptTokens: number;
      completionTokens: number;
      costUsd: number;
    }>;
  };
};
export type AiConnectionTestResult = {
  success: boolean;
  statusCode: number;
  message: string;
  latencyMs: number;
  modelTested: string | null;
};
export type AiBasketExplanationResult = {
  summary: string;
  whyRecommended: string;
  storeTradeoffs: string;
  keySavingsSources: string[];
  warnings: string[];
  source: string;
  modelUsed: string | null;
  promptVersion: string | null;
};
export type AiMatchAdjudicationResult = {
  decision: string;
  confidence: number;
  conciseReason: string;
  attributesInCommon: string[];
  attributesDiffering: string[];
  source: string;
  modelUsed: string | null;
  promptVersion: string | null;
};
export type OptimizerItemAssignment = {
  basketItemId: string;
  itemQuery: string;
  requestedQuantity: number;
  isSubstitute: boolean;
  assignmentReason: string;
};
export type OptimizerStoreBucket = {
  storeKey: string;
  storeName: string;
  retailerCode: string;
  subtotal: number;
  travelLocation?: TravelLocationDto | null;
  items: OptimizerItemAssignment[];
};
export type OptimizerSolution = {
  strategy: number | string;
  maxStoresAllowed: number;
  storesUsedCount: number;
  totalCost: number;
  baselineCost: number;
  totalSavingsVsBaseline: number;
  totalItemsRequested: number;
  itemsCoveredCount: number;
  missingItemsCount: number;
  stores: OptimizerStoreBucket[];
  warnings: string[];
  routePlan: StoreRoutePlan | null;
};

export type FacetCount = {
  value: string;
  count: number;
};

export type ProductSearchFacets = {
  retailers: FacetCount[];
  brands: FacetCount[];
  categories: FacetCount[];
  minPrice: number;
  maxPrice: number;
};

export type ProductSearchResultItem = {
  productId: string;
  externalSkuId: string;
  name: string;
  brand: string | null;
  presentation: string | null;
  imageUrl: string | null;
  retailer: string;
  retailerCode: string;
  price: number;
  listPrice: number | null;
  discountPercentage: number | null;
  promotion: string | null;
  unitPrice: number | null;
  unitLabel: string | null;
  availability: string;
  freshness: string;
  priceScope: string;
  updatedAt: string;
  productUrl: string | null;
  categoryPath: string | null;
  isMarketplace: boolean;
  sellerDisplay: string | null;
  isFallbackContext: boolean;
  contextWarning: string | null;
  liveStatus?: string | null;
  unitPriceDisplay?: string | null;
  unitPriceUnit?: string | null;
  pricingMode?: string | null;
  priceBasis?: string | null;
  priceNotes?: string | null;
};

export type ProductSearchFilters = {
  retailers?: string[] | null;
  brands?: string[] | null;
  categories?: string[] | null;
  minPrice?: number | null;
  maxPrice?: number | null;
  inStockOnly?: boolean | null;
  promoOnly?: boolean | null;
};

export type ProductSearchResponse = {
  total: number;
  totalCount?: number;
  page: number;
  pageSize: number;
  query?: string | null;
  items: ProductSearchResultItem[];
  facets: ProductSearchFacets;
  warnings: string[];
  totalPages?: number;
  hasPrevious?: boolean;
  hasNext?: boolean;
};

export type AssistantProductDto = {
  name: string;
  brand?: string | null;
  presentation?: string | null;
  price: number;
  listPrice?: number | null;
  unitPrice?: number | null;
  unitLabel?: string | null;
  retailer: string;
  availability: string;
  productUrl?: string | null;
  imageUrl?: string | null;
};

export type AssistantProductRecommendationDto = {
  name: string;
  retailer: string;
  price: number;
  reason?: string | null;
  productUrl?: string | null;
};

export type ShoppingAssistantRequest = {
  message: string;
  pageContext: string;
  visibleProducts?: AssistantProductDto[];
  basketId?: string | null;
  comparisonData?: string | null;
};

export type ShoppingAssistantResponse = {
  reply: string;
  recommendedProducts?: AssistantProductRecommendationDto[];
  status: string;
  modelUsed?: string | null;
};

export type CatalogCategory = {
  id: string;
  name: string;
  parentId?: string | null;
  estimatedCount?: number | null;
  children?: CatalogCategory[] | null;
};

export type CatalogImportJob = {
  id: string;
  retailerCode: string;
  retailerName: string;
  categoryIds: string[];
  currentCategoryName?: string | null;
  totalItems: number;
  processedItems: number;
  newItems: number;
  updatedItems: number;
  unchangedItems: number;
  errorCount: number;
  status: "Pending" | "Running" | "Completed" | "Failed" | "Cancelled" | "Partial";
  errorMessage?: string | null;
  startedAt: string;
  finishedAt?: string | null;
};
