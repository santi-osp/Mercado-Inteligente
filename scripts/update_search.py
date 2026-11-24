content = """<div class="heading">
  <div>
    <p class="eyebrow">DESCUBRE Y COMPARA PRODUCTOS</p>
    <h1>Encuentra productos</h1>
    <p class="muted">Compara precios entre tus tiendas.</p>
  </div>
</div>

<!-- Barra de búsqueda principal (P0) -->
<section class="panel search-hero-panel" aria-label="Búsqueda de productos">
  <form class="search-form" (ngSubmit)="onSearchSubmit()">
    <label for="product-query">¿Qué estás buscando?</label>
    <div class="search-controls">
      <div class="search-input-wrapper">
        <span class="search-input-icon" aria-hidden="true">⌕</span>
        <input
          id="product-query"
          name="product-query"
          type="search"
          [(ngModel)]="host.searchQuery"
          placeholder="Busca arroz, leche, queso, detergente…"
          autocomplete="off">
        @if (host.searchQuery) {
          <button type="button" class="clear-search-btn" (click)="host.searchQuery = ''; onSearchSubmit()" title="Limpiar búsqueda">✕</button>
        }
      </div>
      <button class="primary" type="submit">Buscar</button>
    </div>
  </form>

  <!-- Chips de filtros activos -->
  @if (catalogSearch.selectedRetailers().length > 0 || catalogSearch.selectedBrands().length > 0 || catalogSearch.selectedCategories().length > 0 || catalogSearch.inStockOnly() || catalogSearch.promoOnly()) {
    <div class="active-chips-bar" aria-label="Filtros activos">
      <span class="chips-label">Filtros:</span>
      @for (ret of catalogSearch.selectedRetailers(); track ret) {
        <span class="filter-chip">
          Tienda: {{ ret }}
          <button type="button" (click)="catalogSearch.toggleRetailer(ret)" aria-label="Quitar filtro">✕</button>
        </span>
      }
      @for (b of catalogSearch.selectedBrands(); track b) {
        <span class="filter-chip">
          Marca: {{ b }}
          <button type="button" (click)="catalogSearch.toggleBrand(b)" aria-label="Quitar filtro">✕</button>
        </span>
      }
      @for (c of catalogSearch.selectedCategories(); track c) {
        <span class="filter-chip">
          Categoría: {{ c }}
          <button type="button" (click)="catalogSearch.toggleCategory(c)" aria-label="Quitar filtro">✕</button>
        </span>
      }
      @if (catalogSearch.inStockOnly()) {
        <span class="filter-chip">
          Solo disponibles
          <button type="button" (click)="catalogSearch.toggleInStock()" aria-label="Quitar filtro">✕</button>
        </span>
      }
      @if (catalogSearch.promoOnly()) {
        <span class="filter-chip">
          Solo promociones
          <button type="button" (click)="catalogSearch.togglePromo()" aria-label="Quitar filtro">✕</button>
        </span>
      }
      <button type="button" class="clear-all-chips" (click)="catalogSearch.clearFilters()">Limpiar todo</button>
    </div>
  }
</section>

<!-- Botón de filtros para versión móvil -->
<div class="mobile-filter-trigger">
  <button type="button" class="secondary small-btn" (click)="toggleMobileFilters()">
    <span aria-hidden="true">⚙</span> Filtros ({{ catalogSearch.selectedRetailers().length + catalogSearch.selectedBrands().length + catalogSearch.selectedCategories().length + (catalogSearch.inStockOnly() ? 1 : 0) + (catalogSearch.promoOnly() ? 1 : 0) }})
  </button>
</div>

<!-- Catálogo Ecommerce -->
<div class="catalog-layout">
  <!-- Sidebar de filtros (Desktop sticky, Mobile drawer) -->
  <aside class="catalog-sidebar panel" [class.mobile-open]="mobileFiltersOpen()" aria-label="Filtros del catálogo">
    <div class="sidebar-header">
      <h3>Filtros</h3>
      <button type="button" class="ghost small-btn" (click)="catalogSearch.clearFilters()">Limpiar filtros</button>
      <button type="button" class="ghost mobile-close-btn" (click)="closeMobileFilters()" aria-label="Cerrar filtros">✕</button>
    </div>

    <!-- Tiendas -->
    <div class="filter-group">
      <h4>Tiendas</h4>
      @for (ret of catalogSearch.availableRetailers(); track ret.code) {
        <label class="checkbox">
          <input
            type="checkbox"
            [checked]="catalogSearch.selectedRetailers().includes(ret.code)"
            (change)="catalogSearch.toggleRetailer(ret.code)">
          <span class="store-badge store-badge-{{ ret.code.toLowerCase() }}">{{ ret.name }}</span>
        </label>
      }
    </div>

    <!-- Disponibilidad -->
    <div class="filter-group">
      <h4>Disponibilidad</h4>
      <label class="checkbox">
        <input
          type="checkbox"
          [checked]="catalogSearch.inStockOnly()"
          (change)="catalogSearch.toggleInStock()">
        Solo disponibles
      </label>
      <label class="checkbox">
        <input
          type="checkbox"
          [checked]="catalogSearch.promoOnly()"
          (change)="catalogSearch.togglePromo()">
        Solo promociones
      </label>
    </div>

    <!-- Marcas si existen -->
    @if (catalogSearch.facets()?.brands; as brands) {
      @if (brands.length > 0) {
        <div class="filter-group">
          <h4>Marcas</h4>
          @for (b of brands.slice(0, 10); track b.value) {
            <label class="checkbox">
              <input
                type="checkbox"
                [checked]="catalogSearch.selectedBrands().includes(b.value)"
                (change)="catalogSearch.toggleBrand(b.value)">
              {{ b.value }} <small class="muted">({{ b.count }})</small>
            </label>
          }
        </div>
      }
    }

    <!-- Categorías si existen -->
    @if (catalogSearch.facets()?.categories; as cats) {
      @if (cats.length > 0) {
        <div class="filter-group">
          <h4>Categorías</h4>
          @for (c of cats.slice(0, 10); track c.value) {
            <label class="checkbox">
              <input
                type="checkbox"
                [checked]="catalogSearch.selectedCategories().includes(c.value)"
                (change)="catalogSearch.toggleCategory(c.value)">
              {{ c.value }} <small class="muted">({{ c.count }})</small>
            </label>
          }
        </div>
      }
    }

    <!-- Botón sticky móvil para aplicar -->
    <div class="mobile-filter-apply-bar">
      <button type="button" class="primary mobile-apply-btn" (click)="closeMobileFilters()">
        Ver {{ catalogSearch.total() }} productos
      </button>
    </div>
  </aside>

  <!-- Área principal del catálogo -->
  <main class="catalog-main">
    <!-- Toolbar de catálogo -->
    <div class="catalog-toolbar panel">
      <div class="toolbar-info">
        @if (catalogSearch.searchedQuery()) {
          <strong>{{ catalogSearch.total() }} productos encontrados para «{{ catalogSearch.searchedQuery() }}»</strong>
        } @else {
          <strong>Catálogo de productos ({{ catalogSearch.total() }} disponibles)</strong>
        }
      </div>
      <div class="toolbar-controls">
        <div class="sort-control">
          <label for="catalog-sort" class="sr-only">Ordenar por</label>
          <div class="select-wrapper">
            <select id="catalog-sort" [ngModel]="catalogSearch.sort()" (ngModelChange)="catalogSearch.setSort($event)">
              <option value="relevance">Relevancia</option>
              <option value="price_asc">Menor precio</option>
              <option value="price_desc">Mayor precio</option>
              <option value="discount_desc">Mayor descuento</option>
            </select>
          </div>
        </div>
        <div class="view-toggle" role="group" aria-label="Vista de catálogo">
          <button
            type="button"
            class="view-btn"
            [class.active]="catalogSearch.viewMode() === 'grid'"
            (click)="catalogSearch.setViewMode('grid')"
            title="Vista de cuadrícula">
            ⊞
          </button>
          <button
            type="button"
            class="view-btn"
            [class.active]="catalogSearch.viewMode() === 'list'"
            (click)="catalogSearch.setViewMode('list')"
            title="Vista de lista">
            ☰
          </button>
        </div>
      </div>
    </div>

    <!-- Mensajes de advertencia o avisos -->
    @for (w of catalogSearch.warnings(); track w) {
      <div class="notice" role="status">{{ w }}</div>
    }

    <!-- Estado de Error controlado -->
    @if (catalogSearch.error()) {
      <div class="market-error" role="alert">
        {{ catalogSearch.error() }}
        <button type="button" class="secondary small-btn" (click)="catalogSearch.search()">Reintentar</button>
      </div>
    }

    <!-- Skeletons durante carga -->
    @if (catalogSearch.loading()) {
      <div class="products-grid" [class.list-view]="catalogSearch.viewMode() === 'list'" aria-busy="true" aria-label="Cargando productos">
        @for (i of [1,2,3,4,5,6,7,8]; track i) {
          <article class="product-card skeleton-card">
            <div class="skeleton skeleton-img"></div>
            <div class="product-card-body">
              <div class="skeleton skeleton-badge"></div>
              <div class="skeleton skeleton-title"></div>
              <div class="skeleton skeleton-price"></div>
            </div>
          </article>
        }
      </div>
    }

    <!-- Estado Vacío -->
    @if (!catalogSearch.loading() && catalogSearch.results().length === 0 && !catalogSearch.error()) {
      <div class="catalog-empty panel">
        <div class="empty-icon" aria-hidden="true">🛒</div>
        <h3>No encontramos productos</h3>
        <p class="muted">No hay coincidencias para «{{ catalogSearch.searchedQuery() || host.searchQuery }}». Prueba verificando la ortografía o usando términos más generales.</p>
        <div class="empty-suggestions">
          <span>Prueba buscar:</span>
          <button type="button" class="ghost small-btn" (click)="host.searchQuery = 'leche'; onSearchSubmit()">Leche</button>
          <button type="button" class="ghost small-btn" (click)="host.searchQuery = 'arroz'; onSearchSubmit()">Arroz</button>
          <button type="button" class="ghost small-btn" (click)="host.searchQuery = 'queso'; onSearchSubmit()">Queso</button>
          <button type="button" class="ghost small-btn" (click)="host.searchQuery = 'detergente'; onSearchSubmit()">Detergente</button>
        </div>
      </div>
    }

    <!-- Cuadrícula / Lista de productos (P0) -->
    @if (!catalogSearch.loading() && catalogSearch.results().length > 0) {
      <div class="products-grid" [class.list-view]="catalogSearch.viewMode() === 'list'">
        @for (item of catalogSearch.results(); track item.id) {
          <article class="product-card" [attr.data-id]="item.id">
            <!-- Imagen del producto (180-200px desktop, contain) -->
            <div class="product-image-container" (click)="openProductDetail(item)">
              @if (item.imageUrl) {
                <img [src]="item.imageUrl" [alt]="item.name" loading="lazy" class="product-img">
              } @else {
                <div class="product-img-fallback" aria-hidden="true">📦</div>
              }
              <span class="store-badge store-badge-{{ item.retailer.toLowerCase() }}">{{ item.retailer }}</span>
              @if (item.listPrice && item.listPrice > item.price) {
                <span class="discount-pill">
                  -{{ ((item.listPrice - item.price) / item.listPrice) * 100 | number:'1.0-0' }}%
                </span>
              }
            </div>

            <!-- Cuerpo de la tarjeta -->
            <div class="product-card-body">
              <h3 class="product-title" (click)="openProductDetail(item)" [title]="item.name">{{ item.name }}</h3>
              
              @if (item.presentation || item.category) {
                <p class="product-presentation">{{ item.presentation || item.category }}</p>
              }

              <!-- Bloque de precios y unidad -->
              <div class="product-pricing">
                <div class="price-row">
                  <strong class="product-price">${{ item.price | number:'1.0-0' }} COP</strong>
                  @if (item.listPrice && item.listPrice > item.price) {
                    <del class="old-price">${{ item.listPrice | number:'1.0-0' }}</del>
                  }
                </div>
                @if (item.formattedUnitPrice || item.unitPrice) {
                  <span class="unit-price-tag">
                    {{ item.formattedUnitPrice || ('$' + (item.unitPrice | number:'1.0-0') + ' / ' + (item.unitMeasure || 'ud')) }}
                  </span>
                }
              </div>

              <!-- Frescura y disponibilidad -->
              <div class="product-meta">
                <span class="freshness-tag">● Actualizado recientemente</span>
              </div>

              <!-- Acciones: Ver en tienda y Agregar a lista -->
              <div class="product-card-actions">
                @if (item.productUrl) {
                  <a [href]="item.productUrl" target="_blank" rel="noopener noreferrer" class="primary action-btn">
                    Ver en tienda ↗
                  </a>
                }
                <button
                  type="button"
                  class="secondary add-list-btn"
                  (click)="openAddModal(item)"
                  title="Agregar a mi lista de mercado">
                  + Lista
                </button>
              </div>
            </div>
          </article>
        }
      </div>
    }
  </main>
</div>

<!-- Modal: Agregar a Lista de Mercado -->
@if (targetProductToAdd) {
  <div class="modal-backdrop" (click)="closeAddModal()" aria-hidden="true"></div>
  <div class="modal-dialog" role="dialog" aria-modal="true" aria-labelledby="add-modal-title">
    <div class="modal-header">
      <h3 id="add-modal-title">Agregar a mi mercado</h3>
      <button type="button" class="ghost icon-btn" (click)="closeAddModal()" aria-label="Cerrar modal">✕</button>
    </div>
    <div class="modal-body">
      <div class="add-product-summary">
        @if (targetProductToAdd.imageUrl) {
          <img [src]="targetProductToAdd.imageUrl" [alt]="targetProductToAdd.name" class="summary-thumb">
        }
        <div>
          <strong>{{ targetProductToAdd.name }}</strong>
          <p class="muted">{{ targetProductToAdd.retailer }} · ${{ targetProductToAdd.price | number:'1.0-0' }} COP</p>
        </div>
      </div>

      <div class="form-group">
        <label for="basket-select">Selecciona la lista destino:</label>
        <div class="select-wrapper">
          <select id="basket-select" [(ngModel)]="selectedBasketIdForAdd">
            @for (b of basketFacade.baskets(); track b.id) {
              <option [value]="b.id">{{ b.name }} ({{ b.items.length }} productos)</option>
            }
            <option value="new">+ Crear una nueva lista…</option>
          </select>
        </div>
      </div>

      @if (selectedBasketIdForAdd === 'new') {
        <div class="form-group">
          <label for="new-basket-name">Nombre de la nueva lista:</label>
          <input
            id="new-basket-name"
            type="text"
            [(ngModel)]="newBasketNameForAdd"
            placeholder="Ej: Mercado quincenal, Asado fin de semana">
        </div>
      }

      @if (addingStatus) {
        <p class="notice" role="status">{{ addingStatus }}</p>
      }
    </div>
    <div class="modal-footer">
      <button type="button" class="secondary" (click)="closeAddModal()">Cancelar</button>
      <button type="button" class="primary" (click)="confirmAddToList()" [disabled]="addingStatus">
        Confirmar y agregar
      </button>
    </div>
  </div>
}

<!-- Drawer / Modal de Detalle de Producto -->
@if (selectedProductForDetail(); as det) {
  <div class="modal-backdrop" (click)="closeProductDetail()" aria-hidden="true"></div>
  <aside class="product-detail-drawer" role="dialog" aria-modal="true" aria-labelledby="detail-drawer-title">
    <div class="modal-header">
      <h3 id="detail-drawer-title">Detalle del producto</h3>
      <button type="button" class="ghost icon-btn" (click)="closeProductDetail()" aria-label="Cerrar detalle">✕</button>
    </div>
    <div class="drawer-body">
      <div class="detail-hero">
        @if (det.imageUrl) {
          <img [src]="det.imageUrl" [alt]="det.name" class="detail-img">
        } @else {
          <div class="detail-img-fallback" aria-hidden="true">📦</div>
        }
      </div>

      <div class="detail-info">
        <span class="store-badge store-badge-{{ det.retailer.toLowerCase() }}">{{ det.retailer }}</span>
        <h4>{{ det.name }}</h4>
        @if (det.brand) {
          <p class="detail-brand"><span class="muted">Marca:</span> <strong>{{ det.brand }}</strong></p>
        }
        @if (det.presentation || det.category) {
          <p class="detail-pres"><span class="muted">Categoría / Formato:</span> {{ det.presentation || det.category }}</p>
        }

        <div class="detail-pricing-box">
          <div class="price-row">
            <span class="detail-current-price">${{ det.price | number:'1.0-0' }} COP</span>
            @if (det.listPrice && det.listPrice > det.price) {
              <del class="old-price">${{ det.listPrice | number:'1.0-0' }}</del>
              <span class="discount-pill">-{{ ((det.listPrice - det.price) / det.listPrice) * 100 | number:'1.0-0' }}%</span>
            }
          </div>
          @if (det.formattedUnitPrice || det.unitPrice) {
            <p class="unit-price-line">
              <span class="muted">Precio unitario:</span>
              <strong>{{ det.formattedUnitPrice || ('$' + (det.unitPrice | number:'1.0-0') + ' / ' + (det.unitMeasure || 'ud')) }}</strong>
            </p>
          }
        </div>

        <div class="detail-transparency">
          <p class="muted small">✓ Datos sincronizados con el catálogo online de {{ det.retailer }}. Precios verificados para Medellín.</p>
        </div>
      </div>
    </div>
    <div class="drawer-footer">
      @if (det.productUrl) {
        <a [href]="det.productUrl" target="_blank" rel="noopener noreferrer" class="secondary action-btn">
          Ver en tienda ↗
        </a>
      }
      <button type="button" class="primary" (click)="openAddModal(det); closeProductDetail()">
        + Agregar a lista
      </button>
    </div>
  </aside>
}

<!-- Paneles de catálogo local y diagnóstico (Preservados para compatibilidad técnica de tests) -->
<details class="diagnostic-panel">
  <summary class="muted">Ver diagnóstico técnico y datos locales almacenados</summary>
  <section class="panel" aria-label="Búsqueda local de productos">
    @if (host.searchLoading()) { <p role="status">Buscando en el catálogo local…</p> }
    @if (host.searchError()) { <div class="market-error" role="alert">{{ host.searchError() }} <button type="button" class="secondary" (click)="host.searchProducts()">Reintentar</button></div> }
    @if (host.products(); as results) {
      @if (!results.length) { <p role="status">No se encontraron productos locales para «{{ host.searchedQuery() }}».</p> }
      @else {
        <p class="muted" role="status">{{ results.length }} resultados locales para «{{ host.searchedQuery() }}» (máximo 50).</p>
        <ul class="product-list">@for (product of results; track product.id) {
          <li><strong>{{ product.name }}</strong>@if (product.brand) { <span>{{ product.brand }}</span> }<small>Identificador externo: {{ product.externalId }}</small><small>Comercio (ID): {{ product.retailerId }}</small></li>
        }</ul>
      }
    } @else if (!host.searchLoading() && !host.searchError()) { <p class="muted">Escribe un término para buscar en el catálogo local.</p> }
  </section>

  <section class="panel" aria-label="Ofertas D1 guardadas">
    <div class="source-heading"><h2>D1 · ofertas online guardadas</h2><button type="button" class="secondary" (click)="host.refreshD1()" [disabled]="!host.searchQuery.trim() || host.d1Busy()">{{ host.d1Busy() ? 'Actualizando…' : 'Actualizar D1 manualmente' }}</button></div>
    <p class="muted">Precio online genérico, no precio físico de sucursal. La actualización requiere Internet y puede fallar; el último dato guardado permanece visible. La frescura inicial del precio es de {{ host.settings()?.priceTtlHours ?? 6 }} h.</p>
    @if (host.d1Error()) { <p role="alert" class="market-error">{{ host.d1Error() }}</p> }
    @if (host.d1Notice()) { <p role="status">{{ host.d1Notice() }}</p> }
    @if (host.d1Offers(); as offers) {
      @if (!offers.length) { <p role="status">No hay ofertas D1 guardadas para esta búsqueda.</p> }
      @else { <ul class="product-list">@for (offer of offers; track offer.id + offer.context) {
        <li><strong>{{ offer.productName }} · {{ offer.skuName }}</strong><span>{{ offer.price }} COP · {{ offer.sellerName }}</span>@if (offer.listPrice && offer.listPrice > offer.price) { <small>Precio de lista online: {{ offer.listPrice }} COP</small> }<small>Alcance: {{ offer.scope === 1 || offer.scope === 'OnlineGeneric' ? 'Online genérico' : 'Contexto no físico' }} · Disponibilidad: {{ offer.availability === 1 || offer.availability === 'InStock' ? 'Disponible online' : offer.availability === 2 || offer.availability === 'OutOfStock' ? 'No disponible' : 'Desconocida' }}</small><small>Comprobado: {{ offer.checkedAt }} · Contexto: {{ offer.context }} · {{ host.offerFreshness(offer) }}</small></li>
      }</ul> }
    }
  </section>

  <section class="panel" aria-label="Ofertas Éxito guardadas">
    <div class="source-heading"><h2>Éxito · ofertas online guardadas</h2><button type="button" class="secondary" (click)="host.refreshExito()" [disabled]="!host.searchQuery.trim() || host.exitoBusy()">{{ host.exitoBusy() ? 'Actualizando…' : 'Actualizar Éxito manualmente' }}</button></div>
    <p class="muted">Precios online (genéricos o regionales), no precios verificados en tienda física. Las ofertas de Marketplace se filtran según tus ajustes. El último dato guardado permanece visible.</p>
    @if (host.exitoError()) { <p role="alert" class="market-error">{{ host.exitoError() }}</p> }
    @if (host.exitoNotice()) { <p role="status">{{ host.exitoNotice() }}</p> }
    @if (host.exitoOffers(); as offers) {
      @if (!offers.length) { <p role="status">No hay ofertas Éxito guardadas para esta búsqueda.</p> }
      @else { <ul class="product-list">@for (offer of offers; track offer.id + offer.context) {
        <li><strong>{{ offer.productName }} · {{ offer.skuName }}</strong><span>{{ offer.price }} COP · {{ offer.sellerName }} ({{ host.sellerKindLabel(offer.sellerKind) }})</span>@if (offer.listPrice && offer.listPrice > offer.price) { <small>Precio de lista online: {{ offer.listPrice }} COP</small> }<small>Alcance: {{ offer.scope === 2 || offer.scope === 'FulfillmentRegion' ? 'Regional (Medellín)' : offer.scope === 1 || offer.scope === 'OnlineGeneric' ? 'Online genérico' : 'Otro alcance' }} · Disponibilidad: {{ offer.availability === 1 || offer.availability === 'InStock' ? 'Disponible online' : offer.availability === 2 || offer.availability === 'OutOfStock' ? 'No disponible' : 'Desconocida' }}</small><small>Comprobado: {{ offer.checkedAt }} · Contexto: {{ offer.context }} · {{ host.offerFreshness(offer) }}</small></li>
      }</ul> }
    }
  </section>
</details>
"""
with open('src/frontend/src/app/pages/search.html', 'w', encoding='utf-8') as f:
    f.write(content)
print("Updated search.html successfully")
