import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/pages/sources.html', 'r', encoding='utf-8') as f:
    text = f.read()

idx = text.find('Importar catálogo')
print(text[idx:idx+2500])
