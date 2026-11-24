**COMPARADOR INTELIGENTE DE MERCADO**

UI/UX y Design System

Sistema visual, navegación, componentes, estados y especificación de pantallas.

| **Documento** | UI/UX & Design System                                                                    |
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

1\. Dirección de diseño

2\. Principios UX

3\. Tokens visuales

4\. Tipografía e iconografía

5\. Layout y responsive

6\. Navegación

7\. Componentes base

8\. Componentes de dominio

9\. Estados y feedback

10\. Pantallas principales

11\. Flujos UX detallados

12\. Microcopy y lenguaje

13\. Accesibilidad

14\. Modo oscuro

15\. Checklist de fidelidad visual

# 1. Dirección de diseño

La identidad visual recomendada es “minimalista premium de datos”: limpia como una app financiera, cercana como una app de consumo y suficientemente técnica para transmitir confianza. No debe parecer un ecommerce ni un dashboard empresarial saturado.

| **Atributo**         | **Dirección**                                                      |
|----------------------|--------------------------------------------------------------------|
| Personalidad         | Confiable, clara, inteligente, sobria, accesible                   |
| Densidad             | Media: mucha información, pero agrupada y respirable               |
| Forma                | Bordes suaves; pocas sombras; superficies planas                   |
| Color                | Verde profundo como marca; semánticos reservados para estados      |
| Movimiento           | Sutil y funcional; 120–220 ms                                      |
| Ilustración          | No necesaria en V1; priorizar iconos y datos                       |
| Imágenes de producto | Útiles pero secundarias; nunca dominar la decisión                 |
| Gráficas             | Simples, sin decoración; historial de precio con foco en tendencia |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Sensación objetivo</strong></p>
<p>“Entiendo rápidamente dónde comprar y por qué, sin sentir que estoy usando una herramienta técnica.”</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>

# 2. Principios UX

Cada interacción debe reducir decisiones innecesarias y hacer visible la incertidumbre cuando importa.

- La acción principal de cada pantalla debe ser obvia.

- No mezclar exactos y sustitutos en una sola lista sin separación visual.

- Siempre ofrecer comparación por unidad cuando cambie la presentación.

- La recomendación muestra “por qué”, no solo “qué”.

- El modo básico debe cubrir el 90% del uso; opciones avanzadas van detrás de “Ajustar”.

- Los estados de frescura/confianza se expresan con texto + icono + color; nunca solo color.

- Los datos faltantes se muestran como “No disponible/No verificado”, nunca como cero.

- Las acciones destructivas requieren confirmación y los cambios de matching pueden deshacerse.

# 3. Tokens visuales

Los tokens se implementarán como CSS custom properties y serán la única fuente de valores visuales. Esto facilita dark mode y evita estilos inconsistentes.

## 3.1 Paleta principal

| **Token**   | **HEX**      | **Uso**                                          |
|-------------|--------------|--------------------------------------------------|
| Primary 600 | **\#167D5A** | Acción principal, enlaces seleccionados          |
| Primary 800 | **\#0F5F44** | Títulos, énfasis, hover                          |
| Primary 50  | **\#E8F4EF** | Fondos suaves de marca                           |
| Canvas      | **\#F7F8FA** | Fondo de aplicación                              |
| Surface     | **\#FFFFFF** | Tarjetas, modales                                |
| Text 900    | **\#111827** | Texto principal                                  |
| Text 500    | **\#6B7280** | Texto secundario                                 |
| Border      | **\#E5E7EB** | Bordes/divisores                                 |
| Success     | **\#16A34A** | Ahorro, healthy, verificado                      |
| Info        | **\#2563EB** | Información/contexto                             |
| Warning     | **\#D97706** | Stale, ambigüedad                                |
| Error       | **\#DC2626** | Fallo, down, acción destructiva                  |
| AI          | **\#7C3AED** | Features de IA, sin usarlo para estados normales |

