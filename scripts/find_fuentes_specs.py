import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/app.spec.ts', 'r', encoding='utf-8') as f:
    text = f.read()

import re
matches = [m.start() for m in re.finditer(r"navigate\(['\"]Fuentes['\"]\)", text)]
for m in matches:
    print(text[m:m+400])
    print("="*40)
