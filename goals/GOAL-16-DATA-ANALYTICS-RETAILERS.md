# GOAL 16 — GESTIÓN DE DATOS, ANÁLISIS INTELIGENTE, OMNIROUTER Y EXPANSIÓN DE SUPERMERCADOS

Trabaja sobre:

`C:\Users\santi\Documents\0 REPOSITORIOS\Mercado_Inteligente`

Este goal parte de `Mercado Inteligente v1.0.0`.

NO reinicies el proyecto desde cero.

NO elimines funcionalidades funcionales del Goal 15.

NO rediseñes branding.

Mantén:

- Product Discovery independiente de listas;
- catálogo ecommerce;
- CRUD de listas;
- asistente IA;
- búsqueda D1/Éxito;
- Google opcional;
- ejecución local.

---

# 1. OBJETIVO GENERAL

Ampliar Mercado Inteligente en cuatro áreas:

1. **Gestión profesional de base de datos y copias de seguridad.**
2. **Herramientas avanzadas de análisis y calidad/precio.**
3. **Corrección definitiva de OmniRouter.**
4. **Expansión modular a nuevas cadenas.**

Tiendas solicitadas:

- D1
- Éxito
- Ara
- La Vaquita / Supermu
- Mundo Huevo
- Olímpica
- Jumbo
- Price Mark / PriceSmart

IMPORTANTE:

Antes de implementar la última cadena, identificar inequívocamente cuál es la cadena que el usuario quiso indicar como `Price Mark`.

No inventar dominio/provider.

Documentar el nombre oficial utilizado.

---

# 2. P0 — CORREGIR OMNIROUTER

Actualmente el usuario introduce su clave pero recibe:

`No se pudo guardar la clave. Comprueba el formato e inténtalo de nuevo.`

Esto es un blocker funcional.

Reproducir el problema antes de modificar código.

---

# 3. SECRET TYPE CORRECTO

La interfaz y backend deben utilizar:

`omnirouter`

como identificador principal del secreto.

Endpoint:

`/api/v1/secrets/omnirouter`

NO utilizar `openrouter` para una clave de OmniRouter.

---

# 4. COMPATIBILIDAD LEGACY

Si existe configuración anterior bajo:

`openrouter`

no romperla bruscamente.

Implementar migración segura:

1. detectar secret legacy;
2. si OmniRouter no tiene secret propio;
3. permitir migrarlo o probarlo;
4. guardar bajo `omnirouter`;
5. no mostrar la clave.

Después usar `omnirouter` como fuente principal.

---

# 5. NO VALIDAR POR PREFIJO INVENTADO

No rechazar una clave OmniRouter simplemente porque:

- no empieza por `sk-or-`;
- no coincide con OpenRouter;
- Codex supone un patrón.

Validación al guardar debe limitarse a:

- no vacía;
- longitud razonable;
- caracteres válidos;
- no whitespace accidental.

La verdadera validación debe hacerse intentando autenticación contra OmniRouter.

---

# 6. SAVE SECRET

Flujo:

Usuario pega token

↓

`Guardar de forma segura`

↓

DPAPI / SecretStore

↓

respuesta:

`Clave guardada`

↓

opcionalmente:

`Probar conexión`

No imprimir token.

---

# 7. ERRORES DE SECRET

Si DPAPI falla:

mostrar:

`No pudimos guardar la clave de forma segura en este equipo.`

En detalles técnicos:

error real.

No culpar automáticamente al formato.

---

# 8. BOTÓN CONSULTAR ESTADO

El botón:

`Consultar estado`

debe comprobar:

- existe secret;
- IA habilitada;
- OmniRouter responde;
- autenticación válida;
- modelo configurado;
- último test;
- latencia aproximada.

Mostrar:

### OmniRouter
`Conectado`

o:

`No configurado`

o:

`Clave inválida`

o:

`Servicio temporalmente no disponible`

---

# 9. PROBAR CONEXIÓN

Debe realizar prueba real.

No confundir:

`clave guardada`

con:

`clave válida`.

Estados separados:

- Guardada
- Validada
- Error

---

# 10. MODELOS OMNIROUTER

Consultar:

`GET /v1/models`

No hardcodear IDs inventados.

Detectar Gemini Flash solicitado por nombre/catálogo real.

Si `Gemini Flash 3.8` no existe exactamente:

