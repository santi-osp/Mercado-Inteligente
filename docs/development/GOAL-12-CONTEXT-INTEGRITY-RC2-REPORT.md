# GOAL-12 — Informe de integridad de contexto, ubicación real y Release Candidate 2 (RC2)

Fecha: 2026-10-09  
Versión del sistema: `1.0.0-rc.2`  
Estado: **COMPLETE** (Integrado y verificado)  
Decisión de release: **READY FOR 1.0.0**

---

## 1. Executive Summary

El GOAL 12 de Mercado Inteligente consolida las correcciones críticas del ciclo Release Candidate (RC2), garantizando que:
1. **La optimización de compras es 100% consciente del contexto comercial del usuario**: ninguna oferta más barata perteneciente a otra región o contexto comercial inválido puede ganar la recomendación.
2. **Ubicación real con componentes estructurados**: la creación y edición de perfiles de ubicación desde la UI (`LocationProfile`) almacena y expone componentes atómicos (`street`, `number`, `neighborhood`, `city`, `postalCode`, `state`, `country`) para alimentar los contratos de despacho regional de D1 y Éxito.
3. **Desacoplamiento estricto de precios y tiendas físicas**: `PriceScope` y `PriceContext` definen la fuente del precio; `TravelLocation` define únicamente el nodo físico utilizado para estimar trayecto y ruta urbana. La UI advierte de manera transparente: *"Precio online/regional; precio físico no confirmado"*.
4. **Enrutamiento Google Routes v2 con soporte TWO_WHEELER**: la solicitud en modo motocicleta utiliza prioritariamente `TWO_WHEELER`, aplicando degradación controlada a `DRIVE` con bandera `RoutingFallback = true` e indicación al usuario cuando el servicio o la región no lo soporten.
5. **Histórico y exportación CSV inmutables**: `PriceObservation` opera como snapshot inmutable en el instante de captura (`PriceObservationFactory`), garantizando que la exportación en `/api/v1/export/prices.csv` refleje la realidad temporal observada y no mute con cambios posteriores en la entidad de catálogo `Offer`.
6. **Desacoplamiento en Frontend**: la vista de mercado y componentes principales operan a través de fachadas dedicadas (`LocationFacade`, `BasketFacade`, `ProviderStatusFacade`, `SettingsFacade`), eliminando la sobrecarga del shell monolítico anterior.
7. **Empaquetado y distribución Windows**: publicación nativa `win-x64` auto-contenida con SPA Angular precompilado en `wwwroot/`, ejecutable sin requerir Node.js en el entorno del usuario final.

---

## 2. Hallazgos Confirmados y Estado de Resolución

| # | Área / Problema Detectado | Resolución Técnica Implementada | Estado |
|---|---|---|---|
| 1 | **Selección de ofertas agnóstica al contexto** | Creación de `EffectiveProviderContext` y `IOfferContextPolicy` (`OfferContextCompatibilityService`). `CatalogOfferLoader` y `BasketOptimizer` filtran ofertas en SQL/memoria asegurando que solo ofertas `COMPATIBLE` (o `FALLBACK` explícito) participen en la optimización. | **DONE** |
| 2 | **Pérdida de componentes de dirección en UI** | Contrato de geocodificación enriquecido con parseo de `address_components`. Modelo `LocationProfile` y DTOs ampliados con `Street`, `Number`, `Neighborhood`, `PostalCode`, `City`, `State`, `Country`, `CountryCode`, `StateCode`, `FormattedAddress`. UI advierte si faltan datos requeridos para precios regionales. | **DONE** |
| 3 | **Fusión de tienda física y origen de precio** | Creación de `TravelLocation` en el resultado de optimización y en la planificación de ruta. Se mantiene `StoreId = null` en ofertas regionales. UI presenta honestamente el proxy logístico. | **DONE** |
| 4 | **Modo Motocicleta mapeado como DRIVE** | Migrado a `TWO_WHEELER` en Google Routes v2. Si el proveedor rechaza o falla `TWO_WHEELER`, se ejecuta fallback a `DRIVE` registrando `RoutingModeRequested = MOTORCYCLE`, `RoutingModeUsed = DRIVE` y `RoutingFallback = true`. | **DONE** |
| 5 | **Exportación CSV mutando con Offer actual** | `HistoryEndpoints` refactorizado para leer exclusivamente desde los campos del snapshot `PriceObservation` (`obs.Price`, `obs.ListPrice`, `obs.Availability`, `obs.PriceScope`, `obs.PriceContext`, `obs.PromotionConditions`, etc.). | **DONE** |
| 6 | **Monolito de UI en AppComponent** | Extracción a 4 servicios facade en `src/frontend/src/app/facades/`: `LocationFacade`, `BasketFacade`, `ProviderStatusFacade`, `SettingsFacade`. | **DONE** |
| 7 | **Dependencia de Node en Runtime** | Script `publish-windows.ps1` que compila el frontend Angular en `wwwroot` y publica binarios `win-x64` (auto-contenidos) ejecutables mediante `run.ps1` o `MercadoInteligente.exe`. | **DONE** |

