import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/app.css', 'r', encoding='utf-8') as f:
    text = f.read()

import re
matches = [m.start() for m in re.finditer(r'@media', text)]
for m in matches:
    print(text[m:m+250])
    print("="*40)
