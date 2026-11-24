# GOAL-01 — Informe de avance

**Estado: COMPLETE como checkpoint de fundación; no es release.** Fecha de checkpoint: 2026-10-07.

## Implementado (estado acumulado)

- Solucion .NET 10 modular (Domain, Application, Infrastructure, API, tests), Angular 22.2.1 standalone/PWA, SQLite/EF Core con migraciones, constraints de precio positivo, FK, WAL e integridad.
- RAW gzip deduplicado por SHA-256, escritura atomica y recuperacion de fichero ausente con metadata existente. Backup manual SQLite y restore offline con staging, comprobaciones y copia preventiva. Script de restore requiere confirmacion y app detenida.
- Settings persistentes, SecretStore DPAPI CurrentUser, contratos externos, DemoStoreProvider de pruebas no registrado en produccion. IA y mapas nulos. Logs locales acotados sin payload ni secretos.
- API versionada: health, settings, busqueda local, retailers/stores, listas basicas e items; errores API 404 estructurados. La UI permite crear listas y agregar/quitar items sin sugerir precios reales.
- El startup verifica integridad/FK antes de migrar, crea backup si hay migraciones pendientes, comprueba resultado y falla sin restauracion automatica; pruebas de DB limpia, migracion previa, dato incompatible y FK rota.

## Evidencia histórica (reemplazada por el checkpoint final de este archivo)

- Ultimo backend `dotnet build` PASS (0 warnings), `dotnet test` **23/23 PASS**.
- Migracion `ValidPrices` sobre `MARKET_DATA_DIR` temporal PASS; tests rechazan precios no positivos.
- Frontend Angular 22 con Node v24.21.0 temporal: `npm ci --engine-strict` PASS, build PASS con warning de presupuesto CSS de 580 bytes, Vitest/JSDOM **9/9 PASS**. Node global v24.10.0 no es compatible (ADR 0003).
- Smoke anterior: interfaz desktop/movil (1440/360 px), settings 2 a 3 persisten tras recarga, backup desde UI, health DB ok. Capturas `goal-01-desktop.png` y `goal-01-mobile.png`. Esto NO es E2E del flujo nuevo de listas ni comparacion.
- Audit npm: cuatro alertas high derivadas de una cadena opcional Karma/braces; no hay remediacion compatible verificada (ADR 0003).

## Pendientes identificados antes del checkpoint de integración

Revisar alertas de dependencias, accesibilidad/componentes base, estados desconectados, E2E repetible y controles de seguridad. La UI de búsqueda/fuentes y configuración de secretos y el smoke manual de listas/restore fueron completados en el checkpoint siguiente. GOAL-02 a GOAL-10 no han iniciado formalmente. No hay productos/precios reales de supermercados; no usar para decidir compras.

## Checkpoint de integración 2026-10-07

Backend build sin warnings y 25/25 tests PASS. Frontend build sin warnings y 16/16 tests PASS con Node v24.21.0. Búsqueda local y vista de fuentes muestran respuestas reales de API; claves Google/OpenRouter se introducen en backend DPAPI mediante UI con estado booleano, sin devolver valores y sin confundir clave guardada con servicio operativo. Smoke API y navegador (1440/360 px) comprobó cesta, fuentes, búsqueda vacía, ajustes y rechazo de clave inválida. Se detectó y corrigió `using System;` faltante en el runner del script Windows de restore; restore con confirmación sobre base temporal PASS, backup preventivo generado y reinicio API recuperó contenido del snapshot con health `ok` y FK válido. No se probó restore de datos reales. Las alertas npm high de cadena opcional Karma siguen abiertas. El guard de Host se corrigió para comprobar siempre el Host HTTP real, independientemente del servidor; origen externo en escritura sigue rechazado. `dotnet test` compiló y pasó 28/28. Smoke live sobre Kestrel/DB temporal: GET health 200, POST cesta 201 con Host normal; GET y POST con Host externo 403 y POST con Origin externo 403. Se detuvo la instancia desechable. La prueba de socket quedó automatizada en `scripts/smoke-api.ps1` (proceso y DB temporales, migración/health/FK, ajustes, cesta/ítem, backup y rechazos Host/Origin HTTP 403, limpieza solo del proceso propio): PASS con Windows PowerShell 5.1. `scripts/test.ps1` comprueba Node compatible sin ruta temporal absoluta; build backend 0 warnings y suite 28/28 PASS, frontend build 0 warnings y Vitest 16/16 PASS con Node v24.21.0. `npm audit` mantiene cuatro high por Karma/chokidar/braces y propone downgrade incompatible a Angular 19; no se aplicó. Health desconectado limpia el estado obsoleto, preserva listas en memoria y ofrece reintento; foco visible para select/textarea y navegación `aria-current=page`, con test de recuperación. Angular build sin warnings y Vitest 17/17 PASS tras ese cambio. Smoke adicional de navegador real: lista con ítem recuperada tras recarga sobre DB temporal. Tras corregir contraste y meta descripción, Lighthouse snapshot desktop/móvil 360px marcó accesibilidad 100 y cero fallos automatizados, sin overflow horizontal. El service worker servía assets viejos durante una recarga y hubo que darlo de baja antes del audit; la build final era `main-EEJZGFGE.js`. Esto no es auditoría WCAG manual ni E2E de navegador repetible. Queda E2E automatizado y decisión de riesgo de dependencias; este informe **no** cierra el goal.

## Cierre GOAL-01 — 2026-10-07

Build .NET 10 PASS sin warnings; tests 28/28 PASS. Angular 22.2.1 build PASS sin warnings; Vitest 17/17 PASS con Node 24.21.0. Suite Playwright repetible `npm run e2e` en Windows/Chrome local: 3/3 PASS sobre API y SQLite temporales, migración inicial, settings/lista persistidos tras recarga, backup comprobado en disco, búsqueda/fuentes honestas, móvil 360 px sin scroll horizontal y fallo/reintento de health. Smoke HTTP API aislado previo PASS. El reset de desarrollo se comprobó únicamente con DB desechable y conserva backups. Audit npm: 4 high transitivos del tooling opcional Karma, sin solución compatible; aceptación limitada al desarrollo y gate de reevaluación en ADR 0003. No se verificó WCAG manual exhaustivo ni un navegador fijado descargable. GOAL-02 D1 pasa a IN_PROGRESS; ninguna funcionalidad de proveedor D1 real se presume implementada.
