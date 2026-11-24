**COMPARADOR INTELIGENTE DE MERCADO**

Arquitectura Técnica

Stack, módulos, datos, integraciones, seguridad, resiliencia, testing y operación local.

| **Documento** | Technical Architecture Specification                                                     |
|---------------|------------------------------------------------------------------------------------------|
| **Versión**   | 1.0                                                                                      |
| **Fecha**     | 5 de octubre de 2026                                                                     |
| **Estado**    | Aprobado como especificación base para desarrollo                                        |
| **Alcance**   | Aplicación local en Windows · Medellín · D1/Éxito + Ara parcial · IA opcional OpenRouter |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Decisión de diseño</strong></p>
<p>Este documento elimina decisiones implícitas. Cuando exista una recomendación explícita, se considera el comportamiento predeterminado de V1 salvo que una evidencia técnica posterior obligue a cambiarla.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

# Contenido

1\. Decisiones de arquitectura

2\. Stack y versiones base

3\. Topología local

4\. Estructura del repositorio

5\. Capas y módulos

6\. Modelo de datos

7\. SQLite y almacenamiento RAW

8\. Store Providers

9\. Sincronización y caché

10\. Búsqueda y matching

11\. Optimizador

12\. Google Maps Platform

13\. OpenRouter

14\. Seguridad y secretos

15\. API local

16\. Resiliencia y observabilidad

17\. Backups y migraciones

18\. Testing

19\. Performance

20\. Configuración

21\. Flujo de ejecución local

22\. Plan de implementación

23\. Referencias técnicas

# 1. Decisiones de arquitectura

La solución será un modular monolith local. No hay justificación para microservicios, colas distribuidas ni base de datos remota en V1.

| **ADR** | **Decisión**                             | **Motivo**                                              |
|---------|------------------------------------------|---------------------------------------------------------|
| ADR-001 | ASP.NET Core .NET 10 LTS                 | Backend estable LTS y buen soporte Windows/HTTP/SQLite  |
| ADR-002 | Angular 22 estable                       | Frontend moderno; signals/standalone; soporte activo    |
| ADR-003 | SQLite + EF Core                         | Un usuario, local, bajo costo y backups simples         |
| ADR-004 | RAW gzip en filesystem + metadata SQLite | Evita duplicación/peso; preserva reproducibilidad       |
| ADR-005 | Loopback only                            | No autenticación necesaria; reduce superficie de ataque |
| ADR-006 | OpenRouter opcional                      | Un endpoint OpenAI-compatible y selección de modelo     |
| ADR-007 | Google Geocoding + Routes                | Precisión y soporte para rutas; sin mapa embebido V1    |
| ADR-008 | Providers desacoplados                   | Cambios de D1/Éxito no contaminan dominio               |
| ADR-009 | Signals + servicios                      | Evitar NgRx mientras la complejidad no lo justifique    |
| ADR-010 | Contract tests                           | Detectar temprano cambios en fuentes externas           |

# 2. Stack y versiones base

Versiones validadas para el arranque del proyecto al 5 de octubre de 2026. Las actualizaciones de patch deben aplicarse sin cambiar el diseño.

| **Componente**  | **Elección**                                                  |
|-----------------|---------------------------------------------------------------|
| Runtime backend | .NET 10 LTS (último patch estable)                            |
| Lenguaje        | C# 14                                                         |
| Web backend     | ASP.NET Core Minimal APIs/Controllers por módulo              |
| ORM             | Entity Framework Core 10 + Microsoft.Data.Sqlite              |
| Frontend        | Angular 22.x estable, standalone components, Signals          |
| Estilos         | SCSS + CSS Custom Properties; design system propio            |
| Iconos          | Lucide Angular                                                |
| Fuente          | Inter Variable empaquetada localmente                         |
| Charts          | Chart.js únicamente para históricos cuando se requiera        |
| Tests backend   | xUnit + WebApplicationFactory                                 |
| Tests frontend  | Vitest/Angular TestBed                                        |
| E2E             | Playwright                                                    |
| Logging         | Serilog rolling files + console dev                           |
| HTTP resilience | Microsoft.Extensions.Http.Resilience / políticas equivalentes |
| IA              | OpenRouter HTTP API OpenAI-compatible                         |
| Maps            | Google Geocoding API + Routes API                             |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>No usar .NET 11 RC</strong></p>
<p>Aunque existe una release candidate, V1 debe iniciar sobre .NET 10 LTS para soporte y estabilidad.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

