import zipfile, sys
sys.stdout.reconfigure(encoding='utf-8')

with zipfile.ZipFile('Mercado_Inteligente_Source_v1.0.0.zip', 'r') as z:
    home_orig = z.read('src/frontend/src/app/pages/home.html').decode('utf-8')
    market_orig = z.read('src/frontend/src/app/pages/market.html').decode('utf-8')

print("=== ORIG HOME ===")
print(home_orig)
print("=== ORIG MARKET ===")
print(market_orig[:3000])
