import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/app.html', 'r', encoding='utf-8') as f:
    text = f.read()
print(f'Length of app.html: {len(text)} chars, {len(text.splitlines())} lines')
print("Lines 1-80:")
print('\n'.join(text.splitlines()[:80]))
