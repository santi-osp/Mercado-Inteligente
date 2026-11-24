const fs = require('fs');
const path = require('path');

function write(relPath, content) {
  const full = path.resolve(__dirname, '..', relPath);
  fs.mkdirSync(path.dirname(full), { recursive: true });
  fs.writeFileSync(full, content, 'utf8');
  console.log(`Wrote: ${relPath} (${content.length} chars)`);
}

// 1. APP.HTML
write('src/frontend/src/app/app.html', `<div class="layout">
  <aside class="sidebar">
    <div class="brand"><span class="logo">m.</span><div>Mercado<br><strong>Inteligente</strong></div></div>
    <p class="eyebrow nav-label">ESPACIO DE TRABAJO</p>
    <nav aria-label="Navegación principal">
      @for (item of nav; track item) {
        <button type="button" [class.active]="page() === item" [attr.aria-current]="page() === item ? 'page' : null" (click)="navigate(item)"><span class="nav-icon" aria-hidden="true">{{ ['⌂','⌕','▣','⇄','◷','◫','⚙'][$index] }}</span>{{ item === 'Fuentes' ? 'Tiendas' : item }}</button>
      }
    </nav>
    <div class="sidebar-footer"><span class="status-dot"></span> Local · {{ health()?.database === 'ok' ? 'Base local activa' : 'Sin conexión' }}<small>{{ currentAddress() || 'Ubicación no seleccionada' }}</small></div>
  </aside>
  <main>
    <header class="topbar"><div class="mobile-brand">m. <strong>Mercado Inteligente</strong></div><span>◉ {{ currentAddress() || 'Ubicación no seleccionada' }}</span><span class="pill">{{ health()?.database === 'ok' ? 'Sistema local activo' : 'Sin conexión' }}</span></header>
    <div class="content">
      @if (connectionError()) {
        <div class="offline-banner" role="alert">
          <span>Modo sin conexión · Los datos locales permanecen seguros en tu equipo. Las sincronizaciones externas se reanudarán al reconectar.</span>
        </div>
      }
      @if (message()) { <div class="notice" [attr.role]="connectionError() ? 'alert' : 'status'" aria-live="polite">{{ message() }} @if (connectionError()) { <button type="button" class="secondary" (click)="refresh()">Reintentar conexión</button> }</div> }
      <router-outlet />
      <footer>Mercado Inteligente · Tus datos permanecen en este equipo</footer>
    </div>
  </main>
</div>

<!-- Asistente IA Flotante (FAB) -->
<button
  type="button"
  id="shopping-assistant-fab"
  class="shopping-assistant-fab"
  aria-label="Abrir asistente de compras"
  (click)="toggleAssistant()"
  title="Asistente de compras">
  <span class="assistant-fab-icon" aria-hidden="true">✨</span>
  <span class="assistant-fab-label">Asistente IA</span>
</button>

<!-- Drawer del Asistente IA -->
@if (shoppingAssistantFacade.isOpen()) {
  <div class="assistant-backdrop" (click)="closeAssistant()" aria-hidden="true"></div>
  <aside
    id="shopping-assistant-drawer"
    class="shopping-assistant-drawer"
    role="dialog"
    aria-modal="true"
    aria-label="Asistente de compras">
    <header class="assistant-drawer-header">
      <div class="assistant-header-title">
        <span class="assistant-avatar" aria-hidden="true">✨</span>
        <div>
          <h3>Asistente de compras</h3>
          <p class="muted">Pregúntame sobre precios, opciones o ahorros</p>
        </div>
      </div>
      <div class="assistant-header-actions">
        <button type="button" class="ghost icon-btn" (click)="shoppingAssistantFacade.clearHistory()" title="Limpiar historial">🗑</button>
        <button type="button" class="ghost icon-btn" (click)="closeAssistant()" aria-label="Cerrar asistente">✕</button>
      </div>
    </header>

    <div class="assistant-chips" aria-label="Preguntas sugeridas">
      <button type="button" class="chip-btn" (click)="sendQuickQuestion('¿Cuál producto conviene más?')">¿Cuál conviene más?</button>
      <button type="button" class="chip-btn" (click)="sendQuickQuestion('¿Cuál tiene mejor precio por kilo?')">¿Mejor precio por kilo?</button>
      <button type="button" class="chip-btn" (click)="sendQuickQuestion('¿Qué opción comprarías tú?')">¿Qué opción comprarías?</button>
      <button type="button" class="chip-btn" (click)="sendQuickQuestion('¿Hay una presentación más económica?')">¿Presentación más económica?</button>
      <button type="button" class="chip-btn" (click)="sendQuickQuestion('¿Qué productos de mi lista están caros?')">¿Qué está caro en mi lista?</button>
    </div>

    <div class="assistant-messages">
      @if (!shoppingAssistantFacade.messages().length) {
        <div class="assistant-welcome">
          <span class="welcome-icon" aria-hidden="true">🛍️</span>
          <h4>¡Hola! Soy tu asistente de compras</h4>
          <p>Puedo ayudarte a comparar presentaciones, encontrar el mejor precio por kilo o sugerir dónde te rinde más tu presupuesto.</p>
          <p class="muted small">Tus preguntas se responden con base en los precios reales obtenidos de las tiendas.</p>
        </div>
      }
      @for (msg of shoppingAssistantFacade.messages(); track $index) {
        <div class="chat-bubble-wrapper" [class.user-wrapper]="msg.sender === 'user'" [class.bot-wrapper]="msg.sender === 'assistant'">
          <div class="chat-bubble" [class.chat-user]="msg.sender === 'user'" [class.chat-assistant]="msg.sender === 'assistant'">
            <p>{{ msg.text }}</p>
            @if (msg.recommendedProducts && msg.recommendedProducts.length > 0) {
              <div class="assistant-recommendations">
                <p class="rec-heading">Opciones recomendadas:</p>
                @for (rec of msg.recommendedProducts; track rec.productId + rec.retailer) {
                  <div class="rec-card">
                    <div class="rec-info">
                      <strong>{{ rec.name }}</strong>
                      @if (rec.brand) { <span class="rec-brand">{{ rec.brand }}</span> }
                      <span class="rec-retailer">{{ rec.retailer }}</span>
                    </div>
                    <div class="rec-pricing">
                      <span class="rec-price">\${{ rec.price | number:'1.0-0' }} COP</span>
                      @if (rec.unitPrice) { <span class="rec-unit">{{ rec.unitPrice }}</span> }
                    </div>
                    <div class="rec-actions">
                      @if (rec.productUrl) {
                        <a [href]="rec.productUrl" target="_blank" rel="noopener noreferrer" class="secondary small-btn">Ver en tienda ↗</a>
                      }
                      <button type="button" class="primary small-btn" (click)="addProductFromSearch(selectedBasketId() || '', { name: rec.name, brand: rec.brand, quantity: 1 })">+ Agregar</button>
                    </div>
                  </div>
                }
              </div>
            }
          </div>
        </div>
      }
      @if (shoppingAssistantFacade.loading()) {
        <div class="chat-bubble-wrapper bot-wrapper">
          <div class="chat-bubble chat-assistant loading-bubble">
            <span class="typing-dots">Analizando tus opciones…</span>
          </div>
        </div>
      }
    </div>

    <form class="assistant-input-form" (ngSubmit)="sendAssistantMessage()">
      <input
        type="text"
        [(ngModel)]="assistantInputText"
        name="assistantQuery"
        placeholder="Escribe tu consulta…"
        [disabled]="shoppingAssistantFacade.loading()"
        autocomplete="off">
      <button type="submit" class="primary" [disabled]="!assistantInputText.trim() || shoppingAssistantFacade.loading()">Enviar</button>
    </form>
  </aside>
}
`);

