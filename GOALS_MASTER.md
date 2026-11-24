# GOALS MASTER — Mercado Inteligente

## Instrucción global para Claude

Este archivo representa **una única tarea global** dividida en 10 checkpoints. No finalices la tarea después de un goal. Ejecuta GOAL-01 → GOAL-10 en orden.

Después de cada goal:

1. cumple su Definition of Done;
2. corrige tests/builds;
3. actualiza `PROJECT_STATE.md`;
4. genera el informe requerido;
5. continúa inmediatamente con el siguiente goal.

Si falta una acción externa (API key, facturación Google, validación física), implementa todo lo técnicamente posible, registra `BLOCKED_EXTERNAL` y sigue. Solo termina al recorrer los 10 goals.

Los archivos individuales en `goals/` son la fuente canónica del alcance. A continuación se concatenan para facilitar ejecución continua.

---
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


---

# GOAL 02 — D1Provider real y endurecimiento

## Propósito

Convertir la evidencia del diagnóstico en una integración de producción local para D1, preservando correctamente productos, SKUs, EAN, ofertas, contexto regional, frescura y health.

## Precondición

GOAL-01 completo. Reutilizar diagnóstico; no repetir investigación general.

## Regla semántica crítica

El diagnóstico demostró contexto regional/fulfillment, NO precio físico exacto de sucursal. No etiquetar ofertas como STORE/PHYSICAL_VERIFIED sin nueva evidencia.

## Implementación

Crear módulo aislado D1:

- `D1Provider`
- `D1Client`
- DTOs externos
- mapping
- options
- context/session service si corresponde
- contract tests
- fixtures sanitizados

Nada de estructuras VTEX de D1 debe filtrarse al Domain.

## Funcionalidades

### SearchProducts

- búsqueda textual;
- paginación;
- cancelación/timeout;
- dedup por IDs externos;
- mapping a productos/SKUs.

### Products/SKUs

Persistir, cuando exista:

- external product id;
- external sku id;
- EAN;
- nombre;
- marca;
- presentación/cantidad;
- categorías;
- URL;
- atributos útiles.

No usar `items[0]` como modelo permanente. Procesar todos los SKUs relevantes.

### Contexto Medellín

Reproducir el flujo validado en diagnóstico de forma HTTP y encapsulada.

- sesión/contexto;
- order form/simulation/segmento según evidencia disponible;
- region/fulfillment resultante;
- reconstrucción desde cero;
- expiración/fallo.

Persistir un identificador de contexto suficiente para trazabilidad sin guardar secretos innecesarios.

### Offers

Por SKU/contexto:

- Price
- ListPrice
- seller
- availability
- available quantity raw
- promoción estructurada cuando sea posible
- checked_at
- price scope correcto (REGION/FULFILLMENT_REGION)

### Stock

No exponer sentinels como inventario físico exacto. Mapping debe diferenciar disponible/desconocido.

## Persistencia e histórico

- upsert ExternalProduct/ExternalSku;
- Offer representa último estado actual;
- PriceObservation solo ante cambios relevantes o política de heartbeat;
- LastSeenAt/LastCheckedAt;
- RAW gzip dedup para evidence/debug;
- SyncRun por operación/batch.

## Cache/TTL

Aplicar defaults del sistema. Una búsqueda puede devolver SQLite inmediatamente y refrescar si está stale.

## Resiliencia

- timeout explícito;
- retry limitado para fallos transitorios;
- 403/429 sin evasión;
- backoff/cooldown;
- last-known-good;
- provider health por capabilities;
- errores parciales no destruyen catálogo local.

## Health

D1 capabilities iniciales deben reflejar evidencia real. No marcar store-specific prices si no están demostrados.

Health checks/contract checks:

- endpoint accesible;
- búsqueda `arroz` produce estructura válida;
- al menos un SKU válido;
- mapping de precio no negativo;
- contexto Medellín construible cuando Internet está disponible.

## Contract tests

Separar:

- offline fixtures deterministas (CI/local siempre);
- live opt-in (requiere Internet).

No fijar precio concreto como assertion estable.

## UI

Actualizar:

- Stores/Providers status;
- búsqueda real D1;
- detalle con fuente, `checked_at`, PriceScope;
- badges FRESH/STALE;
- mostrar “Precio online regional” o texto equivalente, no “precio físico sucursal”.

Permitir refresh manual con cooldown.

## Casos borde

Probar:

- producto con múltiples SKUs;
- EAN faltante;
- precio cambia por contexto;
- stock sentinel;
- mismo producto repetido por categorías;
- producto desaparece;
- HTTP timeout;
- 500;
- respuesta vacía;
- schema drift fixture;
- sync abortado.

## Métricas locales

Registrar por sync:

- items recibidos;
- products/skus creados/actualizados;
- offers changed;
- errores;
- duración;
- RAW new/dedup.

## Entregables

- D1 provider real;
- fixtures sanitizados;
- tests;
- documentación `docs/providers/d1.md`;
- `goal-02-d1-report.md`;
- screenshots de búsqueda/detalle real si Internet disponible.

## DoD

- unit/integration PASS;
- D1 offline contract PASS;
- live smoke PASS si Internet disponible; si no, documentar BLOCKED_EXTERNAL_NETWORK sin detener roadmap;
- no secrets/cookies persistentes inseguras;
- last-known-good validado;
- PriceScope correcto;
- PROJECT_STATE actualizado.

## Continuación

Completar y pasar inmediatamente a GOAL-03.


---

# GOAL 03 — ExitoProvider real y endurecimiento

## Propósito

Implementar Éxito sobre la vía contextual estable identificada en diagnóstico (FastStore/SearchQuery y contexto correspondiente), modelando correctamente sellers, fulfillment, marketplace, precios, stock y promociones.

## Regla crítica

Nunca calcular “precio de Éxito” como mínimo arbitrario de todos los sellers.

Default de comparador:

- RETAILER_DIRECT: elegible.
- INTERNAL_FULFILLMENT: se conserva como cadena/contexto, no se confunde con seller comercial.
- MARKETPLACE: excluido por defecto.
- UNKNOWN: excluido hasta clasificar o permitir explícitamente.

## Módulo

Crear:

- ExitoProvider
- ExitoClient/FastStore client
- modelos DTO
- mapper
- seller classifier
- context builder
- fixtures
- contract tests

## Search/catalog

Implementar búsqueda estructurada y paginación respetando contexto.

Procesar todos los SKUs/variantes, no solo primer item.

