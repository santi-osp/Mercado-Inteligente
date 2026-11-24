import { APP_VERSION } from './version';
import { Component, ViewEncapsulation, forwardRef, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DecimalPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { pagePaths } from './app.routes';
import { LocationFacade } from './facades/location.facade';
import { BasketFacade } from './facades/basket.facade';
import { ProviderStatusFacade } from './facades/provider-status.facade';
import { SettingsFacade } from './facades/settings.facade';
import { CatalogSearchFacade } from './facades/catalog-search.facade';
import { ShoppingAssistantFacade } from './facades/shopping-assistant.facade';
import { MARKET_SHELL } from './feature-host';
import { AddressComponents, GeocodingResultDto, TravelLocationDto } from './models';
type Settings = { maxStores: number; sizeTolerancePercent: number; allowSubstitutes: boolean; maxOverbuyPercent: number; priceTtlHours?: number; includeMarketplace?: boolean; allowUnverifiedLocationsForTravel?: boolean };
type Health = { database: string; products: number; ai: string; google: string; mode: string };
type ProviderCapabilityState = 'Supported' | 'Partial' | 'NotSupported' | 'NotConfigured';
type ProviderCapabilitiesDto = {
  search?: ProviderCapabilityState;
  details?: ProviderCapabilityState;
  offers?: ProviderCapabilityState;
  stores?: ProviderCapabilityState;
  availability?: ProviderCapabilityState;
  catalogEnumeration?: ProviderCapabilityState;
};
type Provider = {
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
type AraHealthReport = {
  provider: string;
  status: string;
  coverage: string;
  totalActiveOffers: number;
  brochureOffersCount: number;
  manualOffersCount: number;
  lastOfferUpdate: string | null;
  summary: string;
};
type AraManualOffer = {
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
type CreateAraManualOfferRequest = {
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
type D1Probe = { checkedAt: string; search: string; genericOnlinePrice: string };
type D1Health = { status: string; lastRun: { state: number | string; finishedAt: string | null; errorCode: string | null; itemsReceived: number; offersProcessed: number; offersChanged: number; productsCreated: number; productsUpdated: number; skusCreated: number; skusUpdated: number; rawNew: number; rawDeduplicated: number } | null };
type ExitoProbe = { checkedAt: string; search: string; genericOnlinePrice: string };
type ExitoRegionalProbe = { checkedAt: string; regionContext: string; regionalCatalog: string; regionalOnlinePrice: string; errorCode: string | null };
type ExitoHealth = { status: string; lastRun: { state: number | string; finishedAt: string | null; errorCode: string | null; itemsReceived: number; offersProcessed: number; offersChanged: number; productsCreated: number; productsUpdated: number; skusCreated: number; skusUpdated: number; rawNew: number; rawDeduplicated: number } | null };
type SecretType = 'google' | 'openrouter';
type SecretEntry = { type: SecretType; label: string; configured: boolean | null; loading: boolean; busy: boolean; error: string; notice: string; draft: string };
type D1Offer = { id: string; productName: string; skuName: string; ean: string | null; price: number; listPrice: number | null; scope: string | number; availability: string | number; context: string; checkedAt: string; sellerName: string; sellerKind: string | number };
type ExitoOffer = { id: string; productName: string; skuName: string; ean: string | null; price: number; listPrice: number | null; scope: string | number; availability: string | number; context: string; checkedAt: string; sellerName: string; sellerKind: string | number };
type LocalProduct = { id: string; retailerId: string; externalId: string; name: string; brand: string | null; updatedAt: string };
type BasketItem = { id: string; basketId: string; query: string; quantity: number; productVariantId: string | null; notes: string | null };
type Basket = { id: string; name: string; createdAt: string; items: BasketItem[] };
type StorageStats = { databaseBytes: number; sidecarsBytes: number; rawBytes: number; logsBytes: number; backupsBytes: number; externalProductsCount: number; externalSkusCount: number; offersCount: number; priceObservationsCount: number };
type BackupFileDto = { name: string; bytes: number; createdAtUtc: string; type: string };
type PriceObservationDto = {
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
type ProductTrendDto = {
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
type HistoryResponse = {
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
type SystemStatusDto = {
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
type MaintenanceReport = { databaseOptimized: boolean; foreignKeysValid: boolean; backupsPruned: number; backupsRetained: number; logsPruned: number; rawPruned: number };
type BasketSyncResult = { basketId: string; totalItems: number; queriesExecuted: number; results: Array<{ provider: string; status: string; itemsReceived: number; offersProcessed: number; offersChanged: number; errorCode: string | null }>; shoppingContext?: { validationStatus: string; providers: Array<{ provider: string; key: string; scope: string; status: string }> } };
type ComparisonTab = 'exactos' | 'similares' | 'otrasMarcas';
type PairDetail = { pairId: string; description: string; expected: number | string; actual: number | string; confidence: number; evidence: string; isCorrect: boolean };
type ComparisonPairsResponse = { summary: { totalPairs: number; exactPrecision: number; exactRecall: number; exactF1: number; exactFalsePositives: number }; exactos: PairDetail[]; similares: PairDetail[]; otrasMarcas: PairDetail[]; all: PairDetail[] };
type CustomComparisonResult = {
  productA: { normalized: { cleanName: string; normalizedBrand: string; productType: string; variantType: string; quantity: { totalAmount: number; originalUnit: string } | null }; unitPrice: { absolutePrice: number; pricePerBaseUnit: number; formattedPricePerBaseUnit: string } | null; overbuy: { targetQuantity: number; itemQuantity: number; packagesRequired: number; totalCost: number; overbuyPercentage: number; isAcceptable: boolean; explanation: string } | null };
  productB: { normalized: { cleanName: string; normalizedBrand: string; productType: string; variantType: string; quantity: { totalAmount: number; originalUnit: string } | null }; unitPrice: { absolutePrice: number; pricePerBaseUnit: number; formattedPricePerBaseUnit: string } | null; overbuy: { targetQuantity: number; itemQuantity: number; packagesRequired: number; totalCost: number; overbuyPercentage: number; isAcceptable: boolean; explanation: string } | null };
  match: { kind: number | string; confidenceScore: number; evidence: string; hasConflict: boolean; conflictReason: string | null };
};
type LocationInputSource = 'Coordinates' | 'GoogleMapsUrl' | 'GoogleMapsShortUrl' | 'Address' | 'Profile';
type LocationInputStatus = 'Success' | 'NeedsGeocoding' | 'Invalid';
type LocationParseResult = {
  originalInput: string;
  normalizedAddress: string | null;
  latitude: number | null;
  longitude: number | null;
  source: LocationInputSource;
  status: LocationInputStatus;
  errorMessage: string | null;
};
type LocationProfile = {
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
  updatedAt?: string | null;
};
type StoreWithDistance = {
  id: string;
  name: string;
  brand: string;
  address: string;
  latitude: number;
  longitude: number;
  distanceKm: number | null;
};
type RouteType = 'RoadRoute' | 'StraightLineEstimate' | 'CachedRoute';
type RouteLeg = {
  fromName: string;
  toName: string;
  distanceKm: number;
  durationMinutes: number;
  type: RouteType;
  provider: string;
};
type StoreRoutePlan = {
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
type GoogleUsageStats = {
  geocodingConfigured: boolean;
  routingConfigured: boolean;
  todayGeocodeCalls: number;
  todayRouteCalls: number;
  history?: Array<{ date: string; geocodeCount: number; routeCount: number }>;
};
type AiStatusResponse = {
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
type AiConnectionTestResult = {
  success: boolean;
  statusCode: number;
  message: string;
  latencyMs: number;
  modelTested: string | null;
};
type AiBasketExplanationResult = {
  summary: string;
  whyRecommended: string;
  storeTradeoffs: string;
  keySavingsSources: string[];
  warnings: string[];
  source: string;
  modelUsed: string | null;
  promptVersion: string | null;
};
type AiMatchAdjudicationResult = {
  decision: string;
  confidence: number;
  conciseReason: string;
  attributesInCommon: string[];
  attributesDiffering: string[];
  source: string;
  modelUsed: string | null;
  promptVersion: string | null;
};
type OptimizerItemAssignment = {
  basketItemId: string;
  itemQuery: string;
  requestedQuantity: number;
  isSubstitute: boolean;
  assignmentReason: string;
};
type OptimizerStoreBucket = {
  storeKey: string;
  storeName: string;
  retailerCode: string;
  subtotal: number;
  travelLocation?: TravelLocationDto | null;
  items: OptimizerItemAssignment[];
};
type OptimizerSolution = {
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
@Component({ selector: 'app-root', imports: [RouterOutlet, FormsModule, DecimalPipe], providers: [{ provide: MARKET_SHELL, useExisting: forwardRef(() => App) }], templateUrl: './app.html', styleUrl: './app.css', encapsulation: ViewEncapsulation.None })
export class App {
  readonly appVersion = APP_VERSION;
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router, { optional: true });
  readonly locationFacade = inject(LocationFacade);
  readonly basketFacade = inject(BasketFacade);
  readonly providerStatusFacade = inject(ProviderStatusFacade);
  readonly settingsFacade = inject(SettingsFacade);
  readonly catalogSearchFacade = inject(CatalogSearchFacade);
  readonly shoppingAssistantFacade = inject(ShoppingAssistantFacade);

  // Location Facade Delegations
  get locationInput(): string { return this.locationFacade.locationInput(); }
  set locationInput(val: string) { this.locationFacade.locationInput.set(val); }

  get newProfileLabel(): string { return this.locationFacade.newProfileLabel(); }
  set newProfileLabel(val: string) { this.locationFacade.newProfileLabel.set(val); }

  get profileStreet(): string { return this.locationFacade.profileStreet(); }
  set profileStreet(val: string) { this.locationFacade.profileStreet.set(val); }

  get profileNumber(): string { return this.locationFacade.profileNumber(); }
  set profileNumber(val: string) { this.locationFacade.profileNumber.set(val); }

  get profileNeighborhood(): string { return this.locationFacade.profileNeighborhood(); }
  set profileNeighborhood(val: string) { this.locationFacade.profileNeighborhood.set(val); }

  get profilePostalCode(): string { return this.locationFacade.profilePostalCode(); }
  set profilePostalCode(val: string) { this.locationFacade.profilePostalCode.set(val); }

  get profileCity(): string { return this.locationFacade.profileCity(); }
  set profileCity(val: string) { this.locationFacade.profileCity.set(val); }

  get profileState(): string { return this.locationFacade.profileState(); }
  set profileState(val: string) { this.locationFacade.profileState.set(val); }

  readonly currentFormattedAddress = this.locationFacade.currentFormattedAddress;
  readonly currentAddressComponents = this.locationFacade.currentAddressComponents;
  readonly incompleteAddressNotice = this.locationFacade.incompleteAddressNotice;
  readonly currentLatitude = this.locationFacade.currentLatitude;
  readonly currentLongitude = this.locationFacade.currentLongitude;
  readonly currentAddress = this.locationFacade.currentAddress;
  readonly locationParseResult = this.locationFacade.locationParseResult;
  readonly locationBusy = this.locationFacade.locationBusy;
  readonly locationError = this.locationFacade.locationError;
  readonly locationNotice = this.locationFacade.locationNotice;
  readonly locationProfiles = this.locationFacade.locationProfiles;
  readonly selectedProfileId = this.locationFacade.selectedProfileId;
  readonly nearbyStores = this.locationFacade.nearbyStores;
  readonly storesLoading = this.locationFacade.storesLoading;
  readonly storesError = this.locationFacade.storesError;
  readonly selectedStoreIds = this.locationFacade.selectedStoreIds;
  readonly routePlan = this.locationFacade.routePlan;
  readonly routeBusy = this.locationFacade.routeBusy;
  readonly routeError = this.locationFacade.routeError;
  readonly transportMode = this.locationFacade.transportMode;
  readonly returnToOrigin = this.locationFacade.returnToOrigin;
  readonly costPerKm = this.locationFacade.costPerKm;
  readonly googleUsage = this.locationFacade.googleUsage;
  readonly googleUsageLoading = this.locationFacade.googleUsageLoading;
  readonly googleUsageError = this.locationFacade.googleUsageError;

  // Basket Facade Delegations
  get query(): string { return this.basketFacade.query; }
  set query(val: string) { this.basketFacade.query = val; }
  get quantity(): number { return this.basketFacade.quantity; }
  set quantity(val: number) { this.basketFacade.quantity = val; }
  get notes(): string { return this.basketFacade.notes; }
  set notes(val: string) { this.basketFacade.notes = val; }
  get selectedBasket(): Basket | undefined { return this.basketFacade.selectedBasket; }

  readonly baskets = this.basketFacade.baskets;
  readonly selectedBasketId = this.basketFacade.selectedBasketId;
  readonly marketLoading = this.basketFacade.marketLoading;
  readonly marketBusy = this.basketFacade.marketBusy;
  readonly deletingItemId = this.basketFacade.deletingItemId;
  readonly marketError = this.basketFacade.marketError;
  readonly itemError = this.basketFacade.itemError;
  readonly basketSyncBusy = this.basketFacade.basketSyncBusy;
  readonly basketSyncNotice = this.basketFacade.basketSyncNotice;
  readonly basketSyncError = this.basketFacade.basketSyncError;
  readonly basketOptimization = this.basketFacade.basketOptimization;
  readonly basketOptimizationBusy = this.basketFacade.basketOptimizationBusy;
  readonly basketOptimizationError = this.basketFacade.basketOptimizationError;
  readonly basketExplanation = this.basketFacade.basketExplanation;
  readonly basketExplanationBusy = this.basketFacade.basketExplanationBusy;
  readonly basketExplanationError = this.basketFacade.basketExplanationError;

  readonly page = signal('Inicio');
  readonly mobileMenuOpen = signal(false);
  toggleMobileMenu() { this.mobileMenuOpen.update(v => !v); }
  closeMobileMenu() { this.mobileMenuOpen.set(false); }
  readonly mobileMoreOpen = signal(false);
  toggleMobileMore() { this.mobileMoreOpen.update(v => !v); }
  closeMobileMore() { this.mobileMoreOpen.set(false); }
  goToUiKit() { if (this.router) void this.router.navigateByUrl('/dev/ui-kit'); }
  readonly nav = ['Inicio', 'Buscar', 'Mi mercado', 'Comparar', 'Análisis', 'Historial', 'Fuentes', 'Ajustes'];
  readonly health = signal<Health | null>(null);
  readonly settings = signal<Settings | null>(null);
  readonly message = signal('');
  readonly connectionError = signal(false);
  readonly secrets = signal<SecretEntry[]>([
    { type: 'google', label: 'Google', configured: null, loading: false, busy: false, error: '', notice: '', draft: '' },
    { type: 'openrouter', label: 'OpenRouter', configured: null, loading: false, busy: false, error: '', notice: '', draft: '' }
  ]);
  readonly providers = signal<Provider[] | null>(null);
  readonly d1Health = signal<D1Health | null>(null);
  readonly d1HealthError = signal('');
  readonly d1Probe = signal<D1Probe | null>(null);
  readonly d1ProbeBusy = signal(false);
  readonly d1ProbeError = signal('');
  readonly exitoHealth = signal<ExitoHealth | null>(null);
  readonly exitoHealthError = signal('');
  readonly exitoProbe = signal<ExitoProbe | null>(null);
  readonly exitoProbeBusy = signal(false);
  readonly exitoProbeError = signal('');
  readonly exitoRegionalProbe = signal<ExitoRegionalProbe | null>(null);
  readonly exitoRegionalProbeBusy = signal(false);
  readonly exitoRegionalProbeError = signal('');
  readonly providersLoading = signal(false);
  readonly providersError = signal('');
  readonly products = signal<LocalProduct[] | null>(null);
  readonly d1Offers = signal<D1Offer[] | null>(null);
  readonly d1Busy = signal(false);
  readonly d1Error = signal('');
  readonly d1Notice = signal('');
  readonly exitoOffers = signal<ExitoOffer[] | null>(null);
  readonly exitoBusy = signal(false);
  readonly exitoError = signal('');
  readonly exitoNotice = signal('');
  readonly searchLoading = signal(false);
  readonly searchError = signal('');
  readonly searchedQuery = signal('');
  readonly storageStats = signal<StorageStats | null>(null);
  readonly storageLoading = signal(false);
  readonly storageError = signal('');
  readonly maintenanceBusy = signal(false);
  readonly maintenanceReport = signal<MaintenanceReport | null>(null);
  readonly maintenanceError = signal('');

  readonly comparisonTab = signal<ComparisonTab>('exactos');
  readonly comparisonData = signal<ComparisonPairsResponse | null>(null);
  readonly comparisonLoading = signal(false);
  readonly comparisonError = signal('');
  readonly customCompareResult = signal<CustomComparisonResult | null>(null);
  readonly customCompareBusy = signal(false);
  readonly customCompareError = signal('');
  readonly overrideNotice = signal('');


  // GOAL-08 OpenRouter e Inteligencia Artificial
  readonly aiStatus = signal<AiStatusResponse | null>(null);
  readonly aiLoading = signal(false);
  readonly aiError = signal('');
  readonly aiNotice = signal('');
  readonly aiConnectionTest = signal<AiConnectionTestResult | null>(null);
  readonly aiConnectionTestBusy = signal(false);
  readonly aiCacheClearBusy = signal(false);

  // GOAL-09 Ara signals and manual offer form
  readonly araHealth = signal<AraHealthReport | null>(null);
  readonly araHealthLoading = signal(false);
  readonly araHealthError = signal('');
  readonly araManualOffers = signal<AraManualOffer[]>([]);
  readonly araManualLoading = signal(false);
  readonly araManualBusy = signal(false);
  readonly araManualError = signal('');
  readonly araManualNotice = signal('');

  araProductName = '';
  araBrand = '';
  araQuantity: number | null = null;
  araUnit = 'g';
  araPrice: number | null = null;
  araListPrice: number | null = null;
  araStoreName = '';
  araNotes = '';



  readonly aiAdjudication = signal<AiMatchAdjudicationResult | null>(null);
  readonly aiAdjudicationBusy = signal(false);
  readonly aiAdjudicationError = signal('');

  // GOAL-10 Historial y Observabilidad
  readonly historyData = signal<HistoryResponse | null>(null);
  readonly historyLoading = signal(false);
  readonly historyError = signal('');
  historyFilterQuery = '';
  historyFilterRetailer = '';
  historyFilterScope = '';

  readonly backupsList = signal<BackupFileDto[]>([]);
  readonly backupsLoading = signal(false);
  readonly backupsError = signal('');

  readonly systemStatus = signal<SystemStatusDto | null>(null);
  readonly systemStatusLoading = signal(false);
  readonly systemStatusError = signal('');

  compNameA = 'Coca-Cola Sabor Original 1.5 L';
  compPriceA = 5500;
  compNameB = 'Coca-Cola Zero Sin Azúcar 1.5 L';
  compPriceB = 5600;
  compTargetQty = 1.5;
  searchQuery = '';
  private sourceRequest = 0;
  private searchRequest = 0;
  private d1Request = 0;
  private exitoRequest = 0;
  constructor() {
    this.refresh();
    this.router?.events.subscribe(event => {
      if (event instanceof NavigationEnd) {
        const path = event.urlAfterRedirects.split('?')[0].replace(/^\//, '');
        const title = Object.entries(pagePaths).find(([, value]) => value === path)?.[0];
        if (title && title !== this.page()) this.navigate(title, false);
      }
    });
  }
  assistantInputText = '';

  sendAssistantMessage() {
    const text = this.assistantInputText.trim();
    if (!text) return;
    this.assistantInputText = '';
    const visible = this.catalogSearchFacade.results().map(r => ({
      name: r.name,
      brand: r.brand,
      presentation: r.presentation,
      retailer: r.retailer,
      price: r.price,
      listPrice: r.listPrice,
      unitPrice: r.unitPrice,
      unitLabel: r.unitLabel,
      availability: r.availability,
      productUrl: r.productUrl,
      imageUrl: r.imageUrl
    }));
    this.shoppingAssistantFacade.sendMessage(
      text,
      this.page(),
      visible.length > 0 ? visible : undefined,
      this.selectedBasketId() || undefined
    );
  }

  sendQuickQuestion(question: string) {
    this.assistantInputText = question;
    this.sendAssistantMessage();
  }

  openAssistant() { this.shoppingAssistantFacade.open(); }
  closeAssistant() { this.shoppingAssistantFacade.close(); }
  toggleAssistant() { this.shoppingAssistantFacade.toggle(); }

  // Basket CRUD Delegations
  createBasket(name?: string, budget?: number) { return this.basketFacade.createBasket(name, budget); }
  deleteBasket(id: string) { return this.basketFacade.deleteBasket(id); }
  duplicateBasket(id: string) { return this.basketFacade.duplicateBasket(id); }
  renameBasket(id: string, name: string) { return this.basketFacade.renameBasket(id, name); }
  addProductFromSearch(basketId: string, item: { name: string; brand?: string | null; quantity?: number }) {
    return this.basketFacade.addProductFromSearch(basketId, item);
  }

  navigate(item: string, updateUrl = true) {
    this.page.set(item);
    if (updateUrl && this.router) void this.router.navigateByUrl('/' + pagePaths[item]);
    if (item === 'Mi mercado') this.loadBaskets();
    if (item === 'Fuentes' || item === 'Tiendas') this.loadProviders();
    if (item === 'Comparar') this.loadComparisonPairs();
    if (item === 'Historial') this.loadHistory();
    if (item === 'Ajustes') {
      this.loadAiStatus();
      this.secrets().forEach(secret => this.loadSecret(secret));
      this.loadStorageStats();
    }
  }
  loadComparisonPairs() {
    if (this.comparisonLoading()) return;
    this.comparisonLoading.set(true);
    this.comparisonError.set('');
    this.http.get<ComparisonPairsResponse>('/api/v1/matching/pairs').subscribe({
      next: data => { this.comparisonData.set(data); this.comparisonLoading.set(false); },
      error: () => { this.comparisonError.set('No se pudieron cargar las equivalencias.'); this.comparisonLoading.set(false); }
    });
  }
  setComparisonTab(tab: ComparisonTab) {
    this.comparisonTab.set(tab);
  }
  runCustomCompare() {
    if (this.customCompareBusy()) return;
    this.customCompareBusy.set(true);
    this.customCompareError.set('');
    this.customCompareResult.set(null);
    this.aiAdjudication.set(null);
    this.aiAdjudicationError.set('');
    this.http.post<CustomComparisonResult>('/api/v1/matching/compare', {
      nameA: this.compNameA,
      priceA: this.compPriceA,
      nameB: this.compNameB,
      priceB: this.compPriceB,
      targetQuantity: this.compTargetQty
    }).subscribe({
      next: res => { this.customCompareResult.set(res); this.customCompareBusy.set(false); },
      error: () => { this.customCompareError.set('No se pudo comparar los productos.'); this.customCompareBusy.set(false); }
    });
  }
  setOverride(pairId: string, kind: number | string) {
    this.http.post('/api/v1/matching/overrides', {
      externalSkuId: '00000000-0000-0000-0000-000000000001',
      productVariantId: '00000000-0000-0000-0000-000000000002',
      kind: kind === 'Exact' || kind === 1 ? 1 : kind === 'Substitute' || kind === 2 ? 2 : 3
    }).subscribe({
      next: () => this.overrideNotice.set(`Preferencia guardada para ${pairId}: ${kind === 1 || kind === 'Exact' ? 'Mismo producto' : kind === 2 || kind === 'Substitute' ? 'Sustituto' : 'No comparar'}.`),
      error: () => this.overrideNotice.set('No se pudo guardar la preferencia.')
    });
  }
  matchKindLabel(kind: number | string): string {
    if (kind === 1 || kind === 'Exact') return 'Exacto';
    if (kind === 2 || kind === 'Substitute') return 'Sustituto';
    if (kind === 0 || kind === 'Candidate') return 'Candidato en revisión';
    return 'No equivalente';
  }
  refresh() {
    this.http.get<Health>('/api/v1/system/health').subscribe({
      next: value => { this.health.set(value); this.connectionError.set(false); if (this.message().startsWith('Sin conexión')) this.message.set(''); },
      error: () => { this.health.set(null); this.connectionError.set(true); this.message.set('Sin conexión con el servicio local. Tus datos guardados no se han eliminado.'); }
    });
    this.http.get<Settings>('/api/v1/settings').subscribe({ next: value => this.settings.set(value), error: () => this.message.set('No se pudieron cargar los ajustes.') });
  }
  loadProviders() {
    if (this.providersLoading()) return;
    const requestId = ++this.sourceRequest;
    this.providersLoading.set(true);
    this.providersError.set('');
    this.d1HealthError.set('');
    this.exitoHealthError.set('');
    this.http.get<D1Health>('/api/v1/providers/d1/health').subscribe({
      next: value => { if (requestId === this.sourceRequest) this.d1Health.set(value); },
      error: () => { if (requestId === this.sourceRequest) this.d1HealthError.set('No se pudo consultar el historial local de D1.'); }
    });
    this.http.get<ExitoHealth>('/api/v1/providers/exito/health').subscribe({
      next: value => { if (requestId === this.sourceRequest) this.exitoHealth.set(value); },
      error: () => { if (requestId === this.sourceRequest) this.exitoHealthError.set('No se pudo consultar el historial local de Éxito.'); }
    });
    this.http.get<Provider[]>('/api/v1/providers').subscribe({
      next: providers => {
        if (requestId !== this.sourceRequest) return;
        this.providers.set(providers);
        this.providersLoading.set(false);
      },
      error: () => {
        if (requestId !== this.sourceRequest) return;
        this.providersError.set('No se pudieron cargar los estados de las fuentes. Comprueba la conexión e inténtalo de nuevo.');
        this.providersLoading.set(false);
      }
    });
  }
  probeD1() {
    if (this.d1ProbeBusy()) return;
    this.d1ProbeBusy.set(true);
    this.d1ProbeError.set('');
    this.d1Probe.set(null);
    this.http.post<D1Probe>('/api/v1/providers/d1/probe', {}).subscribe({
      next: value => { this.d1Probe.set(value); this.d1ProbeBusy.set(false); },
      error: () => { this.d1ProbeError.set('No se pudo completar el diagnóstico puntual D1.'); this.d1ProbeBusy.set(false); }
    });
  }
  probeExito() {
    if (this.exitoProbeBusy()) return;
    this.exitoProbeBusy.set(true);
    this.exitoProbeError.set('');
    this.exitoProbe.set(null);
    this.http.post<ExitoProbe>('/api/v1/providers/exito/probe', {}).subscribe({
      next: value => { this.exitoProbe.set(value); this.exitoProbeBusy.set(false); },
      error: () => { this.exitoProbeError.set('No se pudo completar el diagnóstico puntual de Éxito.'); this.exitoProbeBusy.set(false); }
    });
  }
  probeRegionalExito() {
    if (this.exitoRegionalProbeBusy()) return;
    this.exitoRegionalProbeBusy.set(true);
    this.exitoRegionalProbeError.set('');
    this.exitoRegionalProbe.set(null);
    this.http.post<ExitoRegionalProbe>('/api/v1/providers/exito/probe/regional', {}).subscribe({
      next: value => { this.exitoRegionalProbe.set(value); this.exitoRegionalProbeBusy.set(false); },
      error: () => { this.exitoRegionalProbeError.set('No se pudo completar el diagnóstico regional de Éxito.'); this.exitoRegionalProbeBusy.set(false); }
    });
  }
  loadAraHealth() {
    if (this.araHealthLoading()) return;
    this.araHealthLoading.set(true);
    this.araHealthError.set('');
    this.http.get<AraHealthReport>('/api/v1/stores/ara/health').subscribe({
      next: report => {
        this.araHealth.set(report);
        this.araHealthLoading.set(false);
      },
      error: () => {
        this.araHealthError.set('No se pudo consultar el estado de Tiendas Ara.');
        this.araHealthLoading.set(false);
      }
    });
  }
  loadAraManualOffers() {
    if (this.araManualLoading()) return;
    this.araManualLoading.set(true);
    this.araManualError.set('');
    this.http.get<AraManualOffer[]>('/api/v1/stores/ara/manual').subscribe({
      next: offers => {
        this.araManualOffers.set(offers);
        this.araManualLoading.set(false);
      },
      error: () => {
        this.araManualError.set('No se pudieron consultar los aportes comunitarios de Ara.');
        this.araManualLoading.set(false);
      }
    });
  }
  submitAraManualOffer() {
    if (this.araManualBusy() || !this.araProductName.trim() || !this.araPrice || this.araPrice <= 0) return;
    this.araManualBusy.set(true);
    this.araManualError.set('');
    this.araManualNotice.set('');

    const payload: CreateAraManualOfferRequest = {
      productName: this.araProductName.trim(),
      brand: this.araBrand.trim() || undefined,
      quantity: this.araQuantity && this.araQuantity > 0 ? this.araQuantity : undefined,
      unit: this.araUnit || undefined,
      price: Math.round(this.araPrice),
      listPrice: this.araListPrice && this.araListPrice > 0 ? Math.round(this.araListPrice) : undefined,
      storeName: this.araStoreName.trim() || undefined,
      notes: this.araNotes.trim() || undefined
    };

    this.http.post<AraManualOffer>('/api/v1/stores/ara/manual', payload).subscribe({
      next: created => {
        this.araManualOffers.update(list => [created, ...list.filter(o => o.offerId !== created.offerId)]);
        this.araManualNotice.set(`Oferta registrada: "${created.productName}" por $${created.price}.`);
        this.araManualBusy.set(false);
        this.araProductName = '';
        this.araBrand = '';
        this.araQuantity = null;
        this.araPrice = null;
        this.araListPrice = null;
        this.araStoreName = '';
        this.araNotes = '';
        if (this.araHealth()) {
          this.loadAraHealth();
        }
      },
      error: () => {
        this.araManualError.set('No se pudo registrar la oferta manual en Ara.');
        this.araManualBusy.set(false);
      }
    });
  }
  deleteAraManualOffer(offerId: string) {
    if (this.araManualBusy() || !offerId) return;
    this.araManualBusy.set(true);
    this.araManualError.set('');
    this.araManualNotice.set('');

    this.http.delete(`/api/v1/stores/ara/manual/${encodeURIComponent(offerId)}`).subscribe({
      next: () => {
        this.araManualOffers.update(list => list.filter(o => o.offerId !== offerId));
        this.araManualNotice.set('Oferta manual eliminada correctamente.');
        this.araManualBusy.set(false);
        if (this.araHealth()) {
          this.loadAraHealth();
        }
      },
      error: () => {
        this.araManualError.set('No se pudo eliminar la oferta manual.');
        this.araManualBusy.set(false);
      }
    });
  }
  searchProducts() {
    const query = this.searchQuery.trim();
    const requestId = ++this.searchRequest;
    this.searchError.set('');
    this.products.set(null);
    this.searchedQuery.set('');
    this.d1Offers.set(null);
    this.d1Error.set('');
    this.d1Notice.set('');
    this.exitoOffers.set(null);
    this.exitoError.set('');
    this.exitoNotice.set('');
    ++this.d1Request;
    ++this.exitoRequest;
    if (!query) {
      this.searchLoading.set(false);
      return;
    }
    this.searchLoading.set(true);
    this.loadD1Offers(query);
    this.loadExitoOffers(query);
    this.http.get<LocalProduct[]>('/api/v1/products/search', { params: { q: query } }).subscribe({
      next: products => {
        if (requestId !== this.searchRequest) return;
        this.products.set(products);
        this.searchedQuery.set(query);
        this.searchLoading.set(false);
      },
      error: () => {
        if (requestId !== this.searchRequest) return;
        this.searchError.set('No se pudo consultar el catálogo local. Comprueba la conexión e inténtalo de nuevo.');
        this.searchLoading.set(false);
      }
    });
  }
  offerFreshness(offer: D1Offer | ExitoOffer): string {
    const checked = Date.parse(offer.checkedAt);
    if (!Number.isFinite(checked)) return 'Frescura desconocida';
    const age = Date.now() - checked;
    if (age < 0) return 'Fecha futura; revisar';
    const ttl = Math.max(1, this.settings()?.priceTtlHours ?? 6) * 3_600_000;
    return age <= ttl ? 'Reciente' : age <= ttl * 4 ? 'Vencido · último dato conocido' : 'Antiguo · verificar antes de comprar';
  }
  sellerKindLabel(kind: string | number): string {
    if (kind === 1 || kind === 'RetailerDirect') return 'Éxito Directo';
    if (kind === 2 || kind === 'Marketplace') return 'Marketplace';
    if (kind === 3 || kind === 'InternalFulfillment') return 'Fulfillment interno';
    return 'Vendedor externo';
  }
  loadD1Offers(query = this.searchQuery.trim()) {
    if (!query) return;
    const requestId = ++this.d1Request;
    this.http.get<D1Offer[]>('/api/v1/providers/d1/offers', { params: { q: query } }).subscribe({
      next: offers => { if (requestId === this.d1Request && query === this.searchQuery.trim()) this.d1Offers.set(offers); },
      error: () => { if (requestId === this.d1Request && query === this.searchQuery.trim()) this.d1Error.set('No se pudieron consultar las ofertas locales de D1.'); }
    });
  }
  loadExitoOffers(query = this.searchQuery.trim()) {
    if (!query) return;
    const requestId = ++this.exitoRequest;
    this.http.get<ExitoOffer[]>('/api/v1/providers/exito/offers', { params: { q: query } }).subscribe({
      next: offers => { if (requestId === this.exitoRequest && query === this.searchQuery.trim()) this.exitoOffers.set(offers); },
      error: () => { if (requestId === this.exitoRequest && query === this.searchQuery.trim()) this.exitoError.set('No se pudieron consultar las ofertas locales de Éxito.'); }
    });
  }
  refreshD1() {
    const query = this.searchQuery.trim();
    if (!query || this.d1Busy()) return;
    this.d1Busy.set(true);
    this.d1Error.set('');
    this.d1Notice.set('');
    const requestId = ++this.d1Request;
    this.http.post<{ status: string; offersSeen: number }>('/api/v1/providers/d1/refresh', null, { params: { q: query } }).subscribe({
      next: result => {
        this.d1Busy.set(false);
        if (requestId !== this.d1Request || query !== this.searchQuery.trim()) return;
        this.d1Notice.set(`Consulta D1 finalizada. ${result.offersSeen} ofertas procesadas; los precios son online genéricos, no de góndola.`);
        this.loadD1Offers(query);
      },
      error: () => {
        this.d1Busy.set(false);
        if (requestId !== this.d1Request || query !== this.searchQuery.trim()) return;
        this.d1Error.set('D1 no respondió o está en espera. Las ofertas locales anteriores se conservaron.');
        this.loadD1Offers(query);
      }
    });
  }
  refreshExito() {
    const query = this.searchQuery.trim();
    if (!query || this.exitoBusy()) return;
    this.exitoBusy.set(true);
    this.exitoError.set('');
    this.exitoNotice.set('');
    const requestId = ++this.exitoRequest;
    this.http.post<{ status: string; offersSeen: number }>('/api/v1/providers/exito/refresh', null, { params: { q: query } }).subscribe({
      next: result => {
        this.exitoBusy.set(false);
        if (requestId !== this.exitoRequest || query !== this.searchQuery.trim()) return;
        this.exitoNotice.set(`Consulta Éxito finalizada. ${result.offersSeen} ofertas procesadas.`);
        this.loadExitoOffers(query);
      },
      error: () => {
        this.exitoBusy.set(false);
        if (requestId !== this.exitoRequest || query !== this.searchQuery.trim()) return;
        this.exitoError.set('Éxito no respondió o está en enfriamiento. Las ofertas locales anteriores se conservaron.');
        this.loadExitoOffers(query);
      }
    });
  }
  loadBaskets() { this.basketFacade.loadBaskets(); }
  addItem() { this.basketFacade.addItem(); }
  deleteItem(item: BasketItem) { this.basketFacade.deleteItem(item); }
  private updateSecret(secret: SecretEntry, changes: Partial<SecretEntry>) {
    this.secrets.update(current => current.map(entry => entry.type === secret.type ? { ...entry, ...changes } : entry));
  }
  setSecretDraft(secret: SecretEntry, value: string) { this.updateSecret(secret, { draft: value }); }
  loadSecret(secret: SecretEntry) {
    if (secret.loading || secret.busy) return;
    this.updateSecret(secret, { loading: true, error: '' });
    this.http.get<{ configured: boolean }>(`/api/v1/secrets/${secret.type}`).subscribe({
      next: status => this.updateSecret(secret, { configured: status.configured, loading: false }),
      error: () => this.updateSecret(secret, { error: 'No se pudo consultar el estado de la clave. Inténtalo de nuevo.', loading: false })
    });
  }
  saveSecret(secret: SecretEntry) {
    if (secret.loading || secret.busy || !secret.draft.trim()) return;
    this.updateSecret(secret, { busy: true, error: '', notice: '' });
    this.http.post<{ configured: boolean }>(`/api/v1/secrets/${secret.type}`, { value: secret.draft }).subscribe({
      next: () => this.updateSecret(secret, { configured: true, draft: '', busy: false, notice: 'Clave guardada en este equipo.' }),
      error: () => this.updateSecret(secret, { error: 'No se pudo guardar la clave. Comprueba el formato e inténtalo de nuevo.', busy: false })
    });
  }
  deleteSecret(secret: SecretEntry) {
    if (secret.loading || secret.busy || secret.configured !== true) return;
    this.updateSecret(secret, { busy: true, error: '', notice: '' });
    this.http.delete<void>(`/api/v1/secrets/${secret.type}`).subscribe({
      next: () => this.updateSecret(secret, { configured: false, draft: '', busy: false, notice: 'Clave eliminada de este equipo.' }),
      error: () => this.updateSecret(secret, { error: 'No se pudo eliminar la clave. Inténtalo de nuevo.', busy: false })
    });
  }
  save() {
    const value = this.settings(); if (!value) return;
    this.http.put<Settings>('/api/v1/settings', value).subscribe({ next: updated => { this.settings.set(updated); this.message.set('Ajustes guardados.'); }, error: () => this.message.set('No se guardaron los ajustes.') });
  }
  backup() {
    this.http.post<{file: string}>('/api/v1/backups', {}).subscribe({
      next: result => {
        this.message.set('Copia creada: ' + result.file);
        this.loadStorageStats();
      },
      error: () => this.message.set('No se pudo crear la copia.')
    });
  }
  loadStorageStats() {
    if (this.storageLoading()) return;
    this.storageLoading.set(true);
    this.storageError.set('');
    this.http.get<StorageStats>('/api/v1/system/storage').subscribe({
      next: stats => { this.storageStats.set(stats); this.storageLoading.set(false); },
      error: () => { this.storageError.set('No se pudo consultar el uso de almacenamiento.'); this.storageLoading.set(false); }
    });
  }
  runMaintenance() {
    if (this.maintenanceBusy()) return;
    this.maintenanceBusy.set(true);
    this.maintenanceError.set('');
    this.maintenanceReport.set(null);
    this.http.post<MaintenanceReport>('/api/v1/system/maintenance', null).subscribe({
      next: report => {
        this.maintenanceReport.set(report);
        this.maintenanceBusy.set(false);
        this.message.set('Mantenimiento completado con éxito.');
        this.loadStorageStats();
      },
      error: () => {
        this.maintenanceError.set('No se pudo completar el mantenimiento de la base de datos.');
        this.maintenanceBusy.set(false);
      }
    });
  }
  syncBasket(locationInput?: string) { this.basketFacade.syncBasket(locationInput ?? this.locationInput); }
  parseLocation() { this.locationFacade.parseLocation(); }
  geocodeAddress(address: string) { this.locationFacade.geocodeAddress(address); }
  loadProfiles() { this.locationFacade.loadProfiles(); }
  selectProfile(profileId: string | null) { this.locationFacade.selectProfile(profileId); }
  saveProfile(label?: string) { this.locationFacade.saveProfile(label); }
  saveCurrentAsProfile() { this.locationFacade.saveCurrentAsProfile(); }
  deleteProfile(id: string) { this.locationFacade.deleteProfile(id); }
  loadNearbyStores() { this.locationFacade.loadNearbyStores(); }
  seedStores() { this.locationFacade.seedStores(); }
  toggleStoreSelection(id: string) { this.locationFacade.toggleStoreSelection(id); }
  optimizeRoute() { this.locationFacade.optimizeRoute(); }
  loadGoogleUsage() { this.locationFacade.loadGoogleUsage(); }
  routeTypeBadgeClass(type: string): string { return this.locationFacade.routeTypeBadgeClass(type); }
  routeTypeLabel(type: string): string { return this.locationFacade.routeTypeLabel(type); }

  formatBytes(bytes: number | null | undefined): string {
    if (bytes === null || bytes === undefined || isNaN(bytes)) return '—';
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB';
    if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(1) + ' MB';
    return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
  }

  // Métodos de integración con OpenRouter e Inteligencia Artificial (GOAL-08)
  loadAiStatus() {
    if (this.aiLoading()) return;
    this.aiLoading.set(true);
    this.aiError.set('');
    this.http.get<AiStatusResponse>('/api/v1/ai/status').subscribe({
      next: status => {
        this.aiStatus.set(status);
        this.aiLoading.set(false);
      },
      error: () => {
        this.aiError.set('No se pudo consultar el estado del servicio de IA.');
        this.aiLoading.set(false);
      }
    });
  }

  saveAiSettings() {
    const current = this.aiStatus();
    if (!current || this.aiLoading()) return;
    this.aiLoading.set(true);
    this.aiNotice.set('');
    this.aiError.set('');

    const payload = {
      enabled: current.enabled,
      baseUrl: current.baseUrl,
      model: current.model,
      timeoutSeconds: current.timeoutSeconds,
      maxTokens: current.maxTokens,
      temperature: current.temperature,
      normalizationEnabled: current.features.normalization,
      matchingEnabled: current.features.matching,
      substituteEnabled: current.features.substitutes,
      explanationEnabled: current.features.explanation,
      monthlyBudgetUsd: current.monthlyBudgetUsd,
      warningBudgetUsd: current.warningBudgetUsd ?? null,
      hardBudgetLimit: current.hardBudgetLimit ?? false
    };

    this.http.put('/api/v1/ai/settings', payload).subscribe({
      next: () => {
        this.aiLoading.set(false);
        this.aiNotice.set('Configuración de IA actualizada.');
        this.refresh();
      },
      error: () => {
        this.aiError.set('No se pudo guardar la configuración de IA.');
        this.aiLoading.set(false);
      }
    });
  }

  testAiConnection() {
    if (this.aiConnectionTestBusy()) return;
    this.aiConnectionTestBusy.set(true);
    this.aiConnectionTest.set(null);
    this.http.post<AiConnectionTestResult>('/api/v1/ai/test-connection', {}).subscribe({
      next: res => {
        this.aiConnectionTest.set(res);
        this.aiConnectionTestBusy.set(false);
      },
      error: () => {
        this.aiConnectionTest.set({
          success: false,
          statusCode: 500,
          message: 'Error al conectar con el endpoint de prueba de IA.',
          latencyMs: 0,
          modelTested: null
        });
        this.aiConnectionTestBusy.set(false);
      }
    });
  }

  clearAiCache() {
    if (this.aiCacheClearBusy()) return;
    this.aiCacheClearBusy.set(true);
    this.http.post<{ cleared: boolean }>('/api/v1/ai/cache/clear', {}).subscribe({
      next: () => {
        this.aiCacheClearBusy.set(false);
        this.aiNotice.set('Caché local de respuestas de IA vaciada con éxito.');
        this.loadAiStatus();
      },
      error: () => {
        this.aiCacheClearBusy.set(false);
        this.aiError.set('No se pudo vaciar la caché de IA.');
      }
    });
  }

  optimizeBasket() { this.basketFacade.optimizeBasket(); }
  explainBasketOptimization() { this.basketFacade.explainBasketOptimization(); }

  adjudicateWithAi() {
    const res = this.customCompareResult();
    if (!res || this.aiAdjudicationBusy()) return;
    this.aiAdjudicationBusy.set(true);
    this.aiAdjudicationError.set('');

    const payload = {
      nameA: this.compNameA,
      brandA: res.productA.normalized.normalizedBrand,
      quantityA: res.productA.normalized.quantity?.totalAmount,
      unitA: res.productA.normalized.quantity?.originalUnit,
      nameB: this.compNameB,
      brandB: res.productB.normalized.normalizedBrand,
      quantityB: res.productB.normalized.quantity?.totalAmount,
      unitB: res.productB.normalized.quantity?.originalUnit,
      deterministicKind: String(res.match.kind),
      deterministicEvidence: res.match.evidence
    };

    this.http.post<AiMatchAdjudicationResult>('/api/v1/ai/adjudicate', payload).subscribe({
      next: result => {
        this.aiAdjudication.set(result);
        this.aiAdjudicationBusy.set(false);
      },
      error: () => {
        this.aiAdjudicationError.set('No se pudo completar el análisis con IA. Se mantiene el dictamen determinista.');
        this.aiAdjudicationBusy.set(false);
      }
    });
  }

  // GOAL-10 Historial y Observabilidad
  loadHistory() {
    this.historyLoading.set(true);
    this.historyError.set('');
    const params: Record<string, string> = {};
    if (this.historyFilterQuery.trim()) params['query'] = this.historyFilterQuery.trim();
    if (this.historyFilterRetailer.trim()) params['retailer'] = this.historyFilterRetailer.trim();
    if (this.historyFilterScope.trim()) params['scope'] = this.historyFilterScope.trim();

    this.http.get<HistoryResponse>('/api/v1/history', { params }).subscribe({
      next: data => {
        this.historyData.set(data);
        this.historyLoading.set(false);
      },
      error: () => {
        this.historyError.set('No se pudo cargar el historial de precios.');
        this.historyLoading.set(false);
      }
    });
  }

  exportPricesCsv() {
    let url = '/api/v1/export/prices.csv';
    const params: string[] = [];
    if (this.historyFilterRetailer.trim()) params.push('retailer=' + encodeURIComponent(this.historyFilterRetailer.trim()));
    if (this.historyFilterScope.trim()) params.push('scope=' + encodeURIComponent(this.historyFilterScope.trim()));
    if (params.length > 0) url += '?' + params.join('&');
    if (typeof window !== 'undefined' && window.open) {
      window.open(url, '_blank');
    }
  }

  loadBackups() {
    this.backupsLoading.set(true);
    this.backupsError.set('');
    this.http.get<BackupFileDto[]>('/api/v1/backups').subscribe({
      next: list => {
        this.backupsList.set(list);
        this.backupsLoading.set(false);
      },
      error: () => {
        this.backupsError.set('No se pudieron consultar las copias de seguridad.');
        this.backupsLoading.set(false);
      }
    });
  }

  loadSystemStatus() {
    this.systemStatusLoading.set(true);
    this.systemStatusError.set('');
    this.http.get<SystemStatusDto>('/api/v1/system/status').subscribe({
      next: status => {
        this.systemStatus.set(status);
        this.systemStatusLoading.set(false);
      },
      error: () => {
        this.systemStatusError.set('No se pudo consultar el estado de observabilidad del sistema.');
        this.systemStatusLoading.set(false);
      }
    });
  }
}

