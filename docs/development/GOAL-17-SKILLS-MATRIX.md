# GOAL 17 — matriz de skills y herramientas

Inventario inicial (2026-10-10). La versión de las skills locales no está declarada en sus `SKILL.md`; se registra como «sin versión declarada» en vez de inferirla. No se instaló software externo ni se otorgó acceso a secretos.

| Necesidad | Skill/Herramienta | Fuente | Versión | Estado | Decisión | Uso |
|---|---|---|---|---|---|---|
| Descubrimiento | `find-skills` | Skill local en `.agents/skills` | Sin versión declarada | Leída | Usar inventario local antes de instalar | Revisión de capacidades disponibles y duplicados |
| Sistema visual | `ui-design` (product UI) | Skill local en `.agents/skills` | Sin versión declarada | Leída junto con anexos | Usar | Jerarquía, densidad, tokens y reducción de paneles |
| Interfaz frontend | `frontend-design` | Skill local en `.agents/skills` | Sin versión declarada | Leída | Usar con restricciones de marca del proyecto | Dirección visual para páginas de consumo |
| Auditoría UI y WCAG | `ui-audit` | Skill local en `.agents/skills` | Sin versión declarada | Leída | Usar | Revisión de controles, foco, estados y navegación |
| Auditoría UX React | `ux-audit` | Skill local en `.agents/skills` | Sin versión declarada | Revisada | Descartar para Angular | Sus reglas de implementación son específicas de React/Next.js |
| Capturas y E2E | Playwright | Dependencia del proyecto `@playwright/test` | ^1.63.0 | Instalada | Usar | Assertions previas a capturas, flujos y viewports |
| Angular | Angular CLI y tests del proyecto | Dependencias oficiales de Angular | 22.2.1 | Instaladas | Usar | Build, tests y arquitectura existente |
| Performance | `web-perf` | Skill local en `.codex/skills` | Sin versión declarada | Revisada | Evaluar según MCP disponible | Métricas web si DevTools MCP está accesible |
| Visual regression | Playwright y comparación de imágenes | Dependencia existente | ^1.63.0 | Disponible | Usar | Comparar capturas certificadas antes/después |

No hay motivo para instalar skills duplicadas de diseño, accesibilidad o Playwright. Cualquier actualización requiere origen y compatibilidad verificables; las skills sin manifiesto de versión no se actualizan a ciegas.
