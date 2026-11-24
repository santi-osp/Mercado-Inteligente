import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { CatalogSearchFacade } from './catalog-search.facade';
import { ProductSearchResponse, ProductSearchResultItem } from '../models';

describe('CatalogSearchFacade', () => {
  let facade: CatalogSearchFacade;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        CatalogSearchFacade,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    facade = TestBed.inject(CatalogSearchFacade);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    TestBed.resetTestingModule();
  });

  it('initializes with default pagination and search state', () => {
    expect(facade.page()).toBe(1);
    expect(facade.pageSize()).toBe(24);
    expect(facade.total()).toBe(0);
    expect(facade.totalPages()).toBe(1);
    expect(facade.hasPrevious()).toBe(false);
    expect(facade.hasNext()).toBe(false);
    expect(facade.showingRange()).toBe('0 de 0');
    expect(facade.pageNumbers()).toEqual([1]);
  });

  it('updates reactive pagination computed signals correctly when response arrives', () => {
    const mockItem: ProductSearchResultItem = {
      productId: 'p1',
      externalSkuId: 'sku1',
      name: 'Arroz Diana 1kg',
      brand: 'Diana',
      presentation: '1 kg',
      imageUrl: null,
      retailer: 'Éxito',
      retailerCode: 'EXITO',
      price: 4200,
      listPrice: 4500,
      discountPercentage: 7,
      promotion: null,
      unitPrice: 4200,
      unitLabel: 'kg',
      availability: 'Disponible',
      freshness: 'Fresh',
      priceScope: 'online',
      updatedAt: new Date().toISOString(),
      productUrl: 'https://exito.com/arroz',
      categoryPath: 'Granos',
      isMarketplace: false,
      sellerDisplay: 'Éxito',
      isFallbackContext: false,
      contextWarning: null,
      unitPriceDisplay: '$4.200/kg',
      unitPriceUnit: 'kg',
      pricingMode: 'STANDARD',
      priceBasis: 'kg',
      priceNotes: ''
    };

    const mockResponse: ProductSearchResponse = {
      items: [mockItem],
      total: 58,
      totalCount: 58,
      page: 1,
      pageSize: 24,
      facets: { retailers: [], brands: [], categories: [], minPrice: 1000, maxPrice: 10000 },
      warnings: [],
      totalPages: 3,
      hasPrevious: false,
      hasNext: true,
      query: 'arroz'
    };

    facade.query.set('arroz');
    facade.search('arroz');

    const req = http.expectOne('/api/v1/search/products');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.query).toBe('arroz');
    expect(req.request.body.page).toBe(1);
    expect(req.request.body.pageSize).toBe(24);
    expect(req.request.body.localOnly).toBe(false);

    req.flush(mockResponse);

    expect(facade.total()).toBe(58);
    expect(facade.totalPages()).toBe(3);
    expect(facade.hasPrevious()).toBe(false);
    expect(facade.hasNext()).toBe(true);
    expect(facade.showingRange()).toBe('1-24 de 58');
    expect(facade.pageNumbers()).toEqual([1, 2, 3]);
    expect(facade.results()[0].unitPriceDisplay).toBe('$4.200/kg');
  });

  it('paginates to next page with localOnly true without triggering remote scraper', () => {
    facade.totalPages.set(3);
    facade.total.set(58);
    facade.searchedQuery.set('arroz');

    facade.nextPage();

    expect(facade.page()).toBe(2);
    const req = http.expectOne('/api/v1/search/products');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.page).toBe(2);
    expect(req.request.body.localOnly).toBe(true);

    req.flush({
      items: [],
      total: 58,
      totalCount: 58,
      page: 2,
      pageSize: 24,
      facets: { retailers: [], brands: [], categories: [], minPrice: 1000, maxPrice: 10000 },
      warnings: [],
      totalPages: 3,
      hasPrevious: true,
      hasNext: true
    });

    expect(facade.showingRange()).toBe('25-48 de 58');
    expect(facade.hasPrevious()).toBe(true);
    expect(facade.hasNext()).toBe(true);
  });

  it('resets page to 1 when changing filter or sort', () => {
    facade.page.set(2);
    facade.searchedQuery.set('leche');

    facade.setSort('price_asc');

    expect(facade.page()).toBe(1);
    expect(facade.sort()).toBe('price_asc');

    const req = http.expectOne('/api/v1/search/products');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.page).toBe(1);
    expect(req.request.body.sort).toBe('price_asc');
    expect(req.request.body.localOnly).toBe(true);
    req.flush({
      items: [],
      total: 10,
      totalCount: 10,
      page: 1,
      pageSize: 24,
      facets: { retailers: [], brands: [], categories: [], minPrice: 1000, maxPrice: 5000 },
      warnings: [],
      totalPages: 1,
      hasPrevious: false,
      hasNext: false
    });
  });

  it('toggles retailer filter and resets to page 1 with localOnly true', () => {
    facade.page.set(3);
    facade.searchedQuery.set('arroz');

    facade.toggleRetailer('D1');

    expect(facade.page()).toBe(1);
    expect(facade.selectedRetailers()).toContain('D1');

    const req = http.expectOne('/api/v1/search/products');
    expect(req.request.method).toBe('POST');
    expect(req.request.body.selectedRetailers).toEqual(['D1']);
    expect(req.request.body.filters.retailers).toEqual(['D1']);
    expect(req.request.body.localOnly).toBe(true);
    req.flush({
      items: [],
      total: 5,
      totalCount: 5,
      page: 1,
      pageSize: 24,
      facets: { retailers: [], brands: [], categories: [], minPrice: 1000, maxPrice: 5000 },
      warnings: [],
      totalPages: 1,
      hasPrevious: false,
      hasNext: false
    });
  });
});