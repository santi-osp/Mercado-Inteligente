**COMPARADOR INTELIGENTE DE MERCADO**

Especificación de Producto

Visión, alcance, comportamiento funcional, reglas de negocio y criterios de aceptación.

| **Documento** | Product Specification                                                                    |
|---------------|------------------------------------------------------------------------------------------|
| **Versión**   | 1.0                                                                                      |
| **Fecha**     | 5 de octubre de 2026                                                                     |
| **Estado**    | Aprobado como especificación base para desarrollo                                        |
| **Alcance**   | Aplicación local en Windows · Medellín · D1/Éxito + Ara parcial · IA opcional OpenRouter |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Decisión de diseño</strong></p>
<p>Este documento elimina decisiones implícitas. Cuando exista una recomendación explícita, se considera el comportamiento predeterminado de V1 salvo que una evidencia técnica posterior obligue a cambiarla.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

# Contenido

1\. Resumen ejecutivo

2\. Visión y principios

3\. Alcance y no alcance

4\. Perfil de uso y contexto

5\. Modelo conceptual del producto

6\. Flujos principales

7\. Requisitos funcionales

8\. Reglas de comparación y matching

9\. Optimización de la cesta

10\. Ubicación y proximidad

11\. Inteligencia artificial opcional

12\. Frescura, confianza y trazabilidad

13\. Configuración

14\. Manejo de errores y modo degradado

15\. Privacidad y seguridad funcional

16\. Criterios de aceptación V1

17\. Roadmap y límites conocidos

# 1. Resumen ejecutivo

La aplicación será un comparador y optimizador de mercado local-first para Medellín. Su función no es vender productos, sino transformar precios, disponibilidad, equivalencias, ubicación y preferencias en decisiones de compra transparentes. La V1 correrá exclusivamente en el PC del usuario y deberá funcionar aun cuando la IA esté desactivada.

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Decisión de producto</strong></p>
<p>V1 = D1 + Éxito como integraciones de catálogo/precio; Ara como fuente parcial cuando exista información pública utilizable. La interfaz nunca simulará que todas las cadenas tienen la misma cobertura de datos.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

| **Decisión**         | **Predeterminado V1**                             | **Configurable**                                 |
|----------------------|---------------------------------------------------|--------------------------------------------------|
| Ejecución            | Local en Windows; acceso por localhost            | Puerto, autoarranque y rutas de datos            |
| Ciudad               | Medellín                                          | Arquitectura preparada para otras regiones       |
| Máximo de tiendas    | 2                                                 | 1–5 y “sin límite” en avanzado                   |
| Comparación          | Exacta + tamaños similares; sustitutos opcionales | Sí                                               |
| Tolerancia de tamaño | ±15%                                              | Estricto/estándar/flexible/personalizado         |
| IA                   | Opcional; solo ambigüedad/análisis                | Proveedor, modelo, funciones y presupuesto       |
| Ubicación            | Dirección o Google Maps URL                       | Perfiles de ubicación                            |
| Precio               | Siempre con fuente, alcance y fecha               | TTL y filtros                                    |
| Optimización         | Equilibrada                                       | Máximo ahorro, rápida, una tienda, personalizada |

# 2. Visión y principios

La experiencia debe reducir el esfuerzo mental de comparar mercados. La complejidad técnica se mantiene detrás de una UX simple, pero nunca se oculta la incertidumbre del dato.

## 2.1 Visión

Permitir que una persona convierta una lista de mercado en una recomendación accionable: qué comprar, en qué tienda, por qué, cuánto ahorra, cuánto debe desplazarse y qué tan confiables son los datos usados.

## 2.2 Principios no negociables

- Transparencia antes que falsa precisión: cada precio debe indicar fuente, contexto y frescura.

- IA opcional: ningún flujo esencial depende de un LLM.

- Preferir determinismo: EAN, atributos, cantidades y reglas preceden a fuzzy e IA.

- Comparar valor, no solo precio de etiqueta: usar precio por kg/L/unidad cuando corresponda.

- No castigar al usuario con configuración: defaults sensatos y modo avanzado separado.

- Evitar viajes inútiles: mostrar el ahorro marginal de agregar una tienda.

- Local-first: privacidad, bajo costo y control de datos.

- Degradación elegante: si un provider falla, la app continúa con caché y lo informa.

# 3. Alcance y no alcance

El alcance debe proteger la V1 de convertirse en un agregador universal antes de validar el valor del producto.

