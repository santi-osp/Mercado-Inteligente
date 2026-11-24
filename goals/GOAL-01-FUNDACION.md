# GOAL 01 — Fundación profesional del proyecto

## Propósito

Crear una base ejecutable, probada y mantenible para Mercado Inteligente. Este goal no busca comparar tiendas reales todavía; busca garantizar que los siguientes nueve goals no obliguen a rehacer arquitectura, persistencia, configuración, seguridad o Design System.

## Fuentes de verdad

Leer antes de implementar:

- `docs/specifications/01_Product_Specification_Comparador_Mercado.md`
- `docs/specifications/02_UI_UX_Design_System_Comparador_Mercado.md`
- `docs/specifications/03_Arquitectura_Tecnica_Comparador_Mercado.md`
- `docs/architecture/*.md`
- `docs/quality/*.md`
- `docs/reference/FINAL_DIAGNOSTIC_SUMMARY.md`

## Resultado

Al terminar debe existir una aplicación local con:

- Angular PWA funcional;
- ASP.NET Core funcional;
- solución/repositorio profesional;
- EF Core + SQLite migrations;
- modelo de dominio base;
- datos locales en `%LOCALAPPDATA%\MercadoInteligente`;
- settings persistentes;
- SecretStore abstracto;
- logging local;
- health/status;
- backups/integrity check;
- contratos de providers/IA/geocoding/routing;
- UI shell coherente con Design System;
- tests base.

## Estructura recomendada

```text
src/
  backend/
    MercadoInteligente.Api/
    MercadoInteligente.Application/
    MercadoInteligente.Domain/
    MercadoInteligente.Infrastructure/
  frontend/
tests/
  unit/
  integration/
  architecture/
docs/
scripts/
```

No crear microservicios.

## Backend

### Domain

Implementar entidades/value objects/enums suficientes para V1:

- Retailer
- Store
- ExternalProduct
- ExternalSku
- Seller
- Offer
- PriceObservation
- CanonicalProduct
- ProductVariant
- ProductMatch
- MatchOverride
- RawCapture
- Basket
- BasketItem
- LocationProfile
- ComparisonProfile
- SyncRun
- ProviderHealth
- App settings relevantes

Evitar lógica de infraestructura en Domain.

### Enums/conceptos obligatorios

- `PriceScope`: ONLINE_GENERIC, CITY, REGION, FULFILLMENT_REGION, STORE, PHYSICAL_VERIFIED, UNKNOWN.
- `AvailabilityStatus`: IN_STOCK, OUT_OF_STOCK, UNKNOWN, NOT_SOLD.
- dimensión: MASS, VOLUME, COUNT, LENGTH, UNKNOWN.
- seller: RETAILER_DIRECT, INTERNAL_FULFILLMENT, MARKETPLACE, UNKNOWN.
- freshness: FRESH, STALE, OUTDATED, UNKNOWN.
- sync states.
- provider capability state.
- provider health.

### Dinero

No usar `float/double`. Utilizar decimal/value object monetario. Moneda inicial COP, pero no hacer imposible soportar otra moneda.

### Fechas

UTC internamente. UI convierte a `America/Bogota`.

## Persistencia

### SQLite

- EF Core migrations, no `EnsureCreated` productivo.
- DB fuera del repo.
- `foreign_keys=ON`.
- evaluar WAL/busy timeout.
- crear índices de IDs externos, EAN, retailer, SKU, offer, checked_at.

### Paths

Crear servicio de rutas locales. No hardcodear usuario Windows.

```text
%LOCALAPPDATA%\MercadoInteligente\
  data\market.db
  data\raw\
  backups\
  logs\
```

### RAW

Crear infraestructura de metadata + filesystem gzip + SHA-256 (u otro hash robusto). Debe ser posible detectar payload duplicado.

No se requiere aún guardar payload real de D1/Éxito.

### Backup

Implementar backup manual funcional y `PRAGMA integrity_check`.

Antes de migraciones futuras debe existir camino backup→migration→integrity.

## Settings

Persistir defaults:

- MaxStores=2
- SizeTolerancePercent=15
- AllowSubstitutes=true
- MaxOverbuyPercent=20
- PriceTtl=6h
- StockTtl=2h
- ProductTtl=7d
- StoresTtl=7d
- RouteTtl=24h
- AI=false

Separar secretos de settings normales.

## SecretStore

Crear `ISecretStore` y una implementación local segura adecuada para Windows/.NET. No inventar criptografía.