# 3. Topología local

En producción local habrá un solo origen. ASP.NET Core sirve la API y los assets compilados de Angular. Kestrel escucha exclusivamente en loopback.

Windows 11  
┌──────────────────────────────────────────────────────────┐  
│ Browser / PWA instalada │  
│ http://127.0.0.1:5280 │  
└───────────────────┬──────────────────────────────────────┘  
│ mismo origen  
┌───────────────────▼──────────────────────────────────────┐  
│ ASP.NET Core 10 │  
│ API + Angular static files + BackgroundServices │  
├───────────┬───────────────┬───────────────┬──────────────┤  
│ SQLite │ raw/\*.json.gz │ StoreProviders│ External APIs│  
└───────────┴───────────────┴───────────────┴──────────────┘  
│  
OpenRouter / Google / Stores

- Producción local: bind a 127.0.0.1, no 0.0.0.0.

- Desarrollo: Angular dev server usa proxy hacia ASP.NET Core.

- No CORS en build local final porque todo comparte origen.

- Un script \`start-local.ps1\` verifica prerequisitos, inicia backend y abre navegador.

# 4. Estructura del repositorio

La estructura prioriza módulos de negocio y contratos claros, no capas genéricas gigantes.

/src  
/backend  
Market.Api  
Market.Application  
Market.Domain  
Market.Infrastructure  
Market.Providers.D1  
Market.Providers.Exito  
Market.Providers.Ara  
/frontend  
market-web  
/tests  
Market.Domain.Tests  
Market.Application.Tests  
Market.IntegrationTests  
Market.ContractTests  
e2e  
/docs  
/scripts  
start-local.ps1  
backup.ps1  
diagnostics.ps1

Los providers pueden ser proyectos separados porque cambian a ritmos distintos y requieren contract tests específicos. No son microservicios: se cargan en el mismo proceso.

# 5. Capas y módulos

Dependencias apuntan hacia el dominio. Infrastructure implementa contratos; Providers no exponen modelos externos al resto del sistema.

| **Módulo**     | **Responsabilidad**                                                     |
|----------------|-------------------------------------------------------------------------|
| Domain         | Entidades canónicas, value objects, reglas puras, interfaces esenciales |
| Application    | Use cases, orchestration, DTOs internos, optimización, matching         |
| Infrastructure | EF Core, filesystem RAW, secretos, backup, Google/OpenRouter clients    |
| Providers.\*   | Adaptadores de D1/Éxito/Ara; mapeo externo → modelos intermedios        |
| Api            | Endpoints, DI, validación, health, static files                         |
| Frontend       | UX, state local, API client, design system                              |

Api → Application → Domain  
Api → Infrastructure → Application/Domain contracts  
Providers → Application/Domain contracts  
Domain → (ninguna dependencia de infraestructura)

# 6. Modelo de datos

El esquema V2 del diagnóstico es la base. Las entidades canónicas se separan de representaciones externas para poder re-normalizar sin perder evidencia.

| **Entidad**       | **Propósito**                                                   |
|-------------------|-----------------------------------------------------------------|
| Retailer          | Cadena: D1, Éxito, Ara.                                         |
| Store             | Sucursal física o entidad geográfica conocida.                  |
| ExternalProduct   | Producto según una fuente.                                      |
| ExternalSku       | Variante comprable; EAN, presentación, atributos.               |
| Seller            | Vendedor/fulfillment; clasificación direct/marketplace/unknown. |
| Offer             | Oferta actual por SKU+seller+contexto.                          |
| PriceObservation  | Observación histórica inmutable.                                |
| RawCapture        | Metadata de payload RAW deduplicado.                            |
| CanonicalProduct  | Concepto de producto normalizado.                               |
| ProductVariant    | Marca/tipo/tamaño/atributos comparables.                        |
| ProductMatch      | Relación calculada entre ExternalSku y ProductVariant.          |
| MatchOverride     | Decisión manual prioritaria.                                    |
| Basket/BasketItem | Lista y restricciones del usuario.                              |
| LocationProfile   | Dirección normalizada y coordenadas.                            |
| ComparisonProfile | Tolerancias, pesos, límites.                                    |
| SyncRun           | Auditoría de sincronizaciones.                                  |
| ProviderHealth    | Estado derivado/últimos fallos.                                 |
| AiUsage           | Modelo, tokens, costo estimado y feature.                       |

## 6.1 Campos críticos de Offer

Offer  
- ExternalSkuId  
- SellerId  
- StoreId?  
- ContextKey / RegionId?  
- Price  
- ListPrice?  
- Currency = COP  
- AvailabilityStatus (Available / OutOfStock / Unknown)  
- AvailableQuantity?  
- PromotionMetadata?  
- PriceScope  
- CheckedAt  
- SourceUrl / RawCaptureId

# 7. SQLite y almacenamiento RAW

SQLite es la base operacional. Los payloads originales se almacenan una sola vez, comprimidos y deduplicados.

- Activar \`PRAGMA foreign_keys=ON\`.

- Usar WAL para lecturas concurrentes y escrituras controladas.

- Configurar busy timeout.

- Migraciones EF Core desde el primer commit.

- FTS5 para búsqueda textual local.

- Índices en EAN, external IDs, normalized name, retailer/store/seller y checked_at.

- No guardar payload completo duplicado en RawProduct y RawCapture.

## 7.1 Ruta de datos Windows

%LOCALAPPDATA%\MarketOptimizer\\  
data\market.db  
raw\\retailer}\YYYY-MM-DD\\sha256}.json.gz  
backups\\  
logs\\  
config\appsettings.user.json  
secrets\secrets.dat

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Desarrollo</strong></p>
<p>En desarrollo puede usarse `./.local-data` mediante configuración, pero esa carpeta debe estar en `.gitignore`.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 7.2 Política RAW

| **Dato**                 | **Retención default** |
|--------------------------|-----------------------|
| Raw payload gzip         | 60 días               |
| Metadata de captura/hash | Indefinida            |
| PriceObservation         | Indefinida            |
| Sync logs detallados     | 30 días               |
| Application logs         | 14 días               |
| Backups diarios          | 7                     |
| Backups semanales        | 4                     |
| Backups mensuales        | 3                     |

# 8. Store Providers

Cada provider implementa un contrato común pero puede declarar capacidades distintas. La UI consulta capacidades antes de ofrecer acciones.

IStoreProvider  
- ProviderId  
- GetCapabilitiesAsync()  
- SearchAsync(query, context)  
- GetProductAsync(externalProductId, context)  
- GetOffersAsync(externalSkuIds, context)  
- GetStoresAsync(area)  
- HealthCheckAsync()

| **Capacidad**     | **D1**            | **Éxito**                      | **Ara V1**                   |
|-------------------|-------------------|--------------------------------|------------------------------|
| Search/Catalog    | Sí                | Sí                             | Parcial/no catálogo completo |
| EAN/SKU           | Sí                | Sí                             | No garantizado               |
| Offer/Price       | Sí regional       | Sí contextual                  | Promociones parciales        |
| Seller model      | Simple/contextual | Crítico: direct vs marketplace | N/A                          |
| Store exact price | No demostrado     | No demostrado                  | No                           |
| HTTP direct       | Sí                | Sí                             | Solo fuentes permitidas      |
| Contract tests    | Obligatorio       | Obligatorio                    | Promociones/fuentes públicas |

## 8.1 Normalización de provider

El provider devuelve modelos intermedios \`ExternalProductSnapshot\`, \`ExternalSkuSnapshot\` y \`OfferSnapshot\`. Application decide persistencia/canonicalización. Ningún DTO VTEX/HTML debe escapar del proyecto del provider.

# 9. Sincronización y caché

La estrategia es híbrida: datos locales primero, refresco por TTL y sync incremental. El objetivo es minimizar solicitudes y mantener una UX rápida.

Query  
↓  
SQLite/FTS  
↓  
¿dato suficiente y fresco? ── sí ──► responder  
│ no  
▼  
Provider HTTP  
↓  
Raw capture + normalize + transaction  
↓  
responder + actualizar índice

| **Tipo**             | **TTL default** | **Estrategia**                                  |
|----------------------|-----------------|-------------------------------------------------|
| Precio               | 6 h             | Refresco on-demand/basket optimization          |
| Stock                | 2 h             | Solo si el provider lo expone y afecta decisión |
| Producto             | 7 días          | Incremental/background                          |
| Search cache externa | 24 h            | Persistir resultados útiles                     |
| Store list           | 7 días          | Manual/on-demand                                |
| Routes               | 24 h            | Cache key origen+paradas+modo                   |

Un \`BackgroundService\` puede ejecutar housekeeping y sync periódico mientras la aplicación está abierta. No se requiere Windows Service en V1.

# 10. Búsqueda y matching

La búsqueda y matching son subsistemas separados. FTS recupera candidatos; Matching determina relación semántica/estructural.

1.  Normalizar consulta: Unicode, acentos, unidades, aliases y tokens.

2.  FTS5 BM25 recupera candidatos locales.

3.  Aplicar filtros de categoría/marca/atributos/tamaño.

4.  Resolver exactos por override/EAN/reglas fuertes.

5.  Calcular score fuzzy solo para candidatos restantes.

6.  Si queda ambiguo y IA está habilitada, enviar máximo 8 candidatos a OpenRouter.

7.  Persistir decisión calculada con versión de algoritmo y permitir override.

## 10.1 Versionado de matching

ProductMatch  
- MatcherVersion  
- MatchType  
- ConfidenceBand (HIGH/MEDIUM/LOW)  
- EvidenceJson  
- CreatedAt  
  
Al cambiar reglas, los matches no manuales pueden recalcularse desde datos RAW/canónicos.

# 11. Optimizador

El optimizador es determinístico y no depende de IA. Debe separar asignación de producto, selección de tiendas y orden de ruta.

1\) Candidate generation por BasketItem  
2) Feasible offers por tienda/contexto  
3) Selección de subconjuntos con \|stores\| ≤ N  
4) Asignación de cada ítem al offer válido de menor costo sujeto a restricciones  
5) Top-K planes por costo/preferencias  
6) Routing solo para Top-K  
7) Score de estrategia / Pareto  
8) Explicación determinística + IA opcional

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Escalabilidad</strong></p>
<p>Con 5–10 cadenas y N pequeño, enumerar subconjuntos de tiendas es perfectamente manejable. No introducir un solver externo hasta que mediciones reales indiquen necesidad.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

# 12. Google Maps Platform

V1 requiere crear un proyecto Google Cloud y dos APIs: Geocoding API y Routes API. No se habilitará Places ni Maps JavaScript API salvo necesidad posterior.

## 12.1 Configuración inicial

8.  Crear un proyecto dedicado en Google Cloud (ej. MarketOptimizer-Local).

9.  Vincular cuenta de facturación. Google Maps Platform requiere billing aun cuando el uso quede dentro de llamadas gratuitas.

10. Habilitar Geocoding API.

11. Habilitar Routes API.

12. Crear una API key exclusiva para backend local.

13. Aplicar API restrictions para permitir solo Geocoding API y Routes API.

14. Configurar cuotas diarias conservadoras y alertas de presupuesto.

15. Guardar la key cifrada mediante SecretStore local; nunca en \`appsettings.json\` ni Angular.

16. Ejecutar health test y registrar consumo.

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Costo esperado</strong></p>
<p>Google Maps Platform ofrece actualmente hasta 10.000 llamadas gratuitas mensuales por SKU Essentials; el uso personal previsto debería quedar muy por debajo. No obstante, la app debe cachear geocoding/rutas y configurar cuotas.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 12.2 No usar mapa embebido en V1

La UI muestra distancias, tiempos y secuencia de paradas. El botón “Abrir ruta” construye un Google Maps URL (\`https://www.google.com/maps/dir/?api=1...\`) que abre navegador/Google Maps sin requerir un mapa JavaScript dentro de la app.

## 12.3 IGeocodingProvider / IRoutingProvider

IGeocodingProvider.GeocodeAsync(text) -\> GeocodeResult  
IRoutingProvider.ComputeRouteAsync(origin, stops, mode) -\> RouteResult  
  
Domain/Application no conocen clases de Google.

# 13. OpenRouter

OpenRouter se implementará vía HTTP desde el backend. La API es OpenAI-compatible y la URL base actual es \`https://openrouter.ai/api/v1\`.

## 13.1 Configuración

AiSettings  
- Enabled  
- BaseUrl = https://openrouter.ai/api/v1  
- ApiKeySecretId  
- SelectedModelId  
- FallbackModelId?  
- MonthlySoftBudgetUsd  
- HardStopEnabled  
- Features: Normalization/Matching/Substitutes/Explanation

## 13.2 Model discovery

No hardcodear un modelo. Consumir \`GET /api/v1/models\`, cachear catálogo y mostrar costo/capacidades. El usuario elige modelo. Para funciones estructurales, filtrar preferentemente modelos que soporten structured outputs.

## 13.3 Structured outputs

Para normalización/matching se enviará \`response_format: { type: "json_schema", ... }\` con \`strict: true\` cuando el modelo lo soporte. El esquema debe prohibir propiedades adicionales y la respuesta se valida nuevamente en backend.

AI request pipeline  
Candidates (≤ 8)  
↓  
Redact/minimize context  
↓  
JSON Schema request  
↓  
Validate response  
↓  
Apply only allowed fields  
↓  
Persist evidence + cost metadata  
↓  
Never overwrite manual override

## 13.4 Cost tracking

- Guardar model ID, tokens input/output y costo reportado/estimado.

- Mostrar acumulado mensual por feature.

- Soft budget configurable; hard stop opcional.

- No enviar catálogos completos.

- Cachear análisis por hash de entrada + modelo + prompt version.

# 14. Seguridad y secretos

El mayor beneficio de la topología local es que no se necesita autenticación de usuario, siempre que el proceso no sea accesible por red.

| **Riesgo**                   | **Control**                                                  |
|------------------------------|--------------------------------------------------------------|
| Acceso desde LAN             | Kestrel bind exclusivo 127.0.0.1/::1                         |
| API keys en frontend         | Prohibido; Angular nunca recibe keys                         |
| Secrets en disco             | DPAPI CurrentUser / secret store cifrado                     |
| Secrets en logs              | Redaction middleware/structured logging                      |
| SQL injection                | EF Core parametrizado; raw SQL excepcional y parametrizado   |
| SSRF por Google Maps URL     | Allowlist de hosts + límites de redirección                  |
| Provider HTML/JSON malicioso | Validación estricta y límites de tamaño                      |
| RAW sensible                 | Retención limitada; no guardar cookies/tokens reutilizables  |
| Supply chain                 | Lockfiles, dependabot/renovate opcional, auditoría npm/NuGet |

## 14.1 SecretStore Windows

En desarrollo se permiten User Secrets/variables de entorno. En ejecución local, las claves configuradas desde UI se cifran con Windows DPAPI bajo el usuario actual. La base de datos guarda únicamente un identificador de secreto, nunca la key.

# 15. API local

La API se versiona y usa DTOs explícitos. Para un solo cliente local, REST es suficiente; no introducir GraphQL.

| **Endpoint**                       | **Función**                         |
|------------------------------------|-------------------------------------|
| GET /api/v1/products/search        | Búsqueda local + estado de frescura |
| GET /api/v1/products/{id}          | Detalle canónico/histórico          |
| POST /api/v1/sync/search           | Refresco externo de consulta        |
| POST /api/v1/baskets               | Crear/guardar mercado               |
| POST /api/v1/baskets/{id}/optimize | Optimizar                           |
| POST /api/v1/location/parse        | Dirección/Maps URL → location       |
| POST /api/v1/routes/compute        | Ruta para plan                      |
| GET /api/v1/providers              | Capacidades/health                  |
| POST /api/v1/providers/{id}/sync   | Sync manual                         |
| GET/PUT /api/v1/settings           | Configuración no secreta            |
| POST /api/v1/secrets/{type}        | Guardar/reemplazar secreto          |
| GET /api/v1/ai/models              | Modelos OpenRouter cacheados        |
| POST /api/v1/matches/{id}/override | Confirmar/rechazar                  |
| POST /api/v1/backups               | Crear backup                        |
| POST /api/v1/backups/{id}/restore  | Restaurar con validación            |

# 16. Resiliencia y observabilidad

Las integraciones externas son el componente más cambiante; por eso deben ser observables y fallar de forma controlada.

- Timeouts separados para connect/overall.

- Retries solo para fallos transitorios idempotentes, con jitter/backoff.

- Respetar 429/Retry-After.

- Circuit breaker por provider.

- Bulkhead/limitación de concurrencia para no saturar fuentes.

- Provider health derivado de contract + errores recientes.

- Logs con CorrelationId por sync/optimización.

- SyncRun registra procesados, creados, actualizados, errores y duración.

| **Estado provider** | **Criterio ejemplo**                                               |
|---------------------|--------------------------------------------------------------------|
| HEALTHY             | Contract canary válido y tasa de error baja                        |
| DEGRADED            | Búsqueda disponible pero precio/stock parcial o uso de caché stale |
| DOWN                | Canary/contrato crítico falla repetidamente                        |

# 17. Backups y migraciones

La base local es valiosa: históricos y overrides mejoran con el tiempo. Backups son parte del producto, no una tarea manual del desarrollador.

17. Antes de aplicar una migración: crear backup consistente.

18. Aplicar migración EF Core.

19. Ejecutar \`PRAGMA integrity_check\` y \`foreign_key_check\`.

20. Si falla, restaurar backup y marcar migración como fallida.

Para backup SQLite activo usar una operación segura (SQLite backup API o checkpoint coordinado), no copiar ciegamente el archivo WAL en mitad de escrituras.

# 18. Testing

El proyecto debe tener pruebas desde la fundación porque los providers pueden romperse aunque el código propio no cambie.

| **Nivel**         | **Cobertura**                                                    |
|-------------------|------------------------------------------------------------------|
| Unit              | Normalización, unidades, score, matching rules, optimizer        |
| Application       | Use cases con repositorios/providers fake                        |
| Integration       | EF Core SQLite real, filesystem RAW, SecretStore abstraído       |
| Contract D1/Éxito | Canary de búsqueda/esquema/precio sin afirmar valores fijos      |
| API               | WebApplicationFactory, validación y errores                      |
| Frontend          | Componentes, servicios, accessibility basics                     |
| E2E               | Buscar → cesta → optimizar; settings IA/Google fake              |
| Manual release    | Prueba física de precios seleccionados y smoke real de providers |

## 18.1 Contract test estable

NO: Assert.Price == 9850  
SÍ: Search("arroz") devuelve lista no vacía; cada item válido tiene external id, nombre; al menos una oferta válida; esquema crítico presente; tipos correctos.

# 19. Performance

Objetivos de rendimiento razonables para PC local.

| **Operación**                        | **Objetivo p95 V1**                       |
|--------------------------------------|-------------------------------------------|
| Búsqueda local FTS                   | \< 150 ms                                 |
| Abrir detalle local                  | \< 200 ms                                 |
| Optimizar 50–100 ítems / ≤10 cadenas | \< 2 s sin rutas                          |
| Render de resultado                  | \< 100 ms después de respuesta            |
| Arranque backend                     | \< 3 s en máquina de desarrollo razonable |
| Sync externo                         | No bloquear UI; progreso visible          |

Las llamadas externas no tienen SLO estricto; la UX debe mostrar caché inmediatamente y refrescar de forma no destructiva cuando sea posible.

# 20. Configuración

La configuración se divide por fuente y sensibilidad.

| **Ubicación**            | **Contenido**                                        |
|--------------------------|------------------------------------------------------|
| appsettings.json         | Defaults no sensibles versionados                    |
| appsettings.user.json    | Preferencias locales no sensibles                    |
| SecretStore/DPAPI        | OpenRouter API key, Google API key                   |
| SQLite                   | Perfiles, TTL, ajustes de dominio, state operacional |
| Environment/User Secrets | Solo desarrollo/CI                                   |

Ejemplo conceptual:  
{  
"Database": { "Path": "%LOCALAPPDATA%/MarketOptimizer/data/market.db" },  
"Providers": { "D1": { "Enabled": true }, "Exito": { "Enabled": true }, "Ara": { "Enabled": true } },  
"Freshness": { "PriceHours": 6, "StockHours": 2, "ProductDays": 7 },  
"Optimization": { "MaxStoresDefault": 2, "Profile": "balanced" }  
}

# 21. Flujo de ejecución local

El objetivo es que usar la app no requiera abrir Visual Studio ni ejecutar comandos manuales una vez empaquetada.

21. El usuario ejecuta un acceso directo o \`start-local.ps1\`.

22. El launcher comprueba que no exista otra instancia y elige/valida el puerto.

23. Inicia el ejecutable ASP.NET Core publicado.

24. Backend ejecuta migraciones seguras/health inicial.

25. Abre \`http://127.0.0.1:5280\` en el navegador predeterminado.

26. Al cerrar la app desde UI, se finalizan jobs, flush logs y proceso.

Fase posterior: empaquetar como instalador/MSIX o wrapper de escritorio solo si aporta valor. No introducir Electron ahora.

# 22. Plan de implementación

Cada fase debe dejar una aplicación ejecutable y tests verdes.

| **Sprint/Fase** | **Entrega técnica**                                                                             |
|-----------------|-------------------------------------------------------------------------------------------------|
| F0              | Repo, solution, Angular shell, design tokens, SQLite v2, migrations, data paths, logging, tests |
| F1              | D1Provider + RAW + sync + local search + contract test                                          |
| F2              | ExitoProvider + seller classification + contract test                                           |
| F3              | Canonicalization + units + FTS + matching + overrides                                           |
| F4              | Basket + deterministic optimizer + strategies                                                   |
| F5              | Google setup wizard + geocoding + routes + Maps URL                                             |
| F6              | Pantallas completas según UI/UX spec                                                            |
| F7              | OpenRouter model discovery + structured matching/normalization + cost meter                     |
| F8              | Backups/restore, provider health, diagnostics, performance/accessibility hardening              |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Definition of Done por fase</strong></p>
<p>Build limpio, migraciones reproducibles, tests unit/integration/contract relevantes verdes, sin secretos en repo, documentación actualizada y smoke local aprobado.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

# Referencias técnicas verificadas

Referencias consultadas para decisiones que dependen del estado actual de herramientas externas. Validación: 5 de octubre de 2026.

- **Google Maps Platform Documentation —** <u>https://developers.google.com/maps/documentation</u> · Geocoding y Routes como productos actuales.

- **Google Maps Platform Pricing —** <u>https://mapsplatform.google.com/pricing/</u> · Essentials: hasta 10K llamadas gratuitas por SKU/mes; billing requerido.

- **Google Maps URLs —** <u>https://developers.google.com/maps/documentation/urls/get-started</u> · URLs de directions para abrir rutas sin mapa embebido.

- **OpenRouter Developer Platform —** <u>https://openrouter.ai/developers</u> · Base URL OpenAI-compatible: https://openrouter.ai/api/v1.

- **OpenRouter Quickstart —** <u>https://openrouter.ai/docs/quickstart</u> · Chat completions y listado programático de modelos.

- **OpenRouter Models API —** <u>https://openrouter.ai/docs/api/api-reference/models/get-models</u> · GET /api/v1/models.

- **OpenRouter Structured Outputs —** <u>https://openrouter.ai/docs/guides/features/structured-outputs</u> · response_format json_schema con strict mode para modelos compatibles.

- **Angular release policy —** <u>https://github.com/angular/angular/blob/main/adev/src/content/reference/releases.md</u> · Angular 22 activo a la fecha.

- **.NET downloads / support —** <u>https://dotnet.microsoft.com/download/dotnet/10.0</u> · .NET 10 LTS activo; .NET 11 aún RC a la fecha.
