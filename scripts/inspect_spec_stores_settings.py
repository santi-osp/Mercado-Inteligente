import sys
sys.stdout.reconfigure(encoding='utf-8')
with open(r"C:\Users\santi\.codex\attachments\3da243e1-ee96-43cb-8bce-812f88688a22\pasted-text-1.txt", 'r', encoding='utf-8') as f:
    text = f.read()
import re
for m in re.finditer(r'# \d+\..*(?:STORE|TIENDA|SETTING|AJUSTE)[\s\S]*?(?=\n# \d+\.|\Z)', text, re.IGNORECASE):
    print(m.group(0)[:500])
    print('='*40)
