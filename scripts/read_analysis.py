import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/pages/analysis.html', 'r', encoding='utf-8') as f:
    text = f.read()
print("Length:", len(text))
print(text[:2000])
