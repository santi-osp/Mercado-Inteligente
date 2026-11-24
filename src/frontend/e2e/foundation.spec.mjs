import { test, expect } from '@playwright/test';

test('foundation persists settings and basket, backs up, and displays honest sources', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByText('Base local activa')).toBeVisible();
  const nav = page.getByRole('navigation', { name: 'Navegación principal' });
  await nav.getByRole('button', { name: 'Ajustes' }).click();
  const stores = page.getByLabel('Máximo de tiendas');
  await expect(stores).toHaveValue('2');
  await stores.fill('3');
  await page.getByRole('button', { name: 'Guardar ajustes', exact: true }).click();
  await expect(page.getByText('Ajustes guardados.')).toBeVisible();
  await page.getByRole('button', { name: 'Crear copia de seguridad' }).click();
  await expect(page.getByText(/Copia creada:/)).toBeVisible();
  await page.reload();
  await nav.getByRole('button', { name: 'Ajustes' }).click();
  await expect(page.getByLabel('Máximo de tiendas')).toHaveValue('3');
  await nav.getByRole('button', { name: 'Mi mercado' }).click();
  await page.getByRole('button', { name: 'Crear lista' }).click();
  await page.getByRole('textbox', { name: 'Producto', exact: true }).fill('Arroz integral');
  await page.getByRole('spinbutton', { name: 'Cantidad' }).fill('2');
  await page.getByRole('button', { name: 'Agregar producto' }).click();
  await expect(page.getByText('Arroz integral')).toBeVisible();
  await page.reload();
  await nav.getByRole('button', { name: 'Mi mercado' }).click();
  await expect(page.getByText('Arroz integral')).toBeVisible();
  await page.getByRole('button', { name: 'Quitar Arroz integral' }).click();
  await expect(page.getByText('Esta lista está vacía. Agrega tu primer producto.')).toBeVisible();
  await nav.getByRole('button', { name: 'Buscar' }).click();
  await page.getByRole('searchbox', { name: 'Nombre o identificador externo' }).fill('producto inexistente');
  await page.getByRole('button', { name: 'Buscar', exact: true }).last().click();
  await expect(page.getByText(/No se encontraron productos locales/)).toBeVisible();
  await nav.getByRole('button', { name: 'Fuentes' }).click();
  await expect(page.getByRole('heading', { name: 'Fuentes registradas' })).toBeVisible();
});

test('D1 diagnosis is opt-in and its generic result is not a physical price', async ({ page }) => {
  let calls = 0;
  await page.route('**/api/v1/providers/d1/probe', route => {
    calls++;
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
      checkedAt: '2026-10-07T12:00:00Z', search: 'VALID', genericOnlinePrice: 'VALID_GENERIC_ONLINE'
    }) });
  });
  await page.goto('/');
  await page.getByRole('navigation', { name: 'Navegación principal' }).getByRole('button', { name: 'Fuentes' }).click();
  const diagnostic = page.getByRole('region', { name: 'Diagnóstico puntual D1' });
  await expect(diagnostic).toContainText('No comprueba precios regionales, góndola');
  expect(calls).toBe(0);
  await diagnostic.getByRole('button', { name: 'Comprobar D1 ahora' }).click();
  await expect(diagnostic).toContainText('Precio online genérico: VALID_GENERIC_ONLINE');
  expect(calls).toBe(1);
  await page.setViewportSize({ width: 360, height: 780 });
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});

test('mobile shell has no horizontal overflow and offers navigation', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 780 });
  await page.goto('/');
  await expect(page.getByRole('heading', { name: 'Compra mejor, con claridad.' })).toBeVisible();
  await page.getByRole('navigation', { name: 'Navegación principal' }).getByRole('button', { name: 'Mi mercado' }).click();
  await expect(page.getByRole('heading', { name: 'Mi mercado', exact: true })).toBeVisible();
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth)).toBe(true);
});

test('failed health request offers retry without claiming data loss', async ({ page }) => {
  await page.route('**/api/v1/system/health', route => route.abort());
  await page.goto('/');
  await expect(page.locator('.offline-banner')).toContainText('Los datos locales permanecen seguros');
  await expect(page.getByText('Base local activa')).toHaveCount(0);
  await page.unroute('**/api/v1/system/health');
  await page.getByRole('button', { name: 'Reintentar conexión' }).click();
  await expect(page.getByText('Base local activa')).toBeVisible();
  await expect(page.getByRole('button', { name: 'Reintentar conexión' })).toHaveCount(0);
});

