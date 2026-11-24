using System.Net;
using System.Text.Json;
using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure.Stores.Template;

/// <summary>
/// PLANTILLA DE EXTENSIBILIDAD: NewStoreProviderTemplate
///
/// Esta clase sirve como guía y patrón de referencia para integrar cualquier nueva cadena o supermercado
/// (ej: Jumbo, Olímpica, Makro, Carulla, etc.) a Mercado Inteligente SIN MODIFICAR EL DOMINIO CENTRAL
/// NI EL ALGORITMO OPTIMIZADOR.
///
/// Pasos para implementar un nuevo proveedor:
/// 1. Crear carpeta dedicada: src/backend/MercadoInteligente.Infrastructure/Stores/{NombreTienda}/
/// 2. Declarar honestamente las capacidades en ProviderCapabilities (Supported / Partial / NotSupported).
/// 3. Implementar IStoreProvider (SearchAsync, GetProductAsync, OffersAsync, GetStoresAsync, EnumerateCatalogAsync).
/// 4. Respetar semántica estricta de PriceScope y SellerKind (nunca catalogar un marketplace como RetailerDirect).
/// 5. Aplicar límite de tamaño en lectura de payloads HTTP (máx 2MB con streaming).
/// 6. Preservar RAW payload con RawStore.SaveUnderGateAsync para auditoría inmutable.
/// 7. Registrar en DI: builder.Services.AddHttpClient<MiTiendaProvider>(); builder.Services.AddSingleton<IStoreProvider, MiTiendaProvider>();
/// </summary>
public abstract class NewStoreProviderTemplate(HttpClient client) : IStoreProvider
{
    /// <summary>
    /// Cliente HTTP subyacente para realizar consultas a la tienda.
    /// </summary>
    protected HttpClient Client => client;
    /// <summary>
    /// Código único corto del retailer en mayúsculas (ej: "JUMBO", "OLIMPICA", "MAKRO").
    /// </summary>
    public abstract string Code { get; }

    /// <summary>
    /// Declaración explícita y honesta de capacidades.
    /// Si la tienda no soporta stock en tiempo real o precios por sucursal, indicar NotSupported o Partial.
    /// </summary>
    public abstract ProviderCapabilities Capabilities { get; }

    /// <summary>
    /// Límite estricto de lectura por respuesta HTTP para prevenir agotamiento de memoria (DoS / OOM).
    /// </summary>
    protected const int MaxPayloadSizeBytes = 2 * 1024 * 1024; // 2 MB

    /// <summary>
    /// Timeout predeterminado por solicitud HTTP hacia el endpoint de la tienda.
    /// </summary>
    protected static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Utilidad segura para leer respuestas JSON con límite estricto de memoria y streaming.
    /// </summary>
    protected static async Task<JsonDocument> ReadBoundedJsonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentLength > MaxPayloadSizeBytes)
            throw new JsonException($"El contenido del proveedor excede el límite seguro de {MaxPayloadSizeBytes} bytes.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var memory = new MemoryStream();
        var buffer = new byte[16384];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer, ct)) != 0)
        {
            if (memory.Length + bytesRead > MaxPayloadSizeBytes)
                throw new JsonException($"El flujo de datos del proveedor excedió el límite seguro de {MaxPayloadSizeBytes} bytes.");

            memory.Write(buffer, 0, bytesRead);
        }

        return JsonDocument.Parse(memory.ToArray());
    }

    /// <summary>
    /// Búsqueda acotada de productos por texto.
    /// </summary>
    public abstract Task<IReadOnlyList<ProviderProduct>> SearchAsync(
        string query,
        ProviderContext context,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Consulta de detalle de un producto por ID externo.
    /// </summary>
    public abstract Task<ProviderProduct?> GetProductAsync(
        string externalProductId,
        ProviderContext context,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Consulta de ofertas de precio y disponibilidad para un producto.
    /// IMPORTANTE: Indicar el PriceScope exacto (OnlineGeneric, FulfillmentRegion, Store o PhysicalVerified).
    /// </summary>
    public abstract Task<IReadOnlyList<ProviderOffer>> OffersAsync(
        string externalProductId,
        ProviderContext context,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Lista de tiendas físicas conocidas con coordenadas geográficas.
    /// </summary>
    public virtual Task<IReadOnlyList<ProviderStore>> GetStoresAsync(
        ProviderContext context,
        CancellationToken cancellationToken
    ) => Task.FromResult<IReadOnlyList<ProviderStore>>([]);

    /// <summary>
    /// Enumeración de catálogo completo. Si la tienda no expone una API oficial masiva de catálogo,
    /// DEBE lanzar NotSupportedException y declarar CatalogEnumeration = NotSupported.
    /// </summary>
    public virtual Task<IReadOnlyList<ProviderProduct>> EnumerateCatalogAsync(
        ProviderContext context,
        CancellationToken cancellationToken
    ) => throw new NotSupportedException($"La enumeración exhaustiva del catálogo no está soportada para el proveedor {Code}.");
}
