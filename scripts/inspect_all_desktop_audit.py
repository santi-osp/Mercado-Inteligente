import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('scripts/capture-ui-audit.mjs', 'r', encoding='utf-8') as f:
    text = f.read()

print(text[56000:65000])