// 2. HOME.HTML
write('src/frontend/src/app/pages/home.html', `<div class="heading">
  <div>
    <p class="eyebrow">TU MERCADO, MÁS INTELIGENTE</p>
    <h1>Tu mercado, más inteligente</h1>
    <p class="muted">Compara precios, organiza tus compras y encuentra dónde te conviene comprar.</p>
  </div>
  <span class="date">Datos en tu equipo · Medellín</span>
</div>
<section class="hero">
  <div>
    <span class="hero-tag">COMIENZA AQUÍ</span>
    <h2>Tu mercado, una mejor decisión.</h2>
    <p>Agrega productos a tu lista y descubre las opciones disponibles. Siempre sabrás de dónde viene cada precio.</p>
    <button class="primary" (click)="host.navigate('Mi mercado')">Crear mi mercado ↗</button>
  </div>
  <div class="hero-art" aria-hidden="true">◎</div>
</section>
<div class="section-title"><h2>Resumen de hoy</h2><span class="muted">Información local</span></div>
<div class="stats">
  <article><span>PRODUCTOS</span><strong>{{ host.health()?.products ?? '—' }}</strong><small>En tu catálogo local</small></article>
  <article><span>AHORRO</span><strong>—</strong><small>Disponible al comparar</small></article>
  <article><span>RECORRIDO</span><strong>—</strong><small>Requiere ubicación y rutas</small></article>
  <article><span>MÁX. TIENDAS</span><strong>{{ host.settings()?.maxStores ?? '—' }}</strong><small>Configurable en ajustes</small></article>
</div>
<div class="section-title"><h2>Tu actividad</h2></div>
<div class="empty">
  <div class="empty-icon">▤</div>
  <h3>Todo empieza con una lista</h3>
  <p>Cuando agregues productos y consultes fuentes, encontrarás aquí tu actividad reciente.</p>
  <button class="secondary" (click)="host.navigate('Mi mercado')">Ir a mi mercado →</button>
</div>
`);

