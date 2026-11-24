# GOAL 15 — PRODUCT DISCOVERY, CATÁLOGO ECOMMERCE, CRUD DE LISTAS, ASISTENTE IA Y UX/UI FINAL

Trabaja sobre el proyecto existente en:

`C:\Users\santi\Documents\0 REPOSITORIOS\Mercado_Inteligente`

## REGLA DE PRECEDENCIA

Este documento **REEMPLAZA cualquier versión anterior de GOAL 15**.

Usa este documento como fuente de verdad para esta fase.

NO reinicies el proyecto desde cero.

NO agregues supermercados nuevos.

NO cambies branding ni paleta aprobada.

NO sobreingenierices.

---

# 1. OBJETIVO GENERAL

Convertir Mercado Inteligente en una aplicación de uso real que combine:

1. catálogo visual de productos tipo ecommerce/comparador;
2. búsqueda independiente de listas;
3. consulta de productos locales ya sincronizados;
4. consulta online D1 + Éxito cuando haga falta;
5. filtros, ordenamiento y navegación práctica;
6. links reales hacia las páginas de los productos;
7. CRUD completo de listas de compra;
8. IA flotante tipo chatbot;
9. español completo;
10. componentes visuales coherentes;
11. UX clara para usuario no técnico;
12. OmniRouter configurable;
13. Google opcional;
14. acceso directo local;
15. first-run funcional.

La aplicación debe sentirse como un **producto de consumo**, no como panel administrativo.

---

# 2. PRINCIPIO CENTRAL — PRODUCT DISCOVERY ≠ LISTAS

Actualmente puede ocurrir:

`Agregar producto a lista`

↓

`Sincronizar lista`

↓

`Producto entra a BD`

↓

`Ahora sí aparece en Buscar`

Eso NO es aceptable como arquitectura principal.

Debe existir:

## DESCUBRIR

`Buscar productos`

independientemente de cualquier lista.

## PLANEAR

`Agregar productos encontrados a una lista`

solo si el usuario quiere organizar una compra.

Una lista NO puede ser requisito para descubrir productos.

---

# 3. CASO CRÍTICO

Con:

- DB nueva;
- cero listas;
- cero productos previamente guardados;

el usuario debe poder:

1. abrir Buscar;
2. escribir `Queso`;
3. pulsar Buscar;
4. consultar D1 y Éxito;
5. recibir productos;
6. verlos visualmente;
7. comparar;
8. abrir página del producto;
9. opcionalmente agregar alguno a una lista.

Sin crear lista previamente.

---

# 4. MODO CATÁLOGO CUANDO LA BD YA ESTÁ SINCRONIZADA

Cuando la base local tenga productos suficientes, Buscar debe funcionar también como **catálogo local navegable**.

No obligar al usuario a consultar proveedores cada vez.

Flujo:

`DB sincronizada`

↓

`Buscar "queso"`

↓

`FTS/local catalog`

↓

`mostrar resultados inmediatamente`

↓

si datos están frescos:
no hacer llamada remota innecesaria

↓

si están stale/outdated:
mostrar local + refrescar en segundo plano

---

# 5. REFERENCIA VISUAL PARA CATÁLOGO

Tomar como referencia de UX la captura proporcionada:

- filtros laterales;
- contador de resultados;
- ordenamiento;
- selector tarjetas/lista;
- grid de productos;
- imagen grande;
- nombre;
- unidad/presentación;
- precio;
- precio anterior;
- descuento/promoción;
- tienda;
- acciones claras.

NO copiar branding de Éxito.

Mantener identidad visual de Mercado Inteligente.

---

# 6. ESTRUCTURA DESKTOP DE BUSCAR

Layout recomendado:

```text
┌───────────────────────────────────────────────────────────┐
│ MINI HERO + SEARCH BAR                                    │
├───────────────┬───────────────────────────────────────────┤
│ FILTROS       │  741 resultados     Ordenar   Vista      │
│               │                                           │
│ Tienda        │  [card] [card] [card] [card]              │
│ Categoría     │                                           │
│ Marca         │  [card] [card] [card] [card]              │
│ Precio        │                                           │
│ Disponibilidad│                                           │
│ Presentación  │                                           │
└───────────────┴───────────────────────────────────────────┘
```

Sidebar de filtros sticky cuando sea razonable.

---

# 7. MINI HERO — BUSCAR

Título:

`Encuentra productos`

Texto:

`Busca en D1, Éxito y tus datos guardados. Compara precios y abre directamente el producto en la tienda.`

Search bar integrada.

Placeholder:

`Busca arroz, leche, queso, detergente…`

---

