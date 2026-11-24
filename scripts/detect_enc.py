for enc in ['utf-8-sig', 'latin-1', 'cp1252', 'utf-8']:
    try:
        with open('src/backend/MercadoInteligente.Infrastructure/ProductAnalysisService.cs', 'r', encoding=enc) as f:
            content = f.read()
            print(f'Successfully read with {enc}')
            break
    except Exception as e:
        print(f'{enc} failed: {e}')
