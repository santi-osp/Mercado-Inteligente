# Informe de Desarrollo — GOAL-06: Cesta, estrategias y optimizador máximo-N

## 1. Resumen Ejecutivo

El objetivo **GOAL-06** implementa el motor de optimización multi-tienda y gestión avanzada de cestas para **Mercado Inteligente**.

Este subsistema resuelve el problema central del comprador: encontrar la combinación óptima de hasta $N$ tiendas ($1 \le N \le 5$) que minimice el costo total de la cesta familiar, maximice la cobertura de productos y balancee el esfuerzo y costo de desplazamiento entre establecimientos físicos o canales de entrega en el contexto retail colombiano (D1, Éxito, etc.).

El motor opera bajo un pipeline determinista de alto rendimiento diseñado para escalar a más de 100 ítems en catálogo de 10+ tiendas en menos de 500 ms sin dependencias de IA en el camino crítico, con estricta aritmética monetaria decimal y respeto riguroso a las condiciones de venta (promociones 2x1, 3x2, % en segunda unidad, advertencias de medios de pago condicionales, frescura y alcance de precios).

---

## 2. Componentes Arquitectónicos

### 2.1 Modelo de Datos y Contratos de Cesta Avanzada

1. **Atributos de Ítem de Cesta (`BasketItem.cs`)**:
   - `Query`: Consulta textual o denominación del producto.
   - `Quantity` y `Unit`: Cantidad deseada y unidad de medida opcional.
   - `Dimension`: Dimensión física (`Mass`, `Volume`, `Count`, `Length`, `Unknown`).
   - `PreferredBrand`: Marca de preferencia; el motor prioriza candidatos de esta marca en empates de precio o equivalencia.
   - `ExcludedBrand`: Marca vetada por el usuario; excluye candidatos de dicha marca de forma determinista.
   - `AllowSubstitutesOverride`: Permite o restringe sustitutos funcionales para este ítem específico.
   - `SizeTolerancePercentOverride`: Tolerancia dimensional específica para el ítem.

2. **Opciones y Estrategias de Optimización (`OptimizationStrategy.cs` & `OptimizerExecutionOptions.cs`)**:
   - `MaxSavings`: Minimiza el costo total monetario estricto dentro de la cuota de $N$ tiendas permitidas.
   - `SingleStore`: Limita la compra a exactamente 1 sola tienda (utilizada también como baseline comparativo de ahorro).
   - `Balanced`: Minimiza una función de costo compuesto:
     $$\text{Score} = \text{Costo Total} + \max(0, \text{Tiendas Usadas} - 1) \times \text{Penalidad Tienda}$$
     con penalidad configurable (por defecto $5,000 COP por tienda adicional).
   - `MinComplexity`: Prioriza minimizar el número de tiendas visitadas manteniendo la máxima cobertura posible, desempatando por costo.

### 2.2 Generación de Candidatos e Indexación Invertida (`CandidateGenerationService.cs`)

1. **Preprocesamiento Único de Ofertas (`PreprocessedOffer`)**:
   - Extrae cantidades normalizadas (`StandardQuantity`), información canónica (`NormalizedProductInfo`), frescura del precio/stock (`Freshness.Fresh`, `Stale`, `Outdated`) y precio unitario base ($/kg, $/L, $/und) antes de las iteraciones de la cesta.

2. **Índice de Catálogo (`CatalogIndex`)**:
   - Indexa ofertas por categoría funcional (`ProductType`), por código EAN/GTIN y por tokens léxicos no genéricos.
   - Para productos clasificados en la categoría genérica "Abarrotes", evalúa tokens con frecuencia inferior al 30% del catálogo para recortar decenas de miles de comparaciones en O(1) búsquedas dirigidas.

3. **Filtros de Disponibilidad y Reglas de Negocio**:
   - Exclusión de ofertas `OutOfStock` o `NotSold`. Nunca se asume `UNKNOWN` como disponible para compra automatizada.
   - Exclusión de vendedores aliados (`SellerKind.Marketplace`) salvo activación explícita de `IncludeMarketplace`.
   - Validación de compatibilidad dimensional física: prohibición absoluta de conversiones volumen ↔ masa sin densidad física justificada.
   - Control de sobrecompra (`OverbuyPercent`): si el paquete más pequeño supera el umbral máximo de sobrecompra configurado (p. ej. 20%), el candidato es clasificado como sobrecompra rechazada (`IsOverbuyRejected`) y solo es considerado si no existe ninguna alternativa viable.

