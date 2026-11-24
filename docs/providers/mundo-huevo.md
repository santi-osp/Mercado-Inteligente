# Adaptador de Proveedor: Mundo Huevo (`MUNDOHUEVO`)

## 1. Clasificación y Alcance Técnico

- **Código**: `MUNDOHUEVO`
- **Nombre**: Tienda Mundo Huevo
- **Tipo de Retailer**: Especialista en Huevo, Proteína y Abarrotes (Medellín / Antioquia)
- **Diagnóstico Arquitectónico**: **Clasificación B (Comercio Especializado con Domicilios en Antioquia)**.
- **Puntos de Venta**: Medellín (Laureles, Belén, Poblado, Envigado, Itagüí).
- **Plataforma Web**: `tiendamundohuevo.com`.

---

## 2. Capacidades Declaradas (`ProviderCapabilities`)

| Dimensión | Estado | Justificación |
| :--- | :--- | :--- |
| **Search** | `Supported` | Búsqueda especializada por tipo de huevo, tamaño y abarrotes. |
| **Details** | `Partial` | Atributos específicos: clasificación (A, AA, AAA, Jumbo), presentación (x30, x15). |
| **Offers** | `Supported` | Precios de venta al detal y por cubeta/panal. |
| **Stores** | `Supported` | Sucursales de distribución física en Medellín. |
| **Availability** | `Partial` | Disponibilidad para entrega local en Valle de Aburrá. |
| **CatalogEnumeration** | `NotSupported` | No soporta volcado completo automático. |

---

## 3. Manejo de Calidad y Clasificación de Huevos

El sistema no compara huevos de diferente categoría como equivalentes directos:
- Se preserva en el SKU la clasificación oficial: `A`, `AA`, `AAA`, `JUMBO`.
- Se preserva el origen o tipo: `Campesino`, `Gallina Feliz / Pastoreo`, `Blanco`, `Rojo`.
- El precio unitario se calcula rigurosamente por unidad de huevo (`$/ud`), permitiendo comparar con precisión cubetas de 30 frente a empaques de 15 o 12.