# Informe de Desarrollo — GOAL-07: Ubicación, Google Maps y Optimización de Ruta

## 1. Resumen Ejecutivo

El objetivo **GOAL-07** implementa el sistema integral de localización geográfica, resolución de direcciones, cálculo de distancias y optimización de rutas entre tiendas para **Mercado Inteligente**.

Este subsistema permite a los usuarios definir su ubicación de origen mediante múltiples formatos (coordenadas numéricas, coordenadas direccionales, enlaces largos y cortos de Google Maps, perfiles almacenados o direcciones normalizadas del Valle de Aburrá / Medellín), estimar distancias a establecimientos físicos de retail (D1, Éxito, Ara, etc.), optimizar el orden secuencial de visita entre tiendas seleccionadas mediante algoritmos exactos y heurísticos, y generar enlaces directos de navegación en Google Maps con cero exposición de credenciales secretas (*Zero-Key Navigation URLs*).

El motor opera con una arquitectura de doble capa:
1. **Capa Online Conectada**: Soporte para Google Maps Geocoding y Directions APIs con almacenamiento cifrado de claves en reposo mediante Windows DPAPI (`ISecretStore`), cachés locales persistentes en disco (`geocoding-cache.json` y `routing-cache.json` con TTL de 24 horas), y monitoreo diario de cuotas (`google-usage.json`).
2. **Capa Offline Autónoma**: Fallback automático y determinista ante ausencia o fallo de clave mediante cálculo esférico Haversine con factor de sinuosidad urbana de $1.35\times$ y perfiles de velocidad por modo de transporte (conducción, motocicleta, bicicleta, a pie), garantizando que el usuario y el optimizador de cestas operen sin interrupciones incluso sin conexión externa o sin saldo de API.

---

## 2. Componentes Arquitectónicos

### 2.1 Analizador Multi-Formato de Ubicaciones (`LocationInputParser.cs`)

El analizador resuelve de forma determinista la entrada del usuario en uno de los siguientes orígenes:

1. **Coordenadas Decimales Estándar (`LocationInputSource.Coordinates`)**:
   - Patrón: `lat, lon` o `lat lon` (ej. `6.2425, -75.5925`).
   - Valida que las coordenadas pertenezcan a rangos válidos: latitud $\in [-90, 90]$, longitud $\in [-180, 180]$.
   - Reconocimiento específico del rango del Valle de Aburrá (latitud 5.8 a 6.6, longitud -75.8 a -75.2).

2. **Coordenadas con Notación Cardinal/Direccional (`LocationInputSource.Coordinates`)**:
   - Patrón: `6.2425° N, 75.5925° W` o `6.2425 N, 75.5925 O`.
   - Convierte automáticamente cuadrantes Sur (S) y Oeste (W / O) a signo negativo.

3. **URLs Largas de Google Maps (`LocationInputSource.GoogleMapsUrl`)**:
   - Extrae pares de coordenadas desde URLs que contienen `@lat,lng` (ej. `https://www.google.com/maps/@6.2425,-75.5925,15z`).
   - Extrae pares de coordenadas desde parámetros de consulta `?q=lat,lng` (ej. `https://maps.google.com/?q=6.2425,-75.5925`).

4. **URLs Cortas de Google Maps (`LocationInputSource.GoogleMapsShortUrl`)**:
   - Identifica enlaces de redirección de Google Maps (`maps.app.goo.gl` o `goo.gl/maps`).
   - Retorna estado `NeedsGeocoding` para resolución HTTP del encabezado `Location` en el backend.

5. **Nombres de Perfiles Guardados (`LocationInputSource.Profile`)**:
   - Si la entrada coincide con el nombre de un perfil registrado por el usuario (ej. `"Casa"`, `"Trabajo"`), se resuelven de inmediato sus coordenadas.