# 8. PRODUCT SEARCH ORCHESTRATOR

Crear/consolidar:

`ProductSearchOrchestrator`

Debe funcionar SIN Basket.

Responsabilidades:

1. validar query;
2. buscar catálogo local;
3. evaluar frescura;
4. consultar providers si hace falta;
5. normalizar;
6. persistir;
7. actualizar histórico;
8. deduplicar;
9. agrupar equivalencias seguras;
10. devolver ViewModels optimizados para UI.

---

# 9. SEARCH API

Endpoint conceptual:

`POST /api/v1/search/products`

Request:

```text
query
selectedRetailers?
locationProfileId?
forceRefresh?
page
pageSize
sort
filters
```

NO requerir:

`basketId`

---

# 10. RESPUESTA

Cada resultado debe poder contener:

- ProductId
- ExternalSkuId
- Name
- Brand
- Presentation
- ImageUrl
- Retailer
- RetailerLogo si existe
- Price
- ListPrice
- Discount
- Promotion
- UnitPrice
- Availability
- Freshness
- PriceScope
- UpdatedAt
- ProductUrl
- MatchGroup
- IsMarketplace
- SellerDisplay
- SourceStatus

---

# 11. URL REAL DEL PRODUCTO — OBLIGATORIO

Cada provider debe intentar conservar:

`ProductUrl`

o equivalente.

D1:

link hacia página real del producto si está disponible.

Éxito:

link real.

No fabricar URLs inseguras.

Si provider entrega canonical URL:

persistirla.

---

# 12. CTA PRINCIPAL DE PRODUCTCARD

NO usar botón principal estilo:

`Agregar al carrito`

La aplicación no es el ecommerce que vende.

CTA principal:

`Ver en tienda`

Debe:

- abrir la URL real;
- nueva pestaña;
- `rel="noopener noreferrer"`;
- no perder estado de Mercado Inteligente.

---

# 13. ACCIÓN SECUNDARIA

Puede existir secundariamente:

`Agregar a lista`

pero visualmente menos dominante.

Ejemplo:

```text
[ Ver en tienda ]
+ Agregar a lista
```

o menú:

`⋯`

→ Agregar a lista

→ Comparar

→ Ver historial

---

# 14. PRODUCT CARD

Debe incluir:

- imagen;
- badges;
- marca;
- nombre;
- presentación;
- precio por unidad;
- precio actual destacado;
- precio anterior tachado;
- descuento;
- promoción;
- tienda;
- disponibilidad;
- frescura;
- CTA Ver en tienda;
- acción Agregar a lista;
- acción Comparar.

---

# 15. BADGES

Ejemplos:

`-20%`

`Precio promocional`

`Envío gratis`

`Precio para tu zona`

`Disponible`

No mostrar información que provider no haya verificado.

---

# 16. IMÁGENES

Usar ImageUrl del retailer.

Implementar:

- lazy loading;
- placeholder;
- fallback;
- aspect ratio consistente;
- no broken images.

---

# 17. GRID

Desktop:

preferencia 4 tarjetas en resoluciones similares a la captura.

Pantallas grandes:

4–5.

Tablet:

2–3.

Mobile:

1–2.

---

# 18. VISTA GRID / LISTA

Control:

`Vista:`

[icono tarjetas] [icono lista]

Persistir preferencia local.

---

# 19. CONTADOR

Mostrar:

`741 resultados`

o:

`24 productos encontrados`

No exagerar conteo si paginación/provider entrega total desconocido.

---

# 20. FILTROS LATERALES

Como mínimo:

## Tienda

- Todas
- D1
- Éxito
- Ara cuando exista información

## Categoría

## Subcategoría

## Marca

## Precio

## Disponibilidad

## Presentación

## Promociones

Generar dinámicamente cuando tenga sentido.

---

# 21. FILTROS ACTIVOS

Arriba del sidebar:

chips:

`Aseo del hogar`

`Detergentes`

`Ariel`

Acción:

`Limpiar filtros`

---

# 22. FILTROS UX

Desktop:

sidebar sticky.

Mobile:

botón:

`Filtros`

abre drawer/bottom sheet.

---

# 23. ORDENAMIENTO

`Ordenar por`

- Relevancia
- Menor precio
- Mayor precio
- Precio por unidad
- Más recientes
- Mayor descuento

---

# 24. LOCAL FIRST EN BÚSQUEDA

Si DB ya está sincronizada:

resultados deben aparecer casi inmediatamente.

No esperar D1/Éxito para renderizar.

---

# 25. REFRESH EN SEGUNDO PLANO

Cuando resultados locales existan pero estén viejos:

mostrar:

`Mostrando datos guardados mientras actualizamos precios…`

