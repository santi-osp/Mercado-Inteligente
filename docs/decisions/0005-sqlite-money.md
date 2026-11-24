# ADR 0005 — Dinero decimal y restricciones SQLite

El código usa `decimal` para precios COP. En SQLite EF Core serializa `decimal` como texto para conservar precisión; por ello un `CHECK (Price > 0)` en SQLite compara por afinidad textual y puede admitir el texto `0.0`. La migración `ValidPrices` usa `CAST(Price AS NUMERIC) > 0` y valida también `ListPrice` y observaciones. Los tests ejecutan inserciones inválidas sobre una base migrada y comprueban rechazo real. La conversión SQLite NUMERIC se usa solo para validar signo, no para calcular dinero: la aritmética sigue en `decimal` .NET.

Antes de aplicar migraciones pendientes a una base existente se crea una copia mediante SQLite backup API. Una base con precios inválidos anteriores a esta migración puede hacer fallar su rebuild; en ese caso no se debe eliminar ni reemplazar la copia, y se requiere revisar los datos antes de reintentar. `PRAGMA integrity_check` y `foreign_key_check` se comprueban al arrancar.