## Contexto Medellín

Implementar explícitamente:

- channel/salesChannel;
- regionId/contexto obtenido según diagnóstico;
- configuración y reconstrucción;
- trazabilidad del contexto.

No atribuir automáticamente a sucursal física si la evidencia solo soporta región/pickup/fulfillment.

## Sellers

Persistir:

- external seller id;
- name;
- classification;
- retailer direct flag conceptual;
- marketplace state;
- fulfillment chain cuando corresponda.

Mantener separación entre seller comercial y warehouse/fulfillment interno.

## Offers

Persistir precio/list price/promo/availability por SKU + seller + contexto.

Promociones que aparecen cuando se solicita seller comercial y se resuelven a fulfillment interno deben conservar esa semántica.

## Marketplace config

Agregar setting avanzado:

`IncludeMarketplace=false` default.

UI debe indicar marketplace cuando se habilite.

## Resiliencia

Diagnóstico observó fallos transitorios 502/504. Implementar:

- timeout;
- retry limitado para status transitorio;
- backoff;
- no retry ciego para 4xx;
- last-known-good;
- health degradation.

## Contract tests

Offline fixtures:

- seller directo;
- fulfillment chain;
- marketplace;
- promo;
- stock;
- schema drift.

Live opt-in:

- búsqueda de productos;
- contexto Medellín;
- consistencia de estructuras;
- no assertion de precio fijo.

## UI

Búsqueda/comparación debe mostrar:

- Éxito directo;
- seller/contexto cuando sea relevante;
- Marketplace excluido/visible según config;
- `PriceScope` honesto;
- freshness.

## Persistencia/histórico

Mismas reglas de Goal 2. No duplicar infraestructura; reutilizar componentes comunes sin crear abstracciones prematuras peligrosas.

## Casos borde obligatorios

- mismo SKU con varios sellers;
- seller desconocido;
- marketplace más barato que directo;
- promoción vinculada a seller comercial;
- contexto genérico vs Medellín con precio diferente;
- stock `99999` u otro sentinel;
- 502/504;
- timeout;
- resultado sin seller directo;
- producto solo marketplace;
- seller desaparece.

## Entregables

- ExitoProvider;
- seller classification;
- settings marketplace;
- tests;
- `docs/providers/exito.md`;
- informe Goal 3.

## DoD

- D1 sigue pasando;
- Éxito tests PASS;
- marketplace no contamina recomendación default;
- context/pricing semánticamente correctos;
- provider health funcional;
- PROJECT_STATE actualizado.

## Continuación

Pasar inmediatamente a GOAL-04.


---

# GOAL 04 — Ciclo de vida de datos, sincronización y mantenimiento

## Propósito

Convertir D1/Éxito en fuentes sostenibles durante meses/años. Implementar el motor de frescura, caché, sync, retención, last-known-good, limpieza y mantenimiento descrito en `DATA_LIFECYCLE_AND_SYNC.md`.

## Resultado

La aplicación debe seguir siendo útil si:

- estuvo apagada varios días;
- Internet falla;
- un provider falla temporalmente;
- productos desaparecen/regresan;
- una sync se interrumpe;
- RAW/logs crecen;
- la DB acumula histórico.

## Sync Engine

Crear/cohesionar servicio central de sincronización con colas/prioridades simples, no infraestructura distribuida.

Tipos:

1. On-demand: producto/búsqueda solicitada y stale.
2. Prioritaria: cesta activa/favoritos/frecuentes.
3. Mantenimiento: catálogo oportunista mientras app está abierta.

Evitar refresh masivo constante.

## Stale-while-revalidate

Patrón UI/API:

1. leer local;
2. devolver inmediatamente si existe;
3. etiquetar freshness;
4. disparar refresh cuando corresponda;
5. actualizar UI/evento cuando termina;
6. conservar valor previo si falla.

## TTL

Implementar configuración global y, si aporta valor, override por provider:

- price 6h;
- stock 2h;
- products 7d;
- stores 7d;
- routes 24h;
- promo 4h.

Crear tests de boundary exacto de TTL.

## Lifecycle de productos

Implementar ACTIVE/MISSING/INACTIVE/UNKNOWN.

No borrar por una ausencia. Política inicial configurable o central:

- missing tras sync(s) sin aparición;
- inactive tras periodo prolongado;
- reactivación automática si reaparece.

## Price history inteligente

No crear observación idéntica en cada polling.

Crear observation cuando cambia al menos uno:

- price;
- list price;
- availability;
- promoción;
- seller/contexto semántico.

Si no cambia: actualizar last_seen/last_checked.

Opcional heartbeat poco frecuente si se necesita demostrar continuidad.

## Anomalías

Crear validación para:

- price<=0;
- cambio extremo configurable;
- quantity/unit incoherente;
- response incompleta.

No descartar automáticamente un precio extremo; marcar `SUSPICIOUS`/requiere segunda lectura cuando sea apropiado.

## RAW retention

Implementar:

- gzip;
- hash dedup;
- relative path;
- cleanup por antigüedad (default 60d);
- no eliminar RAW todavía referenciado por tarea de debugging activa si existe mecanismo equivalente.

Cleanup debe ser seguro e idempotente.

## Logs retention

Rotación y limpieza ~30 días configurable.

## SyncRun

Completar métricas/estados:

- PENDING/RUNNING/SUCCESS/PARTIAL/FAILED/ABORTED;
- start/end;
- counts;
- provider/context;
- error summary.

Al arranque marcar RUNNING antiguo como ABORTED.

## Concurrencia

Evitar sync duplicado del mismo provider/context/resource. Implementar coordinación local simple.

SQLite: escrituras coordinadas, transacciones por lotes razonables.

## Maintenance service

Implementar tareas locales:

- cleanup RAW;
- cleanup logs si aplica;
- PRAGMA optimize;
- integrity check programable/manual;
- backup policy base;
- tamaño DB/RAW.

VACUUM solo bajo criterio medible/manual o scheduler poco frecuente.

## Backups

Completar política:

- diarios 7;
- semanales 4;
- mensuales 3;

Si automatización total complica, debe al menos existir una política funcional y segura ejecutable desde maintenance.

Probar restore a DB temporal como parte de integration tests.

## Offline mode

Detectar dependencia externa no disponible. La UI debe seguir permitiendo búsquedas locales, historial y cestas.

Mostrar estado offline/degraded sin modal bloqueante permanente.

## UI

Agregar:

- freshness consistente;
- progreso sync útil;
- “actualizando…” no bloqueante;
- last sync por provider;
- refresh manual con cooldown;
- maintenance/system stats en Configuración.

## Casos obligatorios

- PC estuvo apagado 3 días;
- price stale + provider funciona;
- price stale + provider falla;
- product missing/reappears;
- sync abortado;
- dos refresh simultáneos;
- RAW duplicado;
- cleanup idempotente;
- DB backup/restore;
- price unchanged no infla history;
- price changed sí crea history.

## Performance

Cargar UI desde DB sin esperar red. Medir tamaños y tiempos. No optimizar prematuramente.

## Entregables

- Sync/Maintenance engine;
- retention;
- backup policy;
- UI freshness/offline;
- tests;
- `docs/development/goal-04-data-lifecycle-report.md`.

## DoD

- D1/Éxito siguen operativos;
- last-known-good validado;
- retention probado en directorio temporal;
- backup/restore PASS;
- sync interrupt/concurrency tests PASS;
- UI no bloquea ante Internet caído;
- PROJECT_STATE actualizado.

## Continuación

Pasar inmediatamente a GOAL-05.


---

# GOAL 05 — Normalización, matching, equivalencias y comparación de presentaciones

## Propósito

Construir el motor determinístico profesional que permite comparar productos de distintas tiendas sin depender de IA.

## Principio

Prioridad de precisión sobre recall para EXACT. Es mejor “ambiguo/no sé” que unir productos incorrectos.

## Pipeline

1. User MatchOverride.
2. EAN/GTIN + conflicto checks.
3. Reglas estructuradas.
4. Normalized exact candidate.
5. Fuzzy candidate generation.
6. IA opcional (se implementa Goal 8).
7. Usuario confirma/rechaza.

## Normalización

Expandir servicio:

- Unicode/acentos;
- casing;
- espacios/puntuación;
- aliases Colombia;
- marcas;
- unidades;
- cantidades;
- packs;
- tokens de variante.

No destruir raw strings.

## Quantity model

Soportar bien:

- MASS: g/kg;
- VOLUME: ml/L;
- COUNT: unidades;
- LENGTH cuando aplique;
- package_count;
- quantity_per_unit;
- total_quantity.

No convertir dimensiones incompatibles.

## Packs/multipacks

Parsear formatos comunes:

- `6 x 1 L`;
- `x12`;
- `2x900 ml`;
- `pack 4`;
- `12 rollos x 40 m` cuando datos permitan.

Guardar cantidad total y estructura del pack.

## Atributos relevantes

Crear vocabulario extensible, no 300 columnas. Campos frecuentes estructurados; atributos raros en JSON.

Ejemplos:

- sugar_free/zero;
- lactose_free;
- milk_type;
- coffee_form;
- detergent_form/concentrated;
- egg_grade;
- diaper_size;
- flavor;
- paper_length.

## CanonicalProduct / ProductVariant

Definir y poblar canónicos/variantes a partir de SKUs externos.

No forzar un canónico si no hay confianza suficiente.

## EXACT

### EAN

EAN idéntico es señal fuerte, pero verificar conflictos críticos:

- brand incompatible;
- quantity muy incompatible;
- variante claramente distinta.

Si conflicto: `AMBIGUOUS/REVIEW`, no auto-match.

### Rule exact

Marca + tipo + variante + presentación compatibles puede confirmar según reglas documentadas.

## SUBSTITUTE

Separado de EXACT.

Sustitución debe considerar:

- misma categoría funcional;
- atributos requeridos;
- tolerancia tamaño;
- marca preferida/excluida;
- overbuy;
- perfil usuario.

No establecer “calidad” subjetiva sin datos/configuración.

## Fuzzy

Implementar algoritmo(s) razonables para candidate retrieval/ranking. Nunca crear ProductMatch EXACT solo por score fuzzy.

Guardar score como evidencia de ranking, no como verdad.

## MatchOverride UI

Permitir:

- “Son el mismo producto”.
- “Son sustitutos”.
- “Nunca comparar”.

Persistencia tiene prioridad futura.

## Comparación por tamaño

Default ±15%, configurable.

Calcular:

- precio absoluto;
- precio/kg;
- precio/L;
- precio/unidad;
- total requerido para cubrir cantidad de cesta;
- overbuy.

No declarar ganador solo por precio unitario si obliga a sobrecompra fuera del límite.

## Productos especiales

### Weight based

Soportar precio/kg y total estimado.

### Papel

Si existen metros/rollo, poder calcular total length; si no, limitarse a $/rollo y declarar limitación.

### Detergente concentrado

No inferir rendimiento por lavadas sin dato explícito.

## Dataset de evaluación

Reutilizar el diagnóstico y construir/expandir un corpus versionado de pares etiquetados:

- EXACT;
- SUBSTITUTE;
- NOT_EQUIVALENT.

Agregar casos reales D1↔Éxito encontrados durante Goals 2/3.

No inflar métricas con duplicados del mismo SKU sin declararlo.

## Métricas

Para exact auto-match medir:

- precision;
- recall;
- F1;
- false positives;
- false negatives.

Criterio prioritario: false positive muy bajo.

Definir thresholds en base a datos, no intuición.

## Search/FTS

Mejorar FTS ranking usando product type/category/brand para que `leche` no priorice “dulce de leche” sobre leche bebida cuando haya mejor señal.

No hace falta un motor externo.

## UI

Pantalla Comparar:

- tabs Exactos / Tamaños similares / Otras marcas;
- explainability breve (EAN, misma marca/tamaño, sustituto sugerido);
- confidence/status;
- botón confirmar/rechazar;
- comparación por unidad.

## Tests obligatorios

- Coca-Cola Original vs Zero no exacto;
- 1L vs 1000ml equivalente tamaño;
- 900g vs 1kg similar según tolerance;
- 5kg no aceptado si overbuy excede perfil;
- mismo EAN con conflicto → review;
- fuzzy=alto con variante distinta → no exact;
- override DO_NOT_MATCH gana;
- pack 6x1L;
- egg AA vs AAA substitute configurable, no exact;
- g vs ml no comparable por unidad base.

## Entregables

- matcher determinístico;
- canonicalization;
- dataset y evaluación;
- Comparison UI funcional D1/Éxito;
- `matching-evaluation.md` actualizado;
- Goal 5 report.

## DoD

- app compara D1/Éxito sin IA;
- exact matching no usa fuzzy como confirmación;
- overrides funcionan;
- unit comparisons correctas;
- métricas documentadas honestamente;
- tests PASS;
- PROJECT_STATE actualizado.