Luego refrescar cards.

---

# 26. BÚSQUEDA REMOTA SOLO CUANDO HACE FALTA

No consultar providers en cada tecla.

Providers únicamente:

- Enter;
- botón Buscar;
- refresh explícito;
- política de stale.

---

# 27. SUGERENCIAS

Mientras escribe:

usar solo FTS/local.

Ejemplo:

`que`

→ Queso mozzarella

→ Queso crema

→ Queso campesino

---

# 28. RESULTADOS PARCIALES

D1 y Éxito concurrentes cuando sea seguro.

Si D1 responde primero:

mostrar D1.

Cuando llegue Éxito:

integrarlo.

---

# 29. ERROR POR TIENDA

D1 falla:

`D1 no respondió en este momento.`

Éxito sigue visible.

No error global.

---

# 30. SEARCH SIN LISTAS

Test obligatorio:

`Search_WorksWithoutAnyShoppingList`

DB:

- cero Basket;
- cero BasketItem.

Query:

`Queso`.

PASS si devuelve productos.

---

# 31. CATÁLOGO SOBREVIVE A LISTAS

Buscar y persistir producto es global.

Eliminar lista NO elimina producto.

Test:

`DeletingBasketDoesNotDeleteSearchedProductCatalog`

---

# 32. DETALLE DE PRODUCTO

Al pulsar tarjeta o acción:

abrir panel/modal/página.

Mostrar:

- imagen;
- nombre;
- marca;
- presentación;
- todas las tiendas compatibles;
- precio;
- unit price;
- promoción;
- disponibilidad;
- actualización;
- historial;
- similares;
- Ver en tienda;
- Agregar a lista.

---

# 33. COMPARAR SIN LISTA

Comparar producto NO requiere Basket.

Desde Buscar:

`Comparar`

↓

vista de comparación.

---

# 34. MATCHING CONSERVADOR

Mantener:

Manual override

→ EAN

→ atributos

→ normalización

→ fuzzy candidate

→ IA opcional

Nunca fuzzy solo = identidad exacta.

---

# 35. CRUD COMPLETO DE LISTAS

`Mi mercado` debe soportar CRUD real.

### Crear
### Leer
### Editar
### Eliminar

Actualmente eliminar falla; corregirlo obligatoriamente.

---

# 36. HERO — MI MERCADO

Título:

`Tus listas de compras`

Texto:

`Organiza compras diferentes y encuentra la mejor combinación de tiendas para cada una.`

CTA:

`Nueva lista`

---

# 37. VISUAL DE LISTAS

No usar solo select.

Cards:

```text
Mercado semanal
12 productos
Estimado $185.400
Actualizado hace 20 min

[Abrir] [...]
```

Menú:

- Renombrar
- Duplicar
- Eliminar

---

# 38. CREAR

Formulario:

`Nombre`

Placeholder:

`Ej. Mercado semanal`

Opcional:

`Descripción`

---

# 39. EDITAR

Permitir:

- nombre;
- descripción;
- preferencias si aplican.

No perder items.

---

# 40. DELETE REAL

Endpoint:

`DELETE /api/v1/baskets/{id}`

o equivalente.

Eliminar correctamente relaciones dependientes.

---

# 41. QUÉ BORRAR

Al borrar lista:

BORRAR:

- Basket
- BasketItems
- configuración exclusiva

NO BORRAR:

- catálogo;
- productos externos;
- SKU;
- Offers;
- PriceObservations;
- histórico global;
- MatchOverrides.

---

# 42. CONFIRMACIÓN DELETE

Modal:

## ¿Eliminar “Mercado semanal”?

`Se eliminará esta lista y los productos agregados a ella. El catálogo y el historial de precios permanecerán disponibles.`

`Cancelar`

`Eliminar lista`

---

# 43. LISTA ACTIVA ELIMINADA

Limpiar:

- activeBasketId;
- localStorage;
- frontend state.

Seleccionar otra si existe.

Si ninguna:

empty state.

---

# 44. CRUD ITEMS

Permitir:

- agregar;
- cambiar cantidad;
- quitar;
- sustitutos on/off cuando corresponda.

---

# 45. AGREGAR DESDE PRODUCTCARD

Pulsar:

`Agregar a lista`

↓

modal:

`¿A qué lista quieres agregarlo?`

Lista disponibles.

`+ Crear nueva lista`

---

# 46. SI NO HAY LISTAS

No impedir búsqueda.

Al intentar agregar:

`Todavía no tienes listas.`

`Crear una lista`

`Cancelar`

---

# 47. EMPTY LISTAS

## Aún no tienes listas

