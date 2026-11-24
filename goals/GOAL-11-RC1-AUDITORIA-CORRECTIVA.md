# GOAL 11 — Auditoría correctiva, integración real y Release Candidate 1

## Proyecto

Ruta objetivo:

`C:\Users\santi\Documents\0 REPOSITORIOS\Mercado_Inteligente`

Este goal trabaja **sobre el proyecto existente**. No se debe reiniciar el proyecto ni rehacer la arquitectura desde cero.

# 1. Objetivo

Convertir el estado actual de Mercado Inteligente en un **Release Candidate realmente integrado y verificable**, corrigiendo las inconsistencias detectadas en la auditoría.

No agregar nuevas cadenas de supermercados ni funcionalidades grandes hasta cerrar este goal.

La prioridad es asegurar que el flujo principal funcione de extremo a extremo con datos coherentes, contexto geográfico correcto, frescura adecuada, histórico fiable y release reproducible.

# 2. Lectura obligatoria antes de modificar código

Leer completamente:
- `CLAUDE.md`
- `PROJECT_STATE.md`
- `GOALS_MASTER.md`
- `docs/development/FINAL_PROJECT_REPORT.md`
- documentación relevante de `docs/`
- código actual relacionado con Google, providers, sincronización, optimizador, IA, histórico y frontend

Antes de corregir un hallazgo:
1. confirmar en el código que realmente existe;
2. documentar qué se encontró;
3. corregirlo;
4. agregar o actualizar pruebas;
5. actualizar `PROJECT_STATE.md`.

No confiar únicamente en reportes previos.

# 3. P0 — Google Maps

## 3.1 Dependency Injection
Auditar el registro actual de:
- `IGeocodingProvider`
- `IRoutingProvider`
- `GoogleGeocodingProvider`
- `GoogleRoutingProvider`
- providers Null/fallback

Corregir el diseño para que:
- si Google está correctamente configurado, se use el provider real;
- si Google no está configurado, se use una degradación segura;
- guardar una API key válida realmente habilite las funciones correspondientes;
- la aplicación siga arrancando sin Google.

No registrar simultáneamente implementaciones contradictorias que provoquen que siempre se resuelva el NullProvider.

## 3.2 Migrar Directions Legacy
Eliminar dependencia productiva de:

`https://maps.googleapis.com/maps/api/directions/json`

Implementar Google Routes API v2:

`POST https://routes.googleapis.com/directions/v2:computeRoutes`

Usar:
- `X-Goog-Api-Key`
- `X-Goog-FieldMask`
- JSON request correcto
- origen/destino
- travel mode configurable cuando aplique

Persistir/cachear únicamente los datos necesarios.

## 3.3 Estados Google
Diferenciar al menos:
- `NOT_CONFIGURED`
- `HEALTHY`
- `AUTH_ERROR`
- `QUOTA_EXCEEDED`
- `NETWORK_ERROR`
- `PROVIDER_ERROR`

No convertir todos los fallos en un único estado genérico.

## 3.4 Tests
Crear/actualizar:
- unit tests;
- integration tests;
- tests de fallback;
- live test opcional.

Si falta Google API Key:
`BLOCKED_EXTERNAL`

No marcar PASS ficticio.

# 4. P0 — ShoppingContext y contexto comercial real

Crear o consolidar un concepto explícito:
`ShoppingContext`

Debe poder contener:
- `LocationProfileId`
- dirección normalizada
- latitude
- longitude
- retailer
- contexto externo/provider
- `PriceScope`
- contexto logístico
- fecha de resolución
- estado de validación

## 4.1 D1
La sincronización normal de una cesta debe poder utilizar el contexto regional/fulfillment descubierto en el diagnóstico.
No dejar el contexto D1 implementado pero desconectado del flujo principal.

## 4.2 Éxito
Resolver correctamente cuando sea posible:
- region
- salesChannel
- seller comercial
- fulfillment
- contexto relevante de ecommerce

Mantener diferenciación entre:
- seller directo;
- fulfillment interno;
- Marketplace.

