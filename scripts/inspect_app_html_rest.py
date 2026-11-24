import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/app.html', 'r', encoding='utf-8') as f:
    text = f.read()
lines = text.splitlines()
print('\n'.join(lines[80:]))
