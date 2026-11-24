# Gates de ejecución 01→10

Este archivo documenta el orden y evita declarar completitud prematuramente. Cada gate requiere su propio DoD en `goals/`, builds/tests/smoke y actualización de `PROJECT_STATE.md` antes de avanzar formalmente.

1. GOAL-01: migración limpia, backend/frontend y smoke local, settings persistentes, backup/restore y shell accesible.
2. GOAL-02: D1 en contexto regional, SKU/EAN, RAW, contratos offline y live opt-in; no precio de góndola.
3. GOAL-03: Éxito FastStore, seller comercial distinto de fulfillment, marketplace excluido por defecto.
4. GOAL-04: sincronización, observaciones inmutables, retención, last-known-good y recovery.
5. GOAL-05: matching conservador con override prioritario, corpus y evaluación.
6. GOAL-06: cesta y máximo N con cobertura parcial explícita; promociones condicionadas.
7. GOAL-07: Google backend + fallback honesto, no llamar ruta a Haversine.
8. GOAL-08: OpenRouter BYOK opcional, esquema validado, IA OFF completa.
9. GOAL-09: Ara parcial de fuente permitida + provider ficticio extensible.
10. GOAL-10: hardening, E2E, seguridad, backup/restore, release solo con gates verificadas.

Actualmente **solo hay trabajo parcial de GOAL-01**. Ninguna dependencia de API key justifica marcar como completo un bloque de implementación local pendiente.