Marketplace continúa excluido por defecto.

## 4.3 Fallback
Si no puede resolverse contexto localizado:
usar explícitamente:
`ONLINE_GENERIC`

y registrarlo como tal.

Nunca simular localización.

# 5. P0 — Precio online/regional vs tienda física

Eliminar cualquier asociación implícita:
`precio regional/online → sucursal física más cercana`

Modelar claramente:
- `PriceScope`
- `PriceContext`
- `PhysicalStore`
- `PurchaseChannel`

Un precio regional puede utilizarse para comparar ecommerce dentro de ese contexto, pero no debe etiquetarse como precio físico de una tienda concreta.

Si se utiliza la sucursal física más cercana para estimar desplazamiento, la UI debe indicar algo equivalente a:

> Precio online/regional. Precio físico en esta sucursal no confirmado.

El optimizador no debe perder esa distinción.

Agregar pruebas que eviten regresiones.

# 6. P0 — Frescura de datos

Consolidar comportamiento:

## FRESH
Candidato normal.

## STALE
Puede utilizarse con advertencia y/o penalización configurable.

## OUTDATED
No debe participar en la recomendación principal por defecto.

Solo permitirlo mediante configuración explícita o fallback claramente indicado.

## UNKNOWN
Penalización fuerte / tratamiento conservador.

El último dato bueno debe seguir visible si una actualización falla.

No borrar o poner null al último precio válido por error externo.

Agregar tests con alternativas de diferentes precios y niveles de frescura para comprobar que un precio muy viejo no gane solamente por ser más barato.

# 7. P0 — Histórico fiable

Auditar `PriceObservation`.

Debe conservar el estado histórico necesario en el momento de observación.

Como mínimo:
- Price
- ListPrice
- AvailabilityStatus
- AvailableQuantity
- PromotionConditions
- PriceScope
- PriceContext
- SellerId
- StoreId
- ObservedAt
- RawCaptureId

No construir una pantalla histórica combinando:
`precio antiguo + metadata actual`

cuando eso pueda producir información incorrecta.

Crear migración EF segura.
Probar migración desde la base actual.

# 8. P0 — SKU, packs y unidades

Ampliar/consolidar `ExternalSku` para conservar:
- `PackageCount`
- `QuantityPerUnit`
- `Unit`
- `TotalQuantity`
- `Dimension`
- `AttributesJson`

Casos de prueba obligatorios:
- `1000 g = 1 kg`
- `1000 ml = 1 L`
- `6 x 1 L = 6 L`
- `2 x 900 g = 1800 g`
- `12 unidades`
- `500 ml != 500 g`

La persistencia debe conservar la información que el parser ya sea capaz de obtener.

# 9. P1 — OpenRouter

## 9.1 Fuente de verdad
Eliminar duplicidad/conflicto entre configuraciones como:
- `AppSettings.AiEnabled`
- `AiSettings.Enabled`

Debe existir una única fuente efectiva de verdad.

## 9.2 Presupuesto
El presupuesto configurado debe tener comportamiento real.

Implementar:
- coste acumulado estimado/real;
- aviso configurable;
- soft limit;
- hard limit opcional;
- bloqueo de nuevas llamadas al superar hard limit.

La app debe seguir funcionando sin IA.

## 9.3 Costos por modelo
Evitar depender exclusivamente de tarifas hardcodeadas genéricas.
Si OpenRouter expone metadata/precios del modelo, utilizarla y cachearla apropiadamente.
Tener fallback conservador si no está disponible.
Nunca bloquear funciones no-IA.

# 10. P1 — Refactor de hotspots

No reescribir la aplicación completa.

## Backend
Dividir `Program.cs` por responsabilidades, por ejemplo:
- `SystemEndpoints`
- `SettingsEndpoints`
- `ProductsEndpoints`
- `BasketEndpoints`
- `SyncEndpoints`
- `LocationEndpoints`
- `AiEndpoints`
- `ProviderEndpoints`

