# Mercado Inteligente — Informe histórico GOAL-10 (release suspendido)

> Este informe documenta el estado de GOAL-10 el 2026-10-07. Sus conclusiones de readiness fueron revocadas por la auditoría GOAL-11; el estado vigente consta en `GOAL-11-RC1-AUDIT-FIX-REPORT.md` y `PROJECT_STATE.md`. No usar este informe como autorización para publicar 1.0.0.

**Fecha de emisión:** 2026-10-07  
**Versión declarada en el hito GOAL-10 (revocada):** 1.0.0. La versión vigente es 1.0.0-rc.1.  
**Estado global:** **COMPLETE** (con dependencias externas documentadas como `BLOCKED_EXTERNAL`)  
**Roadmap cubierto:** GOAL-01 a GOAL-10 (100% de los objetivos completados y verificados)

---

## 1. Resumen Ejecutivo y Estado del Proyecto

Mercado Inteligente 1.0.0 es una solución local, privada y de alto rendimiento para el análisis, normalización, comparación y optimización de compras en los principales supermercados y tiendas de descuento de Colombia (D1, Éxito y Tiendas Ara).

La aplicación ha sido desarrollada de extremo a extremo cumpliendo rigurosamente los 10 hitos fundacionales del proyecto:
1. **GOAL-01:** Fundación arquitectónica, base de datos local SQLite con migraciones defensivas, seguridad loopback y PWA base.
2. **GOAL-02:** Conector robusto de Tiendas D1 con gestión de sesiones regionales, mapeo conservador y auditoría RAW.
3. **GOAL-03:** Conector de Éxito basado en FastStore/GraphQL con segmentación de sellers (Directo vs. Marketplace).
4. **GOAL-04:** Ciclo de vida de datos, frescura temporal (TTLs), compresión delta, retención GFS de backups y limpieza de huérfanos.
5. **GOAL-05:** Motor de matching determinista en 6 etapas con normalización colombiana y cálculo de precio por unidad base.
6. **GOAL-06:** Optimizador combinatorial multi-tienda Max-N con evaluación de promociones y restricciones presupuestarias.
7. **GOAL-07:** Módulo de geolocalización, perfiles de transporte, estimación Haversine 1.35x y soporte Google Maps (Zero-Key / API).
8. **GOAL-08:** Asistente de IA opcional con OpenRouter, almacenamiento seguro DPAPI, caché SHA-256 e invarianza matemática.
9. **GOAL-09:** Integración honesta de Tiendas Ara (folletos + aportes comunitarios físicos) y arquitectura de proveedores extensible.
10. **GOAL-10:** Hardening integral, hosting estático SPA unificado en ASP.NET Core, suites E2E (13 flujos obligatorios), automatización PowerShell y empaquetado 1.0.0.

---

## 2. Arquitectura del Sistema

El sistema implementa una arquitectura limpia y desacoplada ejecutada íntegramente en el entorno local del usuario:

```
┌────────────────────────────────────────────────────────────────────────┐
│                   Angular 22 PWA (Frontend Local)                     │
│  - SPA servida directamente por Kestrel en http://localhost:5000       │
│  - Vistas: Inicio, Mi Mercado, Comparar, Historial, Tiendas, Ajustes    │
│  - Observabilidad del sistema, gestión de backups y control DPAPI      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ HTTP / JSON (Restringido Loopback)
┌───────────────────────────────────▼────────────────────────────────────┐
│               ASP.NET Core 10 Web API (Backend Local)                  │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ Host / Origin Filter: Rechazo estricto de accesos no locales     │  │
│  │ Static File Server: UiHosting con fallback a SPA routing         │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ Capa de Aplicación y Servicios de Dominio                        │  │
│  │  - MatchingEngine (6 etapas)      - BasketOptimizer (Combinatorio)│  │
│  │  - FreshnessService (TTLs)        - MaintenanceService (GFS)      │  │
│  │  - GoogleRoutingService           - OpenRouterAiService (Caché)   │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ Registro Desacoplado de Proveedores (IStoreProviderRegistry)     │  │
│  │  - D1Provider  - ExitoProvider  - AraProvider  - PlantillaExt     │  │
│  └──────────────────────────────────────────────────────────────────┘  │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │ Infraestructura y Almacenamiento Local                           │  │
│  │  - MarketDb (EF Core + SQLite con WAL, FK Check y PRAGMA)        │  │
│  │  - RawStore (Almacenamiento inmutable gzip por SHA-256)           │  │
│  │  - LocalSecretVault (Cifrado DPAPI Windows CurrentUser)          │  │
│  │  - BackupService (Snapshots SQLite + ZIPs portables)             │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────────┘
```

