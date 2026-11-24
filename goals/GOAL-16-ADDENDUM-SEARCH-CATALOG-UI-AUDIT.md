# ADDENDUM OBLIGATORIO AL GOAL 16

Este documento amplía GOAL 16 y tiene precedencia ante cualquier regla anterior incompatible.

Proyecto:

`C:\Users\santi\Documents\0 REPOSITORIOS\Mercado_Inteligente`

---

# 1. CADA BÚSQUEDA EXPLÍCITA = LOCAL + ONLINE

Cada vez que el usuario:

- pulse `Buscar`;
- pulse Enter;
- ejecute explícitamente una nueva consulta;

la aplicación debe consultar:

1. la base de datos local;
2. D1 si está habilitado;
3. Éxito si está habilitado;
4. cada nuevo retailer habilitado que soporte `SearchProducts`.

NO consultar providers por cada tecla escrita.

Las sugerencias mientras escribe siguen siendo locales.

---

# 2. BÚSQUEDA CON RESPUESTA INMEDIATA

Flujo:

`Buscar "Queso"`

↓

consultar BD local inmediatamente

↓

mostrar resultados locales

↓

en paralelo consultar tiendas

↓

recibir resultados remotos

↓

reconciliar con BD

↓

actualizar pantalla automáticamente

El usuario no debe esperar a que todos los retailers terminen para empezar a ver resultados.

---

# 3. SEARCH SESSION

Crear concepto equivalente a:

`SearchSession`

Debe registrar:

- query;
- startedAt;
- retailers consultados;
- estado por retailer;
- resultados locales;
- resultados nuevos;
- actualizados;
- no disponibles confirmados;
- errores;
- finishedAt.

Solo para diagnóstico/UX; no almacenar información innecesaria.

---

# 4. MERGE / RECONCILIACIÓN

No simplemente concatenar resultados.

Crear servicio central:

`CatalogReconciliationService`

o equivalente.

Identidad preferida:

1. retailer + externalSkuId;
2. EAN dentro del retailer;
3. claves externas estables;
4. nunca nombre solamente.

---

# 5. PRODUCTO NUEVO

Si retailer devuelve SKU inexistente:

crear:

- ExternalProduct;
- ExternalSku;
- Offer;
- PriceObservation cuando corresponda;
- ProductUrl;
- ImageUrl;
- categorías;
- atributos disponibles.

Mostrarlo inmediatamente.

---

# 6. PRODUCTO EXISTENTE CAMBIÓ

Si mismo SKU contiene:

- nuevo precio;
- promoción diferente;
- disponibilidad diferente;
- nombre actualizado;
- imagen diferente;
- ProductUrl diferente;
- categoría diferente;
- presentación actualizada;

actualizar estado actual.

Crear PriceObservation cuando el cambio sea históricamente significativo.

NO destruir histórico.

---

# 7. PRECIO SIN CAMBIOS

Si precio/oferta no cambió:

actualizar:

`LastSeenAt`

`LastCheckedAt`

No crear PriceObservation duplicado innecesariamente.

---

# 8. PRODUCTO NO PRESENTE EN UNA BÚSQUEDA

IMPORTANTE:

La ausencia en resultados de:

`search("queso")`

NO prueba que el producto fue eliminado del retailer.

Por tanto NO:

- borrar SKU;
- marcarlo discontinued;
- eliminar histórico;

basándose únicamente en una búsqueda parcial.

---

# 9. CUÁNDO PUEDE MARCARSE NO DISPONIBLE

Solo cuando exista evidencia suficiente:

### A
el retailer responde explícitamente:

`OUT_OF_STOCK`

### B
consulta directa al SKU indica no disponible/eliminado.

### C
sincronización COMPLETA de la categoría confirma ausencia.

### D
sincronizaciones completas repetidas confirman desaparición según política configurada.

---

# 10. LIFECYCLE

Usar:

`ACTIVE`

→ disponible recientemente.