mostrar modelos Gemini Flash realmente disponibles.

No afirmar que existe.

---

# 11. MODELO PREDETERMINADO

Preferencia:

`Gemini Flash 3.8`

solo si OmniRouter devuelve un modelo compatible con ese nombre.

Registrar:

- DisplayName
- exact ModelId
- fecha de consulta

---

# 12. NO MOSTRAR OPENROUTER

Auditar frontend completo.

Si el proveedor actual es OmniRouter:

eliminar copy visible:

`OpenRouter`

Mantener únicamente:

`OmniRouter`

salvo documentación técnica de migración.

---

# 13. P0 — GESTIÓN DE BACKUPS

Actualmente no basta con crear/listar backups.

Implementar CRUD/gestión real.

---

# 14. MÓDULO DE COPIAS DE SEGURIDAD

En:

`Ajustes → Datos y copias de seguridad`

crear una sección visual completa.

Mini hero:

## Protege tus datos

`Crea, restaura y administra copias de tu información local.`

---

# 15. LISTADO DE COPIAS

Mostrar tabla/cards:

- fecha;
- hora;
- tamaño;
- tipo;
- integridad;
- versión de esquema;
- nombre.

Ejemplo:

`10 oct 2026 · 2:20 p. m.`

`12.4 MB`

`SQLite`

`Verificada ✓`

---

# 16. CREAR BACKUP

Botón:

`Crear copia ahora`

Mostrar progreso.

Después:

`Copia creada correctamente.`

Actualizar lista automáticamente.

---

# 17. ELIMINAR BACKUP

Cada copia:

`Eliminar`

Confirmación:

## ¿Eliminar esta copia?

`Esta acción no se puede deshacer.`

No borrar DB actual.

---

# 18. RESTAURAR BACKUP

Acción:

`Restaurar`

Antes de restaurar:

1. comprobar archivo;
2. integrity_check;
3. comprobar formato;
4. comprobar versión;
5. crear automáticamente backup preventivo de DB actual.

Después:

1. cerrar conexiones SQLite;
2. restaurar;
3. ejecutar migrations si procede;
4. integrity_check;
5. foreign_key_check;
6. reiniciar estado/cache.

---

# 19. CONFIRMACIÓN RESTORE

Modal:

## Restaurar copia

`La información actual será reemplazada por la copia seleccionada. Primero crearemos una copia de seguridad automática del estado actual.`

Requerir:

`Restaurar`

---

# 20. NO RESTAURAR BACKUP CORRUPTO

Si:

`PRAGMA integrity_check != ok`

rechazar restore.

Mostrar:

`Esta copia parece estar dañada y no se restaurará.`

---

# 21. ABRIR CARPETA

Botón:

`Abrir carpeta de copias`

Solo en Windows/local.

No exponer filesystem por HTTP remoto.

---

# 22. DESCARGAR / EXPORTAR

Permitir:

`Exportar copia`

si aporta valor.

Como aplicación local puede abrir/copiar archivo mediante mecanismos seguros.

---

# 23. RENOMBRAR / ETIQUETAR BACKUP

Opcional pero recomendable:

permitir etiqueta:

`Antes de agregar Olímpica`

`Configuración estable`

No usar nombre libre directamente como path inseguro.

Guardar label sanitizado/metadata.

---

# 24. BACKUP CRUD API

Crear endpoints seguros equivalentes:

`GET /api/v1/backups`

`POST /api/v1/backups`

`DELETE /api/v1/backups/{id}`

`POST /api/v1/backups/{id}/restore`

`POST /api/v1/backups/{id}/verify`

Usar identificadores controlados.

No aceptar rutas arbitrarias del usuario.

---

# 25. SEGURIDAD PATH TRAVERSAL

Nunca permitir:

`../../otroarchivo`

Resolver siempre contra directorio oficial de backups.

---

# 26. BOTÓN CONSULTAR BD

Crear en:

`Ajustes → Datos`

un botón:

`Actualizar información`

Debe volver a consultar el estado REAL actual de la base.

---

# 27. DASHBOARD DE BASE DE DATOS

Mostrar:

## Base de datos

- tamaño;
- productos;
- SKUs;
- ofertas;
- precios históricos;
- listas;
- items;
- tiendas;
- última actualización;
- última sincronización;
- última copia;
- RAW;
- logs.

---