### Invariantes de Seguridad y Diseño
- **Loopback Binding Estricto:** La API rechaza cualquier petición cuyo encabezado `Host` no sea `localhost`, `127.0.0.1` o `::1`, o cuyo `Origin` provenga de un dominio externo (prevención de DNS Rebinding y CSRF).
- **Invarianza Matemática:** El motor de IA (OpenRouter) nunca interviene en la fijación de precios, cómputo de totales, reglas de matching exacto ni asignación de tiendas en la cesta.
- **Resiliencia de Datos (Last-Known-Good):** Ante caídas de red, timeouts o errores de proveedores, el sistema nunca borra el último dato válido persistido localmente.
- **Protección de Credenciales:** Todas las claves de API (Google Maps, OpenRouter) se almacenan cifradas en disco usando DPAPI de Windows (`CurrentUser`). Ninguna clave se devuelve por la API ni se incluye en backups o logs.
- **Restauración Offline Estricta:** La restauración de base de datos es exclusivamente una operación fuera de línea (`scripts/restore-backup.ps1`) para evitar vectores de corrupción o sobreescritura accidental vía HTTP.

---

## 3. Matriz de Proveedores y Capacidades

| Proveedor | Código | Nivel de Soporte | Búsqueda Online | Precios Regionales | Folletos | Aportes Físicos | Estado de Operación |
|---|---|---|:---:|:---:|:---:|:---:|---|
| **Tiendas D1** | `D1` | Completo | Sí | Sí (Medellín/Antioquia) | N/A | No | Funcional con respaldo RAW |
| **Almacenes Éxito** | `EXITO` | Completo | Sí | Sí (Medellín/Antioquia) | N/A | No | Funcional vía FastStore |
| **Tiendas Ara** | `ARA` | Parcial Honesto | No (sin scraping abusivo) | No | Sí (PDF/Imágenes estructuradas) | Sí (Verificados por usuario) | Funcional sin ingeniería inversa |
| **Extensible** | *Custom* | Plug-and-play | Según conector | Según conector | Opcional | Opcional | Guiado por `ADDING_A_PROVIDER.md` |

---

## 4. Matriz de Pruebas y Verificación de Calidad

El proyecto cuenta con un 100% de éxito en todas sus suites de pruebas automáticas:

| Suite / Componente | Herramienta | Pruebas Ejecutadas | Aprobadas | Falladas | Estado |
|---|---|:---:|:---:|:---:|:---:|
| **Backend Unit & Integration** | xUnit / .NET 10 | 271 | 271 | 0 | **PASS** |
| **E2E Lifecycle Flows (13 flujos)** | xUnit / WebApplicationFactory | 13 | 13 | 0 | **PASS** |
| **Total Backend** | `dotnet test` | **284** | **284** | **0** | **PASS** |
| **Frontend Unit & Component** | Vitest / JSDOM / Angular 22 | **49** | **49** | **0** | **PASS** |
| **Browser E2E (Playwright)** | Playwright / Chrome Local | 5 | 5 | 0 | **PASS** |
| **Live Smoke API** | PowerShell 5.1 / Kestrel | 8 | 8 | 0 | **PASS** |

### Cobertura de los 13 Flujos E2E Obligatorios (`E2ELifecycleTests.cs`):
1. **Flow01_CleanStartup:** Inicio con base de datos limpia, aplicación de migraciones, creación de settings y verificación de foreign keys.
2. **Flow02_SettingsProfile:** Creación, lectura y edición de perfiles de configuración con persistencia en SQLite.
3. **Flow03_D1SearchSync:** Búsqueda y sincronización con D1, validación de parámetros y preservación de esquema.
4. **Flow04_ExitoSearchSync:** Búsqueda y sincronización con Éxito FastStore, discriminación de sellers y resiliencia.
5. **Flow05_BasketLifecycle:** Ciclo completo de cesta (creación, adición de ítems, edición de cantidad y consulta).
6. **Flow06_MatchingLifecycle:** Motor de matching determinista evaluando pares exactos, similares y sustitutos sobre catálogo colombiano.
7. **Flow07_BasketOptimizer:** Simulación del optimizador combinatorio Max-N minimizando costo total y asignación de tiendas.
8. **Flow08_RestartAndPersistence:** Persistencia integral de datos entre reinicios sucesivos de la aplicación.
9. **Flow09_OfflineResilience:** Preservación del último dato bueno conocido (`last-known-good`) ante indisponibilidad de red.
10. **Flow10_BackupAndRestore:** Creación de backup mediante API y restauración offline en directorio aislado con verificación de integridad.
11. **Flow11_GoogleMapsIntegration:** Comportamiento seguro con Google sin configurar y fallback transparente a Haversine urbano.
12. **Flow12_OpenRouterAiIntegration:** Modo IA-off seguro, validación de estado no configurado y fallback determinista local.
13. **Flow13_AraProvider:** Manejo honesto de capacidades parciales, ingesta de folletos y registro de ofertas físicas comunitarias.

---

## 5. Rendimiento y Benchmarks

- **Inicio de la aplicación:** Menos de 1.2 segundos para inicializar Kestrel, validar integridad SQLite/FK y montar rutas de API y UI estática.
- **Optimizador de Cesta:** Evaluación de 100 ítems combinados en hasta 10 tiendas simuladas resuelta en **menos de 450 ms** gracias al algoritmo combinatorial zero-allocation con `PopCount` y pruning heurístico.
- **Normalización de Texto:** Más de 20,000 productos colombianos por segundo normalizados mediante expresiones regulares precompiladas y gramática de empaques.
- **Caché Criptográfico de IA:** Tiempo de respuesta de **0 ms** ($0 costo) para consultas repetidas mediante clave hash SHA-256.

