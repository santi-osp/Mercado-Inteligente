# GOAL 19 — TRANSFORMACIÓN VISUAL INTEGRAL, IDENTIDAD Y MICROINTERACCIONES

Trabaja sobre:

`C:\Users\santi\Documents\0 REPOSITORIOS\Mercado_Inteligente`

Usa como paquete visual obligatorio:

`Mercado_Inteligente_Visual_Identity_Pack_v1.zip`

Extrae el ZIP y lee primero:

- `README.md`
- `docs/DESIGN_TOKENS.md`
- `docs/ASSET_INTEGRATION_GUIDE.md`

Este goal NO es un retoque cosmético. Es una transformación visual completa del producto manteniendo intacta la lógica funcional estable.

---

## 1. OBJETIVO DEL REDISEÑO

Mercado Inteligente debe dejar de sentirse como:

- dashboard administrativo;
- proyecto Angular;
- template SaaS;
- interfaz generada por IA;
- panel de diagnósticos técnicos.

Debe sentirse como una aplicación comercial real con tres capas visuales:

### Grocery commerce
Para búsqueda, catálogo, fichas, filtros, promociones e imágenes.

### Price intelligence / fintech
Para análisis, histórico, comparación y oportunidades.

### Productivity
Para listas, ajustes, tiendas, backups y gestión de datos.

Concepto rector:

> **Mercado visual por fuera, inteligencia de precios por dentro.**

La prioridad visual debe ser:

1. producto;
2. precio;
3. ahorro;
4. cantidad/presentación;
5. tienda;
6. disponibilidad;
7. análisis;
8. IA;
9. diagnóstico técnico.

---

## 2. NO CAMBIAR

No cambies:

- nombre Mercado Inteligente;
- arquitectura funcional estable;
- contratos de búsqueda corregidos;
- reconciliación BD + web;
- CRUD de listas;
- optimizador;
- histórico;
- OmniRouter;
- providers;
- launcher;
- lógica de precios unitarios validada.

Solo modifica backend si es estrictamente necesario para exponer información que la UI necesita.

---

## 3. SKILLS Y HERRAMIENTAS

Antes de tocar UI:

1. revisa `GOAL-17-SKILLS-MATRIX.md` si existe;
2. reutiliza skills ya validadas;
3. busca nuevas skills solo si falta una capacidad concreta;
4. usa realmente herramientas de:
   - Angular frontend;
   - UX audit;
   - accessibility;
   - Playwright;
   - visual regression;
   - performance;
   - web/ecommerce analysis.

No instales skills dudosas ni des acceso a secretos.

---

# 4. ASSETS OBLIGATORIOS

## 4.1 Identidad principal

En producción usa únicamente:

`brand/logos/logo-primary.png`

Para espacios pequeños usa:

`brand/marks/mark-flat.svg`

NO mezcles simultáneamente `logo-secondary` y `logo-alternate` en la aplicación.

Son variantes de respaldo para revisión.

## 4.2 Favicon / PWA

Usa:

- `brand/favicons/favicon.ico`
- `brand/favicons/favicon-16x16.png`
- `brand/favicons/favicon-32x32.png`
- `brand/favicons/app-icon-180x180.png`
- `brand/favicons/app-icon-192x192.png`
- `brand/favicons/app-icon-512x512.png`

Actualizar:

- `index.html`;
- manifest PWA;
- favicon;
- shortcut Windows si aplica;
- metadata del navegador.

No debe quedar ningún icono de Angular/template.

## 4.3 Assets funcionales

Usar exactamente:

### Asistente
`icons/assistant-shopping.svg`

### Producto sin imagen
`illustrations/product-placeholder.svg`

### Búsqueda vacía
`illustrations/empty-search.svg`

### Sin listas
`illustrations/empty-lists.svg`

### Sin histórico
`illustrations/empty-history.svg`

### Sin análisis suficiente
`illustrations/empty-analysis.svg`

### Tienda sin logo
`illustrations/store-placeholder.svg`

### Patrón de hero
`illustrations/hero-pattern.svg`

El patrón de hero es opcional y debe usarse con mucha discreción.

---

# 5. RUTAS DE ASSETS

Copiar a rutas equivalentes:

