import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/models.ts', 'r', encoding='utf-8') as f:
    text = f.read()
import re
match = re.search(r'export interface D1Probe[\s\S]*?(?=export interface|\Z)', text)
if match:
    print(match.group(0)[:500])
