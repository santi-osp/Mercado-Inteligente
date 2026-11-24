import { Component, inject, signal, OnInit } from "@angular/core";
import { DatePipe, DecimalPipe, KeyValuePipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import { RouterLink, ActivatedRoute } from "@angular/router";
import { MARKET_SHELL } from "./feature-host";
import { LocationFacade } from "./facades/location.facade";
import { BasketFacade } from "./facades/basket.facade";
import { ProviderStatusFacade } from "./facades/provider-status.facade";
import { SettingsFacade } from "./facades/settings.facade";
import { CatalogSearchFacade } from "./facades/catalog-search.facade";
import { ShoppingAssistantFacade } from "./facades/shopping-assistant.facade";
import { AnalysisFacade } from "./facades/analysis.facade";
import { CatalogImportFacade } from "./facades/catalog-import.facade";
import { Basket, ProductSearchResultItem } from "./models";

@Component({ imports: [FormsModule], templateUrl: "./pages/home.html" })
export class HomePage {
  readonly host = inject(MARKET_SHELL);
}

@Component({ imports: [FormsModule, DecimalPipe], templateUrl: "./pages/search.html" })
export class SearchPage {
  readonly host = inject(MARKET_SHELL);
  readonly catalogSearch = inject(CatalogSearchFacade);
  readonly basketFacade = inject(BasketFacade);
  readonly assistant = inject(ShoppingAssistantFacade);

  targetProductToAdd: ProductSearchResultItem | null = null;
  selectedBasketIdForAdd = "";
  newBasketNameForAdd = "";
  addingStatus = "";

  readonly mobileFiltersOpen = signal(false);
  readonly expandedBrands = signal(false);
  readonly expandedCategories = signal(false);
  toggleMobileFilters() { this.mobileFiltersOpen.update(v => !v); }
  closeMobileFilters() { this.mobileFiltersOpen.set(false); }

  readonly selectedProductForDetail = signal<ProductSearchResultItem | null>(null);
  openProductDetail(product: ProductSearchResultItem) { this.selectedProductForDetail.set(product); }
  closeProductDetail() { this.selectedProductForDetail.set(null); }

  openAddModal(product: ProductSearchResultItem) {
    this.targetProductToAdd = product;
    const available = this.basketFacade.baskets();
    this.selectedBasketIdForAdd = this.basketFacade.selectedBasketId() || (available[0]?.id ?? "new");
    this.newBasketNameForAdd = "";
    this.addingStatus = "";
  }

  closeAddModal() {
    this.targetProductToAdd = null;
    this.addingStatus = "";
  }

  confirmAddToList() {
    if (!this.targetProductToAdd) return;
    const itemToAdd = {
      name: this.targetProductToAdd.name,
      brand: this.targetProductToAdd.brand,
      quantity: 1
    };

    if (this.selectedBasketIdForAdd === "new" || !this.selectedBasketIdForAdd) {
      const name = this.newBasketNameForAdd.trim() || "Mi lista";
      this.basketFacade.createBasket(name);
      setTimeout(() => {
        const id = this.basketFacade.selectedBasketId();
        if (id) {
          this.basketFacade.addProductFromSearch(id, itemToAdd);
        }
        this.addingStatus = "¡Lista creada y producto agregado!";
        setTimeout(() => this.closeAddModal(), 1200);
      }, 300);
    } else {
      this.basketFacade.addProductFromSearch(this.selectedBasketIdForAdd, itemToAdd);
      this.addingStatus = "¡Agregado a la lista!";
      setTimeout(() => this.closeAddModal(), 1200);
    }
  }

  onPageChange(page: number) {
    this.catalogSearch.setPage(page);
    try {
      const el = document.querySelector(".catalog-toolbar");
      if (el) {
        el.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    } catch {
      // Ignore
    }
  }

  onSearchSubmit() {
    this.catalogSearch.search(this.host.searchQuery, false, false);
    this.host.searchProducts();
  }
}

@Component({ imports: [FormsModule, DecimalPipe], templateUrl: "./pages/market.html" })
export class MarketPage {
  readonly host = inject(MARKET_SHELL);
  readonly locationFacade = inject(LocationFacade);
  readonly basketFacade = inject(BasketFacade);
  readonly assistant = inject(ShoppingAssistantFacade);

  isCreateModalOpen = false;
  newBasketName = "";
  newBasketBudget: number | null = null;

  isRenameModalOpen = false;
  renamingBasketId: string | null = null;
  renameBasketName = "";

  deletingBasket: Basket | null = null;

  openCreateModal() {
    this.newBasketName = "";
    this.newBasketBudget = null;
    this.isCreateModalOpen = true;
  }

  confirmCreate() {
    const name = this.newBasketName.trim();
    if (!name) return;
    this.basketFacade.createBasket(name, this.newBasketBudget ?? undefined);
    this.isCreateModalOpen = false;
  }

  openRenameModal(basket: Basket) {
    this.renamingBasketId = basket.id;
    this.renameBasketName = basket.name;
    this.isRenameModalOpen = true;
  }

  confirmRename() {
    if (!this.renamingBasketId || !this.renameBasketName.trim()) return;
    this.basketFacade.renameBasket(this.renamingBasketId, this.renameBasketName.trim());
    this.isRenameModalOpen = false;
    this.renamingBasketId = null;
  }

  duplicate(id: string) {
    this.basketFacade.duplicateBasket(id);
  }

  openDeleteModal(basket: Basket) {
    this.deletingBasket = basket;
  }

  confirmDelete() {
    if (!this.deletingBasket) return;
    this.basketFacade.deleteBasket(this.deletingBasket.id);
    this.deletingBasket = null;
  }
}

@Component({ imports: [FormsModule, DecimalPipe], templateUrl: "./pages/compare.html" })
export class ComparePage {
  readonly host = inject(MARKET_SHELL);
}

@Component({ imports: [FormsModule, DecimalPipe, DatePipe], templateUrl: "./pages/history.html" })
export class HistoryPage {
  readonly host = inject(MARKET_SHELL);
}

@Component({
  imports: [FormsModule, DecimalPipe, DatePipe],
  templateUrl: "./pages/sources.html"
})
export class SourcesPage {
  readonly host = inject(MARKET_SHELL);
  readonly providerStatus = inject(ProviderStatusFacade);
  readonly catalogImport = inject(CatalogImportFacade);
}

@Component({
  imports: [FormsModule, DecimalPipe, DatePipe, KeyValuePipe],
  templateUrl: "./pages/settings.html"
})
export class SettingsPage implements OnInit {
  readonly host = inject(MARKET_SHELL);
  readonly settingsFacade = inject(SettingsFacade);
  readonly activeTab = signal<'general' | 'ubicacion' | 'ia' | 'datos' | 'copias' | 'avanzado'>('general');

  route = inject(ActivatedRoute, { optional: true });

  setTab(tab: 'general' | 'ubicacion' | 'ia' | 'datos' | 'copias' | 'avanzado') {
    this.activeTab.set(tab);
    if (tab === 'copias') {
      this.host.loadBackups();
    }
  }

  readonly backupLabel = signal('');
  readonly dangerAction = signal<'catalog' | 'history' | 'baskets' | 'reset' | 'cache' | null>(null);
  readonly dangerConfirmText = signal('');
  readonly restoreTarget = signal<string | null>(null);

  ngOnInit() {
    this.route?.queryParamMap?.subscribe(params => {
      const tab = params.get('tab');
      if (tab && ['general', 'ubicacion', 'ia', 'datos', 'copias', 'avanzado'].includes(tab)) {
        this.setTab(tab as any);
      }
    });
  }

  createLabeledBackup() {
    this.settingsFacade.createBackup(this.backupLabel());
    this.backupLabel.set('');
  }

  createLabeledArchive() {
    this.settingsFacade.createArchive(this.backupLabel());
    this.backupLabel.set('');
  }

  openDangerModal(action: 'catalog' | 'history' | 'baskets' | 'reset' | 'cache') {
    this.dangerAction.set(action);
    this.dangerConfirmText.set('');
  }

  closeDangerModal() {
    this.dangerAction.set(null);
    this.dangerConfirmText.set('');
  }

  confirmDangerAction() {
    const action = this.dangerAction();
    const conf = this.dangerConfirmText().trim();
    if (!action) return;

    if (action === 'cache') {
      this.settingsFacade.clearCache(() => this.closeDangerModal());
    } else if (conf === 'BORRAR') {
      if (action === 'catalog') this.settingsFacade.clearCatalog(conf, () => this.closeDangerModal());
      else if (action === 'history') this.settingsFacade.clearHistory(conf, () => this.closeDangerModal());
      else if (action === 'baskets') this.settingsFacade.clearBaskets(conf, () => this.closeDangerModal());
      else if (action === 'reset') this.settingsFacade.resetDatabase(conf, () => this.closeDangerModal());
    }
  }

  startRestore(name: string) {
    this.restoreTarget.set(name);
  }

  cancelRestore() {
    this.restoreTarget.set(null);
  }

  confirmRestore() {
    const target = this.restoreTarget();
    if (target) {
      this.settingsFacade.restoreBackup(target, () => this.restoreTarget.set(null));
    }
  }
}


@Component({ imports: [FormsModule, DecimalPipe], templateUrl: "./pages/analysis.html" })
export class AnalysisPage implements OnInit {
  readonly host = inject(MARKET_SHELL);
  readonly analysis = inject(AnalysisFacade);
  readonly basketFacade = inject(BasketFacade);

  readonly searchQuery = signal('');
  readonly selectedBasketId = signal('');

  ngOnInit() {
    if (!this.analysis.opportunities() && !this.analysis.opportunitiesLoading()) {
      this.analysis.loadOpportunities();
    }
    const baskets = this.basketFacade.baskets();
    if (baskets.length > 0 && !this.selectedBasketId()) {
      this.selectedBasketId.set(this.basketFacade.selectedBasketId() || baskets[0].id);
    }
  }

  setActiveTab(tab: 'opportunities' | 'product' | 'basket') {
    this.analysis.activeTab.set(tab);
  }

  onSearchQueryChange(val: string) {
    this.searchQuery.set(val);
  }

  executeProductSearch() {
    const q = this.searchQuery().trim();
    if (q) {
      this.analysis.searchProductsToAnalyze(q);
    }
  }

  analyzeProduct(productId: string) {
    this.analysis.loadProductAnalysis(productId);
  }

  onBasketSelect(id: string) {
    this.selectedBasketId.set(id);
  }

  analyzeSelectedBasket() {
    const id = this.selectedBasketId();
    if (id) {
      this.analysis.loadBasketAnalysis(id);
    }
  }
}

@Component({
  imports: [FormsModule],
  templateUrl: "./pages/ui-kit.html"
})
export class UiKitPage {
  readonly host = inject(MARKET_SHELL);
}
