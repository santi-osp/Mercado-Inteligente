const fs = require('fs');
const path = require('path');

const content = `<div class="heading">
  <div>
    <p class="eyebrow">DESCUBRE Y COMPARA PRODUCTOS</p>
    <h1>Encuentra productos</h1>
    <p class="muted">Busca directamente en D1 y Éxito, compara precios y abre el producto en la tienda.</p>
  </div>
</div>

<!-- Barra de búsqueda principal -->
<section class="panel search-hero-panel" aria-label="Búsqueda de productos">
  <form class="search-form" (ngSubmit)="onSearchSubmit()">
    <label for="product-query">Nombre o identificador externo</label>
    <div class="search-controls">
      <input
        id="product-query"
        name="product-query"
        type="search"
        [(ngModel)]="host.searchQuery"
        placeholder="Busca arroz, leche, queso, detergente…"
        autocomplete="off">
      <button class="primary" type="submit">Buscar</button>
    </div>
  </form>
</section>

<!-- Catálogo Ecommerce -->
<div class="catalog-layout">
  <!-- Sidebar de filtros (Sticky) -->
  <aside class="catalog-sidebar panel" aria-label="Filtros del catálogo">
    <div class="sidebar-header">
      <h3>Filtros</h3>
      <button type="button" class="ghost small-btn" (click)="catalogSearch.clearFilters()">Limpiar filtros</button>
    </div>

    <!-- Tiendas -->
    <div class="filter-group">
      <h4>Tiendas</h4>
      <label class="checkbox">
        <input
          type="checkbox"
          [checked]="catalogSearch.selectedRetailers().includes('D1')"
          (change)="catalogSearch.toggleRetailer('D1')">
        D1
      </label>
      <label class="checkbox">
        <input
          type="checkbox"
          [checked]="catalogSearch.selectedRetailers().includes('EXITO')"
          (change)="catalogSearch.toggleRetailer('EXITO')">
        Éxito
      </label>
      <label class="checkbox">
        <input
          type="checkbox"
          [checked]="catalogSearch.selectedRetailers().includes('ARA')"
          (change)="catalogSearch.toggleRetailer('ARA')">
        Ara
      </label>
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
  </aside>

  <!-- Área principal del catálogo -->
  <main class="catalog-main">
    <!-- Toolbar de catálogo -->
    <div class="catalog-toolbar panel">
      <div class="toolbar-info">
        @if (catalogSearch.searchedQuery()) {
          <strong>{{ catalogSearch.total() }} productos encontrados para «{{ catalogSearch.searchedQuery() }}»</strong>
        } @else {
          <strong>Catálogo de productos</strong>
        }
      </div>
      <div class="toolbar-controls">
        <div class="sort-control">
          <label for="catalog-sort" class="sr-only">Ordenar por</label>
          <select id="catalog-sort" [ngModel]="catalogSearch.sort()" (ngModelChange)="catalogSearch.setSort($event)">
            <option value="relevance">Relevancia</option>
            <option value="price_asc">Menor precio</option>
            <option value="price_desc">Mayor precio</option>
            <option value="discount_desc">Mayor descuento</option>
          </select>
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

    <!-- Loading Skeletons -->
    @if (catalogSearch.loading()) {
      <div class="products-grid loading-grid">
        @for (i of [1,2,3,4,5,6]; track i) {
          <div class="product-card skeleton-card">
            <div class="skeleton-img"></div>
            <div class="skeleton-line title-line"></div>
            <div class="skeleton-line price-line"></div>
            <div class="skeleton-btn"></div>
          </div>
        }
      </div>
    }

    <!-- Error -->
    @if (catalogSearch.error()) {
      <div class="market-error" role="alert">
        {{ catalogSearch.error() }}
        <button type="button" class="secondary" (click)="catalogSearch.search()">Reintentar</button>
      </div>
    }

    <!-- Grid / List de Productos -->
    @if (!catalogSearch.loading() && catalogSearch.results().length > 0) {
      <div class="products-grid" [class.list-layout]="catalogSearch.viewMode() === 'list'">
        @for (prod of catalogSearch.results(); track prod.productId + prod.retailer) {
          <article class="product-card panel">
            <!-- Badges flotantes -->
            <div class="card-badges">
              <span class="retailer-badge" [class.d1-badge]="prod.retailer === 'D1'" [class.exito-badge]="prod.retailer === 'EXITO'">{{ prod.retailer }}</span>
              @if (prod.discount) {
                <span class="discount-badge">-{{ prod.discount }}%</span>
              }
              @if (prod.availability === 'OutOfStock') {
                <span class="stock-badge out-badge">Agotado</span>
              }
            </div>

            <!-- Imagen del producto con fallback -->
            <div class="product-image-container">
              @if (prod.imageUrl) {
                <img [src]="prod.imageUrl" [alt]="prod.name" loading="lazy" class="product-img">
              } @else {
                <div class="image-placeholder" aria-hidden="true">
                  <svg width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5">
                    <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
                    <circle cx="8.5" cy="8.5" r="1.5"></circle>
                    <polyline points="21 15 16 10 5 21"></polyline>
                  </svg>
                </div>
              }
            </div>

            <!-- Información del producto -->
            <div class="product-info">
              @if (prod.brand) {
                <span class="product-brand">{{ prod.brand }}</span>
              }
              <h3 class="product-title" [title]="prod.name">{{ prod.name }}</h3>
              @if (prod.presentation) {
                <span class="product-presentation">{{ prod.presentation }}</span>
              }

              <!-- Precios -->
              <div class="product-pricing">
                <strong class="current-price">\${{ prod.price | number:'1.0-0' }} COP</strong>
                @if (prod.listPrice && prod.listPrice > prod.price) {
                  <span class="list-price">\${{ prod.listPrice | number:'1.0-0' }}</span>
                }
              </div>

              @if (prod.unitPrice) {
                <span class="unit-price">{{ prod.unitPrice }}</span>
              }
              @if (prod.promotion) {
                <span class="promo-text">{{ prod.promotion }}</span>
              }
            </div>

            <!-- Botones de Acción -->
            <div class="product-actions">
              @if (prod.productUrl) {
                <a [href]="prod.productUrl" target="_blank" rel="noopener noreferrer" class="primary store-link-btn">
                  Ver en tienda ↗
                </a>
              }
              <button type="button" class="secondary add-list-btn" (click)="openAddModal(prod)">
                + Agregar a lista
              </button>
            </div>
          </article>
        }
      </div>
    }

    <!-- Empty State -->
    @if (!catalogSearch.loading() && !catalogSearch.error() && catalogSearch.results().length === 0 && catalogSearch.searchedQuery()) {
      <div class="empty">
        <div class="empty-icon">⌕</div>
        <h3>No encontramos productos</h3>
        <p>No se encontraron resultados para «{{ catalogSearch.searchedQuery() }}». Intenta con otro término más general como "leche", "arroz" o "detergente".</p>
      </div>
    }
  </main>
</div>

<!-- Modal para Agregar Producto a Lista -->
@if (targetProductToAdd; as prod) {
  <div class="assistant-backdrop" (click)="closeAddModal()" aria-hidden="true"></div>
  <div class="modal-dialog panel" role="dialog" aria-modal="true" aria-label="Agregar producto a lista">
    <div class="modal-header">
      <h3>Agregar a lista de compras</h3>
      <button type="button" class="ghost icon-btn" (click)="closeAddModal()">✕</button>
    </div>
    <div class="modal-body">
      <div class="product-summary-preview">
        <strong>{{ prod.name }}</strong>
        <p class="muted">{{ prod.brand || 'Marca no especificada' }} · \${{ prod.price | number:'1.0-0' }} COP ({{ prod.retailer }})</p>
      </div>

      <label for="basket-select-modal">Selecciona la lista destino:</label>
      <select id="basket-select-modal" [(ngModel)]="selectedBasketIdForAdd">
        @for (b of basketFacade.baskets(); track b.id) {
          <option [value]="b.id">{{ b.name }} ({{ b.items.length }} productos)</option>
        }
        <option value="new">+ Crear nueva lista…</option>
      </select>

      @if (selectedBasketIdForAdd === 'new' || !basketFacade.baskets().length) {
        <div class="new-basket-input-group">
          <label for="new-basket-name">Nombre de la nueva lista:</label>
          <input
            id="new-basket-name"
            type="text"
            [(ngModel)]="newBasketNameForAdd"
            placeholder="Ej. Mercado quincenal">
        </div>
      }

      @if (addingStatus) {
        <p class="notice" role="status">{{ addingStatus }}</p>
      }
    </div>
    <div class="modal-footer">
      <button type="button" class="secondary" (click)="closeAddModal()">Cancelar</button>
      <button type="button" class="primary" (click)="confirmAddToList()">Confirmar y agregar</button>
    </div>
  </div>
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
`;

const fullPath = path.resolve(__dirname, '..', 'src/frontend/src/app/pages/search.html');
fs.writeFileSync(fullPath, content, 'utf8');
console.log('Wrote search.html');
