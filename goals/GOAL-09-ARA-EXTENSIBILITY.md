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
