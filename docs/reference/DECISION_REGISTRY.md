# Registro de decisiones cerradas

Estas decisiones son defaults de V1 y no deben reabrirse sin evidencia.

| Tema | Decisión |
|---|---|
| Ejecución | Local en PC Windows |
| Usuario | Único usuario inicialmente |
| Frontend | Angular PWA |
| Backend | ASP.NET Core |
| Arquitectura | Modular monolith |
| ORM | EF Core |
| DB | SQLite |
| RAW | filesystem + gzip + hash/dedup |
| Cloud | No requerido V1 |
| IA | Opcional, OpenRouter BYOK |
| Funcionamiento sin IA | Obligatorio |
| Mapas | Google Geocoding + Routes; sin mapa interactivo obligatorio |
| Región inicial | Medellín |
| Tiendas V1 | D1 + Éxito, Ara parcial |
| Marketplace Éxito | Excluido por defecto |
| Max stores default | 2 |
| Tolerancia tamaño default | ±15% |
| Sustitutos default | Permitidos, pero diferenciados de exactos |
| Sobrecompra default | Máx. 20% |
| Precio TTL default | 6 h |
| Stock TTL default | 2 h |
| Product TTL | 7 días |
| Store TTL | 7 días |
| Route TTL | 24 h |
| Money | decimal/valor monetario seguro, COP |
| Timestamps internos | UTC |
| UI timezone | America/Bogota |
| Fuzzy | Solo candidato |
| User override | Máxima prioridad |
| PriceScope | Siempre preservar contexto |
| Históricos | No sobrescribir, guardar cambios significativos |
| Telemetría externa | No |
| Provider pattern | Obligatorio |
