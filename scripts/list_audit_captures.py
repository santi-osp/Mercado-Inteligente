import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('scripts/capture-ui-audit.mjs', 'r', encoding='utf-8') as f:
    text = f.read()

import re
captures = re.findall(r"capture\(page,\s*'([^']+)',\s*'([^']+)'", text)
for c in captures:
    print(c[0], "-->", c[1])