# 28. ESTADO DE INTEGRIDAD

Mostrar:

`Base de datos: Correcta ✓`

Basado en:

`PRAGMA quick_check`

o `integrity_check` cuando proceda.

También:

`foreign_key_check`.

---

# 29. BOTÓN ACTUALIZAR INFORMACIÓN

Al pulsar:

`Actualizar información`

debe consultar nuevamente.

NO usar números almacenados del inicio de sesión.

Mostrar:

`Actualizado ahora`

---

# 30. ESTADO POR TIENDA

En la misma zona:

### D1
2.450 productos  
Última actualización: hace 35 min

### Éxito
4.510 productos  
Última actualización: hace 22 min

etc.

---

# 31. BOTÓN ACTUALIZAR DATOS

Separar claramente:

`Actualizar información`

de:

`Actualizar productos y precios`

El segundo debe ejecutar sincronización/catalog refresh correspondiente.

---

# 32. ACTUALIZACIÓN GENERAL

Crear:

`Actualizar productos y precios`

Debe consultar providers habilitados respetando:

- rate limits;
- contexto;
- cache;
- timeout;
- capacidades.

No borrar last-known-good por fallo.

---

# 33. PROGRESO ACTUALIZACIÓN

Mostrar por tienda:

`D1 · actualizando…`

`Éxito · listo`

`Olímpica · esperando`

etc.

Resultados parciales permitidos.

---

# 34. ZONA PELIGROSA — BORRAR DATOS

Crear:

`Ajustes → Datos → Zona de peligro`

Visualmente separada.

---

# 35. BOTÓN BORRAR INFORMACIÓN DE PRODUCTOS

Acción:

`Borrar catálogo y precios`

Debe eliminar:

- ExternalProducts;
- ExternalSkus;
- Offers;
- PriceObservations;
- datos derivados;
- FTS correspondiente;
- cache de búsqueda.

NO eliminar:

- OmniRouter key;
- Google key;
- settings;
- backups;
- listas, salvo incompatibilidad explícita.

Si listas quedan con referencias inválidas:

resolver correctamente antes de implementar.

---

# 36. BORRAR HISTÓRICO

Acción separada:

`Borrar historial de precios`

No borrar catálogo actual.

---

# 37. BORRAR RAW/CACHE

Acciones:

`Limpiar archivos temporales`

`Limpiar caché`

No confundir con borrar DB.

---

# 38. BORRAR LISTAS

Acción separada:

`Eliminar todas las listas`

Confirmación fuerte.

No borrar catálogo.

---

# 39. RESTABLECER BASE

Acción avanzada:

`Restablecer datos locales`

Debe:

1. crear backup automático;
2. preservar secretos;
3. preservar configuración si usuario elige;
4. recrear schema;
5. migrations;
6. integrity check.

---

# 40. FACTORY RESET

Si se implementa:

`Restablecer toda la aplicación`

debe explicar exactamente que eliminará.

Secretos solo se borran si el usuario lo solicita explícitamente.

---

# 41. CONFIRMACIÓN DESTRUCTIVA

Para acciones grandes exigir escribir:

`BORRAR`

o nombre equivalente.

No un simple clic accidental.

---

# 42. BACKUP AUTOMÁTICO ANTES DE DELETE GLOBAL

Por defecto:

crear backup antes de:

- borrar catálogo;
- reset;
- restore;
- migraciones destructivas.

Mostrar nombre del backup.

---

# 43. P0 — ANÁLISIS AVANZADO

Crear un módulo:

`Análisis`

Navigation sidebar.

Mini hero:

## Compra con más información

`Analiza precios, promociones y presentaciones para encontrar las opciones que realmente valen la pena.`

---

# 44. ANÁLISIS NO DEBE INVENTAR “CALIDAD”

El sistema no puede afirmar objetivamente:

`este producto tiene mejor calidad`

si no posee evidencia de calidad.

Distinguir:

### Valor/precio
calculable.

### Calidad
solo cuando existan atributos/evidencia.

---

# 45. SCORE CALIDAD/PRECIO

Crear concepto:

`ValueScore`

o internamente equivalente.

En UI:

`Relación calidad-precio`

Debe ser transparente.

---

# 46. COMPONENTES DEL SCORE

Utilizar únicamente evidencia disponible.

Posibles componentes:

- precio normalizado;
- precio por kg/L/unidad;
- posición frente a productos equivalentes;
- descuento actual;
- precio histórico;
- cantidad/presentación;
- disponibilidad;
- frescura;
- desperdicio/sobrecompra;
- preferencias del usuario;
- atributos verificables del producto.

---

# 47. CALIDAD / ATRIBUTOS

Si existe evidencia:

- composición;
- tamaño;
- categoría;
- tipo;
- atributos premium;
- rating del retailer si es fiable;
- características verificadas;

pueden participar.

Si no:

no inventar componente de calidad.

Mostrar:

`Confianza del análisis: Media`

---

# 48. SCORE EXPLICABLE

No mostrar solo:

`87/100`

Mostrar:

### Excelente relación precio/cantidad

`Es 14% más barato por kilogramo que productos equivalentes y está cerca de su precio mínimo reciente.`

---

# 49. IA COMO EXPLICADOR

OmniRouter puede explicar el análisis.

Pero:

el score numérico base debe ser determinista.

IA no puede inventar precio/calidad.

---

# 50. ANÁLISIS DE PRODUCTO

Product Detail debe incluir pestaña:

`Análisis`

Mostrar:

- precio actual;
- unit price;
- mínimo histórico;
- máximo histórico;
- promedio;
- cambio 7/30/90 días cuando exista;
- descuento;
- posición frente a similares;
- mejor tienda;
- ValueScore;
- confianza.

---

# 51. ¿ES BUEN MOMENTO PARA COMPRAR?

Crear indicador:

`Precio actual frente a su historial`

Ejemplo:

`Buen precio`

`8% por debajo del promedio de los últimos 30 días.`

Solo si existen suficientes observaciones.

---

# 52. NO SOBREINTERPRETAR POCOS DATOS

Con menos de N observaciones razonables:

`Todavía no hay suficiente historial para evaluar la tendencia.`

No inventar.

---

# 53. MEJOR PRECIO POR UNIDAD

Análisis:

`Mejor precio por kg`

`Mejor precio por litro`

`Mejor precio por unidad`

respetando dimensiones.

Nunca comparar:

`ml`

contra:

`g`.

---

# 54. PRESENTACIONES

Ejemplo:

500 g $10.000

1 kg $17.000

Mostrar:

`La presentación de 1 kg cuesta 15% menos por gramo.`

---

# 55. PROMOCIONES

Analizar promociones reales.

No asumir:

`2x1`

es mejor si obliga a comprar cantidad que el usuario no necesita.

Considerar cantidad requerida.

---

# 56. SOBRECOMPRA

Mostrar:

`Ahorro aparente`

vs:

`Ahorro efectivo para tu cantidad`.

---

# 57. PRODUCTO CON MEJOR VALOR

En resultados/categoría:

badge:

`Mejor valor`

solo cuando score tenga confianza suficiente.

También:

`Más barato`

`Mejor precio por unidad`

`Mayor descuento`

como conceptos separados.

---

# 58. ANÁLISIS POR CATEGORÍA

Ejemplo:

`Quesos`

Mostrar:

- más barato;
- mejor por kg;
- mejor descuento;
- mejor valor;
- presentación más conveniente.

---

# 59. ANÁLISIS ENTRE TIENDAS

Para mismo producto:

mostrar:

`D1 $10.000`

`Éxito $11.500`

Diferencia:

`Ahorras $1.500 en D1.`

---

# 60. ANÁLISIS DE HISTÓRICO

Charts:

- evolución del precio;
- rango;
- promedio;
- precio actual.

No gráficos decorativos.

---

# 61. VOLATILIDAD

Métrica:

`Precio estable`

`Precio variable`

basado en histórico.

Explicar.

---

# 62. OPORTUNIDADES

Crear vista:

`Oportunidades`

Ejemplos:

`Productos actualmente por debajo de su promedio`

`Descuentos relevantes`

`Productos de tus listas que bajaron de precio`

---

# 63. ANÁLISIS DE LISTA

En Mi mercado:

botón:

`Analizar esta lista`

Mostrar:

- total actual;
- tienda más barata;
- mejor combinación;
- ahorro potencial;
- items caros frente a historial;
- sustitutos con ahorro;
- productos sin datos frescos.

---

# 64. IA EN ANÁLISIS

Shopping Assistant debe poder responder:

`¿Cuál detergente ofrece mejor valor?`

`¿Me conviene comprar el paquete grande?`

`¿Qué productos de mi lista están caros?`

`¿En qué puedo ahorrar más?`

Usando únicamente evidencia estructurada.

---

# 65. P0 — EXPANSIÓN DE TIENDAS

Agregar nuevas cadenas siguiendo arquitectura modular.

Objetivo solicitado:

- La Vaquita / Supermu
- Mundo Huevo
- Olímpica
- Jumbo
- Price Mark / PriceSmart

---

# 66. DIAGNÓSTICO OBLIGATORIO POR CADENA

ANTES de implementar cada provider:

investigar únicamente fuentes oficiales y tráfico técnico autorizado/razonable.

Determinar:

- página oficial;
- ecommerce;
- búsqueda;
- catálogo;
- precio;
- promociones;
- imágenes;
- ProductUrl;
- EAN;
- ubicación/contexto;
- disponibilidad;
- términos/restricciones.

---

# 67. NO IMPLEMENTAR SCRAPING CIEGO

No crear scraper HTML frágil antes de estudiar fuente.

Prioridad:

1. API oficial/pública;
2. datos estructurados del ecommerce;
3. endpoint usado legítimamente por frontend web;
4. HTML estructurado estable;
5. fuente promocional/manual.

---

# 68. TÉRMINOS Y LIMITACIONES

Si una cadena prohíbe claramente automatización:

NO saltarse la restricción.

Implementar capacidad parcial/documentada cuando proceda.

---

# 69. REPORTE POR PROVIDER

Crear:

`docs/providers/<retailer>.md`

Incluyendo:

- fuente;
- capacidades;
- limitaciones;
- context;
- seller;
- freshness;
- tests.

---

# 70. CAPABILITIES

Cada provider declara:

- SearchProducts
- EnumerateCatalog
- Prices
- Promotions
- Availability
- Stores
- EAN
- ProductUrl
- Images
- LocationContext

Estado:

- SUPPORTED
- PARTIAL
- NOT_SUPPORTED

---

# 71. HEALTH

Cada nueva tienda:

- HEALTHY
- DEGRADED
- DOWN
- UNKNOWN

Una tienda caída no bloquea resto.

---

# 72. PROVIDER TEMPLATE

Usar arquitectura actual:

`IStoreProvider`

No agregar:

`if retailer == ...`

por toda la aplicación.

---

# 73. LA VAQUITA / SUPERMU

Primero confirmar si:

- son una misma plataforma;
- marcas relacionadas;
- ecommerce separado.

Crear códigos canónicos apropiados.

No asumir.

---

# 74. MUNDO HUEVO

Integración debe preservar atributos relevantes de huevo:

- cantidad;
- tamaño;
- clasificación A/AA/AAA si existe;
- Jumbo;
- tipo.

No comparar huevo diferente como equivalente exacto.

---

# 75. OLÍMPICA

Implementar provider según diagnóstico.

Conservar:

- seller;
- promotion;
- location context;
- ProductUrl;
- image.

---

# 76. JUMBO

Implementar provider independiente.

Si comparte plataforma técnica con otra cadena:

se puede crear base reutilizable.

No acoplar marca con implementación.

---

# 77. PRICE MARK / PRICESMART

Antes de programar:

determinar el retailer oficial exacto solicitado.

Si es:

`PriceSmart`

usar ese nombre/código.

Si es otra cadena:

documentarlo.

No decidir silenciosamente.

---

# 78. MEMBRESÍAS

Si alguna tienda requiere membresía:

modelar:

`RequiresMembership`

y explicar al usuario.

No asumir precio accesible para todos.

---

# 79. UI DINÁMICA DE TIENDAS

Buscar no debe tener D1/Éxito hardcodeados.

Filtros se generan desde providers habilitados.

Ejemplo:

- D1
- Éxito
- Olímpica
- Jumbo
- La Vaquita
- Mundo Huevo
- PriceSmart

---

# 80. LOGOS

Usar logos oficiales únicamente cuando sea legal/apropiado.

Fallback:

nombre textual.

---

# 81. BUSCAR TODAS LAS TIENDAS

Por defecto:

`Todas las tiendas`

Pero respetar timeout/concurrencia/rate limit.

---

# 82. SELECCIÓN DE TIENDAS