## Continuación

Pasar inmediatamente a GOAL-06.


---

# GOAL 06 — Cesta, estrategias y optimizador máximo-N

## Propósito

Construir la función central del producto: a partir de una lista de compras y reglas de comparación, determinar combinaciones de productos/tiendas que minimicen costo o equilibren costo/comodidad, respetando máximo N tiendas y preferencias.

## Cesta

Implementar CRUD profesional de Basket/BasketItem.

Cada item puede expresar:

- variante exacta o necesidad genérica;
- cantidad requerida;
- unidad/dimensión;
- sustitutos permitidos override;
- tolerancia tamaño override;
- marca preferida/excluida;
- notas.

## Candidate generation

Para cada BasketItem generar candidatos elegibles desde catálogo normalizado:

- exactos;
- tamaños similares;
- substitutes si habilitado;
- freshness aceptable;
- sellers permitidos;
- availability compatible.

No usar IA aún como requisito.

## Cobertura de cantidad

Calcular cuántas unidades/paquetes comprar para cubrir cantidad solicitada.

Ejemplo:

- necesito 2kg;
- pack 1kg → 2;
- pack 2.5kg → 1 con overbuy 25%; si MaxOverbuy=20, excluir salvo override.

## Promociones

Modelar/evaluar inicialmente:

- precio normal;
- 2x1;
- 3x2;
- N por $X;
- segunda unidad %;
- precio condicionado (identificado, no aplicar si usuario no cumple condición conocida).

Conservar texto/condición original.

## Optimizador de selección

Resolver:

1. qué oferta/candidato cubre cada item;
2. qué conjunto de tiendas usar con `count<=MaxStores`;
3. costo total.

Escala objetivo personal:

- 50–100 items;
- 5–10 tiendas/chains/contexts.

Preferir algoritmo exacto comprensible si escala lo permite; medir antes de introducir solver externo.

## Estrategias

Implementar como mínimo:

### Máximo ahorro

Minimiza productos, respetando constraints y MaxStores.

### Una sola tienda

Mejor cesta completa (o mejor cobertura si ninguna completa), mostrando faltantes.

### Equilibrada

Score configurable usando costo + penalización de número de tiendas; hasta Goal 7 distancia real puede ser 0/no disponible.

### Menor complejidad

Prioriza menos tiendas, luego costo.

Después Goal 7 se integra ruta/distancia.

## Máximo N

Default=2, configurable por perfil y por cesta.

Si N no cubre todo:

- no aumentar silenciosamente;
- mostrar cobertura X/Y;
- calcular sugerencia N+1 separada si mejora a cobertura completa.

## Presupuesto

Agregar presupuesto opcional a cesta/perfil.

Si total excede:

- mostrar diferencia;
- sugerir candidatos sustitutos existentes si están permitidos;
- no quitar productos silenciosamente.

## Freshness

Por defecto no usar datos OUTDATED para afirmar óptimo sin advertencia. Configurar policy.

El resultado debe incluir:

- timestamp snapshot;
- cuántas ofertas fresh/stale;
- items sin confirmar.

## Output explicable

Resultado debe incluir:

- tiendas;
- items asignados;
- cantidades;
- precio unitario/efectivo;
- subtotal;
- total;
- ahorro vs baseline;
- faltantes;
- sustituciones;
- overbuy;
- promociones utilizadas;
- warnings de freshness/scope.

## Baseline de ahorro

Definir claramente contra qué se calcula “ahorro”:

- mejor opción de una sola tienda completa, preferentemente;
- si no existe, no inventar ahorro; usar comparación alternativa y explicarla.

## UI

Home/Resultado se vuelve funcional:

- Tu mejor mercado;
- MaxStore configurable;
- resumen de tiendas;
- estrategias alternativas;
- expandir productos por tienda;
- warnings/faltantes;
- enlaces a comparison.

## Tests

Dataset sintético + datos fixtures reales.

Casos:

- 10 items/3 tiendas y resultados conocidos;
- max1/max2/max3;
- N insuficiente;
- producto sin oferta;
- substitute off;
- overbuy;
- multibuy;
- presupuesto;
- stale candidate;
- marketplace excluido;
- price scope no altera matemáticas pero sí warning;
- empate determinista.

## Performance

Medir 100 items/10 tiendas. Objetivo <2s local, preferible <500ms si algoritmo/DB lo permiten.

No sacrificar corrección por microoptimización.

## Entregables

- Basket UX;
- optimizer service;
- strategy definitions;
- tests/performance report;
- `goal-06-optimizer-report.md`.

## DoD

- usuario puede crear cesta real con D1/Éxito;
- obtener resultado máximo-N;
- explicaciones/faltantes correctos;
- no violaciones silenciosas;
- performance razonable;
- tests PASS;
- PROJECT_STATE actualizado.

## Continuación

Pasar inmediatamente a GOAL-07.


---

# GOAL 07 — Ubicación, Google Maps y optimización de ruta

## Propósito

Agregar la dimensión geográfica real de Medellín: entrada de ubicación, geocoding, tiendas cercanas, rutas, tiempo/distancia y orden de visita, sin convertir Google en una dependencia crítica del producto.

## Precondición

GOAL-06 completo. Leer `EXTERNAL_SERVICES_SETUP.md`.

## Configuración externa

Si `GOOGLE_MAPS_API_KEY` todavía no existe:

- implementar todo el soporte técnico y mocks;
- marcar live verification como `BLOCKED_EXTERNAL`;
- no detener el roadmap.

Si existe, almacenarla mediante `ISecretStore` y ejecutar live tests.

## Entrada de ubicación

Soportar:

- dirección escrita;
- coordenadas `lat,lng`;
- URL larga de Google Maps;
- URL corta `maps.app.goo.gl` cuando pueda resolverse legítimamente;
- perfiles guardados como Casa/Trabajo/etc.

Crear/robustecer `ParseLocationInput` con resultado estructurado:

- original input;
- normalized address opcional;
- latitude/longitude opcional;
- source;
- confidence/status;
- errors claros.

## Google Geocoding

Implementar `GoogleGeocodingProvider` detrás de `IGeocodingProvider`.

Requisitos:

- backend only;
- timeout/retry razonable;
- caché persistente;
- no geocodificar repetidamente misma dirección normalizada;
- status NOT_CONFIGURED si falta key;
- errores quota/denied/zero results tratados.

