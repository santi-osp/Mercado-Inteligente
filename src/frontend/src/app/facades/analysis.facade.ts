import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import {
  MarketOpportunitiesResultDto,
  ProductAnalysisResultDto,
  BasketAnalysisResultDto,
  ProductSearchResultItem,
  ProductSearchResponse
} from '../models';

@Injectable({ providedIn: 'root' })
export class AnalysisFacade {
  private readonly http = inject(HttpClient);

  readonly activeTab = signal<'opportunities' | 'product' | 'basket'>('opportunities');

  readonly opportunities = signal<MarketOpportunitiesResultDto | null>(null);
  readonly opportunitiesLoading = signal(false);
  readonly opportunitiesError = signal('');

  readonly selectedProductAnalysis = signal<ProductAnalysisResultDto | null>(null);
  readonly productAnalysisLoading = signal(false);
  readonly productAnalysisError = signal('');

  readonly basketAnalysis = signal<BasketAnalysisResultDto | null>(null);
  readonly basketAnalysisLoading = signal(false);
  readonly basketAnalysisError = signal('');
  readonly selectedBasketId = signal<string>('');

  readonly productSearchQuery = signal('');
  readonly productSearchResults = signal<ProductSearchResultItem[]>([]);
  readonly productSearchLoading = signal(false);

  loadOpportunities() {
    this.opportunitiesLoading.set(true);
    this.opportunitiesError.set('');
    this.http.get<MarketOpportunitiesResultDto>('/api/v1/analysis/opportunities').subscribe({
      next: data => {
        this.opportunities.set(data);
        this.opportunitiesLoading.set(false);
      },
      error: () => {
        this.opportunitiesError.set('No se pudieron cargar las oportunidades del mercado.');
        this.opportunitiesLoading.set(false);
      }
    });
  }

  loadProductAnalysis(productId: string) {
    if (!productId) return;
    this.productAnalysisLoading.set(true);
    this.productAnalysisError.set('');
    this.http.get<ProductAnalysisResultDto>(`/api/v1/analysis/product/${encodeURIComponent(productId)}`).subscribe({
      next: data => {
        this.selectedProductAnalysis.set(data);
        this.productAnalysisLoading.set(false);
        this.activeTab.set('product');
      },
      error: () => {
        this.productAnalysisError.set('No se pudo analizar el producto seleccionado.');
        this.productAnalysisLoading.set(false);
      }
    });
  }

  searchProductsToAnalyze(query: string) {
    const q = query.trim();
    if (!q) {
      this.productSearchResults.set([]);
      return;
    }
    this.productSearchLoading.set(true);
    this.http.get<ProductSearchResponse>(`/api/v1/products/search?q=${encodeURIComponent(q)}&pageSize=8`).subscribe({
      next: res => {
        this.productSearchResults.set(res.items || []);
        this.productSearchLoading.set(false);
      },
      error: () => {
        this.productSearchResults.set([]);
        this.productSearchLoading.set(false);
      }
    });
  }

  loadBasketAnalysis(basketId: string) {
    if (!basketId) return;
    this.selectedBasketId.set(basketId);
    this.basketAnalysisLoading.set(true);
    this.basketAnalysisError.set('');
    this.http.get<BasketAnalysisResultDto>(`/api/v1/analysis/basket/${encodeURIComponent(basketId)}`).subscribe({
      next: data => {
        this.basketAnalysis.set(data);
        this.basketAnalysisLoading.set(false);
        this.activeTab.set('basket');
      },
      error: () => {
        this.basketAnalysisError.set('No se pudo analizar la lista seleccionada.');
        this.basketAnalysisLoading.set(false);
      }
    });
  }

  getValueScoreClass(score: number): string {
    if (score >= 80) return 'score-excellent';
    if (score >= 60) return 'score-good';
    if (score >= 40) return 'score-average';
    return 'score-low';
  }

  getValueScoreLabel(score: number): string {
    if (score >= 80) return 'Muy conveniente';
    if (score >= 60) return 'Conveniente';
    if (score >= 40) return 'Normal';
    return 'Poco conveniente';
  }
}
