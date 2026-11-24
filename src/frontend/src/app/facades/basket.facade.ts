import { Injectable, inject, signal } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { LocationFacade } from "./location.facade";
import {
  Basket,
  BasketItem,
  BasketSyncResult,
  OptimizerSolution,
  AiBasketExplanationResult
} from "../models";

@Injectable({ providedIn: "root" })
export class BasketFacade {
  private readonly http = inject(HttpClient);
  private readonly locationFacade = inject(LocationFacade, { optional: true });

  readonly baskets = signal<Basket[]>([]);
  readonly selectedBasketId = signal<string | null>(null);
  readonly marketLoading = signal(false);
  readonly marketBusy = signal(false);
  readonly deletingItemId = signal<string | null>(null);
  readonly marketError = signal("");
  readonly itemError = signal("");

  query = "";
  quantity = 1;
  notes = "";

  readonly basketSyncBusy = signal(false);
  readonly basketSyncNotice = signal("");
  readonly basketSyncError = signal("");

  readonly basketOptimization = signal<OptimizerSolution | null>(null);
  readonly basketOptimizationBusy = signal(false);
  readonly basketOptimizationError = signal("");

  readonly basketExplanation = signal<AiBasketExplanationResult | null>(null);
  readonly basketExplanationBusy = signal(false);
  readonly basketExplanationError = signal("");

  get selectedBasket(): Basket | undefined {
    return this.baskets().find(b => b.id === this.selectedBasketId());
  }

  loadBaskets() {
    this.marketLoading.set(true);
    this.marketError.set("");
    this.http.get<Basket[]>("/api/v1/baskets").subscribe({
      next: baskets => {
        this.baskets.set(baskets.map(b => ({ ...b, items: b.items ?? [] })));
        if (!this.selectedBasketId() && baskets.length > 0) {
          this.selectedBasketId.set(baskets[0].id);
        }
        this.marketLoading.set(false);
      },
      error: () => {
        this.marketError.set("No se pudieron cargar las listas. Comprueba la conexión e inténtalo de nuevo.");
        this.marketLoading.set(false);
      }
    });
  }

  createBasket(name?: string, budget?: number) {
    if (this.marketBusy() || this.marketLoading()) return;
    this.marketBusy.set(true);
    this.marketError.set("");
    const payload = name?.trim() ? { name: name.trim(), budget: budget && budget > 0 ? budget : null } : {};
    this.http.post<Basket>("/api/v1/baskets", payload).subscribe({
      next: basket => {
        this.baskets.update(current => [...current, { ...basket, items: basket.items ?? [] }]);
        this.selectedBasketId.set(basket.id);
        this.marketBusy.set(false);
      },
      error: () => {
        this.marketError.set("No se pudo crear la lista. Inténtalo de nuevo.");
        this.marketBusy.set(false);
      }
    });
  }

  deleteBasket(id: string) {
    if (this.marketBusy()) return;
    this.marketBusy.set(true);
    this.marketError.set("");
    this.http.delete<void>(`/api/v1/baskets/${id}`).subscribe({
      next: () => {
        this.baskets.update(current => current.filter(b => b.id !== id));
        if (this.selectedBasketId() === id) {
          const remaining = this.baskets();
          this.selectedBasketId.set(remaining.length > 0 ? remaining[0].id : null);
        }
        this.marketBusy.set(false);
      },
      error: () => {
        this.marketError.set("No se pudo eliminar la lista. Inténtalo de nuevo.");
        this.marketBusy.set(false);
      }
    });
  }

  duplicateBasket(id: string) {
    if (this.marketBusy()) return;
    this.marketBusy.set(true);
    this.marketError.set("");
    this.http.post<Basket>(`/api/v1/baskets/${id}/duplicate`, {}).subscribe({
      next: clone => {
        this.baskets.update(current => [...current, { ...clone, items: clone.items ?? [] }]);
        this.selectedBasketId.set(clone.id);
        this.marketBusy.set(false);
      },
      error: () => {
        this.marketError.set("No se pudo duplicar la lista. Inténtalo de nuevo.");
        this.marketBusy.set(false);
      }
    });
  }