`MISSING`

→ no encontrado donde debería aparecer.

`INACTIVE`

→ ausente repetidamente.

`DISCONTINUED`

→ evidencia fuerte de retiro.

Nunca eliminar histórico.

---

# 11. SEARCH RESULT CURRENT STATE

Un producto:

`INACTIVE/DISCONTINUED`

no debe aparecer normalmente entre resultados activos.

Puede aparecer en histórico si usuario lo consulta explícitamente.

---

# 12. RESULTADOS REMOTOS PREVALECEN EN ESTADO ACTUAL

Para el mismo contexto:

datos online recién consultados reemplazan el estado actual anterior.

Ejemplo:

DB:
`$19.900`

Web:
`$18.500`

Después del merge:

Current Offer:
`$18.500`

History:
conserva ambos valores.

---

# 13. ERROR DEL RETAILER

Si retailer falla:

NO modificar datos existentes como no disponibles.

Mantener last-known-good.

Mostrar:

`No pudimos actualizar D1. Mostramos los últimos datos guardados.`

---

# 14. CADA BÚSQUEDA DEBE MOSTRAR ORIGEN

Resultados pueden indicar discretamente:

`Actualizado ahora`

`Dato guardado`

`No pudimos actualizar esta tienda`

Sin lenguaje técnico.

---

# 15. ACTUALIZAR LA UI EN VIVO

Cuando terminan providers:

no exigir F5.

Cards deben:

- aparecer;
- cambiar precio;
- cambiar badges;
- actualizar contador;

automáticamente.

---

# 16. IMPORTAR CATÁLOGO POR CATEGORÍAS

Agregar una función independiente:

`Importar catálogo`

Ubicación sugerida:

`Tiendas → Catálogo`

o:

`Ajustes → Datos → Catálogo`

---

# 17. OBJETIVO

Permitir traer a SQLite grandes partes del catálogo disponible de cada retailer para:

- análisis más completos;
- búsquedas instantáneas;
- comparación entre categorías;
- histórico;
- oportunidades;
- mejor relación precio/cantidad.

---

# 18. DESCUBRIR CATEGORÍAS

Cada provider debe declarar si soporta:

`EnumerateCategories`

`EnumerateCatalogByCategory`

Si soporta categorías:

obtener jerarquía real del retailer.

Ejemplo:

```text
Despensa
  ├─ Arroz y granos
  ├─ Aceites
  └─ Pastas

Lácteos
  ├─ Leches
  ├─ Quesos
  └─ Yogures
```

No inventar categorías si retailer ofrece estructura oficial.

---

# 19. UI DE IMPORTACIÓN

Mostrar retailer.

Ejemplo:

## Éxito

☐ Todo el catálogo

☑ Lácteos

  ☑ Quesos

  ☐ Yogures

☐ Aseo

Botón:

`Importar categorías seleccionadas`

---

# 20. TODO EL CATÁLOGO

Si técnicamente viable:

opción:

`Seleccionar todas las categorías`

Debe recorrer paginación completa.

Respetar:

- rate limits;
- timeouts;
- retries;
- cooldown;
- TOS del retailer.

---

# 21. NO PROMETER CATÁLOGO COMPLETO SI NO ES POSIBLE

Si provider no permite enumerar todo:

mostrar:

`Esta tienda solo permite importar parte de su catálogo.`

Capability:

`PARTIAL`

---

# 22. IMPORT JOB

Crear:

`CatalogImportJob`

Estados:

- Pending
- Running
- Paused
- Completed
- Partial
- Failed
- Cancelled

---

# 23. PROGRESO

UI:

`Éxito · Quesos`

`342 / 781 productos`

`44 %`

Mostrar:

- nuevos;
- actualizados;
- sin cambios;
- errores.

---

# 24. CANCELAR

El usuario debe poder:

`Cancelar importación`

Los productos ya importados permanecen válidos.

No rollback masivo innecesario.

---