## Google Routes

Implementar `GoogleRoutingProvider` detrás de `IRoutingProvider`.

Necesidades:

- origen→destino;
- distance meters;
- duration;
- modo configurable con default coherente (moto puede mapearse según soporte; si API no ofrece motocicleta en región/endpoint elegido, documentar fallback a driving, no inventar);
- caché `RouteTtl=24h`;
- route context/version.

No se requiere mapa interactivo embebido.

## Store locations

Usar `Store` local como fuente de sucursales conocidas.

No usar Google Places en cada búsqueda por defecto. Si las fuentes actuales no cubren sucursales suficientemente, documentar estrategia futura/opt-in, pero evitar ampliar APIs sin necesidad.

Permitir import/seed/actualización de stores desde providers/fuentes legítimas.

## Nearby stores

Calcular inicialmente Haversine para filtrar candidatos cercanos rápidamente.

Luego obtener rutas precisas solo para candidatos relevantes.

Evitar N×M llamadas innecesarias a Google.

## Distancia en UI

Distinguir:

- `ROAD_ROUTE`: ruta real.
- `STRAIGHT_LINE_ESTIMATE`: Haversine/fallback.
- `CACHED_ROUTE`.

Nunca mostrar distancia lineal como “recorrido” sin etiqueta.

## Integración con optimizador

Mantener problemas separados:

1. candidatos de productos;
2. selección de tiendas;
3. orden de visita.

### Estrategia equilibrada

Ahora integrar:

- costo productos;
- número de tiendas;
- distancia/tiempo;
- costo/km opcional.

Pesos configurables:

- PriceWeight;
- DistanceWeight;
- ConvenienceWeight;
- BrandWeight.

Normalizar magnitudes para que porcentajes tengan significado. Documentar fórmula y tests.

## Orden de visita

Para 1–4 tiendas, evaluar permutaciones exactas incluyendo origen y, si configuración lo indica, retorno al origen.

Para más tiendas, mantener una estrategia razonable y medible; no introducir solver complejo salvo necesidad.

Configurar:

- returnToOrigin;
- transport mode;
- cost per km;
- max route km opcional.

## Abrir en Google Maps

Generar URL de navegación/itinerario para que el usuario pueda abrir la ruta en Google Maps desde la UI.

No incluir key en URL pública.

## Cost control

Implementar:

- cache;
- rate limiting local;
- evitar calls duplicadas;
- contador local de requests por servicio/mes aproximado;
- UI de configuración con recomendación de quotas/budget alerts.

No intentar leer facturación privada de Google salvo integración explícita futura.

## Fallback

Si Google falla/quota/key ausente:

- geocoding nuevo no disponible si no hay coordenadas;
- perfiles previamente geocodificados siguen funcionando;
- Haversine disponible;
- optimizer puede ejecutar modo sin route cost o con estimated distance;
- UI explica limitación.

## Privacidad

Dirección exacta solo se envía a Google cuando el usuario solicita geocoding/ruta y la integración está activa.

No enviar ubicación a OpenRouter.

## Tests

Offline:

- parse coordinates;
- parse long Google URL patterns;
- invalid URL;
- Haversine known pairs;
- route cache key;
- Google not configured;
- quota error fixture;
- route ordering exact 2/3/4 stores;
- optimizer weights.

Live si key disponible:

- 3+ direcciones genéricas Medellín;
- route entre puntos conocidos;
- cache evita segunda llamada;
- invalid address handled.

## UI

- Location selector/profile;
- configuración Google con `configured` sin mostrar key;
- rutas/distancia en resultados;
- “Abrir en Google Maps”;
- badges exact/estimated;
- settings cost/km, max route, return origin.

## Entregables

- Google providers;
- location parser;
- route optimizer;
- cache/cost guards;
- tests;
- setup instructions actualizadas;
- `goal-07-location-report.md`.

## DoD

- sin key: app completa sigue funcionando y goal puede ser COMPLETE_WITH_EXTERNAL_BLOCKERS;
- con key: live smoke PASS;
- no key leakage;
- optimizer integra distancia sin romper máximo N;
- route order tested;
- PROJECT_STATE actualizado.

## Continuación

Pasar inmediatamente a GOAL-08 aunque la key de Google esté pendiente.


---

# GOAL 08 — OpenRouter e inteligencia artificial opcional

## Propósito

Agregar IA como capa de mejora controlada para normalización ambigua, matching difícil, sustitutos y explicación de resultados, sin convertirla en fuente de verdad ni requisito de funcionamiento.

## Principios

- IA OFF sigue siendo producto completo.
- IA nunca inventa precio, EAN, stock o disponibilidad.
- Enviar mínimo contexto necesario.
- Structured output + validación.
- Toda respuesta puede fallar y debe existir fallback.
- User override siempre gana.

## OpenRouter provider

Implementar `OpenRouterAiProvider : IAiProvider`.

Config:

- Enabled;
- BaseUrl;
- ApiKey desde SecretStore;
- Model configurable;
- timeout;
- max tokens;
- temperature/params solo si aportan valor;
- features enabled;
- gasto/límite local aproximado opcional.

No hardcodear un modelo concreto como dependencia de dominio.

## Capacidades IA V1

### 1. Ambiguous product normalization

Input: campos estructurados mínimos de un SKU ambiguo.

Output JSON validado:

- normalized type;
- brand candidate;
- quantity/package interpretation;
- attributes;
- confidence;
- explanation corta;
- `needsReview`.

Nunca sobrescribir raw.

### 2. Match adjudication

Solo candidatos que el pipeline determinístico dejó ambiguos.

Input:

- candidate A/B attributes;
- deterministic signals;
- no precio como criterio de identidad.

Output:

- EXACT | SUBSTITUTE | NOT_EQUIVALENT | UNSURE;
- confidence;
- concise reason.

No convertir automáticamente en EXACT salvo threshold alto y reglas sin conflicto; preferencia inicial: almacenar AI suggestion y permitir confirmación.

### 3. Substitute suggestion

Evaluar sustitución funcional según atributos/categoría/tamaño y preferencias conocidas.

No emitir juicios vagos de “mejor calidad” sin evidencia.

### 4. Basket explanation

Recibe resultado YA calculado por optimizer y genera explicación humana:

- por qué opción recomendada;
- tradeoff de tercera tienda;
- principales fuentes de ahorro;
- warnings.

El LLM no recalcula totales; cifras vienen estructuradas y se validan.

## Structured outputs

