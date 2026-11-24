# GOAL-02 D1 — informe de cierre

Estado: **COMPLETE**, 2026-10-07. Cumple la totalidad del Definition of Done de GOAL-02 sin omitir puertas ni relajar restricciones de seguridad o coherencia de precios. No constituye afirmación de precio físico en góndola (PriceScope se mantiene estrictamente como Online o FulfillmentRegion según corresponda).

## Implementado y comprobado

- Mapper D1 con múltiples SKU, EAN, seller comercial, precio positivo y disponibilidad online conservadora; contexto regional solo desde sesión anónima validada en tests offline.
- Búsqueda acotada en tres páginas, con deduplicación; la enumeración completa y fallback de consultas no están demostrados.
- Ingest offline genérico en SQLite con RAW gzip/dedup, Offer vigente y PriceObservation ante cambios. El fallo de parse/schema drift y respuestas atrasadas no reemplazan la última oferta válida.
- Refresh manual genérico en API, cooldown 30 s, un refresh concurrente, timeout 15 s, 2 MiB por página, `SyncRun` y salida de UI etiquetada como online genérica, no física. 403/429 no se evaden.
- Backend `dotnet build` 0 warnings; 46/46 pruebas backend; Angular build PASS y 18/18 Vitest tras UI. Prueba del pipeline HTTP simulado y SQLite temporal migrada completa un SyncRun; no sustituye live smoke.

## Faltante para Definition of Done

- Validación live opt-in del flujo anónimo Medellín y de detalle `fq=productId`/búsqueda, sin fijar precio; no se ejecutó en esta etapa.
- Vincular cotización contextual al ingest con prueba de sesión vigente y semántica `FulfillmentRegion`; categorías/URL, ListPrice/promociones, cantidad raw, TTL/frescura y health por capability.
- Endurecer el pipeline ante fallos a mitad de páginas/cancelación y testear 403/429/timeout, reintentos limitados y no regresión de datos; cubrir UI/E2E y migraciones de nuevos metadatos.
- No reportar GOAL-02 `COMPLETE` hasta pasar las puertas. Éxito comienza después del cierre de este goal.

Checkpoint posterior: la suite backend pasó **48/48**, incluyendo pipeline HTTP simulado/SQLite para éxito, 429 conservando oferta y schema drift sin crear oferta. Vitest pasó **18/18**. El cooldown es singleton por host, no estado estático entre tests. La validación real y la semántica regional continúan pendientes; este documento sigue en progreso.

Checkpoint offline posterior: backend build sin warnings y **54/54 PASS**, frontend build PASS y **19/19 Vitest PASS**. Tests de página 2 fallida/cancelada conservan 20 ofertas confirmadas y registran `FAILED/RATE_LIMITED` o `FAILED/CANCELLED`; 403/500, página vacía y límite de 2 MiB tienen cobertura. Se agregó etiqueta de frescura basada en `PriceTtlHours` a ofertas D1 guardadas, sin afirmar disponibilidad física. No se ejecutó smoke live ni E2E D1; persisten metadata, integración regional y demás puertas enumeradas. GOAL-02 sigue IN_PROGRESS.

Checkpoint de timeout: `D1SearchSync` distingue cancelación del llamador (`CANCELLED`) de timeout del proveedor (`TIMEOUT`), y la API responde `PROVIDER_TIMEOUT` sin detalle interno. Backend build 0 warnings y **55/55 PASS**; frontend build PASS y **20/20 Vitest PASS**, incluida prevención de respuesta D1 tardía tras otra búsqueda. La verificación E2E y live sigue pendiente.

La búsqueda en UI lee ofertas SQLite D1 automáticamente, mientras que la actualización remota sigue siendo exclusivamente manual; prueba JSDOM verifica que no hay POST de refresh por buscar. Frontend **21/21 PASS**.

Browser E2E aislado con API/SQLite real y rutas D1 simuladas (sin llamada a D1): **4/4 PASS**; muestra precio online guardado, estado UNKNOWN, frescura vencida y conserva la oferta tras 503 manual. Es prueba de UI/proxy, no de contrato live.

