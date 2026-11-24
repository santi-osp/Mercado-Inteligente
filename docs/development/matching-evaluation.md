# Evaluación y Benchmark del Motor de Matching Determinista

## 1. Propósito y Principios

El módulo de matching de **Mercado Inteligente** resuelve el problema de equiparar productos y presentaciones entre diferentes cadenas de retail colombianas (D1, Éxito, Carulla, Ara, etc.) bajo reglas rigurosas, reproducibles y explicables.

### Principios Fundamentales:
1. **Precisión Absoluta sobre Cobertura en Coincidencias Exactas:**
   - La tasa de falsos positivos para la categoría `Exact` debe ser **estrictamente cero (0)**.
   - Es preferible clasificar una equivalencia como `Substitute` o `Candidate` para revisión que asociar erróneamente dos productos como idénticos.
2. **Sin Inferencia de Identidad por Similitud Léxica:**
   - Algoritmos como Jaccard o Levenshtein **nunca confirman identidad por sí solos**. Se emplean únicamente para ranking de candidatos en revisión (`Candidate`), con puntuaciones acotadas.
3. **No Conversión Transdimensional Injustificada:**
   - Masa (kg, g) y Volumen (L, ml) pertenecen a dimensiones físicas no intercambiables ($\text{densidad} \ne 1$). Queda prohibido convertir gramos a mililitros sin justificación física/química explícita.
4. **Prioridad del Usuario:**
   - Cualquier anulación manual (`MatchOverride`) establecida por el usuario tiene precedencia irrevocable sobre cualquier regla o código de barras.

---

## 2. Dataset de Evaluación (16 Pares D1 / Éxito Anotados)

El benchmark evalúa casos reales de productos de la canasta básica colombiana:

| ID | Producto A | Producto B | Categoría Esperada | Justificación / Regla |
| :--- | :--- | :--- | :--- | :--- |
| **EX-01** | Coca-Cola Sabor Original 1.5 L | Gaseosa Coca-Cola Original 1500 ml | `Exact` | Mismo EAN, misma marca, volumen equivalente (1.5 L = 1500 ml). |
| **EX-02** | Leche Entera Alquería 1 L | Leche Entera Larga Vida Alquería 1000 ml | `Exact` | Mismo EAN, misma marca, variante entera, 1 L = 1000 ml. |
| **EX-03** | Arroz Blanco Diana 1000 g | Arroz Diana Blanco 1 kg | `Exact` | Mismo EAN, misma marca, 1000 g = 1 kg. |
| **EX-04** | Atún Lomitos en Agua Van Camp's 160 g | Lomitos de Atún en Agua Van Camps 160g | `Exact` | Mismo EAN, lomitos en agua, 160 g. |
| **EX-05** | Aceite Vegetal Premier 1000 ml | Aceite Vegetal Premier 1 L | `Exact` | Mismo EAN, aceite vegetal, 1000 ml = 1 L. |
| **SUB-01** | Leche Entera Latti 1000 ml (D1) | Leche Entera Alquería 1 L (Éxito) | `Substitute` | Marca propia D1 vs marca comercial líder. Misma categoría y volumen. |
| **SUB-02** | Arroz Blanco De La Cuesta 1000 g (D1) | Arroz Blanco Diana 1 kg (Éxito) | `Substitute` | Marca propia D1 vs Éxito. Misma masa y categoría funcional. |
| **SUB-03** | Huevos Rojos Tipo AA 30 und (Redondos) | Huevos Rojos Tipo AAA 30 unidades (Santa Reyes) | `Substitute` | Mismo conteo (30 und), categoría huevos, pero diferencia de calibre/clasificación (AA vs AAA). |
| **SUB-04** | Aceite de Girasol Kythos 900 ml (D1) | Aceite de Girasol Diana 1000 ml (Éxito) | `Substitute` | Mismo tipo de aceite (Girasol), diferencia de tamaño del 10% dentro de la tolerancia del 15%. |
| **SUB-05** | Atún en Trozos en Agua Bruma 140 g (D1) | Atún en Lomitos en Agua Isabel 160 g (Éxito) | `Substitute` | Mismo medio de empaque (Agua), diferencia de tamaño del 12.5% dentro del umbral (15%). |
| **NOT-01** | Coca-Cola Original 1.5 L | Coca-Cola Sin Azúcar Zero 1.5 L | `NotEquivalent` | **Conflicto crítico de azúcar/calorías**. No sustituible automáticamente. |
| **NOT-02** | Leche Entera Colanta 1 L | Leche Deslactosada Colanta 1 L | `NotEquivalent` | **Conflicto crítico de lactosa**. Crítico para consumidores intolerantes. |
| **NOT-03** | Atún en Agua Van Camp's 160 g | Atún en Aceite Van Camp's 160 g | `NotEquivalent` | **Conflicto crítico de líquido de cobertura** (Agua vs Aceite). |
| **NOT-04** | Arroz Blanco Diana 1 kg | Azúcar Blanco Manuelita 1 kg | `NotEquivalent` | Categorías funcionales completamente distintas. |
| **NOT-05** | Leche Entera Alquería 1 L | Arroz Blanco Diana 1 kg | `NotEquivalent` | **Conflicto dimensional no compatible** (Volumen vs Masa). |
| **NOT-06** | Coca-Cola Original 1.5 L (EAN A) | Coca-Cola Zero 1.5 L (EAN A) | `Candidate` | **Conflicto de EAN sucio/reciclado**: variante Zero vs Original. El motor degrada a revisión en lugar de marcar `Exact`. |