# 25. REANUDAR

Si proceso se interrumpe:

permitir reanudar desde checkpoint cuando sea técnicamente viable.

No comenzar siempre desde cero.

---

# 26. DEDUP

Múltiples páginas/categorías pueden devolver el mismo SKU.

Deduplicar mediante identidad externa.

No duplicar productos.

---

# 27. RECONCILIACIÓN COMPLETA DE CATEGORÍA

Una importación COMPLETA y exitosa de categoría sí puede utilizarse para detectar:

- nuevos productos;
- cambios;
- SKUs desaparecidos.

Marcar desaparecidos primero:

`MISSING`

No borrar inmediatamente.

---

# 28. POLÍTICA DE AUSENCIA

Configurable internamente.

Ejemplo inicial:

1 sincronización completa ausente:

`MISSING`

varias sincronizaciones/periodo razonable:

`INACTIVE`

No convertir en DISCONTINUED sin evidencia fuerte.

---

# 29. HISTÓRICO SIEMPRE SE CONSERVA

Ni:

- búsqueda;
- catálogo sync;
- reconciliación;
- delete de oferta actual;

debe eliminar `PriceObservation`.

---

# 30. BOTÓN ACTUALIZAR CATÁLOGO

Después de importar categorías:

mostrar:

`Actualizar catálogo`

Permite refrescar las categorías previamente seleccionadas.

---

# 31. PLAN DE ACTUALIZACIÓN

No actualizar automáticamente 100.000 productos cada vez que abre la app.

Prioridades:

1. consultas explícitas;
2. productos en listas;
3. productos frecuentes;
4. categorías importadas;
5. mantenimiento.

---

# 32. ANALÍTICA USA EL CATÁLOGO IMPORTADO

Módulo Análisis debe beneficiarse automáticamente de los productos importados.

Ejemplo:

Categoría:
`Quesos`

Puede comparar:

- precio/kg;
- retailer;
- descuento;
- histórico;
- presentaciones.

---

# 33. COBERTURA DEL ANÁLISIS

Mostrar:

`Analizamos 436 productos de 4 tiendas.`

Esto ayuda a interpretar resultados.

---

# 34. NO CONFUNDIR COBERTURA CON CALIDAD

Más productos:

→ mejor cobertura estadística.

No significa mejor calidad del producto.

---

# 35. AUDITORÍA UX/UI ANTES DEL PRÓXIMO REDISEÑO

NO realizar todavía un nuevo rediseño visual masivo.

Primero generar un paquete completo de evidencia visual de la versión actual después de completar esta fase funcional.

---

# 36. LIMITACIÓN MCP / LOCALHOST

La aplicación funciona en:

`127.0.0.1`

Un MCP web remoto como Firecrawl NO debe asumirse capaz de acceder al localhost del PC del usuario.

Por tanto la captura primaria debe realizarse LOCALMENTE mediante Playwright.

Luego esas capturas serán entregadas a ChatGPT para auditoría visual.

---

# 37. MCP DE ANÁLISIS WEB

Codex puede usar un MCP/web-analysis tool para:

- analizar patrones de ecommerce públicos;
- tomar referencias de sitios públicos;
- estudiar D1/Éxito/Jumbo/etc.;
- comparar patrones UX.

Pero para Mercado Inteligente localhost:

usar Playwright local.

---

# 38. CAPTURE AUDIT TOOL

Crear script:

`scripts/capture-ui-audit.ps1`

que ejecute un Playwright dedicado.

Salida:

`docs/ui-audit/current/`

---

# 39. RUTAS A CAPTURAR

Actualmente existen como mínimo:

- `/`
- `/buscar`
- `/mi-mercado`
- `/comparar`
- `/historial`
- `/tiendas`
- `/ajustes`

Agregar cualquier ruta nueva:

- `/analisis`
- gestión de datos/catálogo;
- etc.

Descubrir rutas automáticamente cuando sea posible.

---

