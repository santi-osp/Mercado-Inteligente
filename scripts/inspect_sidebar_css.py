import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/styles.css', 'r', encoding='utf-8') as f:
    text = f.read()
import re
for m in re.finditer(r'(\.sidebar|\.layout|nav\b|\.mobile-brand)[^{]*\{[^}]*\}', text):
    print(m.group(0))