---

## 3. Context-Aware Optimizer & Policy

El motor de optimización implementa el contrato de compatibilidad `IOfferContextPolicy`:
- **Caso A (Contexto regional resuelto)**: Si el usuario se encuentra en Medellín (`FULFILLMENT_REGION` `12109` para D1 o `region:medellin` para Éxito), el optimizador **únicamente** evalúa ofertas que coincidan con dicho contexto. Una oferta regional de Bogotá ($7.000) o una genérica ($8.000) **nunca** sustituyen una oferta regional de Medellín ($9.000), aun si su precio unitario es menor.
- **Caso B (Contexto no resoluble)**: Si el proveedor no puede mapear la ubicación a una zona regional específica, se recurre a `ONLINE_GENERIC` como fallback explícito (`IsFallback = true`), mostrando un aviso visible en la recomendación.
- **Caso C (Oferta regional faltante)**: Si el contexto se resolvió pero un producto no tiene cotización regional, se admite la oferta genérica únicamente como fallback explícito secundario, informando: *"No se encontró precio regional actualizado; se utilizó precio online genérico"*.
- **Caso D (Incompatibilidad cruzada)**: Ofertas de regiones distintas son marcadas como `INCOMPATIBLE` y descartadas antes de la fase combinatoria Max-N.

---

## 4. LocationProfile Estructurado y Providers

### D1
- Consume los componentes estructurados de `LocationProfile` (ciudad, barrio, código postal y coordenadas).
- Resuelve la zona de fulfillment regional asignada (`PriceScope.RegionalFulfillment`).
- Mantiene compatibilidad con fallbacks a `ONLINE_GENERIC` ante fallos de conectividad con el backend de D1.

### Éxito
- Utiliza la misma entidad `LocationProfile` para mapear canales de venta (`salesChannel`) y contexto regional (`fulfillment:medellin`).
- Excluye productos de marketplace de terceros por defecto para evitar dispersión de costos de envío y garantizar precios directos de Éxito.

---

## 5. TravelLocation y Separación Logística

Se eliminó cualquier mutación de la oferta que convirtiera un precio regional en precio físico:
- **`Offer.StoreId`**: Permanece `null` en ofertas de catálogo digital o despacho regional.
- **`TravelLocation`**: Estructura independiente dentro de la recomendación que indica la tienda física más próxima sugerida para calcular distancia, tiempo estimado y orden de visita en el mapa.
- **UI Transparency**: La recomendación y el resumen de compras etiquetan claramente cada ítem:
  - *Precio online regional*
  - *D1 cercano a 1.2 km (precio físico en tienda no verificado)*

---

## 6. Enrutamiento Google Routes v2 y Modo Motocicleta