```text
src/frontend/public/assets/brand/
src/frontend/public/assets/icons/
src/frontend/public/assets/illustrations/
```

No referenciar archivos desde carpetas temporales.

---

# 6. PALETA OFICIAL

Usar variables CSS centrales.

```css
--mi-primary: #167D5A;
--mi-primary-dark: #0E5E43;
--mi-primary-soft: #EAF5F0;
--mi-background: #F6F7F5;
--mi-surface: #FFFFFF;
--mi-surface-secondary: #F0F2EF;
--mi-border: #E3E7E4;
--mi-border-strong: #CCD3CF;
--mi-text: #151B18;
--mi-text-secondary: #5F6963;
--mi-muted: #87918B;
--mi-promo: #D94C3D;
--mi-warning: #B7791F;
--mi-info: #3267A8;
```

No inventar tonos verdes nuevos página por página.

---

# 7. REGLAS DE COLOR

El verde NO es decoración.

Usarlo para:

- CTA principal;
- selección;
- navegación activa;
- éxito;
- ahorro cuando semánticamente aplique.

Promociones/descuentos:

`#D94C3D`

No usar todo verde.

La interfaz debe respirar principalmente en:

- blanco;
- gris cálido;
- texto oscuro.

---

# 8. TIPOGRAFÍA

Mantener Inter o equivalente.

Escala:

```text
Display       40/44  700
H1            32/38  700
H2            24/30  700
H3            18/24  650
Body Large    16/24  400-500
Body          14/21  400
Small         13/18  400-500
Micro         12/16  500
```

Precio card:

`20–22px / 700`

Precio protagonista:

`24–28px / 700`

No usar tamaños exagerados tipo landing.

---

# 9. SPACING Y RADIOS

Escala permitida:

`4 / 8 / 12 / 16 / 20 / 24 / 32 / 40 / 48 / 64`

Radios:

```text
controls 8px
buttons 9px
cards 12px
drawers/modals 14px
hero 16px
```

Evitar 24–32px de radio en todo.

---

# 10. SOMBRAS

Preferir border sobre shadow.

Normal card:

sin shadow.

Hover:

`0 4px 14px rgba(20,30,25,.06)`

Modal/drawer:

`0 18px 54px rgba(20,30,25,.16)`

No glow.

---

# 11. MOTION SYSTEM

Centralizar tokens:

```css
--motion-instant: 90ms;
--motion-fast: 140ms;
--motion-base: 180ms;
--motion-medium: 260ms;
--motion-slow: 420ms;
--ease-standard: cubic-bezier(.2,.8,.2,1);
--ease-emphasized: cubic-bezier(.16,1,.3,1);
```

La interfaz debe sentirse fluida, NO animada por espectáculo.

---

# 12. USO DE GSAP

Puedes instalar/usar GSAP si no existe.

GSAP debe reservarse para animaciones coordinadas o de datos.

NO usar GSAP para cada hover.

CSS debe manejar:

- hover;
- focus;
- button press;
- checkbox;
- switch;
- pequeños cambios de color.

GSAP puede manejar:

- entrada coordinada de resultados;
- drawer/modal;
- actualización visual de precio;
- resultado del optimizador;
- transición de gráficas;
- entrada inicial de módulos complejos.

Implementar un servicio/directiva Angular para encapsularlo.

Usar `gsap.context()` y limpiar animaciones al destruir componentes.

---

# 13. PREFERS REDUCED MOTION

Obligatorio.

Si:

`prefers-reduced-motion: reduce`

entonces:

- no stagger;
- no translate largo;
- no count-up;
- no line-draw;
- transición casi instantánea.

La funcionalidad no puede depender de animaciones.

---

# 14. MICROINTERACCIÓN — BUTTONS

## Hover

180ms.

Primary:

`#167D5A → #0E5E43`

Elevación máxima:

`translateY(-1px)`

## Press

90ms.

`translateY(0)` + `scale(.99)`.

## Loading

Mantener ancho del botón.

Spinner pequeño + label:

`Buscando…`

No desplazar layout.

---

# 15. MICROINTERACCIÓN — INPUT

Focus:

- border primary;
- ring suave 2px;
- label/helper unchanged.

Error:

- border rojo sobrio;
- mensaje aparece con fade/slide 120ms.

No shake animations.

---

