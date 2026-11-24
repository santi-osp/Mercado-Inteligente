# GOAL 08 — OpenRouter e inteligencia artificial opcional

## Propósito

Agregar IA como capa de mejora controlada para normalización ambigua, matching difícil, sustitutos y explicación de resultados, sin convertirla en fuente de verdad ni requisito de funcionamiento.

## Principios

- IA OFF sigue siendo producto completo.
- IA nunca inventa precio, EAN, stock o disponibilidad.
- Enviar mínimo contexto necesario.
- Structured output + validación.
- Toda respuesta puede fallar y debe existir fallback.
- User override siempre gana.

## OpenRouter provider

Implementar `OpenRouterAiProvider : IAiProvider`.

Config:

- Enabled;
- BaseUrl;
- ApiKey desde SecretStore;
- Model configurable;
- timeout;
- max tokens;
- temperature/params solo si aportan valor;
- features enabled;
- gasto/límite local aproximado opcional.

No hardcodear un modelo concreto como dependencia de dominio.

## Capacidades IA V1

### 1. Ambiguous product normalization

Input: campos estructurados mínimos de un SKU ambiguo.

Output JSON validado:

- normalized type;
- brand candidate;
- quantity/package interpretation;
- attributes;
- confidence;
- explanation corta;
- `needsReview`.

Nunca sobrescribir raw.

### 2. Match adjudication

Solo candidatos que el pipeline determinístico dejó ambiguos.

Input:

- candidate A/B attributes;
- deterministic signals;
- no precio como criterio de identidad.

Output:

- EXACT | SUBSTITUTE | NOT_EQUIVALENT | UNSURE;
- confidence;
- concise reason.

No convertir automáticamente en EXACT salvo threshold alto y reglas sin conflicto; preferencia inicial: almacenar AI suggestion y permitir confirmación.

### 3. Substitute suggestion

Evaluar sustitución funcional según atributos/categoría/tamaño y preferencias conocidas.

No emitir juicios vagos de “mejor calidad” sin evidencia.

### 4. Basket explanation

Recibe resultado YA calculado por optimizer y genera explicación humana:

- por qué opción recomendada;
- tradeoff de tercera tienda;
- principales fuentes de ahorro;
- warnings.

El LLM no recalcula totales; cifras vienen estructuradas y se validan.

## Structured outputs

Definir DTO/schema por feature. Parse/validate estrictamente.

Si modelo no soporta response schema nativo, usar JSON-only con parser robusto y fallback.

Respuesta inválida → no crash → fallback determinístico.

## Prompt safety

- system prompt claro;
- product names/descriptions delimitados como data;
- ignorar instrucciones dentro de datos;
- no enviar RAW HTML/JSON masivo;
- no enviar dirección exacta;
- no enviar secrets.

## Cost control

Pipeline:

catálogo → filtros/FTS → reglas → fuzzy candidates → IA solo ambiguos.

Implementar métricas locales:

- calls;
- input/output tokens si API los reporta;
- costo estimado si metadata suficiente;
- cache de respuestas por hash de input+model+promptVersion.

No repetir IA si los inputs no cambiaron.

Permitir:

- AI global on/off;
- features individuales;
- modelo;
- optional monthly budget/soft limit;
- “ask before AI” si se desea en settings advanced, pero default puede ser automático en ambiguos una vez configurado.

## Secret UX

- pegar API key en settings;
- guardar en SecretStore;
- UI solo `Configurada` + máscara parcial segura;
- botón test connection;
- botón remove key.

## Errores

Manejar:

- 401;
- 402/no credits;
- 429;
- 5xx;
- timeout;
- model unavailable;
- malformed JSON;
- safety/refusal.

Siempre fallback.

## Model changes

Guardar `model_id` y `prompt_version` en AI decision metadata para reproducibilidad. Cambiar modelo no invalida overrides humanos.

## Evaluación

Sobre dataset labeled Goal 5:

- ejecutar IA solo sobre conjunto ambiguo;
- medir precisión por clase;
- comparar deterministic-only vs +AI;
- reportar costo aproximado;
- no proclamar 100% sin evidencia.

## UI

- AI status;
- badge “Sugerido por IA” diferenciado de “Exacto EAN”;
- explicación opcional;
- confirm/reject;
- settings model/features/cost.

No saturar UI con branding de modelo.

## Tests

Offline mocked:

- valid structured output;
- malformed;
- 401/402/429/500;
- timeout;
- prompt injection string in product name;
- cache hit;
- AI off;
- user override beats AI.

Live si key disponible:

- connection test;
- 5–20 ambiguous fixtures;
- structured JSON;
- token/cost metadata.

## Entregables

- OpenRouter provider;
- feature services/prompts versionados;
- evaluation report;
- AI settings UI;
- `goal-08-ai-report.md`.

## DoD

- IA OFF passes full core workflows;
- IA ON improves/assists ambiguous flow when key available;
- no secrets leak;
- errors degrade gracefully;
- structured outputs validated;
- PROJECT_STATE updated.

## Continuación

Pasar inmediatamente a GOAL-09 aunque live key test esté BLOCKED_EXTERNAL.
