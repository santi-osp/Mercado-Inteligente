# Adaptador de Proveedor: La Vaquita / Supermu (`LAVAQUITA`)

## 1. Clasificación y Alcance Técnico

- **Código**: `LAVAQUITA`
- **Nombre**: Supermercado La Vaquita / Supermu
- **Tipo de Retailer**: Supermercado Regional / Carnicería y Fruver (Antioquia)
- **Diagnóstico Arquitectónico**: **Clasificación B (Ecommerce Regional con Catálogo Web y Domicilios)**.
- **Sedes Principales**: Medellín y Área Metropolitana (Laureles, El Poblado, La 33, La 80, Sabaneta, Envigado).
- **Plataforma Web**: Catálogo web oficial (`cavaquita.com` / `supermu.com`).

---

## 2. Capacidades Declaradas (`ProviderCapabilities`)

| Dimensión | Estado | Justificación |
| :--- | :--- | :--- |
| **Search** | `Supported` | Búsqueda por término en catálogo online y base local. |
| **Details** | `Partial` | Información de marca, descripción y presentación. |
| **Offers** | `Supported` | Precios vigentes de venta online para el Valle de Aburrá. |
| **Stores** | `Supported` | Red de sucursales físicas con coordenadas de geolocalización. |
| **Availability** | `Partial` | Stock estimado para despacho local en Medellín. |
| **CatalogEnumeration** | `NotSupported` | No se expone API de volcado total; lanza `NotSupportedException`. |

---

## 3. Semántica de Precios y Cobertura

- **PriceScope**: `FulfillmentRegion` (Medellín / Valle de Aburrá) con fallback honesto a `OnlineGeneric`.
- **SellerKind**: `RetailerDirect`. La Vaquita opera como comercio directo (no marketplace abierto).
- **Frescura de Precios**: TTL recomendado de 12 horas.