## 3.2 Espaciado, radios y elevación

| **Token** | **Valor**                 | **Uso**                             |
|-----------|---------------------------|-------------------------------------|
| space-1   | 4 px                      | Microseparaciones                   |
| space-2   | 8 px                      | Icono-texto / chips                 |
| space-3   | 12 px                     | Controles internos                  |
| space-4   | 16 px                     | Padding estándar                    |
| space-5   | 20 px                     | Tarjetas compactas                  |
| space-6   | 24 px                     | Tarjetas/paneles                    |
| space-8   | 32 px                     | Secciones                           |
| space-12  | 48 px                     | Separación mayor                    |
| radius-sm | 8 px                      | Inputs/chips                        |
| radius-md | 12 px                     | Botones/tarjetas                    |
| radius-lg | 16 px                     | Paneles/modales                     |
| shadow-1  | 0 1px 2px rgba(0,0,0,.05) | Uso excepcional; no como decoración |

# 4. Tipografía e iconografía

La tipografía prioriza legibilidad numérica y jerarquía. Se recomienda Inter Variable empaquetada en la aplicación para no depender de Google Fonts en runtime.

| **Estilo**  | **Tamaño/LH** | **Peso**    | **Uso**                         |
|-------------|---------------|-------------|---------------------------------|
| Display     | 32/40         | 700         | Portadas/estados vacíos grandes |
| H1          | 24/32         | 700         | Título de pantalla              |
| H2          | 20/28         | 650         | Secciones importantes           |
| H3          | 16/24         | 650         | Tarjetas/subsecciones           |
| Body        | 14/21         | 400         | Texto general                   |
| Body Strong | 14/21         | 600         | Etiquetas/datos                 |
| Small       | 12/18         | 400         | Metadatos/frescura              |
| Numeric XL  | 28/34         | 700 tabular | Total de mercado/ahorro         |
| Numeric     | 14/20         | 600 tabular | Precios/unidades                |

Iconografía: Lucide (stroke 1.75–2 px), 16/20/24 px. No mezclar familias. Los iconos nunca sustituyen texto en acciones críticas.

# 5. Layout y responsive

Aunque V1 se usa en PC, la interfaz se diseñará mobile-first para preservar claridad y permitir una evolución futura sin rediseño.

| **Breakpoint** | **Ancho**    | **Comportamiento**                                         |
|----------------|--------------|------------------------------------------------------------|
| Compact        | \< 640 px    | 1 columna, navegación inferior futura                      |
| Medium         | 640–1023 px  | 1–2 columnas; filtros en drawer                            |
| Desktop        | 1024–1439 px | Sidebar 240 px + contenido                                 |
| Wide           | ≥ 1440 px    | Sidebar + contenido máximo 1280 px; panel lateral opcional |

- Content max-width: 1280 px.

- Gutter desktop: 24–32 px.

- Grid de 12 columnas para vistas complejas.

- No estirar tablas/tarjetas indefinidamente en monitores grandes.

- Paneles de detalle pueden usar 360–420 px de ancho.

# 6. Navegación

La V1 usará sidebar persistente en escritorio. No se recomienda una topbar con demasiadas secciones.

| **Orden** | **Destino** | **Función**                                |
|-----------|-------------|--------------------------------------------|
| 1         | Inicio      | Resumen, acciones rápidas, estado de datos |
| 2         | Buscar      | Catálogo y comparación                     |
| 3         | Mi mercado  | Lista activa y mercados guardados          |
| 4         | Historial   | Precios y mercados previos                 |
| 5         | Fuentes     | D1/Éxito/Ara, sync y salud                 |
| 6         | Ajustes     | Ubicación, comparación, IA, Google, datos  |

En la parte inferior del sidebar: estado “Local”, indicador de conexión/última sincronización y acceso a diagnóstico. “IA” no es una sección principal; aparece como capacidad contextual y en Ajustes.

# 7. Componentes base

