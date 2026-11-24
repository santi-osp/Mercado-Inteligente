# GOAL 07 — Ubicación, Google Maps y optimización de ruta

## Propósito

Agregar la dimensión geográfica real de Medellín: entrada de ubicación, geocoding, tiendas cercanas, rutas, tiempo/distancia y orden de visita, sin convertir Google en una dependencia crítica del producto.

## Precondición

GOAL-06 completo. Leer `EXTERNAL_SERVICES_SETUP.md`.

## Configuración externa

Si `GOOGLE_MAPS_API_KEY` todavía no existe:

- implementar todo el soporte técnico y mocks;
- marcar live verification como `BLOCKED_EXTERNAL`;
- no detener el roadmap.

Si existe, almacenarla mediante `ISecretStore` y ejecutar live tests.

## Entrada de ubicación

Soportar:

- dirección escrita;
- coordenadas `lat,lng`;
- URL larga de Google Maps;
- URL corta `maps.app.goo.gl` cuando pueda resolverse legítimamente;
- perfiles guardados como Casa/Trabajo/etc.

Crear/robustecer `ParseLocationInput` con resultado estructurado:

- original input;
- normalized address opcional;
- latitude/longitude opcional;
- source;
- confidence/status;
- errors claros.

## Google Geocoding

Implementar `GoogleGeocodingProvider` detrás de `IGeocodingProvider`.

Requisitos:

- backend only;
- timeout/retry razonable;
- caché persistente;
- no geocodificar repetidamente misma dirección normalizada;
- status NOT_CONFIGURED si falta key;
- errores quota/denied/zero results tratados.

## Google Routes

Implementar `GoogleRoutingProvider` detrás de `IRoutingProvider`.

Necesidades:

- origen→destino;
- distance meters;
- duration;
- modo configurable con default coherente (moto puede mapearse según soporte; si API no ofrece motocicleta en región/endpoint elegido, documentar fallback a driving, no inventar);
- caché `RouteTtl=24h`;
- route context/version.

No se requiere mapa interactivo embebido.

## Store locations

Usar `Store` local como fuente de sucursales conocidas.

No usar Google Places en cada búsqueda por defecto. Si las fuentes actuales no cubren sucursales suficientemente, documentar estrategia futura/opt-in, pero evitar ampliar APIs sin necesidad.

Permitir import/seed/actualización de stores desde providers/fuentes legítimas.

## Nearby stores

Calcular inicialmente Haversine para filtrar candidatos cercanos rápidamente.

Luego obtener rutas precisas solo para candidatos relevantes.

Evitar N×M llamadas innecesarias a Google.

## Distancia en UI

Distinguir:

- `ROAD_ROUTE`: ruta real.
- `STRAIGHT_LINE_ESTIMATE`: Haversine/fallback.
- `CACHED_ROUTE`.

Nunca mostrar distancia lineal como “recorrido” sin etiqueta.

## Integración con optimizador

Mantener problemas separados:

1. candidatos de productos;
2. selección de tiendas;
3. orden de visita.

### Estrategia equilibrada

Ahora integrar:

- costo productos;
- número de tiendas;
- distancia/tiempo;
- costo/km opcional.

Pesos configurables:

- PriceWeight;
- DistanceWeight;
- ConvenienceWeight;
- BrandWeight.

Normalizar magnitudes para que porcentajes tengan significado. Documentar fórmula y tests.

## Orden de visita

Para 1–4 tiendas, evaluar permutaciones exactas incluyendo origen y, si configuración lo indica, retorno al origen.

Para más tiendas, mantener una estrategia razonable y medible; no introducir solver complejo salvo necesidad.

Configurar:

- returnToOrigin;
- transport mode;
- cost per km;
- max route km opcional.

## Abrir en Google Maps

Generar URL de navegación/itinerario para que el usuario pueda abrir la ruta en Google Maps desde la UI.

No incluir key en URL pública.

## Cost control

Implementar:

- cache;
- rate limiting local;
- evitar calls duplicadas;
- contador local de requests por servicio/mes aproximado;
- UI de configuración con recomendación de quotas/budget alerts.

No intentar leer facturación privada de Google salvo integración explícita futura.

## Fallback

Si Google falla/quota/key ausente:

- geocoding nuevo no disponible si no hay coordenadas;
- perfiles previamente geocodificados siguen funcionando;
- Haversine disponible;
- optimizer puede ejecutar modo sin route cost o con estimated distance;
- UI explica limitación.

## Privacidad

Dirección exacta solo se envía a Google cuando el usuario solicita geocoding/ruta y la integración está activa.

No enviar ubicación a OpenRouter.

## Tests

Offline:

- parse coordinates;
- parse long Google URL patterns;
- invalid URL;
- Haversine known pairs;
- route cache key;
- Google not configured;
- quota error fixture;
- route ordering exact 2/3/4 stores;
- optimizer weights.

Live si key disponible:

- 3+ direcciones genéricas Medellín;
- route entre puntos conocidos;
- cache evita segunda llamada;
- invalid address handled.

## UI

- Location selector/profile;
- configuración Google con `configured` sin mostrar key;
- rutas/distancia en resultados;
- “Abrir en Google Maps”;
- badges exact/estimated;
- settings cost/km, max route, return origin.

## Entregables

- Google providers;
- location parser;
- route optimizer;
- cache/cost guards;
- tests;
- setup instructions actualizadas;
- `goal-07-location-report.md`.

## DoD

- sin key: app completa sigue funcionando y goal puede ser COMPLETE_WITH_EXTERNAL_BLOCKERS;
- con key: live smoke PASS;
- no key leakage;
- optimizer integra distancia sin romper máximo N;
- route order tested;
- PROJECT_STATE actualizado.

## Continuación

Pasar inmediatamente a GOAL-08 aunque la key de Google esté pendiente.
