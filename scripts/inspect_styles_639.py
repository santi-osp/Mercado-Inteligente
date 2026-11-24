import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/styles.css', 'r', encoding='utf-8') as f:
    text = f.read()
import re
match = re.search(r'@media \(max-width: 639px\)[\s\S]*', text)
if match:
    print(match.group(0)[:1500])