Todos los componentes deben tener estados default, hover, focus-visible, disabled, loading y error cuando aplique.

| **Componente**   | **Especificación**                                                               |
|------------------|----------------------------------------------------------------------------------|
| Button Primary   | 44 px alto; fondo Primary 600; radio 10–12 px; una acción dominante por contexto |
| Button Secondary | Borde Border; fondo Surface; mismo alto                                          |
| Button Ghost     | Sin borde; acciones terciarias                                                   |
| Icon Button      | 40×40; tooltip obligatorio cuando no haya label                                  |
| Input            | 44 px; label persistente; helper/error debajo                                    |
| Search           | 48 px; icono izquierda; clear; shortcut opcional “/”                             |
| Select/Combobox  | Búsqueda para listas largas; teclado completo                                    |
| Chip             | Filtro/toggle; no usar como botón destructivo                                    |
| Switch           | Solo ajustes binarios con efecto inmediato                                       |
| Tabs             | Máximo 4–5 opciones; exactos/similares/sustitutos es caso ideal                  |
| Card             | Padding 20–24; borde 1 px; sombra casi nula                                      |
| Dialog           | Confirmaciones/desambiguación; máximo 640 px                                     |
| Drawer           | Filtros y detalles secundarios                                                   |
| Toast            | Éxitos no críticos; 4–6 s; no para errores que requieren acción                  |
| Skeleton         | Listas/cards mientras llega contenido; evitar spinner de página completa         |

# 8. Componentes de dominio

Estos componentes constituyen el lenguaje visual específico del comparador.

## 8.1 Product Row / Product Card

| **Zona**  | **Contenido**                                            |
|-----------|----------------------------------------------------------|
| Identidad | Imagen 56–64 px, nombre, marca, presentación             |
| Precio    | Precio actual grande; precio anterior tachado si procede |
| Unidad    | \$/kg, \$/L o \$/unidad                                  |
| Fuente    | Logo/nombre de cadena + contexto                         |
| Frescura  | “Actualizado hace 2 h” + estado                          |
| Match     | Exacto / tamaño similar / sustituto                      |
| Acciones  | Agregar, comparar, ver detalle                           |

## 8.2 Price Confidence Badge

| **Badge**       | **Color** | **Texto de apoyo**                      |
|-----------------|-----------|-----------------------------------------|
| Exacto          | Verde     | EAN/regla fuerte                        |
| Similar         | Azul      | Misma variante con presentación cercana |
| Sustituto       | Morado    | Producto alternativo                    |
| Stale           | Ámbar     | Precio antiguo                          |
| No verificado   | Gris      | Alcance físico no confirmado            |
| Error de fuente | Rojo      | Provider no disponible                  |

## 8.3 Store Plan Card

Tarjeta usada en resultados de optimización. Debe mostrar total, número de tiendas, recorrido, ahorro frente a baseline, cobertura de productos y una explicación corta. La tarjeta recomendada usa borde Primary; “máximo ahorro” no se pinta automáticamente de verde si implica mucho recorrido.

# 9. Estados y feedback

Los estados semánticos tienen reglas estrictas para evitar ruido visual.

| **Estado**  | **Visual**                  | **Regla**                             |
|-------------|-----------------------------|---------------------------------------|
| Healthy     | Punto/check verde + texto   | Provider responde y contrato válido   |
| Degraded    | Ámbar                       | Función parcial/caché/stale           |
| Down        | Rojo                        | Provider no responde o contrato roto  |
| AI active   | Morado                      | Solo cuando IA intervino realmente    |
| Loading     | Skeleton                    | Mantener layout estable               |
| Empty       | Icono + explicación + CTA   | No usar ilustración decorativa grande |
| Offline     | Banner discreto persistente | Explicar que se usa caché             |
| Stale price | Ámbar pequeño               | No bloquear; mostrar edad del dato    |

# 10. Pantallas principales