| **Incluido en V1**                                  | **No incluido en V1**                        |
|-----------------------------------------------------|----------------------------------------------|
| Búsqueda y comparación D1/Éxito; Ara parcial        | Marketplace nacional con cientos de usuarios |
| Lista de mercado y optimizador con máximo N tiendas | Compra/pago dentro de la app                 |
| Dirección/Google Maps URL; distancia y rutas        | Mapa interactivo embebido                    |
| Histórico de precios y frescura                     | Garantizar precio físico de góndola          |
| Overrides manuales de matching                      | Crowdsourcing multiusuario                   |
| OpenRouter opcional                                 | Entrenamiento de modelo propio               |
| Backups, exportación e importación local            | Cloud sync                                   |
| Panel de salud de providers                         | Apps nativas iOS/Android                     |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Límite crítico</strong></p>
<p>La app debe distinguir “precio online/regional observado” de “precio físico verificado”. No se permite mostrar una sucursal como poseedora de un precio físico exacto sin evidencia específica.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

# 4. Perfil de uso y contexto

V1 tiene un único usuario local. El diseño del dominio evita acoplar preferencias a constantes para que el producto pueda crecer, pero no se implementará un sistema de cuentas.

| **Contexto**          | **Decisión**                                               |
|-----------------------|------------------------------------------------------------|
| Dispositivo principal | PC Windows, navegador moderno o PWA instalada              |
| Red                   | No requiere acceso desde Internet                          |
| Persistencia          | SQLite + archivos RAW gzip + backups locales               |
| Sesiones              | Sin login; aplicación enlazada solo a loopback             |
| Uso típico            | Planificar mercado, revisar alternativas y decidir ruta    |
| Uso avanzado          | Corregir matches, auditar fuentes, configurar IA/TTL/pesos |

# 5. Modelo conceptual del producto

El usuario no debe percibir la complejidad de Product/SKU/Seller/Offer, pero el comportamiento funcional debe respetarla.

Cadena (Retailer)  
└─ Sucursal/Contexto logístico  
└─ Producto externo  
└─ SKU externo  
└─ Oferta (seller + precio + disponibilidad + contexto)  
  
Producto canónico  
└─ Variante canónica (marca + tipo + cantidad + unidad + atributos)  
└─ matches/overrides contra SKU externos

## 5.1 Estados de equivalencia

| **Estado**     | **Significado**                                         | **Tratamiento**                                  |
|----------------|---------------------------------------------------------|--------------------------------------------------|
| EXACT          | Misma variante o evidencia fuerte (preferentemente EAN) | Puede compararse directamente                    |
| SUBSTITUTE     | Producto diferente pero funcionalmente comparable       | Solo si usuario lo permite                       |
| CANDIDATE      | Posible coincidencia no confirmada                      | No entra como exacto; puede usar IA/confirmación |
| NOT_EQUIVALENT | No deben compararse como equivalentes                   | Bloqueado por regla/override                     |

# 6. Flujos principales

Los siguientes journeys definen qué debe sentirse simple aunque internamente exista lógica compleja.

## 6.1 Primer inicio

1.  La app valida base de datos, carpetas, migraciones y providers.

2.  Muestra un onboarding de máximo 4 pasos: ubicación, tiendas, IA opcional y preferencias básicas.

3.  El usuario puede omitir Google/IA y terminar el onboarding; la app sigue siendo usable.

4.  Se crea el perfil predeterminado “Equilibrado” y máximo de 2 tiendas.

5.  Se ejecuta una sincronización ligera o se ofrece “Actualizar ahora”.

## 6.2 Buscar y comparar un producto

6.  Buscar por texto, marca o EAN.

7.  Mostrar coincidencias exactas primero; luego tamaños similares y sustitutos en secciones separadas.

8.  Normalizar unidades y mostrar precio total + precio unitario.

9.  Permitir cambiar tolerancia o excluir marcas desde la misma pantalla.

10. Permitir añadir una variante a la lista de mercado.

## 6.3 Crear y optimizar un mercado

11. Agregar productos y cantidades.

12. Resolver alternativas permitidas por ítem.

13. Elegir máximo N tiendas y estrategia.

14. Calcular candidatos de tienda/producto sin IA.

15. Calcular rutas solo para los candidatos más prometedores.

16. Mostrar recomendación, alternativas y ahorro marginal por tienda.

17. Permitir sustituir manualmente una selección y recalcular.

# 7. Requisitos funcionales

Los requisitos están redactados para poder convertirse después en historias de usuario y pruebas de aceptación.