No guardar keys reales durante este goal.

## Contratos externos

### IStoreProvider

Debe poder representar, según capabilities:

- search;
- product details;
- offers;
- stores;
- availability;
- catalog enumeration opcional.

Crear `ProviderCapabilities` explícito.

Crear `DemoStoreProvider` solo Development/tests.

### IA

- `IAiProvider`
- `NullAiProvider`
- status NOT_CONFIGURED/DISABLED.

### Geocoding/routing

- `IGeocodingProvider`
- `IRoutingProvider`
- null/mock providers.

Google no configurado no es error fatal.

## Sync foundation

Crear `ISyncCoordinator`/modelo `SyncRun` suficiente para goals posteriores.

Al iniciar, RUNNING antiguo debe poder marcarse ABORTED.

No implementar catálogo real.

## API

Versionar `/api/v1`.

Endpoints mínimos útiles:

- system health/status;
- settings get/update;
- retailers/stores;
- búsqueda local/demo;
- baskets básicos;
- backup manual.

Usar error contract coherente y exception middleware. No stack trace a UI.

## Frontend

### Shell

Sidebar desktop:

- Inicio
- Mi mercado
- Comparar
- Historial
- Tiendas
- Configuración

Topbar:

- ubicación actual/default;
- estado general de datos/sistema.

### Design tokens

Implementar tokens de la especificación, incluyendo paleta verde premium, superficies claras, borders y tipografía.

No improvisar estética de marketplace.

### Componentes base

- Button/IconButton
- Card
- Badge/StatusBadge
- Input/Select/Toggle
- EmptyState
- LoadingState
- ErrorState
- PageHeader
- StatCard

### Home demo

Mostrar UI real con DemoStoreProvider o fixtures, claramente DEMO. Debe incluir tarjetas Products/Ahorro/Recorrido/MaxStores y un estado vacío/CTA coherente.

### Settings UI

Persistir:

- max stores;
- size tolerance;
- substitutes;
- max overbuy;
- AI enabled (deshabilitado si no configurado).

Sección avanzada: OpenRouter, Google, DB, backup, provider health.

### Responsive

Desktop excelente y base móvil real. Configurar PWA manifest/service worker apropiadamente sin gastar tiempo en branding final.

## Normalización base

Crear servicios determinísticos mínimos:

- limpieza texto;
- aliases LT/LTR/L, GR/GRS/G, UND;
- parser 1 kg, 1000 g, 1.5 L, 1500 ml, x12, 6 x 1 L;
- conversiones dentro de la misma dimensión;
- unit price COP/kg, COP/L, COP/unit.

No construir matcher completo aún.

## Tests obligatorios

Unit:

- 1000g=1kg;
- 1000ml=1L;
- 500ml != 500g;
- 6x1L=6L;
- price <=0 inválido;
- null stock=UNKNOWN;
- defaults settings;
- hash/dedup RAW.

Integration:

- migration DB limpia;
- foreign keys;
- settings persisten tras reinicio;
- price observation relationship;
- backup;
- integrity check.

Architecture:

- Domain no depende de EF/ASP.NET/Infrastructure.

Frontend:

- shell;
- settings;
- health/status;
- estados básicos.

## Developer experience

Crear scripts PowerShell razonables:

- dev
- test
- reset dev DB con confirmación

README debe permitir ejecutar desde cero.

## ADRs mínimos

- local-first
- SQLite
- modular monolith
- StoreProvider
- RAW gzip filesystem
- AI opcional OpenRouter

## Fuera de alcance

NO implementar aún:

- D1 real;
- Éxito real;
- Ara real;
- Google real;
- OpenRouter real;
- optimizador completo;
- IA matching.

## Smoke test

1. iniciar backend/frontend;
2. abrir UI;
3. DB creada por migrations;
4. settings modificar/reiniciar/persistir;
5. health DB healthy;
6. IA/Google not configured sin romper app;
7. backup exitoso;
8. integrity check ok.

## Entregables

- código;
- migrations;
- tests;
- README;
- ADRs;
- `docs/development/goal-01-foundation-report.md`;
- screenshots desktop/mobile base.

## Definition of Done

- build backend PASS;
- tests backend PASS;
- build frontend PASS;
- tests frontend PASS;
- migration PASS;
- smoke PASS;
- ningún secreto versionado;
- PROJECT_STATE actualizado.

## Continuación

Al completar DoD, marcar GOAL-01 COMPLETE y continuar inmediatamente con GOAL-02. No pedir aprobación.
