# Definition of Done global

## DoD de una tarea

Una tarea se considera terminada cuando:

- implementación real existe;
- errores relevantes están manejados;
- tests apropiados existen/pasan;
- no rompe build;
- documentación afectada está actualizada;
- no introduce secretos ni deuda crítica oculta.

## DoD de un goal

- todas las tareas obligatorias del goal están completas o justificadamente `BLOCKED_EXTERNAL`;
- builds aplicables pasan;
- tests aplicables pasan;
- smoke test definido pasa;
- entregables/documentación existen;
- `PROJECT_STATE.md` está actualizado;
- limitaciones están declaradas;
- no quedan TODOs críticos no registrados.

## DoD del proyecto 1.0.0

### Plataforma

- [ ] Angular PWA ejecutable localmente.
- [ ] ASP.NET Core ejecutable localmente.
- [ ] SQLite con migrations.
- [ ] datos bajo carpeta local segura, no repo.
- [ ] backup e integrity check.

### Datos

- [ ] Product/SKU/Offer correctamente separados.
- [ ] histórico de precios.
- [ ] RAW gzip + dedup + retención.
- [ ] frescura/TTL.
- [ ] last-known-good.

### Stores

- [ ] D1 real.
- [ ] Éxito real.
- [ ] Ara parcial honesto.
- [ ] provider capabilities.
- [ ] health + contract tests.
- [ ] marketplace no mezclado por defecto.

### Comparación

- [ ] unidades/packs.
- [ ] precio por kg/L/unidad.
- [ ] exact matching conservador.
- [ ] substitutes.
- [ ] overrides humanos.
- [ ] fuzzy solo candidato.

### Cesta

- [ ] cantidades.
- [ ] máximo N tiendas configurable.
- [ ] estrategias de optimización.
- [ ] faltantes.
- [ ] promociones.
- [ ] sobrecompra.

### Ubicación

- [ ] dirección/coordenadas/Google Maps URL.
- [ ] geocoding real si key disponible.
- [ ] rutas reales si key disponible.
- [ ] fallback/documentación si no.
- [ ] orden de visita.

### IA

- [ ] app funciona con IA OFF.
- [ ] OpenRouter configurable.
- [ ] SecretStore.
- [ ] structured outputs/validation.
- [ ] fallback ante fallo.
- [ ] IA no inventa precios/stock.

### UI/UX

- [ ] Design System consistente.
- [ ] responsive.
- [ ] empty/loading/error/offline.
- [ ] data freshness visible.
- [ ] PriceScope visible cuando importa.
- [ ] accesibilidad razonable.

### Calidad

- [ ] unit tests.
- [ ] integration tests.
- [ ] contract tests.
- [ ] E2E esenciales.
- [ ] build release.
- [ ] dependencias revisadas.
- [ ] documentación final.

### Release

- [ ] versión 1.0.0.
- [ ] instrucciones desde cero.
- [ ] configuración Google/OpenRouter.
- [ ] backup/restore documentado.
- [ ] limitaciones conocidas.
- [ ] `FINAL_PROJECT_REPORT.md`.
