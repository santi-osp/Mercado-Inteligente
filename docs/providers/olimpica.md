# Adaptador de Proveedor: Supertiendas Olímpica (`OLIMPICA`)

## 1. Clasificación y Alcance Técnico

- **Código**: `OLIMPICA`
- **Nombre**: Supertiendas y Droguerías Olímpica
- **Tipo de Retailer**: Supertienda / Cadena Nacional de Gran Superficie
- **Diagnóstico Arquitectónico**: **Clasificación A (Plataforma VTEX con API Estructurada)**.
- **Cobertura**: Nacional (Barranquilla, Bogotá, Medellín, Cali, Costa Caribe y Eje Cafetero).
- **Plataforma Web**: `olimpica.com` (Arquitectura VTEX IO).

---

## 2. Capacidades Declaradas (`ProviderCapabilities`)

| Dimensión | Estado | Justificación |
| :--- | :--- | :--- |
| **Search** | `Supported` | API de búsqueda VTEX Catalog / Intelligent Search. |
| **Details** | `Supported` | EAN, imágenes en alta resolución, descripción y especificaciones. |
| **Offers** | `Supported` | Precios de lista, promociones bancarias y descuentos por tarjeta Olímpica. |
| **Stores** | `Supported` | Red nacional de supermercados y droguerías físicas. |
| **Availability** | `Partial` | Inventario según centro de distribución regional. |
| **CatalogEnumeration** | `NotSupported` | Paginación acotada para evitar rate-limiting agresivo. |

---

## 3. Semántica de Precios y Marketplace

- **PriceScope**: `FulfillmentRegion` o `OnlineGeneric`.
- **SellerKind**: Diferenciación estricta entre `RetailerDirect` (Olímpica directo) y `MarketplaceThirdParty` (vendedores aliados).