Definir DTO/schema por feature. Parse/validate estrictamente.

Si modelo no soporta response schema nativo, usar JSON-only con parser robusto y fallback.

Respuesta inválida → no crash → fallback determinístico.

## Prompt safety

- system prompt claro;
- product names/descriptions delimitados como data;
- ignorar instrucciones dentro de datos;
- no enviar RAW HTML/JSON masivo;
- no enviar dirección exacta;
- no enviar secrets.

## Cost control

Pipeline:

catálogo → filtros/FTS → reglas → fuzzy candidates → IA solo ambiguos.

Implementar métricas locales:

- calls;
- input/output tokens si API los reporta;
- costo estimado si metadata suficiente;
- cache de respuestas por hash de input+model+promptVersion.

No repetir IA si los inputs no cambiaron.

Permitir:

- AI global on/off;
- features individuales;
- modelo;
- optional monthly budget/soft limit;
- “ask before AI” si se desea en settings advanced, pero default puede ser automático en ambiguos una vez configurado.

## Secret UX

- pegar API key en settings;
- guardar en SecretStore;
- UI solo `Configurada` + máscara parcial segura;
- botón test connection;
- botón remove key.

## Errores

Manejar:

- 401;
- 402/no credits;
- 429;
- 5xx;
- timeout;
- model unavailable;
- malformed JSON;
- safety/refusal.

Siempre fallback.

## Model changes

Guardar `model_id` y `prompt_version` en AI decision metadata para reproducibilidad. Cambiar modelo no invalida overrides humanos.

## Evaluación

Sobre dataset labeled Goal 5:

- ejecutar IA solo sobre conjunto ambiguo;
- medir precisión por clase;
- comparar deterministic-only vs +AI;
- reportar costo aproximado;
- no proclamar 100% sin evidencia.

## UI

- AI status;
- badge “Sugerido por IA” diferenciado de “Exacto EAN”;
- explicación opcional;
- confirm/reject;
- settings model/features/cost.

No saturar UI con branding de modelo.

## Tests

Offline mocked:

- valid structured output;
- malformed;
- 401/402/429/500;
- timeout;
- prompt injection string in product name;
- cache hit;
- AI off;
- user override beats AI.

Live si key disponible:

- connection test;
- 5–20 ambiguous fixtures;
- structured JSON;
- token/cost metadata.

## Entregables

- OpenRouter provider;
- feature services/prompts versionados;
- evaluation report;
- AI settings UI;
- `goal-08-ai-report.md`.

## DoD

- IA OFF passes full core workflows;
- IA ON improves/assists ambiguous flow when key available;
- no secrets leak;
- errors degrade gracefully;
- structured outputs validated;
- PROJECT_STATE updated.

## Continuación

Pasar inmediatamente a GOAL-09 aunque live key test esté BLOCKED_EXTERNAL.


---

# GOAL 09 — Ara parcial y plataforma extensible para nuevas tiendas

## Propósito

Implementar Ara de forma honesta dentro de las capacidades permitidas/validadas y demostrar que la arquitectura soporta nuevas cadenas sin tocar el dominio central.

## Ara — alcance estricto

Diagnóstico clasifica Ara como D. No reabrir ingeniería inversa invasiva.

V1 puede integrar:

- promociones/folletos públicos;
- datos editoriales públicamente accesibles;
- información parcial;
- import/manual override si es útil.

NO afirmar:

- catálogo completo;
- stock completo;
- precio por sucursal;

si la fuente no lo sustenta.

## AraProvider

Implementar capabilities exactas:

- Promotions: SUPPORTED/PARTIAL según fuente.
- SearchProducts: probablemente NOT_SUPPORTED o PARTIAL si solo dataset importado.
- Prices: PARTIAL.
- StoreSpecificPrices: NOT_SUPPORTED.
- etc.

Provider debe funcionar bajo el mismo contrato sin hacks en UI/domain.

## Folletos/promociones

Si se automatiza ingestión pública:

- preservar fuente/fecha/vigencia;
- precio promocional condicionado;
- producto raw;
- normalización conservadora;
- no convertir OCR dudoso en precio confiable sin confidence/review.

Evitar OCR si no hace falta; preferir texto/datos estructurados públicos cuando existan.

## Entrada manual/import

Diseñar una vía sencilla para que usuario pueda añadir/corregir una oferta Ara manualmente si le resulta útil:

- retailer=Ara;
- producto/variant;
- precio;
- fecha;
- source=USER_MANUAL/PUBLIC_PROMO;
- expiration opcional;
- PriceScope=UNKNOWN/STORE solo si usuario lo especifica conscientemente.

No mezclar dato manual con “verificado automáticamente”.

## Capability-aware UI

Comparación debe poder mostrar:

- D1 precio completo;
- Éxito precio completo;
- Ara “Promoción pública / datos parciales”.

No mostrar celda vacía sin explicación ni simular cobertura equivalente.

## Extensibilidad

Crear template/documentación `NewStoreProviderTemplate` o ejemplo sin overengineering.

La guía debe cubrir:

- module layout;
- DTOs;
- mapping;
- capabilities;
- seller semantics;
- PriceScope;
- cache/TTL;
- RAW;
- health;
- contract tests;
- docs.

## Prueba de arquitectura

Para demostrar extensibilidad, crear un provider de prueba ficticio o scaffold compilable (no conectado) siguiendo el template, y confirmar que no requiere modificar domain/optimizer.

No agregar una tienda real extra solo por cumplir este test.

## Abstracciones comunes

Revisar D1/Éxito después de dos providers reales:

- extraer utilidades HTTP/retry/pagination/VTEX solo si hay repetición demostrada;
- evitar una base class gigantesca;
- preferir composable helpers.

## Provider registry

Registrar providers por DI/metadata. Agregar uno nuevo debe implicar módulo+registro, no condicionales esparcidos.

## Settings por provider

Soportar:

- Enabled;
- TTL overrides opcionales;
- marketplace (donde aplique);
- sync policy;
- capabilities read-only.

## Health UI

Mostrar por tienda/capability cuando sea útil:

- HEALTHY;
- DEGRADED;
- DOWN;
- PARTIAL DATA.

## Tests

- Ara partial offer ingestion;
- expired promotion;
- manual entry;
- UI capabilities;
- optimizer no trata `UNKNOWN` como disponible;
- disabled provider;
- new provider scaffold compiles without domain changes;
- D1/Éxito regression.

## Entregables

