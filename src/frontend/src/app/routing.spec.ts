import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { Location } from '@angular/common';
import { App } from './app';
import { routes } from './app.routes';
import { HomePage, SearchPage, MarketPage, ComparePage, HistoryPage, SourcesPage, SettingsPage, AnalysisPage } from './feature-pages';

describe('Angular Router - Deep Linking, Navigation and Redirection', () => {
  const settings = { maxStores: 2, sizeTolerancePercent: 15, allowSubstitutes: true, maxOverbuyPercent: 20, aiEnabled: false, includeMarketplace: false };
  const health = { database: 'ok', products: 10, ai: 'NOT_CONFIGURED', google: 'NOT_CONFIGURED', mode: 'LOCAL' };

  let fixture: ReturnType<typeof TestBed.createComponent<App>>;
  let router: Router;
  let location: Location;
  let http: HttpTestingController;

  async function settle() {
    fixture.detectChanges();
    await fixture.whenStable();
    await new Promise(r => setTimeout(r, 0));
    fixture.detectChanges();
  }

  function flushAppBasics() {
    http.match('/api/v1/system/health').forEach(req => req.flush(health));
    http.match('/api/v1/settings').forEach(req => req.flush(settings));
    http.match('/api/v1/ai/status').forEach(req => req.flush({
      isConfigured: false, enabled: false, model: '', baseUrl: '', timeoutSeconds: 20,
      maxTokens: 1024, temperature: 0, features: {
        normalization: false, matching: false, substitutes: false, explanation: false
      }, monthlyBudgetUsd: null, usage: {
        totalCalls: 0, promptTokens: 0, completionTokens: 0, estimatedCostUsd: 0,
        estimatedCostCop: 0, cacheHits: 0, errorsCount: 0, recentDailyUsage: []
      }
    }));
    http.match('/api/v1/system/storage').forEach(req => req.flush({
      databaseBytes: 1000, sidecarsBytes: 0, rawBytes: 0, logsBytes: 0, backupsBytes: 0,
      externalProductsCount: 10, externalSkusCount: 10, offersCount: 10, priceObservationsCount: 10
    }));
    http.match('/api/v1/secrets/google').forEach(req => req.flush({ configured: false }));
    http.match('/api/v1/secrets/openrouter').forEach(req => req.flush({ configured: false }));
    http.match('/api/v1/baskets').forEach(req => req.flush([]));
    http.match('/api/v1/providers/d1/health').forEach(req => req.flush({ status: 'OK', lastRun: null }));
    http.match('/api/v1/providers/exito/health').forEach(req => req.flush({ status: 'OK', lastRun: null }));
    http.match('/api/v1/providers').forEach(req => req.flush([]));
    http.match('/api/v1/matching/pairs').forEach(req => req.flush({ summary: { totalPairs: 0 }, exactos: [], similares: [], otrasMarcas: [], all: [] }));
    http.match('/api/v1/history').forEach(req => req.flush({ totalObservations: 0, statistics: { totalObservations: 0 }, productTrends: [], observations: [] }));
    http.match('/api/v1/analysis/opportunities').forEach(req => req.flush({ bestUnitPrices: [], highestDiscounts: [], bestValueItems: [] }));
  }

  beforeEach(async () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter(routes)
      ]
    });

    router = TestBed.inject(Router);
    location = TestBed.inject(Location);
    http = TestBed.inject(HttpTestingController);
    fixture = TestBed.createComponent(App);
    flushAppBasics();
    await settle();
  });

  afterEach(() => {
    flushAppBasics();
    http.verify();
    TestBed.resetTestingModule();
  });

  it('navigates directly to deep link /buscar and mounts SearchPage', async () => {
    await router.navigateByUrl('/buscar');
    flushAppBasics();
    await settle();

    expect(location.path()).toBe('/buscar');
    expect(fixture.componentInstance.page()).toBe('Buscar');
    expect(fixture.nativeElement.querySelector('nav button[aria-current="page"]')?.textContent).toContain('Buscar');
    expect(fixture.nativeElement.querySelector('.search-form')).toBeTruthy();
  });

  it('navigates directly to deep link /ajustes and mounts SettingsPage', async () => {
    await router.navigateByUrl('/ajustes');
    flushAppBasics();
    await settle();

    expect(location.path()).toBe('/ajustes');
    expect(fixture.componentInstance.page()).toBe('Ajustes');
    expect(fixture.nativeElement.querySelector('nav button[aria-current="page"]')?.textContent).toContain('Ajustes');
    expect(fixture.nativeElement.querySelector('.settings-grid')).toBeTruthy();
  });

    it('navigates directly to deep link /analisis and mounts AnalysisPage', async () => {
    await router.navigateByUrl('/analisis');
    http.match('/api/v1/analysis/opportunities').forEach(req => req.flush({ bestUnitPrices: [], highestDiscounts: [], bestValueItems: [] }));
    flushAppBasics();
    await settle();

    expect(location.path()).toBe('/analisis');
    expect(fixture.componentInstance.page()).toBe('Análisis');
    expect(fixture.nativeElement.querySelector('nav button[aria-current="page"]')?.textContent).toContain('Análisis');
    expect(fixture.nativeElement.textContent).toContain('Analiza antes de comprar');
  });

  it('navigates directly to deep link /mi-mercado and mounts MarketPage', async () => {
    await router.navigateByUrl('/mi-mercado');
    flushAppBasics();
    await settle();

    expect(location.path()).toBe('/mi-mercado');
    expect(fixture.componentInstance.page()).toBe('Mi mercado');
    expect(fixture.nativeElement.querySelector('nav button[aria-current="page"]')?.textContent).toContain('Mi mercado');
    expect(fixture.nativeElement.querySelector('.basket-section') || fixture.nativeElement.textContent).toContain('Mi mercado');
  });

  it('redirects unknown route to home (wildcard fallback)', async () => {
    await router.navigateByUrl('/ruta-inexistente-123');
    flushAppBasics();
    await settle();

    expect(location.path()).toBe('');
    expect(fixture.componentInstance.page()).toBe('Inicio');
    expect(fixture.nativeElement.textContent).toContain('Todo empieza con una lista');
  });

  it('navigates forward and backward via Location history', async () => {
    await router.navigateByUrl('/buscar');
    flushAppBasics();
    await settle();
    expect(location.path()).toBe('/buscar');
    expect(fixture.componentInstance.page()).toBe('Buscar');

    await router.navigateByUrl('/fuentes');
    flushAppBasics();
    await settle();
    expect(location.path()).toBe('/fuentes');
    expect(fixture.componentInstance.page()).toBe('Fuentes');

    location.back();
    await fixture.whenStable();
    await new Promise(r => setTimeout(r, 50));
    flushAppBasics();
    await settle();
    expect(location.path()).toBe('/buscar');

    location.forward();
    flushAppBasics();
    await settle();
    expect(location.path()).toBe('/fuentes');
    expect(fixture.componentInstance.page()).toBe('Fuentes');
  });

  it('updates route and active nav when clicking sidebar buttons', async () => {
    await router.navigateByUrl('/');
    flushAppBasics();
    await settle();

    const buttons = Array.from(fixture.nativeElement.querySelectorAll('nav button')) as HTMLButtonElement[];
    const compButton = buttons.find(b => b.textContent?.includes('Comparar'))!;
    expect(compButton).toBeTruthy();
    compButton.click();
    flushAppBasics();
    await settle();

    expect(location.path()).toBe('/comparar');
    expect(fixture.componentInstance.page()).toBe('Comparar');
    expect(compButton.getAttribute('aria-current')).toBe('page');
  });
});