### 2.3 Evaluador Determinista de Promociones (`PromotionEvaluator.cs`)

1. **Promociones Multi-Unidad y Escalonadas**:
   - Promociones tipo $N \times M$ (p. ej., "Pague 2 Lleve 3", "3x2", "2x1"): calcula los ciclos completos de promoción y cobra las unidades restantes a precio estándar.
   - Descuentos en la segunda unidad (p. ej., "Segunda unidad con 50% de descuento", "70% dcto en 2da unidad"): aplica el descuento a pares de productos.
   - Descuentos directos porcentuales (p. ej., "20% dcto", "15% de descuento").

2. **Advertencias de Medios de Pago Condicionales**:
   - Detección de promociones atadas a medios de pago específicos (p. ej., "Tarjeta Éxito", "Tarjeta Carulla", "Puntos Colombia", "CMR").
   - Generación de advertencias contextuales (`PromotionWarning` / `ConditionWarning`) sin aplicar el descuento de forma indiscriminada si el medio de pago no está garantizado.

### 2.4 Optimizador Combinatorio Zero-Allocation (`BasketOptimizer.cs`)

1. **Puntuación Rápida de Subconjuntos (`ScoreSubset`)**:
   - Para evaluar todas las combinaciones de tiendas $\binom{M}{K}$ con $K \le 5$, el optimizador realiza un pase de puntuación libre de asignaciones de memoria en heap.
   - Mantiene una máscara de bits (`int usedMask`) y calcula las tiendas efectivamente utilizadas mediante operaciones intrínsecas de hardware (`BitOperations.PopCount`), acumulando costo y cobertura directamente.

2. **Materialización del Subconjunto Ganador (`MaterializeSubset`)**:
   - Únicamente la combinación óptima seleccionada según la estrategia solicitada es instanciada en estructuras completas de asignación (`OptimizerItemAssignment`) y cubos por tienda (`OptimizerStoreBucket`).

3. **Cálculo de Ahorro frente al Baseline**:
   - Evalúa automáticamente la mejor solución de 1 sola tienda (`SingleStore`) con la misma cobertura para calcular el ahorro neto real generado por la compra multi-tienda:
     $$\text{Ahorro} = \max(0, \text{Costo Baseline Monotienda} - \text{Costo Solución Multi-tienda})$$

4. **Sugerencia de Expansión $N+1$ (`OptimizerSuggestion`)**:
   - Si la solución para $N$ tiendas deja productos sin cobertura y existen tiendas adicionales en el catálogo ($N < 5$), el optimizador evalúa el escenario $N+1$.
   - Si $N+1$ tiendas logran cubrir productos adicionales, genera una recomendación transparente con el costo marginal y productos adicionales cubiertos.

5. **Evaluación y Alertas Presupuestarias (`BudgetEvaluation`)**:
   - Si la cesta cuenta con un presupuesto definido y el costo total lo supera, calcula la discrepancia y propone automáticamente sustitutos de menor costo disponibles en el catálogo.

---

## 3. Interfaz de Usuario y API

### 3.1 Endpoints HTTP en ASP.NET Core (`BasketsController.cs`)

- `POST /api/v1/baskets/{id}/optimize`: Ejecuta la optimización sobre una cesta persistida utilizando el catálogo de ofertas locales y las opciones de ejecución enviadas en el body.
- `POST /api/v1/baskets/optimize-transient`: Permite optimizar una cesta efímera enviada directamente en el payload JSON sin necesidad de persistencia previa.

### 3.2 Experiencia en Angular PWA ("Optimizar")

- Selector interactivo de estrategia: *Máximo Ahorro*, *Tienda Única*, *Balanceada*, *Menor Complejidad*.
- Selector numérico de tiendas permitidas (1 a 5).
- Desglose por tienda con subtotales, lista de artículos asignados y justificación del matching.
- Tarjeta de ahorro neto comparativo frente al escenario de compra en una sola tienda.
- Panel de productos faltantes con razones claras de no disponibilidad.
- Alertas de frescura y avisos de precios online/regionales no verificados en góndola física.

