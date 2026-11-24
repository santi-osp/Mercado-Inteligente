# Project State — Mercado Inteligente

Última actualización: 2026-10-10.
Estado global: **COMPLETE** (Versión 1.0.0 Oficial Certificada).
Goal actual: **GOAL-15 COMPLETE** (PRODUCT DISCOVERY, CATÁLOGO ECOMMERCE, CRUD DE LISTAS, ASISTENTE IA Y UX/UI FINAL).
Los quince goals (GOAL-01 a GOAL-15) han sido completamente recorridos, desarrollados, auditados y certificados con suites de pruebas reales.
Decisión de Release: **READY FOR 1.0.0**.

## Estado por goal

| Goal | Estado | Evidencia / impedimento técnico pendiente |
|---|---|---|
| 01 Fundación | COMPLETE | Build backend/frontend PASS; backend 28/28, Vitest 17/17, Playwright 3/3, smoke API aislado PASS; DB temporal/migrations y backup. Riesgo tooling npm acotado en ADR 0003. |
| 02 D1 | COMPLETE | Adaptador D1, contratos offline 143/143 PASS, smoke regional opt-in live 1/1 PASS, backups portables .zip con restore verificado y fail-closed, métricas locales, PriceScope estricto y documentación cerrada. |
| 03 Éxito | COMPLETE | Adaptador FastStore SearchQuery, mapper seguro de sellers (RetailerDirect, Marketplace, InternalFulfillment), cotización regional Medellín, preservación de último dato bueno, métricas SyncRun, 172/172 backend PASS, 28/28 Vitest PASS, build limpia y documentación en docs/providers/exito.md y docs/development/goal-03-exito-report.md. |
| 04 Ciclo de datos | COMPLETE | FreshnessService con TTLs por dominio, delta compression en PriceObservation, detección de anomalías de precios, ciclo de vida de productos externos (Active/Missing/Inactive), mantenimiento GFS (7 diarios, 4 semanales, 3 mensuales), depuración de RAW/logs, PRAGMA optimize/FK check, telemetría y UI en Angular PWA; 181/181 backend PASS, 32/32 frontend PASS. |
| 05 Matching | COMPLETE | Quantity/multipack parser estricto, normalización retail colombiano, pipeline 6 etapas con prevalencia de overrides, comparador $/unidad base, sobrecompra, dataset evaluación 16 pares (0 FP exact, 100% precision), endpoints HTTP y pantalla 'Comparar' en Angular PWA; 194/194 backend PASS, 35/35 frontend PASS, docs en matching-evaluation.md y goal-05-matching-report.md. |
| 06 Optimizador | COMPLETE | Cesta avanzada, candidate generation con CatalogIndex y tokens de baja frecuencia, evaluación de promociones (2x1, 3x2, % en 2da unidad, advertencias de medios de pago), optimizador multi-tienda Max-N con scoring combinatorio zero-allocation (ScoreSubset y PopCount), 4 estrategias (Max ahorro, Single store baseline, Balanceada con penalidad, Menor complejidad), evaluación presupuestaria y sugerencia N+1; 205/205 backend PASS, 35/35 frontend PASS, scale benchmark 100 ítems en 10 tiendas validado bajo 500ms; documentación en docs/development/goal-06-optimizer-report.md. |
| 07 Google | COMPLETE_WITH_EXTERNAL_BLOCKERS | LocationInputParser multi-formato, LocationProfileService persistente, GoogleGeocodingProvider y GoogleRoutingProvider con DPAPI, Routes API v2, clasificación de errores de cuota/auth, fallback offline Haversine 1.35x, optimizador TSP exacto, URLs públicas Zero-Key, UI con tramos y monitor de uso. Bloqueo externo: clave real de Google Maps para consumo en vivo (`BLOCKED_EXTERNAL`). |
| 08 OpenRouter | COMPLETE_WITH_EXTERNAL_BLOCKERS | IA opcional. Invarianza matemática. Almacenamiento DPAPI. Hard budget limit con consulta previa de tarifas de modelo, cálculo de cota de tokens por bytes UTF-8 + max tokens, fallo cerrado (`MODEL_PRICING_UNAVAILABLE`), acumulador con liquidación, y caché criptográfico SHA-256. Bloqueo externo: clave real de OpenRouter para consumo en vivo (`BLOCKED_EXTERNAL`). |
| 09 Ara | COMPLETE | AraProvider honesto con capacidades parciales explícitas (folletos públicos y entradas manuales, sin scraping no permitido). Ingesta estructurada de folletos (AraBrochureIngest) con cota 2MB, vigencia y auditoría RawStore. Servicio de aportes físicos comunitarios (AraManualOfferService) con trazabilidad USER_MANUAL. Registro desacoplado de proveedores (IStoreProviderRegistry) y UI capability-aware. |
| 10 Release | COMPLETE | Hardening integral (loopback, Host/Origin filters, sanitización XSS, DPAPI), hosting estático SPA unificado en ASP.NET Core (`UiHosting.FindBuiltUi`), suite completa de flujos E2E de ciclo de vida (`E2ELifecycleTests.cs`), scripts PowerShell de release (`run-app.ps1`, `build-all.ps1`, `test-all.ps1`). |
| 11 RC1 Auditoría | COMPLETE | Corrección P0/P1 integral: Google DI / Routes API v2 (`computeRoutes`); ShoppingContext integrado con D1/Éxito fulfillment regional y fallback honesto `ONLINE_GENERIC`; desacoplamiento estricto de precios y tiendas físicas con advertencia *"Precio online/regional; precio físico no confirmado"*; política de frescura (FRESH, STALE, OUTDATED, UNKNOWN); snapshots inmutables en `PriceObservation` (`Rc1HistoricalSnapshotsAndPacks`); packs/unidades persistidos en `ExternalSku`; IA con única fuente de verdad (`AiSettings.Enabled`) y hard budget limit verificado; Program.cs modularizado en 7 grupos de endpoints (480 líneas); Angular refactorizado en 7 componentes/páginas con Angular Router real (56/56 tests passing); búsqueda FTS5 SQLite normalizada con ranking; release local autocontenido sin Node.js en runtime (`publish-local.ps1`); E2E Browser offline (7 PASS) y LIVE con red real (7 PASS) con reinicio de API y persistencia verificada. |
| 12 RC2 Context & Ubicación | COMPLETE | Context integrity integral: IOfferContextPolicy y EffectiveProviderContext; el optimizador jamás elige ofertas de otro contexto comercial aunque sean más baratas (7/7 tests críticos en Goal12ContextAndRegressionTests PASS); LocationProfile estructurado con address_components en UI y backend; TravelLocation desacoplado de StoreId y PriceScope; Google Routes v2 con TWO_WHEELER y fallback a DRIVE informado; exportación CSV histórica desde snapshots inmutables PriceObservation sin mutación por cambios en Offer viva; extracción de Angular frontend a 4 facades dedicadas (LocationFacade, BasketFacade, ProviderStatusFacade, SettingsFacade); publicación Windows nativa y auto-contenida sin Node.js (publish-windows.ps1); paquete de evidencias completo (release-evidence/). |
| 13 Certificación Final | COMPLETE | Certificación final e integración total: D1 context garantizado sync -> optimize sin pérdida de sesión; integración plena de UI con LocationFacade y componentes de dirección estructurados; resolución de TravelLocation antes de scoring Balanced; eliminación de falsos positivos en tests live (Assert.Skip explícito); suite Playwright con flujo real de UI (8 PASS, 1 SKIPPED live smoke); versión unificada 1.0.0-rc.3 en todo el stack; publish Windows self-contained verificado con smoke test; informe GOAL-13 emitido. Decisión: **READY FOR 1.0.0**. |
| 14 Final v1.0.0 & Local Launch | COMPLETE | Google Maps 100% opcional (cálculo Haversine local 1.35x, extracción de coordenadas y enlaces de navegación pública zero-key); TravelTrust en optimizador Balanced con AllowUnverifiedLocationsForTravel; OmniRouter como proveedor de IA predeterminado (gemini-3.8-flash) con cifrado DPAPI; lanzador local Windows en segundo plano (launch-mercado-inteligente.ps1, stop-mercado-inteligente.ps1, Mercado Inteligente.lnk, comandos .cmd); publicación self-contained win-x64 libre de dependencias; 325/325 backend PASS, 57/57 frontend PASS; paquetes duales v1.0.0 y manifest validado al 100%. Decisión: **READY FOR 1.0.0**. |
| 15 Descubrimiento y Asistente | COMPLETE | Búsqueda y catálogo ecommerce 100% independientes de listas; ProductSearchOrchestrator con FTS5 local y sincronización concurrente con D1 y Éxito; persistencia de ProductUrl e ImageUrl (migración Goal15ProductDiscoveryCatalog); CRUD completo de listas (Crear, Editar, Duplicar, Eliminar con protección de integridad relacional del catálogo); Asistente IA Flotante (FAB) contextual en Buscar, Mi mercado y Comparar con OmniRouter / Gemini Flash 3.8 y enlaces directos a tiendas; interfaz pulida con mini-heroes en español, filtros reactivos, vista grid/lista, skeletons y toasts; 329/329 backend PASS (4 tests dedicados en Goal15DiscoveryAndListsTests), 57/57 Vitest PASS; standalone Windows publish verificado. Decisión: **READY FOR 1.0.0**. |
| 16 Gestión de Datos, Analítica y Retailers | COMPLETE | OmniRouter 100% configurado y verificado en vivo con modelo antigravity/gemini-3.8-flash-tiered en puerto 20128; clave asegurada con Windows DPAPI; Módulo de Análisis (/analisis) con 3 pestañas operativas (Oportunidades, Producto, Listas); Dashboard de BD local con estadísticas por tienda y optimización rápida; Gestión completa de copias de seguridad (.db y .zip con RAW, etiquetas, verificación y apertura en explorador); Zona de Peligro protegida con auto-backup y confirmación "BORRAR"; Expansión a 8 cadenas (D1, Éxito, Ara, La Vaquita, Mundo Huevo, Olímpica, Jumbo, PriceSmart); 350/350 backend PASS, 58/58 frontend PASS; informe GOAL-16 emitido. Decisión: **READY FOR 1.0.0**. |
| 17 Rediseño UX/UI | COMPLETE | Matriz de discovery en GOAL-17-SKILLS-MATRIX.md; benchmark de referentes en GOAL-17-UX-REFERENCES.md; tokens centralizados en styles.css; componentes rediseñados y simplificados; 364/364 backend PASS, 58/58 frontend PASS; informe GOAL-17 emitido. |
| 18 UX Completion & Identidad | COMPLETE | Favicon e iconografía de marca propia en SVG/ICO/PNG; 53/53 capturas visuales (0 duplicados SHA-256); tabs de Ajustes aisladas y deep-linkables; erradicación del AI-look; métricas internas F1/Recall colapsadas; 364/364 backend PASS, 58/58 frontend PASS; informe GOAL-18 emitido. |
| 18.1 Integridad de Búsqueda | COMPLETE | Paginación exacta con totalCount/totalPages/hasPrevious/hasNext; precios unitarios estandarizados $20.000/kg, $5.000/L, $1.500/unidad y modo WEIGHT_BASED; normalización de 500g sin mostrar 0.5g; protección de scraping con localOnly: true en paginación/filtros; 376/376 backend PASS, 63/63 frontend PASS; informe GOAL-18.1 emitido. |
| 19 Transformación Visual | COMPLETE | Integración de assets oficiales de Mercado_Inteligente_Visual_Identity_Pack_v1.zip (logos, marks, favicons, PWA, ilustraciones funcionales sin cliché); UI Kit en /dev/ui-kit con 17 secciones; microinteracciones con CSS/GSAP contextual y prefers-reduced-motion; locale es-CO registrado; 376/376 backend PASS, 66/66 frontend PASS; 53/53 capturas visuales en docs/ui-audit/goal19/ y Mercado_Inteligente_UI_Audit_Goal19.zip; informe GOAL-19 emitido. Decisión: **READY FOR PRODUCTION**. |

