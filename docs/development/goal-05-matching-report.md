# Informe de Desarrollo — GOAL-05: Normalización, matching, equivalencias y comparación de presentaciones

## 1. Resumen Ejecutivo

El objetivo **GOAL-05** implementa el subsistema integral de normalización de productos, extracción de atributos y variantes, desambiguación dimensional y matching determinista multi-etapa para el proyecto **Mercado Inteligente**.

Este módulo elimina la dependencia de aproximaciones heurísticas no explicables y erradica el riesgo de falsos positivos en comparaciones de precios entre cadenas de retail colombianas (particularmente D1 y Éxito). Se implementó un modelo de dimensiones físicas estrictas (Masa, Volumen, Conteo, Longitud) con prohibición explícita de conversiones transdimensionales injustificadas, soporte completo de multipacks (`6 x 1 L`, `12 rollos x 40 m`, `2x900 ml`), detección de variantes críticas alimentarias (Zero/Sugar-free, Deslactosada, Grado de huevos AA/AAA, Tipo de aceite, Medio de atún), un pipeline de matching determinista en 6 etapas con prevalencia de anulaciones del usuario (`MatchOverride`), cálculo de precio por unidad de medida base ($/kg, $/L, $/und, $/m), modelado de sobrecompra en cestas, un dataset de evaluación con 16 pares de prueba reales con 0 falsos positivos para coincidencias exactas, endpoints HTTP de evaluación, comparación e historial de anulaciones, y la pantalla interactiva "Comparar" en la PWA de Angular con navegación por pestañas y calculador de sobrecompra en vivo.

---

## 2. Componentes Implementados

### 2.1 Backend (.NET 10 / C# 14 / EF Core 10)

1. **Parser de Cantidades y Multipacks (`QuantityParser.cs`)**:
   - Clasificación por dimensión estricta: `Dimension.Mass` (base: kg), `Dimension.Volume` (base: L), `Dimension.Count` (base: count), `Dimension.Length` (base: m).
   - Detección de patrones multipack:
     - Formato multiplicación: `6 x 1 L`, `12 x 500 ml`, `2x900 ml`.
     - Formato prefijo/sufijo: `x12`, `pack 4`, `sixpack`, `tripack`, `duopack`.
     - Formato compuesto: `12 rollos x 40 m` (dimensión Length: 480 m totales, 12 unidades).
   - Cálculo de ratio de diferencia relativa de tamaño:
     $$\text{Ratio} = \frac{|A - B|}{\max(A, B)}$$
   - Prohibición estricta de conversión entre dimensiones distintas (`AreDimensionsCompatible`).

2. **Normalización de Atributos y Marcas (`ProductNormalizer.cs` & `NormalizedProductInfo.cs`)**:
   - Detección canónica de marcas de retail colombiano:
     - Marcas comerciales líderes: Coca-Cola, Alpina, Colanta, Alquería, Diana, Roa, Van Camp's, Familia, Premier, etc.
     - Marcas propias D1: Latti, De La Cuesta, Kythos, Redondos, Bruma, Rendy, etc.
     - Marcas propias Éxito: Taeq, Ekono, Frescampo, etc.
   - Categorización funcional: Leche, Arroz, Huevos, Aceite, Pasta, Atún, Papel Higiénico, Café, etc.
   - Detección de variantes críticas:
     - Sin azúcar / Zero / Diet.
     - Deslactosada / Descremada / Semidescremada / Entera.
     - Calibre de huevos: AAA, AA, A, B.
     - Tipo de aceite: Oliva, Girasol, Vegetal, Canola.
     - Medio de cobertura de atún: Agua vs Aceite.