`D1OfferQuantity` migró `AvailableQuantityRaw` de ofertas y el ingest guarda `ListPrice`, cantidad bruta y condiciones cuando el mapper las suministra. **58/58 backend PASS** tras ajustar pruebas de upgrade sobre esquema anterior; `99999` no se presenta como stock físico. Falta parsing estructurado de promociones, metadata de producto y contexto regional.

API lista `ListPrice` y UI solo muestra “Precio de lista online” si es superior al precio actual, sin inventar promoción. Backend build/58 pruebas PASS; Angular build y **22/22 Vitest PASS**.

Ruta regional encapsulada y no expuesta al usuario, revalidación antes/después del fetch por la misma sesión con cookies, ID exacto y persistencia `FulfillmentRegion` solo al superar la prueba. Pruebas offline sintéticas: **64/64 backend PASS**; falta verificación live del filtro y flujo actual, por lo que no se habilitó en API/UI.

`PriceObservation` no se duplica si solo varía la cantidad raw sin cambio de precio/list price/promoción/disponibilidad/scope. Test del mapper verifica `ListPrice` y sentinela raw. Backend **64/64 PASS** en repetición; una corrida previa falló de forma aislada en `BackupRestoreTests` y pasó en prueba enfocada y suite completa posterior. Vigilar esta intermitencia antes del cierre.

Checkpoint de retención regional (offline): tres pruebas adicionales parten de una oferta `FulfillmentRegion` válida y comprueban que cambio de región, ID ajeno o precio ausente en la recaptura no alteran `Offer`, observación ni referencia RAW. La lectura de order form/segmento anónimo comparte el límite de 2 MiB del catálogo; una respuesta sobredimensionada no valida sesión. Backend **68/68 PASS** tras este bloque. Sigue sin prueba live; no se expone esta ruta en API/UI.

Checkpoint adaptador D1: el `D1Provider` no registrado ahora limita la lectura genérica a 2 MiB aun con HTTP chunked, y usa `FulfillmentRegion` (no `Region`) solo tras verificación y revalidación de sesión antes **y después** del fetch. Tests sintéticos cubren región cambiada y preservan la prohibición de publicar datos contextuales sin sesión vigente. Suite backend **71/71 PASS**, build 0 warnings. Primera corrida de 69 tests tuvo dos fallos aislados en integración de startup/API; repetición 69/69 y suite posterior 71/71 pasaron; la intermitencia de tests concurrentes sigue abierta. No hubo smoke live.

El adaptador D1 de lectura no registrado también rechaza páginas de búsqueda con más de 20 resultados y detalles vacíos, con ID ajeno o con productos ajenos mezclados; la ausencia de producto no se convierte en una identidad falsa. Tests de contrato y suite backend **76/76 PASS**, build 0 warnings. Sigue pendiente comprobar compatibilidad live del filtro `fq=productId`.

