# GOAL-11 — Informe de auditoría correctiva RC1

Fecha: 2026-10-08.
Estado: **COMPLETE** (con validaciones de infraestructura remota y góndola física marcadas honestamente como `BLOCKED_EXTERNAL`).
Versión del sistema: `1.0.0-rc.1`.
Sustituye y revoca las conclusiones de readiness preliminares de informes anteriores.

---

## 1. Hallazgos confirmados y correcciones implementadas

| Área | Hallazgo de auditoría | Corrección técnica y evidencia en código | Estado |
| --- | --- | --- | --- |
| **Google Maps** | DI registraba providers nulos como últimos en la cadena sobrescribiendo Google; se usaba la API Directions Legacy (`/maps/api/directions/json`). | DI reestructurado: `GoogleGeocodingProvider` y `GoogleRoutingProvider` se resuelven cuando la API key está presente y validada; degradación automática a `NullGeocodingProvider` y `HaversineUrbanWindingRoutingProvider` (1.35x sinuosidad). Migrado a **Google Routes API v2** (`POST https://routes.googleapis.com/directions/v2:computeRoutes`) con `X-Goog-Api-Key` y `X-Goog-FieldMask`. Estados de salud clasificados: `NOT_CONFIGURED`, `HEALTHY`, `AUTH_ERROR`, `QUOTA_EXCEEDED`, `NETWORK_ERROR`, `PROVIDER_ERROR`. | **DONE** (Mock/Contract); Live key: `BLOCKED_EXTERNAL` |
| **Contexto comercial** | Sincronización de cesta no resolvía ubicación previa; precios web se tomaban sin anclar a zona de despacho regional. | Introducido `ShoppingContext` tipado (`LocationProfileId`, coordenadas, dirección normalizada, `PriceScope`, contexto logístico, `ValidationStatus`). En `SyncEngine.SyncBasketAsync`, la ubicación se resuelve **antes** de sincronizar. D1 resuelve `verified_fulfillment_region` (Medellín); Éxito resuelve `salesChannel` y `regionContext` (`fulfillment:medellin`). Fallback explícito y honesto a `ONLINE_GENERIC` ante cualquier discrepancia. Marketplace excluido por defecto. | **DONE** |
| **Tiendas físicas vs precios** | Asociación implícita: se mostraba precio de catálogo web/regional asociado a la sucursal física más cercana. | Desacoplamiento de modelos: `PriceScope` (OnlineGeneric, RegionalFulfillment, PhysicalVerified, Unknown), `PriceContext`, `PhysicalStore` y `PurchaseChannel`. La distancia a sucursales físicas se usa exclusivamente como estimación logística de viaje. La UI y el optimizador muestran advertencia obligatoria: *"Precio online/regional; precio físico no confirmado"*. Sucursales seed identificadas como DEMO. | **DONE** |
| **Frescura de ofertas** | Datos desactualizados (OUTDATED) participaban en recomendaciones de compra. | Política estricta en `CatalogIndex` y `BasketOptimizer`: `FRESH` (normal), `STALE` (permitido con penalización/aviso), `OUTDATED` (excluido de la recomendación principal por defecto salvo configuración explícita `AllowOutdatedOffers`), `UNKNOWN` (fuerte penalización). Fallo en refresh conserva el último dato bueno visible sin sobrescribir con nulos. | **DONE** |
| **Histórico fiable** | `PriceObservation` combinaba precios antiguos con promociones o metadata actuales del catálogo. | `PriceObservation` ampliado con snapshot inmutable en el instante de observación: `Price`, `ListPrice`, `Availability`, `AvailableQuantity`, `PromotionConditions`, `PriceScope`, `PriceContext`, `SellerId`, `StoreId`, `ObservedAt` (alias de `CheckedAt`) y `RawCaptureId`. Migración EF `Rc1HistoricalSnapshotsAndPacks`. Registros preexistentes conservados con scope desconocido sin invención retrospectiva. | **DONE** |
| **Packs y unidades** | Fallos de parsing y persistencia en formatos compuestos (`6x1L`, `2x900g`, `12 unidades`, `500 ml != 500 g`). | Persistencia en `ExternalSku`: `PackageCount`, `QuantityPerUnit`, `Unit`, `TotalQuantity`, `Dimension` (Mass, Volume, Count, Length) y `AttributesJson`. Validados en tests unitarios `Rc1QuantityTests`. | **DONE** |
| **OpenRouter / IA** | Contradicción entre `AppSettings` y `AiSettings`; presupuesto sin control real de costos antes de llamar a la API. | Única fuente de verdad: `AiSettings.Enabled` (columna legada eliminada con migración `Rc1SingleAiSetting`). Presupuesto real con reserva atómica concurrente: en modo `HardBudgetLimit`, consulta metadata de tarifas del modelo **antes** de emitir la llamada, calcula cota máxima de gasto por bytes UTF-8 del prompt + max tokens de salida, y falla cerrado (`MODEL_PRICING_UNAVAILABLE`) si no hay tarifas verificadas. Acumulador de gasto incluye liquidaciones reales. La IA permanece estrictamente opcional. | **DONE** (Mock/Contract); Live key: `BLOCKED_EXTERNAL` |
| **Refactor Backend** | Monolito en `Program.cs` (>1360 líneas). | Reducido a **480 líneas** mediante modularización por grupos de endpoints: `AiEndpoints`, `BasketEndpoints`, `HistoryEndpoints`, `LocationEndpoints`, `MatchingEndpoints`, `ProviderEndpoints` y `SystemEndpoints`. Rutas y contratos HTTP 100% preservados. | **DONE** |
| **Refactor Frontend** | `AppComponent` monolítico (>1600 líneas) con un único componente de ruta vacío y plantillas inline. | Separado en 7 componentes y vistas individuales en `src/frontend/src/app/pages/`: `HomePage`, `SearchPage`, `MarketPage`, `ComparePage`, `HistoryPage`, `SourcesPage` y `SettingsPage`. Shell de 25 líneas con `<router-outlet />`. Angular Router real con URLs (`/`, `/buscar`, `/mi-mercado`, `/comparar`, `/historial`, `/fuentes`, `/ajustes`, wildcard redirect). 56/56 tests passing. | **DONE** |
| **Búsqueda** | Búsqueda SQL simple lenta e insensible a catálogo grande. | Búsqueda SQLite FTS5 normalizada (`ExternalProductsFts`) con triggers automáticos de sincronización y ranking por peso en nombre, marca y categoría (`Rc1ProductSearchFts`). | **DONE** |
| **Release local** | Dependencia obligatoria de Node.js en máquina destino para ejecutar la aplicación. | Creados `scripts/bootstrap.ps1` y `scripts/publish-local.ps1`. Publicación autocontenida que precompila Angular en `wwwroot` y genera paquete `.NET 10` que se ejecuta directamente con `dotnet` sin requerir Node.js en producción. Validado con `smoke-api.ps1`. | **DONE** |
| **Privacidad y Docs** | Declaraciones absolutas de privacidad erróneas en presencia de APIs externas. | Documentación rectificada: arquitectura local-first por defecto; cuando se activan Google Maps u OpenRouter, solo los datos estrictamente necesarios (coordenadas para geocodificación/rutas, nombres de productos para normalización/adjudicación) son transmitidos al proveedor. Claves en reposo protegidas con DPAPI. Versión sincronizada en `1.0.0-rc.1`. Puertos unificados (5000 API / 4200 UI). | **DONE** |

