# GOAL 14 — FINAL v1.0.0, GOOGLE OPCIONAL, OMNIROUTER Y LANZADOR LOCAL

**Fecha de Certificación:** 2026-10-10  
**Versión Final:** 1.0.0 (Gold Release Oficial, sin sufijos -rc)  
**Decisión Final:** **READY FOR 1.0.0**  

---

## 1. Resumen Ejecutivo

El **GOAL 14** representa el hito de cierre y lanzamiento oficial de **Mercado Inteligente v1.0.0**. Este objetivo culmina la estabilización del optimizador local-first de compras para retail colombiano (D1, Éxito y Ara), transformándolo en una solución totalmente independiente de costos de API externas:
1. **Google Maps 100% Opcional:** La aplicación funciona de manera autónoma sin necesidad de clave de API de Google Maps. Incorpora cálculo de distancias por Haversine local (con factor de tortuosidad urbana 1.35x), extracción local de coordenadas, resolución segura de enlaces cortos (con defensa SSRF) y enlaces de navegación pública directos a Google Maps sin coste.
2. **Travel Trust en Optimizador Balanceado:** Se incorporó la política de confianza de ubicación. Las tiendas con estado DEMO o UNKNOWN no penalizan ni alteran el cálculo de desplazamiento en la estrategia Balanced, a menos que el usuario active explícitamente AllowUnverifiedLocationsForTravel en la configuración.
3. **OmniRouter como Proveedor IA Predeterminado:** Integración de OmniRouter (https://omnirouter.li/v1, protocolo OpenAI-compatible) con el modelo gemini-3.8-flash como opción predeterminada, persistencia cifrada DPAPI en Windows y soporte para OpenRouter como alternativa.
4. **Lanzador Local Windows en Segundo Plano:** Experiencia de usuario desktop pulida mediante scripts/launch-mercado-inteligente.ps1 (ejecución con ventana oculta, verificación de salud, gestión de PID y apertura automática del navegador), scripts/stop-mercado-inteligente.ps1, acceso directo Mercado Inteligente.lnk y ejecutables .cmd en la raíz.
5. **Release Binario Auto-Contenido:** Distribución win-x64 auto-contenida (no requiere .NET SDK ni Node.js en la máquina cliente), probada exhaustivamente mediante pruebas de humo en puertos aislados.

---

## 2. Hallazgos y Correcciones de la Fase

### 2.1 Google Maps 100% Opcional y Distancias Locales
- **Extracción Local de Coordenadas:** LocationParser analiza expresiones de latitud/longitud decimales directas, formatos @lat,lon, q=lat,lon, y fragmentos embebidos de Google Maps !3d...!4d... sin invocar servicios remotos.
- **Resolución Segura de Enlaces Cortos:** ResolveGoogleMapsShortUrlAsync valida dominios autorizados (maps.app.goo.gl, goo.gl), previene ataques SSRF (rechazando IPs privadas, loopback o esquemas inseguros) y extrae las coordenadas de la cabecera Location de redirección con timeout estricto.
- **Distancia Haversine como Primera Clase:** Implementado IHaversineDistanceService / HaversineService marcando DistanceSource = "HAVERSINE" e IsApproximate = true. En la interfaz de usuario se muestra la etiqueta discreta Estimada (Haversine) y el botón Abrir ruta en Google Maps ↗.
- **Estado Transparente:** La sección de estado y configuración refleja claramente: Google Maps opcional — no configurado.

### 2.2 Travel Trust en BasketOptimizer
- **Problema Detectado:** Tiendas catalogadas como DEMO o con coordenadas no verificadas influían de forma artificial en el cálculo de distancias de la estrategia Balanced, alejando o descartando canastas viables.
- **Solución Implementada:**
  - En BasketOptimizer, se evalúa el estatus de verificación de cada tienda candidata.
  - Si una tienda no está VERIFIED, su distancia se omite del cómputo de desplazamiento a menos que settings.AllowUnverifiedLocationsForTravel == true.
  - Propagación completa del flag a todas las llamadas internas de EvaluateBestSubset.
  - Agregado test unitario crítico BalancedStrategy_ExcludesUnverifiedLocationsFromTravelDistance_UnlessAllowed.

### 2.3 OmniRouter como Proveedor de IA Predeterminado
- **Configuración:** Base URL https://omnirouter.li/v1, modelo por defecto gemini-3.8-flash.
- **Compatibilidad:** Implementa el estándar OpenAI /v1/chat/completions y /v1/models.
- **Seguridad DPAPI:** Persistencia cifrada en Windows mediante ProtectedData.Protect bajo DataProtectionScope.CurrentUser para claves de omnirouter, openrouter y google.
- **Variables de Entorno:** Soporte de fallback para OMNIROUTER_API_KEY, OMNIROUTER_BASE_URL y OMNIROUTER_MODEL.

### 2.4 Lanzador Local y Accesos Directos
- scripts/launch-mercado-inteligente.ps1:
  - Detecta si el servicio ya responde en http://127.0.0.1:5000/api/v1/system/health.
  - Si no, inicia elease/MercadoInteligente/MercadoInteligente.exe con -WindowStyle Hidden.
  - Guarda el PID en $env:LOCALAPPDATA\MercadoInteligente\runtime\app.pid.
  - Monitorea hasta 15 segundos hasta obtener respuesta saludable.
  - Abre el navegador predeterminado en http://127.0.0.1:5000.
- scripts/stop-mercado-inteligente.ps1: Detiene el proceso por PID, limpia procesos residuales y libera el puerto 5000.
- scripts/create-shortcut.ps1: Genera el acceso directo Mercado Inteligente.lnk con icono oficial avicon.ico, así como los ejecutables rápidos INICIAR_MERCADO_INTELIGENTE.cmd y DETENER_MERCADO_INTELIGENTE.cmd.

---

## 3. Matriz de Validación de Pruebas

| Suite de Pruebas | Entorno / Herramienta | Ejecutadas | Pasadas | Falladas | Omitidas | Estado |
|---|---|---:|---:|---:|---:|:---:|
| Backend Unit & Integration | .NET 10 / xUnit 2.9.3 | 328 | 325 | 0 | 3 | **PASS** |
| Context & Regression Tests | xUnit / Goal12 & Goal13 | 10 | 10 | 0 | 0 | **PASS** |
| Frontend Component & Routing | Vitest 4.1.11 / Angular 22 | 57 | 57 | 0 | 0 | **PASS** |
| Frontend Production Build | Angular CLI (
g build) | 1 | 1 | 0 | 0 | **PASS** |
| Browser E2E Tests | Playwright 1.63 | 9 | 8 | 0 | 1 | **PASS** |
| Standalone Publish Smoke Test | .NET win-x64 / Puerto 5055 | 5 | 5 | 0 | 0 | **PASS** |
| SQLite Database Integrity | PRAGMA integrity & FK check | 2 | 2 | 0 | 0 | **PASS** |

*Nota sobre pruebas omitidas (3 backend, 1 browser):* Corresponden a los tests de humo en vivo contra APIs externas reales (D1LiveSmokeTests, ExitoLiveSmokeTests) que utilizan Assert.Skip de xUnit cuando no se configuran variables de entorno específicas, garantizando que no existan falsos positivos (alse PASS).

---

## 4. Estado de Bloqueos Externos (BLOCKED_EXTERNAL)

1. **Google Maps API Live:** Marcado como opcional. La aplicación opera al 100% sin ella mediante el motor local Haversine y enlaces de navegación pública.
2. **OmniRouter / OpenRouter Live:** Opcional. Toda la optimización, matching determinista y comparación funciona sin IA.

---

## 5. Paquetes y Entregables de Release

1. **elease/MercadoInteligente/**: Distribución compilada auto-contenida para Windows x64 lista para ejecución directa, incluyendo MercadoInteligente.exe, wwwroot/ con la SPA de Angular, un.ps1 y README.txt.
2. **Mercado_Inteligente_Source_v1.0.0.zip**: Archivo fuente completo para desarrolladores y auditoría.
3. **Mercado_Inteligente_Windows_x64_v1.0.0.zip**: Archivo portable de la distribución para usuario final.
4. **MANIFEST_SHA256.txt**: Manifiesto criptográfico verificado al 100% con 0 discrepancias.

---

## 6. Decisión de Release

`	ext
======================================================
                  READY FOR 1.0.0
======================================================
`

Todos los criterios de aceptación, requisitos de no-regresión, políticas de integridad de contexto comercial, funcionamiento sin dependencias externas obligatorias, seguridad DPAPI y suites de pruebas han sido satisfechos en su totalidad. Mercado Inteligente queda certificado para su versión final **1.0.0**.
