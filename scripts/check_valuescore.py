import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/pages/analysis.html', 'r', encoding='utf-8') as f:
    text = f.read()

import re
print("Matches for opportunity-card:", len(re.findall(r'opportunity-card', text)))
print("Matches for ValueScore:", len(re.findall(r'ValueScore', text, re.IGNORECASE)))
matches = re.finditer(r'ValueScore', text, re.IGNORECASE)
for m in matches:
    print(text[max(0, m.start()-60):min(len(text), m.end()+60)])
