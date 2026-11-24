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