# 16. MICROINTERACCIÓN — CHECKBOX/RADIO/SWITCH

Checkbox checked:

- fill green;
- check scale 0.8 → 1;
- 140ms.

Radio:

- dot scale;
- 140ms.

Switch:

- thumb translate;
- 180ms emphasized ease.

No rebotes tipo app infantil.

---

# 17. MICROINTERACCIÓN — SIDEBAR

Active item:

- fondo `primary-soft`;
- icon/text green dark;
- transición 180ms.

No mover verticalmente elementos.

Desktop logo:

usar `logo-primary.png` o mark + texto HTML.

Mobile:

solo mark.

---

# 18. MICROINTERACCIÓN — PRODUCT CARD

Hover desktop:

- translateY(-2px);
- border ligeramente verde;
- shadow sutil;
- 180ms.

Image:

max `scale(1.015)`.

No zoom agresivo.

CTA aparece siempre; no ocultarlo hasta hover.

---

# 19. MICROINTERACCIÓN — RESULTADOS DE BÚSQUEDA

Cuando una búsqueda explícita llega:

1. mostrar resultados locales inmediatamente;
2. cuando llegan resultados web, reconciliar;
3. nuevos cards pueden entrar con:
   - opacity 0 → 1;
   - translateY 8px → 0;
   - stagger 30–40ms;
   - máximo 12 items animados.

No reanimar toda la grilla si cambia un solo precio.

---

# 20. MICROINTERACCIÓN — ACTUALIZACIÓN DE PRECIO

Si una card visible cambia de precio tras merge:

- fondo precio `primary-soft` durante 500–700ms;
- transición de número 220–320ms si GSAP se usa;
- si baja de precio, mostrar badge/indicador discreto;
- no hacer flash rojo/verde fuerte.

---

# 21. MICROINTERACCIÓN — FILTROS

Al activar filtro:

- checkbox cambia;
- chip aparece con scale .96 → 1;
- 140ms.

Al eliminar chip:

- opacity + width collapse suave;
- no reflow brusco.

Mobile filter drawer:

slide-up 260ms.

---

# 22. MICROINTERACCIÓN — PAGINACIÓN

Al cambiar página:

1. reducir opacity del grid a .6 durante ~90ms;
2. actualizar items;
3. restaurar opacity 160ms;
4. scroll al inicio del catálogo, no al top global.

No ejecutar nueva consulta web solo por cambiar de página.

---

# 23. MICROINTERACCIÓN — DRAWER DE PRODUCTO

Overlay:

fade 160ms.

Panel:

translateX(24px) + opacity → posición final, 240–260ms.

Close:

200ms.

Focus trap obligatorio.

Esc cierra.

---

# 24. MICROINTERACCIÓN — MODAL

Overlay fade 140ms.

Modal:

opacity + scale(.98 → 1), 180ms.

No zoom grande.

---

# 25. MICROINTERACCIÓN — TOAST

Desktop:

bottom-right.

Enter:

translateY(8px) + opacity.

220ms.

Auto-dismiss:

4–5 s dependiendo del mensaje.

Error importante no debe desaparecer demasiado rápido.

---

# 26. MICROINTERACCIÓN — ASISTENTE

FAB desktop:

icono + `Asistente`.

Hover:

- border/dark green;
- translateY(-1px).

Mobile:

solo icono.

Drawer:

misma animación de panel normal.

NO glow.

NO sparkle.

NO pulso infinito.

---

# 27. MICROINTERACCIÓN — HISTORIAL / CHARTS

Primera carga:

línea puede aparecer progresivamente en 350–450ms.

Cambio de rango:

interpolar datos 220–300ms.

Tooltip:

fade 100ms.

No animar continuamente.

---

# 28. MICROINTERACCIÓN — OPTIMIZADOR

Al finalizar:

- total principal opacity/translate 200ms;
- store cards stagger 50ms;
- ahorro count-up opcional 320ms;
- no confetti.

---

# 29. MICROINTERACCIÓN — EMPTY STATES

Ilustración:

solo fade-in 180ms.

No floating loops.

---

# 30. GLOBAL LAYOUT

Desktop:

Sidebar 228px.

Topbar 56px.

Padding main 32px.

Search max width hasta 1480px.

Settings max width ~1180px.

