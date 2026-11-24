import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('scripts/capture-ui-audit.mjs', 'r', encoding='utf-8') as f:
    text = f.read()

import re
routes = ['compare', 'history', 'stores', 'settings', 'analysis', 'dev-ui-kit']
for r in routes:
    idx = text.find(f"// === {r.upper()}")
    if idx == -1:
        idx = text.find(f"=== {r.upper()}")
    if idx == -1:
        idx = text.find(f"'{r}'")
    print(f"=== Route {r} around {idx} ===")
    if idx != -1:
        print(text[idx:idx+1500])