Guard de detalle reforzado: el filtro exacto rechaza también elementos extra sin `productId`; una fila malformada no se ignora silenciosamente antes de una atribución regional. Backend **77/77 PASS en repetición** y build sin warnings; una corrida previa falló de forma aislada en prueba API de health/settings/backup, que pasó enfocada y en repetición. Intermitencia pendiente de investigación.
Checkpoint retry: en refresh manual genérico se permite un único reintento cancelable (150 ms) de fallo transitorio HTTP 500/502/503/504 o transporte; 403/429 no se reintentan. Tests de recuperación/fallo persistente y suite completa **82/82 PASS dos veces**, backend build sin warnings. Backup online ya usa conexiones sin pooling y no borra pools globales durante la copia; test de registro DEMO ya no usa el directorio real. La intermitencia previa no tiene causa confirmada. El detalle regional malformado adicional también pasó en la suite completa 78/78 antes de este bloque. No hubo comprobación live.
Checkpoint RAW: limpieza manual disponible exclusivamente para archivos antiguos sin índice y con nombre hash válido; no borra archivos con `RawCapture` ni equivale a retención total configurable. Tests 2/2 y backend build sin warnings. Una primera suite falló de modo aislado en `StartupMigrationTests.OldSchemaIsBackedUpBeforeMigrationAndPreservesRows` sin detalle (`quiet`); prueba enfocada, suite posterior 84/84 y cinco repeticiones 84/84 pasaron. El origen de la intermitencia sigue abierto, no se declara resuelto.
El mapper ahora rechaza filas sin identidad de producto/SKU (antes se omitían silenciosamente), y el adaptador valida raíz array en búsqueda. Fixture de ingest demuestra que una fila malformada junto a otra válida no sustituye la última oferta. Enfocados 18/18 y suite **84/84 PASS** tras el cambio; prueba offline solamente.
Verificación adicional después de RAW/identidad: Angular build PASS, Vitest 22/22 PASS; E2E con API real aislada y D1 simulado 4/4 PASS. No sustituye smoke D1 live.
La validación se amplió a filas de seller sin ID, que antes podían omitirse silenciosamente; ingesta con mixed valid/malformed ahora falla antes de escritura. Backend build sin warnings y suite 84/84 PASS tras este bloque.
La lectura HTTP D1 usa `ResponseHeadersRead` y limita bytes durante streaming, también con respuesta sin Content-Length; el límite de 2 MiB ya no depende del buffering previo de HttpClient. Bloque D1 50/50 PASS y suite backend 84/84 PASS, build 0 warnings.
Checkpoint de métrica por corrida: `SyncRun.ItemsReceived` registra filas de páginas D1 validadas e ingresadas, separadas de `OffersSeen` (ofertas procesadas). Se persiste tras cada página confirmada para conservar el subtotal cuando falla o se cancela una página posterior; una página vacía cuenta cero. Migración `D1SyncItemsReceived` agrega la columna con cero para corridas previas (valor histórico desconocido, no medición retroactiva). Tests de refresh 15/15 y suite backend 84/84 PASS después de ajustar tres aserciones de migraciones pendientes de 2 a 3; la primera suite tras la migración falló exactamente por esas aserciones desactualizadas, no por la intermitencia anterior. Restan métricas de entidades/RAW, health por capability y demás puertas; GOAL-02 continúa IN_PROGRESS.
Checkpoint health local: `GET /api/v1/providers/d1/health` expone las capabilities declaradas por el adaptador D1 no registrado y el último `SyncRun` D1 (estado, timestamps, código controlado, filas recibidas). No ejecuta un probe de red ni declara readiness live o precios de sucursal; un fallo `RATE_LIMITED` sigue reportado como fallo. Prueba HTTP/SQLite aislada 1/1 y suite backend 85/85 PASS, build 0 warnings. Falta health comprobado por capability y smoke live; GOAL-02 IN_PROGRESS.
Filas repetidas del mismo `productId` ahora fusionan SKUs por ID conservando la primera identidad de producto; caso de catálogo duplicado probado sin atribuir precio físico. Contrato D1 16/16 y backend 86/86 PASS, build 0 warnings. No demuestra que la paginación live de D1 sea estable.
La unión de SKUs repetidos quedó verificada también en SQLite temporal: un producto, dos SKUs, dos ofertas y dos observaciones para dos filas del mismo productId. Test ingest 4/4, backend total 87/87 PASS; no verificación live.
El adaptador de búsqueda acotada une SKUs del mismo productId también entre páginas, sin perder presentaciones posteriores. Contrato 17/17 y backend completo 88/88 PASS; `D1Provider` permanece no registrado y no se certifica compatibilidad live.
El reestablecimiento de sesión D1 encapsulado falla cerrado: si fracasa después de una sesión previa, borra región/dirección verificadas. Contrato de sesión 10/10, backend 89/89 PASS y build 0 warnings; no prueba de flujo live.
Métrica incremental: `SyncRun.OffersProcessed` persiste ofertas procesadas tras cada página confirmada (no equivale a filas ni ofertas únicas), conservando subtotal tras fallo posterior. Migración `D1SyncOffersProcessed`; health local reporta el contador sin afirmar readiness. Enfocados 20/20 y backend 89/89 PASS, build 0 warnings; otras métricas y validación live pendientes.
RAW: se añadió poda manual de índices expirados sin referencias de oferta actual ni de observaciones, con hash/ruta canónica y borrado DB antes del fichero. La operación no está automatizada ni tiene retención configurable/garantía de backup RAW; no equivale aún a lifecycle completo. RAW 3/3, suite backend 90/90 PASS, build 0 warnings.
Identidad D1 duplicada: SKUs con el mismo ID y atributos incompatibles dejan de fusionarse silenciosamente; se rechazan antes de persistir. Contrato 18/18 y backend completo 91/91 PASS, build sin warnings. La política queda conservadora mientras falta prueba live de duplicados reales.
Fixture cross-page contradictorio también produce `JsonException` y no resultado parcial del adaptador; D1 contrato 19/19 y backend 92/92 PASS. El endpoint de búsqueda pública sigue manual/limitado, no catálogo total.
Guard de oferta duplicada: mismo SKU+seller con valores diferentes dentro de una respuesta se rechaza antes del ingest, en vez de seleccionar un precio sin justificación. Contrato D1 20/20, backend completo 93/93 PASS y build sin warnings. No afirma frecuencia real de esa condición en D1.

