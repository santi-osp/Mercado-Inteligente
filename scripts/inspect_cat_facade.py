import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/facades/catalog-import.facade.ts', 'r', encoding='utf-8') as f:
    text = f.read()
print('CatalogImportFacade content:')
print(text[:2000])
