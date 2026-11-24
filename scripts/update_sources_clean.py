content = """<div class="heading">
  <div>
    <p class="eyebrow">CONFIGURACIÓN DE TIENDAS</p>
    <h1>Tiendas</h1>
    <p class="muted">Administra las tiendas que quieres consultar y mantén actualizado tu catálogo.</p>
  </div>
</div>

<section class="panel mb-6" aria-label="Tiendas registradas">
  <div class="source-heading flex-between align-center mb-4">
    <div>
      <h2 class="text-xl font-bold m-0">Tiendas registradas</h2>
      <p class="text-sm text-muted m-0">Estado de disponibilidad y cobertura de precios para tus búsquedas.</p>
    </div>
    <button type="button" class="secondary" (click)="host.loadProviders()" [disabled]="host.providersLoading()">
      Actualizar estados
    </button>
  </div>

  @if (host.providersLoading()) {
    <p role="status" class="py-4 text-muted">Cargando estados de fuentes…</p>
  }
  @if (host.providersError()) {
    <div class="market-error" role="alert">
      {{ host.providersError() }}
      <button type="button" class="secondary" (click)="host.loadProviders()">Reintentar</button>
    </div>
  }

  @if (host.providers(); as sources) {
    @if (!sources.length && !host.providersLoading()) {
      <p role="status" class="py-4 text-muted">La API no informó fuentes.</p>
    } @else {
      <ul class="source-list retailer-grid" style="list-style: none; padding: 0; display: grid; grid-template-columns: repeat(auto-fill, minmax(320px, 1fr)); gap: 1rem;">
        @for (source of sources; track source.code) {
          <li class="retailer-card provider-card panel" style="margin: 0; display: flex; flex-direction: column; justify-content: space-between; gap: 1rem; border: 1px solid var(--border, #e5e7eb); border-radius: 12px; padding: 1.25rem;">
            <div class="source-item-info">
              <div class="source-title-row flex-between align-start mb-2">
                <div>
                  <div class="flex-align-center gap-2">
                    <span class="retailer-logo-badge font-black text-sm px-2 py-1 rounded bg-slate-100 text-slate-800 border">{{ source.code }}</span>
                    <strong class="text-base">{{ source.name || source.code }}</strong>
                  </div>
                  @if (source.retailerType) {
                    <p class="source-retailer-type text-xs text-muted mt-1 mb-0">{{ source.retailerType }} · {{ source.catalogCoverage }}</p>
                  }
                </div>
                <div class="flex-column align-end gap-1">
                  @if (source.status === 'Healthy' || source.status === 'Supported' || source.status.includes('ONLINE') || source.status.includes('MANUAL')) {
                    <span class="badge badge-success">Disponible</span>
                  } @else if (source.status.includes('PARTIAL') || source.status.includes('RATE_LIMITED') || source.status.includes('Degradado')) {
                    <span class="badge badge-warning">Con problemas</span>
                  } @else {
                    <span class="badge badge-danger">No disponible</span>
                  }
                  <span class="text-xs text-muted" style="font-family: monospace;">{{ source.status }}</span>
                </div>
              </div>

              @if (source.description) {
                <p class="source-desc text-sm text-muted my-2">{{ source.description }}</p>
              }

              <div class="coverage-tags-row flex-wrap gap-1 my-2">
                @if (source.catalogCoverage?.includes('Parcial')) {
                  <span class="badge-coverage-tag badge-tag-partial">[PARCIAL]</span>
                } @else if (source.catalogCoverage?.includes('Completa')) {
                  <span class="badge-coverage-tag badge-tag-complete">[COMPLETA]</span>
                }
                @if (source.code === 'ARA') {
                  <span class="badge-coverage-tag badge-tag-folleto">[FOLLETO]</span>
                  <span class="badge-coverage-tag badge-tag-manual">[MANUAL]</span>
                }
              </div>

              @if (source.capabilities; as caps) {
                <div class="source-caps-row flex-wrap gap-1 mt-3 pt-2 border-t text-xs">
                  <span class="cap-pill" [class.cap-yes]="caps.search === 'Supported'" [class.cap-part]="caps.search === 'Partial'" [class.cap-no]="caps.search === 'NotSupported'">Búsqueda: {{ caps.search === 'Supported' ? 'Soportada' : caps.search === 'Partial' ? 'Parcial' : 'No' }}</span>
                  <span class="cap-pill" [class.cap-yes]="caps.offers === 'Supported'" [class.cap-part]="caps.offers === 'Partial'" [class.cap-no]="caps.offers === 'NotSupported'">Precios: {{ caps.offers === 'Supported' ? 'Soportado' : caps.offers === 'Partial' ? 'Parcial' : 'No' }}</span>
                  <span class="cap-pill" [class.cap-yes]="caps.stores === 'Supported'" [class.cap-part]="caps.stores === 'Partial'" [class.cap-no]="caps.stores === 'NotSupported'">Tiendas: {{ caps.stores === 'Supported' ? 'Soportado' : caps.stores === 'Partial' ? 'Parcial' : 'No' }}</span>
                  <span class="cap-pill" [class.cap-yes]="caps.availability === 'Supported'" [class.cap-part]="caps.availability === 'Partial'" [class.cap-no]="caps.availability === 'NotSupported'">Stock: {{ caps.availability === 'Supported' ? 'Soportado' : caps.availability === 'Partial' ? 'Parcial' : 'No' }}</span>
                  <span class="cap-pill" [class.cap-yes]="caps.catalogEnumeration === 'Supported'" [class.cap-part]="caps.catalogEnumeration === 'Partial'" [class.cap-no]="caps.catalogEnumeration === 'NotSupported'">Catálogo: {{ caps.catalogEnumeration === 'Supported' ? 'Completo' : caps.catalogEnumeration === 'Partial' ? 'Parcial' : 'No' }}</span>
                </div>
              }
            </div>

            <div class="card-footer-actions pt-2 border-t flex-between align-center">
              <span class="text-xs text-muted">Integración local</span>
              @if (source.code === 'D1') {
                <button type="button" class="secondary text-xs px-2 py-1" (click)="host.probeD1()" [disabled]="host.d1ProbeBusy()">
                  {{ host.d1ProbeBusy() ? 'Comprobando…' : 'Comprobar D1 ahora' }}
                </button>
              } @else if (source.code === 'ARA') {
                <button type="button" class="secondary text-xs px-2 py-1" (click)="host.loadAraHealth()" [disabled]="host.araHealthLoading()">
                  {{ host.araHealthLoading() ? 'Consultando…' : 'Consultar Ara' }}
                </button>
              }
            </div>
          </li>
        }
      </ul>
    }
  }

  <!-- DETALLES TÉCNICOS Y DIAGNÓSTICO -->
  <details class="technical-details mt-6 border rounded-lg p-4 bg-slate-50">
    <summary class="font-bold text-sm cursor-pointer text-slate-700">Detalles técnicos y diagnósticos avanzados</summary>
    
    <div class="source-detail mt-4 pt-4 border-t" aria-label="Historial local D1">
      <div class="flex-between align-center">
        <h3 class="text-base font-bold m-0">D1 · última sincronización local</h3>
        <button type="button" class="secondary text-xs" (click)="host.probeD1()" [disabled]="host.d1ProbeBusy()">
          {{ host.d1ProbeBusy() ? 'Comprobando…' : 'Comprobar D1 ahora' }}
        </button>
      </div>
      <p class="muted text-xs">Estos datos describen una ejecución pasada, no una comprobación de Internet, disponibilidad actual ni precio físico.</p>
      
      @if (host.d1HealthError()) {
        <p role="alert" class="market-error text-xs">{{ host.d1HealthError() }} @if (host.d1Health()) { El último estado mostrado puede estar desactualizado. }</p>
      }
      @if (host.d1Health(); as evidence) {
        @if (evidence.lastRun; as run) {
          <p role="status" class="text-xs font-semibold">Estado: {{ run.state === 1 || run.state === 'Completed' ? 'Completada' : run.state === 2 || run.state === 'Failed' ? 'Fallida' : 'No completada' }} · Fin: {{ run.finishedAt ?? 'sin fecha' }} · Error: {{ run.errorCode ?? 'ninguno registrado' }}</p>
          <p class="muted text-xs">Filas: {{ run.itemsReceived }} · Ofertas procesadas: {{ run.offersProcessed }} ({{ run.offersChanged }} cambiadas) · Productos creados/actualizados: {{ run.productsCreated }}/{{ run.productsUpdated }} · SKUs creados/actualizados: {{ run.skusCreated }}/{{ run.skusUpdated }} · RAW nuevos/deduplicados: {{ run.rawNew }}/{{ run.rawDeduplicated }}</p>
        } @else {
          <p role="status" class="text-xs text-muted">Aún no se ha registrado ninguna sincronización local para D1.</p>
        }
      }
      @if (host.d1ProbeError()) { <p role="alert" class="market-error text-xs">{{ host.d1ProbeError() }}</p> }
      @if (host.d1Probe(); as probe) { <p role="status" class="text-xs">Comprobado: {{ probe.checkedAt }} · Búsqueda: {{ probe.search }} · Precio online genérico: {{ probe.genericOnlinePrice }}</p> }
    </div>

    <div class="source-detail mt-4 pt-4 border-t" aria-label="Historial local Éxito">
      <div class="flex-between align-center">
        <h3 class="text-base font-bold m-0">Éxito · diagnóstico de integración FastStore</h3>
        <div class="flex gap-2">
          <button type="button" class="secondary text-xs" (click)="host.probeExito()" [disabled]="host.exitoProbeBusy()">{{ host.exitoProbeBusy() ? 'Comprobando…' : 'Comprobar Éxito' }}</button>
          <button type="button" class="secondary text-xs" (click)="host.probeRegionalExito()" [disabled]="host.exitoRegionalProbeBusy()">{{ host.exitoRegionalProbeBusy() ? 'Comprobando regional…' : 'Comprobar Éxito regional' }}</button>
        </div>
      </div>
      <p class="muted text-xs">Diagnóstico para validar llamadas a la API de Éxito en modo genérico y regional.</p>
      @if (host.exitoHealth(); as exitoHealth) {
        @if (exitoHealth.lastRun; as run) {
          <p role="status" class="text-xs">Última ejecución: {{ run.finishedAt }} · Filas: {{ run.itemsReceived }} · Estado: {{ run.state }}</p>
        }
      }
      @if (host.exitoProbeError()) { <p role="alert" class="market-error text-xs">{{ host.exitoProbeError() }}</p> }
      @if (host.exitoProbe(); as probe) { <p role="status" class="text-xs">Genérico · Comprobado: {{ probe.checkedAt }} · Búsqueda: {{ probe.search }} · Precio online genérico: {{ probe.genericOnlinePrice }}</p> }
      @if (host.exitoRegionalProbeError()) { <p role="alert" class="market-error text-xs">{{ host.exitoRegionalProbeError() }}</p> }
      @if (host.exitoRegionalProbe(); as regProbe) { <p role="status" class="text-xs">Regional · Comprobado: {{ regProbe.checkedAt }} · Región: {{ regProbe.regionContext }} · Catálogo: {{ regProbe.regionalCatalog }} · Precio: {{ regProbe.regionalOnlinePrice }} @if (regProbe.errorCode) { · Error: {{ regProbe.errorCode }} }</p> }
    </div>
  </details>
</section>

<!-- SECCIÓN ARA FOLLETOS Y COMUNIDAD -->
<section class="panel mb-6" aria-label="Historial y ofertas de Tiendas Ara">
  <div class="source-heading flex-between align-center mb-3">
    <div>
      <h2 class="text-xl font-bold m-0">Tiendas Ara · Cobertura y Comunidad</h2>
      <p class="muted text-sm m-0">Precios basados en folletos públicos y observaciones en góndola física.</p>
    </div>
    <div class="probe-buttons flex gap-2">
      <button type="button" class="secondary" (click)="host.loadAraHealth()" [disabled]="host.araHealthLoading()">
        {{ host.araHealthLoading() ? 'Consultando…' : 'Consultar estado Ara' }}
      </button>
      <button type="button" class="secondary" (click)="host.loadAraManualOffers()" [disabled]="host.araManualLoading()">
        {{ host.araManualLoading() ? 'Cargando…' : 'Ver ofertas comunitarias' }}
      </button>
    </div>
  </div>

  <p class="muted text-sm">
    Tiendas Ara opera bajo diagnóstico D: sin raspado web ni ingeniería inversa invasiva. La información se obtiene de folletos públicos promocionales auditados y aportes físicos en tienda ingresados por usuarios.
  </p>

  @if (host.araHealthError()) { <p class="market-error" role="alert">{{ host.araHealthError() }}</p> }
  @if (host.araHealth(); as ah) {
    <div class="ara-health-summary p-4 bg-slate-50 border rounded-lg my-3">
      <p role="status" class="text-sm"><strong>Estado:</strong> {{ ah.status }} · <strong>Cobertura:</strong> {{ ah.coverage }}</p>
      <div class="storage-stats-grid">
        <div class="storage-stat-card"><span>Ofertas activas</span><strong>{{ ah.totalActiveOffers }}</strong></div>
        <div class="storage-stat-card"><span>Folletos públicos</span><strong>{{ ah.brochureOffersCount }}</strong></div>
        <div class="storage-stat-card"><span>Aportes manuales</span><strong>{{ ah.manualOffersCount }}</strong></div>
      </div>
      <p class="muted text-xs mb-0">{{ ah.summary }} @if (ah.lastOfferUpdate) { · Actualizado: {{ ah.lastOfferUpdate }} }</p>
    </div>
  }

  <div class="ara-manual-box mt-4 pt-4 border-t">
    <h3 class="text-lg font-bold">Aporte de precio físico comunitario</h3>
    <p class="muted text-sm">Registra precios observados en góndola física de Tiendas Ara. Indicar la tienda física clasifica el precio como verificado físicamente (PhysicalVerified); sin tienda, se conserva como alcance desconocido (Unknown).</p>

    @if (host.araManualNotice()) { <p class="notice" role="status">{{ host.araManualNotice() }}</p> }
    @if (host.araManualError()) { <p class="market-error" role="alert">{{ host.araManualError() }}</p> }

    <form class="ara-form mt-3" (ngSubmit)="host.submitAraManualOffer()" novalidate>
      <div class="ara-form-grid" style="display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 1rem;">
        <label>Producto *
          <input type="text" [(ngModel)]="host.araProductName" name="araProductName" placeholder="Ej. Arroz Diana 1000g" required [disabled]="host.araManualBusy()">
        </label>
        <label>Marca
          <input type="text" [(ngModel)]="host.araBrand" name="araBrand" placeholder="Ej. Diana" [disabled]="host.araManualBusy()">
        </label>
        <label>Cantidad
          <input type="number" [(ngModel)]="host.araQuantity" name="araQuantity" min="0" placeholder="1000" [disabled]="host.araManualBusy()">
        </label>
        <label>Unidad
          <select [(ngModel)]="host.araUnit" name="araUnit" [disabled]="host.araManualBusy()">
            <option value="g">Gramos (g)</option>
            <option value="kg">Kilogramos (kg)</option>
            <option value="ml">Mililitros (ml)</option>
            <option value="l">Litros (l)</option>
            <option value="und">Unidades (und)</option>
          </select>
        </label>
        <label>Precio góndola ($) *
          <input type="number" [(ngModel)]="host.araPrice" name="araPrice" min="1" placeholder="4200" required [disabled]="host.araManualBusy()">
        </label>
        <label>Precio lista regular ($)
          <input type="number" [(ngModel)]="host.araListPrice" name="araListPrice" min="0" placeholder="4900" [disabled]="host.araManualBusy()">
        </label>
        <label style="grid-column: 1 / -1;">Tienda física (opcional)
          <input type="text" [(ngModel)]="host.araStoreName" name="araStoreName" placeholder="Ej. Ara Belén Parque" [disabled]="host.araManualBusy()">
        </label>
        <label style="grid-column: 1 / -1;">Notas / Observaciones
          <input type="text" [(ngModel)]="host.araNotes" name="araNotes" placeholder="Ej. Góndola de entrada, oferta de temporada" [disabled]="host.araManualBusy()">
        </label>
      </div>
      <button type="submit" class="primary mt-3" [disabled]="host.araManualBusy() || !host.araProductName.trim() || !host.araPrice">
        {{ host.araManualBusy() ? 'Guardando…' : 'Registrar aporte de precio' }}
      </button>
    </form>

    @if (host.araManualLoading()) { <p role="status" class="text-sm text-muted mt-3">Cargando aportes comunitarios…</p> }
    @if (host.araManualOffers().length > 0) {
      <div class="ara-table-container mt-4">
        <h4 class="text-sm font-bold">Aportes comunitarios activos ({{ host.araManualOffers().length }})</h4>
        <ul class="ara-offers-list mt-2" style="list-style: none; padding: 0;">
          @for (off of host.araManualOffers(); track off.offerId) {
            <li class="p-3 border rounded-lg mb-2 flex-between align-center bg-white">
              <div class="ara-offer-info">
                <strong>{{ off.productName }}</strong>
                <span class="muted text-xs block">{{ off.brand || 'Sin marca' }} · {{ off.skuQuantity ? off.skuQuantity + ' ' + off.skuDimension : '' }}</span>
                <span class="ara-price-tag font-bold text-sm text-emerald-700">${{ off.price | number:'1.0-0' }} @if (off.listPrice) { <del class="muted text-xs">${{ off.listPrice | number:'1.0-0' }}</del> }</span>
                <span class="badge" [class.badge-verified]="off.scope === 'PhysicalVerified' || off.scope === '1'">
                  {{ off.storeName ? off.storeName + ' (Físico verificado)' : 'Alcance general (Unknown)' }}
                </span>
              </div>
              <button type="button" class="secondary text-xs px-2 py-1" (click)="host.deleteAraManualOffer(off.offerId)" [disabled]="host.araManualBusy()" [attr.aria-label]="'Eliminar ' + off.productName">
                Eliminar
              </button>
            </li>
          }
        </ul>
      </div>
    }
  </div>
</section>

<!-- SECCIÓN CATÁLOGO LOCAL / IMPORTACIÓN -->
<section class="panel catalog-categories-section mb-6" aria-label="Importar catálogo por categorías">
  <div class="source-heading flex-between align-center mb-3">
    <div>
      <h2 class="text-xl font-bold m-0">Catálogo local</h2>
      <p class="muted text-sm m-0">Guarda categorías completas para búsquedas y análisis más rápidos.</p>
    </div>
  </div>

  <div class="catalog-import-controls" style="margin-top: 1rem; display: flex; flex-direction: column; gap: 1rem;">
    <!-- Retailer and mode selection -->
    <div style="display: flex; gap: 1rem; flex-wrap: wrap; align-items: flex-end;">
      <label style="flex: 1; min-width: 180px;">
        <span style="font-size: 0.85rem; font-weight: 600;">Supermercado</span>
        <select [value]="catalogImport.selectedRetailer()" (change)="catalogImport.setRetailer($any($event.target).value)" style="width: 100%; margin-top: 0.25rem;">
          <option value="D1">Tiendas D1</option>
          <option value="EXITO">Éxito</option>
          <option value="ARA">Tiendas Ara</option>
        </select>
      </label>

      <button type="button" class="secondary" (click)="catalogImport.loadCategories()" [disabled]="catalogImport.loadingCategories()">
        {{ catalogImport.loadingCategories() ? "Consultando categorías…" : "Refrescar categorías" }}
      </button>
    </div>

    @if (catalogImport.activeJob(); as job) {
      <div class="import-progress-card" style="background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 8px; padding: 1rem; margin-top: 0.5rem;">
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem;">
          <h4 style="margin: 0; color: #1e40af;">
            Importación activa: {{ job.retailerName }}
            <span class="badge" [class.badge-exact]="job.status === 'Completed'" [class.badge-substitute]="job.status === 'Running'">
              {{ job.status === 'Running' ? 'En progreso…' : job.status === 'Completed' ? 'Completado' : job.status === 'Cancelled' ? 'Cancelado' : job.status }}
            </span>
          </h4>
          @if (job.status === 'Running' || job.status === 'Pending') {
            <button type="button" class="secondary" style="color: #dc2626; border-color: #fca5a5;" (click)="catalogImport.cancelJob(job.id)">
              Cancelar importación
            </button>
          }
        </div>

        @if (job.currentCategoryName) {
          <p class="muted" style="margin-bottom: 0.75rem; font-size: 0.9rem;">
            Procesando categoría: <strong>{{ job.currentCategoryName }}</strong>
          </p>
        }

        <div style="margin-bottom: 0.75rem;">
          <div class="flex-between text-xs text-muted mb-1">
            <span>Progreso general: {{ job.processedItems }} de {{ job.totalItems || job.processedItems }}</span>
            <span>{{ job.totalItems > 0 ? (job.processedItems / job.totalItems * 100 | number:'1.0-0') : 100 }}%</span>
          </div>
          <div style="width: 100%; height: 8px; background: #dbeafe; border-radius: 4px; overflow: hidden;">
            <div style="height: 100%; background: #2563eb; transition: width 0.3s ease;" [style.width.%]="job.totalItems > 0 ? (job.processedItems / job.totalItems * 100) : 100"></div>
          </div>
        </div>

        <div class="storage-stats-grid" style="display: grid; grid-template-columns: repeat(auto-fit, minmax(130px, 1fr)); gap: 0.75rem; margin-bottom: 0.75rem;">
          <div class="storage-stat-card"><span>Procesados</span><strong>{{ job.processedItems }} / {{ job.totalItems }}</strong></div>
          <div class="storage-stat-card"><span>Nuevos</span><strong style="color: #16a34a;">+{{ job.newItems }}</strong></div>
          <div class="storage-stat-card"><span>Actualizados</span><strong style="color: #2563eb;">{{ job.updatedItems }}</strong></div>
          <div class="storage-stat-card"><span>Sin cambios</span><strong>{{ job.unchangedItems }}</strong></div>
          @if (job.errorCount > 0) {
            <div class="storage-stat-card"><span>Errores</span><strong style="color: #dc2626;">{{ job.errorCount }}</strong></div>
          }
        </div>
      </div>
    }

    <!-- Categories selection -->
    @if (catalogImport.categories().length > 0) {
      <div style="margin-top: 0.5rem;">
        <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 0.5rem;">
          <h4 style="margin: 0;">Categorías disponibles ({{ catalogImport.categories().length }})</h4>
          <label style="display: flex; align-items: center; gap: 0.5rem; font-size: 0.9rem; cursor: pointer;">
            <input type="checkbox" [checked]="catalogImport.selectAll()" (change)="catalogImport.toggleSelectAll()">
            <strong>Seleccionar todas</strong>
          </label>
        </div>

        <div style="display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 0.5rem; max-height: 280px; overflow-y: auto; padding: 0.5rem; border: 1px solid var(--border-color, #e2e8f0); border-radius: 8px;">
          @for (cat of catalogImport.categories(); track cat.id) {
            <label style="display: flex; align-items: center; gap: 0.5rem; padding: 0.35rem 0.5rem; border-radius: 6px; background: var(--surface, #fff); cursor: pointer; font-size: 0.9rem;">
              <input type="checkbox" [checked]="catalogImport.selectedCategoryIds().includes(cat.id)" (change)="catalogImport.toggleCategory(cat.id)">
              <span style="flex: 1;">{{ cat.name }}</span>
              @if (cat.estimatedCount) {
                <span class="muted" style="font-size: 0.8rem;">~{{ cat.estimatedCount }}</span>
              }
            </label>
          }
        </div>

        <div style="margin-top: 1rem; display: flex; gap: 1rem; align-items: center;">
          <button type="button" class="primary" (click)="catalogImport.startImport()" [disabled]="catalogImport.importStarting() || (!catalogImport.selectAll() && catalogImport.selectedCategoryIds().length === 0)">
            {{ catalogImport.importStarting() ? "Iniciando…" : "Importar categorías seleccionadas" }}
          </button>
          <span class="muted" style="font-size: 0.85rem;">
            {{ catalogImport.selectAll() ? "Todas las categorías seleccionadas" : catalogImport.selectedCategoryIds().length + " categorías seleccionadas" }}
          </span>
        </div>
      </div>
    } @else if (catalogImport.loadingCategories()) {
      <p class="muted">Consultando estructura de categorías de la tienda…</p>
    } @else {
      <p class="muted">No hay categorías cargadas para esta tienda. Pulsa "Refrescar categorías" para consultar la tienda.</p>
    }

    <!-- Past Jobs -->
    @if (catalogImport.jobs().length > 0) {
      <div style="margin-top: 1.5rem;">
        <h4>Historial de importaciones</h4>
        <ul class="ara-offers-list" style="margin-top: 0.5rem;">
          @for (j of catalogImport.jobs(); track j.id) {
            <li>
              <div class="ara-offer-info">
                <strong>{{ j.retailerName }}</strong>
                <span class="muted">{{ j.startedAt | date:'short' }} · {{ j.processedItems }} productos</span>
                <span class="badge" [class.badge-verified]="j.status === 'Completed'">{{ j.status }}</span>
              </div>
            </li>
          }
        </ul>
      </div>
    }
  </div>
</section>
"""

with open('src/frontend/src/app/pages/sources.html', 'w', encoding='utf-8') as f:
    f.write(content)
print("Updated sources.html successfully!")