## Matriz de pruebas verificadas

- **Backend Unit / Integration**: 376 passed, 0 failed, 3 skipped (`dotnet test tests/MercadoInteligente.Tests/MercadoInteligente.Tests.csproj`).
- **Goal 15 Discovery & Lists Tests**: 4 passed, 0 failed (`Goal15DiscoveryAndListsTests.cs`).
- **Critical Context & Regression Tests**: 7 passed, 0 failed (`Goal12ContextAndRegressionTests.cs`).
- **Goal 13 Integration Certification Tests**: 3 passed, 0 failed (`Goal13IntegrationCertificationTests.cs`).
- **Frontend Unit / Routing**: 66 passed, 0 failed (Vitest 4.1.11 en `src/frontend`).
- **Browser E2E (Playwright)**: 8 passed, 0 failed, 1 skipped live smoke (`release-evidence/playwright/results.json`).
- **Frontend Build**: Production bundle generado en `dist/mercado-inteligente/browser` y alojado en `wwwroot/`.
- **E2E Lifecycle Tests**: 13 passed, 0 failed (`E2ELifecycleTests.cs`).
- **Smoke Standalone Publish**: `release/MercadoInteligente/MercadoInteligente.exe` probado en puerto aislado 5055 con 5/5 validaciones PASS (Health, Status, UI SPA, SQLite db, clean shutdown).
- **SQLite Database Integrity**: `PRAGMA integrity_check` -> ok; `PRAGMA foreign_key_check` -> 0 violaciones.
- **Release Evidence Package**: 12 artefactos completos en `release-evidence/` (.trx, .txt, .json, .md).