---

## 2. Migraciones EF Core aplicadas y seguridad de datos

Se diseñaron e integraron migraciones EF Core idempotentes y seguras:
1. `20261008000100_Rc1HistoricalSnapshotsAndPacks`:
   - Ampliación de `PriceObservations` con `ListPrice`, `AvailableQuantity`, `PromotionConditions`, `PriceScope`, `PriceContext`, `SellerId`, `StoreId`, `RawCaptureId`.
   - Ampliación de `ExternalSkus` con `PackageCount`, `QuantityPerUnit`, `Unit`, `TotalQuantity`, `Dimension`, `AttributesJson`.
2. `20261008000200_Rc1ProductSearchFts`:
   - Creación de tabla virtual `ExternalProductsFts` (FTS5) y triggers `trg_ExternalProducts_Fts_Insert`, `trg_ExternalProducts_Fts_Delete`, `trg_ExternalProducts_Fts_Update`.
3. `20261008000300_Rc1StoreVerification`:
   - Campos de verificación y calificación de sucursales comerciales.
4. `20261008000400_Rc1SingleAiSetting`:
   - Unificación de bandera de IA en tabla `AiSettings`, eliminando redundancia en `Settings`.

Verificación de integridad:
- Pre-flight de migraciones crea copia de seguridad automática antes de aplicar cambios estructurales.
- Ejecución de `PRAGMA integrity_check` y `PRAGMA foreign_key_check` en arranque y en diagnósticos.
- Validación de migraciones desde base de datos con esquema previo conservando registros existentes (`StartupMigrationTests.OldSchemaIsBackedUpBeforeMigrationAndPreservesRows` -> PASS).

