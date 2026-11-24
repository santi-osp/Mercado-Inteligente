# ADR 0003 — Angular 22 y versión de Node compatible

Decisión: actualizar el frontend de Angular 20.3 a Angular 22.2.1 (framework, CLI, build, compiler-cli y service worker), TypeScript 6.0.3 y Vitest 4.1.11. Se mantiene el builder `@angular/build:application` y el runner Vitest/JSDOM existentes.

Angular 22.2.1 declara `engines.node: ^22.22.3 || ^24.15.0 || >=26.0.0`; `@angular/compiler-cli` requiere TypeScript `>=6.0 <6.1` y `@angular/build` requiere Vitest `^4.0.8 || ^5.0.0`. El Node global de esta máquina es v24.10.0 (npm 11.6.2): **no es compatible**. `src/frontend/.nvmrc` fija v24.21.0 y `package.json` declara el rango de Node admitido. No se modificó el Node global ni se aceptó su advertencia `EBADENGINE` como validación: para build y tests se usó una copia temporal de Node v24.21.0 fuera del repositorio.

Verificación con Node v24.21.0: `ng build` PASS (bundle inicial 243.85 kB), `ng test --watch=false` PASS (4/4). Se ejecutaron mediante `node node_modules/@angular/cli/bin/ng.js build` y `node node_modules/@angular/cli/bin/ng.js test --watch=false` con ese binario explícito. Instalar/activar Node compatible en el entorno habitual antes de usar `npm ci`, `npm run build` o `npm test`; npm 11.6.2 cumple el rango de Angular. No se afirma que el Node global v24.10.0 funcione con Angular 22.

Una instalación limpia `npm ci --engine-strict` también pasó al invocar npm 11.6.2 mediante Node v24.21.0, seguida del mismo build y 4/4 tests. La instalación reportó cuatro vulnerabilidades high en el árbol npm; no se ejecutó `npm audit fix --force` por implicar actualizaciones potencialmente incompatibles. Pendiente operativo: seleccionar Node compatible para el shell habitual, y revisar las alertas de dependencias por separado. Esto no valida todavía E2E ni el DoD completo de GOAL-01.

## Revisión de seguridad npm (2026-10-07)

Con Node v24.21.0 y npm 11.6.2, `npm audit --json` reporta **4 high, 0 critical** (ninguna alerta low/moderate). Las cuatro entradas corresponden a una misma cadena de dependencias de desarrollo, no a cuatro advisories independientes:

- `@angular/build@22.2.1`: dependencia **directa de desarrollo**, marcada high por propagación desde su peer **opcional** `karma@^6.4.0`; no hay advisory propio de `@angular/build` en este resultado.
- `karma@6.4.4`: dependencia **transitiva opcional** instalada por npm para ese peer; marcada high por `braces` y `chokidar`. Karma es el runner antiguo; este proyecto ejecuta sus tests con Vitest/JSDOM.
- `chokidar@3.6.0`: dependencia **transitiva** de Karma, marcada high por `braces` (no confundir con `chokidar@5.0.0` que también usa Angular y no figura vulnerable).
- `braces@3.0.3`: dependencia **transitiva** de Karma y de su `chokidar@3.6.0`. Advisory [GHSA-vfj7-8cjw-p6xm](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm): agotamiento de pila/denegación de servicio mediante patrones muy anidados, CVSS 7.5, afecta `<=3.0.3`.

`npm audit fix --dry-run --json` no encontró cambios compatibles (0 added/removed/changed; 4 high permanecen). A 2026-10-07, npm publica `karma@6.4.4`, `braces@3.0.3` y `@angular/build@22.2.1` como versiones más recientes en sus series pertinentes. El remedio sugerido por npm es `@angular/build@19.1.9`, **cambio mayor incompatible** con Angular 22/TypeScript 6 (sus peers requieren Angular 19 y TypeScript `<5.8`); no se aplicó `--force`, override ni downgrade. El peer Karma es opcional en el manifiesto de Angular, pero npm lo instala por defecto y por eso aparece en el lockfile y el audit. Seguimiento: revisar cuando se publique una corrección upstream de Karma/braces o un Angular build que no arrastre ese peer.

Verificación en este estado sin cambios de dependencias, con Node v24.21.0: `ng build` PASS (advertencia existente: presupuesto CSS de `src/app/app.css` excedido en 580 bytes), `ng test --watch=false` PASS (9/9). La reducción del riesgo operativo se limita al uso actual de Vitest en lugar de Karma; las cuatro alertas del lockfile siguen **sin resolver**.

## Decisión de riesgo de tooling (2026-10-07)

Tras incorporar Playwright 1.63.0 como dependencia de desarrollo, `npm audit --json` sigue reportando exactamente cuatro `high` (`@angular/build`, `karma`, `chokidar`, `braces`), cero `critical`. No se aplica el downgrade forzado a Angular 19 ni se declara remediada la vulnerabilidad. **Aceptación acotada para desarrollo local de GOAL-01:** Karma no es el runner configurado (se ejecutan Vitest y Playwright), no se procesan patrones no confiables con Karma y las dependencias de desarrollo no se distribuyen en el bundle compilado de la PWA. El riesgo residual afecta al entorno de build/test y queda abierto; antes de release (GOAL-10) repetir audit, revisar si hay versión upstream compatible y decidir remediación o excepción explícita de distribución. Si el alcance de ejecución de Karma cambia, esta aceptación caduca.

Playwright utiliza Chrome instalado localmente (`channel: chrome`) porque la descarga del navegador fijado por Playwright agotó el tiempo de espera en este entorno. El runner E2E conserva versión de Playwright fija, pero el navegador instalado no está fijado: esto es una limitación de reproducibilidad que debe revisarse antes de release.