  renameBasket(id: string, name: string) {
    const trimmed = name.trim();
    if (!trimmed || this.marketBusy()) return;
    this.marketBusy.set(true);
    this.marketError.set("");
    this.http.put<Basket>(`/api/v1/baskets/${id}`, { name: trimmed }).subscribe({
      next: updated => {
        this.baskets.update(current => current.map(b => b.id === id ? { ...b, name: updated.name } : b));
        this.marketBusy.set(false);
      },
      error: () => {
        this.marketError.set("No se pudo renombrar la lista. Inténtalo de nuevo.");
        this.marketBusy.set(false);
      }
    });
  }

  addProductFromSearch(basketId: string, item: { name: string; brand?: string | null; quantity?: number; notes?: string | null }) {
    if (this.marketBusy()) return;
    this.marketBusy.set(true);
    this.itemError.set("");
    const payload = {
      name: item.name.trim(),
      brand: item.brand?.trim() || null,
      quantity: item.quantity && item.quantity > 0 ? item.quantity : 1,
      notes: item.notes?.trim() || null
    };
    this.http.post<BasketItem>(`/api/v1/baskets/${basketId}/items/from-product`, payload).subscribe({
      next: newItem => {
        this.baskets.update(current => current.map(b => b.id === basketId ? { ...b, items: [...b.items, newItem] } : b));
        this.marketBusy.set(false);
      },
      error: () => {
        this.itemError.set("No se pudo agregar el producto a la lista.");
        this.marketBusy.set(false);
      }
    });
  }

  addItem() {
    const basket = this.selectedBasket;
    if (!basket || this.marketBusy() || this.deletingItemId()) return;
    const query = this.query.trim();
    if (!query || query.length > 500 || !Number.isFinite(this.quantity) || this.quantity <= 0 || this.notes.length > 2000) {
      this.itemError.set("Indica un producto (máximo 500 caracteres), una cantidad positiva y notas de hasta 2000 caracteres.");
      return;
    }
    this.itemError.set("");
    this.marketBusy.set(true);
    this.http.post<BasketItem>(`/api/v1/baskets/${basket.id}/items`, {
      query, quantity: this.quantity, productVariantId: null, notes: this.notes.trim() || null
    }).subscribe({
      next: item => {
        this.baskets.update(current => current.map(entry => entry.id === basket.id ? { ...entry, items: [...entry.items, item] } : entry));
        this.query = ""; this.quantity = 1; this.notes = "";
        this.marketBusy.set(false);
      },
      error: () => {
        this.itemError.set("No se pudo agregar el producto. Comprueba los datos e inténtalo de nuevo.");
        this.marketBusy.set(false);
      }
    });
  }

  deleteItem(item: BasketItem) {
    const basket = this.selectedBasket;
    if (!basket || this.marketBusy() || this.deletingItemId()) return;
    this.itemError.set("");
    this.deletingItemId.set(item.id);
    this.http.delete<void>(`/api/v1/baskets/${basket.id}/items/${item.id}`).subscribe({
      next: () => {
        this.baskets.update(current => current.map(entry => entry.id === basket.id ? { ...entry, items: entry.items.filter(existing => existing.id !== item.id) } : entry));
        this.deletingItemId.set(null);
      },
      error: () => {
        this.itemError.set("No se pudo quitar el producto. Inténtalo de nuevo.");
        this.deletingItemId.set(null);
      }
    });
  }

