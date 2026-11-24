# Adaptador de Proveedor: Tiendas Jumbo (`JUMBO`)

## 1. Clasificación y Alcance Técnico

- **Código**: `JUMBO`
- **Nombre**: Tiendas Jumbo (Cencosud Colombia)
- **Tipo de Retailer**: Hipermercado / Cadena Multinacional
- **Diagnóstico Arquitectónico**: **Clasificación A (Plataforma VTEX con API de Catálogo)**.
- **Cobertura**: Medellín, Bogotá, Cali, Barranquilla, Bucaramanga.
- **Plataforma Web**: `tiendasjumbo.co` (Arquitectura VTEX).

---

## 2. Capacidades Declaradas (`ProviderCapabilities`)

| Dimensión | Estado | Justificación |
| :--- | :--- | :--- |
| **Search** | `Supported` | Consulta sobre endpoints VTEX Catalog y búsqueda de productos. |
| **Details** | `Supported` | EAN, tabla nutricional básica, marca y gramaje oficial. |
| **Offers** | `Supported` | Precios vigentes, promociones Cencosud y puntos Cencosud. |
| **Stores** | `Supported` | Hipermercados físicos con coordenadas GPS comprobadas. |
| **Availability** | `Partial` | Validación de stock para entrega a domicilio o retiro en tienda. |
| **CatalogEnumeration** | `NotSupported` | Consulta acotada por términos de búsqueda. |

---

## 3. Semántica de Precios y Marketplace

- **PriceScope**: `FulfillmentRegion` o `OnlineGeneric`.
- **SellerKind**: Filtro explícito para identificar si el producto es vendido y entregado por Jumbo o por un tercero en marketplace.