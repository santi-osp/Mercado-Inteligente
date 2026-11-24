# Guía de Extensibilidad: Cómo Añadir un Nuevo Proveedor de Tienda

Esta guía detalla el procedimiento estándar para integrar un nuevo supermercado o cadena de distribución (ej: *Jumbo, Olímpica, Makro, Carulla, etc.*) a **Mercado Inteligente** de forma modular, segura y sin alterar el dominio central ni los motores de optimización matemática.

---

## 1. Principios de Integración y Filosofía Arquitectónica

1. **Desacoplamiento Absoluto**:
   - `MercadoInteligente.Domain` y `MercadoInteligente.Application` no contienen lógica condicional dependiente de una tienda específica (`if (store == "JUMBO")`).
   - El optimizador de canastas (`BasketOptimizer`) opera exclusivamente sobre la abstracción genérica `RawCatalogOffer`.
2. **Honestidad en Capacidades (`ProviderCapabilities`)**:
   - Nunca simular cobertura total si la fuente solo provee folletos o cobertura parcial.
   - Declarar cada una de las 6 dimensiones de capacidad: `Search`, `Details`, `Offers`, `Stores`, `Availability` y `CatalogEnumeration` con su estado real (`Supported`, `Partial`, `NotSupported`).
3. **Seguridad y Resiliencia HTTP**:
   - Lectura acotada con límite estricto de memoria (máximo 2 MB por respuesta HTTP).
   - Preservación obligatoria de evidencia RAW inmutable (`RawStore.SaveUnderGateAsync`) con compresión gzip y hash criptográfico SHA-256.
4. **Semántica Estricta de Precios y Vendedores**:
   - Respetar `PriceScope`: `OnlineGeneric`, `FulfillmentRegion`, `Store` o `PhysicalVerified`.
   - Respetar `SellerKind`: Nunca catalogar productos de terceros en marketplace como `RetailerDirect`.

---

## 2. Estructura de Módulos (Module Layout)

Cada nuevo proveedor debe alojarse en su propio subdirectorio aislado dentro de la capa de infraestructura:

```
src/backend/MercadoInteligente.Infrastructure/Stores/{RetailerCode}/
├── {RetailerCode}Provider.cs          // Implementación de IStoreProvider
├── {RetailerCode}Mapper.cs            // Conversión segura de DTOs JSON a modelos de dominio
├── {RetailerCode}CatalogIngest.cs     // Ingestión y persistencia con RawStore y deduplicación
├── {RetailerCode}HealthProbe.cs       // Diagnóstico de salud y conectividad de la tienda
└── {RetailerCode}Models.cs            // DTOs de transporte y esquemas JSON específicos
```

---

## 3. Implementación Paso a Paso

### Paso 1: Heredar de la plantilla o implementar `IStoreProvider`

Utilizar como referencia `NewStoreProviderTemplate.cs`:

```csharp
using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure.Stores.Jumbo;

public sealed class JumboProvider(HttpClient client) : IStoreProvider
{
    public string Code => "JUMBO";

    public ProviderCapabilities Capabilities { get; } = new(
        Search: CapabilityState.Supported,
        Details: CapabilityState.Supported,
        Offers: CapabilityState.Supported,
        Stores: CapabilityState.Partial,
        Availability: CapabilityState.Partial,
        CatalogEnumeration: CapabilityState.NotSupported
    );

    public async Task<IReadOnlyList<ProviderProduct>> SearchAsync(string query, ProviderContext context, CancellationToken cancellationToken)
    {
        // 1. Validar argumentos
        // 2. Ejecutar petición HTTP acotada (máx 2MB)
        // 3. Mapear a ProviderProduct
    }

    public async Task<ProviderProduct?> GetProductAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
    {
        // Consulta por ID
    }

    public async Task<IReadOnlyList<ProviderOffer>> OffersAsync(string externalProductId, ProviderContext context, CancellationToken cancellationToken)
    {
        // Consulta de ofertas
    }

    public Task<IReadOnlyList<ProviderStore>> GetStoresAsync(ProviderContext context, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProviderStore>>([]);

    public Task<IReadOnlyList<ProviderProduct>> EnumerateCatalogAsync(ProviderContext context, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Catalog enumeration is not supported for Jumbo.");
}
```

---

### Paso 2: Mapeo y DTOs (`{RetailerCode}Mapper.cs`)

- Parsear campos numéricos mediante `decimal.Parse(..., CultureInfo.InvariantCulture)`. Nunca usar `float` o `double` para dinero.
- Normalizar unidades de empaque (gramos, mililitros, unidades) usando `QuantityParser.Parse(...)`.
- Preservar el código de barras `EAN` siempre que esté disponible en la respuesta.

---

### Paso 3: Preservación RAW y Transacciones

Cualquier ingestión o sincronización debe almacenar el payload en `RawStore`:

```csharp
var gate = rawStore.Gate;
await gate.WaitAsync(ct);
try
{
    var (rawCapture, isDuplicate) = await rawStore.SaveUnderGateAsync(payloadBytes, ct);
    // Persistir productos, SKUs, ofertas y PriceObservations con FK hacia rawCapture.Id
}
finally
{
    gate.Release();
}
```

---

### Paso 4: Registro en el Contenedor de Inyección de Dependencias (DI)

En `src/backend/MercadoInteligente.Api/Program.cs`:

```csharp
builder.Services.AddHttpClient<JumboProvider>(client =>
{
    client.BaseAddress = new Uri("https://www.tiendasjumbo.co/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddSingleton<IStoreProvider, JumboProvider>();
```

Al registrar `IStoreProvider`, el `StoreProviderRegistry` detectará y expondrá automáticamente el nuevo proveedor en `GET /api/v1/providers`, en la interfaz gráfica de usuario y en el ciclo de sincronización sin requerir cambios adicionales.

---

## 4. Checklist de Pruebas de Contrato

Para considerar completa la integración de una nueva tienda, se debe agregar una clase de pruebas en `tests/MercadoInteligente.Tests/{RetailerCode}ProviderTests.cs` verificando:

1. **Capacidades Declaradas**: Verificar que `Capabilities` refleja fielmente el soporte técnico real.
2. **Resiliencia ante Cargas Maliciosas u OOM**: Probar que payloads superiores a 2 MB son rechazados con `JsonException`.
3. **Mapeo de Sellers**: Validar que productos comercializados por terceros lleven `SellerKind.Marketplace`.
4. **Preservación RAW**: Confirmar que los datos crudos se almacenan en `.json.gz` con digest SHA-256 válido.
5. **Aislamiento del Optimizador**: Confirmar que `BasketOptimizer` asigna productos de la nueva tienda basándose únicamente en precio y restricciones, sin condicionales especiales.
