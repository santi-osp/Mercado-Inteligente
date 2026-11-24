import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/app.css', 'r', encoding='utf-8') as f:
    lines = f.readlines()
print('First 60 lines of app.css:')
print(''.join(lines[:60]))
