# Resumen consolidado del diagnóstico técnico

## Decisión

- `PROJECT STATUS`: READY FOR DEVELOPMENT
- `GENERAL FEASIBILITY`: GO WITH LIMITATIONS
- riesgo global: MEDIUM

El diagnóstico ya debe considerarse cerrado. No crear un tercer diagnóstico general salvo evidencia nueva que bloquee implementación.

## D1

Clasificación aproximada: **B**.

Validado:

- catálogo/productos accesibles de forma estructurada;
- EAN/SKU en buena proporción;
- precios;
- contexto de Medellín reproducible por HTTP;
- flujo relacionado con sesión/order form/simulation/segmento-región;
- un precio puede cambiar con contexto geográfico.

Limitación:

El contexto demostrado corresponde mejor a región/fulfillment que a precio físico exacto de una sucursal. Utilizar `PriceScope=REGION/FULFILLMENT_REGION`, no afirmar “precio físico D1 Laureles” sin validación adicional.

## Éxito

Clasificación aproximada: **B**.

Validado:

- FastStore/SearchQuery como vía más estable para cotización contextual;
- contexto Medellín mediante channel/salesChannel/regionId;
- sellers múltiples;
- seller comercial, fulfillment interno y marketplace deben diferenciarse;
- precios/stock pueden variar por contexto;
- HTTP directo reproducible.

Regla crítica:

No elegir el mínimo de todos los sellers. Marketplace debe excluirse por defecto. Conservar seller chain y diferenciar seller comercial de fulfillment.

## Ara

Clasificación: **D**.

La aplicación pública de Ara ofrece experiencias de producto/precio, pero no se identificó una fuente adecuada para automatización del catálogo completo dentro de los límites planteados. Los términos revisados desaconsejan técnicas de acceso/copia/monitorización de la aplicación.

V1:

- promociones/folletos públicos;
- información parcial;
- eventualmente entrada/manual/import;
- no simular catálogo completo.

## SQLite v2

Aprobada.

El diseño inicial duplicaba RAW y escalaba mal (~59 MB con unas centenas de productos). El rediseño validó:

- SQLite para datos normalizados/histórico/config;
- RAW fuera de SQLite;
- gzip;
- hash/deduplicación;
- `ExternalProduct → ExternalSku → Offer`;
- `CanonicalProduct → ProductVariant`;
- integridad y foreign keys correctas en la prueba.

## Matching

- EAN y reglas fuertes: base de exact matching.
- fuzzy: genera candidatos, NO prueba identidad.
- dataset del diagnóstico mostró falsos positivos incluso con similitud alta.
- `MatchOverride` humano es necesario.
- substitute matching está listo para desarrollo, no “resuelto perfectamente”.

## Optimizador

Validado matemáticamente para máximo N tiendas a escala del producto personal. Mantener separados:

1. selección de variante/producto;
2. selección de tiendas;
3. orden de ruta.

## Ubicación

Arquitectura viable. Recomendación del proyecto:

- Google Geocoding API.
- Google Routes API.
- interfaces desacopladas.
- no requiere mapa interactivo embebido en V1.

La API key todavía debe ser creada/configurada.

## IA

No requerida. OpenRouter será una capa BYOK opcional para:

- normalización difícil;
- candidatos ambiguos;
- sustitutos;
- análisis/explicación.

Nunca usar IA como fuente de precio, stock o cálculo matemático básico.

## Validación física pendiente

Todavía no existe evidencia suficiente para equiparar ecommerce con góndola física. La app debe ser honesta con `PriceScope`.

Durante hardening se recomienda una muestra humana de 5–10 productos por cadena principal cuando sea práctico, pero esta acción externa no bloquea el desarrollo técnico.
