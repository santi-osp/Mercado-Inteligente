# Proveedor Éxito — Adaptador FastStore GraphQL y sincronización local

## 1. Resumen y arquitectura

El proveedor de Éxito (`ExitoProvider`) conecta el comparador local con la plataforma FastStore de Almacenes Éxito S.A. Proporciona búsqueda de catálogo, extracción de ofertas comerciales y cotización regional para la zona de Medellín, persistiendo los datos de manera estructurada en SQLite local y almacenando la evidencia RAW inmutable y deduplicada.

### Componentes clave
- **`ExitoFastStoreClient`**: Cliente HTTP de bajo nivel para GraphQL (`https://www.exito.com/api/graphql`), con streaming seguro delimitado a 2 MiB por respuesta, reintentos únicos para errores transitorios de red/5xx, y respeto estricto a códigos 4xx (403, 429).
- **`ExitoProductMapper`**: Analizador semántico que transforma las respuestas de `SearchQuery` en entidades de dominio (`Product`, `ProductVariant`/SKU, `Offer`), diferenciando sellers y aplicando reglas de stock conservadoras.
- **`ExitoRegionalPricingService`**: Servicio para resolver el contexto de sesión regional de Medellín mediante coordenadas geográficas (`sessionByCoords`), sin cookies persistentes ni filtración de credenciales.
- **`ExitoCatalogIngest`**: Ingestor local que ejecuta transacciones seguras en SQLite, gestiona el almacenamiento gzip RAW deduplicado, actualiza `SyncRun` con métricas precisas y registra observaciones históricas (`PriceObservation`) ante fluctuaciones de precio.
- **`ExitoProvider`**: Fachada registrada en la inyección de dependencias (`IProvider`) que expone capacidades, health y refresco controlado con cooldown de 30 segundos y límite de 1 refresco concurrente.

---

## 2. Protocolo y consultas FastStore

### Endpoint y operación
- **URL Base:** `https://www.exito.com/api/graphql`
- **Operación principal:** `SearchQuery` (`operationName=SearchQuery`)
- **Facetas estándar:**
  - `vendido-por=exito`: Inyectada automáticamente cuando el usuario no ha habilitado ofertas de marketplace.
  - `channel={"salesChannel":"1","regionId":"..."}`: Canal de venta minorista y región de entrega.
  - `locale=es-CO`: Localización colombiana estándar.

### Ámbitos de precio (`PriceScope`)
1. **`OnlineGeneric` (`PriceScope.OnlineGeneric`)**: Catálogo y precio público del portal web sin geolocalización de entrega fijada.
2. **`FulfillmentRegion` (`PriceScope.FulfillmentRegion`)**: Catálogo y precio contextualizado para fulfillment en el área metropolitana de Medellín.

> **Regla de oro de precios:** Los precios online o regionales **NUNCA** se confunden con precios de góndola ni cajas de tiendas físicas. En la interfaz y el modelo de datos se rotulan explícitamente como ofertas online.

---

## 3. Modelo de vendedores y seguridad comercial

Para evitar distorsiones en comparaciones de precios y preservar la confianza del usuario, Éxito clasifica rigurosamente a cada vendedor (`SellerKind`):

| SellerKind | Identificadores conocidos | Descripción y comportamiento |
| :--- | :--- | :--- |
| **`RetailerDirect` (1)** | `sellerId == "1"`, `isMkp == false` | Venta directa oficial de Éxito. Es la oferta comercial primaria. |
| **`Marketplace` (2)** | `sellerId` verificado (`16377`, `16025`), `isMkp == true` | Vendedores aliados en la plataforma. **Excluidos por defecto**. Solo se persisten y visualizan si el usuario activa `includeMarketplace` en Ajustes. |
| **`InternalFulfillment` (3)** | `exitocol037`, `exitocol031` | Centros de distribución y bodegas logísticas internas. **No son vendedores comerciales independientes**; no se eligen como precio menor arbitrario. |
| **`Unknown` (0)** | Cualquier otro `sellerId` no verificado | Vendedor con esquema no reconocido. Se descarta para evitar registrar ofertas falsas o no homologadas. |

---

## 4. Disponibilidad y stock

- El valor `AvailableQuantity` o `inStock` de FastStore es una estimación de inventario para comercio electrónico, no una garantía de stock físico en pasillo.
- Si la disponibilidad es `UNKNOWN` (desconocida), **nunca se asume disponible**.
- Si el stock reportado es 0, se mapea estrictamente como `OutOfStock`.

---

## 5. Endpoints locales expuestos en la API

| Método | Ruta | Propósito | Reglas de seguridad |
| :--- | :--- | :--- | :--- |
| `GET` | `/api/v1/providers/exito/health` | Consulta estado y métricas del último `SyncRun`. | Lectura exclusiva de SQLite local. |
| `GET` | `/api/v1/providers/exito/offers?q=...` | Consulta ofertas locales guardadas en SQLite. | No hace llamadas HTTP a Éxito. Búsqueda local por texto. |
| `POST` | `/api/v1/providers/exito/refresh?q=...` | Actualiza ofertas desde FastStore bajo demanda. | Cooldown de 30 segundos, máx 1 concurrente. No borra datos previos en fallo. |
| `POST` | `/api/v1/providers/exito/probe` | Diagnóstico puntual genérico online. | Bounded (1 pág, 2 MiB). **No muta SQLite ni escribe SyncRun**. |
| `POST` | `/api/v1/providers/exito/probe/regional` | Diagnóstico puntual regional (Medellín). | Bounded (1 pág, 2 MiB). **No muta SQLite ni escribe SyncRun**. |

---

## 6. Tolerancia a fallos y preservación de datos

1. **Persistencia del último dato bueno (*Last-Known-Good*):**
   Si una consulta de refresco a Éxito falla (por timeout de red, error 500, o enfriamiento por rate limit 429), la aplicación **conserva todas las ofertas locales previamente guardadas**. Nunca se reemplaza ni vacía una oferta válida ante una respuesta incompleta o errónea.
2. **Evidencia RAW inmutable:**
   Cada respuesta JSON válida de GraphQL se comprime en gzip y se indexa por su hash SHA-256 (`raw/exito/{hash}.json.gz`). Si el contenido es idéntico al de una captura existente, se deduplica sin duplicar espacio en disco.
3. **Métricas de sincronización (`SyncRun`):**
   Se registran de forma atómica:
   - Filas recibidas (`ItemsReceived`)
   - Ofertas procesadas (`OffersProcessed`)
   - Ofertas modificadas (`OffersChanged`)
   - Productos creados/actualizados
   - SKUs creados/actualizados
   - Capturas RAW nuevas vs deduplicadas

---

## 7. Ajustes de usuario y UI

- **Opción en Ajustes:** `includeMarketplace: boolean` (por defecto `false`).
- **Vista Buscar:** Tarjeta independiente para ofertas Éxito guardadas, indicando el tipo de vendedor ("Éxito Directo", "Marketplace", "Fulfillment interno"), alcance de precio ("Online genérico", "Regional (Medellín)"), disponibilidad y frescura según el TTL configurado.
- **Vista Fuentes:** Diagnósticos directos y auditoría de la última sincronización local.