Las siguientes especificaciones son suficientemente concretas para crear Figma o implementar directamente.

### 10.1 Inicio

┌ Sidebar ─────┬──────────────────────────────────────────────┐  
│ Inicio │ Buenos días \[Actualizar\] │  
│ Buscar │ 📍 Casa · Medellín · Máx. 2 tiendas │  
│ Mi mercado │ │  
│ Historial │ \[ Buscar producto o marca… \] │  
│ Fuentes │ │  
│ Ajustes │ Mercado activo │  
│ │ 12 productos Est. \$186.400 \[Continuar\] │  
│ │ │  
│ │ Últimos precios / Ofertas relevantes │  
│ Local ● │ D1 ● Éxito ● Ara ◐ │  
└──────────────┴──────────────────────────────────────────────┘

Inicio prioriza retomar el mercado actual, búsqueda y salud de datos. No debe ser un dashboard de métricas sin acción.

### 10.2 Buscar / resultados

\[ arroz diana 1 kg \] \[Filtros\]  
Exactos (3) Tamaños similares (5) Sustitutos (12)  
────────────────────────────────────────────────────────  
Arroz Diana Premium 1 kg D1 \$5.490 \$5.490/kg  
Exacto · actualizado 1 h \[+ Agregar\] \[Comparar\]  
────────────────────────────────────────────────────────  
Arroz Diana Premium 900 g Éxito \$5.190 \$5.767/kg  
Tamaño similar · -10% \[+ Agregar\] \[Comparar\]

### 10.3 Mi mercado

Mi mercado · 12 productos \[Optimizar\]  
Máx. tiendas \[2▼\] Estrategia \[Equilibrada▼\] 📍 Casa  
──────────────────────────────────────────────────────────  
Leche entera 2 L Exacto \$… \[Editar\] \[×\]  
Huevos 30 Sustitutos ✓ \$… \[Editar\] \[×\]  
Arroz 2 kg Cualquier \$… \[Editar\] \[×\]  
──────────────────────────────────────────────────────────  
Estimado actual: \$… 12/12 cubiertos

### 10.4 Resultado optimizado

⭐ Recomendación · Equilibrada  
TOTAL \$176.100 AHORRO \$24.700 2 TIENDAS · 4,6 km  
──────────────────────────────────────────────────────────  
1. D1 / contexto Medellín \$104.300  
8 productos · 1,2 km  
2. Éxito \$71.800  
4 productos · 3,4 km  
\[Ver productos\] \[Abrir ruta en Google Maps\]  
  
Por qué: agregar una tercera tienda ahorra solo \$3.200  
y añade 6,8 km de recorrido.  
  
Alternativas: \[Máximo ahorro\] \[1 tienda\] \[Menor recorrido\]

### 10.5 Detalle de producto

← Arroz Diana Premium 1 kg Exacto ●  
\$5.490 · D1 \$5.490/kg  
Actualizado hace 1 h · Precio regional online  
──────────────────────────────────────────────────────────  
Historial 30 días ─╲\_\_╱──╲\_\_\_  
Mín \$5.290 · Prom \$5.610 · Máx \$5.990  
  
Ofertas por tienda / seller  
D1 … Éxito …  
  
Coincidencias y sustitutos \[Gestionar\]

### 10.6 Ajustes

Ajustes  
General \| Comparación \| Ubicación \| IA \| Datos \| Avanzado  
──────────────────────────────────────────────────────────  
IA con OpenRouter  
\[●\] Habilitada  
API key ••••••••••••••••• \[Cambiar\]  
Modelo \[ seleccionar modelo ▼ \]  
\[✓\] Matching ambiguo \[✓\] Sustitutos \[✓\] Explicaciones  
Presupuesto aviso mensual \[5.00 USD\]

# 11. Flujos UX detallados

Las transiciones deben evitar modales innecesarios y preservar contexto.

## 11.1 De búsqueda a comparación

1.  El usuario escribe; resultados locales aparecen inmediatamente.