## Bloqueos externos (BLOCKED_EXTERNAL)

1. **Google Maps Live API Key**: La aplicación funciona al 100% con fallback offline Haversine 1.35x y URLs zero-key públicas. Para habilitar Google Routes v2 en vivo, el usuario debe introducir una API key en Ajustes (`/ajustes`).
2. **OpenRouter Live API Key**: La aplicación funciona al 100% de manera determinista local sin IA. Para activar capacidades semánticas opcionales con presupuesto estricto, el usuario debe introducir una API key en Ajustes (`/ajustes`).
3. **Validación física en góndola**: La correlación de precios web con góndolas físicas de sucursales en Medellín requiere inspección presencial humana. La aplicación nunca presume que un precio web es físico y advierte explícitamente al usuario.

## Addendum al Goal 16 — Auditoría Visual UI/UX & Capturas

- **Estado**: COMPLETE
- **Ejecución**: Script `scripts/capture-ui-audit.mjs` ejecutado vía Playwright Chromium contra la compilación de producción del SPA Angular.
- **Resultados de Auditoría**:
  - 53 capturas completas generadas en `docs/ui-audit/current/` (todas con tamaño > 0 bytes).
  - Cobertura: Desktop (`1440x1000`) y Mobile (`390x844`) para las 9 rutas activas: `/`, `/buscar`, `/mi-mercado`, `/comparar`, `/analisis`, `/historial`, `/tiendas`, `/ajustes` y `/dev/ui-kit`.
  - Estados evaluados: Initial, Typing, Loading (skeletons), Populated, Filters, List/Grid views, Modales (crear, renombrar, eliminar, detalle), Provider Warnings, Empty States, Controlled Errors, Chatbot Drawer (abierto y con respuesta estructurada), Optimizer Solutions, Dashboard y Component Gallery.
  - Metadatos exportados: `MANIFEST.md`, `ROUTES.txt`, `VERSION.txt` (1.0.0-rc.3), `NOTES.md`.
