# GOAL 14 — FINAL v1.0.0, GOOGLE OPCIONAL, OMNIROUTER Y LANZADOR LOCAL

Trabaja sobre el proyecto existente en:
`C:\Users\santi\Documents\0 REPOSITORIOS\Mercado_Inteligente`

Este goal culmina la versión final definitiva 1.0.0 de Mercado Inteligente.

## OBJETIVOS PRINCIPALES

1. Google Maps 100% Opcional & Distancias Locales:
   - App 100% funcional sin API key de Google.
   - Extracción local de coordenadas (lat, lon, @lat,lon, q=lat,lon, !3d...!4d...).
   - Resolución segura de short URLs de Google Maps con protección SSRF y timeout.
   - Reverse geocoding opcional si Google está configurado para enriquecer AddressComponents.
   - Distancias sin Google vía IHaversineDistanceService / HaversineService con DistanceSource="HAVERSINE" e IsApproximate=true.
   - Botón público "Abrir ruta en Google Maps" sin requerir API key.
   - Estado en UI: "Google Maps opcional — no configurado".

2. Travel Trust en Balanced Optimizer:
   - Tiendas DEMO o UNKNOWN no alteran por defecto el scoring de distancia de estrategia Balanced.
   - Solo tiendas VERIFIED participan en el cálculo de distancia por defecto.
   - Opción AllowUnverifiedLocationsForTravel (default: false).

3. OmniRouter como Proveedor IA Predeterminado:
   - Base URL: https://omnirouter.li/v1.
   - Protocolo OpenAI-compatible (/v1/chat/completions, /v1/models).
   - Renombrar/adaptar OmniRouterAiProvider en backend y UI.
   - Almacenamiento seguro DPAPI en Windows para "omnirouter", "openrouter", "google".
   - Variables de entorno OMNIROUTER_API_KEY, OMNIROUTER_BASE_URL, OMNIROUTER_MODEL.
   - Modelo predeterminado: Gemini Flash 3.8 mediante /v1/models.

4. Lanzador Local Windows & Acceso Directo:
   - Acceso directo real en raíz: Mercado Inteligente.lnk.
   - Script lanzador: scripts/launch-mercado-inteligente.ps1.
   - Script de parada: scripts/stop-mercado-inteligente.ps1.
   - Script generador: scripts/create-shortcut.ps1.
   - Fallback CMD en raíz: INICIAR_MERCADO_INTELIGENTE.cmd y DETENER_MERCADO_INTELIGENTE.cmd.

5. Versión y Release v1.0.0:
   - Versión 1.0.0 unificada.
   - Publicación self-contained win-x64 en release/MercadoInteligente/.
   - Paquetes duales: Mercado_Inteligente_Source_v1.0.0.zip y Mercado_Inteligente_Windows_x64_v1.0.0.zip.
   - MANIFEST_SHA256.txt actualizado y verificado.
   - Reporte final docs/development/GOAL-14-FINAL-V1-RELEASE-REPORT.md con decisión READY FOR 1.0.0.