2.  Si se requiere refresco, mostrar “Actualizando precio…” sin borrar resultados existentes.

3.  Exactos aparecen primero; no expandir sustitutos por defecto cuando existe exacto.

4.  Comparar abre un panel o vista con 2–4 variantes seleccionadas.

5.  Agregar conserva las reglas de sustitución del ítem, no solo el SKU puntual.

## 11.2 De cesta a optimización

6.  Validar ítems sin candidatos antes de optimizar.

7.  Mostrar progreso por etapas: resolviendo productos → comparando tiendas → calculando rutas.

8.  Si Google no está configurado, completar optimización de precio y avisar que no se ponderó ruta.

9.  Presentar recomendación y alternativas en el mismo resultado.

10. Cualquier ajuste recalcula sin perder la cesta.

# 12. Microcopy y lenguaje

El tono debe ser directo, neutral y orientado a decisión. Evitar “IA mágica”, “mejor precio garantizado” o afirmaciones que excedan la evidencia.

| **Evitar**                        | **Preferir**                                           |
|-----------------------------------|--------------------------------------------------------|
| “Mejor precio garantizado”        | “Mejor opción según los precios disponibles”           |
| “Este D1 cuesta…”                 | “Precio online/regional observado para D1”             |
| “La IA decidió…”                  | “Análisis asistido por IA”                             |
| “Error 502”                       | “Éxito no respondió; usamos el último dato disponible” |
| “Sin stock” cuando es desconocido | “Disponibilidad no verificada”                         |
| “Producto igual” por fuzzy        | “Posible coincidencia”                                 |

# 13. Accesibilidad

Objetivo mínimo: WCAG 2.2 AA en la interfaz principal.

- Contraste ≥4.5:1 para texto normal.

- Focus-visible claro en todos los controles.

- Navegación completa por teclado.

- Targets interactivos ≥40×40 px; preferencia 44 px.

- No usar color como única señal.

- Tablas con encabezados y alternativas responsive.

- aria-live para actualizaciones importantes no disruptivas.

- Respeto a prefers-reduced-motion.

- Formato COP legible y números tabulares.

# 14. Modo oscuro

No es requisito de la primera entrega funcional, pero los tokens deben soportarlo desde el inicio. Se recomienda implementarlo después de estabilizar el tema claro.

| **Token claro** | **Valor claro** | **Token oscuro sugerido** |
|-----------------|-----------------|---------------------------|
| Canvas          | \#F7F8FA        | \#0B1220                  |
| Surface         | \#FFFFFF        | \#111827                  |
| Text            | \#111827        | \#F9FAFB                  |
| Muted           | \#6B7280        | \#9CA3AF                  |
| Border          | \#E5E7EB        | \#253047                  |
| Primary         | \#167D5A        | \#3DB889                  |

# 15. Checklist de fidelidad visual

Antes de aprobar cualquier pantalla, comprobar estas reglas.

- Una sola acción primaria dominante por bloque.

- No más de 3 colores semánticos visibles simultáneamente salvo tabla/leyenda.

- Precios alineados y con cifras tabulares.

- Metadatos de fuente/frescura presentes sin competir con el precio.

- Separación explícita entre exacto/similar/sustituto.

- No más de 2 niveles de tarjetas anidadas.

- Filtros avanzados colapsados por defecto.

- Skeleton en cargas; no reflow brusco.

- Tooltips no contienen información indispensable.

- Cada estado de provider tiene texto, no solo punto de color.

- Responsive comprobado en 360, 768, 1024, 1440 y 1920 px.

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><p><strong>Criterio final</strong></p>
<p>La UI está bien diseñada si el usuario puede responder en menos de 10 segundos: “¿qué opción recomienda la app, cuánto cuesta, cuánto ahorro y qué sacrifico para conseguir ese ahorro?”.</p></th>
</tr>
</thead>
<tbody>
</tbody>
</table>