- **Paquete para ChatGPT Web**: `Mercado_Inteligente_UI_Audit.zip` generado con 57 archivos limpios (sin secretos ni código fuente).


## Addendum al Goal 19 — Transformación Visual Integral, Identidad y Microinteracciones

- **Estado**: COMPLETE
- **Ejecución**: Integración sistemática de `Mercado_Inteligente_Visual_Identity_Pack_v1.zip`, rediseño de vistas de consumo (Grocery + Fintech + Productivity), living styleguide en `/dev/ui-kit` y script `scripts/capture-ui-audit.mjs` ejecutado vía Playwright Chromium con target `goal19`.
- **Resultados de Auditoría**:
  - 53 capturas completas generadas en `docs/ui-audit/goal19/` (todas con tamaño > 0 bytes y 0 duplicados SHA-256).
  - Cobertura: Desktop (`1440x1000`) y Mobile (`390x844`) para las 9 rutas activas.
  - UI Kit de 17 secciones completamente operativo en `/dev/ui-kit`.
  - Locale `es-CO` registrado para formato monetario y precios unitarios.
  - Microinteracciones refinadas y cumplimiento total de `prefers-reduced-motion`.
- **Paquete de Evidencia**: `Mercado_Inteligente_UI_Audit_Goal19.zip` generado con 57 archivos limpios (11.99 MB).
- **Informe de Cierre**: `docs/development/GOAL-19-VISUAL-IDENTITY-MOTION-REPORT.md`.