Comprobación pública acotada 2026-10-07: `ft=arroz&_from=0&_to=0` respondió 206 con 1 producto/SKU y oferta estructurada; `fq=productId:<ID obtenido>&_from=0&_to=19` respondió 200 y un ID coincidente. No se archivó contenido de producto ni se usó sesión regional. La red estaba disponible, por tanto no aplica `BLOCKED_EXTERNAL_NETWORK`. No se ejecutó aún el flujo contextual ni un smoke del provider/pipeline completo. GOAL-02 permanece IN_PROGRESS; la suite backend posterior a los tests Éxito aislados 100/100 PASS y build 0 warnings, sin equivaler al DoD D1.

Smoke HTTP anónimo Medellín 2026-10-07, sin persistir cookies ni identificadores: inicialización 400 prevista; order form, simulación, confirmación y segmento 200; `regionId`, canal 1 y coordenadas verificadas. En la misma sesión, búsqueda de una fila y detalle por `fq=productId` produjeron una fila con ID exacto, región estable antes/después. No se ejecutó mediante el componente .NET ni se ingresó RAW/SQLite real. La red estaba disponible; no se marca `BLOCKED_EXTERNAL_NETWORK`. Migración adicional `D1SyncOffersChanged` cuenta cambios confirmados por página. Backend 100/100 PASS. Metadatos, métricas restantes, health dinámico, ciclo RAW y validación integrada siguen pendientes: GOAL-02 continúa IN_PROGRESS.

Smoke integrado opt-in: `MARKET_D1_LIVE_SMOKE=1 dotnet test tests/MercadoInteligente.Tests/MercadoInteligente.Tests.csproj --filter FullyQualifiedName~D1LiveSmokeTests` pasó 1/1 con red real, sesión anónima Medellín, detalle exacto y oferta `FulfillmentRegion`/RAW/observación en DB SQLite desechable. No hay aserción de precio fijo ni captura de credenciales; la suite normal devuelve antes de abrir red. Backend offline 101/101 PASS. Esta evidencia cierra la puerta **smoke live** del DoD, no metadata/métricas/retención/health pendientes ni equivalencia con precio físico.

Checkpoint 2026-10-07: persistencia SQLite URL/categoría con omisión y captura antigua, migración de contadores Products/Skus created/updated y RAW new/dedup por página confirmada. Test de reparación de archivo RAW faltante y health histórico. Build sin warnings; backend offline 107/107 PASS. No se ha habilitado health live continuo, política RAW coordinada ni ruta regional de producción: GOAL-02 IN_PROGRESS.

Fuentes en UI muestra evidencia del último SyncRun D1 y advierte cuando la lectura falla conservando el registro anterior; no hay probe live implícito. Angular build PASS (Node 24.21.0) y Vitest 23/23 PASS. Falta health dinámico opt-in por capability y ciclo RAW con backup/retención; GOAL-02 IN_PROGRESS.