---

## 3. Resultados de Evaluación y Métricas de Calidad

Ejecución automatizada mediante `MatchingEvaluationService.EvaluateDataset()`:

- **Total de pares evaluados:** 16
- **Verdaderos Positivos Exactos (Exact TP):** 5
- **Falsos Positivos Exactos (Exact FP):** **0**
- **Falsos Negativos Exactos (Exact FN):** 0
- **Verdaderos Negativos Exactos (Exact TN):** 11
- **Precisión en Coincidencias Exactas:** **100.0% (1.0)**
- **Cobertura (Recall) en Coincidencias Exactas:** **100.0% (1.0)**
- **F1 Score en Coincidencias Exactas:** **100.0% (1.0)**
- **Sustitutos identificados correctamente:** 5 (100% concordancia)
- **No equivalentes identificados correctamente:** 5 (100% concordancia)
- **Casos degradados a revisión (`Candidate`) por conflicto:** 1 (100% concordancia)

### Matriz de Confusión para `Exact`:
| | Clasificado como Exact | Clasificado como No Exact |
| :--- | :---: | :---: |
| **Realmente Exact** | 5 | 0 |
| **Realmente No Exact** | **0** | 11 |

---

## 4. Pipeline de Decisión de 6 Etapas

1. **Etapa 1 — Anulación por Usuario (`MatchOverride`):**
   - Si existe un registro explícito en base de datos para la tupla `(ExternalSkuId, ProductVariantId)`, se devuelve el veredicto del usuario con confianza 1.0.
2. **Etapa 2 — Coincidencia EAN con Detección de Conflictos:**
   - Si ambos productos tienen EAN idéntico, se auditan:
     - Conflicto crítico de variante (Zero vs Regular, Deslactosada vs Entera, Agua vs Aceite).
     - Conflicto dimensional (Masa vs Volumen).
     - Discrepancia extrema de cantidad ($> 25\%$).
     - Conflicto de marca no compatible.
   - En presencia de cualquier conflicto, la coincidencia no se auto-aprueba como `Exact`; se degrada a `Candidate` para revisión con bandera de conflicto.
   - Si no hay conflictos, se califica como `Exact` (confianza 0.99).
3. **Etapa 3 — Regla Determinista Exacta:**
   - Marca normalizada idéntica + Categoría funcional idéntica + Sin conflicto de variante + Diferencia de cantidad $< 2\%$.
   - Veredicto: `Exact` (confianza 0.95).
4. **Etapa 4 — Evaluación de Sustitutos:**
   - Misma categoría funcional.
   - Dimensiones físicas compatibles.
   - Sin conflicto crítico de variante (Zero vs Regular, etc.).
   - Diferencia de tamaño relativa $\le 15\%$ (configurable).
   - Veredicto: `Substitute` (confianza 0.80).
5. **Etapa 5 — Ranking de Candidatos (Fuzzy Retrieval):**
   - Jaccard sobre tokens normalizados $\ge 0.40$.
   - Veredicto: `Candidate` (confianza acotada $\le 0.70$). **Nunca promovido a Exact.**
6. **Etapa 6 — No Equivalente:**
   - Categorías distintas, dimensiones incompatibles o variantes críticas opuestas.
   - Veredicto: `NotEquivalent` (confianza 0.0).

---

## 5. Comparación de Precios Unitarios y Sobrecompra

El servicio `UnitComparisonService` garantiza que los usuarios no tomen decisiones erróneas basadas en precios absolutos engañosos:

1. **Normalización a Unidad Base:**
   - Masa: precio por kilogramo ($\text{COP} / \text{kg}$).
   - Volumen: precio por litro ($\text{COP} / \text{L}$).
   - Conteo: precio por unidad ($\text{COP} / \text{und}$).
   - Longitud: precio por metro ($\text{COP} / \text{m}$).
2. **Evaluación de Sobrecompra en Cestas:**
   - Si una receta o lista requiere $1.0\text{ kg}$ de arroz y la presentación ofrecida es un bulto de $5.0\text{ kg}$, el modelo calcula:
     $$\text{Paquetes requeridos} = \lceil 1.0 / 5.0 \rceil = 1$$
     $$\text{Sobrecompra} = \frac{5.0 - 1.0}{1.0} \times 100\% = 400\%$$
   - Con un umbral máximo configurado (por ejemplo, 20%), el sistema marca la opción como **no aceptable** por sobrecompra excesiva, protegiendo el desembolso de caja del usuario.