Permitir al usuario configurar:

`Tiendas que quiero consultar`

Así puede desactivar cadenas que no usa.

---

# 83. UBICACIÓN Y CADENAS

No afirmar precio de sucursal física si es:

- online;
- regional;
- ecommerce.

Mantener PriceScope.

---

# 84. MARKETPLACE

Mantener diferenciación.

Marketplace desactivado por defecto salvo configuración.

---

# 85. PRODUCT SEARCH

Las nuevas tiendas deben integrarse automáticamente con:

`ProductSearchOrchestrator`.

No crear buscadores separados.

---

# 86. ANÁLISIS MULTITIENDA

El módulo Análisis debe funcionar con todos los providers habilitados.

---

# 87. OPTIMIZER

Las nuevas cadenas deben poder participar del optimizador cuando:

- precio válido;
- contexto compatible;
- disponibilidad razonable.

---

# 88. PROVIDER FAILURE

Ejemplo:

Jumbo falla.

No bloquear:

D1

Éxito

Olímpica.

---

# 89. P1 — GESTIÓN DE DATOS AVANZADA

Agregar:

`Exportar datos`

Formatos:

- CSV para precios;
- JSON opcional;
- backup completo.

---

# 90. IMPORT

No implementar import arbitrario inseguro.

Solo restauración de backups compatibles.

---

# 91. DB INFO DETAILS

Opcional:

mostrar:

`Esquema vX`

`SQLite version`

`WAL`

`Última migración`

en:

`Detalles técnicos`.

---

# 92. REFRESH DB STATS

Después de:

- search;
- sync;
- delete;
- restore;
- reset;

actualizar contadores.

---

# 93. MAINTENANCE

Mantener:

`Optimizar base de datos`

Acción separada de:

`Actualizar productos`.

Explicar:

`Organiza internamente la base local. No cambia precios.`

---

# 94. VACUUM

Solo ejecutar cuando sea razonable.

No cada vez.

---

# 95. ANALYSIS UI

Sidebar:

`Análisis`

Icono apropiado.

---

# 96. ANALYSIS HOME

Cards:

### Mejores oportunidades

### Productos con mayor descuento

### Mejores precios por unidad

### Productos que bajaron

### Productos que subieron

### Tus listas

---

# 97. VALUE SCORE UI

Ejemplo:

`Muy buena relación precio/cantidad`

No depender únicamente de un número.

Mostrar score opcional:

`82/100`

con explicación.

---

# 98. CONFIDENCE

Mostrar:

`Confianza alta`

`Confianza media`

`Datos insuficientes`

---

# 99. AI ASSISTANT + ANALYSIS

FAB contextual también en Análisis.

Ejemplos rápidos:

`Explícame estas oportunidades`

`¿Qué comprarías hoy?`

`¿Cuál es el mejor detergente por precio/cantidad?`

---

# 100. TODO EN ESPAÑOL

Nuevos módulos también.

No mostrar al usuario:

- ValueScore
- Provider
- Backup
- Restore
- Vacuum
- Integrity check

Traducir:

`Relación calidad-precio`

`Tienda`

`Copia de seguridad`

`Restaurar`

`Optimizar base`

`Comprobar base`

---

# 101. MINI HERO — ANÁLISIS

## Analiza antes de comprar

`Compara precios, tamaños, promociones e historial para descubrir qué opciones ofrecen más por tu dinero.`

---

# 102. MINI HERO — DATOS

## Tus datos locales

`Consulta cuánto has guardado, actualiza la información y administra tus copias de seguridad.`

---

# 103. DISEÑO BACKUPS

No una lista de nombres de archivo fea.

Usar table/card UI consistente.

Acciones mediante menú.

---

# 104. DANGER ZONE UX

Color/estilo danger.

No usar botones rojos para acciones normales.

---

# 105. TESTS OMNIROUTER

Crear:

`OmniRouterSecret_CanBeStoredWithoutOpenRouterPrefix`

`OmniRouterSecret_IsNeverReturned`

`OmniRouterConnectionTest_DistinguishesStoredFromValid`

---

# 106. TEST BACKUPS

Crear:

`BackupCanBeCreatedListedVerifiedAndDeleted`

`RestoreCreatesPreRestoreBackup`

`CorruptBackupCannotBeRestored`

`BackupDeleteRejectsPathTraversal`

---