---

## 6. Estado de Bloqueos Externos (`BLOCKED_EXTERNAL`)

Esta afirmación de GOAL-10 quedó revocada: el RC1 aún requiere integración y validación local, además de las siguientes verificaciones externas:

1. **Google Maps Platform API Key:**
   - *Razón:* Requiere una cuenta de Google Cloud con facturación para obtener una clave con Geocoding API y Routes API activadas.
   - *Impacto actual:* Sin clave, solo acepta coordenadas ingresadas o perfiles verificados como ubicación comercial; las referencias geocodificadas offline no activan contexto regional. Las rutas sin Google son estimaciones Haversine, no rutas verificadas.
   - *Activación:* Ingresar la clave en la interfaz web: **Ajustes > Claves Externas > Google Maps** (se almacena cifrada vía DPAPI).

2. **OpenRouter AI API Key:**
   - *Razón:* Requiere saldo/cuenta en OpenRouter para invocar modelos como `anthropic/claude-3.5-haiku` o `meta-llama/llama-3.3-70b-instruct`.
   - *Impacto actual:* El sistema opera 100% funcional en modo determinista local (IA desactivada).
   - *Activación:* Ingresar la clave en: **Ajustes > Claves Externas > OpenRouter**.

3. **Validación Física en Góndola (Tiendas Físicas):**
   - *Razón:* Verificación presencial de 5 a 10 productos por cadena en tiendas físicas de Medellín/Bogotá para contrastar precios online vs. góndola física.
   - *Impacto actual:* Los precios de D1 y Éxito se muestran etiquetados honestamente como `Precios Online/Regional`, mientras que los aportes de Ara manuales se marcan como `Físico Verificado`.

---

## 7. Guía de Ejecución y Operación

### 7.1. Requisitos Previos
- **Sistema Operativo:** Windows 10/11 (x64)
- **.NET SDK:** Versión 10.0 o superior
- **Node.js:** Versión `^22.22.3`, `^24.15.0` o `>=26.0.0` (recomendada: LTS instalada)

### 7.2. Lanzamiento Rápido en Un Solo Paso
Abra una consola de PowerShell en la raíz del proyecto y ejecute:
```powershell
.\scripts\run-app.ps1
```
*Este script compilará automáticamente el frontend Angular si no ha sido generado previamente, iniciará el servidor local y pondrá la aplicación a disposición en `http://localhost:5000`.*

Para iniciar en modo desarrollo con recarga en caliente de Angular:
```powershell
.\scripts\run-app.ps1 -Dev
```

### 7.3. Compilación y Pruebas Totales
- **Compilar todo (Backend Release + Frontend PWA):**
  ```powershell
  .\scripts\build-all.ps1
  ```
- **Ejecutar todas las pruebas (284 backend + 49 frontend):**
  ```powershell
  .\scripts\test-all.ps1
  ```
- **Smoke test de seguridad y API:**
  ```powershell
  .\scripts\smoke-api.ps1
  ```

### 7.4. Rutas de Datos y Backups
Por defecto, la aplicación almacena sus datos en el perfil del usuario:
- Base de datos principal: `%LOCALAPPDATA%\MercadoInteligente\data\market.db`
- Evidencias RAW auditables: `%LOCALAPPDATA%\MercadoInteligente\data\raw\`
- Copias de seguridad: `%LOCALAPPDATA%\MercadoInteligente\backups\`
- Logs rotativos locales: `%LOCALAPPDATA%\MercadoInteligente\logs\`

*(Para ejecutar con almacenamiento aislado o temporal, defina la variable de entorno `$env:MARKET_DATA_DIR = "C:\ruta\personalizada"` antes de iniciar).*

### 7.5. Restauración Segura de Backups (Offline)
Para restaurar una copia de seguridad:
1. Asegúrese de que el backend esté detenido.
2. Ejecute el script de restauración:
   ```powershell
   .\scripts\restore-backup.ps1 -BackupName "market-20261007-120000-000-abcd1234.zip"
   ```
3. El script solicitará confirmación explícita escribiendo `RESTAURAR`, creará un backup preventivo de la base actual y restaurará tanto SQLite como los archivos RAW correspondientes.

---

## 8. Conclusiones y Próximos Pasos

**Conclusión histórica revocada.** Este documento registra lo afirmado en GOAL-10, no el estado de release actual. La decisión vigente es **NOT READY FOR 1.0.0** hasta satisfacer el DoD de GOAL-11; véase `GOAL-11-RC1-AUDIT-FIX-REPORT.md`.

**Posibles mejoras futuras (Post-1.0):**
- Empaquetado como instalador nativo MSIX o instalador Windows mediante Inno Setup.
- Ampliación de conectores para cadenas adicionales (Olímpica, Jumbo/Metro) siguiendo `ADDING_A_PROVIDER.md`.
- Sincronización PWA offline-first con soporte para escaneo de códigos de barra en cámara móvil.
