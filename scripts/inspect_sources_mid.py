import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/pages/sources.html', 'r', encoding='utf-8') as f:
    lines = f.readlines()
print(''.join(lines[80:200]))
