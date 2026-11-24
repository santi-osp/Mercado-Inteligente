export interface SupportedRetailer {
  code: string;
  name: string;
}

export const ALL_SUPPORTED_RETAILERS: SupportedRetailer[] = [
  { code: 'D1', name: 'D1' },
  { code: 'EXITO', name: 'Éxito' },
  { code: 'ARA', name: 'Ara' },
  { code: 'LAVAQUITA', name: 'La Vaquita' },
  { code: 'MUNDOHUEVO', name: 'Mundo Huevo' },
  { code: 'OLIMPICA', name: 'Olímpica' },
  { code: 'JUMBO', name: 'Jumbo' },
  { code: 'PRICESMART', name: 'PriceSmart' }
];

import { Injectable, inject, signal, computed } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import {
  ProductSearchResultItem,
  ProductSearchFacets,
  ProductSearchResponse,
  ProductSearchFilters
} from "../models";

@Injectable({ providedIn: "root" })
export class CatalogSearchFacade {
  private readonly http = inject(HttpClient);

  readonly query = signal("");
  readonly searchedQuery = signal("");
  readonly results = signal<ProductSearchResultItem[]>([]);
  readonly facets = signal<ProductSearchFacets | null>(null);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(24);
  readonly sort = signal("relevance");
  readonly viewMode = signal<"grid" | "list">("grid");
  readonly loading = signal(false);
  readonly error = signal("");
  readonly notice = signal("");
  readonly warnings = signal<string[]>([]);

  // Paginación reactiva
  readonly totalPages = signal(1);
  readonly hasPrevious = signal(false);
  readonly hasNext = signal(false);

  readonly showingRange = computed(() => {
    const total = this.total();
    if (total === 0) return "0 de 0";
    const start = (this.page() - 1) * this.pageSize() + 1;
    const end = Math.min(this.page() * this.pageSize(), total);
    return `${start}-${end} de ${total}`;
  });

  readonly pageNumbers = computed(() => {
    const total = this.totalPages();
    const current = this.page();
    if (total <= 1) return [1];
    const pages: number[] = [];
    const delta = 2;
    for (let i = Math.max(1, current - delta); i <= Math.min(total, current + delta); i++) {
      pages.push(i);
    }
    return pages;
  });

  // Filtros reactivos
  readonly availableRetailers = signal<SupportedRetailer[]>(ALL_SUPPORTED_RETAILERS);
  readonly selectedRetailers = signal<string[]>([]);
  readonly selectedBrands = signal<string[]>([]);
  readonly selectedCategories = signal<string[]>([]);
  readonly minPrice = signal<number | null>(null);
  readonly maxPrice = signal<number | null>(null);
  readonly inStockOnly = signal(false);
  readonly promoOnly = signal(false);

  constructor() {
    try {
      const savedMode = localStorage.getItem("mercado_view_mode");
      if (savedMode === "grid" || savedMode === "list") {
        this.viewMode.set(savedMode);
      }
    } catch {
      // Ignore localStorage unavailable
    }
  }

  setViewMode(mode: "grid" | "list") {
    this.viewMode.set(mode);
    try {
      localStorage.setItem("mercado_view_mode", mode);
    } catch {
      // Ignore
    }
  }

  setSort(newSort: string) {
    this.sort.set(newSort);
    this.page.set(1);
    this.search(undefined, false, true);
  }

  setPage(page: number) {
    if (page < 1 || (this.totalPages() > 0 && page > this.totalPages()) || page === this.page()) {
      return;
    }
    this.page.set(page);
    this.search(undefined, false, true);
  }

  nextPage() {
    this.setPage(this.page() + 1);
  }

  prevPage() {
    this.setPage(this.page() - 1);
  }

  setPageSize(size: number) {
    if (size <= 0 || size === this.pageSize()) return;
    this.pageSize.set(size);
    this.page.set(1);
    this.search(undefined, false, true);
  }

  toggleRetailer(code: string) {
    const upper = code.toUpperCase();
    this.selectedRetailers.update(current =>
      current.includes(upper) ? current.filter(c => c !== upper) : [...current, upper]
    );
    this.page.set(1);
    this.search(undefined, false, true);
  }

  toggleBrand(brand: string) {
    this.selectedBrands.update(current =>
      current.includes(brand) ? current.filter(b => b !== brand) : [...current, brand]
    );
    this.page.set(1);
    this.search(undefined, false, true);
  }

  toggleCategory(category: string) {
    this.selectedCategories.update(current =>
      current.includes(category) ? current.filter(c => c !== category) : [...current, category]
    );
    this.page.set(1);
    this.search(undefined, false, true);
  }

  toggleInStock() {
    this.inStockOnly.update(v => !v);
    this.page.set(1);
    this.search(undefined, false, true);
  }

  togglePromo() {
    this.promoOnly.update(v => !v);
    this.page.set(1);
    this.search(undefined, false, true);
  }

  setPriceRange(min: number | null, max: number | null) {
    this.minPrice.set(min);
    this.maxPrice.set(max);
    this.page.set(1);
    this.search(undefined, false, true);
  }

  clearFilters() {
    this.selectedRetailers.set([]);
    this.selectedBrands.set([]);
    this.selectedCategories.set([]);
    this.minPrice.set(null);
    this.maxPrice.set(null);
    this.inStockOnly.set(false);
    this.promoOnly.set(false);
    this.page.set(1);
    if (this.searchedQuery()) {
      this.search(undefined, false, true);
    }
  }

  search(newQuery?: string, forceRefresh = false, localOnly = false) {
    if (newQuery !== undefined) {
      this.query.set(newQuery);
      this.page.set(1);
    }
    const q = this.query().trim();
    this.loading.set(true);
    this.error.set("");
    this.notice.set("");

    const filters: ProductSearchFilters = {
      retailers: this.selectedRetailers().length > 0 ? this.selectedRetailers() : null,
      brands: this.selectedBrands().length > 0 ? this.selectedBrands() : null,
      categories: this.selectedCategories().length > 0 ? this.selectedCategories() : null,
      minPrice: this.minPrice(),
      maxPrice: this.maxPrice(),
      inStockOnly: this.inStockOnly() || null,
      promoOnly: this.promoOnly() || null
    };

    const requestPayload = {
      query: q || null,
      selectedRetailers: this.selectedRetailers().length > 0 ? this.selectedRetailers() : null,
      forceRefresh,
      page: this.page(),
      pageSize: this.pageSize(),
      sort: this.sort(),
      filters,
      localOnly
    };

    this.http.post<ProductSearchResponse>("/api/v1/search/products", requestPayload).subscribe({
      next: res => {
        this.results.set(res.items ?? []);
        this.facets.set(res.facets ?? null);
        const totalCount = res.totalCount ?? res.total ?? 0;
        this.total.set(totalCount);
        this.page.set(res.page ?? this.page());
        this.pageSize.set(res.pageSize ?? this.pageSize());
        const totalPages = res.totalPages ?? Math.max(1, Math.ceil(totalCount / (res.pageSize ?? this.pageSize())));
        this.totalPages.set(totalPages);
        this.hasPrevious.set(res.hasPrevious ?? (this.page() > 1));
        this.hasNext.set(res.hasNext ?? (this.page() < totalPages));
        this.searchedQuery.set(res.query ?? q);
        this.warnings.set(res.warnings ?? []);
        this.loading.set(false);
      },
      error: () => {
        this.error.set("No se pudieron cargar los productos. Comprueba tu conexión e inténtalo de nuevo.");
        this.loading.set(false);
      }
    });
  }
}
