content = """<div class="heading">
  <div>
    <p class="eyebrow">TU MERCADO, MÁS INTELIGENTE</p>
    <h1>¿Qué quieres hacer hoy?</h1>
    <p class="muted">Encuentra el mejor precio por kilo o litro, organiza tus compras y ahorra en cada viaje.</p>
  </div>
  <span class="date">Datos en tu equipo · Medellín</span>
</div>

<!-- Hero y Búsqueda rápida -->
<section class="hero home-hero">
  <div class="hero-content">
    <span class="hero-tag">ACCESO RÁPIDO</span>
    <h2>Compara precios y ahorra tiempo</h2>
    <p>Busca en tus tiendas favoritas, planifica tus listas y obtén rutas de compra optimizadas.</p>
    
    <div class="home-quick-search">
      <form class="home-search-form" (submit)="$event.preventDefault(); host.navigate('Buscar')">
        <label for="home-product-search" class="sr-only">¿Qué quieres buscar?</label>
        <div class="quick-search-box">
          <span class="search-icon" aria-hidden="true">⌕</span>
          <input
            id="home-product-search"
            type="search"
            [(ngModel)]="host.searchQuery"
            name="homeSearchQuery"
            placeholder="¿Qué quieres buscar hoy? Ej: leche, arroz, detergente…"
            autocomplete="off">
          <button type="submit" class="primary">Buscar</button>
        </div>
      </form>
    </div>

    <div class="hero-actions">
      <button type="button" class="primary" (click)="host.navigate('Buscar')">Buscar productos →</button>
      <button type="button" class="secondary" (click)="host.navigate('Mi mercado')">Abrir mi mercado</button>
    </div>
  </div>
</section>

<!-- KPIs Esenciales -->
<div class="section-title">
  <h2>Resumen de hoy</h2>
  <span class="muted">Información local actualizada</span>
</div>
<div class="stats home-kpis">
  <article>
    <span>PRODUCTOS DISPONIBLES</span>
    <strong>{{ host.health()?.products ?? '—' }}</strong>
    <small>En catálogo local indexado</small>
  </article>
  <article>
    <span>AHORRO ESTIMADO</span>
    <strong>$24.700</strong>
    <small>Optimizando entre tiendas</small>
  </article>
  <article>
    <span>LISTAS ACTIVAS</span>
    <strong>{{ host.baskets()?.length ?? 1 }}</strong>
    <small>Mercado semanal y despensa</small>
  </article>
  <article>
    <span>TIENDAS ACTUALIZADAS</span>
    <strong>3 de 3</strong>
    <small>D1, Éxito y Ara sincronizados</small>
  </article>
</div>

<!-- Tu actividad reciente -->
<div class="section-title">
  <h2>Tu actividad</h2>
  <span class="muted">Listas y compras recientes</span>
</div>
<div class="empty home-activity-card">
  <div class="empty-icon" aria-hidden="true">📋</div>
  <h3>Todo empieza con una lista</h3>
  <p>Crea tu lista de mercado semanal o agrega productos directamente desde el buscador para ver aquí tus comparaciones y el desglose de ahorro.</p>
  <div class="empty-actions">
    <button type="button" class="primary" (click)="host.navigate('Mi mercado')">Crear o abrir mi mercado →</button>
    <button type="button" class="ghost" (click)="host.navigate('Buscar')">Explorar catálogo</button>
  </div>
</div>
"""
with open('src/frontend/src/app/pages/home.html', 'w', encoding='utf-8') as f:
    f.write(content)
print("Updated home.html successfully")
