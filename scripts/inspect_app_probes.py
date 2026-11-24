import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/app.ts', 'r', encoding='utf-8') as f:
    text = f.read()
import re
for m in re.finditer(r'probe\w+|d1\w+|exito\w+', text, re.IGNORECASE):
    print(m.group(0))
