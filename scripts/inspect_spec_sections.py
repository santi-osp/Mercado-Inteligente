import sys
sys.stdout.reconfigure(encoding='utf-8')
with open(r"C:\Users\santi\.codex\attachments\3da243e1-ee96-43cb-8bce-812f88688a22\pasted-text-1.txt", 'r', encoding='utf-8') as f:
    text = f.read()
import re
match = re.search(r'# 136\. STORES AFTER[\s\S]*?(?=# 137\.)', text)
if match:
    print(match.group(0))
match2 = re.search(r'# 137\. SETTINGS AFTER[\s\S]*?(?=# 138\.)', text)
if match2:
    print(match2.group(0))
