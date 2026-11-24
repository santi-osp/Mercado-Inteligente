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
