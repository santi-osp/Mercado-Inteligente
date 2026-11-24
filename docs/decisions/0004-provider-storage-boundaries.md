# ADR 0004 — Contratos de proveedores y evidencia local

Decisión: los supermercados implementan `IStoreProvider` en adaptadores aislados y devuelven productos, SKUs y ofertas tipadas junto con un contexto opaco trazable. `ProviderCapabilities` distingue las funciones soportadas; un precio contextual nunca prueba que exista precio de góndola de una sucursal. El proveedor sintético `DemoStoreProvider` se limita a pruebas/desarrollo y no se registra en el contenedor de producción. No se introduce código VTEX/FastStore en Domain.

Los datos relacionales, settings y metadatos RAW viven en SQLite mediante migraciones EF Core, con claves foráneas e índices. Payloads RAW viven como gzip en filesystem local, deduplicados por SHA-256 de los bytes originales; el nombre de archivo deriva del hash, sin incluir nombres arbitrarios de origen. La evidencia histórica conserva observaciones por separado del último estado vigente. Las copias de SQLite se crean por la API de backup, no por copia directa de un `.db` abierto. Este diseño local-first evita servicios externos para la operación básica y mantiene OpenRouter/Google opcionales.

Limitación: aún faltan adaptadores D1/Éxito, políticas completas de retención y validación runtime de recuperación y release; se completan en goals posteriores. Una copia solo de SQLite no incluye RAW, secretos DPAPI ni archivos de logs.
