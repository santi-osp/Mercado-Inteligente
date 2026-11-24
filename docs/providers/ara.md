# Adaptador de Proveedor: Tiendas Ara (`ARA`)

## 1. Clasificación y Alcance Técnico

- **Código**: `ARA`
- **Nombre**: Tiendas Ara (Jerónimo Martins Colombia)
- **Tipo de Retailer**: Hard Discount (Descuento Duro)
- **Diagnóstico Arquitectónico**: **Clasificación D (Sin API transaccional pública ni catálogo unificado)**.
- **Principio Rector**: **Honestidad de Cobertura Parcial**. El sistema NO afirma poseer catálogo completo, inventario en tiempo real ni precios individuales por sucursal para Ara, a menos que el dato esté explícitamente sustentado en una fuente verificable.

---

## 2. Capacidades Declaradas (`ProviderCapabilities`)

| Dimensión | Estado | Justificación |
| :--- | :--- | :--- |
| **Search** | `Partial` | Búsqueda restringida sobre folletos de rebajas y ofertas registradas en la base de datos local. |
| **Details** | `Partial` | Detalle limitado a nombre, marca, gramaje y condiciones promocionales. |
| **Offers** | `Partial` | Ofertas de precios vinculadas a campañas publicitarias públicas o entradas manuales de usuario. |
| **Stores** | `Partial` | Directorio de sucursales conocidas (ej. Medellín y Valle de Aburrá) sin inventario en vivo. |
| **Availability** | `NotSupported` | No existe API que reporte disponibilidad o stock por punto de venta. |
| **CatalogEnumeration** | `NotSupported` | Prohibida la suposición de catálogo completo; lanza `NotSupportedException`. |

---

## 3. Modos de Ingesta

### 3.1 Folletos y Promociones Públicas (`AraBrochureIngest`)
- Ingesta de folletos semanales o quincenales ("Rebajas de la Semana", "La Gran Canasta Ara").
- Preservación íntegra del archivo fuente en `RawStore` (formato `.json.gz` con digest SHA-256).
- Mapeo de fechas de vigencia: `ValidFrom` y `ValidTo`. Las ofertas caducan automáticamente al expirar el plazo.
- `PriceScope`: `OnlineGeneric` (promociones de alcance nacional/regional según folleto).
- `SellerKind`: `RetailerDirect`.

### 3.2 Aportes Comunitarios y Entrada Manual de Usuario (`AraManualOfferService`)
- Vía interactiva desde la interfaz gráfica o API (`POST /api/v1/stores/ara/manual`) para registrar precios observados físicamente por el usuario en tiendas de barrio o sucursales de Ara.
- Metadatos obligatorios: `productName`, `quantity`, `unit`, `price`.
- Metadatos opcionales: `brand`, `listPrice`, `storeName`, `validUntil`, `notes`.
- Distinción visual y semántica estricta: etiquetado como `source: USER_MANUAL` con `PriceScope.PhysicalVerified` si se suministra sucursal, o `PriceScope.Unknown`.
- Auditoría en `RawStore` de cada inserción manual.

---

## 4. Endpoints Expuestos

- `GET /api/v1/stores/ara/health`: Reporte de salud honesto y recuento de ofertas (folletos vs manuales).
- `POST /api/v1/stores/ara/brochure`: Ingesta de un nuevo folleto público de promociones.
- `GET /api/v1/stores/ara/manual`: Lista de ofertas registradas manualmente por el usuario.
- `POST /api/v1/stores/ara/manual`: Registro de una nueva oferta manual.
- `DELETE /api/v1/stores/ara/manual/{id}`: Eliminación de una oferta manual.