- AraProvider parcial;
- manual/public promo support si implementado;
- provider template;
- `docs/providers/ADDING_A_PROVIDER.md`;
- capability UI;
- `goal-09-providers-report.md`.

## DoD

- Ara no promete más de lo que sabe;
- D1/Éxito intactos;
- agregar provider está documentado y probado;
- no store-specific conditionals en domain;
- PROJECT_STATE actualizado.

## Continuación

Pasar inmediatamente a GOAL-10.


---

# GOAL 10 — Hardening, UX final, validación y release local 1.0.0

## Propósito

Convertir el sistema funcional de Goals 1–9 en un producto personal terminado, consistente, recuperable, medido y agradable de usar diariamente.

Este es el último goal del roadmap inicial.

## Auditoría integral

Antes de cambiar estética, ejecutar:

- backend build/tests;
- frontend build/tests;
- integration;
- offline contract tests;
- live contract tests disponibles;
- DB integrity;
- dependency audit razonable.

Registrar baseline.

## UI/UX refinement

Revisar todas las pantallas contra `02_UI_UX...md` y `assets/prototipo-ui.png`.

### Pantallas mínimas completas

- Inicio/resultados.
- Mi mercado.
- Comparar.
- Historial.
- Tiendas/providers.
- Configuración simple.
- Configuración avanzada/System status.

### Estados

Cada pantalla relevante debe soportar:

- loading;
- empty;
- stale;
- partial;
- error;
- offline;
- disabled/not configured.

No spinners eternos.

### Responsive

QA desktop y mobile width. La app sigue siendo local PC, pero PWA responsive debe ser sólida.

### Accesibilidad

- keyboard navigation;
- focus visible;
- labels;
- contrast;
- no depender solo de color;
- semantic headings;
- buttons/links correctos.

## Historial

Implementar UX útil:

- historial de precio por ProductVariant/offer context;
- mínimo/máximo/promedio cuando datos suficientes;
- cambios;
- no mezclar scopes/sellers incompatibles en una sola serie sin filtros.

Gráficos solo si aportan claridad; no dependencia pesada innecesaria.

## Observability local

System panel:

- app version;
- DB size;
- RAW size;
- last backup;
- DB integrity;
- provider health;
- last sync;
- Google status;
- OpenRouter status;
- pending external actions.

## Backup/restore UX

- crear backup;
- listar backups;
- restore seguro con confirmación y backup preventivo;
- documentar ruta manual.

Test restore end-to-end en DB temporal antes de habilitar camino destructivo real.

## Export/import

Agregar export útil si encaja sin riesgo:

- CSV histórico/precios;
- JSON/backup de configuración/datos;

Prioridad a backup completo antes que formatos cosméticos.

## Release packaging

Definir experiencia de ejecución local.

Objetivo: usuario pueda iniciar fácilmente en Windows sin recordar cinco comandos.

Opciones a evaluar:

- script PowerShell launcher;
- backend sirviendo frontend compilado;
- otro packaging simple.

Elegir opción más mantenible, no Electron salvo necesidad demostrada.

## Seguridad hardening

- loopback binding;
- CORS;
- secrets scan;
- logs scan;
- dependency vulnerabilities;
- inputs/URLs;
- path traversal RAW/backup;
- no HTML unsafe;
- prompt injection fixtures;
- sensitive error handling.

## Performance

Medir con dataset ampliado/realista:

- local search;
- comparison;
- basket optimization;
- startup;
- sync batches;
- DB size/history.

Crear índices solo donde medición los justifique.

## Data maintenance hardening

- RAW retention;
- logs retention;
- PRAGMA optimize;
- backup retention;
- product lifecycle;
- orphan cleanup seguro;
- no borrar conocimiento canónico/override.

## External validation

### Physical price validation

Scaffold/archivo ya previsto. Si el usuario ha aportado observaciones físicas, analizarlas.

Si no:

- NO bloquear release;
- mantener `BLOCKED_EXTERNAL`;
- generar checklist exacto para 5–10 productos D1 y Éxito;
- la UI mantiene PriceScope honesto.

### Google/OpenRouter

Si keys disponibles:

- live final smoke.

Si no:

- release puede completar con integración configurada pero `NOT_CONFIGURED`;
- documentar pasos humanos.

## E2E obligatorios

1. Primer inicio DB limpia.
2. Crear/editar perfil configuración.
3. D1 search/sync.
4. Éxito search/sync.
5. Crear cesta.
6. Exact/similar/substitute comparison.
7. Optimizar max N.
8. Reiniciar y persistencia.
9. Offline/last-known-good.
10. Backup y restore test.
11. Google configured path si disponible / unconfigured fallback.
12. OpenRouter configured path si disponible / AI-off fallback.
13. Ara partial state.

## Regression gates

Nada de Goal 10 puede romper:

- D1;
- Éxito;
- optimizer;
- matching;
- AI-off mode;
- local-only privacy.

## Documentación final

Actualizar README con:

- requisitos;
- instalación;
- inicio;
- datos locales;
- backup/restore;
- configuración Google;
- configuración OpenRouter;
- uso sin IA;
- providers;
- troubleshooting.

Crear:

`docs/development/FINAL_PROJECT_REPORT.md`

Debe contener:

- versión;
- arquitectura final;
- funcionalidades;
- providers/capabilities;
- tests/resultados;
- performance;
- external blockers;
- limitaciones conocidas;
- pasos de ejecución;
- rutas de datos;
- backup;
- próximos posibles enhancements.

## Version

Establecer 1.0.0 solo si DoD global se cumple. Si existen fallos técnicos internos relevantes, usar prerelease y corregir antes de cerrar.

Bloqueos puramente externos (key no suministrada, validación física) pueden coexistir con 1.0.0 si la integración está completa y el producto funciona sin ellos, siempre documentados.

## Limpieza

- eliminar demo data accidental de release;
- no eliminar fixtures de test;
- resolver TODOs críticos;
- limpiar código muerto;
- format/lint;
- verificar `.gitignore`;
- secretos scan.

## Final DoD

Cumplir `docs/quality/DEFINITION_OF_DONE.md` completo.

Ejecutar suite completa y guardar resultados en final report.

Actualizar `PROJECT_STATE.md`:

- Goals 01–10 complete/complete_with_external_blockers;
- global status COMPLETE;
- lista de acciones humanas pendientes exacta.

## Condición de terminación

SOLO después de completar este DoD, Claude puede finalizar la tarea global.