# 107. TEST DB RESET

`ClearCatalog_PreservesSecretsSettingsAndBackups`

`ClearPriceHistory_PreservesCurrentOffers`

`FactoryResetRequiresExplicitConfirmation`

---

# 108. TEST STATS

`DatabaseStatsRefreshReflectsCurrentDatabase`

---

# 109. TEST ANALYSIS

`BestUnitPrice_UsesCompatibleDimensionsOnly`

`ValueAnalysis_DoesNotInventQualityWithoutEvidence`

`HistoricalOpportunity_RequiresEnoughObservations`

---

# 110. PROVIDER TESTS

Por cada tienda:

- fixture/contract;
- mapper;
- ingest;
- search;
- partial failure;
- health.

Live opt-in separado.

---

# 111. PLAYWRIGHT — DATA MANAGEMENT

1. crear backup;
2. lista aparece;
3. comprobar;
4. eliminar;
5. desaparece.

Restore en DB aislada/test.

---

# 112. PLAYWRIGHT — DB CLEAR

En entorno test:

1. cargar catálogo;
2. borrar catálogo;
3. contadores → 0;
4. SecretStore sigue configurado;
5. backup permanece.

---

# 113. PLAYWRIGHT — ANALYSIS

1. cargar productos equivalentes;
2. abrir Análisis;
3. comprobar mejor precio/unidad;
4. abrir explicación;
5. datos coinciden.

---

# 114. PLAYWRIGHT — OMNIROUTER

Con mock:

1. pegar key;
2. guardar;
3. consultar estado;
4. conectado;
5. key no aparece en DOM después.

---

# 115. LIVE OMNIROUTER

Si key real disponible:

ejecutar.

No mostrar key.

---

# 116. LIVE PROVIDERS

Probar individualmente nuevas tiendas cuando sea técnicamente viable.

Guardar evidencia.

---

# 117. NO FALSO PASS

Provider sin soporte:

`PARTIAL`

o:

`NOT_SUPPORTED`

No `DONE` ficticio.

---

# 118. MIGRACIONES

Si nuevas tablas/campos:

EF Migration.

Antes:

backup.

Probar upgrade desde v1.0.0.

---

# 119. DB RESET DE DESARROLLO

Si las nuevas integraciones requieren reestructuración significativa y migrar la BD actual personal es más riesgoso:

se autoriza reset de la BD local de desarrollo.

ANTES:

backup completo.

Preservar:

- secretos;
- settings importantes;
- backups.

---

# 120. NO BORRAR SIN NECESIDAD

Preferir migration.

Documentar cualquier reset.

---

# 121. EVIDENCIA

Actualizar:

`release-evidence/`

Con:

- tests;
- DB integrity;
- backup CRUD;
- restore;
- analysis;
- provider diagnostics;
- live providers;
- OmniRouter;
- screenshots.

---

# 122. SCREENSHOTS

Guardar:

`docs/development/screenshots/goal-16/`

Mínimo:

1. panel Datos;
2. backups;
3. restore modal;
4. danger zone;
5. database stats;
6. Análisis;
7. análisis producto;
8. oportunidades;
9. comparación calidad/precio;
10. tiendas nuevas;
11. OmniRouter conectado.

---

# 123. REPORT

Crear:

`docs/development/GOAL-16-DATA-ANALYTICS-RETAILERS-REPORT.md`

---

# 124. REPORT CONTENIDO

## Executive Summary

## OmniRouter fix

## Secret migration

## Database management

## Backup CRUD

## Restore validation

## Database clearing tools

## Database statistics

## Analysis engine

## Value/quality methodology

## Historical analysis

## Retailer diagnostics

## La Vaquita / Supermu

## Mundo Huevo

## Olímpica

## Jumbo

## Price Mark / PriceSmart

## Tests

## Live tests

## Migrations

## Known limitations

## Final status

---

# 125. DEFINITION OF DONE — OMNIROUTER

- [ ] clave se puede guardar
- [ ] se usa secret `omnirouter`
- [ ] no requiere prefijo OpenRouter
- [ ] DPAPI funciona
- [ ] token no se devuelve
- [ ] Consultar estado funciona
- [ ] Probar conexión funciona
- [ ] `/v1/models` funciona
- [ ] modelo real seleccionado
- [ ] copy OpenRouter eliminado
- [ ] errores humanos

