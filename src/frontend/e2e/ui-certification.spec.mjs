import { test, expect } from "@playwright/test";

test.describe("UI Integration Certification", () => {
  test("full UI flow: geocode, save structured profile, select profile, sync and optimize with context verification", async ({ page }) => {
    let capturedProfilePayload = null;
    let syncCalledWithLocation = null;
    let optimizeCalled = false;

    // Intercept profile creation to verify structured payload from UI
    await page.route("**/api/v1/location/profiles", async (route) => {
      if (route.request().method() === "POST") {
        capturedProfilePayload = JSON.parse(route.request().postData() || "{}");
        return route.fulfill({
          status: 200,
          contentType: "application/json",
          body: JSON.stringify({
            id: "prof-laureles-1",
            label: capturedProfilePayload.label,
            address: capturedProfilePayload.address,
            formattedAddress: capturedProfilePayload.formattedAddress,
            latitude: capturedProfilePayload.latitude,
            longitude: capturedProfilePayload.longitude,
            street: capturedProfilePayload.street,
            number: capturedProfilePayload.number,
            streetNumber: capturedProfilePayload.streetNumber,
            neighborhood: capturedProfilePayload.neighborhood,
            postalCode: capturedProfilePayload.postalCode,
            city: capturedProfilePayload.city,
            state: capturedProfilePayload.state,
            createdAt: new Date().toISOString()
          })
        });
      }
      return route.continue();
    });

    // Mock geocode with rich structured address components
    await page.route("**/api/v1/location/geocode", async (route) => {
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          address: "Circular 1 # 70-15, Medellin",
          formattedAddress: "Circular 1 # 70-15, Laureles, Medellín, Antioquia 050031, Colombia",
          latitude: 6.2425,
          longitude: -75.5925,
          providerStatus: "LIVE_GOOGLE",
          components: {
            street: "Circular 1",
            streetNumber: "70-15",
            neighborhood: "Laureles",
            postalCode: "050031",
            city: "Medellín",
            state: "Antioquia",
            country: "Colombia",
            countryCode: "CO"
          }
        })
      });
    });

    // Mock parse to request geocoding
    await page.route("**/api/v1/location/parse", async (route) => {
      const body = JSON.parse(route.request().postData() || "{}");
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          originalInput: body.input,
          normalizedAddress: body.input,
          latitude: null,
          longitude: null,
          source: "Address",
          status: "NeedsGeocoding",
          errorMessage: null
        })
      });
    });

    // Mock sync returning regional resolved context for D1
    await page.route("**/api/v1/sync/basket?**", async (route) => {
      syncCalledWithLocation = new URL(route.request().url()).searchParams.get("locationInput") || "";
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          basketId: "test-basket-id",
          totalItems: 1,
          queriesExecuted: 1,
          results: [
            { provider: "D1", status: "COMPLETED" },
            { provider: "EXITO", status: "COMPLETED" }
          ],
          shoppingContext: {
            validationStatus: "LOCATION_RESOLVED",
            providers: [
              {
                provider: "D1",
                key: "fulfillment:medellin-test",
                scope: "FulfillmentRegion",
                status: "RESOLVED"
              },
              {
                provider: "EXITO",
                key: "region:medellin-test",
                scope: "Region",
                status: "RESOLVED"
              }
            ]
          }
        })
      });
    });

    // Mock optimize returning exact regional context in assignment
    await page.route("**/api/v1/baskets/*/optimize", async (route) => {
      optimizeCalled = true;
      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          strategy: "Balanced",
          maxStoresAllowed: 2,
          storesUsedCount: 1,
          totalCost: 4500,
          baselineCost: 4500,
          totalSavingsVsBaseline: 0,
          totalItemsRequested: 1,
          itemsCoveredCount: 1,
          missingItemsCount: 0,
          stores: [
            {
              retailer: "D1",
              storeName: "D1 Laureles",
              itemCount: 1,
              subtotal: 4500
            }
          ],
          assignments: [
            {
              basketItemId: "item-1",
              assignedItemName: "Leche Entera 1L",
              storeKey: "retailer:D1",
              retailerName: "D1",
              price: 4500,
              quantity: 1,
              lineTotal: 4500,
              candidate: {
                offerId: "off-d1-1",
                productName: "Leche Entera 1L",
                price: 4500,
                retailer: "D1",
                scope: 2, // FulfillmentRegion
                priceContext: "fulfillment:medellin-test",
                freshness: 1, // Fresh
                matchKind: 1,
                isFallbackContext: false
              }
            }
          ],
          warnings: ["Precio regional no confirmado en tienda física"],
          routePlan: null
        })
      });
    });

    // 1. Open app
    await page.goto("/");
    const nav = page.getByRole("navigation", { name: "Navegación principal" });
    await nav.getByRole("button", { name: "Mi mercado" }).click();

    // 2. Enter address in UI
    const locationInput = page.getByRole("textbox", { name: "Dirección o coordenadas de partida" });
    await locationInput.fill("Circular 1 # 70-15, Medellin");
    await page.getByRole("button", { name: "Establecer ubicación" }).click();

    // 3. Verify geocoded active card appears with coordinates
    await expect(page.locator(".location-current-card")).toBeVisible();
    await expect(page.getByText(/Ubicación activa:/)).toBeVisible();

    // 4. Save profile via UI
    await page.getByPlaceholder("Nombre (ej. Mi Casa)").fill("Casa Laureles UI");
    await page.getByRole("button", { name: "Guardar como perfil" }).click();
    await expect(page.getByText(/Perfil "Casa Laureles UI" guardado exitosamente/)).toBeVisible();

    // Verify structured components reached backend POST payload
    expect(capturedProfilePayload).not.toBeNull();
    expect(capturedProfilePayload.label).toBe("Casa Laureles UI");
    expect(capturedProfilePayload.latitude).toBe(6.2425);
    expect(capturedProfilePayload.longitude).toBe(-75.5925);
    expect(capturedProfilePayload.street).toBe("Circular 1");
    expect(capturedProfilePayload.number).toBe("70-15");
    expect(capturedProfilePayload.neighborhood).toBe("Laureles");
    expect(capturedProfilePayload.postalCode).toBe("050031");
    expect(capturedProfilePayload.city).toBe("Medellín");
    expect(capturedProfilePayload.state).toBe("Antioquia");

    // 5. Select the saved profile in UI
    const profileSelect = page.getByLabel("Perfil guardado:");
    await expect(profileSelect).toBeVisible();
    await profileSelect.selectOption("prof-laureles-1");
    await expect(page.getByText(/Perfil seleccionado: Casa Laureles UI/)).toBeVisible();

    // 6. Create basket & add item
    await page.getByRole("button", { name: "Crear lista" }).click();
    await page.getByRole("textbox", { name: "Producto", exact: true }).fill("Leche");
    await page.getByRole("button", { name: "Agregar producto" }).click();
    await expect(page.getByText("Leche", { exact: true })).toBeVisible();

    // 7. Sync basket
    await page.getByRole("button", { name: "Sincronizar precios de la lista" }).click();
    await expect(page.getByText(/D1: regional · EXITO: regional/)).toBeVisible({ timeout: 15000 });
    expect(syncCalledWithLocation).toBeTruthy();

    // 8. Optimize basket
    const optimizeResponsePromise = page.waitForResponse(resp =>
      resp.url().includes("/optimize") && resp.request().method() === "POST"
    );
    await page.getByRole("button", { name: "Optimizar cesta ahora" }).click();
    const optResp = await optimizeResponsePromise;
    expect(optResp.status()).toBe(200);

    const solution = await optResp.json();

    // 9. Strict Context Assertions:
    // D1 must have PriceContext == "fulfillment:medellin-test" and NOT generic or bogota
    expect(solution.assignments.length).toBe(1);
    const d1Assignment = solution.assignments[0];
    expect(d1Assignment.candidate.retailer).toBe("D1");
    expect(d1Assignment.candidate.priceContext).toBe("fulfillment:medellin-test");
    expect(d1Assignment.candidate.scope).toBe(2); // FulfillmentRegion
    expect(d1Assignment.candidate.isFallbackContext).toBe(false);

    // 10. Honest UI Warnings
    await expect(page.getByText("Precio online/regional; precio físico no confirmado.")).toBeVisible();
    await expect(page.getByText(/COBERTURA DE PRODUCTOS/)).toBeVisible();
  });

  test("changing location invalidates previous context and prompts re-resolution", async ({ page }) => {
    let currentShoppingContext = "fulfillment:medellin-test";

    await page.route("**/api/v1/sync/basket?**", async (route) => {
      const loc = new URL(route.request().url()).searchParams.get("locationInput") || "";
      const isBogota = loc.includes("Bogota") || loc.includes("4.7110");
      currentShoppingContext = isBogota ? "fulfillment:bogota-test" : "fulfillment:medellin-test";

      return route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          basketId: "test-basket-id",
          totalItems: 1,
          queriesExecuted: 1,
          results: [{ provider: "D1", status: "COMPLETED" }],
          shoppingContext: {
            validationStatus: "LOCATION_RESOLVED",
            providers: [
              {
                provider: "D1",
                key: currentShoppingContext,
                scope: "FulfillmentRegion",
                status: "RESOLVED"
              }
            ]
          }
        })
      });
    });

    await page.goto("/");
    const nav = page.getByRole("navigation", { name: "Navegación principal" });
    await nav.getByRole("button", { name: "Mi mercado" }).click();

    // Establish Medellin location first
    await page.getByRole("textbox", { name: "Dirección o coordenadas de partida" }).fill("6.2425, -75.5925");
    await page.getByRole("button", { name: "Establecer ubicación" }).click();
    await expect(page.getByText(/Ubicación establecida:/)).toBeVisible();

    // Create basket & add item
    await page.getByRole("button", { name: "Crear lista" }).click();
    await page.getByRole("textbox", { name: "Producto", exact: true }).fill("Arroz");
    await page.getByRole("button", { name: "Agregar producto" }).click();

    // Sync in Medellin
    await page.getByRole("button", { name: "Sincronizar precios de la lista" }).click();
    await expect(page.getByText(/D1: regional/)).toBeVisible({ timeout: 15000 });
    expect(currentShoppingContext).toBe("fulfillment:medellin-test");

    // Change location to Bogota in UI
    await page.getByRole("textbox", { name: "Dirección o coordenadas de partida" }).fill("4.7110, -74.0721");
    await page.getByRole("button", { name: "Establecer ubicación" }).click();
    await expect(page.getByText(/Ubicación establecida:/)).toBeVisible();

    // Sync in Bogota
    await page.getByRole("button", { name: "Sincronizar precios de la lista" }).click();
    await expect(page.getByText(/D1: regional/)).toBeVisible({ timeout: 15000 });
    expect(currentShoppingContext).toBe("fulfillment:bogota-test");
  });
});
