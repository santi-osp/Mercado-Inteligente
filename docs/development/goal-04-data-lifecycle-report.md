# Informe de Desarrollo — GOAL-04: Ciclo de vida de datos, sincronización y mantenimiento

## 1. Resumen Ejecutivo

El objetivo **GOAL-04** implementa el motor integral de ciclo de vida de datos, políticas de frescura con TTLs por dominio, deduplicación de histórico de precios mediante delta compression, detección de anomalías de precios, ciclo de vida de productos externos (Active, Missing, Inactive), política de retención Grandfather-Father-Son (GFS) para backups de base de datos, tareas programadas de mantenimiento y recuperación de sincronizaciones abortadas.

Asimismo, se conectó la experiencia de usuario en la Progressive Web Application (PWA) desarrollada en Angular 21, proporcionando visibilidad honesta del almacenamiento local (SQLite, RAW, logs, backups), disparadores de optimización de base de datos (`PRAGMA optimize`) y verificación de integridad, sincronización dirigida de listas de compra activas (Priority 2 Basket Sync), y avisos no bloqueantes de modo sin conexión.

---

## 2. Componentes Implementados

### 2.1 Backend (.NET 10 / C# 14 / EF Core 10)

1. **Evaluación de Frescura y TTLs (`FreshnessService.cs`)**:
   - Estados: `Fresh`, `Stale`, `Outdated`, `Unknown`.
   - Protección contra skew de reloj (marcas temporales en el futuro se tratan como válidas/frescas).
   - TTLs configurables por dominio:
     - Precios online: 6 horas (vencido a 18 horas).
     - Stock / Disponibilidad: 2 horas (vencido a 6 horas).
     - Promociones: 4 horas (vencido a 12 horas).
     - Catálogo de productos: 7 días (vencido a 21 días).
     - Tiendas / Sedes: 7 días.
     - Rutas / Geocoding: 24 horas.

2. **Detección de Anomalías de Precios (`PriceAnomalyDetector.cs`)**:
   - Estados: `Valid`, `Suspicious`, `Invalid`.
   - Precios $\le 0$ o precios de lista $\le 0$ son marcados como `Invalid`.
   - Caídas extremas ($> 80\%$) o subidas extremas ($> 500\%$) sobre el precio anterior son clasificadas como `Suspicious`.
   - Precios de oferta que superan el 150% del precio de lista son clasificados como `Suspicious`.

3. **Ciclo de Vida de Productos Externos (`ProductLifecycleService.cs`)**:
   - Estados: `Active`, `Missing`, `Inactive`, `Unknown`.
   - Transiciones: Un producto ausente en sincronizaciones sucesivas incrementa su contador de ausencias; superado el umbral (2 ausencias), transiciona a `Missing`. Tras 30 días sin ser visto, transiciona a `Inactive`. Al reaparecer en una sincronización, se reactiva automáticamente a `Active` reseteando el contador.

4. **Historial Inteligente de Precios (`SmartPriceHistoryService.cs`)**:
   - Registro de observaciones de precio (`PriceObservation`) exclusivamente cuando existe una variación real en precio, precio de lista, disponibilidad o condiciones promocionales.
   - Si no hay cambio, se actualiza `CheckedAt` en la oferta sin inflar la tabla de observaciones (delta compression).

5. **Mantenimiento y Retención GFS (`MaintenanceService.cs`)**:
   - Política Grandfather-Father-Son:
     - 7 copias diarias más recientes.
     - 4 copias semanales (una por semana en las últimas 4 semanas).
     - 3 copias mensuales (una por mes en los últimos 3 meses).
   - Depuración de capturas RAW expiradas (por defecto retención configurable de 60 días).
   - Depuración de archivos de log obsoletos (> 14 días).
   - Ejecución segura de `PRAGMA optimize` y verificación de claves foráneas (`PRAGMA foreign_key_check`).
   - Cálculo del desglose de almacenamiento (`GetStorageStatsAsync`): bytes de base de datos `.db`, sidecars (`-wal`, `-shm`), capturas RAW comprimidas (`.json.gz`), logs y copias de seguridad.

6. **Motor de Sincronización y Recuperación (`SyncEngine.cs`)**:
   - Niveles de prioridad:
     - P0: Sincronización manual bajo demanda del usuario.
     - P1: Sincronización al buscar productos nuevos en la UI.
     - P2: Sincronización periódica de productos en listas activas ("Mi mercado").
     - P3: Sincronización en segundo plano de productos frecuentes/básicos.
   - Detección y recuperación de ejecuciones colgadas al arranque del servicio (`AbortHangingRunsAsync`).

7. **Endpoints HTTP de Ciclo de Vida y Sistema (`Program.cs`)**:
   - `GET /api/v1/system/storage`: métricas de ocupación en disco y conteos de entidades.
   - `POST /api/v1/system/maintenance`: disparo manual de mantenimiento integral.
   - `POST /api/v1/sync/basket`: sincronización enfocada de productos en canasta.
   - `GET /api/v1/sync/runs`: auditoría de ejecuciones de sincronización.

---

### 2.2 Frontend (Angular 21 PWA)

1. **Panel de Almacenamiento y Mantenimiento (`app.html`, `app.ts`, `app.css`)**:
   - Tarjetas de estadísticas de almacenamiento formateadas en B, KB, MB o GB sin notaciones confusas.
   - Botón "Ejecutar mantenimiento" con reporte de resultados en tiempo real (DB optimizada, copias depuradas, RAW depurados).
   - Botón "Sincronizar precios de la lista" en la vista "Mi mercado" con reporte transparente de consultas ejecutadas por proveedor.
   - Banner accesible de modo sin conexión (`.offline-banner`) que informa la conservación de los datos locales sin bloquear el uso de la aplicación ni borrar listas.

---

## 3. Verificación y Pruebas

- **Backend (.NET 10)**:
  - 181 pruebas ejecutadas exitosamente (`dotnet test`).
  - Pruebas unitarias y de integración cubriendo `FreshnessService`, `PriceAnomalyDetector`, `ProductLifecycleService`, `SmartPriceHistoryService`, `MaintenanceService` (GFS + `PRAGMA optimize`), `SyncEngine` (recuperación de ejecuciones colgadas) y endpoints de API en `Goal04DataLifecycleTests.cs`.
- **Frontend (Angular 21 / Vitest)**:
  - 32 pruebas unitarias ejecutadas exitosamente (`npm test`).
  - Cobertura de telemetría de almacenamiento, disparo y reporte de mantenimiento, sincronización de canasta y banner de modo sin conexión.
  - Generación de bundle de producción limpia sin advertencias de presupuesto (`npm run build`).

---

## 4. Estado de Cumplimiento

- **Definition of Done de GOAL-04**: **CUMPLIDO AL 100%**.
- Se preserva el principio fundamental de no borrar datos válidos por fallos externos ni exponer credenciales.
- Se mantiene el uso exclusivo de `decimal` para precios y cálculos monetarios.
- No se utiliza IA en el camino crítico de sincronización ni mantenimiento.
