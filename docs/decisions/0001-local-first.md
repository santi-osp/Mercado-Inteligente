# ADR 0001 — Persistencia local y monolito modular

Decisión: ASP.NET Core/EF Core/SQLite en loopback con frontend Angular PWA, sin microservicios ni telemetría externa. RAW gzip/hash fuera de SQLite y secretos DPAPI CurrentUser fuera del repositorio. Provider interface aísla supermercados; OpenRouter es opcional y no participa en cálculos ni en funcionamiento básico. Conforme a `docs/reference/DECISION_REGISTRY.md`; pendiente validar migración, recuperación y endpoints completos antes de release.