| **ID** | **Área**        | **Requisito**                                                                        |
|--------|-----------------|--------------------------------------------------------------------------------------|
| FR-001 | Inicio y salud  | La app debe abrir sin conexión a Internet y mostrar datos locales/caché disponibles. |
| FR-002 | Búsqueda        | Debe buscar por nombre, marca, EAN y términos parciales usando índice local.         |
| FR-003 | Comparación     | Debe separar exactos, tamaños similares y sustitutos.                                |
| FR-004 | Unidades        | Debe normalizar g/kg, ml/L, unidades y multipacks cuando los datos lo permitan.      |
| FR-005 | Precio unitario | Debe mostrar \$/kg, \$/L, \$/100g, \$/unidad u otra unidad relevante.                |
| FR-006 | Lista           | Debe permitir cantidades, notas, restricciones y preferencia por variante.           |
| FR-007 | Optimización    | Debe resolver mejor combinación con máximo N tiendas configurable.                   |
| FR-008 | Estrategias     | Debe ofrecer Máximo ahorro, Equilibrada, Menor recorrido y Una tienda.               |
| FR-009 | Ubicación       | Debe aceptar dirección escrita, coordenadas y enlaces Google Maps soportados.        |
| FR-010 | Rutas           | Debe calcular distancia/tiempo de candidatos y permitir abrir ruta en Google Maps.   |
| FR-011 | IA              | Debe poder habilitar/deshabilitar IA por función y elegir modelo OpenRouter.         |
| FR-012 | Overrides       | Debe permitir confirmar/rechazar equivalencias y darles prioridad futura.            |
| FR-013 | Histórico       | Debe conservar observaciones de precio y mostrar evolución básica.                   |
| FR-014 | Frescura        | Debe etiquetar precios como frescos, vencidos o desconocidos según TTL.              |
| FR-015 | Fuentes         | Debe mostrar provider, contexto, seller y timestamp cuando sean relevantes.          |
| FR-016 | Salud           | Debe mostrar providers Healthy/Degraded/Down y último sync.                          |
| FR-017 | Backups         | Debe crear y restaurar backups sin manipulación manual de SQLite.                    |
| FR-018 | Exportación     | Debe poder exportar configuración/lista/diagnóstico en formatos legibles.            |
| FR-019 | Actualización   | Debe permitir actualización manual por tienda/categoría/producto.                    |
| FR-020 | Degradación     | Un provider caído no debe bloquear búsqueda local ni optimización con caché.         |

# 8. Reglas de comparación y matching

El sistema prioriza precisión. Un falso positivo exacto es más dañino que no encontrar una coincidencia.

## 8.1 Orden de resolución

18. Override manual explícito.

19. EAN/GTIN válido compatible con marca/variante/tamaño.

20. Regla determinística fuerte: nombre canónico + marca + atributos + presentación compatible.

21. Aliases y normalización textual.

22. Fuzzy/FTS como generación de candidatos, nunca como prueba suficiente.

23. IA opcional para casos ambiguos con salida estructurada.

24. Confirmación manual cuando permanezca incertidumbre.

## 8.2 Tolerancia de tamaño

| **Perfil**         | **Rango**                  | **Uso**                              |
|--------------------|----------------------------|--------------------------------------|
| Estricto           | ±5%                        | Cuando la presentación importa mucho |
| Estándar (default) | ±15%                       | Comparación cotidiana                |
| Flexible           | ±30%                       | Maximizar alternativas               |
| Personalizado      | Valor definido por usuario | Casos específicos                    |

## 8.3 Multipacks

La cantidad total y el número de unidades deben conservarse por separado. Un “6 × 1 L” equivale a 6 L para precio por volumen, pero no debe perderse la condición de multipack porque promociones y conveniencia dependen de ella.

# 9. Optimización de la cesta

La app debe entregar una decisión explicable. Primero resuelve factibilidad y costo; luego incorpora desplazamiento y preferencias.

| **Estrategia**        | **Objetivo**                              | **Comportamiento**                                           |
|-----------------------|-------------------------------------------|--------------------------------------------------------------|
| Máximo ahorro         | Minimizar precio de productos             | Respeta máximo N; ignora comodidad salvo restricciones duras |
| Equilibrada (default) | Balance precio/ruta/paradas/preferencias  | Score recomendado 65/20/10/5 como punto inicial configurable |
| Menor recorrido       | Minimizar distancia/tiempo                | Acepta pagar más para evitar desplazamientos                 |
| Una tienda            | Resolver todo en una sola cadena/contexto | Prioriza completitud y luego precio                          |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Regla de transparencia</strong></p>
<p>Siempre mostrar el ahorro marginal de añadir otra tienda. Ejemplo: “La tercera tienda ahorra $3.200 adicionales y agrega 6,8 km”.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 9.1 Restricciones configurables

