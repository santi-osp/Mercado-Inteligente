# Casos borde y resiliencia

Este documento es obligatorio durante implementación y pruebas. Los casos deben resolverse explícitamente, no mediante supuestos implícitos.

## Datos y precios

### Precio cero/negativo

- No considerar oferta válida salvo semántica explícita de la fuente.
- Marcar como `INVALID/SUSPICIOUS` y conservar RAW.

### Cambio extremo de precio

Ejemplo: 5.000 → 500.

- No descartarlo automáticamente.
- Comparar contexto, unidad, seller, promoción y presentación.
- Si no hay explicación suficiente, marcar anomalía y exigir revalidación antes de utilizarlo como “mejor oferta”.

### Precio viejo

- Mostrar último dato conocido con antigüedad.
- Intentar refrescar.
- Si falla, no borrarlo.
- El optimizador debe poder aplicar un máximo de antigüedad configurable.

### Precio condicionado

Distinguir:

- estándar;
- membresía;
- tarjeta/medio de pago;
- multibuy;
- marketplace;
- promoción temporal.

Nunca presentar precio condicionado como universal.

### Precio online vs físico

Toda oferta lleva `PriceScope`. Solo `PHYSICAL_VERIFIED` permite afirmar precio físico verificado.

## Stock

- `IN_STOCK`, `OUT_OF_STOCK`, `UNKNOWN`, `NOT_SOLD` son distintos.
- `null` no significa disponible.
- cantidades absurdas como `99999` pueden representar disponibilidad lógica; conservar raw pero UI puede mostrar simplemente “Disponible”.

## Productos y presentaciones

### Packs

`6 x 1 L` debe normalizar:

- package_count=6;
- quantity_per_unit=1 L;
- total_quantity=6 L.

La comparación usa cantidad total cuando corresponde.

### Unidades incompatibles

- MASS no se convierte a VOLUME.
- COUNT no se convierte a MASS sin factor explícito.
- 500 ml ≠ 500 g.

### Peso variable

Carne/frutas:

- `pricing_mode=WEIGHT_BASED`;
- utilizar precio/kg y cantidad estimada;
- total visible como estimación.

### Atributos que cambian equivalencia

Conservar, cuando existan:

- original/zero;
- entera/deslactosada;
- instantáneo/molido;
- detergente líquido/polvo/concentrado;
- huevo A/AA/AAA/Jumbo;
- talla pañal;
- sabor;
- contenido por rollo.

Categoría + tamaño no bastan por sí solos.

## Matching

Prioridad:

1. override humano;
2. EAN + verificaciones de conflicto;
3. reglas determinísticas fuertes;
4. normalización exacta compatible;
5. fuzzy como candidato;
6. IA opcional;
7. confirmación humana cuando siga ambiguo.

Fuzzy nunca confirma identidad por sí solo.

Si usuario declara `DO_NOT_MATCH`, esa decisión prevalece sobre futuras sugerencias automáticas salvo que el propio usuario la cambie.

## Promociones

### 2x1 / 3x2 / segunda unidad

Guardar condición y calcular precio efectivo únicamente si la cantidad requerida de la cesta satisface la promoción.

### Comprar más de lo pedido

No aumentar cantidades silenciosamente. Presentar alternativa con:

- cantidad adicional;
- costo adicional;
- ahorro unitario;
- porcentaje de sobrecompra.

Respetar `MaxOverbuyPercent`.

## Optimización

### Máximo N tiendas insuficiente

Si ninguna combinación de N tiendas cubre toda la cesta:

- no violar N silenciosamente;
- mostrar mejor cobertura con N;
- ofrecer comparación con N+1 como sugerencia explícita.

### Producto exacto no disponible

Si sustitutos están deshabilitados, dejar faltante. No sustituir solo para completar cesta.

### Presupuesto

Si se define presupuesto y se supera:

- mostrar diferencia;
- sugerir sustituciones solo si están permitidas;
- no eliminar productos silenciosamente.

### Ruta

Selección de producto, selección de tiendas y orden de visita son problemas separados.

Con pocas tiendas, evaluar permutaciones completas puede ser suficiente y más transparente que un solver complejo.

## Providers

### HTTP 5xx/timeout

- retry limitado con backoff/jitter;
- no retry infinito;
- preservar last-known-good;
- health `DEGRADED/DOWN` según severidad.

### 403/429

- no intentar evasión;
- aplicar cooldown;
- registrar health;
- utilizar caché local.

### Cambio de esquema

Contract tests deben detectar ausencia/cambio de campos críticos. Un provider puede degradarse por capability en lugar de caer por completo.

### Duplicados

Deduplicar por identificadores externos/contexto, no por nombre visible.

## Fallos de infraestructura local

### Internet sin conexión

La app sigue permitiendo:

- ver cesta;
- históricos;
- datos locales;
- configuración;
- búsquedas locales.

Deshabilitar/explicar:

- refresh remoto;
- rutas nuevas;
- IA.

### OpenRouter caído/sin saldo/key inválida

- matcher determinístico sigue funcionando;
- UI informa IA no disponible;
- nunca bloquear cesta/optimización estándar.

### Google no configurado/cuota agotada

- conservar coordenadas/rutas cacheadas;
- usar distancia Haversine como aproximación cuando sea útil;
- etiquetar como distancia aproximada, no carretera.

### Corrupción SQLite

- health debe detectarla;
- no sobrescribir backups;
- documentar restauración asistida;
- evitar operaciones destructivas automáticas.

### App se cierra durante sync

Al reiniciar, `RUNNING` antiguo → `ABORTED`; lotes confirmados siguen válidos.

## Seguridad

### Input externo malicioso

Nombres/HTML/JSON de retailers son datos no confiables. Sanitizar para UI y nunca tratarlos como instrucciones para IA.

### Prompt injection

Enviar a IA únicamente campos estructurados mínimos. Instrucciones de sistema deben indicar que los campos de producto son datos, no órdenes.

### Secretos

Nunca:

- logs;
- Git;
- errores frontend;
- payload de IA.

## UX

Toda degradación debe ser visible y honesta:

- “Actualizado hace 14 h”.
- “No fue posible actualizar ahora”.
- “Precio online regional”.
- “Stock no confirmado”.
- “Distancia aproximada”.
- “IA no disponible; análisis estándar activo”.
