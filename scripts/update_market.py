content = """<div class="heading">
  <div>
    <p class="eyebrow">TUS LISTAS LOCALES</p>
    <h1>Mi mercado</h1>
    <p class="muted">Organiza tus compras, compara alternativas y optimiza el ahorro total entre tiendas.</p>
  </div>
</div>

<!-- Layout Master / Detail de Listas -->
<div class="market-master-detail-layout">
  <!-- Columna Izquierda: Master (Selector y Gestión de Listas) -->
  <aside class="market-sidebar panel" aria-label="Listas de mercado guardadas">
    <div class="market-actions">
      <h2>Tus listas</h2>
      <button
        type="button"
        class="primary small-btn"
        (click)="openCreateModal()"
        [disabled]="host.marketBusy() || host.marketLoading()">
        + Nueva lista
      </button>
    </div>

    @if (host.marketLoading()) { <p role="status" class="muted">Cargando listas…</p> }
    @if (host.marketError()) {
      <div class="market-error" role="alert">
        {{ host.marketError() }}
        <button type="button" class="secondary small-btn" (click)="host.loadBaskets()">Reintentar</button>
      </div>
    }
    @if (!host.marketLoading() && !host.baskets().length && !host.marketError()) {
      <div class="empty-lists-notice">
        <p class="muted">Aún no tienes listas. Crea una para comenzar.</p>
        <button type="button" class="primary small-btn" (click)="host.createBasket()">Crear primera lista</button>
      </div>
    }

    <!-- Selector nativo para accesibilidad y tests -->
    @if (host.baskets().length) {
      <div class="basket-selector sr-only">
        <label for="basket-select">Lista activa</label>
        <select
          id="basket-select"
          [ngModel]="host.selectedBasketId()"
          (ngModelChange)="host.selectedBasketId.set($event); host.itemError.set('')"
          [disabled]="host.marketBusy() || !!host.deletingItemId()">
          @for (b of host.baskets(); track b.id) {
            <option [value]="b.id">{{ b.name }} · {{ b.items.length }} productos</option>
          }
        </select>
      </div>

      <!-- Tarjetas de listas (Master) -->
      <div class="baskets-cards-grid">
        @for (b of host.baskets(); track b.id) {
          <article class="basket-card panel" [class.active-basket-card]="b.id === host.selectedBasketId()" (click)="host.selectedBasketId.set(b.id)">
            <div class="basket-card-header">
              <div>
                <h3>{{ b.name }}</h3>
                <span class="muted small">{{ b.items.length }} productos</span>
              </div>
              @if (b.id === host.selectedBasketId()) {
                <span class="status-pill status-exact">Activa</span>
              }
            </div>
            <div class="basket-card-actions" (click)="$event.stopPropagation()">
              @if (b.id !== host.selectedBasketId()) {
                <button type="button" class="secondary small-btn" (click)="host.selectedBasketId.set(b.id)">Abrir</button>
              }
              <button type="button" class="ghost small-btn" (click)="duplicate(b.id)" title="Duplicar lista">Duplicar</button>
              <button type="button" class="ghost small-btn" (click)="openRenameModal(b)" title="Renombrar lista">Renombrar</button>
              <button type="button" class="ghost small-btn text-danger" (click)="openDeleteModal(b)" title="Eliminar lista">Eliminar</button>
            </div>
          </article>
        }
      </div>
    }
  </aside>

  <!-- Columna Derecha: Detail (Productos de la Lista Activa) -->
  <main class="market-main-detail">
    @if (host.selectedBasket; as basket) {
      <section class="panel active-list-panel" aria-label="Productos de la lista">
        <div class="active-list-header">
          <div>
            <h2>{{ basket.name }}</h2>
            <p class="muted">{{ basket.items.length }} productos en esta lista</p>
          </div>
          <div class="active-list-actions">
            <button
              type="button"
              class="secondary small-btn"
              (click)="host.syncBasket()"
              [disabled]="host.basketSyncBusy() || !basket.items.length"
              title="Sincronizar precios de tiendas">
              {{ host.basketSyncBusy() ? 'Sincronizando…' : 'Actualizar precios' }}
            </button>
            <button
              type="button"
              class="primary small-btn"
              (click)="host.optimizeBasket()"
              [disabled]="host.basketOptimizationBusy() || !basket.items.length">
              {{ host.basketOptimizationBusy() ? 'Optimizando…' : 'Optimizar' }}
            </button>
          </div>
        </div>

        @if (host.basketSyncNotice()) { <p class="notice" role="status">{{ host.basketSyncNotice() }}</p> }
        @if (host.basketSyncError()) { <p class="market-error" role="alert">{{ host.basketSyncError() }}</p> }

        <!-- Formulario para agregar producto a la lista -->
        <form class="item-form" (ngSubmit)="host.addItem()" novalidate>
          <div class="item-form-grid">
            <label class="query-field">
              <span>Producto</span>
              <input name="query" type="text" [(ngModel)]="host.query" maxlength="500" [disabled]="host.marketBusy() || !!host.deletingItemId()" placeholder="Ej. Leche entera, Arroz 1kg, Queso">
            </label>
            <label class="qty-field">
              <span>Cantidad</span>
              <input name="quantity" type="number" min="0.01" step="any" [(ngModel)]="host.quantity" [disabled]="host.marketBusy() || !!host.deletingItemId()">
            </label>
            <label class="notes-field">
              <span>Notas (opcional)</span>
              <input name="notes" type="text" [(ngModel)]="host.notes" maxlength="2000" [disabled]="host.marketBusy() || !!host.deletingItemId()" placeholder="Ej. Marca propia o en promoción">
            </label>
            <div class="add-btn-wrapper">
              <button class="primary" type="submit" [disabled]="host.marketBusy() || !!host.deletingItemId()">
                {{ host.marketBusy() ? 'Agregando…' : 'Agregar' }}
              </button>
            </div>
          </div>
        </form>

        @if (host.itemError()) { <p class="market-error" role="alert">{{ host.itemError() }}</p> }

        <!-- Lista compacta de items -->
        @if (!basket.items.length) {
          <div class="empty-basket-state">
            <p class="muted" role="status">Esta lista está vacía. Escribe un producto arriba o agrégalo desde el buscador.</p>
          </div>
        } @else {
          <ul class="item-list">
            @for (item of basket.items; track item.id) {
              <li>
                <div class="item-info">
                  <strong>{{ item.query }}</strong>
                  <span class="item-quantity">Cantidad: {{ item.quantity }}</span>
                  @if (item.notes) { <small class="item-notes">{{ item.notes }}</small> }
                </div>
                <div class="item-actions">
                  <button
                    type="button"
                    class="ghost small-btn text-danger"
                    (click)="host.deleteItem(item)"
                    [disabled]="host.marketBusy() || !!host.deletingItemId()"
                    [attr.aria-label]="'Quitar ' + item.query">
                    {{ host.deletingItemId() === item.id ? 'Quitando…' : 'Quitar' }}
                  </button>
                </div>
              </li>
            }
          </ul>
        }
      </section>

      <!-- Panel de Optimización y Ahorro -->
      <section class="panel basket-optimization-panel" aria-label="Optimización y Explicación de Cesta">
        <div class="market-actions">
          <div>
            <p class="eyebrow">DISTRIBUCIÓN Y AHORRO ÓPTIMO</p>
            <h2>Optimizar compra de la lista</h2>
          </div>
          <button type="button" class="primary" (click)="host.optimizeBasket()" [disabled]="host.basketOptimizationBusy() || !basket.items.length">
            {{ host.basketOptimizationBusy() ? 'Optimizando cesta…' : 'Optimizar cesta ahora' }}
          </button>
        </div>
        <p class="muted">
          Calcula la combinación más conveniente de tiendas considerando precios unitarios y tus sucursales cercanas.
        </p>

        @if (host.basketOptimizationError()) { <p class="market-error" role="alert">{{ host.basketOptimizationError() }}</p> }

        @if (host.basketOptimization(); as opt) {
          <div class="optimization-results">
            <p class="notice" role="status">Precios de tiendas verificados para Medellín. Las distancias corresponden a tus sucursales configuradas.</p>
            @if (opt.warnings.length) {
              <ul class="explanation-warnings">@for (warning of opt.warnings; track $index) { <li>{{ warning }}</li> }</ul>
            }
            <div class="route-summary-cards">
              <div class="route-card">
                <span>COSTO TOTAL ÓPTIMO</span>
                <strong>${{ opt.totalCost | number:'1.0-0' }} COP</strong>
                <small>{{ opt.storesUsedCount }} tienda(s) seleccionada(s)</small>
              </div>
              <div class="route-card">
                <span>AHORRO VS TIENDA ÚNICA</span>
                <strong class="text-success">${{ opt.totalSavingsVsBaseline | number:'1.0-0' }} COP</strong>
                <small>Base: ${{ opt.baselineCost | number:'1.0-0' }}</small>
              </div>
              <div class="route-card">
                <span>COBERTURA DE PRODUCTOS</span>
                <strong>{{ opt.itemsCoveredCount }} / {{ opt.totalItemsRequested }}</strong>
                <small>{{ opt.missingItemsCount }} faltante(s)</small>
              </div>
            </div>

            <div class="stores-breakdown">
              <h3>Asignación de compras por tienda:</h3>
              @for (st of opt.stores; track st.storeKey) {
                <div class="store-bucket-card panel">
                  <div class="bucket-header">
                    <div>
                      <strong>{{ st.storeName }} ({{ st.retailerCode }})</strong>
                      @if (st.travelLocation?.transparencyNotice) {
                        <p class="store-transparency-notice muted"><small>📍 {{ st.travelLocation.transparencyNotice }}</small></p>
                      }
                    </div>
                    <span class="bucket-subtotal">Subtotal: ${{ st.subtotal | number:'1.0-0' }} COP</span>
                  </div>
                  <ul class="bucket-items">
                    @for (item of st.items; track item.basketItemId) {
                      <li>
                        <span>{{ item.itemQuery }} ({{ item.requestedQuantity }})</span>
                        @if (item.isSubstitute) {
                          <span class="badge badge-substitute">Sustituto</span>
                        }
                        <small class="muted">{{ item.assignmentReason }}</small>
                      </li>
                    }
                  </ul>
                </div>
              }
            </div>

            <div class="ai-explanation-action-row">
              <button type="button" class="secondary" (click)="host.explainBasketOptimization()" [disabled]="host.basketExplanationBusy()">
                {{ host.basketExplanationBusy() ? 'Generando explicación…' : '✨ Explicar optimización con IA' }}
              </button>
              <span class="muted">Explicación en lenguaje natural de los compromisos de compra y desplazamiento.</span>
            </div>

            @if (host.basketExplanationError()) { <p class="market-error" role="alert">{{ host.basketExplanationError() }}</p> }

            @if (host.basketExplanation(); as exp) {
              <div class="ai-explanation-card panel">
                <div class="explanation-header">
                  <span class="ai-badge" [class.badge-ai]="exp.source === 'AI'" [class.badge-fallback]="exp.source !== 'AI'">
                    {{ exp.source === 'AI' ? 'Generado por IA (' + (exp.modelUsed || 'OpenRouter') + ')' : 'Explicación del optimizador' }}
                  </span>
                </div>
                <div class="explanation-body">
                  <h4>Resumen de la recomendación</h4>
                  <p>{{ exp.summary }}</p>

                  <h4>¿Por qué se recomienda esta distribución?</h4>
                  <p>{{ exp.whyRecommended }}</p>

                  <h4>Balance entre tiendas y desplazamientos</h4>
                  <p>{{ exp.storeTradeoffs }}</p>

                  @if (exp.keySavingsSources && exp.keySavingsSources.length > 0) {
                    <h4>Fuentes clave de ahorro</h4>
                    <ul>
                      @for (src of exp.keySavingsSources; track $index) {
                        <li>{{ src }}</li>
                      }
                    </ul>
                  }
                </div>
              </div>
            }
          </div>
        }
      </section>
    } @else {
      <div class="panel empty-selection-panel">
        <p class="muted">Selecciona una lista a la izquierda o crea una nueva.</p>
      </div>
    }

    <!-- Ruta y Ubicación (Secundario / Plegable per Spec #59) -->
    <details class="location-details panel" aria-label="Ubicación y Perfiles">
      <summary class="location-summary-header">
        <div>
          <span class="summary-title">📍 Ruta y ubicación de partida</span>
          <span class="summary-subtitle muted">{{ host.currentAddress() || 'Sin ubicación seleccionada' }}</span>
        </div>
      </summary>

      <div class="location-details-content">
        <div class="market-actions">
          <p class="muted">Configura tu dirección de partida en Medellín para calcular distancias a tiendas.</p>
          <button type="button" class="secondary small-btn" (click)="host.loadProfiles()">Cargar perfiles guardados</button>
        </div>

        @if (host.locationProfiles().length > 0) {
          <div class="profile-select-row">
            <label for="profile-select">Perfil guardado:</label>
            <div class="select-wrapper">
              <select id="profile-select" [ngModel]="host.selectedProfileId()" (ngModelChange)="host.selectProfile($event)">
                <option [value]="null">-- Seleccionar perfil --</option>
                @for (prof of host.locationProfiles(); track prof.id) {
                  <option [value]="prof.id">{{ prof.label }} ({{ prof.address || (prof.latitude + ', ' + prof.longitude) }})</option>
                }
              </select>
            </div>
            @if (host.selectedProfileId(); as profId) {
              <button type="button" class="secondary small-btn" (click)="host.deleteProfile(profId)" title="Eliminar perfil">Eliminar perfil</button>
            }
          </div>
        }

        <div class="location-input-group">
          <input type="text" [(ngModel)]="host.locationInput" placeholder="Dirección, coordenadas o link de Google Maps" [disabled]="host.locationBusy()" aria-label="Dirección o coordenadas de partida">
          <button type="button" class="primary" (click)="host.parseLocation()" [disabled]="host.locationBusy() || !host.locationInput.trim()">
            {{ host.locationBusy() ? 'Procesando…' : 'Establecer ubicación' }}
          </button>
        </div>

        @if (host.locationNotice()) { <p class="notice" role="status">{{ host.locationNotice() }}</p> }
        @if (host.locationError()) { <p class="market-error" role="alert">{{ host.locationError() }}</p> }
        @if (host.incompleteAddressNotice()) {
          <div class="market-warning incomplete-address-card" role="alert">
            <p>⚠️ {{ host.incompleteAddressNotice() }}</p>
            <div class="incomplete-inputs">
              <input type="text" [(ngModel)]="host.profileStreet" placeholder="Calle / Cra" />
              <input type="text" [(ngModel)]="host.profileNumber" placeholder="Número (ej. 45-20)" />
              <input type="text" [(ngModel)]="host.profileNeighborhood" placeholder="Barrio" />
              <input type="text" [(ngModel)]="host.profilePostalCode" placeholder="Código Postal" />
            </div>
          </div>
        }

        @if (host.currentLatitude() !== null && host.currentLongitude() !== null) {
          <div class="location-current-card">
            <div>
              <strong>Ubicación activa:</strong> {{ host.currentAddress() }}
              <span class="muted">({{ host.currentLatitude() | number:'1.4-4' }}, {{ host.currentLongitude() | number:'1.4-4' }})</span>
            </div>
            <div class="profile-save-row">
              <input type="text" [(ngModel)]="host.newProfileLabel" placeholder="Nombre (ej. Mi Casa)" maxlength="50" [disabled]="host.locationBusy()">
              <button type="button" class="secondary small-btn" (click)="host.saveCurrentAsProfile()" [disabled]="host.locationBusy() || !host.newProfileLabel.trim()">Guardar como perfil</button>
            </div>
          </div>
        }

        <!-- Tiendas físicas y plan de ruta -->
        <div class="stores-route-subpanel">
          <div class="market-actions">
            <div>
              <h3>Tiendas físicas y plan de ruta</h3>
            </div>
            <div class="actions-group">
              <button type="button" class="secondary small-btn" (click)="host.loadNearbyStores()" [disabled]="host.storesLoading()">Actualizar distancias</button>
              <button type="button" class="secondary small-btn" (click)="host.seedStores()" [disabled]="host.storesLoading()">Cargar tiendas Medellín</button>
            </div>
          </div>

          @if (host.storesLoading()) { <p role="status" class="muted">Calculando distancias a tiendas…</p> }
          @if (host.storesError()) { <p class="market-error" role="alert">{{ host.storesError() }}</p> }

          @if (host.nearbyStores().length > 0) {
            <ul class="store-list">
              @for (st of host.nearbyStores(); track st.id) {
                <li [class.selected]="host.selectedStoreIds().includes(st.id)">
                  <label class="store-checkbox">
                    <input type="checkbox" [checked]="host.selectedStoreIds().includes(st.id)" (change)="host.toggleStoreSelection(st.id)">
                    <div class="store-info">
                      <strong>{{ st.name }}</strong>
                      <span class="store-brand">{{ st.brand }}</span>
                      <small class="muted">{{ st.address }}</small>
                    </div>
                  </label>
                  <div class="store-distance">
                    @if (st.distanceKm !== null) {
                      <span class="dist-tag">{{ st.distanceKm | number:'1.2-2' }} km</span>
                    }
                  </div>
                </li>
              }
            </ul>

            <div class="route-params-row">
              <label>
                Modo:
                <select [ngModel]="host.transportMode()" (ngModelChange)="host.transportMode.set($event)">
                  <option value="Driving">Vehículo</option>
                  <option value="Motorcycle">Motocicleta</option>
                  <option value="Bicycling">Bicicleta</option>
                  <option value="Walking">Caminando</option>
                </select>
              </label>
              <button type="button" class="primary small-btn" (click)="host.optimizeRoute()" [disabled]="host.routeBusy() || host.selectedStoreIds().length === 0">
                {{ host.routeBusy() ? 'Calculando…' : 'Calcular ruta' }}
              </button>
            </div>

            @if (host.routePlan(); as plan) {
              <div class="route-results-panel">
                <div class="route-summary-cards">
                  <div class="route-card">
                    <span>DISTANCIA</span>
                    <strong>{{ plan.totalDistanceKm | number:'1.2-2' }} km</strong>
                  </div>
                  <div class="route-card">
                    <span>TIEMPO</span>
                    <strong>{{ plan.totalDurationMinutes | number:'1.0-0' }} min</strong>
                  </div>
                  <div class="route-card">
                    <span>COSTO</span>
                    <strong>${{ plan.estimatedTravelCost | number:'1.0-0' }} COP</strong>
                  </div>
                </div>
                @if (plan.googleMapsNavigationUrl) {
                  <div class="route-navigation-action">
                    <a [href]="plan.googleMapsNavigationUrl" target="_blank" rel="noopener noreferrer" class="primary-btn-link">
                      Abrir ruta en Google Maps ↗
                    </a>
                  </div>
                }
              </div>
            }
          }
        </div>
      </div>
    </details>
  </main>
</div>

<!-- Modal: Crear lista -->
@if (isCreateModalOpen) {
  <div class="modal-backdrop" (click)="isCreateModalOpen = false" aria-hidden="true"></div>
  <div class="modal-dialog panel" role="dialog" aria-modal="true" aria-label="Crear nueva lista">
    <div class="modal-header">
      <h3>Crear nueva lista de compras</h3>
      <button type="button" class="ghost icon-btn" (click)="isCreateModalOpen = false">✕</button>
    </div>
    <div class="modal-body">
      <div class="form-group">
        <label for="modal-basket-name">Nombre de la lista:</label>
        <input
          id="modal-basket-name"
          type="text"
          [(ngModel)]="newBasketName"
          placeholder="Ej. Mercado semanal, Asado domingo…"
          (keyup.enter)="confirmCreate()">
      </div>
      <div class="form-group">
        <label for="modal-basket-budget">Presupuesto estimado (opcional):</label>
        <input
          id="modal-basket-budget"
          type="number"
          [(ngModel)]="newBasketBudget"
          placeholder="Ej. 150000">
      </div>
    </div>
    <div class="modal-footer">
      <button type="button" class="secondary" (click)="isCreateModalOpen = false">Cancelar</button>
      <button type="button" class="primary" (click)="confirmCreate()" [disabled]="!newBasketName.trim()">Guardar lista</button>
    </div>
  </div>
}

<!-- Modal: Renombrar lista -->
@if (isRenameModalOpen) {
  <div class="modal-backdrop" (click)="isRenameModalOpen = false" aria-hidden="true"></div>
  <div class="modal-dialog panel" role="dialog" aria-modal="true" aria-label="Renombrar lista">
    <div class="modal-header">
      <h3>Renombrar lista</h3>
      <button type="button" class="ghost icon-btn" (click)="isRenameModalOpen = false">✕</button>
    </div>
    <div class="modal-body">
      <div class="form-group">
        <label for="rename-basket-name">Nuevo nombre:</label>
        <input
          id="rename-basket-name"
          type="text"
          [(ngModel)]="renameBasketName"
          (keyup.enter)="confirmRename()">
      </div>
    </div>
    <div class="modal-footer">
      <button type="button" class="secondary" (click)="isRenameModalOpen = false">Cancelar</button>
      <button type="button" class="primary" (click)="confirmRename()" [disabled]="!renameBasketName.trim()">Guardar cambios</button>
    </div>
  </div>
}

<!-- Modal: Confirmar eliminación de lista -->
@if (deletingBasket; as b) {
  <div class="modal-backdrop" (click)="deletingBasket = null" aria-hidden="true"></div>
  <div class="modal-dialog panel" role="dialog" aria-modal="true" aria-label="Confirmar eliminación">
    <div class="modal-header">
      <h3>¿Eliminar «{{ b.name }}»?</h3>
      <button type="button" class="ghost icon-btn" (click)="deletingBasket = null">✕</button>
    </div>
    <div class="modal-body">
      <p>Se eliminará esta lista y los productos agregados a ella. El catálogo y el historial de precios permanecerán disponibles.</p>
    </div>
    <div class="modal-footer">
      <button type="button" class="secondary" (click)="deletingBasket = null">Cancelar</button>
      <button type="button" class="primary text-danger" (click)="confirmDelete()">Eliminar lista</button>
    </div>
  </div>
}
"""
with open('src/frontend/src/app/pages/market.html', 'w', encoding='utf-8') as f:
    f.write(content)
print("Updated market.html successfully")
