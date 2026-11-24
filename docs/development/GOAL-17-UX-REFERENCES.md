# GOAL 17 — Informe de Referentes y Patrones UX

Este documento consolida la investigación de patrones de diseño UX/UI en aplicaciones de comercio electrónico, supermercados, comparadores de precios, listas de compras y analítica de consumo, con el fin de guiar la transformación de Mercado Inteligente de una herramienta administrativa a una experiencia de usuario de consumo premium y moderna.

---

## 1. Supermercados Ecommerce (Instacart, Mercadona, Carulla, Éxito)

### 1.1 ProductCard y Grilla de Productos
- **Patrón útil**: Tarjetas con proporción vertical equilibrada (1:1.3 a 1:1.4), contenedor de imagen con fondo neutro/blanco y padding para que los empaques respiren (object-fit: contain), badge de retailer y disponibilidad en la esquina superior, título limitado a 2-3 líneas con peso tipográfico legible, precio prominente en pesos colombianos () con tamaño grande (18-20px bold), precio anterior tachado discreto, precio por unidad de medida estandarizado ($/kg, $/g, $/ml, $/unid) en gris neutro, y botón de acción principal claro («Ver en tienda ↗») junto con acción secundaria («+ Lista»).
- **Problema que resuelve**: La sobrecarga visual y el desorden cuando se presentan productos con empaques y dimensiones dispares, y la dificultad para comparar si un producto de 900g es más barato que uno de 1000g.
- **Aplicación en Mercado Inteligente**: En Buscar (search.html), estandarizar las tarjetas en una cuadrícula responsive (1 columna en mobile 390px, 2 en tablet 768px, 3 en desktop 1024/1280px, 4 en 1440px y hasta 5 en 1920px). Incorporar cálculo visible de precio por unidad de medida y botón secundario rápido para agregar a «Mi mercado».
- **Qué NO copiar**: Carruseles promocionales infinitos, banners publicitarios invasivos o interfaces saturadas de badges de marketing irrelevantes.

### 1.2 Navegación Facetada y Filtros
- **Patrón útil**: Barra de búsqueda central (48-52px de altura) con placeholder contextual, botones de filtros rápidos tipo «chips» horizontales (Supermercado, Categoría, Rango de precio, En oferta, Solo disponibles), sidebar colapsable en desktop con recuento de resultados por faceta («Éxito (42)», «D1 (18)») y chips de filtros activos con botón «Limpiar todos».
- **Problema que resuelve**: La fricción de tener que escribir consultas largas o no saber qué filtros están activos ni cuántos resultados generará un criterio.
- **Aplicación en Mercado Inteligente**: Toolbar de búsqueda limpia con selector de ordenamiento (Menor precio, Mayor ahorro, Relevancia), facetas laterales en desktop y bottom sheet en mobile.
- **Qué NO copiar**: Árboles taxonómicos profundos de 5 niveles que abruman al comprador ordinario.

---

## 2. Comparadores de Precios y Agregadores (Google Shopping, Idealo, CamelCamelCamel)

### 2.1 Comparación de Ofertas y Productos Similares
- **Patrón útil**: Matriz o tabla comparativa lado a lado que destaca visualmente el «Mejor precio» con un badge sutil verde, mostrando claramente el ahorro absoluto () y porcentual frente a la media o la alternativa más cara. Foco en atributos tangibles: volumen/peso, marca, precio actual, tienda y frescura del dato.
- **Problema que resuelve**: Los usuarios no quieren calcular mentalmente la diferencia entre 3 tiendas; necesitan ver de inmediato cuál es la opción ganadora y por qué.
- **Aplicación en Mercado Inteligente**: En Comparar (compare.html), presentar una vista comparativa visual de 2 a 4 productos seleccionados. Eliminar por completo métricas técnicas internas de algoritmos (como recall, precision, F1 score, umbrales de embeddings) del flujo de usuario general, trasladando la experimentación algorítmica a /dev/matching.
- **Qué NO copiar**: Interfaces llenas de anuncios de afiliados, tablas abarrotadas con decenas de columnas técnicas incomprensibles.

### 2.2 Histórico y Tendencias de Precios
- **Patrón útil**: Gráfico lineal minimalista y limpio con selector de rango temporal (30 días, 3 meses, 1 año, Todo), tooltip interactivo con fecha exacta y precio en cada tienda, y tarjetas KPI sintéticas en la parte superior: Precio actual, Mínimo histórico, Máximo histórico y Variación en el último mes (+/- %).
- **Problema que resuelve**: La incertidumbre del consumidor sobre si el precio de hoy es realmente una buena oferta o si suele estar más bajo.
- **Aplicación en Mercado Inteligente**: En Historial (history.html), diseño centrado en el producto: buscador directo del producto a inspeccionar, seguido inmediatamente de los 4 KPIs clave y el gráfico de líneas con curvas suaves y diferenciación por tienda.
- **Qué NO copiar**: Gráficos de velas financieras complejas, indicadores técnicos tipo bolsa (MACD/RSI) o interfaces de terminal de trading.