- Máximo de tiendas.

- Radio o recorrido máximo.

- Productos/marcas obligatorios o excluidos.

- Permitir o no sustitutos.

- Presupuesto opcional.

- Disponibilidad mínima/confiabilidad requerida.

- Ignorar precios vencidos o permitirlos con advertencia.

# 10. Ubicación y proximidad

La V1 utilizará Google Geocoding API y Routes API desde el backend local. No incluirá mapa dinámico embebido.

| **Entrada**           | **Tratamiento**                                                             |
|-----------------------|-----------------------------------------------------------------------------|
| Dirección escrita     | Geocodificar y guardar coordenadas + dirección normalizada                  |
| Google Maps URL corta | Resolver redirección y extraer coordenadas/place context cuando sea posible |
| Google Maps URL larga | Parsear coordenadas; geocodificar solo si hace falta                        |
| Coordenadas           | Usar directamente; reverse geocoding opcional                               |

Los perfiles de ubicación (“Casa”, “Trabajo”, etc.) se almacenan localmente. La geocodificación se realiza una vez y se reutiliza mientras la dirección no cambie.

# 11. Inteligencia artificial opcional

OpenRouter será el único proveedor habilitado en V1, pero la interfaz interna será genérica. El usuario configura su API key y el modelo desde Ajustes.

| **Función IA**        | **Default**         | **Disparador**                                |
|-----------------------|---------------------|-----------------------------------------------|
| Normalización ambigua | ON si IA habilitada | Reglas no logran extraer atributos críticos   |
| Evaluación de match   | ON                  | Candidato queda ambiguo después de reglas     |
| Sustitutos            | ON                  | Usuario solicita otras marcas o no hay exacto |
| Explicación de cesta  | ON                  | Resultado optimizado listo                    |
| Clasificación masiva  | OFF                 | Solo bajo acción explícita                    |
| Buscar/precios        | PROHIBIDO           | La IA nunca es fuente de precio/stock         |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Política de IA</strong></p>
<p>La salida de IA para datos estructurales debe usar JSON Schema estricto cuando el modelo lo soporte. Si falla la IA, el sistema continúa con la decisión determinística y muestra que el enriquecimiento no estuvo disponible.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 11.1 Control de costo

- Selección manual de modelo; no hardcodear un modelo específico.

- Lista de modelos obtenida de OpenRouter y cacheada.

- Máximo de candidatos por llamada (default 8).

- Máximo 2 llamadas IA paralelas.

- Presupuesto mensual de advertencia configurable (default sugerido: USD 5); hard stop opcional.

- Registro local de tokens/costo estimado por feature y modelo.

# 12. Frescura, confianza y trazabilidad

Todo dato externo debe ser auditable desde la interfaz sin abrumar al usuario.

| **Dato**                  | **TTL default** | **Acción al vencer**                                       |
|---------------------------|-----------------|------------------------------------------------------------|
| Precio/oferta             | 6 h             | Marcar stale; refrescar bajo demanda o al optimizar        |
| Disponibilidad/stock      | 2 h             | Refrescar si afecta una decisión                           |
| Producto/metadatos        | 7 días          | Actualizar en sincronización                               |
| Catálogo/búsqueda externa | 24 h            | Usar local; refresco incremental                           |
| Sucursales                | 7 días          | Actualizar cuando se solicite ubicación o manualmente      |
| Ruta                      | 24 h            | Recalcular si cambian origen, tiendas o modo de transporte |

## 12.1 Ámbito de precio

- ONLINE_GENERIC: precio online sin contexto geográfico específico.

- CITY/REGION: precio contextualizado por ciudad/región/fulfillment.

- STORE: precio asociado técnicamente a una tienda/contexto específico, sin afirmar góndola física.

- PHYSICAL_VERIFIED: precio contrastado manualmente en tienda física.

- UNKNOWN: no se puede inferir el ámbito con suficiente confianza.

# 13. Configuración

La configuración se divide en Básica y Avanzada para evitar que la flexibilidad deteriore la experiencia.

