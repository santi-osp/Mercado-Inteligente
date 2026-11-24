import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('scripts/capture-ui-audit.mjs', 'r', encoding='utf-8') as f:
    text = f.read()
import re
for m in re.finditer(r'/ajustes[^\n]*', text):
    start = max(0, m.start() - 50)
    end = min(len(text), m.end() + 250)
    print('---')
    print(text[start:end])