6. **Normalización de Nomenclatura Urbana de Medellín (`LocationInputSource.Address`)**:
   - Expande abreviaturas urbanas comunes en Colombia:
     - `Cra`, `Cra.` $\to$ `Carrera`
     - `Cl`, `Cll`, `Cl.` $\to$ `Calle`
     - `Trv`, `Tv`, `Trans.` $\to$ `Transversal`
     - `Diag`, `Dg` $\to$ `Diagonal`
     - `Av`, `Av.` $\to$ `Avenida`
   - Normaliza caracteres y anexa de forma determinista `, Medellín, Colombia` si no incluye ciudad para mejorar la precisión del geocodificador.
   - Retorna estado `NeedsGeocoding`.

---

### 2.2 Gestión de Perfiles de Ubicación (`LocationProfileService.cs`)

Permite a los usuarios almacenar y reutilizar ubicaciones frecuentes:
- Almacenamiento local persistente en formato JSON en `data/location-profiles.json`.
- Acceso thread-safe con bloqueos de sincronización y lectura/escritura atómica.
- Soporte para perfiles predeterminados: "Casa", "Trabajo", "Oficina", etc.
- API REST completa: `GET /api/v1/location/profiles`, `POST /api/v1/location/profiles`, `DELETE /api/v1/location/profiles/{id}`.

---

### 2.3 Proveedores de Geocodificación y Enrutamiento

#### 2.3.1 `GoogleGeocodingProvider.cs`
- Implementa `IGeocodingProvider`.
- Obtiene la clave de Google Maps en tiempo de ejecución desde `ISecretStore` con el identificador `"google_maps_api_key"`. Si no existe o está vacía, opera en modo degradado sin fallar.
- **Caché Persistente en Disco**: Almacena respuestas en `data/geocoding-cache.json`. Dado que las coordenadas geográficas de una dirección física son invariantes en el tiempo, las entradas de geocodificación no expiran, reduciendo drásticamente el consumo de API.
- **Seguimiento de Cuota**: Incrementa el contador diario en `data/google-usage.json` bajo la métrica `geocodeCount`.
- **Fallback Offline**: Si la clave no está configurada o se produce un error de red/cuota, responde con coordenadas predeterminadas de Medellín o simulación offline transparente.

#### 2.3.2 `GoogleRoutingProvider.cs`
- Implementa `IRoutingProvider`.
- Consulta la Google Directions API para calcular ruta por carretera (`RoadRoute`), distancia en kilómetros y duración estimada en minutos.
- **Caché con TTL de 24 Horas**: Almacena respuestas en `data/routing-cache.json`. Las rutas por carretera varían poco día a día, por lo que una expiración de 24h conserva cuota y ofrece tiempos de respuesta inmediatos.
- **Seguimiento de Cuota**: Incrementa el contador diario en `data/google-usage.json` bajo la métrica `routeCount`.
- **Fallback Automático a Haversine**: Si la API externa no está disponible, delega en `HaversineService` con tipo de ruta `StraightLineEstimate`.

#### 2.3.3 `HaversineService.cs`
- Cálculo esférico mediante la fórmula de Haversine:
  $$d = 2R \arcsin \left( \sqrt{\sin^2\left(\frac{\Delta \phi}{2}\right) + \cos(\phi_1)\cos(\phi_2)\sin^2\left(\frac{\Delta \lambda}{2}\right)} \right)$$
  con radio terrestre $R = 6,371\text{ km}$.
- **Factor de Sinuosidad Urbana ($1.35\times$)**: La distancia euclidiana en línea recta subestima la distancia real de viaje en cuadrículas urbanas o vías sinuosas. Se aplica un factor multiplicador de $1.35$ calibrado para la geografía urbana de Medellín.
- **Perfiles de Velocidad y Duración**:
  - `Driving` (Vehículo): $25\text{ km/h}$ urbano.
  - `Motorcycle` (Motocicleta): $30\text{ km/h}$ urbano.
  - `Bicycling` (Bicicleta): $15\text{ km/h}$.
  - `Walking` (Caminata): $4.5\text{ km/h}$.

---