// 3. COMPARE.HTML
write('src/frontend/src/app/pages/compare.html', `<div class="heading">
  <div>
    <p class="eyebrow">EQUIVALENCIAS Y COMPARACIÓN DETERMINISTA</p>
    <h1>Compara antes de comprar</h1>
    <p class="muted">Revisa precios y presentaciones para elegir la opción que más te conviene.</p>
  </div>
</div>

@if (host.overrideNotice()) {
  <p class="notice" role="status">{{ host.overrideNotice() }}</p>
}

@if (host.comparisonData()?.summary; as s) {
  <div class="stats" aria-label="Métricas de evaluación del benchmark">
    <article>
      <span>PRECISIÓN EXACTA</span>
      <strong>{{ s.exactPrecision * 100 | number:'1.0-0' }}%</strong>
      <small>0 falsos positivos garantizados</small>
    </article>
    <article>
      <span>COBERTURA (RECALL)</span>
      <strong>{{ s.exactRecall * 100 | number:'1.0-0' }}%</strong>
      <small>Detección de pares idénticos</small>
    </article>
    <article>
      <span>PUNTUACIÓN F1</span>
      <strong>{{ s.exactF1 * 100 | number:'1.0-0' }}%</strong>
      <small>Armónica precisión/cobertura</small>
    </article>
    <article>
      <span>TOTAL PARES EVALUADOS</span>
      <strong>{{ s.totalEvaluated }}</strong>
      <small>{{ s.truePositives }} idénticos confirmados</small>
    </article>
  </div>
}

<div class="section-title">
  <h2>Grupos de productos comparados</h2>
  <span class="muted">{{ host.comparisonData()?.groups?.length ?? 0 }} grupos analizados</span>
</div>

@if (host.comparisonData()?.groups; as groups) {
  @if (!groups.length) {
    <div class="empty">
      <div class="empty-icon">⇄</div>
      <h3>Sin grupos para comparar</h3>
      <p>Sincroniza una cesta con productos de D1 y Éxito para ver las equivalencias detectadas automáticamente.</p>
      <button class="secondary" (click)="host.navigate('Mi mercado')">Ir a mi mercado →</button>
    </div>
  } @else {
    <div class="comparison-groups-grid">
      @for (group of groups; track group.groupId) {
        <article class="panel group-card">
          <div class="group-header">
            <div>
              <h3>{{ group.name }}</h3>
              @if (group.brand) { <span class="badge badge-brand">{{ group.brand }}</span> }
              <span class="badge badge-cat">{{ group.category }}</span>
            </div>
            <div class="group-status">
              @if (group.isIdenticalAcrossStores) {
                <span class="status-pill status-exact">✓ Idéntico entre tiendas</span>
              } @else {
                <span class="status-pill status-substitute">≈ Sustituto equivalente</span>
              }
            </div>
          </div>
          <div class="group-items">
            @for (item of group.items; track item.skuId) {
              <div class="group-item-row">
                <div>
                  <strong>{{ item.name }}</strong>
                  <small class="muted">{{ item.retailer }} · {{ item.quantity }} {{ item.unit }}</small>
                </div>
                <div class="group-item-price">
                  <strong>\${{ item.price | number:'1.0-0' }} COP</strong>
                  @if (item.unitPrice) { <small class="muted">{{ item.unitPrice }}</small> }
                </div>
              </div>
            }
          </div>
        </article>
      }
    </div>
  }
}
`);

