import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/app.css', 'r', encoding='utf-8') as f:
    text = f.read()
print(f'Length of app.css: {len(text)} chars, {len(text.splitlines())} lines')
print('Media queries in app.css:')
import re
for m in re.finditer(r'@media[^{]*\{', text):
    print(m.group(0))
