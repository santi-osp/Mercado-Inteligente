<p align="center">
  <img src="docs/images/banner.svg" alt="Mercado Inteligente RC1 Banner" width="100%">
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Architecture-Local--First%20PWA-065F46?style=for-the-badge&logo=pwa&logoColor=white" alt="Local-First PWA">
  <img src="https://img.shields.io/badge/.NET%20Core-10.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 10">
  <img src="https://img.shields.io/badge/Angular-20%20Standalone-DD0031?style=for-the-badge&logo=angular&logoColor=white" alt="Angular 20">
  <img src="https://img.shields.io/badge/Database-SQLite%20Local-003B57?style=for-the-badge&logo=sqlite&logoColor=white" alt="SQLite">
  <img src="https://img.shields.io/badge/Retailers-D1%20%7C%20Éxito%20%7C%20Ara-F59E0B?style=for-the-badge" alt="Retailers">
  <img src="https://img.shields.io/badge/Release-RC1%20Stable-10B981?style=for-the-badge" alt="RC1 Stable">
</p>

<p align="center">
  <img src="docs/images/divider.svg" width="100%">
</p>

## 🛒 Visión General

**Mercado Inteligente (RC1)** es una solución de escritorio y PWA **local-first** diseñada para el análisis, normalización, comparación y optimización de listas de compras en las principales cadenas de supermercados de Colombia (**Tiendas D1**, **Almacenes Éxito** y **Tiendas Ara**).

A diferencia de los servicios comerciales tradicionales basados en la nube, Mercado Inteligente opera con **privacidad absoluta**: todos los catálogos de productos, históricos de precios, listas de mercado y combinaciones optimizadas residen exclusivamente en la base de datos SQLite de tu máquina local. Los conectores externos hacia los supermercados se activan de forma controlada y opcional para refrescar tarifas cuando el usuario lo decide.

---

## 📸 Recorrido Visual (UI Showcase)

### 1. Panel Principal & Comparativa Multi-Tienda
Vista unificada de productos con cálculo automático de valor por unidad de medida (precio por gramo o mililitro) para comparativas directas y justas entre marcas.

| Panel Principal (Desktop) | Comparador Multi-Tienda Directo |
| :---: | :---: |
| <img src="docs/images/screenshots/home-desktop.png" alt="Home Dashboard" width="500"> | <img src="docs/images/screenshots/compare-stores.png" alt="Comparador Tiendas" width="500"> |

<br>

### 2. Optimizador Inteligente de Canasta y Análisis Histórico
El algoritmo analiza tu lista de mercado y calcula la estrategia de compra más económica: compra centralizada en una sola tienda vs. compra dividida óptima con desglose de ahorro en pesos colombianos ($ COP).

| Optimizador de Compras y Canasta | Histórico de Precios e Inflación |
| :---: | :---: |
| <img src="docs/images/screenshots/cart-optimizer.png" alt="Optimizador de Canasta" width="500"> | <img src="docs/images/screenshots/price-history.png" alt="Historial de Precios" width="500"> |

<br>

### 3. Experiencia Móvil / PWA Responsive
Diseño totalmente adaptable a pantallas táctiles de smartphone con modo oscuro y atajos rápidos de escaneo y consulta.

<p align="center">
  <img src="docs/images/screenshots/home-mobile.png" alt="Mobile Experience" width="340">
</p>

<p align="center">
  <img src="docs/images/divider.svg" width="100%">
</p>

## ⚡ Capacidades Principales

- **📊 Normalizador de Unidades:** Convierte automáticamente presentaciones dispersas (libras, kilos, onzas, litros, mililitros) a un valor unitario estándar para evitar trampas de empaque.
- **🎯 Algoritmo de Compra Óptima:**
  - *Opción Monotienda:* La tienda más barata para comprar toda tu lista en un solo viaje.
  - *Opción Multi-tienda:* La combinación matemática perfecta entre D1, Éxito y Ara para maximizar tu ahorro.
- **🔒 Privacidad Local-First:** No requiere cuentas externas, ni almacenamiento en nubes públicas, ni rastreadores de publicidad.
- **📈 Historial de Precios y Tendencias:** Registro cronológico de cambios de precio para identificar promociones reales vs falsos descuentos.
- **⚡ Proceso Unificado:** Script en PowerShell que compila y orquesta el backend ASP.NET Core y el cliente Angular en un solo clic.

---

## 🏛️ Arquitectura del Sistema

```mermaid
graph TD
    Client[Angular 20 Standalone PWA] -->|HTTP / Localhost| API[ASP.NET Core 10 Web API]
    API --> NormEngine[Motor de Normalización de Unidades]
    API --> OptEngine[Optimizador de Canasta Heurístico]
    API --> SyncEngine[Conectores de Sincronización Opcionales]
    
    SyncEngine -.->|Lectura Externa| D1[Catálogo D1]
    SyncEngine -.->|Lectura Externa| Exito[Catálogo Éxito]
    SyncEngine -.->|Lectura Externa| Ara[Catálogo Ara]
    
    API --> LocalDB[(SQLite Local Storage)]
```

---

## 🚀 Inicio Rápido en Un Solo Paso

Para iniciar la aplicación completa (backend compilado y frontend Angular sirviendo en `http://localhost:5000`):

```powershell
.\scripts\run-app.ps1
```

*Si el frontend aún no ha sido compilado, el script detectará las dependencias y lo compilará automáticamente antes de arrancar.*

### Modo Desarrollo (Hot-Reload)
Para desarrolladores que deseen modificar el frontend en caliente con proxy automático:
```powershell
.\scripts\run-app.ps1 -Dev
```
- **Backend API:** `http://127.0.0.1:5000`
- **Angular Dev Server:** `http://localhost:4200`

---

## 📋 Requisitos del Entorno

- **Sistema Operativo:** Windows 10 o Windows 11 (x64)
- **.NET SDK:** Versión 10.0 o superior
- **Node.js:** Versión 20.x, 22.x o 24.x LTS
- **PowerShell:** 5.1 o PowerShell 7+

---

## 📁 Estructura del Repositorio

```text
Mercado_Inteligente/
├── artifacts/                   # Binarios y builds compilados listos para empaque
├── assets/                      # Iconografía PWA, logotipos y prototipos
├── docs/
│   ├── images/
│   │   ├── banner.svg           # Hero banner vectorial
│   │   ├── divider.svg          # Separador decorativo
│   │   └── screenshots/         # Capturas oficiales de auditoría de interfaz
│   ├── development/             # Reportes de avance y trazabilidad
│   └── ui-audit/                # Evidencia fotográfica de auditorías visuales
├── scripts/
│   ├── build.ps1                # Compilación integral de la solución
│   ├── run-app.ps1              # Lanzador rápido producción / dev
│   └── test.ps1                 # Ejecución de suites de prueba unitarias
├── src/
│   ├── backend/                 # ASP.NET Core Web API (.NET 10)
│   └── frontend/                # Angular 20 Standalone Components & PWA
├── tests/                       # Pruebas automatizadas de integración y unidad
└── README.md
```

---

## 👨‍💻 Autor y Contacto

Desarrollado por **Santiago Ospina Arango**  
- **GitHub:** [@santi-osp](https://github.com/santi-osp)  
- **Especialidad:** Soluciones Local-First · .NET Core / Angular · Automatización de Datos

---

<p align="center">
  <sub>Mercado Inteligente © 2026 · Release Candidate 1 · Construido para la comunidad</sub>
</p>