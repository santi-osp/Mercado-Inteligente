# GOAL 10 — Hardening, UX final, validación y release local 1.0.0

## Propósito

Convertir el sistema funcional de Goals 1–9 en un producto personal terminado, consistente, recuperable, medido y agradable de usar diariamente.

Este es el último goal del roadmap inicial.

## Auditoría integral

Antes de cambiar estética, ejecutar:

- backend build/tests;
- frontend build/tests;
- integration;
- offline contract tests;
- live contract tests disponibles;
- DB integrity;
- dependency audit razonable.

Registrar baseline.

## UI/UX refinement

Revisar todas las pantallas contra `02_UI_UX...md` y `assets/prototipo-ui.png`.

### Pantallas mínimas completas

- Inicio/resultados.
- Mi mercado.
- Comparar.
- Historial.
- Tiendas/providers.
- Configuración simple.
- Configuración avanzada/System status.

### Estados

Cada pantalla relevante debe soportar:

- loading;
- empty;
- stale;
- partial;
- error;
- offline;
- disabled/not configured.

No spinners eternos.

### Responsive

QA desktop y mobile width. La app sigue siendo local PC, pero PWA responsive debe ser sólida.

### Accesibilidad

- keyboard navigation;
- focus visible;
- labels;
- contrast;
- no depender solo de color;
- semantic headings;
- buttons/links correctos.

## Historial

Implementar UX útil:

- historial de precio por ProductVariant/offer context;
- mínimo/máximo/promedio cuando datos suficientes;
- cambios;
- no mezclar scopes/sellers incompatibles en una sola serie sin filtros.

Gráficos solo si aportan claridad; no dependencia pesada innecesaria.

## Observability local

System panel:

- app version;
- DB size;
- RAW size;
- last backup;
- DB integrity;
- provider health;
- last sync;
- Google status;
- OpenRouter status;
- pending external actions.

## Backup/restore UX

- crear backup;
- listar backups;
- restore seguro con confirmación y backup preventivo;
- documentar ruta manual.

Test restore end-to-end en DB temporal antes de habilitar camino destructivo real.

## Export/import

Agregar export útil si encaja sin riesgo:

- CSV histórico/precios;
- JSON/backup de configuración/datos;

Prioridad a backup completo antes que formatos cosméticos.

## Release packaging

Definir experiencia de ejecución local.

Objetivo: usuario pueda iniciar fácilmente en Windows sin recordar cinco comandos.

Opciones a evaluar:

- script PowerShell launcher;
- backend sirviendo frontend compilado;
- otro packaging simple.

Elegir opción más mantenible, no Electron salvo necesidad demostrada.

## Seguridad hardening

- loopback binding;
- CORS;
- secrets scan;
- logs scan;
- dependency vulnerabilities;
- inputs/URLs;
- path traversal RAW/backup;
- no HTML unsafe;
- prompt injection fixtures;
- sensitive error handling.

## Performance

Medir con dataset ampliado/realista:

- local search;
- comparison;
- basket optimization;
- startup;
- sync batches;
- DB size/history.

Crear índices solo donde medición los justifique.

## Data maintenance hardening

- RAW retention;
- logs retention;
- PRAGMA optimize;
- backup retention;
- product lifecycle;
- orphan cleanup seguro;
- no borrar conocimiento canónico/override.

## External validation

### Physical price validation

Scaffold/archivo ya previsto. Si el usuario ha aportado observaciones físicas, analizarlas.

Si no:

- NO bloquear release;
- mantener `BLOCKED_EXTERNAL`;
- generar checklist exacto para 5–10 productos D1 y Éxito;
- la UI mantiene PriceScope honesto.

### Google/OpenRouter

Si keys disponibles:

- live final smoke.

Si no:

- release puede completar con integración configurada pero `NOT_CONFIGURED`;
- documentar pasos humanos.

## E2E obligatorios

1. Primer inicio DB limpia.
2. Crear/editar perfil configuración.
3. D1 search/sync.
4. Éxito search/sync.
5. Crear cesta.
6. Exact/similar/substitute comparison.
7. Optimizar max N.
8. Reiniciar y persistencia.
9. Offline/last-known-good.
10. Backup y restore test.
11. Google configured path si disponible / unconfigured fallback.
12. OpenRouter configured path si disponible / AI-off fallback.
13. Ara partial state.

## Regression gates

Nada de Goal 10 puede romper:

- D1;
- Éxito;
- optimizer;
- matching;
- AI-off mode;
- local-only privacy.

## Documentación final

Actualizar README con:

- requisitos;
- instalación;
- inicio;
- datos locales;
- backup/restore;
- configuración Google;
- configuración OpenRouter;
- uso sin IA;
- providers;
- troubleshooting.

Crear:

`docs/development/FINAL_PROJECT_REPORT.md`

Debe contener:

- versión;
- arquitectura final;
- funcionalidades;
- providers/capabilities;
- tests/resultados;
- performance;
- external blockers;
- limitaciones conocidas;
- pasos de ejecución;
- rutas de datos;
- backup;
- próximos posibles enhancements.

## Version

Establecer 1.0.0 solo si DoD global se cumple. Si existen fallos técnicos internos relevantes, usar prerelease y corregir antes de cerrar.

Bloqueos puramente externos (key no suministrada, validación física) pueden coexistir con 1.0.0 si la integración está completa y el producto funciona sin ellos, siempre documentados.

## Limpieza

- eliminar demo data accidental de release;
- no eliminar fixtures de test;
- resolver TODOs críticos;
- limpiar código muerto;
- format/lint;
- verificar `.gitignore`;
- secretos scan.

## Final DoD

Cumplir `docs/quality/DEFINITION_OF_DONE.md` completo.

Ejecutar suite completa y guardar resultados en final report.

Actualizar `PROJECT_STATE.md`:

- Goals 01–10 complete/complete_with_external_blockers;
- global status COMPLETE;
- lista de acciones humanas pendientes exacta.

## Condición de terminación

SOLO después de completar este DoD, Claude puede finalizar la tarea global.

No terminar con “Goal 10 terminado” solamente. Entregar un resumen final del proyecto completo, comandos de ejecución y bloqueos externos restantes.
