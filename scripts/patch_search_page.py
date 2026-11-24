path = 'src/frontend/src/app/feature-pages.ts'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

target = "  addingStatus = \"\";"
replacement = """  addingStatus = "";

  readonly mobileFiltersOpen = signal(false);
  toggleMobileFilters() { this.mobileFiltersOpen.update(v => !v); }
  closeMobileFilters() { this.mobileFiltersOpen.set(false); }

  readonly selectedProductForDetail = signal<ProductSearchResultItem | null>(null);
  openProductDetail(product: ProductSearchResultItem) { this.selectedProductForDetail.set(product); }
  closeProductDetail() { this.selectedProductForDetail.set(null); }"""

if 'mobileFiltersOpen' not in c:
    c = c.replace(target, replacement, 1)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(c)
    print("Patched SearchPage in feature-pages.ts")
else:
    print("SearchPage already patched")
