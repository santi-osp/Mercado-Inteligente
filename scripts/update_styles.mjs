import fs from 'fs';

const content = `/* ==========================================================================
   MERCADO INTELIGENTE — DESIGN SYSTEM & BASE TOKENS (GOAL 17)
   Paleta: Brand #167D5A, Surface #FFFFFF, Canvas #F7F8FA, Text #111827
   ========================================================================== */

:root {
  /* --- Brand & Surfaces --- */
  --primary: #167d5a;
  --primary-hover: #126649;
  --dark: #0f5f44;
  --canvas: #f7f8fa;
  --surface: #ffffff;
  --surface-raised: #ffffff;
  --surface-hover: #f9fafb;
  --surface-active: #f3f4f6;
  --ink: #111827;
  --ink-secondary: #374151;
  --muted: #6b7280;
  --muted-light: #9ca3af;
  --border: #e5e7eb;
  --border-subtle: #f3f4f6;
  --border-focus: #167d5a;
  --soft: #eaf5ef;
  --soft-hover: #ddede4;

  /* --- Semantic Colors --- */
  --success: #16a34a;
  --success-bg: #f0fdf4;
  --success-border: #bbf7d0;
  --info: #2563eb;
  --info-bg: #eff6ff;
  --info-border: #bfdbfe;
  --warning: #d97706;
  --warning-bg: #fffbeb;
  --warning-border: #fde68a;
  --error: #dc2626;
  --error-bg: #fef2f2;
  --error-border: #fecaca;

  /* --- Spacing Scale (4, 8, 12, 16, 24, 32, 48, 64) --- */
  --space-1: 4px;
  --space-2: 8px;
  --space-3: 12px;
  --space-4: 16px;
  --space-6: 24px;
  --space-8: 32px;
  --space-12: 48px;
  --space-16: 64px;

  /* --- Radii Scale --- */
  --radius-xs: 4px;
  --radius-sm: 8px;
  --radius-md: 10px;
  --radius-lg: 12px;
  --radius-xl: 14px;
  --radius-2xl: 16px;
  --radius-full: 9999px;

  /* --- Shadows --- */
  --shadow-xs: 0 1px 2px rgba(0, 0, 0, 0.05);
  --shadow-sm: 0 1px 3px rgba(0, 0, 0, 0.08), 0 1px 2px rgba(0, 0, 0, 0.04);
  --shadow-md: 0 4px 6px -1px rgba(0, 0, 0, 0.07), 0 2px 4px -2px rgba(0, 0, 0, 0.05);
  --shadow-lg: 0 10px 15px -3px rgba(0, 0, 0, 0.08), 0 4px 6px -4px rgba(0, 0, 0, 0.04);
  --shadow-xl: 0 20px 25px -5px rgba(0, 0, 0, 0.1), 0 8px 10px -6px rgba(0, 0, 0, 0.05);

  /* --- Typography Scale --- */
  font-family: 'Inter', system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  color: var(--ink);
  background: var(--canvas);
}

*, *::before, *::after {
  box-sizing: border-box;
}

body {
  margin: 0;
  font-family: inherit;
  color: var(--ink);
  background: var(--canvas);
  -webkit-font-smoothing: antialiased;
  -moz-osx-font-smoothing: grayscale;
}

button, input, select, textarea {
  font: inherit;
  color: inherit;
}

button {
  cursor: pointer;
  border: none;
  background: none;
}

button:focus-visible, input:focus-visible, select:focus-visible, textarea:focus-visible {
  outline: 2px solid var(--primary);
  outline-offset: 2px;
}

h1, h2, h3, h4, h5, h6, p {
  margin-top: 0;
}

h1 {
  font-size: clamp(1.75rem, 2.5vw, 2.25rem);
  font-weight: 700;
  letter-spacing: -0.03em;
  margin-bottom: 8px;
  color: var(--ink);
}

h2 {
  font-size: clamp(1.25rem, 1.8vw, 1.5rem);
  font-weight: 600;
  letter-spacing: -0.02em;
  color: var(--ink);
}

h3 {
  font-size: 1.125rem;
  font-weight: 600;
  letter-spacing: -0.01em;
  color: var(--ink);
}

p {
  line-height: 1.5;
  color: var(--ink-secondary);
}

.muted {
  color: var(--muted);
  font-size: 0.875rem;
}

@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after {
    scroll-behavior: auto !important;
    transition: none !important;
    animation: none !important;
  }
}

/* ==========================================================================
   FORM CONTROLS PRIMITIVES (P0) — 44px min height, 20px checks/radios, switches
   ========================================================================== */

/* Text Inputs, Search, and Selects */
input[type="text"],
input[type="search"],
input[type="number"],
input[type="email"],
input[type="password"],
select,
textarea {
  min-height: 44px;
  padding: 10px 14px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  color: var(--ink);
  font-size: 0.9375rem;
  transition: border-color 0.15s ease, box-shadow 0.15s ease;
  width: 100%;
}

input[type="text"]:hover,
input[type="search"]:hover,
input[type="number"]:hover,
select:hover,
textarea:hover {
  border-color: #cbd5e1;
}

input[type="text"]:focus,
input[type="search"]:focus,
input[type="number"]:focus,
select:focus,
textarea:focus {
  border-color: var(--primary);
  box-shadow: 0 0 0 3px rgba(22, 125, 90, 0.15);
  outline: none;
}

/* Custom Checkbox (20px box, touch target >= 40px) */
label.checkbox {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  min-height: 40px;
  font-size: 0.875rem;
  color: var(--ink-secondary);
  cursor: pointer;
  user-select: none;
  position: relative;
}

label.checkbox input[type="checkbox"] {
  appearance: none;
  -webkit-appearance: none;
  width: 20px;
  height: 20px;
  min-height: 20px;
  border: 1.5px solid #d1d5db;
  border-radius: 5px;
  background-color: var(--surface);
  cursor: pointer;
  margin: 0;
  display: inline-grid;
  place-content: center;
  transition: all 0.15s ease;
}

label.checkbox input[type="checkbox"]:hover {
  border-color: var(--primary);
}

label.checkbox input[type="checkbox"]:checked {
  background-color: var(--primary);
  border-color: var(--primary);
}

label.checkbox input[type="checkbox"]:checked::after {
  content: "";
  width: 6px;
  height: 10px;
  border: solid #ffffff;
  border-width: 0 2px 2px 0;
  transform: rotate(45deg);
  margin-top: -2px;
}

/* Switch Toggle (44x24px pill, 18px knob) */
label.switch {
  display: inline-flex;
  align-items: center;
  gap: 12px;
  min-height: 40px;
  cursor: pointer;
  user-select: none;
}

label.switch input[type="checkbox"] {
  appearance: none;
  -webkit-appearance: none;
  width: 44px;
  height: 24px;
  min-height: 24px;
  border-radius: var(--radius-full);
  background-color: #e5e7eb;
  position: relative;
  cursor: pointer;
  margin: 0;
  transition: background-color 0.2s ease;
}

label.switch input[type="checkbox"]::after {
  content: "";
  position: absolute;
  top: 3px;
  left: 3px;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  background-color: #ffffff;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.2);
  transition: transform 0.2s ease;
}

label.switch input[type="checkbox"]:checked {
  background-color: var(--primary);
}

label.switch input[type="checkbox"]:checked::after {
  transform: translateX(20px);
}

/* Custom Select wrapper */
.select-wrapper {
  position: relative;
  width: 100%;
}

.select-wrapper select {
  appearance: none;
  -webkit-appearance: none;
  padding-right: 36px;
  background-image: url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='16' height='16' viewBox='0 0 24 24' fill='none' stroke='%236B7280' stroke-width='2' stroke-linecap='round' stroke-linejoin='round'%3E%3Cpolyline points='6 9 12 15 18 9'%3E%3C/polyline%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 12px center;
  background-size: 16px;
}

/* ==========================================================================
   BUTTON SYSTEM — Primary, Secondary, Ghost, Danger, Micro
   ========================================================================== */

button.primary {
  min-height: 44px;
  padding: 10px 18px;
  background: var(--primary);
  color: #ffffff;
  border-radius: var(--radius-sm);
  font-weight: 600;
  font-size: 0.9375rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: background-color 0.15s ease, transform 0.1s ease, box-shadow 0.15s ease;
  box-shadow: var(--shadow-xs);
}

button.primary:hover:not(:disabled) {
  background: var(--primary-hover);
  box-shadow: var(--shadow-sm);
}

button.primary:active:not(:disabled) {
  transform: scale(0.99);
}

button.primary:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

button.secondary {
  min-height: 44px;
  padding: 10px 18px;
  background: var(--surface);
  color: var(--ink);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  font-weight: 500;
  font-size: 0.9375rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: all 0.15s ease;
}

button.secondary:hover:not(:disabled) {
  background: var(--surface-hover);
  border-color: #cbd5e1;
}

button.ghost {
  min-height: 38px;
  padding: 8px 14px;
  background: transparent;
  color: var(--ink-secondary);
  border: 1px solid transparent;
  border-radius: var(--radius-sm);
  font-weight: 500;
  font-size: 0.875rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  transition: all 0.15s ease;
}

button.ghost:hover:not(:disabled) {
  background: var(--surface-active);
  color: var(--ink);
}

button.danger {
  min-height: 44px;
  padding: 10px 18px;
  background: #fef2f2;
  color: var(--error);
  border: 1px solid var(--error-border);
  border-radius: var(--radius-sm);
  font-weight: 600;
  font-size: 0.9375rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  transition: all 0.15s ease;
}

button.danger:hover:not(:disabled) {
  background: var(--error);
  color: #ffffff;
}

button.btn-micro {
  min-height: 30px;
  padding: 4px 10px;
  font-size: 0.75rem;
  font-weight: 500;
  border-radius: var(--radius-xs);
  background: var(--surface);
  border: 1px solid var(--border);
  color: var(--ink);
  transition: all 0.12s ease;
}

button.btn-micro:hover {
  background: var(--surface-active);
  border-color: #cbd5e1;
}

/* ==========================================================================
   ALERT BANNERS & NOTICES — Compact & Semantic
   ========================================================================== */

.notice, .alert {
  padding: 12px 16px;
  border-radius: var(--radius-md);
  font-size: 0.875rem;
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: var(--space-4);
  line-height: 1.4;
}

.notice {
  background: var(--soft);
  color: var(--dark);
  border: 1px solid rgba(22, 125, 90, 0.2);
}

.alert-info {
  background: var(--info-bg);
  color: #1e40af;
  border: 1px solid var(--info-border);
}

.alert-success {
  background: var(--success-bg);
  color: #166534;
  border: 1px solid var(--success-border);
}

.alert-warning {
  background: var(--warning-bg);
  color: #92400e;
  border: 1px solid var(--warning-border);
}

.alert-error, .market-error {
  background: var(--error-bg);
  color: #991b1b;
  border: 1px solid var(--error-border);
  border-radius: var(--radius-md);
  padding: 12px 16px;
  font-size: 0.875rem;
}

/* ==========================================================================
   LEGACY SELECTORS PRESERVED FOR TEST & ROUTING INTEGRITY
   ========================================================================== */

.search-form label {
  display: block;
  font-size: 0.875rem;
  font-weight: 600;
  margin-bottom: 8px;
  color: var(--ink);
}

.search-controls {
  display: flex;
  gap: 10px;
}

.search-controls input {
  flex: 1;
  min-width: 0;
}

.product-list, .source-list {
  list-style: none;
  padding: 0;
  margin: 16px 0 0;
}

.product-list li, .source-list li {
  border-top: 1px solid var(--border);
  padding: 14px 0;
}

.product-list li {
  display: flex;
  flex-direction: column;
  gap: 4px;
  overflow-wrap: anywhere;
}

.product-list span, .product-list small {
  color: var(--muted);
}

.source-heading, .source-list li {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}

.source-heading h2 {
  margin: 0;
}

.source-list span {
  font-family: ui-monospace, monospace;
  font-size: 12px;
  overflow-wrap: anywhere;
  text-align: right;
}

.search-form + p {
  margin-top: 16px;
}

.secrets-panel {
  margin-top: 18px;
}

.secret-row {
  border-top: 1px solid var(--border);
  padding: 16px 0;
}

.secret-row h3 {
  font-size: 16px;
  margin: 0;
}

.secret-row .muted {
  margin: 8px 0;
}

.secret-row label {
  margin: 12px 0 8px;
}

.secret-controls {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.secret-controls input {
  flex: 1 1 240px;
  min-width: 0;
}

.secret-row .market-error {
  font-size: 13px;
}

@media (max-width: 639px) {
  .source-heading {
    align-items: flex-start;
    flex-direction: column;
  }
  .search-controls {
    flex-wrap: wrap;
  }
  .search-controls input {
    flex-basis: 100%;
  }
}
`;

fs.writeFileSync('src/frontend/src/styles.css', content, 'utf8');
console.log('src/frontend/src/styles.css updated successfully');
