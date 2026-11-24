# Matriz final de aceptación 1.0.0

| Área | Criterio | Evidencia esperada |
|---|---|---|
| Backend | Build release exitoso | comando/log final |
| Frontend | Build production exitoso | comando/log final |
| DB | migrations desde cero | integration test |
| DB | integrity check OK | health/report |
| Backup | crear + restaurar DB temporal | integration/E2E |
| D1 | search/product/offer/context | contract + smoke |
| Éxito | search/seller/offer/context | contract + smoke |
| Ara | cobertura parcial honesta | provider tests/UI |
| Histórico | cambio crea observation | integration test |
| Histórico | polling idéntico no infla | integration test |
| Matching | EAN/reglas | unit/evaluation |
| Matching | fuzzy no auto exact | unit test |
| Override | usuario prevalece | unit/integration |
| Optimizer | max N | deterministic tests |
| Optimizer | N insuficiente explicado | test/UI |
| Promos | condiciones respetadas | unit tests |
| Location | parse input | unit tests |
| Google | real o NOT_CONFIGURED seguro | smoke/fallback |
| OpenRouter | real o NOT_CONFIGURED seguro | smoke/fallback |
| IA OFF | flujo completo | E2E |
| Offline | last-known-good | E2E |
| UI | desktop/mobile | screenshots/QA |
| UI | stale/error/partial | component/E2E |
| Security | secrets no versionados | scan/check |
| Security | loopback + safe errors | test/config |
| Maintenance | RAW/log retention | integration |
| Docs | README + final report | files |
| Release | 1.0.0 ejecutable local | release smoke |