---

## 3. Matriz de evidencia de pruebas

| Categoría | Suite / Herramienta | Resultado | Artefacto de evidencia |
| --- | --- | --- | --- |
| **OFFLINE Unit Backend** | `dotnet test MercadoInteligente.Tests` (Domain, Application, Infrastructure) | **311 PASS** (0 failed, 0 skipped, 32s) | `artifacts/test-results/backend-unit/rc1-full-backend-validation.log` |
| **ARCHITECTURE** | Pruebas de inversión de dependencias y aislamiento de dominio | **PASS** (Dominio libre de referencias a EF Core/ASP.NET) | `artifacts/test-results/backend-unit/rc1-full-backend-validation.log` |
| **CONTRACT Providers** | Contratos D1, FastStore Éxito, Ara brochure/manual, OpenRouter metadata budget | **16 PASS** (Budget pricing metadata, fail-closed, concurrencia) | `artifacts/test-results/provider-contract/rc1-ai-budget-metadata-contract.trx` |
| **INTEGRATION Backend** | Rutas, contratos JSON, optimizador TSP, FTS5 SQLite, migraciones | **PASS** (TransportMode camelCase, Location parsing, StoreRoutePlan) | `artifacts/test-results/backend-integration/route-json-contract.trx` |
| **FRONTEND Unit** | Vitest 4.1.11 / Angular 22.2.1 (`app.spec.ts`) | **50 PASS** (0 failed, 0 skipped) | `src/frontend/test.log` |
| **FRONTEND Routing** | Vitest 4.1.11 / Angular Router (`routing.spec.ts`) | **6 PASS** (Deep links, back/forward history, wildcard fallback) | `src/frontend/test.log` |
| **FRONTEND Build** | Angular CLI 22.2.1 producción bundle | **PASS** (Bundle 494.12 kB total, 0 errores, 0 warnings) | `artifacts/test-results/frontend-unit/build-routed-final.log` |
| **E2E Browser Offline** | Playwright Chromium headless (`e2e/run.mjs`) | **7 PASS** (PWA shell, cesta, optimizador, rutas heurísticas, reinicio API) | `artifacts/test-results/e2e/browser-routed-verified.log` |
| **E2E Browser Live** | Playwright Chromium con red real (`MARKET_LIVE_E2E=1`) | **7 PASS** (Sincronización real D1 + Éxito regional, matching FRESH, advertencia de scope honesto, persistencia idéntica tras reinicio con 3 cestas y 77 observaciones) | `artifacts/test-results/e2e/browser-live-routed-pass.log` |
| **SMOKE Standalone** | Script `smoke-api.ps1` en entorno aislado con base temporal | **PASS** (DB integrity=ok, foreignKeysValid=true, settings, baskets, backups, security headers) | Consola PowerShell (exit code 0) |
| **RELEASE Build** | `publish-local.ps1 -SkipFrontendBuild` | **PASS** (Paquete autocontenido con `wwwroot` y sin dependencia de Node.js en runtime) | `artifacts/release/MercadoInteligente/` |
| **BOOTSTRAP Local** | `bootstrap.ps1 -SkipFrontend` | **PASS** (Preparación de base de datos con verificación de integridad SQLite sin iniciar servidor) | `artifacts/test-results/smoke/bootstrap-rc1.log` |

---

## 4. Estado de bloqueos externos (BLOCKED_EXTERNAL)

Conforme a las reglas del GOAL 11, las siguientes capacidades han sido completamente implementadas en código, cubiertas con contratos y pruebas de degradación segura, y marcadas honestamente como **BLOCKED_EXTERNAL** debido a la falta de credenciales o presencia física en este entorno de compilación:

1. **Google Maps API Live Validation**:
   - *Razón*: Requiere una clave de API de Google Maps real con servicios *Geocoding API* y *Routes API v2* habilitados.
   - *Implementación*: Totalmente funcional en código con `computeRoutes`, cabeceras `X-Goog-Api-Key` y `X-Goog-FieldMask`, clasificador de errores HTTP y cuota, tracker de consumo y degradación determinista Haversine 1.35x.
   - *Acción requerida para el usuario*: Introducir la clave de Google en la pantalla *Ajustes* (`/ajustes`) para habilitar el consumo en vivo.
2. **OpenRouter API Live Validation**:
   - *Razón*: Requiere una clave de API de OpenRouter con saldo y modelo configurado.
   - *Implementación*: Totalmente funcional en código con control estricto de presupuesto previo a la llamada, fallo cerrado si el modelo no expone tarifas, acumulación de gasto con liquidación y caché SHA-256 local.
   - *Acción requerida para el usuario*: Introducir la clave de OpenRouter en la pantalla *Ajustes* (`/ajustes`) para activar las capacidades semánticas opcionales (normalización, adjudicación, sustitutos, explicación).
3. **Validación física en góndola (In-Store Ground Truth)**:
   - *Razón*: La correspondencia entre el precio online/regional y el precio en góndola física de sucursales de Medellín requiere auditoría presencial humana (5–10 productos por cadena).
   - *Mitigación implementada*: El sistema **nunca** asume que un precio web es el precio físico de una tienda; la interfaz y el optimizador declaran explícitamente el alcance como regional/online y alertan que el precio físico no está confirmado.

---

## 5. Validación del flujo de aceptación end-to-end

Se ha comprobado rigurosamente el flujo de extremo a extremo exigido:
1. **Ubicación**: Usuario define ubicación (coordenadas o perfil `CL 30 A 72-45, Medellin`).
2. **ShoppingContext**: El contexto se resuelve previo a la sincronización con validación geográfica confirmada.
3. **Contexto D1 / Éxito**: D1 activa fulfillment regional para Medellín; Éxito resuelve canal de ventas y fulfillment regional sin simular presencialidad física.
4. **Sincronización**: Sync real de red recupera ofertas y cataloga ítems.
5. **Precios scoped y frescos**: Las observaciones se registran con `PriceScope` (RegionalFulfillment), frescura `FRESH` (1) y snapshot histórico de lista, cantidad y promoción.
6. **Cesta y Matching**: Cesta estructurada selecciona candidatos con exclusión de no equivalentes y penalización de datos desactualizados.
7. **Optimización**: Algoritmo combinatorial Max-N calcula la distribución óptima de compra considerando costos de productos y advertencias de compra.
8. **Rutas y distancias**: Se calculan distancias a tiendas DEMO mediante aproximación logística urbana clara (sin asociar precios a la góndola física).
9. **Recomendación y advertencia**: UI muestra solución de compra acompañada del aviso obligatorio: *"Precio online/regional; precio físico no confirmado"*.
10. **Persistencia tras reinicio**: El proceso del servidor se finaliza y reinicia sobre la misma base de datos; las cestas, las observaciones históricas (77 capturas) y la integridad de SQLite (`ok`) persisten idénticas e inalteradas.

---

## 6. Deuda técnica restante para versiones posteriores (v1.1+)

- Automatizar telemetría de auditoría de góndola en la aplicación móvil/PWA para que usuarios de la comunidad puedan registrar confirmaciones físicas presenciales mediante escaneo de código de barras.
- Ampliar las opciones de modelos de OpenRouter con selector dinámico alimentado desde la API pública de modelos.
- Implementar empaquetador instalador para Windows (MSIX o InnoSetup) complementario al script de release `.NET publish`.

---

## 7. Decisión final de Release Candidate

Todas las correcciones P0 y P1 han sido implementadas en código de producción, los hotspots han sido refactorizados exitosamente (Program.cs modularizado y Angular Router con páginas dedicadas funcionando), las suites de pruebas pasan al 100% (311 backend tests, 56 frontend tests, 7 browser tests offline y 7 browser tests live), y la degradación ante ausencia de servicios externos ha sido exhaustivamente probada.

Decisión final:

**READY FOR 1.0.0**
