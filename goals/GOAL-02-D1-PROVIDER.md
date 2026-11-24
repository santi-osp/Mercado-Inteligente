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
