import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/styles.css', 'r', encoding='utf-8') as f:
    text = f.read()
import re
print("Matches for nav / mobile in styles.css:")
for m in re.finditer(r'@media[^{]*\{[\s\S]*?(?=\n@media|\Z)', text):
    header = m.group(0).split('{')[0]
    if 'max-width' in header or '768' in header:
        print('Media query:', header.strip())
