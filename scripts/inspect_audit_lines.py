import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('scripts/capture-ui-audit.mjs', 'r', encoding='utf-8') as f:
    lines = f.readlines()
for i, line in enumerate(lines[835:875], 836):
    print(f'{i}: {line}', end='')
