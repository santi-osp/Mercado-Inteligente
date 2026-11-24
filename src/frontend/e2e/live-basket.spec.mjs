import { test, expect } from '@playwright/test';

test('live regional basket from location to scoped recommendation and reload', async ({ page }) => {
  test.skip(process.env.MARKET_LIVE_E2E !== '1', 'Opt-in network test; requires public D1 and Éxito access.');
  test.setTimeout(120000);

  // Flow starts by opening the UI
  await page.goto('/');
  const market = page.getByRole('navigation', { name: 'Navegación principal' }).getByRole('button', { name: 'Mi mercado' });
  await market.click();

  // 1. Enter location in UI
  const locationInput = page.getByRole('textbox', { name: 'Dirección o coordenadas de partida' });
  await locationInput.fill('6.232177, -75.5932585');
  await page.getByRole('button', { name: 'Establecer ubicación' }).click();
  await expect(page.locator('.location-current-card')).toBeVisible();

  // 2. Complete structured fields in UI if required
  await page.getByPlaceholder('Nombre (ej. Mi Casa)').fill('Entrega Live UI');
  await page.getByRole('button', { name: 'Guardar como perfil' }).click();
  await expect(page.getByText(/Perfil "Entrega Live UI" guardado exitosamente/)).toBeVisible();

  // 3. Create active basket
  await page.getByRole('button', { name: 'Crear lista' }).click();
  await page.getByRole('textbox', { name: 'Producto', exact: true }).fill('leche');
  await page.getByRole('button', { name: 'Agregar producto' }).click();
  const basketId = await page.getByLabel('Lista activa').inputValue();

  // 4. Select the saved profile in UI
  await page.getByLabel('Perfil guardado:').selectOption({ label: /Entrega Live UI/ });
  await expect(page.getByText(/Perfil seleccionado: Entrega Live UI/)).toBeVisible();

  // 5. Synchronize basket
  await page.getByRole('button', { name: 'Sincronizar precios de la lista' }).click();
  await expect(page.getByText(/D1: regional · EXITO: regional/)).toBeVisible({ timeout: 90000 });

  // 6. Stores & Routing
  await page.getByRole('button', { name: 'Cargar tiendas Medellín' }).click();
  await page.locator('.store-list input[type="checkbox"]').first().check();
  await page.locator('.store-list input[type="checkbox"]').nth(1).check();
  await page.getByRole('combobox', { name: 'Modo de transporte:' }).selectOption('Walking');
  await page.getByRole('button', { name: 'Calcular ruta óptima' }).click();
  await expect(page.getByText('DISTANCIA TOTAL')).toBeVisible();
  await expect(page.getByText('TIEMPO ESTIMADO')).toBeVisible();
  await expect(page.getByText('Estimación heurística 1.35x').first()).toBeVisible();

  // 7. Optimize
  const optimized = page.waitForResponse(response => response.url().includes(`/api/v1/baskets/${basketId}/optimize`) && response.request().method() === 'POST');
  await page.getByRole('button', { name: 'Optimizar cesta ahora' }).click();
  const optimizedResponse = await optimized;
  expect(optimizedResponse.status()).toBe(200);
  const solution = await optimizedResponse.json();
  expect(solution.itemsCoveredCount).toBe(1);
  expect(solution.assignments.length).toBe(1);
  expect(solution.assignments[0].candidate.freshness).toBe(1);
  expect([1, 2, 3, 4]).toContain(solution.assignments[0].candidate.scope);
  expect(solution.assignments[0].candidate.matchKind).not.toBe(3);

  // 8. Honest warnings & UI display
  await expect(page.getByText('Precio online/regional; precio físico no confirmado.')).toBeVisible();
  await expect(page.getByText(/COBERTURA DE PRODUCTOS/)).toBeVisible();

  // 9. Reload & verify persistence
  await page.reload();
  await market.click();
  await page.getByLabel('Lista activa').selectOption(basketId);
  await expect(page.getByText('leche', { exact: true })).toBeVisible();
});
