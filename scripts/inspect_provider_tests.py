import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/app.spec.ts', 'r', encoding='utf-8') as f:
    text = f.read()

import re
matches = [m.start() for m in re.finditer(r"it\('[^']*provider|fuente|ara|tienda", text, re.IGNORECASE)]
for m in matches:
    end = text.find("  it(", m+10)
    if end == -1: end = m + 1500
    print(text[m:end])
    print("="*60)