# 40. CAPTURAS DESKTOP

Viewport:

`1440 × 1000`

Tomar full-page.

---

# 41. CAPTURAS MOBILE

Viewport:

aprox.

`390 × 844`

Tomar full-page.

---

# 42. CAPTURAS DE ESTADOS

NO capturar solo una imagen por página.

Para cada módulo relevante capturar:

### Default
estado normal.

### Empty
sin datos.

### Loading
operación en curso.

### Populated
con datos reales/fixtures representativos.

### Error
error controlado.

### Modal/Drawer
cada modal importante.

---

# 43. BUSCAR — SCREENSHOT MATRIX

Capturar:

1. inicial;
2. escribiendo consulta;
3. loading;
4. resultados;
5. filtros abiertos;
6. lista vs grid;
7. producto detalle;
8. provider parcial;
9. sin resultados;
10. error;
11. chatbot abierto;
12. chatbot con respuesta.

Desktop + mobile donde aplique.

---

# 44. MI MERCADO

Capturar:

- cero listas;
- múltiples listas;
- lista vacía;
- lista con items;
- create modal;
- rename;
- delete modal;
- optimizer result.

---

# 45. COMPARAR

Capturar:

- vacío;
- comparación 2 productos;
- múltiples tiendas;
- sustitutos;
- responsive.

---

# 46. HISTORIAL

Capturar:

- vacío;
- producto con histórico;
- gráfico;
- filtros;
- detalle.

---

# 47. TIENDAS

Capturar:

- providers healthy;
- provider degraded;
- selección tiendas;
- catálogo/categorías;
- import progress.

---

# 48. AJUSTES

Capturar cada sección:

- General
- Ubicación
- IA
- Comparación
- Datos
- Copias de seguridad
- Estado

También:

- token modal/input;
- backups;
- danger zone.

---

# 49. ANÁLISIS

Cuando exista:

- dashboard;
- product analysis;
- category analysis;
- opportunities;
- list analysis.

---

# 50. COMPONENT GALLERY

Crear una página SOLO para desarrollo:

`/dev/ui-kit`

NO visible en navegación de producción.

Mostrar todos:

- buttons;
- inputs;
- checkbox;
- switches;
- selects;
- badges;
- cards;
- alerts;
- toasts;
- modal;
- skeleton;
- tabs;
- chips;
- ProductCard.

Capturarla.

Esto permitirá detectar inconsistencias globales.

---

# 51. DATOS DE SCREENSHOT

Usar fixture dataset determinista.

No depender de que providers estén online para capturar estados.

Debe parecer datos reales, pero claramente test/dev.

---

# 52. NO EXPONER SECRETOS

Antes de screenshot:

ocultar:

- API keys;
- paths sensibles innecesarios;
- tokens;
- cookies.

---

# 53. NOMBRES DE ARCHIVOS

Ejemplo:

`search-desktop-results.png`

`search-mobile-results.png`

`market-desktop-delete-modal.png`

`settings-desktop-ai.png`

---

# 54. MANIFEST DE CAPTURAS

Crear:

`docs/ui-audit/current/MANIFEST.md`

Con tabla:

| Screenshot | Ruta | Viewport | Estado | Descripción |

---

# 55. ZIP PARA CHATGPT

Crear:

`Mercado_Inteligente_UI_Audit.zip`

Debe contener:

- screenshots;
- MANIFEST.md;
- lista de rutas;
- versión;
- notas de estados.

NO incluir código completo.

NO incluir secretos.

---

# 56. INSTRUCCIÓN FINAL AL USUARIO

Al terminar esta fase, informar claramente:

`Sube Mercado_Inteligente_UI_Audit.zip a ChatGPT para realizar la auditoría visual antes del próximo goal de diseño.`

---

# 57. NO IMPLEMENTAR REDISEÑO BASADO EN SUPOSICIONES

Después de generar el paquete:

NO iniciar un rediseño visual masivo por cuenta propia.

