const fs = require('fs');
const path = require('path');

const cssToAppend = `
/* ==========================================================================
   GOAL 15: Shopping Assistant (FAB + Drawer), Ecommerce Catalog, Lists CRUD
   ========================================================================== */

/* --- Shopping Assistant Floating Action Button (FAB) --- */
.shopping-assistant-fab {
  position: fixed;
  bottom: 24px;
  right: 24px;
  z-index: 1000;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  background: linear-gradient(135deg, #10b981 0%, #059669 100%);
  color: #ffffff;
  padding: 12px 20px;
  border-radius: 9999px;
  font-weight: 600;
  font-size: 14px;
  box-shadow: 0 4px 14px rgba(16, 185, 129, 0.4);
  border: none;
  cursor: pointer;
  transition: transform 0.2s cubic-bezier(0.4, 0, 0.2, 1), box-shadow 0.2s;
}

.shopping-assistant-fab:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 20px rgba(16, 185, 129, 0.5);
}

.assistant-fab-icon {
  font-size: 16px;
}

/* --- Backdrop --- */
.assistant-backdrop {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(15, 23, 42, 0.4);
  backdrop-filter: blur(2px);
  z-index: 1040;
  animation: fadeIn 0.2s ease-out;
}

@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

/* --- Shopping Assistant Drawer --- */
.shopping-assistant-drawer {
  position: fixed;
  top: 0;
  right: 0;
  bottom: 0;
  width: 440px;
  max-width: 100vw;
  background: #ffffff;
  box-shadow: -4px 0 24px rgba(0, 0, 0, 0.15);
  z-index: 1050;
  display: flex;
  flex-direction: column;
  animation: slideIn 0.25s cubic-bezier(0.16, 1, 0.3, 1);
}

@keyframes slideIn {
  from { transform: translateX(100%); }
  to { transform: translateX(0); }
}

.assistant-drawer-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border);
  background: #f8fafc;
}

.assistant-header-title {
  display: flex;
  align-items: center;
  gap: 12px;
}

.assistant-avatar {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  background: #d1fae5;
  color: #059669;
  border-radius: 8px;
  font-size: 18px;
}

.assistant-header-title h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 700;
  color: #0f172a;
}

.assistant-header-title p {
  margin: 0;
  font-size: 12px;
}

.assistant-header-actions {
  display: flex;
  gap: 4px;
}

.icon-btn {
  padding: 6px 10px;
  font-size: 16px;
  border-radius: 6px;
  border: none;
  background: transparent;
  cursor: pointer;
  color: var(--muted);
}

.icon-btn:hover {
  background: #e2e8f0;
  color: #0f172a;
}

/* Chips de preguntas sugeridas */
.assistant-chips {
  display: flex;
  gap: 6px;
  overflow-x: auto;
  padding: 10px 16px;
  border-bottom: 1px solid var(--border);
  background: #ffffff;
  scrollbar-width: thin;
}

.chip-btn {
  white-space: nowrap;
  background: #f1f5f9;
  border: 1px solid #e2e8f0;
  padding: 6px 12px;
  border-radius: 9999px;
  font-size: 12px;
  font-weight: 500;
  color: #334155;
  cursor: pointer;
  transition: all 0.15s;
}

.chip-btn:hover {
  background: #e2e8f0;
  border-color: #cbd5e1;
  color: #0f172a;
}

/* Área de mensajes de chat */
.assistant-messages {
  flex: 1;
  overflow-y: auto;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  background: #f8fafc;
}

.assistant-welcome {
  text-align: center;
  padding: 32px 16px;
  background: #ffffff;
  border: 1px solid var(--border);
  border-radius: 12px;
}

.welcome-icon {
  font-size: 36px;
  display: block;
  margin-bottom: 8px;
}

.assistant-welcome h4 {
  margin: 0 0 8px;
  font-size: 16px;
  color: #0f172a;
}

.assistant-welcome p {
  margin: 0 0 6px;
  font-size: 13px;
  color: var(--muted);
}

.chat-bubble-wrapper {
  display: flex;
  width: 100%;
}

.user-wrapper {
  justify-content: flex-end;
}

.bot-wrapper {
  justify-content: flex-start;
}

.chat-bubble {
  max-width: 85%;
  padding: 12px 16px;
  border-radius: 14px;
  font-size: 13px;
  line-height: 1.5;
}

.chat-user {
  background: #10b981;
  color: #ffffff;
  border-bottom-right-radius: 2px;
}

.chat-assistant {
  background: #ffffff;
  color: #1e293b;
  border: 1px solid var(--border);
  border-bottom-left-radius: 2px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.chat-bubble p {
  margin: 0;
}

.loading-bubble {
  font-style: italic;
  color: var(--muted);
}

/* Tarjetas de recomendación dentro del chat */
.assistant-recommendations {
  margin-top: 10px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.rec-heading {
  font-weight: 700;
  font-size: 12px;
  color: #475569;
  margin-bottom: 4px;
}

.rec-card {
  background: #f8fafc;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 10px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.rec-info strong {
  display: block;
  font-size: 13px;
  color: #0f172a;
}

.rec-brand {
  font-size: 11px;
  color: var(--muted);
  margin-right: 6px;
}

.rec-retailer {
  display: inline-block;
  font-size: 10px;
  font-weight: 700;
  background: #e2e8f0;
  padding: 2px 6px;
  border-radius: 4px;
}

.rec-pricing {
  display: flex;
  align-items: baseline;
  gap: 8px;
}

.rec-price {
  font-size: 14px;
  font-weight: 700;
  color: #059669;
}

.rec-unit {
  font-size: 11px;
  color: var(--muted);
}

.rec-actions {
  display: flex;
  gap: 6px;
  margin-top: 4px;
}

.small-btn {
  padding: 4px 10px;
  font-size: 11px;
  border-radius: 4px;
}

/* Formulario de entrada del chat */
.assistant-input-form {
  display: flex;
  gap: 8px;
  padding: 14px 16px;
  border-top: 1px solid var(--border);
  background: #ffffff;
}

.assistant-input-form input {
  flex: 1;
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 10px 14px;
  font-size: 13px;
  outline: none;
}

.assistant-input-form input:focus {
  border-color: #10b981;
  box-shadow: 0 0 0 2px rgba(16, 185, 129, 0.2);
}

.assistant-input-form button {
  padding: 10px 16px;
  border-radius: 8px;
  font-weight: 600;
}

/* --- Modales Generales --- */
.modal-dialog {
  position: fixed;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  width: 90%;
  max-width: 480px;
  background: #ffffff;
  border-radius: 12px;
  z-index: 1050;
  box-shadow: 0 10px 30px rgba(0, 0, 0, 0.2);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  animation: modalScale 0.2s cubic-bezier(0.16, 1, 0.3, 1);
}

@keyframes modalScale {
  from { opacity: 0; transform: translate(-50%, -48%) scale(0.96); }
  to { opacity: 1; transform: translate(-50%, -50%) scale(1); }
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border);
}

.modal-header h3 {
  margin: 0;
  font-size: 16px;
  font-weight: 700;
}

.modal-body {
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.modal-body label {
  font-size: 13px;
  font-weight: 600;
  color: #334155;
}

.modal-body input,
.modal-body select {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--border);
  border-radius: 6px;
  font-size: 14px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  padding: 14px 20px;
  border-top: 1px solid var(--border);
  background: #f8fafc;
}

.text-danger {
  color: #dc2626 !important;
}

/* --- Catálogo Ecommerce Layout --- */
.catalog-layout {
  display: grid;
  grid-template-columns: 240px 1fr;
  gap: 20px;
  margin-top: 16px;
  align-items: start;
}

@media (max-width: 860px) {
  .catalog-layout {
    grid-template-columns: 1fr;
  }
}

.catalog-sidebar {
  position: sticky;
  top: 80px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.sidebar-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.sidebar-header h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 700;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
  border-top: 1px solid var(--border);
  padding-top: 12px;
}

.filter-group h4 {
  margin: 0 0 4px;
  font-size: 12px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--muted);
}

.filter-group .checkbox {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 13px;
  cursor: pointer;
  user-select: none;
}

/* Toolbar de Catálogo */
.catalog-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  margin-bottom: 16px;
}

.toolbar-controls {
  display: flex;
  align-items: center;
  gap: 12px;
}

.view-toggle {
  display: flex;
  border: 1px solid var(--border);
  border-radius: 6px;
  overflow: hidden;
}

.view-btn {
  padding: 6px 10px;
  background: #ffffff;
  border: none;
  cursor: pointer;
  color: var(--muted);
  font-size: 14px;
}

.view-btn.active {
  background: #f1f5f9;
  color: #0f172a;
  font-weight: 700;
}

/* Grid de Productos */
.products-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(210px, 1fr));
  gap: 16px;
}

.products-grid.list-layout {
  grid-template-columns: 1fr;
}

/* Product Card */
.product-card {
  position: relative;
  display: flex;
  flex-direction: column;
  padding: 14px;
  border-radius: 10px;
  background: #ffffff;
  border: 1px solid var(--border);
  transition: transform 0.15s, box-shadow 0.15s;
}

.product-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 6px 18px rgba(0, 0, 0, 0.06);
}

.card-badges {
  position: absolute;
  top: 10px;
  left: 10px;
  right: 10px;
  display: flex;
  justify-content: space-between;
  pointer-events: none;
  z-index: 2;
}

.retailer-badge {
  font-size: 10px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 4px;
  background: #e2e8f0;
  color: #1e293b;
}

.retailer-badge.d1-badge {
  background: #fee2e2;
  color: #991b1b;
}

.retailer-badge.exito-badge {
  background: #fef08a;
  color: #854d0e;
}

.discount-badge {
  font-size: 10px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 4px;
  background: #dc2626;
  color: #ffffff;
}

.stock-badge.out-badge {
  font-size: 10px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 4px;
  background: #f1f5f9;
  color: #64748b;
}

.product-image-container {
  height: 140px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 12px;
  overflow: hidden;
}

.product-img {
  max-height: 100%;
  max-width: 100%;
  object-fit: contain;
}

.image-placeholder {
  color: #cbd5e1;
}

.product-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.product-brand {
  font-size: 11px;
  color: var(--muted);
  font-weight: 600;
  text-transform: uppercase;
}

.product-title {
  margin: 0;
  font-size: 14px;
  font-weight: 600;
  color: #0f172a;
  line-height: 1.3;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.product-presentation {
  font-size: 11px;
  color: var(--muted);
}

.product-pricing {
  margin-top: 6px;
  display: flex;
  align-items: baseline;
  gap: 6px;
}

.current-price {
  font-size: 16px;
  font-weight: 700;
  color: #0f172a;
}

.list-price {
  font-size: 12px;
  color: var(--muted);
  text-decoration: line-through;
}

.unit-price {
  font-size: 11px;
  color: #059669;
  font-weight: 500;
}

.promo-text {
  font-size: 11px;
  color: #dc2626;
  font-weight: 600;
}

.product-actions {
  display: flex;
  flex-direction: column;
  gap: 6px;
  margin-top: 12px;
}

.store-link-btn {
  text-align: center;
  text-decoration: none;
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 600;
  border-radius: 6px;
  display: block;
}

.add-list-btn {
  padding: 6px 12px;
  font-size: 12px;
  border-radius: 6px;
}

/* Vista lista */
.products-grid.list-layout .product-card {
  flex-direction: row;
  align-items: center;
  gap: 16px;
}

.products-grid.list-layout .product-image-container {
  width: 100px;
  height: 90px;
  margin-bottom: 0;
}

.products-grid.list-layout .product-actions {
  flex-direction: row;
  margin-top: 0;
  min-width: 220px;
}

/* Skeleton Loading */
.skeleton-card {
  height: 260px;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
}

.skeleton-img {
  height: 120px;
  background: #e2e8f0;
  border-radius: 6px;
  margin-bottom: 12px;
  animation: pulse 1.5s infinite;
}

.skeleton-line {
  height: 14px;
  background: #e2e8f0;
  border-radius: 4px;
  margin-bottom: 8px;
  animation: pulse 1.5s infinite;
}

.title-line { width: 80%; }
.price-line { width: 50%; }

.skeleton-btn {
  height: 32px;
  background: #e2e8f0;
  border-radius: 6px;
  margin-top: auto;
  animation: pulse 1.5s infinite;
}

@keyframes pulse {
  0% { opacity: 0.6; }
  50% { opacity: 0.3; }
  100% { opacity: 0.6; }
}

/* --- Cards de Listas de Compras --- */
.baskets-cards-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: 14px;
  margin-top: 14px;
}

.basket-card {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: 14px;
  border: 1px solid var(--border);
  border-radius: 8px;
  background: #ffffff;
}

.basket-card.active-basket-card {
  border-color: #10b981;
  background: #f0fdf4;
}

.basket-card-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}

.basket-card-header h3 {
  margin: 0;
  font-size: 15px;
  font-weight: 700;
}

.basket-card-actions {
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}

/* Diagnostic details panel */
.diagnostic-panel {
  margin-top: 32px;
  padding: 14px;
  background: #f8fafc;
  border: 1px dashed var(--border);
  border-radius: 8px;
}

.diagnostic-panel summary {
  cursor: pointer;
  font-size: 13px;
  font-weight: 600;
}
`;

const cssPath = path.resolve(__dirname, '../src/frontend/src/app/app.css');
fs.appendFileSync(cssPath, cssToAppend, 'utf8');
console.log('Appended Goal 15 styles to app.css');