Puede continuar usando Minimal APIs.

`Program.cs` debe quedar principalmente como composición/configuración.

## Angular
Reducir concentración en:
- `app.ts`
- `app.html`

Crear estructura por features, por ejemplo:
- `core/`
- `shared/`
- `features/home/`
- `features/basket/`
- `features/search/`
- `features/compare/`
- `features/history/`
- `features/stores/`
- `features/settings/`

Utilizar Angular Router real.
Mantener comportamiento y diseño.
Agregar pruebas/regresión.

# 11. P1 — Búsqueda escalable

Auditar la búsqueda actual basada en `Contains`.

Implementar o completar búsqueda normalizada compatible con grandes catálogos.

Preferencia:
SQLite FTS5 + ranking.

Indexar cuando sea razonable:
- normalized name
- brand
- category

Priorizar resultados:
1. exactos;
2. prefijos relevantes;
3. nombre;
4. marca;
5. categoría;
6. similitud/ranking FTS.

Agregar pruebas.

# 12. P1 — Tiendas físicas

Revisar el seed actual de tiendas.

No presentar sucursales ficticias/demo como directorio real.

Separar claramente:
- DEMO
- VERIFIED
- UNKNOWN

Si todavía no existe una fuente suficientemente fiable para poblar directorio real:
mantener infraestructura y marcar datos demo.

No inventar direcciones o disponibilidad.

# 13. P1 — Release local reproducible

Crear:
`scripts/bootstrap.ps1`

Debe:
1. verificar Windows/entorno;
2. comprobar versión .NET requerida;
3. comprobar Node/npm cuando sean necesarios;
4. ejecutar `dotnet restore`;
5. ejecutar `npm ci`;
6. preparar base/migraciones;
7. explicar cualquier dependencia faltante.

Crear estrategia de release final donde el usuario no necesite Node para ejecutar la app si es razonablemente posible.

Preferencia:
- Angular precompilado;
- `dotnet publish`;
- archivos preparados;
- script de arranque.

Documentar comandos exactos.

# 14. P1 — Versionado y configuración

Unificar versión:
- backend
- frontend
- PWA/manifest cuando corresponda

Corregir discrepancias como frontend `0.0.0` vs backend `1.0.0`.

Centralizar puertos/configuración.
Eliminar contradicciones como `5078` vs `5000` salvo justificación/documentación.
Corregir mojibake/encoding.
UTF-8 consistente.

# 15. P1 — Privacidad y documentación

Corregir documentación que afirme que ningún dato sale del equipo.

Texto conceptual correcto:

> Mercado Inteligente es local-first. Los datos permanecen locales por defecto. Al activar integraciones externas como Google Maps u OpenRouter, solo los datos necesarios para ejecutar esas funciones se envían al proveedor correspondiente.

Documentar exactamente qué datos pueden salir en cada integración.

No enviar:
- API keys;
- cookies sensibles;
- base completa;
- RAW completo;
- historial innecesario.

# 16. Health y diagnóstico

`/system/health` y `/system/status` deben distinguir:
- backend
- SQLite
- D1
- Éxito
- Ara
- Google Geocoding
- Google Routes
- OpenRouter
- backups
- última sync

No declarar HEALTHY por simplemente existir una clase.

# 17. Evidencia de tests

No utilizar únicamente resúmenes escritos.

Guardar artefactos de ejecución para el RC.

Crear por ejemplo:
`artifacts/test-results/`

Separar:
- `backend-unit`
- `backend-integration`
- `architecture`
- `provider-contract`
- `provider-live`
- `frontend-unit`
- `e2e`
- `smoke`

Guardar TRX/JUnit/Playwright report o equivalentes cuando las herramientas lo permitan.

# 18. Suite obligatoria

Ejecutar:
- `dotnet build`
- `dotnet test`
- Angular build
- Angular tests
- integration tests
- architecture tests
- provider contract tests
- E2E/browser tests
- smoke tests
- SQLite integrity check
- migration test desde DB previa
- backup test
- restore test si está implementado