---

# 126. DEFINITION OF DONE — BACKUPS

- [ ] listar
- [ ] crear
- [ ] verificar
- [ ] eliminar
- [ ] restaurar
- [ ] pre-restore backup
- [ ] corrupción bloqueada
- [ ] abrir carpeta
- [ ] UI amigable
- [ ] path traversal protegido

---

# 127. DEFINITION OF DONE — BD

- [ ] botón Actualizar información
- [ ] estadísticas actuales
- [ ] integrity status
- [ ] FK status
- [ ] conteo por retailer
- [ ] última sync
- [ ] borrar catálogo
- [ ] borrar historial
- [ ] limpiar cache/RAW
- [ ] eliminar listas
- [ ] reset completo controlado
- [ ] confirmaciones
- [ ] auto backup
- [ ] secretos preservados

---

# 128. DEFINITION OF DONE — ANÁLISIS

- [ ] módulo Análisis
- [ ] análisis producto
- [ ] unit price
- [ ] historical average
- [ ] min/max
- [ ] tendencia
- [ ] mejor tienda
- [ ] mejor presentación
- [ ] mejor descuento
- [ ] relación calidad/precio
- [ ] score explicable
- [ ] confidence
- [ ] no inventar calidad
- [ ] oportunidades
- [ ] análisis listas
- [ ] integración IA

---

# 129. DEFINITION OF DONE — TIENDAS

Para cada una:

- [ ] diagnóstico
- [ ] fuente oficial identificada
- [ ] capacidades
- [ ] provider modular
- [ ] search cuando viable
- [ ] ingest
- [ ] ProductUrl
- [ ] image
- [ ] price
- [ ] scope
- [ ] health
- [ ] tests
- [ ] UI
- [ ] análisis
- [ ] optimizer cuando aplicable
- [ ] limitaciones documentadas

Objetivos:

- [ ] La Vaquita / Supermu
- [ ] Mundo Huevo
- [ ] Olímpica
- [ ] Jumbo
- [ ] Price Mark / PriceSmart identificado correctamente

---

# 130. CRITERIO DE ACEPTACIÓN — DATOS

Usuario abre:

`Ajustes → Datos`

Puede:

- ver estado actual;
- actualizar información;
- crear copia;
- restaurar;
- borrar copia;
- limpiar catálogo;
- borrar histórico;
- ver tamaños.

Sin consola.

PASS.

---

# 131. CRITERIO DE ACEPTACIÓN — ANÁLISIS

Usuario busca:

`Detergente`

Abre:

`Análisis`

Puede descubrir:

- opción más barata;
- mejor por kg/L;
- mejor descuento;
- producto con mejor valor;
- evolución histórica;
- explicación.

PASS.

---

# 132. CRITERIO DE ACEPTACIÓN — OMNIROUTER

Usuario pega clave OmniRouter.

Pulsa:

`Guardar`

↓

éxito.

Pulsa:

`Consultar estado`

↓

estado real.

Pulsa:

`Probar conexión`

↓

respuesta real.

No necesita PowerShell.

PASS.

---

# 133. CRITERIO DE ACEPTACIÓN — NUEVAS TIENDAS

Buscar producto.

Resultados pueden venir de múltiples cadenas habilitadas.

Un provider caído no bloquea otros.

Filtros se generan dinámicamente.

PASS.

---

# 134. REGLA FINAL

No marques Goal 16 como DONE simplemente porque:

- existen botones;
- hay endpoints;
- una tienda aparece en configuración;
- existe un score;
- backup se crea.

Debe funcionar de extremo a extremo.

Especialmente:

### OmniRouter
`guardar → consultar → validar → usar`

### Backups
`crear → listar → verificar → restaurar/eliminar`

### BD
`consultar → actualizar → borrar de forma segura`

### Análisis
`datos reales → cálculo determinista → explicación`

### Nuevas tiendas
`buscar → persistir → mostrar → comparar`

Implementa.

Prueba.

Corrige.

Vuelve a probar.

No pidas confirmación entre subtareas.

Empieza en este orden:

1. reproducir y corregir guardado OmniRouter;
2. implementar gestión de backups;
3. implementar herramientas de BD;
4. construir motor de análisis;
5. diagnosticar nuevas cadenas;
6. integrar providers viables;
7. ejecutar suite completa;
8. documentar resultados.