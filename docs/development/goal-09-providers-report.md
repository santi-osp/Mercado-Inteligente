# Reporte de Desarrollo — GOAL-09: Ara Parcial y Plataforma Extensible para Nuevas Tiendas

**Fecha:** 2026-10-07  
**Estado:** COMPLETE  
**Responsable:** Antigravity / Claude Opus 5.5  

---

## 1. Resumen Ejecutivo

En cumplimiento estricto con `CLAUDE.md`, `GOALS_MASTER.md` y las especificaciones técnicas del proyecto Mercado Inteligente, se completó la implementación de **GOAL-09**:
1. **Modelado honesto de Tiendas Ara** bajo clasificación de diagnóstico D (hard-discount sin catálogo online estructurado público, sin raspado invasivo ni ingeniería inversa no autorizada).
2. **Ingesta estructurada de folletos promocionales (`AraBrochureIngest`)** con ventanas temporales de vigencia (`ValidFrom`, `ValidTo`), auditoría inmutable en `RawStore` (`.json.gz` con SHA-256) y cota estricta de memoria de 2 MB.
3. **Servicio de aportes de precios comunitarios (`AraManualOfferService`)** con trazabilidad (`source: USER_MANUAL`), diferenciación entre precio en tienda física verificada (`PriceScope.PhysicalVerified`) y alcance general desconocido (`PriceScope.Unknown`), y persistencia de observaciones en `PriceObservations`.
4. **Registro desacoplado de proveedores (`IStoreProviderRegistry`, `StoreProviderRegistry`)**: eliminación de condicionales acoplados `if (store == ...)` en el dominio, optimizador y aplicación, exponiendo metadatos (`ProviderMetadata`) mediante Inyección de Dependencias (DI).
5. **Kit de extensibilidad para desarrolladores**: plantilla de código lista para producción (`src/backend/MercadoInteligente.Infrastructure/Stores/Template/NewStoreProviderTemplate.cs`) y guía paso a paso (`docs/providers/ADDING_A_PROVIDER.md`).
6. **Pruebas de arquitectura y extensibilidad**: `SampleStoreProviderTests.cs` demuestra cómo un proveedor arbitrario (`MockBodegaProvider`) se inyecta por DI y es consumido universalmente por el `BasketOptimizer` basándose puramente en matemática combinatoria.
7. **Frontend PWA capability-aware**: visualización de etiquetas honestas (`[PARCIAL]`, `[FOLLETO]`, `[MANUAL]`, `[COMPLETA]`), desglose de capacidades por píldoras, monitoreo de salud/ofertas de Ara y formulario interactivo reactivo de registro y eliminación de ofertas comunitarias.

---

## 2. Componentes Implementados

### 2.1 Capacidades Declaradas y Contrato de Honestidad (`AraProvider.cs`)
```csharp
public static readonly ProviderCapabilities DeclaredCapabilities = new(
    Search: CapabilityState.Partial,
    Details: CapabilityState.Partial,
    Offers: CapabilityState.Partial,
    Stores: CapabilityState.Partial,
    Availability: CapabilityState.NotSupported,
    CatalogEnumeration: CapabilityState.NotSupported
);
```
- `AraProvider` no simula capacidades inexistentes. Consultar `EnumerateCatalogAsync` arroja `NotSupportedException` explícito y documentado.
- La búsqueda (`SearchAsync`), detalles (`GetProductAsync`) y ofertas (`OffersAsync`) operan directamente sobre los registros vigentes de folletos y aportes comunitarios en `MarketDb`.

### 2.2 Ingesta de Folletos Promocionales (`AraBrochureIngest.cs`)
- **Límite de memoria**: Rechazo inmediato ante cargas superiores a 2 MB (`ArgumentException`).
- **Auditoría inmutable**: Cada lote de folleto se comprime y almacena en `RawStore` con prefijo `ara/brochure` y digest SHA-256.
- **Ventana de vigencia**: Solo los ítems con fechas válidas son indexados como ofertas activas (`PriceScope.OnlineGeneric`, `context: "brochure:<campaña>"`).

### 2.3 Aportes Comunitarios Manuales (`AraManualOfferService.cs`)
- Permite a los usuarios y administradores registrar observaciones físicas de precios en góndola.
- **Semántica de alcance**:
  - Si se especifica una tienda física: `PriceScope.PhysicalVerified` vinculado a la entidad `Store`.
  - Si no se especifica tienda: `PriceScope.Unknown`.
- **Eliminación y reactividad**: Endpoint `DELETE /api/v1/stores/ara/manual/{id}` limpia atómicamente la oferta y sus observaciones de precio vinculadas.

### 2.4 Registro Desacoplado de Proveedores (`StoreProviderRegistry.cs`)
- Implementa `IStoreProviderRegistry`.
- Descubre y cataloga todos los `IStoreProvider` inyectados en la colección de servicios de ASP.NET Core.
- Expone `GetMetadata()` con información enriquecida (`Code`, `Name`, `RetailerType`, `CatalogCoverage`, `Capabilities`, `Status`, `Enabled`, `SupportsManualOffers`, `Description`).

### 2.5 Extensibilidad Universal (`SampleStoreProviderTests.cs`)
- Demuestra que `MockBodegaProvider` ("BODEGA") se registra vía DI sin tocar una sola línea de código del dominio o del optimizador.
- `BasketOptimizer.Optimize(...)` selecciona automáticamente la mejor oferta entre múltiples proveedores basándose estrictamente en costos, restricciones y matemática pura.

### 2.6 Frontend PWA (`app.ts`, `app.html`, `app.css`, `app.spec.ts`)
- Badges de cobertura: `[PARCIAL]`, `[FOLLETO]`, `[MANUAL]`, `[COMPLETA]`.
- Píldoras de capacidad: Búsqueda, Precios, Tiendas, Stock, Catálogo.
- Sección dedicada en "Fuentes" para consulta de métricas de Ara y formulario interactivo con validación de entradas, feedback de éxito/error y eliminación en tiempo real.

---

## 3. Evidencia de Pruebas y Verificación

### 3.1 Backend .NET
```
Test run for MercadoInteligente.Tests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 271, Skipped: 0, Total: 271, Duration: 7 s
```
- `AraProviderTests.cs`: 7 pruebas (Capacidades honestas, NotSupportedException en enumeración, Ingesta de folleto con auditoría RawStore, Rechazo de payload > 2MB, Creación de oferta PhysicalVerified, Oferta con Scope Unknown, Eliminación y limpieza de observaciones, Health probe, Endpoints API completos).
- `SampleStoreProviderTests.cs`: 2 pruebas (Registro extensible vía DI y optimización universal de canasta sin conditionals).

### 3.2 Frontend Angular 22
```
Test Files  1 passed (1)
Tests       46 passed (46)
Duration    2.54s
```
- Pruebas unitarias completas en `app.spec.ts` cubriendo renderizado de badges de cobertura, consulta de salud de Ara, y ciclo de vida de creación/eliminación de ofertas manuales con reactividad optimista.
- Build de producción `npm run build`: 0 errores, 0 advertencias.

---

## 4. Conclusión

El objetivo **GOAL-09** ha cumplido satisfactoriamente con la totalidad de sus criterios de aceptación y Definition of Done (DoD). Se avanza de forma inmediata y sin interrupción a **GOAL-10 (Hardening, E2E y Release)**.