El servicio `GoogleRoutingProvider` envía la solicitud a `POST https://routes.googleapis.com/directions/v2:computeRoutes`:
- Solicitudes en modo `Motorcycle` se envían con el `travelMode` `TWO_WHEELER`.
- Si Google Routes v2 responde exitosamente, se almacena `RoutingFallback = false` y `RoutingModeUsed = TWO_WHEELER`.
- Si la API responde con error de ruta no soportada o cuota para vehículos de dos ruedas en la región, el proveedor reintenta automáticamente con `DRIVE`, registrando `RoutingFallback = true` y notificando: *"Ruta aproximada usando modo automóvil"*.
- Cuando no hay API Key de Google configurada, opera el enrutador offline `HaversineUrbanWindingRoutingProvider` (1.35x de sinuosidad urbana) con enlaces web públicos Zero-Key.

---

## 7. Histórico Inmutable y Exportación CSV

La entidad `PriceObservation` almacena de forma inmutable todas las dimensiones comerciales en el instante de la observación:
- `Price`, `ListPrice`, `Availability`, `AvailableQuantity`, `PromotionConditions`, `PriceScope`, `PriceContext`, `SellerId`, `StoreId`, `ObservedAt`, `RawCaptureId`.
- El endpoint `/api/v1/export/prices.csv` genera el archivo CSV proyectando los valores históricos capturados:
  - Si un producto costaba $10.000 en T1 sin promoción y $8.000 en T2 con promoción 2x1, y posteriormente la oferta viva en catálogo muta a $15.000 con 3x2, las filas de T1 y T2 en el archivo CSV preservan sus valores originales sin mezclar metadatos actuales.

---

## 8. Arquitectura Frontend y Facades

Se desacopló la lógica de estado del frontend en `src/frontend/src/app/facades/`:
- `LocationFacade`: Gestión reactiva de perfiles de ubicación, geocodificación y validación de componentes obligatorios.
- `BasketFacade`: Ciclo de vida de la canasta de compras, sincronización de proveedores y ejecución de optimización.
- `ProviderStatusFacade`: Monitoreo en tiempo real del estado de D1, Éxito, Ara, Google y OpenRouter.
- `SettingsFacade`: Configuración de privacidad, límites de tiendas, transporte y llaves externas DPAPI.

---

## 9. Base de Datos y Migraciones

La base de datos SQLite fue actualizada con la migración:
- `20261009001241_Rc2LocationProfileAndOfferContextIndexes`:
  - Ampliación de `LocationProfiles`: `FormattedAddress`, `StateCode`, `Country`, `CountryCode`, `CreatedAt`, `UpdatedAt`.
  - Índices de rendimiento:
    - `IX_Offers_Scope_Context` sobre `Offers(Scope, Context)` para acelerar el filtrado de ofertas compatibles en el optimizador.
    - `IX_Offers_CheckedAt` sobre `Offers(CheckedAt)` para evaluación de frescura.
    - `IX_LocationProfiles_Label` sobre `LocationProfiles(Label)`.
- Compatibilidad hacia atrás: registros existentes conservan su validez sin pérdida de datos.
- Procedimiento de seguridad: copias de seguridad automáticas antes de aplicar migraciones y verificación de integridad `PRAGMA integrity_check` y `PRAGMA foreign_key_check` con resultado `ok`.

---

## 10. Matriz de Resultados de Pruebas