| **Básica**                | **Avanzada**                               |
|---------------------------|--------------------------------------------|
| Ubicación predeterminada  | Geocoding/routing provider y cuotas        |
| Máximo de tiendas         | TTL por tipo de dato                       |
| Estrategia predeterminada | Pesos exactos del score                    |
| Tolerancia de tamaño      | Timeouts/reintentos por provider           |
| Permitir sustitutos       | Retención RAW y backups                    |
| IA ON/OFF + modelo        | Presupuesto IA y features individualizadas |
| Tiendas habilitadas       | Diagnóstico/logging/exportaciones          |

# 14. Manejo de errores y modo degradado

La aplicación debe explicar qué ocurre y conservar utilidad parcial.

| **Situación**             | **Comportamiento esperado**                                             |
|---------------------------|-------------------------------------------------------------------------|
| Sin Internet              | Búsqueda local, históricos y listas disponibles; refresco deshabilitado |
| D1 caído                  | Usar último precio conocido con advertencia                             |
| Éxito 502/504             | Reintento limitado; circuit breaker; caché                              |
| Ara sin dato              | Mostrar “sin precio disponible”, nunca \$0                              |
| Google no configurado     | Usar distancia lineal si hay coordenadas; ocultar ruta/tiempo           |
| OpenRouter no configurado | Ocultar acciones IA; todo lo esencial sigue funcionando                 |
| Match ambiguo             | No sustituir silenciosamente; pedir revisión o IA                       |
| DB dañada                 | Bloquear escritura, ofrecer restaurar backup y exportar diagnóstico     |

# 15. Privacidad y seguridad funcional

Los datos permanecen en el PC. V1 no envía listas, preferencias ni ubicaciones a un servidor propio.

- El backend escucha solo en loopback (127.0.0.1).

- OpenRouter recibe únicamente el contexto mínimo de los candidatos que requieren análisis.

- Google recibe las consultas necesarias de geocoding/routing cuando la función se usa.

- API keys se protegen con mecanismos de Windows y nunca se almacenan en texto plano ni en Angular.

- Logs deben redactar secretos, cookies, tokens y payloads sensibles.

- RAW se conserva por política configurable y puede purgarse sin borrar histórico normalizado.

# 16. Criterios de aceptación V1

V1 se considera lista cuando los siguientes comportamientos funcionan de punta a punta con datos reales y tests automatizados.

25. Buscar al menos D1 y Éxito desde índice local y actualizar datos externos.

26. Comparar un producto exacto mediante EAN cuando exista.

27. Mostrar alternativas de tamaño con precio unitario normalizado.

28. Crear una cesta de al menos 30 ítems y optimizarla para máximo 1, 2 y 3 tiendas.

29. Mostrar al menos cuatro estrategias y explicar diferencias.

30. Aceptar una dirección o enlace Google Maps y calcular ruta cuando Google esté configurado.

31. Funcionar completamente con IA deshabilitada.

32. Habilitar OpenRouter y resolver un caso ambiguo con salida estructurada.

33. Guardar un override manual y respetarlo en futuras comparaciones.

34. Mostrar frescura, ámbito y fuente del precio.

35. Sobrevivir a provider caído usando caché sin romper la interfaz.

36. Crear y restaurar un backup válido.

37. Pasar contract tests de D1/Éxito y pruebas de integridad SQLite.

# 17. Roadmap y límites conocidos

La implementación debe seguir el riesgo técnico, no el orden visual.

| **Fase**                | **Resultado**                                                            |
|-------------------------|--------------------------------------------------------------------------|
| 0 · Fundación           | Repositorio, dominio, SQLite v2, configuración, secretos, logging, tests |
| 1 · D1                  | Provider real + sync + contract tests                                    |
| 2 · Éxito               | Provider real + seller classification + contract tests                   |
| 3 · Catálogo y matching | Canonicalización, FTS, equivalencias, overrides                          |
| 4 · Cesta y optimizador | Máximo N, estrategias, explicación determinística                        |
| 5 · Ubicación           | Google Geocoding/Routes + perfiles + cache                               |
| 6 · UI completa         | Dashboard, búsqueda, cesta, resultados, settings, health                 |
| 7 · OpenRouter          | IA opcional con JSON Schema, costos y controles                          |
| 8 · Endurecimiento      | Backups, métricas, pruebas físicas, performance, accesibilidad           |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Límites aceptados</strong></p>
<p>Ara seguirá parcial hasta que exista una fuente utilizable; los precios físicos exactos se etiquetarán como no verificados salvo contraste real; el nombre comercial del producto puede definirse después sin afectar la arquitectura.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>