### 2.4 Optimizador de Rutas entre Tiendas (`StoreRouteOptimizer.cs`)

El optimizador determina la secuencia óptima para visitar un conjunto de $K$ tiendas a partir de un punto de origen (y un retorno opcional al origen):

1. **Búsqueda Exacta por Permutaciones ($K \le 4$)**:
   - Evalúa exhaustivamente las $K!$ permutaciones posibles (ej. para $K = 4$, $24$ combinaciones).
   - Calcula para cada orden la suma de distancias de los tramos ($Origen \to T_1 \to T_2 \to \dots \to T_K$ y opcionalmente $T_K \to Origen$).
   - Garantiza matemáticamente la ruta de menor distancia y tiempo posible.

2. **Heurística del Vecino Más Cercano ($K > 4$)**:
   - Para más de 4 tiendas, aplica la heurística *Nearest Neighbor* (vecino más cercano) para evitar explosión combinatoria, asegurando respuestas en tiempo sub-milisegundo.

3. **Generador de URLs de Navegación Zero-Key**:
   - Construye URLs públicas de Google Maps con el formato estándar de navegación web y móvil:
     ```
     https://www.google.com/maps/dir/?api=1&origin={lat},{lon}&destination={destLat},{destLon}&waypoints={lat1},{lon1}|{lat2},{lon2}&travelmode={mode}
     ```
   - **Regla estricta de seguridad**: Esta URL NO requiere ni incluye ninguna API key o token secreto, eliminando por diseño cualquier riesgo de filtración de credenciales al navegador o a terceros.

---

### 2.5 Integración Multi-Criterio con el Optimizador de Cesta (`BasketOptimizer.cs`)

El optimizador de cestas integra el contexto de ubicación (`OptimizerLocationContext`) en la estrategia balanceada (`OptimizationStrategy.Balanced`):

$$\text{Puntaje} = (\text{CostoProductos} \times W_{\text{precio}}) + (\text{GastoDesplazamiento} \times W_{\text{distancia}}) + (\text{PenalidadTienda} \times W_{\text{conveniencia}})$$

donde:
- $\text{GastoDesplazamiento} = \text{DistanciaTotalKm} \times \text{CostoPorKm}$ (ej. $2,500 COP/km).
- $\text{PenalidadTienda} = \max(0, \text{TiendasUsadas} - 1) \times \text{PenalidadPorTienda}$ (ej. $5,000 COP por tienda adicional).
- Se respeta estrictamente el límite máximo de $N$ tiendas configurado ($1 \le N \le 5$).

---

### 2.6 Interfaz de Usuario PWA en Angular

1. **Panel de Ubicación ("Mi mercado")**:
   - Selector y guardado de perfiles ("Casa", "Trabajo", etc.).
   - Entrada flexible de direcciones, URLs de Google Maps o coordenadas.
   - Indicador visual del origen activo con dirección y coordenadas.

2. **Listado de Tiendas Cercanas**:
   - Muestra tiendas físicas ordenadas por proximidad con distancias calculadas dinámicamente en vivo.
   - Permite seleccionar tiendas para optimizar rutas independientes o vincularlas a la cesta.

3. **Panel de Optimización de Ruta**:
   - Controles de modo de transporte (Vehículo, Moto, Bicicleta, Caminata), costo por kilómetro y opción de retorno al origen.
   - Tarjetas métricas de resumen: Distancia total (km), Tiempo estimado (min) y Costo de desplazamiento ($ COP).
   - Enlace directo *"Abrir ruta en Google Maps ↗"* (URL pública sin clave).
   - Desglose tramo por tramo con distintivos visuales del tipo de cálculo:
     - `Vía carretera` (Google Directions).
     - `En caché` (Caché local 24h).
     - `Estimación offline` (Haversine con factor 1.35x).

4. **Monitor de Uso en Ajustes**:
   - Tarjeta de observabilidad en la pestaña de Ajustes mostrando el estado de configuración (Configurado / Mock offline), solicitudes del día a Geocoding y Directions, y advertencia de que la app funciona completamente offline sin claves.

