# Estrategia de pruebas

## Principio

Las pruebas deben proteger decisiones de negocio y contratos frágiles, no perseguir un porcentaje artificial de cobertura.

## Pirámide

### Unit tests

Prioridad alta para:

- normalización;
- quantity parsing;
- conversión de unidades;
- packs;
- price-per-unit;
- frescura/TTL;
- scoring;
- optimizador;
- matching determinístico;
- promociones;
- reglas seller/marketplace;
- seguridad de settings.

### Integration tests

Usar SQLite aislada/temporal:

- migrations;
- constraints/foreign keys;
- repositories;
- price history;
- settings;
- backup/restore;
- dedup RAW metadata;
- SyncRun state transitions.

### Contract tests de providers

No fijar precios exactos que cambian diariamente. Verificar contrato estructural y semántico:

- request exitosa;
- lista razonable;
- identificadores;
- tipos;
- campos críticos;
- precio válido cuando corresponda;
- contexto esperado;
- mapping.

Contract tests live deben poder separarse de suite offline para evitar que Internet rompa cada build local.

### Frontend tests

Cubrir especialmente:

- navegación/shell;
- estados empty/loading/error/offline;
- settings;
- stale data badges;
- comparison modes;
- resumen de optimizador;
- provider capability rendering.

### E2E

En Goal 10:

- crear mercado;
- buscar/agregar productos;
- actualizar precios;
- comparar;
- optimizar;
- configurar ubicación;
- configurar IA opcional;
- reiniciar y comprobar persistencia;
- backup.

## Datos de prueba

Separar:

- `DEMO` visual;
- fixtures determinísticos;
- snapshots sanitizados de providers;
- live tests.

Nunca mezclar demo con datos de usuario real.

## Casos obligatorios

### Unidades

- 1000 g = 1 kg.
- 1000 ml = 1 L.
- 500 ml != 500 g.
- 6 x 1 L = 6 L.
- pack x12 con unidad count.

### Dinero

- cero/negativo inválido;
- promociones multibuy;
- redondeo decimal;
- COP.

### Stock

- null → UNKNOWN.
- 0 explícito → OUT_OF_STOCK cuando la fuente lo semantice así.
- gran sentinel no se expone como precisión física.

### Matching

- mismo EAN compatible → candidato exacto.
- mismo EAN pero conflicto crítico → no auto-aceptar.
- Zero vs Original → no exacto.
- fuzzy alto con atributos incompatibles → no exacto.
- override humano gana.

### Optimizador

- máximo N;
- N insuficiente;
- producto faltante;
- promoción condicionada;
- sobrecompra;
- sustitutos on/off;
- presupuesto;
- datos outdated.

### Resiliencia

- provider timeout;
- 500;
- 429;
- schema drift fixture;
- Google no configurado;
- OpenRouter no configurado;
- SQLite reinicio;
- sync abortado.

## Gates por goal

Ningún goal se marca COMPLETE con build roto o tests relevantes fallando.

Los goals 2/3/9 deben incluir contract tests del provider.

Goal 10 debe ejecutar suite completa y E2E.

## Performance targets iniciales

No son SLA rígidos, pero sirven de guardrails locales:

- búsqueda local visible: sensación <300 ms para dataset razonable;
- pantalla no debe esperar sync completo;
- optimizador típico 50–100 ítems / 5–10 tiendas: objetivo <2 s local, preferiblemente muy inferior;
- no ejecutar llamadas IA innecesarias por cada render/UI.

Medir antes de optimizar.