---

## 3. Aplicaciones de Productividad y Listas de Mercado (AnyList, Bring!, Apple Reminders)

### 3.1 Gestión de Canasta y Listas de Mercado
- **Patrón útil**: Lista jerárquica con checkboxes táctiles grandes (mínimo 40px de área interactiva), tachado visual suave al marcar como comprado, conteo de items completados vs pendientes («8 de 12 items»), agrupación opcional por pasillo o categoría, y botón de acción flotante o superior destacado para optimizar la canasta.
- **Problema que resuelve**: Listas de compras que parecen tablas de bases de datos con botones de editar/borrar diminutos que son imposibles de usar con una sola mano en el pasillo del supermercado.
- **Aplicación en Mercado Inteligente**: En Mi mercado (market.html), priorizar la lista de compras del usuario como elemento protagonista. La configuración de ubicación/perfil geográfico pasa a un bloque secundario o colapsable. La optimización genera un resumen claro: «Canasta más barata en Tienda X: .000 (Ahorras .500)» con desglose amigable.
- **Qué NO copiar**: Gamificación excesiva, animaciones lentas que retrasan el marcado de items.

---

## 4. Dashboards Analíticos Modernos (Linear, Stripe, Shopify)

### 4.1 Jerarquía Visual sin «Panelitis»
- **Patrón útil**: Uso inteligente de espacios en blanco (escala 4/8/12/16/24/32/48/64), tipografía con contraste de pesos (Inter regular/medium/semibold/bold), sutiles separadores de línea (#E5E7EB) y agrupaciones por proximidad visual, en lugar de envolver cada dato individual en una tarjeta blanca con borde y sombra idénticos.
- **Problema que resuelve**: La fatiga visual conocida como «panelitis» o «card fatigue», donde la pantalla es un mosaico de 15 rectángulos blancos sin jerarquía de lectura clara.
- **Aplicación en Mercado Inteligente**: Reestructurar Análisis (nalysis.html), Ajustes (settings.html) y Tiendas (sources.html). Reemplazar términos como «ValueScore» por «Valor de compra» (0-100 con explicación humana de calidad/precio).
- **Qué NO copiar**: Estética excesivamente corporativa B2B o interfaces oscuras («dark mode forzoso») que se alejen de la frescura de una app de alimentos.

### 4.2 Asistente Inteligente Conversacional
- **Patrón útil**: Drawer lateral deslizante en desktop (380-420px) con backdrop suave y botón de cierre claro; en mobile, bottom sheet expandible con drag handle. Respuestas compactas con mini-tarjetas de productos embebidas que permiten interactuar (ver producto, agregar a lista) sin salir del chat. Sugerencias rápidas («prompts rápidos») tipo chips al inicio.
- **Problema que resuelve**: Modales centrados que bloquean la vista del contenido subyacente y chats que solo arrojan texto plano sin enlaces a los productos reales.
- **Aplicación en Mercado Inteligente**: En el Asistente IA (pp.html / eature-host.ts), transformar el modal invasivo en un drawer lateral integrado en desktop y bottom sheet en mobile, con chips de inicio («¿Dónde está la leche más barata?», «Optimizar mi canasta»).
- **Qué NO copiar**: Avatares robotizados extravagantes o mensajes interminables generados por IA que no ofrecen respuestas directas al ahorro.

---

## 5. Resumen de Decisiones de Diseño para Mercado Inteligente

| Módulo | Enfoque Anterior (Técnico/Admin) | Enfoque Nuevo (Consumo Humano) | Referente Principal |
|---|---|---|---|
| **Buscar** | Tabla/Grid plano, pocos datos visuales, sin $/kg claro | Cuadrícula ecommerce rica, badges de tienda, $/unidad, drawer de detalle | Instacart / Mercadona |
| **Mi mercado** | Foco en perfiles geográficos, formularios densos | Foco en la lista de compras, tachado ágil, optimizador visual claro | Bring! / AnyList |
| **Comparar** | Métricas ML (F1, Recall, embeddings), tablas duras | Comparación visual de 2-4 productos, sandbox técnico a /dev/matching | Idealo / Google Shopping |
| **Análisis** | ValueScore crudo, gráficos aislados, panelitis | Oportunidades de ahorro, «Valor de compra» explicado, KPIs limpios | Stripe / Shopify |
| **Historial** | Filtros complejos desconectados, gráfico pequeño | Búsqueda directa de producto, 4 KPIs clave, gráfico temporal amplio | CamelCamelCamel |
| **Tiendas** | «Fuentes» con terminología técnica de scraping/APIs | Tarjetas de tiendas, estado humano (Disponible/Con problemas), progreso claro | Statuspage / Shopify App Store |
| **Ajustes** | Pantalla plana larga con diagnósticos mezclados | Subnavegación limpia (General, Ubicación, IA, Datos, Copias, Avanzado) | Linear / Stripe Settings |
| **Chat IA** | Modal centrado pesado | Drawer lateral desktop (380-420px) y bottom sheet mobile con ProductCards | Linear / Perplexity |