Esperar la auditoría UX/UI externa solicitada.

Las correcciones funcionales necesarias sí deben quedar terminadas.

---

# 58. REFERENCIAS PÚBLICAS OPCIONALES

Para preparar la auditoría pueden estudiarse con MCP/web:

- ecommerce de supermercados;
- comparadores;
- apps de compras;
- dashboards financieros/productividad.

Documentar patrones, no copiar diseños.

---

# 59. TEST SEARCH RECONCILIATION

Crear:

`Search_MergesLocalAndRemoteResults`

---

# 60. TEST UPDATED PRICE

`Search_UpdatesExistingOffer_WhenRemotePriceChanged`

---

# 61. TEST NEW SKU

`Search_InsertsPreviouslyUnknownRemoteSku`

---

# 62. TEST ABSENCE

`SearchResultAbsence_DoesNotDeactivateExistingSku`

Obligatorio.

---

# 63. TEST PROVIDER EXPLICIT UNAVAILABLE

`ExplicitProviderUnavailable_UpdatesCurrentAvailabilityButPreservesHistory`

---

# 64. TEST CATEGORY RECONCILIATION

`CompletedCategorySync_MarksMissingSkuWithoutDeletingHistory`

---

# 65. TEST CATEGORY IMPORT

`CategoryImport_PaginatesPersistsAndDeduplicates`

---

# 66. TEST IMPORT CANCEL

`CancelledCatalogImport_PreservesCompletedProducts`

---

# 67. TEST UI AUDIT

Playwright script debe verificar que cada ruta crítica produce screenshot.

Si falta una:

fallar capture job.

---

# 68. DEFINICIÓN DE DONE — SEARCH

- [ ] cada búsqueda explícita consulta BD
- [ ] cada búsqueda explícita intenta providers habilitados
- [ ] no provider call por tecla
- [ ] local se muestra primero
- [ ] online se mergea
- [ ] nuevos productos se insertan
- [ ] productos existentes se actualizan
- [ ] precio histórico preservado
- [ ] ausencia en search no implica eliminación
- [ ] unavailable explícito se refleja
- [ ] provider failure conserva last-known-good
- [ ] UI se actualiza sin reload

---

# 69. DEFINICIÓN DE DONE — CATÁLOGO

- [ ] categorías enumerables por provider
- [ ] selección de categorías
- [ ] importar categoría
- [ ] seleccionar todo cuando viable
- [ ] paginación
- [ ] progreso
- [ ] cancelar
- [ ] reanudar cuando viable
- [ ] dedup
- [ ] persistencia
- [ ] reconciliación completa
- [ ] lifecycle
- [ ] refresh posterior
- [ ] análisis usa catálogo

---

# 70. DEFINICIÓN DE DONE — AUDITORÍA VISUAL

- [ ] todas las rutas capturadas
- [ ] desktop
- [ ] mobile
- [ ] empty states
- [ ] populated
- [ ] loading
- [ ] errors
- [ ] dialogs
- [ ] chat
- [ ] ui-kit
- [ ] manifest
- [ ] ZIP generado
- [ ] secretos ausentes
- [ ] listo para enviar a ChatGPT

---

# 71. REGLA FINAL DE DATOS

No implementar:

`remote result = verdad que reemplaza destructivamente DB`.

Implementar:

`remote observation + reconciliation + lifecycle + history`.

El estado actual puede cambiar.

El histórico no se destruye.

---

# 72. ORDEN DE EJECUCIÓN

1. búsqueda DB + online en cada consulta;
2. reconciliación;
3. lifecycle correcto;
4. importación por categorías;
5. actualización completa de catálogo;
6. tests;
7. terminar funciones Goal 16 restantes;
8. estabilizar aplicación;
9. ejecutar capture audit;
10. generar `Mercado_Inteligente_UI_Audit.zip`;
11. detenerse antes de rediseño visual masivo.

No pidas confirmación entre subtareas.