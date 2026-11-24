const fs = require('fs');
const path = require('path');

// Leemos el contenido existente de market.html para extraer los paneles de ubicación y optimización sin alteraciones
const oldMarket = fs.readFileSync(path.resolve(__dirname, '../src/frontend/src/app/pages/market.html'), 'utf8');

// Extraemos desde <section class="panel location-panel" hasta el final
const locIdx = oldMarket.indexOf('<section class="panel location-panel"');
const remainingPanels = locIdx !== -1 ? oldMarket.slice(locIdx) : '';

const headerAndBaskets = `<div class="heading">
  <div>
    <p class="eyebrow">TUS LISTAS LOCALES</p>
    <h1>Tus listas de compras</h1>
    <p class="muted">Organiza diferentes compras y encuentra la mejor combinación de tiendas para cada una. Esta lista no muestra precios ni compara tiendas.</p>
  </div>
</div>

<section class="panel market-panel" aria-label="Listas de mercado">
  <div class="market-actions">
    <h2>Listas</h2>
    <div class="action-buttons-group">
      @if (host.selectedBasket?.items?.length) {
        <button
          type="button"
          class="secondary"
          (click)="host.syncBasket()"
          [disabled]="host.basketSyncBusy() || host.marketBusy() || host.marketLoading()">
          {{ host.basketSyncBusy() ? 'Sincronizando cesta…' : 'Sincronizar precios de la lista' }}
        </button>
      }
      <button
        type="button"
        class="secondary"
        (click)="openCreateModal()"
        [disabled]="host.marketBusy() || host.marketLoading()">
        + Nueva lista
      </button>
      <button
        type="button"
        class="primary"
        (click)="host.createBasket()"
        [disabled]="host.marketBusy() || host.marketLoading() || !!host.deletingItemId()">
        {{ host.marketBusy() ? 'Guardando…' : 'Crear lista' }}
      </button>
    </div>
  </div>

  @if (host.basketSyncNotice()) { <p class="notice" role="status">{{ host.basketSyncNotice() }}</p> }
  @if (host.basketSyncError()) { <p class="market-error" role="alert">{{ host.basketSyncError() }}</p> }
  @if (host.marketLoading()) { <p role="status">Cargando listas…</p> }
  @if (host.marketError()) {
    <div class="market-error" role="alert">
      {{ host.marketError() }}
      <button type="button" class="secondary" (click)="host.loadBaskets()" [disabled]="host.marketLoading() || host.marketBusy()">Reintentar</button>
    </div>
  }
  @if (!host.marketLoading() && !host.baskets().length && !host.marketError()) {
    <p class="muted">Aún no tienes listas. Crea una para comenzar.</p>
  }

  <!-- Selector dropdown (para compatibilidad de pruebas de interfaz) -->
  @if (host.baskets().length) {
    <div class="basket-selector">
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

    <!-- Cards visuales de listas de compras -->
    <div class="baskets-cards-grid">
      @for (b of host.baskets(); track b.id) {
        <div class="basket-card panel" [class.active-basket-card]="b.id === host.selectedBasketId()">
          <div class="basket-card-header">
            <div>
              <h3>{{ b.name }}</h3>
              <span class="muted small">{{ b.items.length }} productos</span>
            </div>
            @if (b.id === host.selectedBasketId()) {
              <span class="status-pill status-exact">Activa</span>
            }
          </div>
          <div class="basket-card-actions">
            @if (b.id !== host.selectedBasketId()) {
              <button type="button" class="secondary small-btn" (click)="host.selectedBasketId.set(b.id)">Abrir</button>
            }
            <button type="button" class="ghost small-btn" (click)="duplicate(b.id)" title="Duplicar lista">Duplicar</button>
            <button type="button" class="ghost small-btn" (click)="openRenameModal(b)" title="Renombrar lista">Renombrar</button>
            <button type="button" class="ghost small-btn text-danger" (click)="openDeleteModal(b)" title="Eliminar lista">Eliminar</button>
          </div>
        </div>
      }
    </div>
  }
</section>

<!-- Modal: Crear lista -->
@if (isCreateModalOpen) {
  <div class="assistant-backdrop" (click)="isCreateModalOpen = false" aria-hidden="true"></div>
  <div class="modal-dialog panel" role="dialog" aria-modal="true" aria-label="Crear nueva lista">
    <div class="modal-header">
      <h3>Crear nueva lista de compras</h3>
      <button type="button" class="ghost icon-btn" (click)="isCreateModalOpen = false">✕</button>
    </div>
    <div class="modal-body">
      <label for="modal-basket-name">Nombre de la lista:</label>
      <input
        id="modal-basket-name"
        type="text"
        [(ngModel)]="newBasketName"
        placeholder="Ej. Mercado mensual, Asado domingo…"
        (keyup.enter)="confirmCreate()">
      <label for="modal-basket-budget">Presupuesto estimado (opcional):</label>
      <input
        id="modal-basket-budget"
        type="number"
        [(ngModel)]="newBasketBudget"
        placeholder="Ej. 150000">
    </div>
    <div class="modal-footer">
      <button type="button" class="secondary" (click)="isCreateModalOpen = false">Cancelar</button>
      <button type="button" class="primary" (click)="confirmCreate()" [disabled]="!newBasketName.trim()">Guardar lista</button>
    </div>
  </div>
}

<!-- Modal: Renombrar lista -->
@if (isRenameModalOpen) {
  <div class="assistant-backdrop" (click)="isRenameModalOpen = false" aria-hidden="true"></div>
  <div class="modal-dialog panel" role="dialog" aria-modal="true" aria-label="Renombrar lista">
    <div class="modal-header">
      <h3>Renombrar lista</h3>
      <button type="button" class="ghost icon-btn" (click)="isRenameModalOpen = false">✕</button>
    </div>
    <div class="modal-body">
      <label for="rename-basket-name">Nuevo nombre:</label>
      <input
        id="rename-basket-name"
        type="text"
        [(ngModel)]="renameBasketName"
        (keyup.enter)="confirmRename()">
    </div>
    <div class="modal-footer">
      <button type="button" class="secondary" (click)="isRenameModalOpen = false">Cancelar</button>
      <button type="button" class="primary" (click)="confirmRename()" [disabled]="!renameBasketName.trim()">Guardar cambios</button>
    </div>
  </div>
}

<!-- Modal: Confirmar eliminación de lista -->
@if (deletingBasket; as b) {
  <div class="assistant-backdrop" (click)="deletingBasket = null" aria-hidden="true"></div>
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

@if (host.selectedBasket; as basket) {
  <section class="panel market-panel" aria-label="Productos de la lista">
    <h2>Productos de {{ basket.name }}</h2>
    <p class="muted">Anota productos libremente; no están vinculados al catálogo ni tienen precio verificado.</p>
    <form class="item-form" (ngSubmit)="host.addItem()" novalidate>
      <label>Producto <input name="query" type="text" [(ngModel)]="host.query" maxlength="500" [disabled]="host.marketBusy() || !!host.deletingItemId()" placeholder="Ej. Arroz"></label>
      <label>Cantidad <input name="quantity" type="number" min="0.01" step="any" [(ngModel)]="host.quantity" [disabled]="host.marketBusy() || !!host.deletingItemId()"></label>
      <label class="notes-field">Notas (opcional) <textarea name="notes" [(ngModel)]="host.notes" maxlength="2000" rows="2" [disabled]="host.marketBusy() || !!host.deletingItemId()"></textarea></label>
      <button class="primary" type="submit" [disabled]="host.marketBusy() || !!host.deletingItemId()">{{ host.marketBusy() ? 'Agregando…' : 'Agregar producto' }}</button>
    </form>
    @if (host.itemError()) { <p class="market-error" role="alert">{{ host.itemError() }}</p> }
    @if (!basket.items.length) { <p class="muted" role="status">Esta lista está vacía. Agrega tu primer producto.</p> }
    @else { <ul class="item-list">@for (item of basket.items; track item.id) {
      <li><div><strong>{{ item.query }}</strong><span>Cantidad: {{ item.quantity }}</span>@if (item.notes) { <small>{{ item.notes }}</small> }</div><button type="button" class="secondary" (click)="host.deleteItem(item)" [disabled]="host.marketBusy() || !!host.deletingItemId()" [attr.aria-label]="'Quitar ' + item.query">{{ host.deletingItemId() === item.id ? 'Quitando…' : 'Quitar' }}</button></li>
    }</ul> }
  </section>
}
`;

const finalMarket = headerAndBaskets + '\n' + remainingPanels;
const fullPath = path.resolve(__dirname, '..', 'src/frontend/src/app/pages/market.html');
fs.writeFileSync(fullPath, finalMarket, 'utf8');
console.log('Wrote market.html (' + finalMarket.length + ' chars)');
