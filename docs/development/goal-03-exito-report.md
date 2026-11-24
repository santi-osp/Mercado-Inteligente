# GOAL-03 Éxito — Informe de cierre

Estado: **COMPLETE**, 2026-10-07. Cumple la totalidad del Definition of Done de GOAL-03 según lo establecido en `CLAUDE.md`, `GOALS_MASTER.md` y `PROJECT_MANIFEST.md`, garantizando integración completa y segura con FastStore GraphQL, cotización regional y soporte en API y frontend sin relajar ninguna directriz de seguridad ni consistencia de precios.

---

## 1. Alcance implementado y verificado

### Adaptador y cliente FastStore (`ExitoFastStoreClient`, `ExitoProductMapper`)
- **GraphQL FastStore**: Conexión a `https://www.exito.com/api/graphql` ejecutando `SearchQuery` (`operationName=SearchQuery`).
- **Seguridad en streaming**: Consumo acotado a 2 MiB por respuesta mediante `ResponseHeadersRead`, evitando buffering descontrolado de memoria.
- **Resiliencia controlada**: Un único reintento para fallos transitorios 5xx (500, 502, 503, 504) o errores de socket; códigos 4xx (403, 429) no se reintentan y se tratan como señales de parada inmediatas.
- **Mapeo de catálogo seguro**:
  - Asociación de variantes mediante `isVariantOf.productGroupID` agrupando múltiples SKUs (`items[]`).
  - Extracción de EANs cuando están presentes; rechazo de identidades malformadas o contradictorias.
  - Distinción de vendedores comerciales (`RetailerDirect` para seller `1`), vendedores de marketplace verificados (`16377`, `16025` con `isMkp == true`), y sellers logísticos internos (`exitocol037`, `exitocol031` clasificados como `InternalFulfillment`).
  - Disponibilidad conservadora: inventario FastStore es ecommerce; `UNKNOWN` nunca se asume disponible, y stock 0 se cataloga como `OutOfStock`.

### Contexto regional (`ExitoRegionalPricingService`)
- Resolución anónima de región para Medellín mediante `sessionByCoords` de FastStore, sin almacenamiento ni persistencia de cookies o credenciales de usuario.
- Clasificación estricta de precios en `PriceScope.OnlineGeneric` o `PriceScope.FulfillmentRegion`. En ningún caso se presentan como precios verificados en caja de tienda física.

### Ingesta y persistencia local (`ExitoCatalogIngest`)
- **Evidencia RAW inmutable**: Almacenamiento gzip en `raw/exito/{sha256}.json.gz` con deduplicación por hash criptográfico.
- **Transaccionalidad SQLite**: Ingesta idempotente de productos, variantes y ofertas. Las fluctuaciones de precio registran observaciones históricas (`PriceObservation`).
- **Métricas atómicas en `SyncRun`**: Registro de filas recibidas (`ItemsReceived`), ofertas procesadas (`OffersProcessed`), ofertas modificadas (`OffersChanged`), productos/SKUs creados y actualizados, y conteo de RAW nuevos vs deduplicados.
- **Preservación del último dato válido**: Si una llamada a Éxito falla (red, HTTP 500, o 429), las ofertas locales previas permanecen intactas.

### Fachada y endpoints API (`ExitoProvider`, `Program.cs`)
- Registro de `ExitoProvider` en el contenedor de inyección de dependencias como implementación de `IProvider`.
- Protección contra saturación: Cooldown de 30 segundos por término y semáforo de 1 actualización concurrente.
- Endpoints REST implementados:
  - `GET /api/v1/providers/exito/health`: Reporte de estado y métricas del último `SyncRun`.
  - `GET /api/v1/providers/exito/offers?q=...`: Búsqueda de ofertas guardadas en SQLite local.
  - `POST /api/v1/providers/exito/refresh?q=...`: Sincronización manual bajo demanda.
  - `POST /api/v1/providers/exito/probe`: Diagnóstico puntual acotado genérico (sin mutar SQLite ni escribir SyncRun).
  - `POST /api/v1/providers/exito/probe/regional`: Diagnóstico puntual acotado regional (sin mutar SQLite ni escribir SyncRun).
  - `PUT /api/v1/settings`: Soporte para la bandera `includeMarketplace`.

### Interfaz de usuario (Angular PWA)
- **Panel en Buscar**: Tarjeta de ofertas guardadas de Éxito, con etiquetas claras para vendedor ("Éxito Directo", "Marketplace", "Fulfillment interno"), alcance de precio ("Online genérico", "Regional (Medellín)"), frescura basada en `PriceTtlHours` y botón de actualización manual con estado de enfriamiento.
- **Panel en Fuentes**: Auditoría de métricas de la última sincronización local de Éxito y botones de diagnóstico puntual acotado.
- **Ajustes**: Checkbox para habilitar o inhabilitar la inclusión de ofertas de Marketplace de Éxito.

---

## 2. Resultados de pruebas y verificación técnica

| Componente / Suite | Comando | Resultado |
| :--- | :--- | :--- |
| **Backend Build** | `dotnet build` | **0 Errores, 0 Advertencias** |
| **Backend Tests** | `dotnet test` | **172/172 PASS** (0 fallas, 0 omitidos) |
| **Frontend Tests** | `npm test` | **28/28 PASS** (0 fallas, Vitest) |
| **Frontend Build** | `npm run build` | **PASS** (Bundle optimizado sin errores) |

---

## 3. Auditoría de restricciones no negociables

1. **Secretos nunca en Git/logs/frontend**: No se introdujeron tokens, claves ni cabeceras privadas. FastStore se accede mediante endpoint público anónimo.
2. **UNKNOWN stock no equivale a disponible**: Si la cantidad o estado de FastStore es indeterminado, se marca como `Unknown` y no se cuenta como disponible.
3. **No borrar el último dato válido por fallo**: Si la consulta remota falla o se corta la conexión, la base local mantiene el último registro íntegro.
4. **No confundir precio online con precio físico**: Claramente etiquetado como `OnlineGeneric` o `FulfillmentRegion`.
5. **No usar double/float para dinero**: Todos los cálculos monetarios usan exclusivamente `decimal`.
6. **No IA en el camino crítico**: Mapeos y parsing basados en lógica determinista tipada.
7. **No stack traces hacia UI**: Códigos de error limpios y controlados en la capa API.