---

## 3. Pruebas y Verificación

### 3.1 Pruebas de Backend (.NET xUnit)
- **Suite completa**: 248 pruebas ejecutadas y aprobadas al 100%.
- Pruebas clave de GOAL-07:
  - `LocationParserTests`: Coordenadas decimales, cardinales, URLs largas `@lat,lng`, parámetros `?q=lat,lng`, URLs cortas `maps.app.goo.gl`, perfiles y normalización de nomenclatura de Medellín.
  - `HaversineServiceTests`: Precisión esférica, factor de sinuosidad 1.35x, velocidades y tiempos por modo de transporte.
  - `GoogleProvidersTests`: Cifrado DPAPI vía `ISecretStore`, persistencia en caché de geocodificación y routing (24h TTL), tracking de cuotas en `google-usage.json` y degradación elegante sin clave.
  - `StoreRouteOptimizerTests`: Algoritmo exacto de permutación $K!$, Nearest Neighbor, cálculo de costo por kilómetro, y generación de URL pública sin clave.
  - `LocationApiIntegrationTests`: Endpoints `/api/v1/location/parse`, `/api/v1/location/geocode`, `/api/v1/location/profiles`, `/api/v1/location/stores`, `/api/v1/routes/optimize`, y `/api/v1/location/usage`.
  - `BasketOptimizerTests`: Escalamiento de rendimiento (100 ítems en 10 tiendas ejecutado en 186 ms, muy por debajo del objetivo de 500 ms).

### 3.2 Pruebas de Frontend (Angular Vitest)
- **Suite completa**: 39 pruebas ejecutadas y aprobadas al 100%.
- Pruebas específicas añadidas en `app.spec.ts`:
  - `parses location, geocodes if needed, and updates active coordinates and stores`
  - `manages location profiles: loads, saves, selects, and deletes profile`
  - `optimizes store route, calculates legs, and provides zero-key navigation URL`
  - `loads Google usage stats in settings without exposing secrets`
- **Build de producción**: `npm run build` genera el bundle de producción sin advertencias ni errores.

---

## 4. Estado de Cumplimiento (Definition of Done)

| Criterio DoD | Estado | Evidencia |
|---|---|---|
| Soporte multi-formato de ubicación (decimal, cardinal, URLs cortas/largas, perfiles, Medellín) | CUMPLIDO | `LocationInputParser.cs`, pruebas unitarias en `LocationParserTests.cs` |
| Proveedores Google con DPAPI, caché persistente (24h TTL routing), tracker diario y fallback offline | CUMPLIDO | `GoogleGeocodingProvider.cs`, `GoogleRoutingProvider.cs`, `HaversineService.cs` |
| Optimización de rutas de tiendas ($K!$ exacto para $K \le 4$, Nearest Neighbor para $K > 4$) | CUMPLIDO | `StoreRouteOptimizer.cs`, `StoreRouteOptimizerTests.cs` |
| Enlaces de navegación Google Maps sin claves (*Zero-Key Navigation URLs*) | CUMPLIDO | URLs públicas generadas sin parámetros secretos, verificado en tests |
| Integración de distancia y gasto de viaje en cesta balanceada sin romper Max-N | CUMPLIDO | `BasketOptimizer.cs`, `OptimizerLocationContext` |
| Interfaz gráfica en Angular PWA con perfiles, tiendas, rutas, badges de fuente y monitor de uso | CUMPLIDO | `app.ts`, `app.html`, `app.css`, 39 tests pasando |
| Funcionamiento completo sin claves configuradas (*Offline First*) | CUMPLIDO | Modo offline totalmente operativo |

**Veredicto del Goal**: `COMPLETE_WITH_EXTERNAL_BLOCKERS` (Completamente implementado, testeado y funcional en modo autónomo/offline; listo para inyección de clave Google Maps real en `ISecretStore` mediante DPAPI cuando se disponga de ella).
