import sys
sys.stdout.reconfigure(encoding='utf-8')

# 1. Update home.html
with open('src/frontend/src/app/pages/home.html', 'r', encoding='utf-8') as f:
    home = f.read()

# Replace the 4th KPI in home.html to include MÁX. TIENDAS
old_kpi = """  <article>
    <span>TIENDAS ACTUALIZADAS</span>
    <strong>3 de 3</strong>
    <small>D1, Éxito y Ara sincronizados</small>
  </article>"""
new_kpi = """  <article>
    <span>MÁX. TIENDAS</span>
    <strong>{{ host.settings()?.maxStores ?? 2 }}</strong>
    <small>Configurable en ajustes</small>
  </article>"""
if old_kpi in home:
    home = home.replace(old_kpi, new_kpi)
else:
    # try flexible replace
    home = home.replace('<span>TIENDAS ACTUALIZADAS</span>', '<span>MÁX. TIENDAS</span>')
    home = home.replace('<strong>3 de 3</strong>', '<strong>{{ host.settings()?.maxStores ?? 2 }}</strong>')
    home = home.replace('<small>D1, Éxito y Ara sincronizados</small>', '<small>Configurable en ajustes</small>')

with open('src/frontend/src/app/pages/home.html', 'w', encoding='utf-8') as f:
    f.write(home)
print("Updated home.html")

# 2. Update market.html
with open('src/frontend/src/app/pages/market.html', 'r', encoding='utf-8') as f:
    market = f.read()

# Heading description
market = market.replace(
    '<p class="muted">Organiza tus compras, compara alternativas y optimiza el ahorro total entre tiendas.</p>',
    '<p class="muted">Organiza tus compras, compara alternativas y optimiza el ahorro total entre tiendas. Esta lista no muestra precios ni compara tiendas.</p>'
)

# market-actions buttons
old_actions = """    <div class="market-actions">
      <h2>Tus listas</h2>
      <button
        type="button"
        class="primary small-btn"
        (click)="openCreateModal()"
        [disabled]="host.marketBusy() || host.marketLoading()">
        + Nueva lista
      </button>
    </div>"""

new_actions = """    <div class="market-actions">
      <h2>Tus listas</h2>
      <div class="action-buttons-group">
        <button
          type="button"
          class="primary small-btn"
          (click)="host.createBasket()"
          [disabled]="host.marketBusy() || host.marketLoading() || !!host.deletingItemId()">
          {{ host.marketBusy() ? 'Guardando…' : 'Crear lista' }}
        </button>
        <button
          type="button"
          class="secondary small-btn"
          (click)="openCreateModal()"
          [disabled]="host.marketBusy() || host.marketLoading()">
          + Nueva lista con nombre
        </button>
        @if (host.selectedBasket?.items?.length) {
          <button
            type="button"
            class="secondary small-btn"
            (click)="host.syncBasket()"
            [disabled]="host.basketSyncBusy() || host.marketBusy() || host.marketLoading()">
            {{ host.basketSyncBusy() ? 'Sincronizando cesta…' : 'Sincronizar precios de la lista' }}
          </button>
        }
      </div>
    </div>"""

if old_actions in market:
    market = market.replace(old_actions, new_actions)
else:
    print("Warning: old_actions not found directly, checking...")

with open('src/frontend/src/app/pages/market.html', 'w', encoding='utf-8') as f:
    f.write(market)
print("Updated market.html")
