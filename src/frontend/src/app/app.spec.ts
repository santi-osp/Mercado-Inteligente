import { APP_VERSION } from './version';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { App } from './app';
import { provideRouter, Router } from '@angular/router';
import { By } from '@angular/platform-browser';
import { NgModel } from '@angular/forms';
import { routes } from './app.routes';

describe('PWA shell and local settings', () => {
  const settings = { maxStores: 2, sizeTolerancePercent: 15, allowSubstitutes: true, maxOverbuyPercent: 20, aiEnabled: false, includeMarketplace: false };
  const health = { database: 'ok', products: 0, ai: 'NOT_CONFIGURED', google: 'NOT_CONFIGURED', mode: 'LOCAL' };
  const mockStorageStats = {
    databaseBytes: 1048576,
    sidecarsBytes: 32768,
    rawBytes: 2097152,
    logsBytes: 65536,
    backupsBytes: 4194304,
    externalProductsCount: 150,
    externalSkusCount: 180,
    offersCount: 220,
    priceObservationsCount: 500
  };
  let fixture: ReturnType<typeof TestBed.createComponent<App>>;
  let http: HttpTestingController;
  let router: Router;
  async function settle() {
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise(r => setTimeout(r, 0));
    fixture.detectChanges();
  }

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ imports: [App], providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter(routes)] });
    fixture = TestBed.createComponent(App);
    http = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
  });
  afterEach(() => {
    http.match('/api/v1/ai/status').forEach(request => request.flush({
      isConfigured: false, enabled: false, model: '', baseUrl: '', timeoutSeconds: 20,
      maxTokens: 1024, temperature: 0, features: {
        normalization: false, matching: false, substitutes: false, explanation: false
      }, monthlyBudgetUsd: null, usage: {
        totalCalls: 0, promptTokens: 0, completionTokens: 0, estimatedCostUsd: 0,
        estimatedCostCop: 0, cacheHits: 0, errorsCount: 0, recentDailyUsage: []
      }
    }));
    http.verify();
  TestBed.resetTestingModule();
});

  async function load() {
    http.expectOne('/api/v1/system/health').flush(health);
    http.expectOne('/api/v1/settings').flush({ ...settings });
    await router.navigateByUrl('/');
    await settle();
  }

  it('renders local status, accessible navigation and honest empty home', async () => {
    await load();
    const text = fixture.nativeElement.textContent;
    expect(fixture.nativeElement.querySelector('nav[aria-label="Navegación principal"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('nav button[aria-current="page"]')?.textContent).toContain('Inicio');
    expect(text).toContain('Sistema local activo');
    expect(text).toContain('Todo empieza con una lista');
    expect(text).toContain('MÁX. TIENDAS');
    expect(text).toContain('2');
  });

  it('navigates to settings, displays defaults and saves changed comparison preferences', async () => {
    await load();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('nav button')) as HTMLButtonElement[];
    buttons.find(button => button.textContent?.includes('Ajustes'))!.click();
    http.expectOne('/api/v1/system/storage').flush(mockStorageStats);
    http.expectOne('/api/v1/secrets/google').flush({ configured: false });
    http.expectOne('/api/v1/secrets/openrouter').flush({ configured: false });
    await settle();
    const inputs = fixture.nativeElement.querySelectorAll('.settings-grid input') as NodeListOf<HTMLInputElement>;
    expect(inputs[0].value).toBe('2');
    expect(inputs[1].value).toBe('15');
    expect(inputs[2].value).toBe('20');
    expect(inputs[3].checked).toBe(true);
    expect(inputs[4].checked).toBe(false);
    expect(inputs[5].disabled).toBe(true);
    inputs[0].value = '3'; inputs[0].dispatchEvent(new Event('input'));
    inputs[3].click();
    inputs[4].click();
    await settle();
    (fixture.nativeElement.querySelector('.settings-grid .primary') as HTMLButtonElement).click();
    const request = http.expectOne('/api/v1/settings');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ ...settings, maxStores: 3, allowSubstitutes: false, includeMarketplace: true });
    request.flush({ ...settings, maxStores: 3, allowSubstitutes: false, includeMarketplace: true });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Ajustes guardados.');
  });

  async function openSecrets() {
    await load();
    fixture.componentInstance.navigate('Ajustes');
    http.expectOne('/api/v1/system/storage').flush(mockStorageStats);
    await settle();
  }

  it('keeps configuration status distinct from operational health and never reveals a saved key', async () => {
    await openSecrets();
    const google = http.expectOne('/api/v1/secrets/google');
    const router = http.expectOne('/api/v1/secrets/openrouter');
    expect(google.request.method).toBe('GET');
    expect(router.request.method).toBe('GET');
    google.flush({ configured: true });
    router.flush({ configured: false });
    await settle();
    const rows = fixture.nativeElement.querySelectorAll('.secret-row') as NodeListOf<HTMLElement>;
    expect(rows[0].textContent).toContain('Configurada');
    expect(rows[1].textContent).toContain('No configurada');
    expect(fixture.nativeElement.textContent).toContain('Disponibilidad operativa de Google: NOT_CONFIGURED');
    expect(fixture.nativeElement.textContent).toContain('Disponibilidad operativa de OpenRouter: NOT_CONFIGURED');
    expect((fixture.nativeElement.querySelector('.settings-grid input[type="checkbox"]:disabled') as HTMLInputElement).disabled).toBe(true);
    expect((rows[0].querySelector('input') as HTMLInputElement).value).toBe('');
  });

  it('saves each provider key only to its secret endpoint and clears the input after success', async () => {
    await openSecrets();
    http.expectOne('/api/v1/secrets/google').flush({ configured: false });
    http.expectOne('/api/v1/secrets/openrouter').flush({ configured: false });
    await settle();
    const key = 'sk-or-v1-private-value';
    const input = fixture.nativeElement.querySelector('#secret-openrouter') as HTMLInputElement;
    input.value = key; input.dispatchEvent(new Event('input'));
    await settle();
    expect(fixture.componentInstance.secrets()[1].draft).toBe(key);
    (input.closest('form')!.querySelector('button.primary') as HTMLButtonElement).click();
    await settle();
    const request = http.expectOne('/api/v1/secrets/openrouter');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ value: key });
    expect(http.match('/api/v1/settings').length).toBe(0);
    request.flush({ configured: true });
    await settle();
    expect(input.value).toBe('');
    expect(fixture.nativeElement.textContent).not.toContain(key);
    expect(input.closest('.secret-row')!.textContent).toContain('Configurada');
  });

  it('deletes only a configured key and updates status after success', async () => {
    await openSecrets();
    http.expectOne('/api/v1/secrets/google').flush({ configured: true });
    http.expectOne('/api/v1/secrets/openrouter').flush({ configured: false });
    await settle();
    const rows = fixture.nativeElement.querySelectorAll('.secret-row') as NodeListOf<HTMLElement>;
    expect((rows[1].querySelector('button.secondary') as HTMLButtonElement).disabled).toBe(true);
    (rows[0].querySelector('button.secondary') as HTMLButtonElement).click();
    const request = http.expectOne('/api/v1/secrets/google');
    expect(request.request.method).toBe('DELETE');
    expect(rows[0].textContent).toContain('Configurada');
    request.flush(null);
    await settle();
    expect(rows[0].textContent).toContain('No configurada');
    expect((rows[0].querySelector('button.secondary') as HTMLButtonElement).disabled).toBe(true);
  });

  it('preserves known status on loading, save and delete failures without echoing key or server error', async () => {
    await openSecrets();
    const google = fixture.componentInstance.secrets()[0];
    http.expectOne('/api/v1/secrets/google').flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne('/api/v1/secrets/openrouter').flush({ configured: true });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Estado desconocido');
    expect(fixture.nativeElement.textContent).toContain('No se pudo consultar el estado');
    fixture.componentInstance.setSecretDraft(google, 'private-google-key');
    fixture.componentInstance.saveSecret(fixture.componentInstance.secrets()[0]);
    http.expectOne('/api/v1/secrets/google').flush({ message: 'private-google-key' }, { status: 400, statusText: 'Bad Request' });
    await settle();
    expect(fixture.componentInstance.secrets()[0].draft).toBe('private-google-key');
    expect(google.configured).toBeNull();
    expect(fixture.nativeElement.textContent).not.toContain('private-google-key');
    const router = fixture.componentInstance.secrets()[1];
    fixture.componentInstance.deleteSecret(router);
    http.expectOne('/api/v1/secrets/openrouter').flush('private-google-key', { status: 500, statusText: 'Error' });
    await settle();
    expect(router.configured).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('No se pudo eliminar la clave');
    expect(fixture.nativeElement.textContent).not.toContain('private-google-key');
  });

  it('reports connection failure without inventing system status or settings', async () => {
    http.expectOne('/api/v1/system/health').flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne('/api/v1/settings').flush('offline', { status: 503, statusText: 'Unavailable' });
    fixture.componentInstance.navigate('Ajustes');
    http.expectOne('/api/v1/system/storage').flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne('/api/v1/secrets/google').flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne('/api/v1/secrets/openrouter').flush('offline', { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Sin conexión');
    expect(fixture.nativeElement.textContent).toContain('No se pudieron cargar los ajustes');
    expect(fixture.nativeElement.querySelector('.settings-grid')).toBeNull();
    expect(fixture.nativeElement.querySelector('[role="alert"] button')?.textContent).toContain('Reintentar conexión');
  });

  it('clears stale health after disconnect and offers a retry without deleting local lists', async () => {
    await load();
    fixture.componentInstance.baskets.set([{ id: 'saved', name: 'Mi mercado', createdAt: '', items: [] }]);
    fixture.componentInstance.refresh();
    http.expectOne('/api/v1/system/health').flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne('/api/v1/settings').flush('offline', { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Sin conexión');
    expect(fixture.componentInstance.health()).toBeNull();
    expect(fixture.componentInstance.baskets().length).toBe(1);
    (fixture.nativeElement.querySelector('[role="alert"] button') as HTMLButtonElement).click();
    http.expectOne('/api/v1/system/health').flush(health);
    http.expectOne('/api/v1/settings').flush(settings);
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Sistema local activo');
    expect(fixture.nativeElement.querySelector('[role="alert"]')).toBeNull();
  });

  const basket = { id: 'basket-1', name: 'Mi mercado', createdAt: '2026-10-07T00:00:00Z', items: [] as Array<{ id: string; basketId: string; query: string; quantity: number; productVariantId: null; notes: string | null }> };
  const item = { id: 'item-1', basketId: basket.id, query: 'Arroz', quantity: 2, productVariantId: null, notes: 'Integral' };
  async function openMarket(baskets = [basket]) {
    await load();
    fixture.componentInstance.navigate('Mi mercado');
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Cargando listas');
    const request = http.expectOne('/api/v1/baskets');
    expect(request.request.method).toBe('GET');
    request.flush(baskets);
    await settle();
  }

  it('lists baskets, selects a basket and does not suggest a comparison', async () => {
    await openMarket([basket, { ...basket, id: 'basket-2', items: [item] }]);
    const select = fixture.nativeElement.querySelector('#basket-select') as HTMLSelectElement;
    expect(select.options.length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('no muestra precios ni compara tiendas');
    select.value = 'basket-2'; select.dispatchEvent(new Event('change'));
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Arroz');
  });

  it('creates a basket and only selects it after the server responds', async () => {
    await openMarket([]);
    expect(fixture.nativeElement.textContent).toContain('Aún no tienes listas');
    (fixture.nativeElement.querySelector('.market-actions button') as HTMLButtonElement).click();
    const request = http.expectOne('/api/v1/baskets');
    expect(request.request.method).toBe('POST');
    expect(fixture.componentInstance.selectedBasketId()).toBeNull();
    await settle();
    expect((fixture.nativeElement.querySelector('.market-actions button') as HTMLButtonElement).disabled).toBe(true);
    request.flush(basket);
    await settle();
    expect(fixture.componentInstance.selectedBasketId()).toBe(basket.id);
    expect(fixture.nativeElement.textContent).toContain('Esta lista está vacía');
  });

  it('reports basket creation failure without fabricating a list', async () => {
    await openMarket([]);
    (fixture.nativeElement.querySelector('.market-actions button') as HTMLButtonElement).click();
    http.expectOne('/api/v1/baskets').flush('error', { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(fixture.componentInstance.baskets()).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('No se pudo crear la lista');
    expect((fixture.nativeElement.querySelector('.market-actions button') as HTMLButtonElement).disabled).toBe(false);
  });

  it('validates item fields, posts a trimmed item and deletes only after success', async () => {
    await openMarket();
    const app = fixture.componentInstance;
    app.query = '   '; app.quantity = 0;
    app.addItem(); await settle();
    expect(fixture.nativeElement.querySelector('[role="alert"]').textContent).toContain('cantidad positiva');
    app.query = ' Arroz '; app.quantity = 2; app.notes = 'Integral';
    app.addItem();
    const post = http.expectOne('/api/v1/baskets/basket-1/items');
    expect(post.request.method).toBe('POST');
    expect(post.request.body).toEqual({ query: 'Arroz', quantity: 2, productVariantId: null, notes: 'Integral' });
    expect(app.selectedBasket?.items.length).toBe(0);
    post.flush(item);
    await settle();
    expect(app.selectedBasket?.items.length).toBe(1);
    expect(app.query).toBe('');
    (fixture.nativeElement.querySelector('.item-list button') as HTMLButtonElement).click();
    const deletion = http.expectOne('/api/v1/baskets/basket-1/items/item-1');
    expect(deletion.request.method).toBe('DELETE');
    expect(app.selectedBasket?.items.length).toBe(1);
    deletion.flush(null);
    await settle();
    expect(app.selectedBasket?.items.length).toBe(0);
  });

  it('preserves existing data and draft when requests fail, with retry for loading', async () => {
    await openMarket([{ ...basket, items: [item] }]);
    const app = fixture.componentInstance;
    app.query = 'Leche'; app.quantity = 1;
    app.addItem();
    http.expectOne('/api/v1/baskets/basket-1/items').flush('error', { status: 500, statusText: 'Error' });
    await settle();
    expect(app.query).toBe('Leche');
    expect(app.selectedBasket?.items).toEqual([item]);
    expect(fixture.nativeElement.textContent).toContain('No se pudo agregar');
    app.deleteItem(item);
    http.expectOne('/api/v1/baskets/basket-1/items/item-1').flush('error', { status: 500, statusText: 'Error' });
    expect(app.selectedBasket?.items).toEqual([item]);
    app.loadBaskets();
    http.expectOne('/api/v1/baskets').flush('error', { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(app.selectedBasket?.items).toEqual([item]);
    expect(fixture.nativeElement.textContent).toContain('No se pudieron cargar las listas');
    (fixture.nativeElement.querySelector('.market-error button') as HTMLButtonElement).click();
    http.expectOne('/api/v1/baskets').flush([{ ...basket, items: [item] }]);
    await settle();
    expect(app.marketError()).toBe('');
  });

  it('creates a backup from settings and reports the returned path', async () => {
    await load();
    fixture.componentInstance.navigate('Ajustes');
    http.expectOne('/api/v1/system/storage').flush(mockStorageStats);
    http.expectOne('/api/v1/secrets/google').flush({ configured: false });
    http.expectOne('/api/v1/secrets/openrouter').flush({ configured: false });
    await settle();
    const button = Array.from(fixture.nativeElement.querySelectorAll('.settings-grid button') as NodeListOf<HTMLButtonElement>)
      .find(item => item.textContent?.includes('Crear copia'))!;
    button.click();
    const request = http.expectOne('/api/v1/backups');
    expect(request.request.method).toBe('POST');
    request.flush({ file: 'backup.db' });
    http.expectOne('/api/v1/system/storage').flush(mockStorageStats);
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Copia creada: backup.db');
  });
  it('renders only statuses supplied by the providers API, and retries after failure', async () => {
    await load();
    fixture.componentInstance.navigate('Fuentes');
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Cargando estados de fuentes');
    http.expectOne('/api/v1/providers/d1/health').flush({ status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers/exito/health').flush({ status: 'PARTIAL_FASTSTORE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers').flush('offline', { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('No se pudieron cargar los estados');
    expect(fixture.nativeElement.querySelectorAll('.source-list li').length).toBe(0);
    (fixture.nativeElement.querySelector('.market-error button') as HTMLButtonElement).click();
    http.expectOne('/api/v1/providers/d1/health').flush({ status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH', lastRun: {
      state: 2, finishedAt: '2026-10-07T12:00:00Z', errorCode: 'RATE_LIMITED', itemsReceived: 20,
      offersProcessed: 18, offersChanged: 3, productsCreated: 8, productsUpdated: 0,
      skusCreated: 11, skusUpdated: 0, rawNew: 1, rawDeduplicated: 0
    } });
    http.expectOne('/api/v1/providers/exito/health').flush({ status: 'PARTIAL_FASTSTORE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers').flush([
      { code: 'D1', status: 'NOT_IMPLEMENTED' },
      { code: 'ARA', status: 'PARTIAL_NOT_IMPLEMENTED' }
    ]);
    await settle();
    expect(fixture.nativeElement.querySelectorAll('.source-list li').length).toBe(2);
    expect(fixture.nativeElement.textContent).toContain('PARTIAL_NOT_IMPLEMENTED');
    expect(fixture.nativeElement.textContent).not.toContain('EXITO');
    expect(fixture.nativeElement.textContent).toContain('Fallida');
    expect(fixture.nativeElement.textContent).toContain('RATE_LIMITED');
    expect(fixture.nativeElement.textContent).toContain('RAW nuevos/deduplicados: 1/0');
    expect(fixture.nativeElement.textContent).toContain('no una comprobación de Internet');
  });

  it('keeps the last known D1 run visible when a later status request fails', async () => {
    await load();
    const app = fixture.componentInstance;
    app.navigate('Fuentes');
    http.expectOne('/api/v1/providers/d1/health').flush({ status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH', lastRun: {
      state: 2, finishedAt: '2026-10-07T12:00:00Z', errorCode: 'RATE_LIMITED', itemsReceived: 20,
      offersProcessed: 18, offersChanged: 3, productsCreated: 8, productsUpdated: 0,
      skusCreated: 11, skusUpdated: 0, rawNew: 1, rawDeduplicated: 0
    } });
    http.expectOne('/api/v1/providers/exito/health').flush({ status: 'PARTIAL_FASTSTORE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers').flush([{ code: 'D1', status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH' }]);
    await settle();
    app.loadProviders();
    http.expectOne('/api/v1/providers/d1/health').flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne('/api/v1/providers/exito/health').flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne('/api/v1/providers').flush('offline', { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('RATE_LIMITED');
    expect(fixture.nativeElement.textContent).toContain('puede estar desactualizado');
  });

  it('runs a bounded D1 diagnostic only after explicit action and clears stale evidence on failure', async () => {
    await load();
    fixture.componentInstance.navigate('Fuentes');
    http.expectOne('/api/v1/providers/d1/health').flush({ status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers/exito/health').flush({ status: 'PARTIAL_FASTSTORE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers').flush([{ code: 'D1', status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH' }]);
    expect(http.match('/api/v1/providers/d1/probe').length).toBe(0);
    await settle();
    const button = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(element => element.textContent?.includes('Comprobar D1 ahora'))!;
    button.click();
    await settle();
    expect(button.disabled).toBe(true);
    const request = http.expectOne('/api/v1/providers/d1/probe');
    expect(request.request.method).toBe('POST');
    request.flush({ checkedAt: '2026-10-07T12:00:00Z', search: 'VALID', genericOnlinePrice: 'VALID_GENERIC_ONLINE' });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Precio online genérico: VALID_GENERIC_ONLINE');
    button.click();
    http.expectOne('/api/v1/providers/d1/probe').flush('offline', { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('No se pudo completar el diagnóstico puntual');
    expect(fixture.nativeElement.textContent).not.toContain('Precio online genérico: VALID_GENERIC_ONLINE');
    expect(fixture.nativeElement.textContent).toContain('Sin sincronizaciones D1 registradas');
  });

  it('searches the local catalog with a trimmed query and renders only API fields', async () => {
    await load();
    fixture.componentInstance.navigate('Buscar');
    await settle();
    expect(http.match('/api/v1/products/search').length).toBe(0);
    const input = fixture.nativeElement.querySelector('#product-query') as HTMLInputElement;
    input.value = ' arroz '; input.dispatchEvent(new Event('input'));
    await settle();
    fixture.componentInstance.searchQuery = ' arroz ';
    (fixture.nativeElement.querySelector('.search-form') as HTMLFormElement).dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
    await settle();
    const catalogReqs = http.match('/api/v1/search/products');
    if (catalogReqs.length > 0) {
      catalogReqs[0].flush({ query: 'arroz', totalCount: 0, page: 1, pageSize: 24, items: [], facets: { retailers: [], brands: [], categories: [] } });
    }
    const request = http.expectOne(req => req.url === '/api/v1/products/search');
    const d1 = http.expectOne(req => req.url === '/api/v1/providers/d1/offers');
    const exito = http.expectOne(req => req.url === '/api/v1/providers/exito/offers');
    expect(d1.request.params.get('q')).toBe('arroz');
    expect(exito.request.params.get('q')).toBe('arroz');
    d1.flush([]);
    exito.flush([]);
    expect(request.request.params.get('q')).toBe('arroz');
    expect(fixture.nativeElement.textContent).toContain('Buscando en el catálogo local');
    request.flush([{ id: 'product-1', retailerId: 'retailer-1', externalId: 'sku-1', name: 'Arroz blanco', brand: 'Marca', updatedAt: '2026-10-07T00:00:00Z' }]);
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Arroz blanco');
    expect(fixture.nativeElement.textContent).toContain('sku-1');
    expect(fixture.nativeElement.textContent).not.toContain('Precio:');
  });

  it('keeps a saved D1 online offer visible after failed manual refresh', async () => {
    await load();
    fixture.componentInstance.navigate('Buscar');
    const app = fixture.componentInstance;
    app.searchQuery = 'arroz';
    app.loadD1Offers();
    http.expectOne(req => req.url === '/api/v1/providers/d1/offers').flush([{
      id: 'p1', productName: 'Arroz', skuName: '500 G', ean: null, price: 1990,
      scope: 1, availability: 0, context: 'online-generic', checkedAt: '2026-10-07T12:00:00Z',
      sellerName: 'D1', sellerKind: 1
    }]);
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Online genérico');
    expect(fixture.nativeElement.textContent).toContain('Desconocida');
    app.refreshD1();
    const refresh = http.expectOne(req => req.url === '/api/v1/providers/d1/refresh');
    expect(refresh.request.method).toBe('POST');
    refresh.flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne(req => req.url === '/api/v1/providers/d1/offers').flush(app.d1Offers());
    await settle();
    expect(fixture.nativeElement.textContent).toContain('1990 COP');
    expect(fixture.nativeElement.textContent).toContain('se conservaron');
  });

  it('shows list price only when above current price and does not infer a promotion', async () => {
    await load();
    fixture.componentInstance.navigate('Buscar');
    const app = fixture.componentInstance;
    app.searchQuery = 'arroz';
    app.loadD1Offers();
    http.expectOne(req => req.url === '/api/v1/providers/d1/offers').flush([{
      id: 'saved', productName: 'Arroz', skuName: '500 G', ean: null,
      price: 1990, listPrice: 2200, scope: 1, availability: 0, context: 'online-generic',
      checkedAt: new Date().toISOString(), sellerName: 'D1', sellerKind: 1
    }]);
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Precio de lista online: 2200 COP');
    expect(fixture.nativeElement.textContent).not.toContain('Promoción');
  });

  it('shows saved D1 offers from search without making a provider refresh request', async () => {
    await load();
    fixture.componentInstance.navigate('Buscar');
    const app = fixture.componentInstance;
    app.searchQuery = 'arroz';
    app.searchProducts();
    http.expectOne(req => req.url === '/api/v1/products/search').flush([]);
    http.expectOne(req => req.url === '/api/v1/providers/exito/offers').flush([]);
    http.expectOne(req => req.url === '/api/v1/providers/d1/offers').flush([{
      id: 'saved', productName: 'Arroz', skuName: '500 G', ean: null, price: 1990,
      scope: 1, availability: 0, context: 'online-generic', checkedAt: new Date().toISOString(),
      sellerName: 'D1', sellerKind: 1
    }]);
    await settle();
    expect(fixture.nativeElement.textContent).toContain('1990 COP');
    expect(http.match(req => req.url === '/api/v1/providers/d1/refresh')).toHaveLength(0);
    expect(http.match(req => req.url === '/api/v1/providers/exito/refresh')).toHaveLength(0);
  });

  it('ignores a delayed D1 response after the search query changes', async () => {
    await load();
    fixture.componentInstance.navigate('Buscar');
    const app = fixture.componentInstance;
    app.searchQuery = 'arroz';
    app.loadD1Offers();
    const old = http.expectOne(req => req.url === '/api/v1/providers/d1/offers');
    app.searchQuery = 'leche';
    app.searchProducts();
    http.expectOne(req => req.url === '/api/v1/products/search').flush([]);
    const automatic = http.expectOne(req => req.url === '/api/v1/providers/d1/offers');
    http.expectOne(req => req.url === '/api/v1/providers/exito/offers').flush([]);
    app.loadD1Offers();
    automatic.flush([]);
    const current = http.expectOne(req => req.url === '/api/v1/providers/d1/offers');
    old.flush([{ id: 'old', productName: 'Arroz' }]);
    expect(app.d1Offers()).toBeNull();
    current.flush([]);
    expect(app.d1Offers()).toEqual([]);
  });

  it('labels D1 prices by TTL without treating unknown stock as available', async () => {
    await load();
    fixture.componentInstance.navigate('Buscar');
    const app = fixture.componentInstance;
    app.searchQuery = 'arroz';
    app.loadD1Offers();
    const now = Date.now();
    const offer = (checkedAt: string) => ({
      id: checkedAt, productName: 'Arroz', skuName: '500 G', ean: null, price: 1990,
      scope: 1, availability: 0, context: 'online-generic', checkedAt,
      sellerName: 'D1', sellerKind: 1
    });
    http.expectOne(req => req.url === '/api/v1/providers/d1/offers').flush([
      offer(new Date(now - 60_000).toISOString()),
      offer(new Date(now - 7 * 3_600_000).toISOString()),
      offer(new Date(now - 26 * 3_600_000).toISOString())
    ]);
    await settle();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Reciente');
    expect(text).toContain('Vencido · último dato conocido');
    expect(text).toContain('Antiguo · verificar antes de comprar');
    expect(text).toContain('Desconocida');
    expect(text).not.toContain('Precio físico');
  });

  it('handles empty, failed and stale local searches without showing old results', async () => {
    await load();
    fixture.componentInstance.navigate('Buscar');
    const app = fixture.componentInstance;
    app.searchQuery = 'leche'; app.searchProducts();
    const first = http.expectOne(req => req.url === '/api/v1/products/search');
    app.searchQuery = 'arroz'; app.searchProducts();
    const second = http.expectOne(req => req.url === '/api/v1/products/search');
    http.match(req => req.url === '/api/v1/providers/d1/offers').forEach(req => req.flush([]));
    http.match(req => req.url === '/api/v1/providers/exito/offers').forEach(req => req.flush([]));
    first.flush([{ id: 'old', retailerId: 'r', externalId: 'old', name: 'Leche', brand: null, updatedAt: '' }]);
    expect(app.products()).toBeNull();
    second.flush([]);
    await settle();
    expect(fixture.nativeElement.textContent).toContain('No se encontraron productos locales para «arroz»');
    app.searchProducts();
    http.expectOne(req => req.url === '/api/v1/providers/d1/offers').flush([]);
    http.expectOne(req => req.url === '/api/v1/providers/exito/offers').flush([]);
    http.expectOne(req => req.url === '/api/v1/products/search').flush('error', { status: 500, statusText: 'Error' });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('No se pudo consultar el catálogo local');
    expect(fixture.nativeElement.textContent).not.toContain('No se encontraron productos locales');
    app.searchQuery = ' '; app.searchProducts();
    await settle();
    expect(http.match('/api/v1/products/search').length).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('Escribe un término');
  });

  it('renders Éxito sync health and preserves last known run on failure', async () => {
    await load();
    const app = fixture.componentInstance;
    app.navigate('Fuentes');
    http.expectOne('/api/v1/providers/d1/health').flush({ status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers/exito/health').flush({ status: 'PARTIAL_FASTSTORE_MANUAL_REFRESH', lastRun: {
      state: 1, finishedAt: '2026-10-07T14:30:00Z', errorCode: null, itemsReceived: 35,
      offersProcessed: 30, offersChanged: 5, productsCreated: 12, productsUpdated: 3,
      skusCreated: 14, skusUpdated: 2, rawNew: 4, rawDeduplicated: 1
    } });
    http.expectOne('/api/v1/providers').flush([{ code: 'EXITO', status: 'PARTIAL_FASTSTORE_MANUAL_REFRESH' }]);
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Completada');
    expect(fixture.nativeElement.textContent).toContain('Filas: 35');
    expect(fixture.nativeElement.textContent).toContain('Ofertas procesadas: 30');
    expect(fixture.nativeElement.textContent).toContain('RAW nuevos/deduplicados: 4/1');

    app.loadProviders();
    http.expectOne('/api/v1/providers/d1/health').flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne('/api/v1/providers/exito/health').flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne('/api/v1/providers').flush('offline', { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('No se pudo consultar el historial local de Éxito.');
    expect(fixture.nativeElement.textContent).toContain('Filas: 35');
  });

  it('runs Éxito generic and regional diagnostic probes boundedly', async () => {
    await load();
    fixture.componentInstance.navigate('Fuentes');
    http.expectOne('/api/v1/providers/d1/health').flush({ status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers/exito/health').flush({ status: 'PARTIAL_FASTSTORE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers').flush([{ code: 'EXITO', status: 'PARTIAL_FASTSTORE_MANUAL_REFRESH' }]);
    await settle();

    const genericBtn = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(element => element.textContent?.includes('Comprobar Éxito (genérico)'))!;
    const regionalBtn = Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>)
      .find(element => element.textContent?.includes('Comprobar Éxito regional'))!;

    genericBtn.click();
    await settle();
    expect(genericBtn.disabled).toBe(true);
    const reqGeneric = http.expectOne('/api/v1/providers/exito/probe');
    expect(reqGeneric.request.method).toBe('POST');
    reqGeneric.flush({ checkedAt: '2026-10-07T12:00:00Z', search: 'VALID', genericOnlinePrice: 'VALID_GENERIC_ONLINE' });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Genérico · Comprobado: 2026-10-07T12:00:00Z');
    expect(fixture.nativeElement.textContent).toContain('Precio online genérico: VALID_GENERIC_ONLINE');

    regionalBtn.click();
    await settle();
    expect(regionalBtn.disabled).toBe(true);
    const reqRegional = http.expectOne('/api/v1/providers/exito/probe/regional');
    expect(reqRegional.request.method).toBe('POST');
    reqRegional.flush({ checkedAt: '2026-10-07T12:05:00Z', regionContext: 'fulfillment:medellin', regionalCatalog: 'VALID_REGIONAL_CATALOG', regionalOnlinePrice: 'VALID_REGIONAL_ONLINE', errorCode: null });
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Regional · Comprobado: 2026-10-07T12:05:00Z');
    expect(fixture.nativeElement.textContent).toContain('Región: fulfillment:medellin');
    expect(fixture.nativeElement.textContent).toContain('VALID_REGIONAL_ONLINE');
  });

  it('renders Éxito offers distinguishing direct, marketplace and internal fulfillment', async () => {
    await load();
    fixture.componentInstance.navigate('Buscar');
    const app = fixture.componentInstance;
    app.searchQuery = 'café';
    app.loadExitoOffers();
    http.expectOne(req => req.url === '/api/v1/providers/exito/offers').flush([
      {
        id: 'o1', productName: 'Café Directo', skuName: '500g', ean: '7701234567890',
        price: 18500, listPrice: 20000, scope: 1, availability: 1, context: 'online-generic',
        checkedAt: new Date().toISOString(), sellerName: 'Éxito', sellerKind: 1
      },
      {
        id: 'o2', productName: 'Café Marketplace', skuName: '500g', ean: null,
        price: 17900, listPrice: null, scope: 1, availability: 1, context: 'online-generic',
        checkedAt: new Date().toISOString(), sellerName: 'Vendedor Aliado', sellerKind: 2
      },
      {
        id: 'o3', productName: 'Café Regional', skuName: '500g', ean: null,
        price: 19200, listPrice: null, scope: 2, availability: 2, context: 'fulfillment:reg1',
        checkedAt: new Date().toISOString(), sellerName: 'exitocol037', sellerKind: 3
      }
    ]);
    await settle();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Café Directo · 500g');
    expect(text).toContain('Éxito Directo');
    expect(text).toContain('Precio de lista online: 20000 COP');
    expect(text).toContain('Marketplace');
    expect(text).toContain('Fulfillment interno');
    expect(text).toContain('Regional (Medellín)');
    expect(text).toContain('No disponible');
  });

  it('keeps saved Éxito online offer visible after failed manual refresh', async () => {
    await load();
    fixture.componentInstance.navigate('Buscar');
    const app = fixture.componentInstance;
    app.searchQuery = 'café';
    app.loadExitoOffers();
    http.expectOne(req => req.url === '/api/v1/providers/exito/offers').flush([{
      id: 'ex1', productName: 'Café Sello Rojo', skuName: '500g', ean: null, price: 15400,
      scope: 1, availability: 1, context: 'online-generic', checkedAt: '2026-10-07T12:00:00Z',
      sellerName: 'Éxito', sellerKind: 1
    }]);
    await settle();
    expect(fixture.nativeElement.textContent).toContain('15400 COP');

    app.refreshExito();
    const refresh = http.expectOne(req => req.url === '/api/v1/providers/exito/refresh');
    expect(refresh.request.method).toBe('POST');
    refresh.flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne(req => req.url === '/api/v1/providers/exito/offers').flush(app.exitoOffers());
    await settle();
    expect(fixture.nativeElement.textContent).toContain('15400 COP');
    expect(fixture.nativeElement.textContent).toContain('Éxito no respondió o está en enfriamiento. Las ofertas locales anteriores se conservaron.');
  });

  it('loads and renders storage telemetry in settings', async () => {
    await load();
    fixture.componentInstance.navigate('Ajustes');
    http.expectOne('/api/v1/system/storage').flush(mockStorageStats);
    http.expectOne('/api/v1/secrets/google').flush({ configured: false });
    http.expectOne('/api/v1/secrets/openrouter').flush({ configured: false });
    await settle();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Almacenamiento local');
    expect(text).toContain('Base SQLite');
    expect(text).toContain('1.0 MB'); // 1048576 bytes
    expect(text).toContain('Evidencia RAW');
    expect(text).toContain('2.0 MB'); // 2097152 bytes
    expect(text).toContain('Registros');
    expect(text).toContain('64.0 KB'); // 65536 bytes
    expect(text).toContain('Copias');
    expect(text).toContain('4.0 MB'); // 4194304 bytes
    expect(text).toContain('150'); // products count
    expect(text).toContain('220'); // offers count
  });

  it('triggers manual database maintenance and displays optimization and pruning report', async () => {
    await load();
    fixture.componentInstance.navigate('Ajustes');
    http.expectOne('/api/v1/system/storage').flush(mockStorageStats);
    http.expectOne('/api/v1/secrets/google').flush({ configured: false });
    http.expectOne('/api/v1/secrets/openrouter').flush({ configured: false });
    await settle();

    const maintBtn = Array.from(fixture.nativeElement.querySelectorAll('.settings-grid button') as NodeListOf<HTMLButtonElement>)
      .find(btn => btn.textContent?.includes('Ejecutar mantenimiento'))!;
    expect(maintBtn).toBeTruthy();
    maintBtn.click();
    await settle();
    expect(maintBtn.disabled).toBe(true);

    const maintReq = http.expectOne('/api/v1/system/maintenance');
    expect(maintReq.request.method).toBe('POST');
    maintReq.flush({
      databaseOptimized: true,
      foreignKeysValid: true,
      backupsPruned: 3,
      backupsRetained: 7,
      logsPruned: 1,
      rawPruned: 5
    });

    // runMaintenance reloads storage stats upon success
    http.expectOne('/api/v1/system/storage').flush({ ...mockStorageStats, databaseBytes: 900000 });
    await settle();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Mantenimiento completado con éxito.');
    expect(text).toContain('DB optimizada: Sí');
    expect(text).toContain('Copias retenidas/depuradas: 7/3');
    expect(text).toContain('RAW depurados: 5');
  });

  it('syncs basket items with external providers and informs result', async () => {
    const testBasket = {
      id: 'basket-sync-1',
      name: 'Mercado Semanal',
      createdAt: '2026-10-07T00:00:00Z',
      items: [{ id: 'i1', basketId: 'basket-sync-1', query: 'Leche', quantity: 3, productVariantId: null, notes: null }]
    };
    await openMarket([testBasket]);
    await settle();

    const syncBtn = Array.from(fixture.nativeElement.querySelectorAll('.market-actions button') as NodeListOf<HTMLButtonElement>)
      .find(btn => btn.textContent?.includes('Sincronizar precios de la lista'))!;
    expect(syncBtn).toBeTruthy();
    syncBtn.click();
    await settle();
    expect(syncBtn.disabled).toBe(true);

    const syncReq = http.expectOne(req => req.url === '/api/v1/sync/basket' && req.params.get('basketId') === 'basket-sync-1');
    expect(syncReq.request.method).toBe('POST');
    syncReq.flush({
      basketId: 'basket-sync-1',
      totalItems: 1,
      queriesExecuted: 2,
      results: [
        { provider: 'D1', status: 'Completed', itemsReceived: 10, offersProcessed: 8, offersChanged: 2, errorCode: null },
        { provider: 'EXITO', status: 'Completed', itemsReceived: 15, offersProcessed: 12, offersChanged: 3, errorCode: null }
      ]
    });
    await settle();

    expect(fixture.nativeElement.textContent).toContain('2 consultas para 1 productos. contexto no resuelto.');
  });

  it('renders non-blocking offline banner when server connection fails and clears on reconnection', async () => {
    http.expectOne('/api/v1/system/health').flush('offline', { status: 503, statusText: 'Unavailable' });
    http.expectOne('/api/v1/settings').flush('offline', { status: 503, statusText: 'Unavailable' });
    await settle();

    const banner = fixture.nativeElement.querySelector('.offline-banner');
    expect(banner).toBeTruthy();
    expect(banner?.textContent).toContain('Modo sin conexión');
    expect(banner?.textContent).toContain('Los datos locales permanecen seguros en tu equipo');

    // Retry connection
    fixture.componentInstance.refresh();
    http.expectOne('/api/v1/system/health').flush(health);
    http.expectOne('/api/v1/settings').flush(settings);
    await settle();

    expect(fixture.nativeElement.querySelector('.offline-banner')).toBeNull();
  });

  it('navigates to Comparar, loads benchmark pairs and switches tabs', async () => {
    await load();
    const app = fixture.componentInstance;
    app.navigate('Comparar');
    await settle();

    const req = http.expectOne('/api/v1/matching/pairs');
    expect(req.request.method).toBe('GET');
    req.flush({
      summary: { totalPairs: 16, exactPrecision: 1.0, exactRecall: 1.0, exactF1: 1.0, exactFalsePositives: 0 },
      exactos: [
        { pairId: 'EX-01', description: 'Coca-Cola Original 1.5L', expected: 1, actual: 1, confidence: 0.99, evidence: 'EAN idéntico', isCorrect: true }
      ],
      similares: [
        { pairId: 'SUB-04', description: 'Aceite Kythos 900ml vs Diana 1L', expected: 2, actual: 2, confidence: 0.8, evidence: 'Dentro de 15%', isCorrect: true }
      ],
      otrasMarcas: [
        { pairId: 'SUB-01', description: 'Leche Latti vs Alquería', expected: 2, actual: 2, confidence: 0.8, evidence: 'Marcas alternativas', isCorrect: true }
      ],
      all: []
    });
    await settle();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('PRECISIÓN EXACTA');
    expect(text).toContain('100%');
    expect(text).toContain('0 falsos positivos garantizados');
    expect(text).toContain('Coca-Cola Original 1.5L');
    expect(text).toContain('Exacto');

    // Switch tab to similares
    app.setComparisonTab('similares');
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Aceite Kythos 900ml vs Diana 1L');
    expect(fixture.nativeElement.textContent).not.toContain('Coca-Cola Original 1.5L');

    // Switch tab to otrasMarcas
    app.setComparisonTab('otrasMarcas');
    await settle();
    expect(fixture.nativeElement.textContent).toContain('Leche Latti vs Alquería');
  });

  it('allows saving user match overrides with clear confirmation', async () => {
    await load();
    const app = fixture.componentInstance;
    app.navigate('Comparar');
    http.expectOne('/api/v1/matching/pairs').flush({
      summary: { totalPairs: 1, exactPrecision: 1.0, exactRecall: 1.0, exactF1: 1.0, exactFalsePositives: 0 },
      exactos: [
        { pairId: 'EX-01', description: 'Coca-Cola Original 1.5L', expected: 1, actual: 1, confidence: 0.99, evidence: 'EAN idéntico', isCorrect: true }
      ],
      similares: [],
      otrasMarcas: [],
      all: []
    });
    await settle();

    app.setOverride('EX-01', 'Substitute');
    const req = http.expectOne('/api/v1/matching/overrides');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.kind).toBe(2);
    req.flush({ id: '00000000-0000-0000-0000-000000000099', kind: 2 });
    await settle();

    expect(app.overrideNotice()).toContain('Preferencia guardada para EX-01: Sustituto.');
    expect(fixture.nativeElement.textContent).toContain('Preferencia guardada para EX-01');
  });

  it('executes live presentation comparison with unit price and overbuy calculation', async () => {
    await load();
    const app = fixture.componentInstance;
    app.navigate('Comparar');
    http.expectOne('/api/v1/matching/pairs').flush({
      summary: { totalPairs: 0, exactPrecision: 1, exactRecall: 1, exactF1: 1, exactFalsePositives: 0 },
      exactos: [], similares: [], otrasMarcas: [], all: []
    });
    await settle();

    app.compNameA = 'Arroz Diana 1 kg';
    app.compPriceA = 4800;
    app.compNameB = 'Arroz De La Cuesta 1000 g';
    app.compPriceB = 3800;
    app.compTargetQty = 2.0;

    app.runCustomCompare();
    const compareReq = http.expectOne('/api/v1/matching/compare');
    expect(compareReq.request.method).toBe('POST');
    expect(compareReq.request.body.nameA).toBe('Arroz Diana 1 kg');
    expect(compareReq.request.body.priceA).toBe(4800);
    expect(compareReq.request.body.targetQuantity).toBe(2.0);

    compareReq.flush({
      productA: {
        normalized: { cleanName: 'Arroz Diana 1 kg', normalizedBrand: 'Diana', productType: 'Arroz', variantType: 'Regular', quantity: { totalAmount: 1.0, originalUnit: 'kg' } },
        unitPrice: { absolutePrice: 4800, pricePerBaseUnit: 4800, formattedPricePerBaseUnit: '$4,800/kg' },
        overbuy: { targetQuantity: 2.0, itemQuantity: 1.0, packagesRequired: 2, totalCost: 9600, overbuyPercentage: 0.0, isAcceptable: true, explanation: 'Compra exacta de 2 unidades' }
      },
      productB: {
        normalized: { cleanName: 'Arroz De La Cuesta 1000 g', normalizedBrand: 'De La Cuesta', productType: 'Arroz', variantType: 'Regular', quantity: { totalAmount: 1.0, originalUnit: 'g' } },
        unitPrice: { absolutePrice: 3800, pricePerBaseUnit: 3800, formattedPricePerBaseUnit: '$3,800/kg' },
        overbuy: { targetQuantity: 2.0, itemQuantity: 1.0, packagesRequired: 2, totalCost: 7600, overbuyPercentage: 0.0, isAcceptable: true, explanation: 'Compra exacta de 2 unidades' }
      },
      match: {
        kind: 2, // Substitute
        confidenceScore: 0.8,
        evidence: 'Substitute: Alternative brand (Diana vs De La Cuesta)',
        hasConflict: false,
        conflictReason: null
      }
    });
    await settle();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Sustituto');
    expect(text).toContain('$4,800/kg');
    expect(text).toContain('$3,800/kg');
    expect(text).toContain('Paquetes requeridos: 2');
    expect(text).toContain('$9,600');
    expect(text).toContain('$7,600');
  });

  it('parses location, geocodes if needed, and updates active coordinates and stores', async () => {
    await load();
    const app = fixture.componentInstance;
    app.locationInput = '6.2425, -75.5925';
    app.parseLocation();

    const parseReq = http.expectOne('/api/v1/location/parse');
    expect(parseReq.request.method).toBe('POST');
    expect(parseReq.request.body.input).toBe('6.2425, -75.5925');
    parseReq.flush({
      originalInput: '6.2425, -75.5925',
      normalizedAddress: '6.24250, -75.59250',
      latitude: 6.2425,
      longitude: -75.5925,
      source: 'Coordinates',
      status: 'Success',
      errorMessage: null
    });

    const storesReq = http.expectOne(req => req.url === '/api/v1/location/stores');
    expect(storesReq.request.params.get('originLat')).toBe('6.2425');
    expect(storesReq.request.params.get('originLon')).toBe('-75.5925');
    storesReq.flush([
      { id: 'store-1', name: 'D1 Laureles', brand: 'D1', address: 'Transversal 39B # 72-10', latitude: 6.2440, longitude: -75.5910, distanceKm: 0.23 }
    ]);
    await settle();

    expect(app.currentLatitude()).toBe(6.2425);
    expect(app.currentLongitude()).toBe(-75.5925);
    expect(app.nearbyStores().length).toBe(1);
    expect(app.nearbyStores()[0].name).toBe('D1 Laureles');
  });

  it('does not accept offline geocode reference coordinates as an active location', async () => {
    await load();
    const app = fixture.componentInstance;
    app.locationInput = 'Laureles Circular 1';
    app.parseLocation();
    http.expectOne('/api/v1/location/parse').flush({
      originalInput: 'Laureles Circular 1', normalizedAddress: 'Laureles Circular 1',
      latitude: null, longitude: null, source: 'Address', status: 'NeedsGeocoding', errorMessage: null
    });
    http.expectOne('/api/v1/location/geocode').flush({
      address: 'Laureles', latitude: 6.2425, longitude: -75.5925, providerStatus: 'MOCK_OFFLINE'
    });
    expect(app.currentLatitude()).toBeNull();
    expect(app.currentLongitude()).toBeNull();
    expect(app.locationError()).toContain('no verificada');
    http.expectNone(req => req.url === '/api/v1/location/stores');
  });

  it('manages location profiles: loads, saves, selects, and deletes profile', async () => {
    await load();
    const app = fixture.componentInstance;

    // 1. Load profiles
    app.loadProfiles();
    const loadReq = http.expectOne('/api/v1/location/profiles');
    expect(loadReq.request.method).toBe('GET');
    loadReq.flush([
      { id: 'p1', label: 'Mi Casa', address: 'Circular 1 # 70-15', latitude: 6.2425, longitude: -75.5925, createdAt: '2026-10-07T00:00:00Z' }
    ]);
    expect(app.locationProfiles().length).toBe(1);

    // 2. Select profile
    app.selectProfile('p1');
    const storeReq = http.expectOne(req => req.url === '/api/v1/location/stores');
    storeReq.flush([]);
    expect(app.currentLatitude()).toBe(6.2425);
    expect(app.currentLongitude()).toBe(-75.5925);

    // 3. Save new profile
    app.newProfileLabel = 'Oficina';
    app.saveCurrentAsProfile();
    const saveReq = http.expectOne('/api/v1/location/profiles');
    expect(saveReq.request.method).toBe('POST');
    expect(saveReq.request.body.label).toBe('Oficina');
    saveReq.flush({
      id: 'p2', label: 'Oficina', address: 'Circular 1 # 70-15', latitude: 6.2425, longitude: -75.5925, createdAt: '2026-10-07T00:00:00Z'
    });
    expect(app.locationProfiles().length).toBe(2);

    // 4. Delete profile
    app.deleteProfile('p1');
    const delReq = http.expectOne('/api/v1/location/profiles/p1');
    expect(delReq.request.method).toBe('DELETE');
    delReq.flush(null);
    expect(app.locationProfiles().length).toBe(1);
    expect(app.locationProfiles()[0].id).toBe('p2');
  });

  it('saves location profile with structured address components from geocode result', async () => {
    await load();
    const app = fixture.componentInstance;

    // 1. Geocode via LocationFacade
    app.geocodeAddress('Circular 1 # 70-15, Medellín');
    const geoReq = http.expectOne('/api/v1/location/geocode');
    expect(geoReq.request.method).toBe('POST');
    expect(geoReq.request.body.address).toBe('Circular 1 # 70-15, Medellín');

    geoReq.flush({
      address: 'Circular 1 # 70-15, Medellín',
      formattedAddress: 'Circular 1 # 70-15, Laureles, Medellín, Antioquia 050031, Colombia',
      latitude: 6.2425,
      longitude: -75.5925,
      providerStatus: 'LIVE_GOOGLE',
      components: {
        street: 'Circular 1',
        streetNumber: '70-15',
        neighborhood: 'Laureles',
        postalCode: '050031',
        city: 'Medellín',
        state: 'Antioquia',
        country: 'Colombia',
        countryCode: 'CO'
      }
    });

    const storeReq = http.expectOne(req => req.url === '/api/v1/location/stores');
    storeReq.flush([]);

    expect(app.locationFacade.profileStreet()).toBe('Circular 1');
    expect(app.locationFacade.profileNumber()).toBe('70-15');
    expect(app.locationFacade.profileNeighborhood()).toBe('Laureles');
    expect(app.locationFacade.profilePostalCode()).toBe('050031');
    expect(app.locationFacade.profileCity()).toBe('Medellín');
    expect(app.locationFacade.profileState()).toBe('Antioquia');

    // 2. Save profile
    app.newProfileLabel = 'Casa Laureles';
    app.saveCurrentAsProfile();

    const saveReq = http.expectOne('/api/v1/location/profiles');
    expect(saveReq.request.method).toBe('POST');
    const body = saveReq.request.body;
    expect(body.label).toBe('Casa Laureles');
    expect(body.latitude).toBe(6.2425);
    expect(body.longitude).toBe(-75.5925);
    expect(body.street).toBe('Circular 1');
    expect(body.number).toBe('70-15');
    expect(body.postalCode).toBe('050031');
    expect(body.neighborhood).toBe('Laureles');
    expect(body.city).toBe('Medellín');
    expect(body.state).toBe('Antioquia');

    saveReq.flush({
      id: 'p-laureles',
      label: 'Casa Laureles',
      address: 'Circular 1 # 70-15, Laureles, Medellín, Antioquia 050031, Colombia',
      latitude: 6.2425,
      longitude: -75.5925,
      street: 'Circular 1',
      number: '70-15',
      postalCode: '050031',
      neighborhood: 'Laureles',
      city: 'Medellín',
      state: 'Antioquia',
      createdAt: '2026-10-09T00:00:00Z'
    });

    expect(app.locationProfiles().some(p => p.id === 'p-laureles')).toBe(true);
  });

  it('optimizes store route, calculates legs, and provides zero-key navigation URL', async () => {
    await load();
    const app = fixture.componentInstance;
    app.currentLatitude.set(6.2425);
    app.currentLongitude.set(-75.5925);
    app.selectedStoreIds.set(['s1', 's2']);
    app.transportMode.set('Driving');
    app.returnToOrigin.set(true);
    app.costPerKm.set(2500);

    app.optimizeRoute();
    const optReq = http.expectOne('/api/v1/routes/optimize');
    expect(optReq.request.method).toBe('POST');
    expect(optReq.request.body.originLatitude).toBe(6.2425);
    expect(optReq.request.body.storeIds).toEqual(['s1', 's2']);
    expect(optReq.request.body.returnToOrigin).toBe(true);
    expect(optReq.request.body.transportMode).toBe('Driving');

    optReq.flush({
      orderedStores: [
        { id: 's1', name: 'D1 Laureles', brand: 'D1', address: 'Transversal 39B', latitude: 6.2440, longitude: -75.5910 },
        { id: 's2', name: 'Éxito Viva Laureles', brand: 'Éxito', address: 'Cra 81', latitude: 6.2480, longitude: -75.6020 }
      ],
      legs: [
        { fromName: 'Origen', toName: 'D1 Laureles', distanceKm: 0.5, durationMinutes: 2, type: 'StraightLineEstimate', provider: 'HaversineUrbanWinding' },
        { fromName: 'D1 Laureles', toName: 'Éxito Viva Laureles', distanceKm: 1.5, durationMinutes: 5, type: 'StraightLineEstimate', provider: 'HaversineUrbanWinding' },
        { fromName: 'Éxito Viva Laureles', toName: 'Origen (Retorno)', distanceKm: 1.8, durationMinutes: 6, type: 'StraightLineEstimate', provider: 'HaversineUrbanWinding' }
      ],
      totalDistanceKm: 3.8,
      totalDurationMinutes: 13,
      estimatedTravelCost: 9500,
      googleMapsNavigationUrl: 'https://www.google.com/maps/dir/?api=1&origin=6.2425%2C-75.5925&destination=6.2425%2C-75.5925&waypoints=6.2440%2C-75.5910%7C6.2480%2C-75.6020&travelmode=driving',
      costPerKm: 2500
    });

    await settle();
    expect(app.routePlan()).toBeTruthy();
    expect(app.routePlan()?.totalDistanceKm).toBe(3.8);
    expect(app.routePlan()?.estimatedTravelCost).toBe(9500);
    expect(app.routePlan()?.googleMapsNavigationUrl).toContain('https://www.google.com/maps/dir/?api=1');
    expect(app.routePlan()?.googleMapsNavigationUrl).not.toContain('key=');
  });

  it('loads Google usage stats in settings without exposing secrets', async () => {
    await load();
    const app = fixture.componentInstance;
    app.loadGoogleUsage();

    const req = http.expectOne('/api/v1/location/usage');
    expect(req.request.method).toBe('GET');
    req.flush({
      geocodingConfigured: false,
      routingConfigured: false,
      todayGeocodeCalls: 4,
      todayRouteCalls: 2,
      history: [{ date: '2026-10-07', geocodeCount: 4, routeCount: 2 }]
    });

    expect(app.googleUsage()).toBeTruthy();
    expect(app.googleUsage()?.todayGeocodeCalls).toBe(4);
    expect(app.googleUsage()?.todayRouteCalls).toBe(2);
  });

  it('loads AI status and saves AI settings securely', async () => {
    await load();
    const app = fixture.componentInstance;
    app.loadAiStatus();

    const req = http.expectOne('/api/v1/ai/status');
    expect(req.request.method).toBe('GET');
    req.flush({
      isConfigured: true,
      enabled: true,
      model: 'anthropic/claude-3.5-haiku',
      baseUrl: 'https://openrouter.ai/api/v1',
      timeoutSeconds: 20,
      maxTokens: 1024,
      temperature: 0.2,
      features: { normalization: true, matching: true, substitutes: true, explanation: true },
      monthlyBudgetUsd: 5.0,
      warningBudgetUsd: 3.0,
      hardBudgetLimit: true,
      usage: {
        totalCalls: 12,
        promptTokens: 1200,
        completionTokens: 800,
        estimatedCostUsd: 0.005,
        estimatedCostCop: 21,
        cacheHits: 4,
        errorsCount: 0,
        recentDailyUsage: []
      }
    });

    expect(app.aiStatus()).toBeTruthy();
    expect(app.aiStatus()?.isConfigured).toBe(true);
    expect(app.aiStatus()?.model).toBe('anthropic/claude-3.5-haiku');

    // Save AI settings
    app.aiStatus()!.model = 'openai/gpt-4o-mini';
    app.aiStatus()!.timeoutSeconds = 15;
    app.saveAiSettings();

    const saveReq = http.expectOne('/api/v1/ai/settings');
    expect(saveReq.request.method).toBe('PUT');
    expect(saveReq.request.body.model).toBe('openai/gpt-4o-mini');
    expect(saveReq.request.body.timeoutSeconds).toBe(15);
    expect(saveReq.request.body.warningBudgetUsd).toBe(3.0);
    expect(saveReq.request.body.hardBudgetLimit).toBe(true);
    saveReq.flush({
      enabled: true,
      model: 'openai/gpt-4o-mini',
      timeoutSeconds: 15,
      features: { normalization: true, matching: true, substitutes: true, explanation: true }
    });

    // saveAiSettings triggers refresh() which loads health and settings
    http.expectOne('/api/v1/system/health').flush(health);
    http.expectOne('/api/v1/settings').flush(settings);
    await settle();

    expect(app.aiNotice()).toContain('Configuración de IA actualizada');
    expect(app.aiStatus()?.model).toBe('openai/gpt-4o-mini');
  });

  it('tests AI connection and handles cache clearance', async () => {
    await load();
    const app = fixture.componentInstance;
    app.testAiConnection();

    const testReq = http.expectOne('/api/v1/ai/test-connection');
    expect(testReq.request.method).toBe('POST');
    testReq.flush({
      success: true,
      statusCode: 200,
      message: 'Conexión a OpenRouter exitosa (latencia: 312ms)',
      latencyMs: 312,
      modelTested: 'anthropic/claude-3.5-haiku'
    });

    expect(app.aiConnectionTest()).toBeTruthy();
    expect(app.aiConnectionTest()?.success).toBe(true);
    expect(app.aiConnectionTest()?.latencyMs).toBe(312);

    // Clear AI cache
    app.clearAiCache();
    const clearReq = http.expectOne('/api/v1/ai/cache/clear');
    expect(clearReq.request.method).toBe('POST');
    clearReq.flush({ cleared: true });

    // clearAiCache reloads status via loadAiStatus()
    const reloadReq = http.expectOne('/api/v1/ai/status');
    reloadReq.flush({
      isConfigured: true,
      enabled: true,
      model: 'anthropic/claude-3.5-haiku',
      baseUrl: 'https://openrouter.ai/api/v1',
      timeoutSeconds: 20,
      maxTokens: 1024,
      temperature: 0.2,
      features: { normalization: true, matching: true, substitutes: true, explanation: true },
      monthlyBudgetUsd: null,
      usage: {
        totalCalls: 12,
        promptTokens: 1200,
        completionTokens: 800,
        estimatedCostUsd: 0.005,
        estimatedCostCop: 21,
        cacheHits: 0,
        errorsCount: 0,
        recentDailyUsage: []
      }
    });

    expect(app.aiNotice()).toContain('Caché local de respuestas de IA vaciada');
  });

  it('runs basket optimization and requests AI natural language explanation', async () => {
    const testBasket = {
      id: 'b-opt-1',
      name: 'Mercado Mensual',
      createdAt: '2026-10-07T00:00:00Z',
      items: [
        { id: 'i1', basketId: 'b-opt-1', query: 'Leche Alquería', quantity: 2, productVariantId: null, notes: null }
      ]
    };
    await openMarket([testBasket]);
    const app = fixture.componentInstance;

    app.optimizeBasket();
    const optReq = http.expectOne('/api/v1/baskets/b-opt-1/optimize');
    expect(optReq.request.method).toBe('POST');

    optReq.flush({
      strategy: 1, // MinimizeCost
      maxStoresAllowed: 2,
      storesUsedCount: 1,
      totalCost: 11000,
      baselineCost: 14000,
      totalSavingsVsBaseline: 3000,
      totalItemsRequested: 1,
      itemsCoveredCount: 1,
      missingItemsCount: 0,
      stores: [
        {
          storeId: 's1',
          storeName: 'D1 Laureles',
          brand: 'D1',
          itemsCount: 1,
          subtotal: 11000,
          items: [
            { query: 'Leche Alquería', productName: 'Leche Latti 1.1L', quantity: 2, unitPrice: 5500, lineTotal: 11000 }
          ]
        }
      ],
      warnings: [],
      routePlan: null
    });

    expect(app.basketOptimization()).toBeTruthy();
    expect(app.basketOptimization()?.totalCost).toBe(11000);
    expect(app.basketOptimization()?.totalSavingsVsBaseline).toBe(3000);

    // Request AI Explanation
    app.explainBasketOptimization();
    const explainReq = http.expectOne('/api/v1/ai/explain');
    expect(explainReq.request.method).toBe('POST');
    expect(explainReq.request.body.totalCost).toBe(11000);
    expect(explainReq.request.body.estimatedSavings).toBe(3000);

    explainReq.flush({
      summary: 'Tu compra se resuelve completamente en D1 Laureles con un ahorro de $3,000 COP.',
      whyRecommended: 'D1 ofrece el mejor precio por litro sin requerir desplazamientos adicionales.',
      storeTradeoffs: 'Una sola tienda evita costos y tiempo de desplazamiento.',
      keySavingsSources: ['$3,000 COP en lácteos'],
      warnings: [],
      source: 'AI',
      modelUsed: 'anthropic/claude-3.5-haiku',
      promptVersion: 'v1.0'
    });

    expect(app.basketExplanation()).toBeTruthy();
    expect(app.basketExplanation()?.source).toBe('AI');
    expect(app.basketExplanation()?.summary).toContain('D1 Laureles');
  });

  it('runs AI match adjudication on custom comparison', async () => {
    await load();
    const app = fixture.componentInstance;
    app.navigate('Comparar');
    http.expectOne('/api/v1/matching/pairs').flush({ summary: { totalPairs: 0 }, exactos: [], similares: [], otrasMarcas: [], all: [] });

    app.compNameA = 'Aceite Premier 900ml';
    app.compPriceA = 8000;
    app.compNameB = 'Aceite Diana 900ml';
    app.compPriceB = 7500;
    app.runCustomCompare();

    const compareReq = http.expectOne('/api/v1/matching/compare');
    compareReq.flush({
      productA: {
        normalized: { cleanName: 'Aceite Premier 900ml', normalizedBrand: 'Premier', productType: 'Aceite', variantType: 'Regular', quantity: { totalAmount: 900, originalUnit: 'ml' } },
        unitPrice: { absolutePrice: 8000, pricePerBaseUnit: 8.89, formattedPricePerBaseUnit: '$8.89/ml' },
        overbuy: { targetQuantity: 1, itemQuantity: 1, packagesRequired: 1, totalCost: 8000, overbuyPercentage: 0, isAcceptable: true, explanation: 'OK' }
      },
      productB: {
        normalized: { cleanName: 'Aceite Diana 900ml', normalizedBrand: 'Diana', productType: 'Aceite', variantType: 'Regular', quantity: { totalAmount: 900, originalUnit: 'ml' } },
        unitPrice: { absolutePrice: 7500, pricePerBaseUnit: 8.33, formattedPricePerBaseUnit: '$8.33/ml' },
        overbuy: { targetQuantity: 1, itemQuantity: 1, packagesRequired: 1, totalCost: 7500, overbuyPercentage: 0, isAcceptable: true, explanation: 'OK' }
      },
      match: {
        kind: 2,
        confidenceScore: 0.8,
        evidence: 'Similar items from different brands',
        hasConflict: false,
        conflictReason: null
      }
    });

    app.adjudicateWithAi();

    const adjReq = http.expectOne('/api/v1/ai/adjudicate');
    expect(adjReq.request.method).toBe('POST');
    expect(adjReq.request.body.nameA).toBe('Aceite Premier 900ml');
    expect(adjReq.request.body.nameB).toBe('Aceite Diana 900ml');

    adjReq.flush({
      decision: 'SUBSTITUTE',
      confidence: 0.88,
      conciseReason: 'Misma función culinaria y volumen idéntico, marcas diferentes.',
      attributesInCommon: ['aceite vegetal', '900ml'],
      attributesDiffering: ['Premier vs Diana'],
      source: 'AI',
      modelUsed: 'anthropic/claude-3.5-haiku',
      promptVersion: 'v1.0'
    });

    expect(app.aiAdjudication()).toBeTruthy();
    expect(app.aiAdjudication()?.decision).toBe('SUBSTITUTE');
    expect(app.aiAdjudication()?.confidence).toBe(0.88);
    expect(app.aiAdjudication()?.source).toBe('AI');
  });

  it('displays honest capabilities and coverage badges for registered providers', async () => {
    await load();
    const app = fixture.componentInstance;
    app.navigate('Fuentes');
    http.expectOne('/api/v1/providers/d1/health').flush({ status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers/exito/health').flush({ status: 'PARTIAL_FASTSTORE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers').flush([
      {
        code: 'ARA',
        name: 'Tiendas Ara',
        retailerType: 'Hard Discount (Descuento Duro)',
        catalogCoverage: 'Parcial (Folletos públicos y entradas manuales)',
        status: 'PARTIAL_HONEST_BROCHURE_AND_MANUAL',
        capabilities: {
          search: 'Partial',
          details: 'Partial',
          offers: 'Partial',
          stores: 'Partial',
          availability: 'NotSupported',
          catalogEnumeration: 'NotSupported'
        }
      },
      {
        code: 'D1',
        name: 'Tiendas D1',
        retailerType: 'Hard Discount',
        catalogCoverage: 'Completa (Online y Regional)',
        status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH',
        capabilities: {
          search: 'Supported',
          details: 'Supported',
          offers: 'Supported',
          stores: 'Supported',
          availability: 'Supported',
          catalogEnumeration: 'Supported'
        }
      }
    ]);
    await settle();

    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Tiendas Ara');
    expect(text).toContain('[PARCIAL]');
    expect(text).toContain('[FOLLETO]');
    expect(text).toContain('[MANUAL]');
    expect(text).toContain('[COMPLETA]');
    expect(text).toContain('Stock: No');
    expect(text).toContain('Stock: Soportado');
  });

  it('queries Ara health report and statistics', async () => {
    await load();
    const app = fixture.componentInstance;
    app.navigate('Fuentes');
    http.expectOne('/api/v1/providers/d1/health').flush({ status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers/exito/health').flush({ status: 'PARTIAL_FASTSTORE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers').flush([]);
    await settle();

    app.loadAraHealth();
    expect(app.araHealthLoading()).toBe(true);

    const healthReq = http.expectOne('/api/v1/stores/ara/health');
    healthReq.flush({
      provider: 'ARA',
      status: 'PARTIAL_HONEST_BROCHURE_AND_MANUAL',
      coverage: 'Public brochures and community observations',
      totalActiveOffers: 14,
      brochureOffersCount: 10,
      manualOffersCount: 4,
      lastOfferUpdate: '2026-10-07T14:30:00Z',
      summary: '14 ofertas activas (10 folletos, 4 manuales)'
    });
    await settle();

    expect(app.araHealth()).toBeTruthy();
    expect(app.araHealth()?.totalActiveOffers).toBe(14);
    expect(app.araHealth()?.brochureOffersCount).toBe(10);
    expect(app.araHealth()?.manualOffersCount).toBe(4);
    expect(fixture.nativeElement.textContent).toContain('14 ofertas activas');
  });

  it('creates and deletes Ara manual community offers with optimistic reactivity', async () => {
    await load();
    const app = fixture.componentInstance;
    app.navigate('Fuentes');
    http.expectOne('/api/v1/providers/d1/health').flush({ status: 'PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers/exito/health').flush({ status: 'PARTIAL_FASTSTORE_MANUAL_REFRESH', lastRun: null });
    http.expectOne('/api/v1/providers').flush([]);
    await settle();

    // 1. Submit manual offer
    app.araProductName = 'Arroz Diana 1000g';
    app.araBrand = 'Diana';
    app.araQuantity = 1000;
    app.araUnit = 'g';
    app.araPrice = 4200;
    app.araStoreName = 'Ara Belén';
    app.araNotes = 'Góndola central';

    app.submitAraManualOffer();
    expect(app.araManualBusy()).toBe(true);

    const postReq = http.expectOne('/api/v1/stores/ara/manual');
    expect(postReq.request.method).toBe('POST');
    expect(postReq.request.body.productName).toBe('Arroz Diana 1000g');
    expect(postReq.request.body.price).toBe(4200);
    expect(postReq.request.body.storeName).toBe('Ara Belén');

    postReq.flush({
      offerId: 'ara-off-1',
      productId: 'p-1',
      productName: 'Arroz Diana 1000g',
      brand: 'Diana',
      skuQuantity: 1000,
      skuDimension: 'Mass',
      price: 4200,
      listPrice: null,
      storeName: 'Ara Belén',
      scope: 'PhysicalVerified',
      checkedAt: '2026-10-07T12:00:00Z',
      validUntil: null,
      notes: 'Góndola central'
    });
    await settle();

    expect(app.araManualBusy()).toBe(false);
    expect(app.araManualOffers().length).toBe(1);
    expect(app.araManualOffers()[0].productName).toBe('Arroz Diana 1000g');
    expect(app.araManualNotice()).toContain('Oferta registrada');
    expect(fixture.nativeElement.textContent).toContain('Arroz Diana 1000g');
    expect(fixture.nativeElement.textContent).toContain('Ara Belén (Físico verificado)');

    // 2. Delete manual offer
    app.deleteAraManualOffer('ara-off-1');
    const delReq = http.expectOne('/api/v1/stores/ara/manual/ara-off-1');
    expect(delReq.request.method).toBe('DELETE');
    delReq.flush(null);
    await settle();

    expect(app.araManualOffers().length).toBe(0);
    expect(app.araManualNotice()).toContain('eliminada correctamente');
  });

  // GOAL-10 Historial, Observabilidad y Release
  it('navigates to Historial, loads price history, filters and renders trends and observations', async () => {
    await load();
    const app = fixture.componentInstance;
    app.navigate('Historial');
    await settle();

    const historyReq = http.expectOne('/api/v1/history');
    expect(historyReq.request.method).toBe('GET');

    historyReq.flush({
      totalObservations: 2,
      statistics: {
        totalObservations: 2,
        minPrice: 3800,
        maxPrice: 4200,
        avgPrice: 4000,
        uniqueProducts: 1,
        uniqueRetailers: 2
      },
      productTrends: [
        {
          productName: 'Arroz Diana 1000g',
          skuName: '1000g',
          retailerCode: 'D1',
          count: 2,
          minPrice: 3800,
          maxPrice: 4200,
          latestPrice: 4200,
          priceDelta: 400,
          latestCheckedAt: '2026-10-07T14:00:00Z'
        }
      ],
      observations: [
        {
          id: 'obs-1',
          offerId: 'off-1',
          productName: 'Arroz Diana 1000g',
          brand: 'Diana',
          skuName: '1000g',
          ean: '7701234567890',
          retailerCode: 'D1',
          retailerName: 'Tiendas D1',
          sellerName: 'D1',
          price: 4200,
          listPrice: 4500,
          availability: 'InStock',
          scope: 'PhysicalVerified',
          context: 'store:d1-laureles',
          checkedAt: '2026-10-07T14:00:00Z'
        }
      ]
    });
    await settle();

    const text = fixture.nativeElement.textContent;
    expect(app.historyData()).toBeTruthy();
    expect(text).toContain('Historial de precios');
    expect(text).toContain('Arroz Diana 1000g');
    expect(text).toContain('+$400');
    expect(text).toContain('Físico góndola');
    expect(text).toContain('store:d1-laureles');

    // Test filtering
    app.historyFilterQuery = 'Arroz';
    app.historyFilterRetailer = 'D1';
    app.historyFilterScope = 'PhysicalVerified';
    app.loadHistory();

    const filterReq = http.expectOne(req =>
      req.url === '/api/v1/history' &&
      req.params.get('query') === 'Arroz' &&
      req.params.get('retailer') === 'D1' &&
      req.params.get('scope') === 'PhysicalVerified'
    );
    expect(filterReq.request.method).toBe('GET');
    filterReq.flush({
      totalObservations: 1,
      statistics: {
        totalObservations: 1,
        minPrice: 4200,
        maxPrice: 4200,
        avgPrice: 4200,
        uniqueProducts: 1,
        uniqueRetailers: 1
      },
      productTrends: [],
      observations: []
    });
    await settle();
    expect(app.historyData()?.totalObservations).toBe(1);
  });

  it('loads system status and renders observability metrics and pending external actions', async () => {
    await openSecrets();
    http.expectOne('/api/v1/secrets/google').flush({ configured: false });
    http.expectOne('/api/v1/secrets/openrouter').flush({ configured: false });
    await settle();

    const app = fixture.componentInstance;
    app.loadSystemStatus();
    app.loadBackups();
    await settle();

    const statusReq = http.expectOne('/api/v1/system/status');
    const backupsReq = http.expectOne('/api/v1/backups');

    expect(statusReq.request.method).toBe('GET');
    expect(backupsReq.request.method).toBe('GET');

    statusReq.flush({
      version: APP_VERSION,
      appStatus: 'OPERATIONAL',
      database: {
        integrity: 'ok',
        foreignKeysValid: true,
        sizeBytes: 1048576,
        productsCount: 42,
        skusCount: 50,
        offersCount: 88,
        observationsCount: 120
      },
      raw: {
        sizeBytes: 524288,
        filesCount: 15
      },
      backups: {
        sizeBytes: 2097152,
        filesCount: 2,
        lastBackupName: 'market-20261007-120000-000-abcd1234.db',
        available: []
      },
      lastSync: {
        provider: 'D1',
        state: 'Completed',
        finishedAt: '2026-10-07T12:00:00Z',
        itemsReceived: 25
      },
      providers: [],
      google: {
        configured: false,
        status: 'NOT_CONFIGURED'
      },
      ai: {
        configured: false,
        status: 'NOT_CONFIGURED'
      },
      pendingExternalActions: [
        'Configurar clave de OpenRouter en Ajustes para capacidades semánticas opcionales.',
        'Validación física en góndola (5-10 productos por cadena D1 y Éxito).'
      ]
    });

    backupsReq.flush([
      {
        name: 'market-20261007-120000-000-abcd1234.db',
        bytes: 1048576,
        createdAtUtc: '2026-10-07T12:00:00Z',
        type: 'Database'
      },
      {
        name: 'market-20261007-130000-000-efgh5678.zip',
        bytes: 2097152,
        createdAtUtc: '2026-10-07T13:00:00Z',
        type: 'Archive'
      }
    ]);
    await settle();

    const text = fixture.nativeElement.textContent;
    expect(app.systemStatus()).toBeTruthy();
    expect(app.systemStatus()?.version).toBe(APP_VERSION);
    expect(app.systemStatus()?.appStatus).toBe('OPERATIONAL');
    expect(text).toContain('OPERATIONAL');
    expect(text).toContain(`Versión ${APP_VERSION}`);
    expect(text).toContain('Validación física en góndola');

    expect(app.backupsList().length).toBe(2);
    expect(text).toContain('market-20261007-120000-000-abcd1234.db');
    expect(text).toContain('market-20261007-130000-000-efgh5678.zip');
    expect(text).toContain('scripts/restore-backup.ps1');
  });

  it('triggers safe backup creation and reports result', async () => {
    await load();
    const app = fixture.componentInstance;
    app.backup();

    const postReq = http.expectOne('/api/v1/backups');
    expect(postReq.request.method).toBe('POST');
    postReq.flush({ file: 'market-20261007-140000-000-11223344.db' });

    http.expectOne('/api/v1/system/storage').flush(mockStorageStats);
    await settle();

    expect(app.message()).toContain('Copia creada: market-20261007-140000-000-11223344.db');
  });
});


    // marker
