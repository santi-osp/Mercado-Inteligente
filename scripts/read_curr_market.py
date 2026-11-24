import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/pages/market.html', 'r', encoding='utf-8') as f:
    text = f.read()
print(text[:2500])
