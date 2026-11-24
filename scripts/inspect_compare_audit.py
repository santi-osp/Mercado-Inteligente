import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('scripts/capture-ui-audit.mjs', 'r', encoding='utf-8') as f:
    text = f.read()

import re
matches = [m.start() for m in re.finditer(r'compare-desktop', text)]
for m in matches[:3]:
    print("Match at:", m)
    print(text[m-200:m+400])
    print("="*40)