No terminar con “Goal 10 terminado” solamente. Entregar un resumen final del proyecto completo, comandos de ejecución y bloqueos externos restantes.


---



---

# GOAL 11 — Auditoría correctiva, integración real y Release Candidate 1

Ver archivo completo de especificación: goals/GOAL-11-RC1-AUDITORIA-CORRECTIVA.md
Informe de cierre y evidencia: docs/development/GOAL-11-RC1-AUDIT-FIX-REPORT.md

Estado: **COMPLETE** (Release Candidate 1 integrado y verificado; dependencias externas de producción marcadas honestamente como BLOCKED_EXTERNAL).
Decisión final: **READY FOR 1.0.0**


---

# GOAL 12 — Integridad de Contexto, Ubicación Real y RC2

Ver archivo completo de especificación: goals/GOAL-12-CONTEXT-INTEGRITY-RC2.md
Informe de cierre y evidencia: docs/development/GOAL-12-CONTEXT-INTEGRITY-RC2-REPORT.md

Estado: **COMPLETE** (Integridad estricta de contexto comercial; ofertas de otra región nunca ganan; LocationProfile estructurado).
Decisión final: **READY FOR 1.0.0**

---

# GOAL 13 — Certificación de Integración Final y RC3

Ver archivo completo de especificación: goals/GOAL-13-FINAL-INTEGRATION-CERTIFICATION.md
Informe de cierre y evidencia: docs/development/GOAL-13-FINAL-INTEGRATION-CERTIFICATION-REPORT.md

Estado: **COMPLETE** (Preservación de sesión D1; integración total de facades de frontend; TravelLocation previo a scoring).
Decisión final: **READY FOR 1.0.0**

---

# GOAL 14 — Lanzamiento Final v1.0.0 y Despliegue Local

Ver archivo completo de especificación: goals/GOAL-14-FINAL-V1-RELEASE.md
Informe de cierre y evidencia: docs/development/GOAL-14-FINAL-V1-RELEASE-REPORT.md

Estado: **COMPLETE** (Google Maps 100% opcional con Haversine y Zero-Key; OmniRouter predeterminado con DPAPI; ejecutables nativos Windows).
Decisión final: **READY FOR 1.0.0**

---

# GOAL 15 — Descubrimiento de Productos, Catálogo Ecommerce y Asistente IA

Ver archivo completo de especificación: goals/GOAL-15-PRODUCT-DISCOVERY-CATALOG-LISTS-AI-UX.md
Informe de cierre y evidencia: docs/development/GOAL-15-PRODUCT-DISCOVERY-CATALOG-LISTS-AI-UX-REPORT.md

Estado: **COMPLETE** (Búsqueda y catálogo independientes de listas; CRUD de listas; Asistente IA flotante; UX ecommerce).
Decisión final: **READY FOR 1.0.0**

---

# GOAL 16 — Gestión de Datos, Análisis Inteligente, OmniRouter y Expansión de Supermercados

Ver archivo completo de especificación: goals/GOAL-16-DATA-ANALYTICS-RETAILERS.md
Informe de cierre y evidencia: docs/development/GOAL-16-DATA-ANALYTICS-RETAILERS-REPORT.md

Estado: **COMPLETE** (OmniRouter verificado en vivo con DPAPI; módulo de Análisis con 3 pestañas; herramientas de base de datos y copias de seguridad; zona de peligro protegida; expansión a 8 cadenas de supermercados).
Decisión final: **READY FOR 1.0.0**

---

# GOAL 17 — Rediseño UX/UI Profesional, Skill Discovery y Certificación Visual

Ver archivo de especificación: goals/GOAL-17-UX-UI-REDESIGN.md (o pasted-text-1.txt)
Informe de cierre y evidencia: docs/development/GOAL-17-UX-UI-REDESIGN-REPORT.md y docs/ui-audit/COMPARISON.md

Estado: **COMPLETE** (Auditoría Playwright de 53 capturas Desktop/Mobile; tokens centralizados en styles.css; componentes rediseñados; 364/364 backend tests, 58/58 frontend tests).
Decisión final: **PASS**

---

# GOAL 18 — UX Completion, Identidad Profesional y Polish Final

Ver archivo completo de especificación: docs/development/GOAL-18-UX-COMPLETION-PROFESSIONAL-IDENTITY-REPORT.md
Evidencia de capturas: docs/ui-audit/goal18/ y Mercado_Inteligente_UI_Audit_Goal18.zip

Estado: **COMPLETE** (Favicon e iconografía de marca propia en SVG/ICO/PNG; 53/53 capturas con 0 duplicados SHA-256; tabs de Ajustes aisladas y deep-linkables; erradicación del AI-look; métricas internas F1/Recall colapsadas; 364/364 backend tests, 58/58 frontend tests).
Decisión final: **READY FOR PRODUCTION**


---

# GOAL 18.1 — Integridad de Búsqueda, Paginación y Precios Unitarios

Informe de cierre y evidencia: docs/development/GOAL-18-1-SEARCH-DATA-INTEGRITY-REPORT.md

Estado: **COMPLETE** (Paginación exacta con totalCount/totalPages/hasPrevious/hasNext; precios unitarios estandarizados $20.000/kg, $5.000/L, $1.500/unidad y modo WEIGHT_BASED; normalización de 500g sin mostrar 0.5g; protección de scraping con localOnly: true en paginación/filtros; 376/376 backend tests, 63/63 frontend tests).
Decisión final: **READY FOR PRODUCTION**

---

# GOAL 19 — Transformación Visual Integral, Identidad y Microinteracciones

Ver archivo completo de especificación: goals/GOAL_19_TRANSFORMACION_VISUAL_MERCADO_INTELIGENTE.md
Informe de cierre y evidencia: docs/development/GOAL-19-VISUAL-IDENTITY-MOTION-REPORT.md
Evidencia de capturas: docs/ui-audit/goal19/ y Mercado_Inteligente_UI_Audit_Goal19.zip

Estado: **COMPLETE** (Integración de assets oficiales de Mercado_Inteligente_Visual_Identity_Pack_v1.zip: logo maestro, mark vectorial/plano, favicons/PWA, ilustraciones temáticas para estados vacíos y placeholder sin cliché; UI Kit completo en /dev/ui-kit con 17 secciones; microinteracciones y motion accesible con soporte total prefers-reduced-motion; locale es-CO registrado; 376/376 backend tests, 66/66 frontend tests, build de producción limpia, 53/53 capturas visuales únicas).
Decisión final: **READY FOR PRODUCTION**
