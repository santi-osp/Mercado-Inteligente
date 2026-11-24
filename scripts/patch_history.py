import sys
sys.stdout.reconfigure(encoding='utf-8')

history_html = """<div class="heading">
  <div>
    <p class="eyebrow">HISTORIAL Y TENDENCIAS</p>
    <h1>Historial de precios</h1>
    <p class="muted">Busca un producto para ver su historial y cómo ha variado su precio en el tiempo.</p>
  </div>
  <div class="history-actions">
    <button type="button" class="primary small-btn" (click)="host.exportPricesCsv()">
      Descargar CSV
    </button>
    <button type="button" class="secondary small-btn" (click)="host.loadHistory()" [disabled]="host.historyLoading()">
      {{ host.historyLoading() ? 'Actualizando…' : 'Refrescar historial' }}
    </button>
  </div>
</div>

<!-- Barra de búsqueda product-first -->
<section class="panel history-filter-panel" aria-label="Búsqueda de historial">
  <div class="history-search-hero">
    <div class="search-input-wrapper">
      <input
        type="text"
        [(ngModel)]="host.historyFilterQuery"
        placeholder="Busca un producto para ver su historial (ej. Arroz, Leche, Huevos)"
        (keyup.enter)="host.loadHistory()"
        class="history-main-input">
      <button type="button" class="primary" (click)="host.loadHistory()" [disabled]="host.historyLoading()">
        Buscar historial
      </button>
    </div>

    <!-- Filtros secundarios en una fila limpia -->
    <div class="history-subfilters">
      <div class="filter-control">
        <label for="history-retailer">Tienda:</label>
        <select id="history-retailer" [(ngModel)]="host.historyFilterRetailer" (change)="host.loadHistory()">
          <option value="">Todas las tiendas</option>
          <option value="D1">Tiendas D1</option>
          <option value="EXITO">Almacenes Éxito</option>
          <option value="ARA">Tiendas Ara</option>
        </select>
      </div>

      <div class="filter-control">
        <label for="history-scope">Tipo de precio:</label>
        <select id="history-scope" [(ngModel)]="host.historyFilterScope" (change)="host.loadHistory()">
          <option value="">Todos los alcances</option>
          <option value="OnlineGeneric">Online (Catálogo general)</option>
          <option value="PhysicalVerified">Físico góndola (Verificado)</option>
          <option value="Unknown">No especificado</option>
        </select>
      </div>
    </div>
  </div>
</section>

@if (host.historyLoading()) {
  <div class="empty" role="status">Cargando observaciones de precio…</div>
}
@if (host.historyError()) {
  <p class="market-error" role="alert">{{ host.historyError() }}</p>
}

@if (host.historyData(); as h) {
  <!-- KPIs de producto / agregados -->
  <section class="panel history-stats-panel" aria-label="Estadísticas de observaciones">
    <div class="stats-header">
      <h2>Resumen de precios y comportamiento</h2>
      <span class="muted small">{{ h.statistics.totalObservations }} observaciones registradas</span>
    </div>

    <div class="storage-stats-grid history-kpi-grid">
      <div class="storage-stat-card">
        <span>PRECIO ACTUAL</span>
        <strong>${{ (h.statistics.minPrice ?? 0) | number:'1.0-0' }}</strong>
        <small>Último valor registrado</small>
      </div>
      <div class="storage-stat-card">
        <span>PRECIO MÍNIMO</span>
        <strong class="text-success">${{ (h.statistics.minPrice ?? 0) | number:'1.0-0' }}</strong>
        <small>Mejor oportunidad histórica</small>
      </div>
      <div class="storage-stat-card">
        <span>PRECIO PROMEDIO</span>
        <strong>${{ (h.statistics.avgPrice ?? 0) | number:'1.0-0' }}</strong>
        <small>Media ponderada</small>
      </div>
      <div class="storage-stat-card">
        <span>PRECIO MÁXIMO</span>
        <strong class="text-warning">${{ (h.statistics.maxPrice ?? 0) | number:'1.0-0' }}</strong>
        <small>Pico más alto</small>
      </div>
      <div class="storage-stat-card">
        <span>VARIACIÓN 30 DÍAS</span>
        <strong class="text-primary">{{ h.statistics.maxPrice && h.statistics.minPrice ? ((h.statistics.maxPrice - h.statistics.minPrice) / h.statistics.minPrice * 100 | number:'1.1-1') + '%' : '0.0%' }}</strong>
        <small>Rango de fluctuación</small>
      </div>
    </div>
  </section>

  <!-- Gráfico de tendencia visual (SVG) con Timeframes -->
  <section class="panel history-chart-panel" aria-label="Evolución temporal de precios">
    <div class="chart-header-row">
      <div>
        <h2>Evolución del precio</h2>
        <p class="muted">Tendencia de precios en el tiempo por tienda</p>
      </div>
      <!-- Selector de periodo (Timeframes) -->
      <div class="timeframe-selector" role="group" aria-label="Periodo temporal">
        <button type="button" class="timeframe-btn active">30 días</button>
        <button type="button" class="timeframe-btn">3 meses</button>
        <button type="button" class="timeframe-btn">1 año</button>
        <button type="button" class="timeframe-btn">Todo</button>
      </div>
    </div>

    <div class="chart-container">
      <svg class="price-trend-svg" viewBox="0 0 600 180" preserveAspectRatio="none" aria-label="Gráfica de tendencia">
        <!-- Grid lines -->
        <line x1="40" y1="20" x2="580" y2="20" stroke="#E5E7EB" stroke-dasharray="3,3" />
        <line x1="40" y1="70" x2="580" y2="70" stroke="#E5E7EB" stroke-dasharray="3,3" />
        <line x1="40" y1="120" x2="580" y2="120" stroke="#E5E7EB" stroke-dasharray="3,3" />
        <line x1="40" y1="160" x2="580" y2="160" stroke="#D1D5DB" />
        
        <!-- Y-axis labels -->
        <text x="35" y="24" text-anchor="end" font-size="10" fill="#9CA3AF">${{ ((h.statistics.maxPrice ?? 5000) | number:'1.0-0') }}</text>
        <text x="35" y="74" text-anchor="end" font-size="10" fill="#9CA3AF">${{ ((h.statistics.avgPrice ?? 4000) | number:'1.0-0') }}</text>
        <text x="35" y="124" text-anchor="end" font-size="10" fill="#9CA3AF">${{ ((h.statistics.minPrice ?? 3000) | number:'1.0-0') }}</text>

        <!-- Area under curve -->
        <polygon points="50,160 50,110 160,115 280,100 400,105 520,80 570,85 570,160" fill="rgba(22, 125, 90, 0.08)" />

        <!-- Line curve -->
        <polyline
          points="50,110 160,115 280,100 400,105 520,80 570,85"
          fill="none"
          stroke="#167D5A"
          stroke-width="3"
          stroke-linecap="round"
          stroke-linejoin="round" />

        <!-- Data points -->
        <circle cx="50" cy="110" r="4" fill="#167D5A" stroke="#FFFFFF" stroke-width="2" />
        <circle cx="160" cy="115" r="4" fill="#167D5A" stroke="#FFFFFF" stroke-width="2" />
        <circle cx="280" cy="100" r="4" fill="#167D5A" stroke="#FFFFFF" stroke-width="2" />
        <circle cx="400" cy="105" r="4" fill="#167D5A" stroke="#FFFFFF" stroke-width="2" />
        <circle cx="520" cy="80" r="4" fill="#167D5A" stroke="#FFFFFF" stroke-width="2" />
        <circle cx="570" cy="85" r="4" fill="#167D5A" stroke="#FFFFFF" stroke-width="2" />

        <!-- X-axis dates -->
        <text x="50" y="174" text-anchor="middle" font-size="10" fill="#9CA3AF">Hace 30d</text>
        <text x="280" y="174" text-anchor="middle" font-size="10" fill="#9CA3AF">Hace 15d</text>
        <text x="570" y="174" text-anchor="middle" font-size="10" fill="#9CA3AF">Hoy</text>
      </svg>
    </div>
  </section>

  @if (h.productTrends && h.productTrends.length > 0) {
    <section class="panel history-trends-panel" aria-label="Tendencias por producto">
      <h2>Tendencias por producto y tienda</h2>
      <div class="table-container">
        <table class="market-table" aria-label="Tendencias de precios">
          <thead>
            <tr>
              <th scope="col">Comercio</th>
              <th scope="col">Producto</th>
              <th scope="col">SKU</th>
              <th scope="col">Registros</th>
              <th scope="col">Precio actual</th>
              <th scope="col">Rango (Mín - Máx)</th>
              <th scope="col">Variación</th>
              <th scope="col">Última auditoría</th>
            </tr>
          </thead>
          <tbody>
            @for (trend of h.productTrends; track trend.skuName + trend.retailerCode) {
              <tr>
                <td><span class="badge badge-retailer">{{ trend.retailerCode }}</span></td>
                <td><strong>{{ trend.productName }}</strong></td>
                <td class="muted">{{ trend.skuName }}</td>
                <td>{{ trend.count }}</td>
                <td><strong>${{ trend.latestPrice | number:'1.0-0' }}</strong></td>
                <td>${{ trend.minPrice | number:'1.0-0' }} - ${{ trend.maxPrice | number:'1.0-0' }}</td>
                <td>
                  @if (trend.priceDelta > 0) {
                    <span class="delta-badge delta-up">+${{ trend.priceDelta | number:'1.0-0' }}</span>
                  } @else if (trend.priceDelta < 0) {
                    <span class="delta-badge delta-down">-${{ (trend.priceDelta * -1) | number:'1.0-0' }}</span>
                  } @else {
                    <span class="delta-badge delta-neutral">= $0</span>
                  }
                </td>
                <td class="muted">{{ trend.latestCheckedAt | date:'medium' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    </section>
  }

  <!-- Observaciones individuales -->
  <section class="panel history-observations-panel" aria-label="Observaciones individuales">
    <div class="section-header-compact">
      <h2>Observaciones individuales registradas ({{ h.observations.length }})</h2>
      <span class="muted small">Trazabilidad inmutable de precios</span>
    </div>
    @if (h.observations.length === 0) {
      <div class="empty">No se encontraron observaciones con los filtros seleccionados.</div>
    } @else {
      <div class="table-container">
        <table class="market-table" aria-label="Observaciones individuales">
          <thead>
            <tr>
              <th scope="col">Fecha</th>
              <th scope="col">Comercio</th>
              <th scope="col">Producto</th>
              <th scope="col">Precio</th>
              <th scope="col">Precio lista</th>
              <th scope="col">Alcance</th>
              <th scope="col">Contexto / Folleto</th>
            </tr>
          </thead>
          <tbody>
            @for (obs of h.observations; track obs.id) {
              <tr>
                <td class="muted">{{ obs.checkedAt | date:'short' }}</td>
                <td><span class="badge badge-retailer">{{ obs.retailerName }}</span></td>
                <td>
                  <strong>{{ obs.productName }}</strong>
                  @if (obs.brand) { <span class="muted">· {{ obs.brand }}</span> }
                </td>
                <td><strong>${{ obs.price | number:'1.0-0' }}</strong></td>
                <td class="muted">{{ obs.listPrice ? ('$' + (obs.listPrice | number:'1.0-0')) : '—' }}</td>
                <td>
                  <span class="badge" [class.badge-verified]="obs.scope === 'PhysicalVerified'" [class.badge-online]="obs.scope === 'OnlineGeneric'">
                    {{ obs.scope === 'PhysicalVerified' ? 'Físico góndola' : obs.scope === 'OnlineGeneric' ? 'Online' : obs.scope }}
                  </span>
                </td>
                <td class="muted">{{ obs.context }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  </section>
}
"""

with open('src/frontend/src/app/pages/history.html', 'w', encoding='utf-8') as f:
    f.write(history_html)
print("Updated history.html successfully")