3. **Motor de Matching Determinista (`DeterministicMatcher.cs`)**:
   - 6 etapas ordenadas por prioridad:
     1. **Prevalencia de Anulación Manual (`MatchOverride`):** Decisión del usuario con confianza 1.0.
     2. **Coincidencia EAN con Detección de Conflictos:** Detección de EANs sucios o reciclados. Si hay conflicto de variante, dimensión o tamaño extremo, se degrada a `Candidate` para revisión; si no hay conflictos, se califica como `Exact` (0.99).
     3. **Regla Determinista Exacta:** Misma marca, categoría y variante + diferencia de tamaño $< 2\%$. Califica como `Exact` (0.95).
     4. **Evaluación de Sustitutos:** Misma categoría, dimensiones compatibles, sin conflicto crítico de variante + diferencia de tamaño $\le 15\%$. Califica como `Substitute` (0.80).
     5. **Ranking de Candidatos (Fuzzy Retrieval):** Similitud Jaccard $\ge 0.40$ sobre tokens normalizados. Califica estrictamente como `Candidate` ($\le 0.70$), nunca promovido a `Exact`.
     6. **No Equivalente:** Descarte seguro sin inferencias engañosas.

4. **Comparación de Precios por Unidad y Sobrecompra (`UnitComparisonService.cs`)**:
   - Normalización de precio por unidad base ($/kg, $/L, $/und, $/m).
   - Evaluación de sobrecompra en cestas: cálculo de paquetes requeridos para satisfacer una meta (`Math.Ceiling(target / itemQty)`), costo total del desembolso, porcentaje de exceso y descarte si supera el umbral configurado (ej. 20%).

5. **Dataset de Evaluación y Métricas de Benchmark (`MatchingEvaluationService.cs`)**:
   - 16 pares anotados de productos reales D1 y Éxito.
   - 5 pares exactos, 5 sustitutos, 5 no equivalentes, 1 candidato con conflicto de EAN.
   - Métricas: 100% Precisión en Exact, 100% Cobertura en Exact, F1 Score 1.0, **0 falsos positivos para Exact**.

6. **Endpoints HTTP de Matching y Equivalencias (`Program.cs`)**:
   - `GET /api/v1/matching/evaluate`: reporte completo de evaluación del benchmark con métricas y detalle de pares.
   - `GET /api/v1/matching/pairs`: pares clasificados por pestañas (`exactos`, `similares`, `otrasMarcas`, `all`) y resumen estadístico.
   - `POST /api/v1/matching/compare`: comparador ad-hoc entre dos nombres y precios con cantidad meta.
   - `GET /api/v1/matching/overrides`: listado de anulaciones manuales persistidas en SQLite.
   - `POST /api/v1/matching/overrides`: creación/actualización de anulación de usuario.
   - `DELETE /api/v1/matching/overrides/{id}`: eliminación de anulación de usuario.

---

### 2.2 Frontend (Angular 21 PWA)

1. **Pantalla "Comparar" (`app.html`, `app.ts`, `app.css`)**:
   - Pestañas de visualización: **Exactos**, **Tamaños similares**, **Otras marcas / Diferencias**.
   - Tarjetas de pares evaluados con explicabilidad contextual (EAN, variante, tolerancia de tamaño), distintivos visuales (`badge-exact`, `badge-substitute`, `badge-noteq`, `badge-candidate`), confianza numérica y estado de evaluación.
   - Botones de acción rápida para el usuario: "Mismo producto" (`Exact`), "Sustituto" (`Substitute`), "No comparar" (`NotEquivalent`).
   - Métricas consolidadas en tarjetas de resumen del benchmark (100% Precisión, 0 Falsos Positivos, Cobertura, F1).
   - **Comparador interactivo en vivo**: formulario para contrastar dos productos con sus precios y meta de cantidad, detallando precio unitario ($/kg o $/L) y evaluación de sobrecompra con advertencia cuando se excede el umbral.

2. **Pruebas Unitarias del Frontend (`app.spec.ts`)**:
   - Navegación a "Comparar" y carga de pares clasificados con cambio reactivo de pestañas.
   - Persistencia de anulaciones manuales de usuario (`POST /api/v1/matching/overrides`) con confirmación en pantalla.
   - Evaluación en vivo de presentaciones con desglose de precios unitarios y cálculo de sobrecompra.

