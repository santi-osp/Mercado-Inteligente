const fs = require('fs');
const p = 'src/frontend/src/app/pages/market.html';
let content = fs.readFileSync(p, 'utf8');

const oldGroup = /<div class="action-buttons-group">[\s\S]*?<\/div>[\r\n\s]*<\/div>/;
const newGroup = `<div class="action-buttons-group">
      <button
        type="button"
        class="primary"
        (click)="host.createBasket()"
        [disabled]="host.marketBusy() || host.marketLoading() || !!host.deletingItemId()">
        {{ host.marketBusy() ? 'Guardando…' : 'Crear lista' }}
      </button>
      <button
        type="button"
        class="secondary"
        (click)="openCreateModal()"
        [disabled]="host.marketBusy() || host.marketLoading()">
        + Nueva lista con nombre
      </button>
      @if (host.selectedBasket?.items?.length) {
        <button
          type="button"
          class="secondary"
          (click)="host.syncBasket()"
          [disabled]="host.basketSyncBusy() || host.marketBusy() || host.marketLoading()">
          {{ host.basketSyncBusy() ? 'Sincronizando cesta…' : 'Sincronizar precios de la lista' }}
        </button>
      }
    </div>
  </div>`;

if (oldGroup.test(content)) {
  content = content.replace(oldGroup, newGroup);
  fs.writeFileSync(p, content, 'utf8');
  console.log('market.html buttons reordered');
} else {
  console.log('pattern not found in market.html');
}
