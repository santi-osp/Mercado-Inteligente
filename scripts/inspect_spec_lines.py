import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/app.spec.ts', 'r', encoding='utf-8') as f:
    lines = f.readlines()
print("Lines 405-420:")
for i, line in enumerate(lines[404:420], 405):
    print(f'{i}: {line}', end='')
print("\nLines 585-635:")
for i, line in enumerate(lines[584:635], 585):
    print(f'{i}: {line}', end='')
