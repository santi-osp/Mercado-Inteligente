# Guía de extensibilidad de Store Providers

## Objetivo

Agregar una nueva cadena debe ser un trabajo localizado, no un rediseño del dominio.

## Contrato conceptual

Cada provider implementa `IStoreProvider` y expone `ProviderCapabilities`.

Capacidades mínimas modelables:

- SearchProducts
- EnumerateCatalog
- ProductDetails
- Prices
- Promotions
- Availability
- NumericStock
- Stores
- LocationContext
- StoreSpecificPrices
- Ean
- SellerInformation

Cada una puede ser:

- `SUPPORTED`
- `PARTIAL`
- `NOT_SUPPORTED`

## Flujo recomendado

Fuente externa → DTO específico del provider → mapper → modelo normalizado común.

El dominio no debe conocer nombres como `commercialOffer`, `FastStore`, `regionId`, etc.

## Estructura sugerida

```text
Infrastructure/
  Stores/
    D1/
      D1Provider.cs
      D1Client.cs
      D1Mapper.cs
      D1Models.cs
      D1Options.cs
      D1ContractTests...
    Exito/
    Ara/
```

## Requisitos para un provider nuevo

1. Documento de fuente y alcance.
2. Capabilities declaradas.
3. Cliente HTTP aislado.
4. Timeouts/retry/cooldown definidos.
5. Mapping a `ExternalProduct/ExternalSku/Offer/Seller`.
6. Contexto geográfico explícito.
7. `PriceScope` correcto.
8. Contract tests.
9. Health check.
10. Política de caché/TTL.
11. Evidencia de términos/riesgo operativo cuando sea relevante.
12. README técnico del provider.

## ProviderHealth

Global:

- HEALTHY
- DEGRADED
- DOWN
- UNKNOWN

Idealmente también por capability.

Ejemplo: Éxito puede tener Search/Prices HEALTHY y Stock DEGRADED sin eliminarlo de la comparación.

## Marketplaces

Providers con sellers externos deben clasificar:

- RETAILER_DIRECT
- INTERNAL_FULFILLMENT
- MARKETPLACE
- UNKNOWN

Default V1: marketplace excluido del comparador principal salvo configuración explícita.

## Abstracción compartida

No crear una gran clase base VTEX anticipadamente. Extraer código compartido solo cuando D1/Éxito/Carulla/etc. demuestren repetición real. Preferir composición sobre herencia rígida.

## UI

La UI nunca debe asumir capacidades homogéneas. Debe consultar/recibir capabilities y mostrar:

- precio disponible;
- promoción parcial;
- stock desconocido;
- fuente no soportada;

sin simular paridad entre tiendas.

## Checklist de alta de una nueva tienda

- [ ] fuente identificada y permitida para uso planteado
- [ ] IDs de producto/SKU entendidos
- [ ] EAN evaluado
- [ ] precios entendidos
- [ ] seller/contexto entendido
- [ ] ubicación entendida
- [ ] promociones entendidas
- [ ] DTOs aislados
- [ ] mapping normalizado
- [ ] contract tests
- [ ] health
- [ ] TTL
- [ ] datos RAW sanitizados/deduplicados
- [ ] UI capability-aware
- [ ] documentación