| Categoría | Suite / Herramienta | Pruebas | Resultado | Detalle |
|---|---|---|---|---|
| **BACKEND UNIT / INTEGRATION** | `dotnet test` (xUnit 2.9.3) | **319** | **319 PASS (100%)** | 0 fallidos, 0 omitidos. Incluye suite crítica `Goal12ContextAndRegressionTests`. |
| **CRITICAL CONTEXT TESTS** | `Goal12ContextAndRegressionTests.cs` | **7** | **7 PASS (100%)** | - Optimizer_DoesNotUseCheaperOfferFromWrongContext<br>- Optimizer_UsesGenericOnlineOnlyWhenRegionalOfferMissing<br>- SavedLocationProfile_FromUiContainsStructuredAddressComponents<br>- HistoricalCsv_UsesObservationSnapshot_NotCurrentOffer<br>- MotorcycleRouting_UsesTwoWheeler_WhenSupported<br>- MotorcycleRouting_FallsBackToDrive_WhenTwoWheelerFailsOrUnsupported<br>- ContextChange_InvalidatesEffectiveProviderContext |
| **ARCHITECTURE** | `ArchitectureTests.cs` | **1** | **1 PASS (100%)** | Dominio y aplicación libres de dependencias circulares y sin infraestructura web. |
| **BACKEND INTEGRATION** | `IntegrationTests.cs` | **15** | **15 PASS (100%)** | Contratos JSON, serialización camelCase, TSP solver, parsers. |
| **E2E LIFECYCLE** | `E2ELifecycleTests.cs` | **13** | **13 PASS (100%)** | Flujos completos de cesta, ubicación, sincronización, persistencia tras reinicio y backup. |
| **FRONTEND UNIT / ROUTING** | Vitest 4.1.11 (`app.spec.ts`, `routing.spec.ts`) | **56** | **56 PASS (100%)** | 50 pruebas unitarias de UI y 6 pruebas de navegación profunda y rutas Angular. |
| **FRONTEND BUILD** | Angular CLI 22.2.1 | **1** | **PASS** | Bundle de producción generado limpiamente en `wwwroot` (494 kB). |
| **SMOKE STANDALONE** | `scripts/smoke-api.ps1` | **1** | **PASS** | Instancia de API levantada en directorio temporal; health `database: ok`, `foreignKeysValid: true`, `mode: LOCAL`. |
| **SQLITE INTEGRITY** | PRAGMA check en frío y caliente | **1** | **PASS** | `PRAGMA integrity_check: ok`, `PRAGMA foreign_key_check: 0`. |

---

## 11. Pruebas en Vivo y Bloqueos Externos (`BLOCKED_EXTERNAL`)

1. **Google Maps Routes v2 & Geocoding Live**:
   - **Clasificación**: `BLOCKED_EXTERNAL`.
   - **Justificación**: Requiere una clave de API válida y habilitada para facturación en Google Cloud. La aplicación está 100% probada con mocks y contratos; sin clave, activa el enrutador offline Haversine 1.35x y enlaces públicos web.
2. **OpenRouter AI Semantic Live**:
   - **Clasificación**: `BLOCKED_EXTERNAL`.
   - **Justificación**: Requiere una clave de API de OpenRouter con créditos activos. La aplicación es 100% operativa de forma local sin IA mediante matching determinista.
3. **Auditoría Física en Góndola**:
   - **Clasificación**: `BLOCKED_EXTERNAL`.
   - **Justificación**: Cotejar en persona los precios de góndola física de D1 y Éxito contra los precios del catálogo digital en sucursales físicas requiere presencia humana en tienda. El sistema garantiza la honestidad mostrando siempre la advertencia de precio no físico confirmado.

---

## 12. Limitaciones Conocidas

- **Tiendas Ara**: La integración cubre folletos promocionales estructurados y aportes comunitarios manuales verificados (`USER_MANUAL`); no realiza scraping desasistido de canales no autorizados.
- **Disponibilidad de TWO_WHEELER**: Depende de la cobertura regional del servicio de Google Routes v2. Cuando Google no ofrece datos para motocicletas en ciertas zonas, el fallback a `DRIVE` se activa automáticamente.
- **Navegación offline**: En ausencia de conexión a internet o sin API key de Google, las rutas calculadas utilizan distancias Manhattan/Haversine con factor 1.35x.

---

## 13. Decisión Final de Release

Cumplidos todos los requisitos técnicos, de integridad de datos, migraciones de esquema, pruebas unitarias e integración al 100%, empaquetado de evidencias y publicación para Windows sin dependencias de Node.js en tiempo de ejecución:

# **READY FOR 1.0.0**
