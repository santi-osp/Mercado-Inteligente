# GOAL 04 — Ciclo de vida de datos, sincronización y mantenimiento

## Propósito

Convertir D1/Éxito en fuentes sostenibles durante meses/años. Implementar el motor de frescura, caché, sync, retención, last-known-good, limpieza y mantenimiento descrito en `DATA_LIFECYCLE_AND_SYNC.md`.

## Resultado

La aplicación debe seguir siendo útil si:

- estuvo apagada varios días;
- Internet falla;
- un provider falla temporalmente;
- productos desaparecen/regresan;
- una sync se interrumpe;
- RAW/logs crecen;
- la DB acumula histórico.

## Sync Engine

Crear/cohesionar servicio central de sincronización con colas/prioridades simples, no infraestructura distribuida.

Tipos:

1. On-demand: producto/búsqueda solicitada y stale.
2. Prioritaria: cesta activa/favoritos/frecuentes.
3. Mantenimiento: catálogo oportunista mientras app está abierta.

Evitar refresh masivo constante.

## Stale-while-revalidate

Patrón UI/API:

1. leer local;
2. devolver inmediatamente si existe;
3. etiquetar freshness;
4. disparar refresh cuando corresponda;
5. actualizar UI/evento cuando termina;
6. conservar valor previo si falla.

## TTL

Implementar configuración global y, si aporta valor, override por provider:

- price 6h;
- stock 2h;
- products 7d;
- stores 7d;
- routes 24h;
- promo 4h.

Crear tests de boundary exacto de TTL.

## Lifecycle de productos

Implementar ACTIVE/MISSING/INACTIVE/UNKNOWN.

No borrar por una ausencia. Política inicial configurable o central:

- missing tras sync(s) sin aparición;
- inactive tras periodo prolongado;
- reactivación automática si reaparece.

## Price history inteligente

No crear observación idéntica en cada polling.

Crear observation cuando cambia al menos uno:

- price;
- list price;
- availability;
- promoción;
- seller/contexto semántico.

Si no cambia: actualizar last_seen/last_checked.

Opcional heartbeat poco frecuente si se necesita demostrar continuidad.

## Anomalías

Crear validación para:

- price<=0;
- cambio extremo configurable;
- quantity/unit incoherente;
- response incompleta.

No descartar automáticamente un precio extremo; marcar `SUSPICIOUS`/requiere segunda lectura cuando sea apropiado.

## RAW retention

Implementar:

- gzip;
- hash dedup;
- relative path;
- cleanup por antigüedad (default 60d);
- no eliminar RAW todavía referenciado por tarea de debugging activa si existe mecanismo equivalente.

Cleanup debe ser seguro e idempotente.

## Logs retention

Rotación y limpieza ~30 días configurable.

## SyncRun

Completar métricas/estados:

- PENDING/RUNNING/SUCCESS/PARTIAL/FAILED/ABORTED;
- start/end;
- counts;
- provider/context;
- error summary.

Al arranque marcar RUNNING antiguo como ABORTED.

## Concurrencia

Evitar sync duplicado del mismo provider/context/resource. Implementar coordinación local simple.

SQLite: escrituras coordinadas, transacciones por lotes razonables.

## Maintenance service

Implementar tareas locales:

- cleanup RAW;
- cleanup logs si aplica;
- PRAGMA optimize;
- integrity check programable/manual;
- backup policy base;
- tamaño DB/RAW.

VACUUM solo bajo criterio medible/manual o scheduler poco frecuente.

## Backups

Completar política:

- diarios 7;
- semanales 4;
- mensuales 3;

Si automatización total complica, debe al menos existir una política funcional y segura ejecutable desde maintenance.

Probar restore a DB temporal como parte de integration tests.

## Offline mode

Detectar dependencia externa no disponible. La UI debe seguir permitiendo búsquedas locales, historial y cestas.

Mostrar estado offline/degraded sin modal bloqueante permanente.

## UI

Agregar:

- freshness consistente;
- progreso sync útil;
- “actualizando…” no bloqueante;
- last sync por provider;
- refresh manual con cooldown;
- maintenance/system stats en Configuración.

## Casos obligatorios

- PC estuvo apagado 3 días;
- price stale + provider funciona;
- price stale + provider falla;
- product missing/reappears;
- sync abortado;
- dos refresh simultáneos;
- RAW duplicado;
- cleanup idempotente;
- DB backup/restore;
- price unchanged no infla history;
- price changed sí crea history.

## Performance

Cargar UI desde DB sin esperar red. Medir tamaños y tiempos. No optimizar prematuramente.

## Entregables

- Sync/Maintenance engine;
- retention;
- backup policy;
- UI freshness/offline;
- tests;
- `docs/development/goal-04-data-lifecycle-report.md`.

## DoD

- D1/Éxito siguen operativos;
- last-known-good validado;
- retention probado en directorio temporal;
- backup/restore PASS;
- sync interrupt/concurrency tests PASS;
- UI no bloquea ante Internet caído;
- PROJECT_STATE actualizado.

## Continuación

Pasar inmediatamente a GOAL-05.
