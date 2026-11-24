import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/app.html', 'r', encoding='utf-8') as f:
    print("=== app.html ===")
    print(f.read())
