# CLAUDE.md — Contrato de ejecución del proyecto Mercado Inteligente

## Tu tarea NO es un goal individual

Tu tarea es **desarrollar el proyecto completo recorriendo TODOS los goals 01→10** definidos en `GOALS_MASTER.md` y en `goals/`.

Un goal individual es un checkpoint interno, no el final de la tarea.

**NO detengas la ejecución al terminar GOAL-01, GOAL-02, etc.** Después de verificar el Definition of Done de cada goal, actualiza `PROJECT_STATE.md` y continúa inmediatamente con el siguiente.

## Condición de terminación global

Solo puedes considerar terminado el trabajo cuando:

- los goals 01–10 hayan sido recorridos;
- cada goal esté `COMPLETE` o `COMPLETE_WITH_EXTERNAL_BLOCKERS`;
- builds y tests aplicables pasen;
- la aplicación sea ejecutable localmente;
- exista documentación de release;
- cualquier dependencia que requiera intervención humana externa esté claramente registrada y no exista trabajo técnico local pendiente que pueda hacerse sin esa intervención.

## Acciones externas no deben detenerte

Si falta una API key de Google/OpenRouter o una validación física:

- NO pidas la key en medio del roadmap si puedes continuar;
- implementa todo el soporte, configuración segura, mocks, contract tests y UI correspondiente;
- marca la sub-tarea `BLOCKED_EXTERNAL`;
- continúa al siguiente trabajo ejecutable;
- al final genera una lista corta y exacta de acciones humanas pendientes.

Nunca inventes una credencial ni agregues secretos al repositorio.

## Lectura obligatoria

Antes de modificar código, lee `PROJECT_MANIFEST.md` y todos los documentos marcados como obligatorios.

No reinterpretes decisiones cerradas sin evidencia técnica. Si una evidencia runtime obliga a cambiar algo importante:

1. documenta el hallazgo;
2. crea/actualiza un ADR;
3. aplica el cambio mínimo coherente;
4. actualiza las especificaciones si corresponde;
5. continúa.

## Disciplina de ejecución

Para cada goal:

1. Audita el estado actual del repositorio.
2. No recrees trabajo válido ya existente.
3. Implementa por lotes coherentes.
4. Ejecuta tests inmediatamente después de cada bloque significativo.
5. Corrige antes de seguir si una regresión afecta código existente.
6. Actualiza documentación y `PROJECT_STATE.md`.
7. Cumple el DoD del goal.
8. Continúa con el siguiente goal.

## Calidad no negociable

- No placeholders engañosos.
- No TODOs críticos sin registrar.
- No mocks conectados por accidente en release.
- No secretos versionados.
- No stack traces o datos sensibles hacia UI.
- No `if (store == "D1")` disperso fuera del provider correspondiente.
- No IA en el camino crítico de funcionamiento.
- No borrar el último dato válido por fallo de actualización.
- No fuzzy matching como prueba automática de identidad.
- No confundir precio online/regional con precio físico verificado.
- No usar `double`/`float` para dinero.
- No convertir masa↔volumen solo porque los números coinciden.
- No sobreingeniería: sin microservicios, Kafka, Redis, Kubernetes, Elasticsearch, CQRS complejo o Event Sourcing sin necesidad demostrada.

## Comandos y comprobaciones

Siempre que corresponda ejecuta:

- backend build;
- backend tests;
- frontend build;
- frontend tests;
- migrations sobre DB temporal;
- integration tests;
- contract tests de providers;
- smoke test local;
- lint/format si el proyecto lo configura.

No marques un goal como COMPLETE si su build/test relevante está roto.

## Persistencia de contexto

`PROJECT_STATE.md` es el estado canónico de progreso.

Antes de una compactación o al llegar a un checkpoint largo:

- registra goal actual;
- tareas completadas;
- tareas pendientes;
- fallos conocidos;
- archivos clave modificados;
- próximos comandos exactos.

Si una nueva sesión comienza con goals ya completados, NO los repitas. Continúa desde el primer goal incompleto.

## Versionado y checkpoints

Si Git está disponible:

- usa commits coherentes por bloque/goal;
- no hagas commits destructivos ni reescribas historia innecesariamente;
- no mezcles refactors masivos con funcionalidad sin razón.

Si Git no está inicializado, inicialízalo solo si es apropiado y no hay una política contraria en el repositorio.

## Experiencia visual

`docs/specifications/02_UI_UX_Design_System_Comparador_Mercado.md` y `assets/prototipo-ui.png` son referencias visuales. La implementación debe mantener:

- aspecto minimalista premium y orientado a datos;
- jerarquía clara;
- accesibilidad;
- responsive real;
- estados loading/empty/error/stale/offline;
- no parecer un ecommerce tradicional.

## Resultado final esperado

El resultado debe ser una aplicación local profesional, mantenible y utilizable, con:

- Angular PWA;
- ASP.NET Core;
- EF Core + SQLite;
- D1 + Éxito reales;
- Ara parcial honesto;
- histórico y sincronización;
- matching exacto/sustitutos;
- cesta y optimizador máximo-N;
- geocoding/routing preparados o activos según credencial;
- OpenRouter opcional preparado o activo según credencial;
- health/observability/backups;
- release local documentado.

Al finalizar los 10 goals genera `docs/development/FINAL_PROJECT_REPORT.md` con:

- resumen de arquitectura;
- funcionalidades completas;
- tests ejecutados;
- integraciones activas;
- bloqueos externos restantes;
- pasos exactos para ejecución;
- pasos exactos para configurar Google/OpenRouter;
- limitaciones conocidas;
- versión final.