// 4. HISTORY.HTML
write('src/frontend/src/app/pages/history.html', `<div class="heading">
  <div>
    <p class="eyebrow">TRAZABILIDAD Y EVOLUCIÓN TEMPORAL</p>
    <h1>Cómo han cambiado los precios</h1>
    <p class="muted">Consulta precios anteriores y cuándo fue actualizado cada producto.</p>
  </div>
  <div class="history-actions">
    <button type="button" class="primary" (click)="host.exportPricesCsv()">
      Descargar CSV (RFC-4180)
    </button>
    <button type="button" class="secondary" (click)="host.loadHistory()" [disabled]="host.historyLoading()">
      {{ host.historyLoading() ? 'Actualizando…' : 'Refrescar historial' }}
    </button>
  </div>
</div>

<section class="panel history-filter-panel" aria-label="Filtros de historial">
  <h2>Filtros de consulta</h2>
  <div class="history-filter-bar">
    <label>
      Producto o SKU:
      <input type="text" [(ngModel)]="host.historyFilterQuery" placeholder="Ej. Leche, Arroz, Huevos" (keyup.enter)="host.loadHistory()">
    </label>
    <label>
      Comercio:
      <select [(ngModel)]="host.historyFilterRetailer" (change)="host.loadHistory()">
        <option value="">Todos</option>
        <option value="D1">D1</option>
        <option value="EXITO">Éxito</option>
        <option value="ARA">Ara</option>
      </select>
    </label>
    <label>
      Días atrás:
      <input type="number" min="1" max="365" [(ngModel)]="host.historyFilterDays" (change)="host.loadHistory()">
    </label>
    <button type="button" class="primary" (click)="host.loadHistory()" [disabled]="host.historyLoading()">
      Filtrar
    </button>
  </div>
</section>

@if (host.historyLoading()) { <p role="status">Cargando historial de precios…</p> }
@if (host.historyError()) { <div class="market-error" role="alert">{{ host.historyError() }} <button type="button" class="secondary" (click)="host.loadHistory()">Reintentar</button></div> }

@if (host.historyObservations(); as obs) {
  <section class="panel" aria-label="Tabla de observaciones históricas">
    <div class="source-heading">
      <h2>Observaciones registradas ({{ obs.length }})</h2>
    </div>
    @if (!obs.length && !host.historyLoading()) {
      <p class="muted">No hay observaciones de precios que coincidan con los filtros.</p>
    } @else {
      <div class="table-wrapper">
        <table class="data-table">
          <thead>
            <tr>
              <th>Fecha / Hora</th>
              <th>Comercio</th>
              <th>Producto / Presentación</th>
              <th>Precio actual</th>
              <th>Precio de lista</th>
              <th>Alcance</th>
              <th>Disponibilidad</th>
            </tr>
          </thead>
          <tbody>
            @for (row of obs; track row.id) {
              <tr>
                <td>{{ row.observedAt | date:'short' }}</td>
                <td><span class="badge">{{ row.retailerCode }}</span></td>
                <td><strong>{{ row.productName }}</strong>@if (row.skuName) { <small class="muted"> · {{ row.skuName }}</small> }</td>
                <td><strong>\${{ row.price | number:'1.0-0' }} COP</strong></td>
                <td>{{ row.listPrice ? ('$' + (row.listPrice | number:'1.0-0') + ' COP') : '—' }}</td>
                <td><small>{{ row.priceScope }}</small></td>
                <td><span class="status-dot" [class.status-online]="row.availability === 'InStock' || row.availability === 1"></span> {{ row.availability === 'InStock' || row.availability === 1 ? 'Disponible' : 'Agotado' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  </section>
}
`);

