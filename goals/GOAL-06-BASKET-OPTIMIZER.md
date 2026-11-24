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