---

## 4. Batería de Pruebas y Certificación

La suite de pruebas para el optimizador (`BasketOptimizerTests.cs`) consta de 11 pruebas unitarias y de rendimiento que cubren la totalidad de los requerimientos funcionales:

| Prueba | Descripción | Resultado |
|---|---|---|
| `SingleStoreStrategyPicksStoreWithBestCoverageAndCost` | Verifica la selección óptima de una sola tienda para el baseline monotienda. | **PASS** |
| `MaxSavingsStrategySplitsAcrossStoresForCheapestTotal` | Comprueba la partición inteligente entre tiendas para alcanzar el costo mínimo. | **PASS** |
| `BalancedStrategyPenalizesExtraStores` | Valida que la penalidad por tienda evite divisiones triviales donde el ahorro no justifica el desplazamiento. | **PASS** |
| `MinComplexityStrategyPrefersFewerStores` | Comprueba la preferencia por el menor número de tiendas a igual cobertura. | **PASS** |
| `PromotionsAreCorrectlyFactoredIntoTotalCost` | Valida la aplicación de promociones 2x1 y 3x2 en el costo total final. | **PASS** |
| `ConditionalPaymentPromotionProducesWarning` | Asegura que ofertas atadas a tarjetas de crédito emitan alertas claras. | **PASS** |
| `SubstituteIsUsedWhenExactIsMissingAndAllowed` | Comprueba la asignación de sustitutos ante ausencia de coincidencia exacta. | **PASS** |
| `MissingItemsAreExplicitlyReported` | Verifica el reporte transparente de ítems no encontrados en las tiendas evaluadas. | **PASS** |
| `BudgetEvaluationDetectsOverspendAndSuggestsCheaper` | Valida la detección de sobrecosto sobre presupuesto y sugerencias de ahorro. | **PASS** |
| `PlusOneStoreSuggestionAppearsWhenCoverageIncreases` | Comprueba la emisión de sugerencia $N+1$ cuando permite cubrir ítems faltantes. | **PASS** |
| `PerformanceScaleBenchmark100Items10StoresUnder500ms` | Evalúa el rendimiento a escala: 100 ítems en 10 tiendas dentro del umbral estricto (< 500 ms). | **PASS** |

### Métricas de Validación Global

- **Backend .NET 10**: 205 de 205 pruebas unitarias y de integración superadas exitosamente (`dotnet test`).
- **Frontend Angular 22**: 35 de 35 pruebas unitarias superadas (`npm test`).
- **Build de producción**: Cero errores y cero advertencias tanto en backend como frontend.

---

## 5. Verificación de Restricciones No Negociables

1. **Aritmética Monetaria**: El optimizador utiliza el tipo primitivo `decimal` de C# en la totalidad de sus cálculos de costos, subtotales, penalidades y ahorros; se prohíbe el uso de `float` o `double`.
2. **Ausencia de IA en el Camino Crítico**: Toda la lógica de optimización, indexación, coincidencia y asignación opera mediante algoritmos combinatorios deterministas.
3. **Manejo Riguroso de Inventario**: Solo las ofertas explícitamente en stock son adquiribles. Las ofertas agotadas o con señal insuficiente nunca se asignan.
4. **Distinción de Precios**: Se emite advertencia explícita en la solución si alguna oferta asignada corresponde a canales online o fulfillment regional no verificado en góndola física.
5. **Preservación de Evidencia**: Ninguna optimización altera ni elimina datos históricos ni registros RAW.

---

## 6. Estado y Próximos Pasos

Con la totalidad del motor de optimización, la integración de promociones, las 4 estrategias implementadas y el benchmark a escala superado, **GOAL-06 queda certificado como COMPLETE**.

Se procede de inmediato con **GOAL-07: Routing, geocoding y estimación de desplazamiento**, implementando la arquitectura de geolocalización y cálculo de distancias/tiempos de viaje con aislamiento estricto de secretos y soporte para proveedores locales y externos.
