# Informe de Desarrollo — GOAL-08: OpenRouter e Inteligencia Artificial Opcional

## 1. Resumen Ejecutivo

El objetivo **GOAL-08** implementa la capa de enriquecimiento con Inteligencia Artificial opcional y asistida para **Mercado Inteligente**, respaldada por el agregador de modelos **OpenRouter**.

### 1.1 Principio No Negociable: "IA OFF sigue siendo producto completo"
La premisa rectora de la arquitectura es que **el sistema funciona al 100% de manera determinista, offline y autónoma sin necesidad de IA ni conexión a OpenRouter**:
- La normalización de productos opera mediante reglas léxicas y diccionarios de patrones (`DeterministicNormalizer`).
- La equivalencia de productos opera mediante matching determinista de EAN, marca, categoría y gramaje (`DeterministicMatcher`).
- La optimización de cestas de compra (K-tiendas) calcula la asignación de tiendas, ahorros y rutas de forma puramente matemática (`BasketOptimizer`).
- Todas las explicaciones y recomendaciones cuentan con plantillas textuales en español generadas sin llamadas de red.

### 1.2 Garantía de Invarianza Matemática
**La IA nunca calcula, inventa ni altera precios, códigos EAN, inventario disponible, asignación de tiendas ni costos de transporte.**
Todos los totales de compra, ahorros calculados respecto a la línea base y planes de ruta entre establecimientos se computan de manera exacta antes de invocar los servicios de IA. El rol de la IA se limita estrictamente a:
1. Enriquecer con explicaciones comprensibles y contextualizadas para el usuario humano.
2. Analizar diferencias semánticas cualitativas entre productos candidatos ambiguos.
3. Sugerir alternativas funcionales en categorías complejas.
4. Extraer atributos adicionales en nombres comerciales atípicos o mal formateados.

---

## 2. Componentes Arquitectónicos

### 2.1 Proveedor Central y Gestión de Cuotas (`OpenRouterAiProvider.cs`)
- **Cliente HTTP Resiliente**: Comunica con la API de OpenRouter (`https://openrouter.ai/api/v1/chat/completions`) implementando manejo transparente de cabeceras de identificación de la app (`HTTP-Referer`, `X-Title`).
- **Degradación Elegante**: Detección inmediata de códigos de error HTTP:
  - `401 Unauthorized`: Clave inválida o revocada.
  - `402 Payment Required`: Saldo insuficiente en OpenRouter.
  - `429 Too Many Requests`: Límite de tasa excedido.
  - `5xx / Timeouts`: Caída temporal del proveedor o modelo.
  Ante cualquiera de estos eventos, el sistema no bloquea la interfaz ni aborta la operación: conmuta automáticamente al resultado del servicio determinista correspondiente.
- **Diagnóstico y Prueba de Conexión**: Endpoint `POST /api/v1/ai/test-connection` que verifica latencia en milisegundos y validez de credenciales con 1 token de prueba sin incurrir en costos significativos.

### 2.2 Seguridad, Privacidad y Minimización de Contexto (`AiPromptSafety.cs`)
- **Almacenamiento Seguro (DPAPI)**: La clave API de OpenRouter se almacena cifrada en disco mediante Windows Data Protection API (`ISecretStore`) bajo la clave `"openrouter"`. La clave nunca se escribe en texto plano en archivos de configuración ni se expone a la UI del frontend.
- **Minimización de Contexto**: Ninguna dirección física exacta, coordenada GPS sin procesar ni identificador de usuario es enviado a OpenRouter. Solo se transmiten nombres normalizados de productos, categorías, marcas y resúmenes agregados de tiendas.
- **Aislamiento contra Inyección de Prompts**: Todos los datos externos provistos por catálogos o usuarios se sanitizan de caracteres de control nulos o invisibles (`\0`, `\x08`, etc.) y se encapsulan dentro de etiquetas XML estrictas (`<data>...</data>`, `<product_name>...</product_name>`).

### 2.3 Caché Criptográfica Local (`AiCacheService.cs`)
- **Persistencia en Disco**: Archivo `data/ai-cache.json` que almacena respuestas de IA indexadas por un hash SHA-256 de cuatro factores:
  $$\text{CacheKey} = \text{SHA256}(\text{Feature} \mathbin{\Vert} \text{PromptVersion} \mathbin{\Vert} \text{Model} \mathbin{\Vert} \text{InputHash})$$