`Puedes buscar y comparar productos sin crear ninguna. Crea una lista cuando quieras organizar una compra.`

CTA:

`Buscar productos`

CTA secundaria:

`Nueva lista`

---

# 48. LISTA VACÍA

`Tu lista está vacía`

`Busca productos y agrégalos cuando quieras.`

CTA:

`Buscar productos`

---

# 49. SEARCH → LIST

Buscar no crea lista.

Agregar sí.

Esta separación debe quedar documentada.

---

# 50. LIST SYNC

`Actualizar precios`

solo actualiza items de lista.

No es mecanismo de descubrimiento.

---

# 51. ASISTENTE IA FLOTANTE — NUEVO REQUISITO

Crear botón flotante abajo a la derecha.

Posición:

`fixed`

bottom/right con safe spacing.

Visual:

- icono IA/chat;
- badge opcional;
- accesible;
- no tapar acciones.

Tooltip:

`Asistente de compras`

---

# 52. FAB IA

Nombre conceptual:

`ShoppingAssistantFab`

Al pulsar:

abrir panel lateral/drawer/chat.

Desktop:

drawer derecho.

Mobile:

bottom sheet/full-screen.

---

# 53. HERO DEL CHAT

Título:

`Asistente de compras`

Texto:

`Pregúntame cuál producto conviene más, cómo ahorrar o qué alternativas puedes elegir.`

---

# 54. IA CONTEXTUAL

El asistente debe conocer, cuando el usuario lo permita:

- búsqueda actual;
- productos visibles;
- filtros;
- listas actuales;
- items de lista activa;
- ofertas;
- precios;
- unit prices;
- promociones;
- frescura.

NO enviar toda la DB.

---

# 55. EJEMPLOS DE PREGUNTAS

Chips sugeridos:

`¿Cuál queso conviene más?`

`¿Cuál tiene mejor precio por kilo?`

`¿Qué opción comprarías tú?`

`¿Hay una presentación más económica?`

`¿Qué puedo sustituir para ahorrar?`

`¿Me conviene comprar todo en una sola tienda?`

`¿Qué productos de mi lista están caros?`

---

# 56. IA EN PÁGINA BUSCAR

Si query actual:

`Queso`

el asistente debe poder responder usando esos resultados.

Ejemplo:

Usuario:

`¿Cuál tiene mejor relación precio/cantidad?`

IA:

analiza cards actuales.

---

# 57. IA EN LISTA

En Mi mercado:

`¿Cómo puedo gastar menos?`

Debe poder analizar lista activa.

---

# 58. IA EN COMPARAR

`¿Cuál de estos dos conviene?`

Usar comparación actual.

---

# 59. IA NO INVENTA PRECIOS

Nunca permitir que modelo sea fuente de:

- precio;
- disponibilidad;
- promoción;
- tienda;
- URL.

La IA debe recibir esos valores desde dominio.

---

# 60. RESPUESTA CON EVIDENCIA

Respuesta IA debería poder decir:

`Te conviene X porque cuesta $18.900 por 450 g ($42.000/kg), mientras Y cuesta...`

Datos deben venir del sistema.

---

# 61. LINKS EN CHAT

Cuando recomiende producto:

mostrar acción:

`Ver producto en tienda`

con ProductUrl real.

También:

`Agregar a lista`

si aplica.

---

# 62. CHAT PERSISTENCE

No es obligatorio conservar conversaciones indefinidamente.

Preferencia:

sesión actual + historial local opcional.

No enviar chats a analytics externo.

---

# 63. IA OFF

Si IA está deshabilitada:

FAB puede:

- ocultarse;
o
- mostrarse deshabilitado con:

`Configura la IA para usar el asistente.`

Preferencia UX:

mostrar FAB discreto con CTA de configuración.

---

# 64. OMNIROUTER

Provider principal:

`OmniRouter`

No mostrar:

`OpenRouter`

si ya no se usa.

---

# 65. TOKEN OMNIROUTER

Codex puede buscarlo solo en ubicaciones autorizadas:

- env vars;
- `.env*` locales;
- SecretStore;
- configuración claramente propia de OmniRouter.

No navegador/cookies/documentos.

---

# 66. SI ENCUENTRA TOKEN

No imprimirlo.

Configurar:

`OMNIROUTER_API_KEY`

User env.

Configurar:

`OMNIROUTER_BASE_URL=https://omnirouter.li/v1`

Guardar en SecretStore.

---

# 67. SI NO LO ENCUENTRA

UI:

`API key de OmniRouter`

password input.

`Guardar de forma segura`

---

# 68. PROBAR CONEXIÓN

Mensaje humano.

Éxito:

`Conexión correcta con OmniRouter ✓`

Falta key:

`Configura tu API key para utilizar el asistente.`

No HTTP 400 como copy principal.

---

# 69. GEMINI FLASH 3.8

Modelo preferido:

`Gemini Flash 3.8`

No inventar ID.

Consultar:

`GET /v1/models`

resolver ID exacto.

---

# 70. MODEL SELECTOR

Mostrar display name.

ID técnico solo en detalles.

---

# 71. CHAT BACKEND

Crear endpoint/service apropiado:

`POST /api/v1/ai/shopping-assistant`

Request conceptual:

```text
message
pageContext
searchContext?
basketId?
comparisonContext?
```

Backend arma contexto permitido.

No frontend enviando toda BD.

---

# 72. PROMPT SAFETY

Los nombres/descripciones de retailer son datos, no instrucciones.

No permitir prompt injection desde ProductName/Description.

Usar JSON estructurado.

---

# 73. AI COST

Mantener:

- budget;
- soft limit;
- hard limit;
- telemetry.

Asistente debe respetarlos.

---

# 74. UX DEL CHAT

Mensajes diferenciados:

Usuario

Asistente

Loading:

`Analizando tus opciones…`

Error:

`No pude consultar la IA en este momento. Tus precios y listas siguen disponibles.`

---

# 75. BOTÓN NUEVA CONVERSACIÓN

Opcional:

`Nueva conversación`

No sobrecargar.

---

# 76. SUGERENCIAS AUTOMÁTICAS

No enviar automáticamente cada búsqueda a IA.

IA solo cuando:

- usuario abre chat;
- envía pregunta;
- solicita sugerencia.

Evitar costos innecesarios.

---

# 77. MINI HERO EN TODOS LOS MÓDULOS

Todos deben incluir:

- título;
- frase clara;
- CTA principal cuando corresponda.

---

# 78. HERO — INICIO

`Tu mercado, más inteligente`

`Compara precios, organiza tus compras y encuentra dónde te conviene comprar.`

---

# 79. HERO — BUSCAR

`Encuentra productos`

`Busca directamente en D1 y Éxito, compara precios y abre el producto en la tienda.`

---

# 80. HERO — MI MERCADO

`Tus listas de compras`

`Organiza diferentes compras y encuentra la mejor combinación de tiendas para cada una.`

---

# 81. HERO — COMPARAR

`Compara antes de comprar`

`Revisa precios y presentaciones para elegir la opción que más te conviene.`

---

# 82. HERO — HISTORIAL

`Cómo han cambiado los precios`

`Consulta precios anteriores y cuándo fue actualizado cada producto.`

---

# 83. HERO — TIENDAS

Renombrar:

`Fuentes`

→ `Tiendas`

`Consulta qué tiendas están disponibles y cuándo obtuvimos precios por última vez.`

---

# 84. HERO — AJUSTES

`Personaliza Mercado Inteligente`

`Configura ubicación, inteligencia artificial y preferencias de comparación.`

---

# 85. TODO EN ESPAÑOL

Auditar strings.

No mostrar:

- Provider
- Scope
- Context
- Fallback
- Cache
- Sync
- Raw
- Healthy
- Stale
- Seller
- Store
- Budget
- Timeout
- Model
- Enabled
- Live
- Health
- Basket

---

# 86. TERMINOLOGÍA

Basket → Lista

Provider → Tienda

Sync → Actualizar precios

Cache → Datos guardados

Health → Estado del sistema

Model → Modelo de IA

Timeout → Tiempo máximo de espera

---

# 87. PRICE SCOPE

ONLINE_GENERIC:

`Precio online`

FULFILLMENT_REGION:

`Precio online para tu zona`

PHYSICAL_VERIFIED:

`Precio verificado en tienda`

---

# 88. STOCK

IN_STOCK:

`Disponible`

OUT_OF_STOCK:

`Agotado`

UNKNOWN:

`Disponibilidad no confirmada`

NOT_SOLD:

`No disponible`

---

# 89. DESIGN SYSTEM

Corregir todos:

- inputs;
- checkbox;
- selects;
- textarea;
- radio;
- switches;
- buttons.

---

# 90. INPUTS

Estados:

- normal;
- hover;
- focus;
- disabled;
- error;
- success.

Unificar:

- altura;
- padding;
- border;
- radius;
- focus ring.

---

# 91. CHECKBOXES

No usar checkbox nativo visual sin diseño.

Personalizar.

Label clickeable.

Focus visible.

---

# 92. SWITCHES

Para:

- IA;
- Google;
- marketplace;
- settings booleanos.

---

# 93. SELECT

