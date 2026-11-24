import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/feature-pages.ts', 'r', encoding='utf-8') as f:
    text = f.read()
import re
for m in re.finditer(r'@Component\(\{[\s\S]*?templateUrl:\s*["\']\./pages/(sources|settings)\.html["\'][\s\S]*?\}\)\s*export class (\w+)', text):
    print(m.group(0))