test('D1 search shows saved online data and preserves it after a failed manual refresh', async ({ page }) => {
  let refreshCalls = 0;
  await page.route('**/api/v1/providers/d1/offers?**', route => route.fulfill({
    status: 200, contentType: 'application/json', body: JSON.stringify([{
      id: 'saved-product', productName: 'Arroz guardado', skuName: '500 G', ean: null,
      price: 1990, scope: 1, availability: 0, context: 'online-generic',
      checkedAt: new Date(Date.now() - 8 * 3600_000).toISOString(), sellerName: 'D1', sellerKind: 1
    }])
  }));
  await page.route('**/api/v1/providers/d1/refresh?**', route => {
    refreshCalls++;
    return route.fulfill({ status: 503, contentType: 'application/json', body: '{"code":"RATE_LIMITED"}' });
  });
  await page.goto('/');
  await page.getByRole('navigation', { name: 'Navegación principal' }).getByRole('button', { name: 'Buscar' }).click();
  await page.getByRole('searchbox', { name: 'Nombre o identificador externo' }).fill('arroz');
  await page.getByRole('button', { name: 'Buscar', exact: true }).last().click();
  const saved = page.getByRole('region', { name: 'Ofertas D1 guardadas' });
  await expect(saved).toContainText('Arroz guardado');
  await expect(saved).toContainText('Online genérico');
  await expect(saved).toContainText('Desconocida');
  await expect(saved).toContainText('Vencido · último dato conocido');
  expect(refreshCalls).toBe(0);
  await saved.getByRole('button', { name: 'Actualizar D1 manualmente' }).click();
  await expect(saved).toContainText('se conservaron');
  await expect(saved).toContainText('1990 COP');
  expect(refreshCalls).toBe(1);
});

test('location and basket persist while mocked regional sync and recommendation retain honest scope', async ({ page }) => {
  let syncLocation = '';
  let optimizedLocation = '';
  await page.route('**/api/v1/sync/basket?**', route => {
    syncLocation = new URL(route.request().url()).searchParams.get('locationInput') ?? '';
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
      basketId: 'mock', totalItems: 1, queriesExecuted: 1,
      results: [{ provider: 'D1', status: 'COMPLETED' }, { provider: 'EXITO', status: 'COMPLETED' }],
      shoppingContext: { validationStatus: 'LOCATION_RESOLVED', providers: [
        { provider: 'D1', key: 'online-generic', scope: 'OnlineGeneric', status: 'ONLINE_GENERIC_FALLBACK' },
        { provider: 'EXITO', key: 'fulfillment:region-med', scope: 'FulfillmentRegion', status: 'RESOLVED' }
      ] }
    }) });
  });
  await page.route('**/api/v1/baskets/*/optimize', route => {
    optimizedLocation = JSON.parse(route.request().postData() ?? '{}').locationInput;
    return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({
      strategy: 'Balanced', maxStoresAllowed: 2, storesUsedCount: 1, totalCost: 3200,
      baselineCost: 3200, totalSavingsVsBaseline: 0, totalItemsRequested: 1,
      itemsCoveredCount: 1, missingItemsCount: 0, stores: [],
      warnings: ['Precio regional no confirmado en tienda física'], routePlan: null
    }) });
  });
  await page.goto('/');
  const market = page.getByRole('navigation', { name: 'Navegación principal' }).getByRole('button', { name: 'Mi mercado' });
  await market.click();
  await page.getByRole('button', { name: 'Crear lista' }).click();
  await page.getByRole('textbox', { name: 'Producto', exact: true }).fill('Leche');
  await page.getByRole('button', { name: 'Agregar producto' }).click();
  await page.getByRole('textbox', { name: 'Dirección o coordenadas de partida' }).fill('6.2425, -75.5925');
  const basketId = await page.getByLabel('Lista activa').inputValue();
  await page.getByRole('button', { name: 'Establecer ubicación' }).click();
  await expect(page.getByText(/Ubicación establecida:/)).toBeVisible();
  await page.getByRole('button', { name: 'Sincronizar precios de la lista' }).click();
  await expect(page.getByText(/falló contexto regional/)).toBeVisible();
  expect(syncLocation).toBe('6.2425, -75.5925');
  await page.getByRole('button', { name: 'Optimizar cesta ahora' }).click();
  await expect(page.getByText('Precio online/regional; precio físico no confirmado.')).toBeVisible();
  await expect(page.getByText('Precio regional no confirmado en tienda física')).toBeVisible();
  expect(optimizedLocation).toBe(syncLocation);
  await page.reload();
  await market.click();
  await page.getByLabel('Lista activa').selectOption(basketId);
  await expect(page.getByText('Leche')).toBeVisible();
});