No imponer mismo ancho a todos los módulos.

---

# 31. MINI HERO SYSTEM

Cada módulo debe tener mini hero integrado en la página.

No una card blanca enorme.

Altura visual aprox. 100–140px.

Puede usar `hero-pattern.svg` con opacidad mínima.

Máximo 1 CTA principal.

---

# 32. INICIO

Título:

`Tu mercado, mejor informado`

Texto:

`Busca productos, compara precios y organiza tus compras.`

Search rápida protagonista.

Después mostrar:

- última lista;
- oportunidades;
- cambios de precio;
- estado de tiendas.

No 4 KPI cards idénticas sin jerarquía.

---

# 33. BUSCAR — PÁGINA MÁS IMPORTANTE

Debe sentirse como ecommerce de supermercado premium.

Desktop:

```text
240px filtros | catálogo
```

1440px:

4 cards por fila cuando sea razonable.

1700+:

5 solo si las cards mantienen legibilidad.

---

# 34. SEARCH HERO

Título:

`Encuentra productos`

Texto:

`Compara precios entre las tiendas disponibles.`

Search input integrado; eliminar card gigante alrededor.

Label técnico `Nombre o identificador externo` NO debe existir.

Placeholder:

`Busca arroz, leche, carne, detergente…`

---

# 35. PRODUCT CARD

Orden visual:

1. retailer badge;
2. imagen;
3. marca real;
4. nombre;
5. presentación;
6. precio;
7. precio anterior/descuento;
8. precio unitario;
9. frescura;
10. acciones.

Imagen:

- 180–200px desktop;
- `object-fit: contain`;
- usar imagen real del retailer;
- si falla usar `product-placeholder.svg`.

No stock photos inventadas.

---

# 36. PRODUCT CARD COPY

Marca:

no mostrar códigos como `SM` si no son marcas válidas.

Precio:

`$18.900`

Precio anterior:

`$22.500`

Precio unitario:

`$42.000/kg`

Actualización:

`Actualizado hace 18 min`

CTA principal:

`Ver en tienda`

Secundario:

`Agregar a lista`

Tercero:

`Comparar`

---

# 37. FILTERS

Secciones accordion:

- Tienda
- Categoría
- Marca
- Precio
- Presentación
- Disponibilidad
- Promociones

Counts si existen.

Mobile:

NO mostrar filtros inline.

Usar drawer/bottom sheet.

---

# 38. TOOLBAR

Izquierda:

`83 productos`

Derecha:

- ordenar;
- grid/list.

Paginación visible abajo.

---

# 39. MI MERCADO

Debe sentirse como gestor de listas, no formulario.

Desktop:

280px panel listas + contenido activo.

Active list header:

- nombre;
- cantidad de productos;
- estimado;
- acciones.

Items compactos con imagen 56px.

Cantidad mediante `− 2 +`.

---

# 40. COMPARAR

Pantalla comercial, no técnica.

No mostrar:

- F1;
- recall;
- precision;
- benchmark;
- confidence numérico.

Mover a `/dev/matching`.

User-facing:

2–4 productos comparados.

Mostrar:

- imagen;
- precio;
- presentación;
- precio unitario;
- tienda;
- promoción;
- historial;
- disponibilidad.

Badges:

- Más barato
- Mejor precio/kg
- Mayor descuento

---

# 41. ANÁLISIS

Estética fintech.

Título:

`Analiza antes de comprar`

No usar `ValueScore` visible.

Usar:

`Valor de compra`

Ejemplo:

`Muy conveniente · 82/100`

Explicación:

`Está 14% por debajo de su promedio y tiene buen precio por kilogramo.`

No afirmar “calidad” sin evidencia.

---

# 42. HISTORIAL

Debe parecer price tracker.

Orden:

1. búsqueda de producto;
2. producto actual;
3. KPIs;
4. gráfica grande;
5. eventos.

KPIs:

- actual;
- mínimo;
- promedio;
- máximo;
- cambio 30 días.

Fechas `es-CO`.

No mostrar scopes técnicos.

---

# 43. TIENDAS

Debe parecer gestión comercial.

Cada retailer card:

- nombre/logo;
- estado humano;
- productos guardados;
- última actualización;
- toggle;
- acciones.

Estados:

- Disponible
- Con problemas
- No disponible
- Sin confirmar

FastStore/API/provider code solo en `Detalles técnicos`.

---

# 44. IMPORTAR CATÁLOGO

Diseñar como tarea profesional:

árbol de categorías;

progress bar;

`342 de 781 productos`;

nuevos/actualizados/sin cambios.

No un bloque de checkboxes sin jerarquía.

---

# 45. AJUSTES

Desktop:

220px subnav.

Secciones:

- General
- Ubicación
- Asistente
- Datos
- Copias
- Avanzado

Solo una sección visible a la vez.

No página kilométrica.

---

# 46. OMNIROUTER

En interfaz usar únicamente:

`OmniRouter`

No OpenRouter si no corresponde.

Vista normal:

- estado;
- modelo;
- clave masked;
- uso;
- límite mensual.

Base URL y detalles HTTP en Avanzado.

---

# 47. BACKUPS / DATOS

Estética utility.

No cards gigantes.

KPIs compactos.

Backups en tabla/listado.

Danger zone aislada al final.

---

# 48. ASISTENTE

La IA es secundaria.

Nombre visible:

`Asistente de compras`

o simplemente:

`Asistente`

No `Asistente IA` como identidad principal.

Usar `assistant-shopping.svg`.

Desktop drawer ~400px.

Mobile full-screen/bottom sheet.

Quick prompts:

- ¿Cuál conviene más?
- ¿Dónde ahorro?
- Mejor precio por kilo

---

# 49. FORM CONTROLS

Crear/refinar componentes consistentes:

- UiInput
- UiSelect
- UiCheckbox
- UiRadio
- UiSwitch
- UiButton
- UiModal
- UiDrawer
- UiToast

No controles nativos visualmente incongruentes.

Touch target mínimo 40–44px.

Focus visible.

---

# 50. EMPTY STATES

Usar assets del ZIP.

Tamaño recomendado:

120–180px.

No convertir ilustración en hero.

Siempre incluir:

- título;
- explicación;
- CTA cuando proceda.

---

# 51. MOBILE NAV

Máximo 5 items:

- Inicio
- Buscar
- Mi mercado
- Análisis
- Más

`Más` contiene:

- Comparar
- Historial
- Tiendas
- Ajustes

FAB del asistente no debe tapar bottom nav.

---

# 52. MOBILE PRODUCT CARD

Puede ser horizontal:

- imagen 110–120px;
- datos a la derecha;
- precio claro;
- CTA.

No obligar a recorrer enormes cards verticales.

---

# 53. COPY PROFESIONAL

No usar:

- magia;
- increíble;
- potenciado por IA;
- inteligente repetido en cada copy;
- tecnología de próxima generación.

Usar frases directas.

Ejemplo bueno:

`Compara precios y elige la opción que más te conviene.`

---

# 54. ICONOGRAFÍA

Unificar iconos SVG outline.

No emojis.

No mezclar 3 familias.

---

# 55. PERFORMANCE

No empeorar bundle de forma significativa.

GSAP debe importarse solo donde se utiliza.

Lazy load de imágenes.

No precargar todas las fotografías.

No animar cientos de cards.

---

# 56. ACCESSIBILITY

Obligatorio:

- labels;
- contrastes;
- keyboard;
- focus visible;
- aria;
- focus trap;
- Escape en overlays;
- reduced motion;
- no depender solo de color.

Ejecutar herramienta de accessibility disponible.

---

# 57. UI KIT

Antes de migrar todas las pantallas:

actualiza `/dev/ui-kit`.

Debe mostrar:

- logo;
- mark;
- typography;
- palette;
- spacing;
- buttons;
- forms;
- badges;
- ProductCard;
- ListCard;
- RetailerCard;
- charts;
- modal;
- drawer;
- toast;
- skeleton;
- empty states;
- assistant FAB.

Aprueba visualmente el sistema antes de propagarlo.

---

# 58. ORDEN DE IMPLEMENTACIÓN

1. Integrar assets.
2. Favicon/PWA/shortcut.
3. Tokens CSS.
4. Typography.
5. Motion tokens.
6. Form controls.
7. Buttons/feedback.
8. Sidebar/topbar.
9. UI Kit.
10. Inicio.
11. Buscar.
12. Product detail.
13. Mi mercado.
14. Comparar.
15. Análisis.
16. Historial.
17. Tiendas.
18. Ajustes.
19. Asistente.
20. Mobile.
21. Accessibility.
22. Performance.
23. Playwright.
24. Visual audit.

