import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/app.spec.ts', 'r', encoding='utf-8') as f:
    text = f.read()

import re
matches = [m.start() for m in re.finditer(r'fuentes|tiendas|provider', text, re.IGNORECASE)]
print("Total matches:", len(matches))
for m in matches[:6]:
    print(text[m-30:m+150])
    print("="*40)
