import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/feature-pages.ts', 'r', encoding='utf-8') as f:
    text = f.read()
import re
match = re.search(r'export class SourcesPage[\s\S]*', text)
if match:
    print(match.group(0)[:3000])