- **Cero Costo y Latencia Inmediata**: Cualquier consulta idéntica (mismo producto, misma comparación, misma versión de prompt) se resuelve en $0\text{ ms}$ y \$0 USD de costo.
- **Gestión desde UI**: Botón de vaciado de caché en el panel de Ajustes con recuento en tiempo real de entradas almacenadas.

### 2.4 Telemetría Local y Control de Presupuesto (`AiUsageTracker.cs`)
- **Registro de Consumo**: Almacenado localmente en `data/ai-usage.json`. Registra:
  - Número total de llamadas y aciertos en caché (*cache hits*).
  - Tokens de entrada (*prompt tokens*) y tokens generados (*completion tokens*).
  - Costo estimado acumulado en USD y su conversión a COP según TRM local.
  - Desglose histórico de consumo diario.
- **Límite Presupuestario Mensual**: Alerta configurable por el usuario para prevenir consumos accidentales de saldo en OpenRouter.

---

## 3. Capacidades Funcionales V1

| Capacidad | Servicio Backend | Modelo Recomendado | Modo Determinista de Respaldo |
| :--- | :--- | :--- | :--- |
| **Normalización de Productos** | `ProductNormalizationAiService` | `anthropic/claude-3.5-haiku` | `DeterministicNormalizer` con extracción Regex de marcas y gramajes |
| **Adjudicación de Candidatos** | `MatchAdjudicationAiService` | `anthropic/claude-3.5-haiku` | `DeterministicMatcher` con scores de similitud y reglas de marca |
| **Sugerencia de Sustitutos** | `SubstituteSuggestionAiService` | `openai/gpt-4o-mini` | Filtro por categoría idéntica y ordenamiento por precio unitario |
| **Explicación de Cestas** | `BasketExplanationAiService` | `anthropic/claude-3.5-haiku` | Generador de plantilla estructurada en español |

### 3.1 Explicación de Optimización de Cestas (`BasketExplanationAiService`)
Recibe los totales matemáticos invariantes generados por `BasketOptimizer`:
- Costo total de compra y costo en tienda única de referencia.
- Ahorro neto estimado en COP.
- Costo de transporte, distancia y duración de ruta.
- Distribución de productos por tienda.
Genera un análisis en lenguaje natural estructurado:
- **Resumen**: Síntesis en 1-2 oraciones del plan recomendado.
- **Por qué se recomienda**: Justificación financiera del balance entre ahorro en canasta básica y costo de movilidad.
- **Trade-offs entre tiendas**: Explicación clara de por qué vale (o no) la pena desplazarse a una segunda tienda.
- **Fuentes clave de ahorro**: Lista de productos específicos donde se concentra la mayor diferencia de precio.
- **Advertencias**: Notificaciones sobre productos faltantes o con empaques atípicos.

### 3.2 Adjudicación de Emparejamientos Ambiguos (`MatchAdjudicationAiService`)
Cuando dos productos de marcas distintas o empaques no idénticos caen en estado de "Candidato" determinista:
- La IA evalúa la función culinaria o doméstica principal.
- Emite un dictamen estructurado: `EXACT`, `SUBSTITUTE` o `NOT_EQUIVALENT`.
- Detalla los atributos compartidos (ej. "ambos son arroz blanco de grano largo 1000g") y discrepancias (ej. "marca líder vs marca propia").
- **Prioridad Absoluta del Usuario**: Si el usuario define un *User Override* en la interfaz, su decisión prevalece sobre la IA y sobre el algoritmo determinista de forma irrevocable.

---

## 4. Experiencia de Usuario y Frontend

### 4.1 Panel de Control en "Ajustes"
- **Interruptor Maestro**: Activa o desactiva completamente la capa de IA en un clic.
- **Selector de Modelo**: Opciones preconfiguradas y probadas:
  - `anthropic/claude-3.5-haiku` *(Recomendado: ultra rápido y excelente seguimiento de esquemas JSON)*.
  - `openai/gpt-4o-mini` *(Económico y veloz)*.
  - `meta-llama/llama-3.3-70b-instruct` *(Modelo abierto / Open-weights)*.
  - `google/gemini-2.0-flash-001` *(Baja latencia)*.
- **Selectores Granulares de Funcionalidades**: Permite habilitar IA únicamente para explicación de cestas sin usarla en normalización, o viceversa.
- **Herramientas de Diagnóstico**:
  - Botón "Probar conexión": ejecuta ping live a OpenRouter y reporta latencia exacta en ms.
  - Botón "Vaciar caché local": libera espacio y fuerza re-evaluación con modelos actualizados.