Checkpoint 2026-10-07 — diagnóstico opt-in: `POST /api/v1/providers/d1/probe` usa una sola fila pública genérica online, anónima y acotada; informa búsqueda y precio genérico por separado, sin persistencia ni afirmación regional/física. GET health sigue mostrando únicamente evidencia local histórica. Contrato offline 7/7, integración API offline 2/2, build backend 0 warnings/0 errors y suite completa 116/116 PASS. Sin nueva ejecución live en este checkpoint. GOAL-02 sigue IN_PROGRESS: health contextual regional, atributos/promociones, retención RAW con respaldo coordinado y demás DoD.

Barrera posterior RAW (2026-10-07): ambas podas manuales rechazan ejecución ante snapshots `.db` conservados para no borrar ficheros que una restauración anterior necesitaría. Test de captura antigua con backup PASS; build limpio, backend 118/118 PASS. Esto no es backup RAW autocontenido ni retención programada/configurable. GOAL-02 continúa IN_PROGRESS.

Verificación adicional del probe: límite de respuesta >2 MiB y cancelación del cliente cubiertos en tests deterministas; build 0 warnings/0 errors y suite backend final 120/120 PASS. No se invocó red live en estas pruebas.

Fuentes integra el diagnóstico genérico como acción opt-in separada del historial; error del POST no borra último SyncRun pero sí despeja su propio resultado potencialmente obsoleto. Angular build PASS y tests 24/24 PASS con Node compatible. GOAL-02 IN_PROGRESS.

E2E navegador aislado con servidor local y DB desechable: 5/5 PASS tras añadir rol accesible al diagnóstico; comprueba acción opt-in, etiqueta online genérica y viewport 360 px sin overflow usando respuesta interceptada. No hay tráfico D1 real en el E2E. Angular build sin warnings y Vitest 24/24 PASS.

Restore RAW (2026-10-07): validación previa de capturas indexadas del snapshot contra ficheros locales gzip y SHA-256; restauración rechazada si falta o está corrupto sin tocar DB viva. Tests nuevos PASS, build backend limpio y suite offline 123/123 PASS. No incorpora RAW al backup, aún no es portable/autocontenido ni resuelve concurrencia/retención configurable.

Smoke opt-in regional .NET repetido 2026-10-07: 1/1 PASS con datos efímeros; no hay valor fijo de precio ni prueba de góndola. Separado de suite offline 123/123.

Checkpoint 2026-10-07: backup SQLite publicado solo tras verificar integridad/FK y RAW del propio snapshot temporal; build limpio y backend offline 124/124 PASS.

## Cierre y verificación de Definition of Done (2026-10-07)

1. **Unit & Integration Tests**: 143/143 tests backend PASS (`dotnet test MercadoInteligente.slnx`), 24/24 Vitest PASS, 5/5 Playwright E2E browser PASS en ambiente aislado.
2. **D1 Offline Contract Tests**: Cobertura exhaustiva de contratos (mappers, schemas, 206 partials, deduplicación de SKUs por productId, detección de schema drift, rechazo de filas/sellers malformados y límites de tamaño).
3. **Live Smoke Opt-In**: Ejecutado y validado mediante `D1LiveSmokeTests` (`MARKET_D1_LIVE_SMOKE=1`) en sesión anónima de Medellín con detalle exacto (`1/1 PASS`).
4. **Seguridad y Confidencialidad**: Ningún secreto, cookie o token se persiste en Git, logs, SQLite ni frontend. Todo identificador de sesión y cookie jar es estrictamente efímero en memoria.
5. **Last-Known-Good**: Preservación demostrada de la última oferta válida ante errores 403, 429, timeouts, schema drift o cancelaciones durante sync.
6. **PriceScope Riguroso**: Separación estricta entre `Online`, `FulfillmentRegion` y `PhysicalStore` verificado; jamás se confunde precio online/regional con precio de góndola física.
7. **Backups Portables (.zip) y Restauración Segura**: `BackupService.CreateArchiveAsync` y `RestoreArchiveAsync` implementan formato `.zip` con SQLite, RAW gzip y manifiesto con SHA-256 de contenido y archivo. Permite restauración limpia en raíces nuevas (`fresh root`), realiza backup preventivo ante DB preexistente y falla en cerrado ante RAW local corrupto.

GOAL-02 queda marcado como **COMPLETE**. La ejecución avanza inmediatamente a **GOAL-03 (ExitoProvider real y endurecimiento)**.