// 5. SOURCES.HTML
write('src/frontend/src/app/pages/sources.html', `<div class="heading">
  <div>
    <p class="eyebrow">ESTADOS INFORMADOS POR LA API</p>
    <h1>Tiendas disponibles</h1>
    <p class="muted">Consulta qué tiendas están disponibles y cuándo obtuvimos precios por última vez.</p>
  </div>
</div>
<section class="panel" aria-label="Estados de fuentes">
  <div class="source-heading"><h2>Fuentes registradas</h2><button type="button" class="secondary" (click)="host.loadProviders()" [disabled]="host.providersLoading()">Actualizar estados</button></div>
  @if (host.providersLoading()) { <p role="status">Cargando estados de fuentes…</p> }
  @if (host.providersError()) { <div class="market-error" role="alert">{{ host.providersError() }} <button type="button" class="secondary" (click)="host.loadProviders()">Reintentar</button></div> }
  @if (host.providers(); as sources) {
    @if (!sources.length && !host.providersLoading()) { <p role="status">La API no informó fuentes.</p> }
    @else {
      <ul class="source-list">
        @for (source of sources; track source.code) {
          <li>
            <div class="source-row-main">
              <span class="status-dot" [class.status-online]="source.status.includes('HEALTHY') || source.status.includes('PARTIAL')" [class.status-warning]="source.status.includes('STALE')" [class.status-offline]="source.status.includes('UNAVAILABLE') || source.status.includes('NOT_IMPLEMENTED')"></span>
              <strong>{{ source.code }}</strong>
              <span class="source-status-badge">{{ source.status }}</span>
            </div>
            <p class="source-note muted">Estos estados son los que devuelve el servicio local; no una comprobación de Internet en tiempo real.</p>
          </li>
        }
      </ul>
    }
  }
</section>

<section class="panel" aria-label="Diagnóstico puntual D1">
  <div class="source-heading">
    <h2>D1 · Diagnóstico y última sincronización</h2>
    <button type="button" class="secondary" (click)="host.probeD1()" [disabled]="host.d1ProbeBusy()">
      {{ host.d1ProbeBusy() ? 'Comprobando…' : 'Comprobar D1 ahora' }}
    </button>
  </div>
  @if (host.d1Health(); as d1) {
    @if (d1.lastRun; as run) {
      <div class="sync-stat-card">
        <p><strong>Última ejecución:</strong> {{ run.finishedAt | date:'medium' }}</p>
        <p><strong>Estado:</strong> {{ run.state === 1 ? 'Exitosa' : 'Fallida' }} @if (run.errorCode) { <span class="text-danger">({{ run.errorCode }})</span> }</p>
        <p><strong>Artículos recibidos:</strong> {{ run.itemsReceived }} · <strong>Ofertas procesadas:</strong> {{ run.offersProcessed }}</p>
        <p><strong>RAW nuevos/deduplicados:</strong> {{ run.rawNew }}/{{ run.rawDeduplicated }}</p>
        @if (host.providersError()) {
          <p class="muted warning-note">El estado mostrado puede estar desactualizado debido a un error de conexión reciente.</p>
        }
      </div>
    } @else {
      <p class="muted">Sin sincronizaciones D1 registradas.</p>
    }
  }
  @if (host.d1ProbeResult(); as probe) {
    <div class="probe-result-card">
      <p><strong>Comprobación puntual D1:</strong> {{ probe.checkedAt | date:'medium' }}</p>
      <p>Búsqueda: <code>{{ probe.search }}</code></p>
      <p>Precio online genérico: {{ probe.genericOnlinePrice }}</p>
    </div>
  }
  @if (host.d1ProbeError()) {
    <p class="market-error" role="alert">{{ host.d1ProbeError() }}</p>
  }
</section>

<section class="panel" aria-label="Diagnóstico puntual Éxito">
  <div class="source-heading">
    <h2>Éxito · Última sincronización</h2>
  </div>
  @if (host.exitoHealth(); as exito) {
    @if (exito.lastRun; as run) {
      <div class="sync-stat-card">
        <p><strong>Última ejecución:</strong> {{ run.finishedAt | date:'medium' }}</p>
        <p><strong>Estado:</strong> {{ run.state === 1 ? 'Exitosa' : 'Fallida' }} @if (run.errorCode) { <span class="text-danger">({{ run.errorCode }})</span> }</p>
        <p><strong>Artículos recibidos:</strong> {{ run.itemsReceived }} · <strong>Ofertas procesadas:</strong> {{ run.offersProcessed }}</p>
        <p><strong>RAW nuevos/deduplicados:</strong> {{ run.rawNew }}/{{ run.rawDeduplicated }}</p>
      </div>
    } @else {
      <p class="muted">Sin sincronizaciones Éxito registradas.</p>
    }
  }
</section>
`);

