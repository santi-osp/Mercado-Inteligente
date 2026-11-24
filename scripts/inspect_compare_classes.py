import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/pages/compare.html', 'r', encoding='utf-8') as f:
    text = f.read()

import re
print("Select tags/classes/ids in compare.html:")
for l in text.splitlines():
    if any(k in l for k in ['id=', 'class=', 'tabs-nav', 'override', 'prod1', 'prod2', 'overbuy', 'unit-price', 'ai-adjudication']):
        print(l.strip()[:100])