Diseñados acorde al resto.

---

# 94. BUTTONS

Variantes:

- primary;
- secondary;
- tertiary;
- danger;
- ghost;
- icon.

---

# 95. MODALS

Reutilizable para:

- create list;
- delete;
- list picker;
- product details;
- confirmations.

---

# 96. TOASTS

Ejemplos:

`Lista eliminada`

`Producto agregado`

`Precios actualizados`

`D1 no respondió; mostramos datos guardados.`

---

# 97. LOADING

Skeleton product cards.

Spinner en botones.

No pantalla congelada.

---

# 98. EMPTY STATES

Diseñados y explicativos.

---

# 99. PRIMERA EJECUCIÓN

Si DB vacía:

`Bienvenido a Mercado Inteligente`

`Busca cualquier producto. Consultaremos las tiendas disponibles por ti.`

CTA:

`Buscar mi primer producto`

---

# 100. DATABASE RESET / REBUILD — AUTORIZACIÓN

Si para aplicar correctamente:

- schema;
- FTS;
- ProductUrl;
- search catalog;
- migrations;
- indexes;
- constraints;

es técnicamente mejor reconstruir la BD de desarrollo/local actual:

ESTÁ AUTORIZADO.

Pero seguir reglas.

---

# 101. ANTES DE RESET

Obligatorio:

1. backup;
2. integrity check;
3. guardar settings relevantes;
4. guardar secretos aparte;
5. documentar motivo.

No resetear simplemente para evitar crear migración.

---

# 102. RESET SOLO SI ES NECESARIO

Preferencia:

migración compatible.

Reset solo cuando:

- DB es todavía entorno personal/dev;
- migrar produce riesgo/deuda innecesaria;
- los datos actuales pueden volver a sincronizarse.

---

# 103. NO PERDER SECRETOS

Reset DB no debe eliminar:

- OmniRouter token si SecretStore está separado;
- configuración segura.

Si depende de DB:

export/import seguro.

---

# 104. DESPUÉS DEL RESET

Ejecutar:

- migrations;
- seed estructural;
- FTS setup;
- integrity check;
- sync inicial según estrategia.

---

# 105. SINCRONIZACIÓN DE CATÁLOGO

Crear/ajustar mecanismo que permita llenar base sin listas.

Puede ser:

`CatalogSync`

separado de:

`BasketSync`.

---

# 106. CATALOGSYNC

Responsabilidad:

- descubrir/cargar catálogo;
- actualizar productos;
- actualizar offers;
- actualizar imágenes/URLs;
- no crear BasketItems.

---

# 107. NO DESCARGAR TODO SIN CONTROL

No necesariamente sincronizar catálogo completo de todas las tiendas si eso es costoso.

Puede ser:

- search-driven catalog;
- categorías bajo demanda;
- catálogo progresivo;
- productos frecuentes.

Documentar estrategia.

---

# 108. SEARCH-DRIVEN CATALOG

Buscar algo nuevo:

provider search

↓

guardar

↓

desde ahí queda localmente consultable.

Esto es perfectamente válido.

---

# 109. CATEGORÍAS

Cuando datos provider soporten categorías:

persistirlas.

Permitir navegación:

`Aseo`

`Despensa`

`Lácteos`

etc.

Pero no bloquear Goal si retailer no expone navegación fiable.

---

# 110. PRODUCT URL PERSISTENCE

Agregar/migrar campo si falta.

Debe sobrevivir reinicio.

Actualizar cuando provider cambie URL.

---

# 111. LINKS SEGUROS

Solo:

`http/https`

Validar.

No javascript/data URLs.

---

# 112. CRUD BACKEND LISTAS

Garantizar endpoints equivalentes:

GET `/baskets`

GET `/baskets/{id}`

POST `/baskets`

PATCH `/baskets/{id}`

DELETE `/baskets/{id}`

Items CRUD.

---

# 113. DELETE TEST

`DeleteBasket_RemovesBasketAndItems_ButPreservesCatalogAndPriceHistory`

---

# 114. UI DELETE TEST

`DeletingActiveList_UpdatesUiWithoutReload`

---

# 115. SEARCH TEST

`Search_WorksWithoutAnyShoppingList`

---

# 116. EMPTY DB TEST

`Search_WhenDatabaseIsEmpty_QueriesEnabledRetailers`

---

# 117. PARTIAL PROVIDER TEST

`Search_D1Failure_DoesNotHideExitoResults`

---

# 118. LINK TEST

`SearchResult_ContainsValidRetailerProductUrl`

---

# 119. CATALOG TEST

`DeletingBasket_DoesNotRemoveProductDiscoveryResults`

---

