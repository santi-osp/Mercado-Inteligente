# Ciclo de vida de datos y sincronización

## Objetivo

Definir cómo se crean, actualizan, conservan, invalidan, compactan y respaldan los datos durante semanas, meses y años de uso local.

## Principios

1. El último valor conocido válido nunca se destruye por un fallo de actualización.
2. El estado actual y el histórico son conceptos distintos.
3. Los datos RAW son evidencia temporal; los datos normalizados e históricos son conocimiento persistente.
4. Una sincronización parcial no invalida automáticamente datos previos.
5. Todo dato externo relevante debe llevar `checked_at`, fuente y contexto.
6. El usuario debe poder distinguir dato reciente, viejo, desconocido y fallido.

## Capas de persistencia

### Datos canónicos

Retención indefinida salvo borrado explícito:

- Retailer
- Store
- CanonicalProduct
- ProductVariant
- MatchOverride
- preferencias del usuario
- perfiles de comparación

### Datos externos normalizados

Persisten mientras sigan siendo útiles:

- ExternalProduct
- ExternalSku
- Seller
- Offer actual

No borrar inmediatamente cuando desaparezcan. Utilizar estados de ciclo de vida.

### Histórico

`PriceObservation` conserva cambios relevantes de:

- precio;
- list price;
- promoción;
- disponibilidad;
- seller/contexto cuando cambie el significado de la oferta.

No crear necesariamente una observación nueva ante cada polling idéntico. Si nada cambia, actualizar `LastSeenAt/LastCheckedAt`.

### RAW

Payloads externos:

- se comprimen con gzip;
- se deduplican por hash;
- se guardan fuera de SQLite;
- SQLite almacena metadatos y ruta relativa.

Retención recomendada V1: 60 días configurable (rango razonable 30–90).

## Frescura y TTL

Defaults iniciales:

| Recurso | TTL inicial |
|---|---:|
| Precio | 6 h |
| Stock/disponibilidad | 2 h |
| Producto/SKU | 7 días |
| Catálogo/categorías | 7 días |
| Sucursales | 7 días |
| Coordenadas de sucursal | 30 días |
| Ruta/distancia | 24 h |
| Promociones | 4 h |

Estados:

- `FRESH`: dentro del TTL.
- `STALE`: vencido pero todavía utilizable como último dato conocido.
- `OUTDATED`: suficientemente antiguo para no utilizarlo silenciosamente en una recomendación final.
- `UNKNOWN`: no existe evidencia utilizable.

Los umbrales deben ser configurables y visibles en UI avanzada.

## Estrategia de actualización

### 1. On demand

Cuando el usuario busca o abre una cesta:

1. consultar SQLite;
2. mostrar inmediatamente dato local si existe;
3. evaluar frescura;
4. si está vencido, actualizar en segundo plano/local async;
5. reemplazar UI cuando llegue dato nuevo;
6. si falla, conservar y marcar el último dato bueno.

### 2. Prioritaria

Prioridad de sincronización:

1. productos de la cesta activa;
2. productos frecuentes/favoritos;
3. resultados visibles de búsqueda;
4. catálogo restante.

### 3. Mantenimiento oportunista

Mientras la app está abierta y no existe carga prioritaria, sincronizar segmentos de catálogo con límites conservadores.

No se requiere un daemon 24/7. El PC puede permanecer apagado varios días.

## Inicio después de varios días apagado

La app debe abrir con datos locales inmediatamente. En paralelo:

- detectar última sincronización;
- actualizar primero cesta/favoritos;
- mostrar progreso no bloqueante;
- continuar con mantenimiento si procede.

Nunca bloquear la UI esperando una sincronización completa.

## Estados de producto externo

Sugeridos:

- `ACTIVE`
- `MISSING`
- `INACTIVE`
- `UNKNOWN`

Ejemplo de política inicial:

- ausente en 1–2 sincronizaciones: `MISSING`;
- ausente persistentemente durante ~30 días: `INACTIVE`;
- si reaparece: `ACTIVE`.

No borrar históricos ni relaciones canónicas solo porque un producto desaparezca temporalmente.

## Cambios de nombre y EAN

- Si el external SKU conserva ID/EAN y cambia el nombre, actualizar metadatos sin crear un canónico nuevo.
- Si cambia EAN o presentación, tratar inicialmente como SKU nuevo.
- El matcher puede proponer una relación/reemplazo, nunca asumirla solo por parecido textual.

## Sincronización transaccional

- Operaciones por lote deben ser transaccionales.
- Un fallo de página/lote no debe invalidar lotes ya confirmados.
- `SyncRun` registra estado y conteos.
- Un `RUNNING` antiguo encontrado al iniciar se marca `ABORTED`.

## Concurrencia

Un único backend .NET coordina escrituras SQLite.

Usar locks lógicos para evitar dos syncs concurrentes del mismo provider/contexto/recurso.

No bloquear lecturas prolongadamente.

## Histórico y compactación futura

V1 conserva eventos significativos de precio indefinidamente.

Si a futuro el volumen lo exige, se puede agregar roll-up para datos muy antiguos:

- mínimo diario;
- máximo diario;
- promedio diario;
- primera/última observación.

Nunca implementar compactación destructiva antes de medir crecimiento real.

## Retención recomendada

| Dato | Retención |
|---|---|
| Producto/SKU/canónico | Indefinida |
| PriceObservation | Indefinida inicialmente |
| MatchOverride | Indefinida |
| RAW | 60 días configurable |
| Logs | 30 días |
| SyncRun detallado | 90 días |
| Backups diarios | 7 |
| Backups semanales | 4 |
| Backups mensuales | 3 |

## Mantenimiento SQLite

- `foreign_keys=ON`.
- WAL cuando sea apropiado.
- `PRAGMA optimize` periódicamente.
- `VACUUM` solo cuando haya beneficio medible y no en cada inicio.
- `PRAGMA integrity_check` en health/mantenimiento y después de migraciones importantes.

## Backup y migraciones

Antes de migrar una BD existente:

1. backup consistente;
2. aplicar migración EF Core;
3. integrity check;
4. si falla, conservar backup y reportar error recuperable.

## Política de última evidencia válida

Si un provider falla:

- conservar `Offer` anterior;
- actualizar estado del provider/sync, no el precio a `null`;
- UI muestra antigüedad y fallo de actualización;
- el optimizador puede excluir datos `OUTDATED` según perfil/configuración.
