using MercadoInteligente.Application;
using MercadoInteligente.Domain;

namespace MercadoInteligente.Infrastructure;

public sealed class StoreProviderRegistry(IEnumerable<IStoreProvider> providers) : IStoreProviderRegistry
{
    private readonly Dictionary<string, IStoreProvider> _providers =
        providers.ToDictionary(p => p.Code.ToUpperInvariant(), p => p, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IStoreProvider> GetAllProviders() => _providers.Values.ToArray();

    public IStoreProvider? GetProvider(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        _providers.TryGetValue(code.Trim().ToUpperInvariant(), out var provider);
        return provider;
    }

    public IReadOnlyList<ProviderMetadata> GetMetadata()
    {
        var list = new List<ProviderMetadata>();

        foreach (var p in _providers.Values)
        {
            var code = p.Code.ToUpperInvariant();
            switch (code)
            {
                case "D1":
                    list.Add(new ProviderMetadata(
                        Code: "D1",
                        Name: "Tiendas D1",
                        RetailerType: "Hard Discount (Descuento Duro)",
                        CatalogCoverage: "Completa (Online y Regional)",
                        Capabilities: p.Capabilities,
                        Status: "PARTIAL_GENERIC_ONLINE_MANUAL_REFRESH",
                        Enabled: true,
                        PriceTtlHours: 6,
                        SupportsManualOffers: false,
                        Description: "Cadena de descuento duro con catálogo online estructurado y cotización por fulfillment regional.",
                        WebsiteUrl: "https://d1.com.co"
                    ));
                    break;

                case "EXITO":
                    list.Add(new ProviderMetadata(
                        Code: "EXITO",
                        Name: "Éxito",
                        RetailerType: "Hipermercado / Omnicanal",
                        CatalogCoverage: "Completa (Directo + Marketplace)",
                        Capabilities: p.Capabilities,
                        Status: "PARTIAL_FASTSTORE_MANUAL_REFRESH",
                        Enabled: true,
                        PriceTtlHours: 6,
                        SupportsManualOffers: false,
                        Description: "Hipermercado omnicanal consultado mediante API FastStore SearchQuery con filtro seguro de sellers y cobertura regional.",
                        WebsiteUrl: "https://www.exito.com"
                    ));
                    break;

                case "ARA":
                    list.Add(new ProviderMetadata(
                        Code: "ARA",
                        Name: "Tiendas Ara",
                        RetailerType: "Hard Discount (Descuento Duro)",
                        CatalogCoverage: "Parcial (Folletos públicos y entradas manuales)",
                        Capabilities: p.Capabilities,
                        Status: "PARTIAL_HONEST_BROCHURE_AND_MANUAL",
                        Enabled: true,
                        PriceTtlHours: 24,
                        SupportsManualOffers: true,
                        Description: "Cadena de descuento operando con cobertura parcial honesta: ingesta de folletos públicos promocionales y registro de ofertas comunitarias por el usuario.",
                        WebsiteUrl: "https://aratiendas.com"
                    ));
                    break;

                case "LAVAQUITA":
                    list.Add(new ProviderMetadata(
                        Code: "LAVAQUITA",
                        Name: "Supermercado La Vaquita",
                        RetailerType: "Supermercado Regional (Antioquia)",
                        CatalogCoverage: "Completa (Fruver, Carnes y Abarrotes)",
                        Capabilities: p.Capabilities,
                        Status: "ACTIVE_REGIONAL",
                        Enabled: true,
                        PriceTtlHours: 12,
                        SupportsManualOffers: false,
                        Description: "Supermercado emblemático del Valle de Aburrá especializado en carnes frescas, fruver seleccionado y abarrotes con múltiples sucursales físicas en Medellín.",
                        WebsiteUrl: "https://cavaquita.com"
                    ));
                    break;

                case "MUNDOHUEVO":
                    list.Add(new ProviderMetadata(
                        Code: "MUNDOHUEVO",
                        Name: "Mundo Huevo",
                        RetailerType: "Especialista Avícola / Granja",
                        CatalogCoverage: "Especializada (Huevos A, AA, AAA, Jumbo, Campesino)",
                        Capabilities: p.Capabilities,
                        Status: "ACTIVE_SPECIALIST",
                        Enabled: true,
                        PriceTtlHours: 24,
                        SupportsManualOffers: false,
                        Description: "Distribuidor especialista en huevos frescos clasificados por calibre (A, AA, AAA, Jumbo) y pastoreo con el mejor precio unitario garantizado en Medellín.",
                        WebsiteUrl: "https://mundohuevo.com"
                    ));
                    break;

                case "OLIMPICA":
                    list.Add(new ProviderMetadata(
                        Code: "OLIMPICA",
                        Name: "Supertiendas Olímpica",
                        RetailerType: "Supertienda Nacional",
                        CatalogCoverage: "Completa (Marca Propia Medalla de Oro)",
                        Capabilities: p.Capabilities,
                        Status: "ACTIVE_NATIONAL",
                        Enabled: true,
                        PriceTtlHours: 12,
                        SupportsManualOffers: false,
                        Description: "Cadena de supertiendas y droguerías a nivel nacional con fuerte presencia en despensa, marca propia Medalla de Oro y promociones diarias.",
                        WebsiteUrl: "https://www.olimpica.com"
                    ));
                    break;

                case "JUMBO":
                    list.Add(new ProviderMetadata(
                        Code: "JUMBO",
                        Name: "Hipermercados Jumbo (Cencosud)",
                        RetailerType: "Hipermercado / Premium",
                        CatalogCoverage: "Completa (Importados y Cuisine & Co)",
                        Capabilities: p.Capabilities,
                        Status: "ACTIVE_PREMIUM",
                        Enabled: true,
                        PriceTtlHours: 12,
                        SupportsManualOffers: false,
                        Description: "Hipermercados de gran superficie de Cencosud con amplio surtido de marcas gourmet, productos importados exclusivos y marca Cuisine & Co.",
                        WebsiteUrl: "https://tienda.tiendasjumbo.co"
                    ));
                    break;

                case "PRICESMART":
                    list.Add(new ProviderMetadata(
                        Code: "PRICESMART",
                        Name: "PriceSmart Club de Membresía",
                        RetailerType: "Club de Compras / Bulk (Price Mark)",
                        CatalogCoverage: "Completa (Formatos familiares e institucionales)",
                        Capabilities: p.Capabilities,
                        Status: "ACTIVE_MEMBERSHIP_CLUB",
                        Enabled: true,
                        PriceTtlHours: 24,
                        SupportsManualOffers: false,
                        Description: "Club de compras por membresía que ofrece formatos de gran volumen (bulk), empaques institucionales y marca Member's Selection con alto rendimiento por unidad.",
                        WebsiteUrl: "https://www.pricesmart.com"
                    ));
                    break;

                default:
                    list.Add(new ProviderMetadata(
                        Code: p.Code,
                        Name: p.Code,
                        RetailerType: "Supermercado / Retailer",
                        CatalogCoverage: p.Capabilities.Search == CapabilityState.Supported ? "Completa" : "Parcial",
                        Capabilities: p.Capabilities,
                        Status: "ACTIVE_EXTENSIBLE",
                        Enabled: true,
                        PriceTtlHours: 6,
                        SupportsManualOffers: false,
                        Description: $"Proveedor registrado dinámicamente mediante arquitectura extensible ({p.Code}).",
                        WebsiteUrl: null,
                        IsDemo: p.IsDemo
                    ));
                    break;
            }
        }

        return list;
    }
}