- **Métricas de Telemetría**: Indicadores en tiempo real de llamadas realizadas, tasa de acierto en caché, tokens consumidos y gasto acumulado en USD y COP.

### 4.2 Integración en "Mi mercado"
- Sección dedicada de **Optimización y Explicación de Cesta**:
  - Resumen visual de la solución óptima (K tiendas, ahorro neto, costo de ruta).
  - Tarjetas de asignación por tienda (*Store Buckets*) con productos específicos y subtotales.
  - Botón "✨ Explicar optimización con IA": despliega la tarjeta de explicación humana con insignia distintiva de procedencia (`[IA]` o `[Determinista]`).

### 4.3 Integración en "Comparar"
- Botón "✨ Analizar equivalencia con IA": disponible en la vista de comparación personalizada.
- Despliega tarjeta con dictamen formal (`EXACTO`, `SUSTITUTO`, `NO EQUIVALENTE`), nivel de confianza probabilística, razón concisa y listas comparativas de atributos en común y divergencias.

---

## 5. Verificación y Resultados de Pruebas

### 5.1 Pruebas del Backend (`OpenRouterAiTests.cs`)
10 pruebas unitarias e integración exhaustivas en C# (.NET 10 xUnit):
1. `AiSettings_LoadsAndSavesDefaults`: Carga y persistencia segura de configuración.
2. `AiProvider_WhenDisabledOrNoKey_ReportsNotConfigured`: Detección estricta de estado no configurado.
3. `AiProvider_SuccessfulCall_ParsesTokensAndCachesResponse`: Validación de token usage y caching criptográfico con 0ms en segunda llamada.
4. `AiProvider_HttpErrors_DegradeGracefully`: Manejo robusto de errores 401, 402 y 429 sin excepciones no controladas.
5. `ProductNormalization_WithValidAi_ExtractsRichAttributes`: Extracción estructurada de marca, volumen, empaque y confianza.
6. `ProductNormalization_WhenAiDisabledOrFails_UsesDeterministicFallback`: Degradación transparente a `DeterministicNormalizer`.
7. `MatchAdjudication_AmbiguousCandidates_ReturnsStructuredDecision`: Dictamen de equivalencia y atributos compartidos/divergentes.
8. `SubstituteSuggestion_ReturnsRankedFunctionalAlternatives`: Sugerencia y ordenamiento de sustitutos funcionales.
9. `BasketExplanation_GeneratesHumanReadableSummaryWithoutRecalculatingTotals`: Verificación de invarianza de totales y formato natural.
10. `PromptSafety_SanitizesAndIsolatesAdversarialInstructions`: Sanitización de caracteres de control y encapsulamiento XML seguro.
11. `TestConnection_ValidatesEndpointLatencyAndStatusCodes`: Validación de latencia y código de estado 200.
12. `UserOverride_AlwaysBeatsAiAndDeterministicSignals`: Garantía de que la preferencia del usuario anula cualquier decisión de IA.

**Resultado Backend**: **260 pruebas superadas con éxito** (0 fallos).

### 5.2 Pruebas del Frontend (`app.spec.ts`)
Pruebas integradas de componentes en Angular 22 / Vitest:
- Carga de estado de IA y almacenamiento seguro de parámetros de configuración.
- Prueba de conexión con informe de latencia y vaciado de caché local.
- Ejecución de optimización de cesta con llamada y renderizado de explicación de IA.
- Ejecución de adjudicación de coincidencia con IA en comparador personalizado.

**Resultado Frontend**: **43 pruebas superadas con éxito** (0 fallos).
**Compilación Frontend**: `ng build` completado en 2.09s con **0 advertencias y 0 errores**.

---

## 6. Conclusión y Estado del Objetivo

El objetivo **GOAL-08** queda completado con éxito con estado **COMPLETE_WITH_EXTERNAL_BLOCKERS**:
- Toda la infraestructura técnica, de seguridad, caching, telemetría y UI se encuentra 100% implementada y verificada localmente.
- Si el usuario suministra una clave de OpenRouter válida, la aplicación utiliza de inmediato los modelos de lenguaje configurados.
- Si el usuario no suministra clave o no cuenta con saldo, la aplicación sigue funcionando de forma completa y transparente con sus algoritmos deterministas locales.