---

## 3. Verificación de Calidad y Pruebas

### 3.1 Backend (.NET 10)
- **Comando:** `dotnet test --nologo`
- **Resultado:** **194 pruebas pasadas**, 0 fallidas, 0 omitidas.
- **Pruebas clave de GOAL-05:**
  - `CocaColaOriginal_Vs_Zero_IsNotExactMatch` (verificación de conflicto crítico de azúcar).
  - `OneLiter_Vs_ThousandMilliliters_IsEquivalentSize` (equivalencia 1 L = 1000 ml).
  - `NineHundredGrams_Vs_OneKilogram_IsSimilarWithinTolerance` (tolerancia 10% $\le$ 15%).
  - `FiveKilograms_NotAccepted_IfOverbuyExceedsThreshold` (sobrecompra excesiva rechazada).
  - `SameEan_WithCriticalVariantConflict_DoesNotAutoMatchExact` (EAN con conflicto degradado a revisión).
  - `HighFuzzySimilarity_WithDifferentVariant_IsNotExact` (similitud léxica no genera Exact si hay conflicto de lactosa).
  - `UserOverride_DoNotMatch_WinsOverAnyRuleOrEan` (anulación manual de usuario tiene precedencia total).
  - `Multipack_SixByOneLiter_ParsesCorrectlyAndMatchesSixLiters` (multipack $6 \times 1\text{ L} = 6\text{ L}$).
  - `EggGrade_AA_Vs_AAA_IsSubstitute_NotExact` (calibres de huevo distintos son sustitutos, nunca exactos).
  - `Grams_Vs_Milliliters_CannotBeComparedAcrossBaseUnit` (rechazo de masa vs volumen).
  - `UnitComparisonService_CalculatesPricePerUnitCorrectly` (cálculo correcto $/kg).
  - `BenchmarkDataset_Evaluation_ProducesZeroFalsePositivesForExact` (0 falsos positivos para Exact).
  - `ToiletPaper_MultipackWithMeters_CalculatesCorrectly` (papel higiénico $12\text{ rollos} \times 40\text{ m} = 480\text{ m}$).

### 3.2 Frontend (Angular 21 / Vitest)
- **Comando:** `npm test -- --watch=false`
- **Resultado:** **35 pruebas pasadas**, 0 fallidas, 0 omitidas.
- **Compilación de producción:** `npm run build` ejecutada con éxito en 2.04 segundos con **0 advertencias** y cumplimiento estricto de presupuestos de tamaño.

---

## 4. Estado de Cumplimiento del Definition of Done

- [x] Modelo de cantidades multidimensional estricto (Masa, Volumen, Conteo, Longitud).
- [x] Prohibición de conversión transdimensional injustificada (g $\leftrightarrow$ ml).
- [x] Extracción de atributos críticos (azúcar, lactosa, grasas de leche, grado de huevos, tipo de aceite, medio de atún).
- [x] Parser de multipacks y presentaciones complejas.
- [x] Pipeline determinista de 6 etapas con prevalencia de anulaciones del usuario.
- [x] Cálculo transparente de precios por unidad de medida base ($/kg, $/L, $/und, $/m).
- [x] Modelado de sobrecompra en cestas con descarte por umbral.
- [x] Dataset de evaluación con 16 pares reales D1 y Éxito anotados.
- [x] Métrica de evaluación formal: 0 falsos positivos para Exact, 100% precisión.
- [x] Endpoints HTTP de matching, pares clasificados, evaluación y anulaciones de usuario.
- [x] Pantalla "Comparar" en Angular PWA con navegación por pestañas (Exactos, Tamaños similares, Otras marcas), badges de explicabilidad, y comparador interactivo.
- [x] Todas las pruebas automatizadas de backend y frontend pasando limpiamente.
