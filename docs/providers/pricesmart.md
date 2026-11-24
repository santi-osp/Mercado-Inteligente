# Adaptador de Proveedor: PriceSmart (`PRICESMART`)

## 1. Clarificación de Identidad y Clasificación

- **Identificación Oficial**: Identificado inequívocamente a partir de la referencia "Price Mark / PriceSmart". En Colombia no existe un retailer formal denominado "Price Mark"; corresponde a **PriceSmart Colombia** (Club de compras por membresía).
- **Código**: `PRICESMART`
- **Nombre**: PriceSmart
- **Tipo de Retailer**: Club de Compras por Membresía (Warehouse Club)
- **Diagnóstico Arquitectónico**: **Clasificación B (Plataforma de Ecommerce con Restricción de Membresía)**.
- **Sedes en Colombia**: Medellín (El Poblado), Bogotá (Salitre, Usaquén, Chía), Cali, Barranquilla, Pereira, Bucaramanga.
- **Plataforma Web**: `pricesmart.com/co/es`.

---

## 2. Capacidades Declaradas (`ProviderCapabilities`)

| Dimensión | Estado | Justificación |
| :--- | :--- | :--- |
| **Search** | `Supported` | Catálogo de productos en formatos institucionales y gran volumen. |
| **Details** | `Supported` | Información detallada de unidades, empaque múltiple y especificaciones. |
| **Offers** | `Supported` | Precios de venta para socios. |
| **Stores** | `Supported` | Clubes de compra físicos con geolocalización precisa. |
| **Availability** | `Partial` | Stock disponible en el club seleccionado. |
| **CatalogEnumeration** | `NotSupported` | Búsqueda acotada bajo demanda. |

---

## 3. Reglas Especiales: Membresía Obligatoria y Gran Formato

1. **Membresía Requerida (`RequiresMembership = true`)**:
   - PriceSmart exige membresía activa (Diamond o Platinum) para adquirir productos.
   - La interfaz de usuario muestra claramente el distintivo: `Requiere membresía de socio`.
2. **Normalización de Gran Formato (Packs y Bulk)**:
   - Los productos suelen venderse en empaques de gran volumen (ej. detergente de 8 kg, papel higiénico x 36 rollos, leche x 6 unidades).
   - Se preserva el conteo de unidades y contenido total para calcular el precio unitario real ($/kg, $/L, $/ud).