# 120. CHAT TEST

`ShoppingAssistant_UsesVisibleSearchResultsWithoutInventingPrices`

---

# 121. CHAT IA OFF TEST

`ShoppingAssistant_WhenAiDisabled_ShowsConfigurationState`

---

# 122. PLAYWRIGHT — CERO LISTAS

1. DB vacía;
2. cero listas;
3. Buscar;
4. Queso;
5. resultados;
6. cards;
7. Ver en tienda visible;
8. cero listas todavía.

---

# 123. PLAYWRIGHT — CATÁLOGO LOCAL

Después primera búsqueda:

1. reiniciar;
2. buscar Queso nuevamente;
3. resultados locales aparecen rápido;
4. no dependen de lista.

---

# 124. PLAYWRIGHT — LISTA

Desde ProductCard:

1. Agregar a lista;
2. crear lista;
3. verificar;
4. renombrar;
5. cambiar cantidad;
6. eliminar;
7. catálogo permanece.

---

# 125. PLAYWRIGHT — CHAT

Con IA mock:

1. buscar Queso;
2. abrir FAB;
3. preguntar:
   `¿Cuál tiene mejor precio por kilo?`
4. asistente recibe contexto;
5. responde con datos visibles;
6. link Ver en tienda funcional.

---

# 126. LIVE SEARCH

Si red disponible:

`Queso`

D1 + Éxito reales.

Sin Basket.

Guardar evidencia.

---

# 127. OMNIROUTER LIVE

Si token disponible:

- `/v1/models`;
- Gemini Flash 3.8 exact ID;
- pregunta mínima;
- no exponer secreto.

---

# 128. GOOGLE OPCIONAL

Nada de búsqueda/listas/chat básico depende de Google.

---

# 129. FAB RESPONSIVE

Desktop:

abajo derecha.

Mobile:

respetar navegación/bottom safe-area.

No tapar botón flotante de otros módulos.

---

# 130. CHAT ACCESSIBILITY

aria-label:

`Abrir asistente de compras`

Escape cierra drawer.

Keyboard usable.

---

# 131. RESPONSIVE GLOBAL

Probar:

1920

1440

1280

tablet

mobile.

---

# 132. SCREENSHOTS

Guardar:

`docs/development/screenshots/goal-15/`

Mínimo:

1. catálogo desktop;
2. filtros;
3. resultados;
4. ProductCard;
5. product detail;
6. Mi mercado;
7. delete modal;
8. sin listas;
9. chatbot cerrado;
10. chatbot abierto;
11. respuesta IA;
12. Ajustes IA;
13. mobile search;
14. mobile chat.

---

# 133. COPY REVIEW

Recorrer:

Inicio

Buscar

Mi mercado

Comparar

Historial

Tiendas

Ajustes

Chat IA.

Corregir inglés/tecnicismos.

---

# 134. SETTINGS IA

Reorganizar.

Mostrar:

## Inteligencia artificial

`Usa IA para ayudarte a comparar y tomar decisiones. Es opcional.`

Estado.

Modelo.

API Key.

Límite mensual.

Funciones.

Uso.

---

# 135. TELEMETRÍA COPY

No:

`Telemetría y consumo local`

Usar:

`Uso de inteligencia artificial`

---

# 136. PRESUPUESTO COPY

`Límite mensual de IA`

---

# 137. FIRST RUN SIN IA

No mostrar error rojo.

Mostrar:

`La IA está desactivada. Puedes usar todas las funciones principales sin ella.`

---

# 138. ACCESO DIRECTO

Verificar:

`Mercado Inteligente.lnk`

Abre app.

No consola manual.

---

# 139. EVIDENCE

Actualizar:

`release-evidence/`

Con:

- backend;
- integration;
- frontend;
- Playwright;
- live search;
- IA live/mock;
- SQLite;
- screenshots;
- build.

---

# 140. REPORT FINAL

Crear:

`docs/development/GOAL-15-PRODUCT-DISCOVERY-CATALOG-LISTS-AI-UX-REPORT.md`

---

# 141. REPORT CONTENIDO

## Executive Summary

## Product Discovery

## Catalog UX

## Search independent from lists

## Local catalog

## Remote search

## Product URLs

## ProductCard

## Filters

## Basket CRUD

## Delete behavior

## Shopping Assistant

## OmniRouter

## Gemini model

## Design System

## Spanish audit

## Database migrations/reset

## Tests

## Live tests

## Screenshots

## Known limitations

## Final decision

---

# 142. PROJECT_STATE

Actualizar con:

- Goal 15;
- architecture Search;
- catalog persistence;
- Basket CRUD;
- ProductUrl;
- assistant;
- OmniRouter;
- DB reset/migration;
- tests;
- blockers.