  syncBasket(locationInput?: string) {
    const basket = this.selectedBasket;
    if (!basket || this.basketSyncBusy()) return;
    this.basketSyncBusy.set(true);
    this.basketSyncError.set("");
    this.basketSyncNotice.set("");
    this.http.post<BasketSyncResult>("/api/v1/sync/basket", null, {
      params: { basketId: basket.id, locationInput: locationInput ?? this.locationFacade?.locationInput() ?? "" }
    }).subscribe({
      next: res => {
        this.basketSyncBusy.set(false);
        const sources = res.shoppingContext?.providers.map(p =>
          `${p.provider}: ${p.status === "RESOLVED" ? "regional" : p.status === "MIXED_SCOPES" ? "mezcla regional/online genérica por fallos parciales" : p.status === "ONLINE_GENERIC_FALLBACK" ? "online genérico (falló contexto regional)" : p.status === "NO_REGIONAL_OFFERS" ? "sin ofertas regionales" : "online genérico"}`).join(" · ") ?? "contexto no resuelto";
        const failures = res.results.filter(result => result.status.toUpperCase() !== "COMPLETED").length;
        this.basketSyncNotice.set(`${res.queriesExecuted} consultas para ${res.totalItems} productos. ${sources}. ${failures ? `${failures} actualización(es) no completadas; se conservan precios previos.` : "Precios físicos de sucursal no confirmados."}`);
      },
      error: () => {
        this.basketSyncBusy.set(false);
        this.basketSyncError.set("No se pudo sincronizar la lista con tiendas externas. Se conservan los datos locales.");
      }
    });
  }

  optimizeBasket(options?: {
    originLatitude?: number | null;
    originLongitude?: number | null;
    locationInput?: string;
    returnToOrigin?: boolean;
    transportMode?: string;
    costPerKm?: number;
  }) {
    const basket = this.selectedBasket;
    if (!basket || this.basketOptimizationBusy()) return;
    this.basketOptimizationBusy.set(true);
    this.basketOptimizationError.set("");
    this.basketExplanation.set(null);

    const payload = {
      originLatitude: options?.originLatitude ?? this.locationFacade?.currentLatitude() ?? null,
      originLongitude: options?.originLongitude ?? this.locationFacade?.currentLongitude() ?? null,
      locationInput: options?.locationInput ?? this.locationFacade?.locationInput() ?? "",
      returnToOrigin: options?.returnToOrigin ?? this.locationFacade?.returnToOrigin() ?? true,
      transportMode: options?.transportMode ?? this.locationFacade?.transportMode() ?? "Driving",
      costPerKm: options?.costPerKm ?? this.locationFacade?.costPerKm() ?? 2500
    };

    this.http.post<OptimizerSolution>(`/api/v1/baskets/${basket.id}/optimize`, payload).subscribe({
      next: solution => {
        this.basketOptimization.set(solution);
        this.basketOptimizationBusy.set(false);
      },
      error: () => {
        this.basketOptimizationError.set("No se pudo optimizar la cesta. Verifica que contenga productos y existan ofertas disponibles.");
        this.basketOptimizationBusy.set(false);
      }
    });
  }

  explainBasketOptimization() {
    const solution = this.basketOptimization();
    const basket = this.selectedBasket;
    if (!solution || !basket || this.basketExplanationBusy()) return;
    this.basketExplanationBusy.set(true);
    this.basketExplanationError.set("");

    const storeNames = solution.stores?.map(s => s.storeName) || [];
    const payload = {
      strategy: typeof solution.strategy === "string" ? solution.strategy : "Balanced",
      totalCost: solution.totalCost,
      productCost: solution.stores?.reduce((acc, s) => acc + s.subtotal, 0) || solution.totalCost,
      travelCost: solution.routePlan?.estimatedTravelCost || 0,
      distanceKm: solution.routePlan?.totalDistanceKm,
      durationMinutes: solution.routePlan?.totalDurationMinutes,
      storeCount: solution.storesUsedCount,
      stores: storeNames,
      singleStoreCost: solution.baselineCost > 0 ? solution.baselineCost : undefined,
      estimatedSavings: solution.totalSavingsVsBaseline > 0 ? solution.totalSavingsVsBaseline : undefined,
      itemCount: basket.items.length,
      warnings: solution.warnings || []
    };

    this.http.post<AiBasketExplanationResult>("/api/v1/ai/explain", payload).subscribe({
      next: exp => {
        this.basketExplanation.set(exp);
        this.basketExplanationBusy.set(false);
      },
      error: () => {
        this.basketExplanationError.set("No se pudo obtener la explicación de IA. Los cálculos matemáticos se conservan intactos.");
        this.basketExplanationBusy.set(false);
      }
    });
  }
}