// 6. SETTINGS.HTML
write('src/frontend/src/app/pages/settings.html', `<div class="heading">
  <div>
    <p class="eyebrow">PREFERENCIAS LOCALES</p>
    <h1>Personaliza Mercado Inteligente</h1>
    <p class="muted">Configura ubicación, inteligencia artificial y preferencias de comparación.</p>
  </div>
</div>

@if (host.settings(); as s) {
  <div class="settings-grid">
    <section class="panel">
      <h2>Comparación</h2>
      <label>Máximo de tiendas <input type="number" min="1" max="5" [(ngModel)]="s.maxStores"></label>
      <label>Tolerancia de tamaño (%) <input type="number" min="0" max="100" [(ngModel)]="s.sizeTolerancePercent"></label>
      <label>Sobrecompra máxima (%) <input type="number" min="0" max="100" [(ngModel)]="s.maxOverbuyPercent"></label>
      <label class="checkbox"><input type="checkbox" [(ngModel)]="s.allowSubstitutes"> Permitir sustitutos diferenciados</label>
      <label class="checkbox"><input type="checkbox" [(ngModel)]="s.includeMarketplace"> Incluir ofertas de Marketplace (Éxito)</label>
      <label class="checkbox"><input type="checkbox" [(ngModel)]="s.aiEnabled" [disabled]="true"> Habilitar inteligencia artificial</label>
      <button class="primary" (click)="host.save()">Guardar ajustes</button>
    </section>

    <section class="panel">
      <h2>Servicios y datos</h2>
      <p>Base de datos: <strong>{{ host.health()?.database === 'ok' ? 'Activa' : 'Desconocida' }}</strong></p>
      <p>Disponibilidad operativa de Google: <strong>{{ host.health()?.google ?? 'DESCONOCIDO' }}</strong></p>
      <p>Disponibilidad operativa de OpenRouter: <strong>{{ host.health()?.ai ?? 'DESCONOCIDO' }}</strong></p>

      <h3>Copias de seguridad</h3>
      <button type="button" class="secondary" (click)="host.createBackup()" [disabled]="host.backupBusy()">
        {{ host.backupBusy() ? 'Creando copia…' : 'Crear copia de seguridad' }}
      </button>
      @if (host.backupNotice()) { <p class="notice" role="status">{{ host.backupNotice() }}</p> }
      @if (host.backupError()) { <p class="market-error" role="alert">{{ host.backupError() }}</p> }

      @if (host.storageStats(); as st) {
        <div class="storage-stats-box">
          <h4>Almacenamiento local</h4>
          <p>Base de datos: {{ (st.databaseBytes / 1048576) | number:'1.1-2' }} MB</p>
          <p>Archivos RAW: {{ (st.rawBytes / 1048576) | number:'1.1-2' }} MB</p>
          <p>Copias de seguridad: {{ (st.backupsBytes / 1048576) | number:'1.1-2' }} MB</p>
          <p>Total ofertas: {{ st.offersCount }} · Observaciones: {{ st.priceObservationsCount }}</p>
        </div>
      }
    </section>

    <section class="panel" aria-label="Claves de proveedores">
      <h2>Claves de proveedores externos</h2>
      <p class="muted">Las claves se almacenan localmente de forma protegida y nunca se muestran en pantalla.</p>

      @for (sec of host.secrets(); track sec.provider) {
        <div class="secret-row">
          <form (ngSubmit)="host.saveSecret(sec)">
            <div class="secret-label-row">
              <strong>{{ sec.label }}</strong>
              <span class="badge" [class.status-online]="sec.configured === true" [class.status-offline]="sec.configured === false">
                {{ sec.configured === true ? 'Configurada' : sec.configured === false ? 'No configurada' : 'Estado desconocido' }}
              </span>
            </div>
            <div class="secret-input-row">
              <input
                id="secret-{{ sec.provider }}"
                type="password"
                [(ngModel)]="sec.draft"
                name="secret-{{ sec.provider }}"
                placeholder="Ingresa nueva clave…"
                autocomplete="off">
              <button type="submit" class="primary" [disabled]="!sec.draft || sec.saving">Guardar</button>
              <button type="button" class="secondary" (click)="host.deleteSecret(sec)" [disabled]="!sec.configured || sec.deleting">Eliminar</button>
            </div>
            @if (sec.notice) { <p class="notice" role="status">{{ sec.notice }}</p> }
            @if (sec.error) { <p class="market-error" role="alert">{{ sec.error }}</p> }
          </form>
        </div>
      }
    </section>
  </div>
} @else if (host.settingsError()) {
  <div class="market-error" role="alert">
    {{ host.settingsError() }}
    <button type="button" class="secondary" (click)="host.refresh()">Reintentar</button>
  </div>
}
`);

console.log('Finished writing 1-6');