---

# 143. DEFINITION OF DONE — DISCOVERY

- [ ] Buscar funciona sin listas
- [ ] Search consulta local
- [ ] Search consulta D1
- [ ] Search consulta Éxito
- [ ] catálogo se persiste
- [ ] búsqueda posterior usa catálogo local
- [ ] lista no requerida
- [ ] resultados visuales
- [ ] filtros
- [ ] ordenamiento
- [ ] grid/list
- [ ] imágenes
- [ ] ProductUrl
- [ ] Ver en tienda
- [ ] comparar sin lista
- [ ] agregar a lista opcional
- [ ] resultados parciales
- [ ] errors humanos

---

# 144. DEFINITION OF DONE — CRUD

- [ ] crear lista
- [ ] listar
- [ ] abrir
- [ ] editar
- [ ] renombrar
- [ ] eliminar
- [ ] confirmar delete
- [ ] eliminar lista activa
- [ ] no FK issues
- [ ] catálogo preservado
- [ ] histórico preservado
- [ ] items CRUD
- [ ] varias listas
- [ ] selector desde producto

---

# 145. DEFINITION OF DONE — CHAT IA

- [ ] FAB abajo derecha
- [ ] responsive
- [ ] drawer/chat
- [ ] contextual a Buscar
- [ ] contextual a lista
- [ ] contextual a comparación
- [ ] no inventa precios
- [ ] usa datos estructurados
- [ ] links Ver en tienda
- [ ] IA OFF no rompe app
- [ ] OmniRouter
- [ ] Gemini Flash 3.8 por ID real
- [ ] budget
- [ ] privacy
- [ ] errores humanos

---

# 146. DEFINITION OF DONE — UI/UX

- [ ] mini hero por módulo
- [ ] español completo
- [ ] inputs diseñados
- [ ] checkbox diseñados
- [ ] switches diseñados
- [ ] selects diseñados
- [ ] botones
- [ ] modales
- [ ] toasts
- [ ] skeletons
- [ ] empty states
- [ ] accesibilidad
- [ ] responsive
- [ ] catálogo tipo ecommerce
- [ ] Ajustes reorganizados

---

# 147. DEFINITION OF DONE — DB

- [ ] migration o reset justificado
- [ ] backup previo
- [ ] secrets preservados
- [ ] schema final
- [ ] FTS OK
- [ ] ProductUrl persistido
- [ ] integrity_check = ok
- [ ] catálogo funciona sin Basket

---

# 148. CRITERIO DE ACEPTACIÓN PRINCIPAL

## Escenario 1

Usuario nuevo.

Cero listas.

Busca:

`Queso`

Obtiene catálogo.

PASS.

## Escenario 2

Cierra app.

Abre app.

Busca Queso.

Obtiene datos locales inmediatamente.

PASS.

## Escenario 3

Pulsa:

`Ver en tienda`

Abre producto real.

PASS.

## Escenario 4

Agrega producto a lista.

Lista CRUD funciona.

PASS.

## Escenario 5

Elimina lista.

Producto sigue en catálogo.

PASS.

## Escenario 6

Abre chatbot.

Pregunta:

`¿Cuál de estos quesos conviene más?`

Recibe recomendación basada en datos reales.

PASS.

---

# 149. REGLA DE CALIDAD

NO marcar Search DONE si:

solo encuentra productos que antes estuvieron en una lista.

NO marcar catálogo DONE si:

depende de sync de Basket.

NO marcar CRUD DONE si:

Delete no funciona.

NO marcar Chat DONE si:

responde sin contexto o inventa precios.

NO marcar UI DONE si:

inputs/checkbox/select siguen viéndose nativos/inconsistentes.

---

# 150. REGLA FINAL

Mercado Inteligente debe quedar con dos experiencias claramente separadas:

# DESCUBRIR

`Buscar y comparar libremente productos`

↓

`Ver en tienda`

↓

opcional:

`Agregar a lista`

# PLANEAR

`Listas de compra`

↓

`Actualizar precios`

↓

`Optimizar`

Y una tercera capa transversal:

# DECIDIR

`Asistente IA`

↓

`recomendaciones basadas en precios reales`

Este goal termina únicamente cuando las tres funcionan de forma integrada.

Comienza reproduciendo estos escenarios:

1. buscar un producto nunca agregado a una lista;
2. navegar una búsqueda ya sincronizada como catálogo;
3. eliminar una lista;
4. abrir el asistente IA desde Buscar.

Luego implementa, prueba y documenta todo el goal sin pedirme confirmación entre subtareas.