---

# 59. PLAYWRIGHT — OBLIGATORIO

Validar:

## Buscar
buscar → filtrar → ordenar → paginar → detalle → Ver en tienda.

## Lista
crear → agregar → cantidad → optimizar → eliminar.

## Comparar
2–4 productos.

## Análisis
oportunidad → detalle.

## Historial
producto → rango → chart.

## Tiendas
catálogo → progreso.

## Ajustes
OmniRouter → Datos → Backups.

## Asistente
abrir → preguntar → respuesta → cerrar.

---

# 60. VIEWPORTS

Certificar:

- 390
- 430
- 768
- 1024
- 1280
- 1440
- 1920

---

# 61. VISUAL AUDIT

Generar BEFORE/AFTER.

No guardar screenshots de estados incorrectos.

Assertions semánticas obligatorias.

Detectar screenshots duplicados por hash.

---

# 62. SCREENSHOTS AFTER

Como mínimo:

- home desktop/mobile;
- search grid;
- search mobile;
- filters drawer;
- ProductCard;
- detail drawer;
- market;
- optimizer;
- compare;
- analysis;
- history;
- stores;
- import catalog;
- settings assistant;
- settings data;
- backups;
- assistant desktop/mobile;
- ui-kit;
- favicon/browser tab.

---

# 63. DEFINITION OF DONE — IDENTIDAD

- [ ] assets copiados correctamente
- [ ] logo principal único
- [ ] mark correcto
- [ ] favicon propio
- [ ] PWA icons
- [ ] shortcut icon
- [ ] no Angular favicon
- [ ] no clichés visuales de IA
- [ ] iconografía consistente
- [ ] sistema de tokens centralizado

---

# 64. DEFINITION OF DONE — MOTION

- [ ] motion tokens
- [ ] CSS para microinteracciones simples
- [ ] GSAP solo donde aporta valor
- [ ] drawer/modal fluidos
- [ ] resultados de búsqueda suaves
- [ ] actualización de precio perceptible pero sobria
- [ ] charts animados con moderación
- [ ] optimizer reveal
- [ ] no animaciones infinitas
- [ ] reduced-motion completo
- [ ] limpieza de GSAP contexts

---

# 65. DEFINITION OF DONE — SEARCH

- [ ] estética ecommerce premium
- [ ] imágenes reales
- [ ] placeholder correcto
- [ ] precio protagonista
- [ ] precio unitario
- [ ] descuentos
- [ ] filtros
- [ ] orden
- [ ] paginación
- [ ] product detail
- [ ] responsive
- [ ] microinteracciones

---

# 66. DEFINITION OF DONE — GLOBAL

Mercado Inteligente debe verse y sentirse como un producto construido específicamente para:

**buscar, comparar, analizar y planear compras.**

No como una plantilla.

No como una demo de IA.

No como un panel interno.

Pregunta final de revisión:

> ¿Esta interfaz sería creíble si la publicara una empresa de retail, fintech o price intelligence?

Si la respuesta es no, continúa refinando.

---

# 67. REPORT FINAL

Crear:

`docs/development/GOAL-19-VISUAL-IDENTITY-MOTION-REPORT.md`

Debe incluir:

- assets usados;
- rutas finales;
- tokens;
- componentes;
- microinteracciones;
- GSAP usage;
- reduced motion;
- Home;
- Search;
- Market;
- Compare;
- Analysis;
- History;
- Stores;
- Settings;
- Assistant;
- accessibility;
- performance;
- before/after;
- screenshots;
- tests;
- limitaciones.

---

# 68. REGLA FINAL

No interpretes este goal como:

`cambiar colores y poner animaciones`.

Debes transformar:

- identidad;
- sistema visual;
- jerarquía;
- componentes;
- interacción;
- motion;
- responsive;
- accesibilidad;
- consistencia.

Primero integra el Asset Pack y el UI Kit.

Después migra módulo por módulo.

Finalmente certifica con Playwright, auditoría visual, accessibility y performance.

No pidas confirmación entre subtareas.
