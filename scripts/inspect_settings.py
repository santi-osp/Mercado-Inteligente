import sys
import re
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/pages/settings.html', 'r', encoding='utf-8') as f:
    text = f.read()
print('Headers in settings.html:')
for m in re.finditer(r'<h[23][^>]*>(.*?)</h[23]>', text):
    print('-', m.group(1).strip())
print('\nBackup elements:')
for line in text.splitlines():
    if 'backup' in line.lower():
        print(line[:120])