Live tests:
- D1
- Éxito
- Google
- OpenRouter

deben diferenciarse de fixtures/mocks.

Si una credencial falta:
`BLOCKED_EXTERNAL`

# 19. Flujo end-to-end obligatorio

El goal no se considera cerrado hasta verificar el flujo:

1. usuario abre app;
2. selecciona/escribe ubicación;
3. se resuelve ubicación;
4. se crea ShoppingContext;
5. se resuelve contexto D1;
6. se resuelve contexto Éxito;
7. se sincronizan precios;
8. cada precio conserva scope/context correcto;
9. usuario crea cesta;
10. matching selecciona candidatos;
11. optimizador excluye datos inválidos/outdated según política;
12. calcula alternativas;
13. distancia/rutas se calculan si Google está disponible;
14. UI diferencia precio online/regional de precio físico;
15. se muestra recomendación;
16. cerrar aplicación;
17. abrir nuevamente;
18. datos/settings/histórico persisten;
19. health/status siguen correctos.

Si Google no puede probarse por falta de key:
todo el flujo restante debe probarse y Google queda `BLOCKED_EXTERNAL`.

# 20. No agregar nuevas tiendas

Durante este goal NO implementar:
- Jumbo
- Olímpica
- Carulla
- Makro
- Euro
- La Vaquita
- PriceSmart

Primero estabilizar D1 + Éxito + Ara parcial.

# 21. Entregables

Crear:
`docs/development/GOAL-11-RC1-AUDIT-FIX-REPORT.md`

Debe incluir:
- hallazgos confirmados;
- correcciones;
- migraciones;
- Google;
- D1;
- Éxito;
- frescura;
- histórico;
- OpenRouter;
- refactor;
- tests;
- live tests;
- BLOCKED_EXTERNAL;
- flujo E2E;
- deuda restante;
- decisión final.

La decisión final debe ser una sola:

`READY FOR 1.0.0`

o

`NOT READY FOR 1.0.0`

con argumentos.

# 22. Actualizar PROJECT_STATE

Al finalizar, `PROJECT_STATE.md` debe contener:
- GOAL-11;
- estado;
- migraciones;
- decisiones;
- test results;
- BLOCKED_EXTERNAL;
- release readiness.

# 23. Definition of Done

No cerrar el goal hasta comprobar:

- [ ] Google DI corregido
- [ ] Routes API v2 implementada
- [ ] fallback sin Google funcional
- [ ] ShoppingContext integrado
- [ ] D1 usa contexto apropiado cuando está disponible
- [ ] Éxito usa contexto apropiado cuando está disponible
- [ ] online/regional no se presenta como físico verificado
- [ ] OUTDATED no domina recomendaciones por defecto
- [ ] histórico conserva metadata histórica
- [ ] packs/unidades persisten correctamente
- [ ] IA tiene única fuente de verdad
- [ ] presupuesto IA funciona realmente
- [ ] Program.cs reducido/modularizado
- [ ] Angular modularizado y Router real
- [ ] búsqueda preparada para catálogo grande
- [ ] datos demo de tiendas se identifican correctamente
- [ ] bootstrap/release reproducible
- [ ] versiones/puertos/documentación consistentes
- [ ] privacidad documentada correctamente
- [ ] tests ejecutados
- [ ] evidencia de tests guardada
- [ ] SQLite integrity OK
- [ ] migraciones verificadas
- [ ] backup verificado
- [ ] flujo E2E validado
- [ ] PROJECT_STATE actualizado
- [ ] informe final creado

# 24. Regla final

No marques elementos como completados si:
- son placeholders;
- solo existen en documentación;
- no fueron probados;
- dependen de una credencial ausente;
- el flujo principal todavía no los utiliza.

Usa:
- `DONE`
- `PARTIAL`
- `BLOCKED_EXTERNAL`
- `FAILED`
- `NOT_APPLICABLE`

No declares `v1.0.0` hasta que el informe de este goal concluya explícitamente:

`READY FOR 1.0.0`
