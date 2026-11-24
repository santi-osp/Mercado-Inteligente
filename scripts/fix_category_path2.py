with open('src/backend/MercadoInteligente.Infrastructure/ProductAnalysisService.cs', 'r', encoding='utf-8') as f:
    content = f.read()

content = content.replace('product.CategoryPathPath', 'product.CategoryPath')

with open('src/backend/MercadoInteligente.Infrastructure/ProductAnalysisService.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print('Fixed CategoryPathPath')
