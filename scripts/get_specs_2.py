import sys
sys.stdout.reconfigure(encoding='utf-8')
with open(r'C:\Users\santi\.codex\attachments\3da243e1-ee96-43cb-8bce-812f88688a22\pasted-text-1.txt', encoding='utf-8') as f:
    text = f.read()

def get_section(title, length=1500):
    idx = text.find(title)
    if idx != -1:
        print(f"=== {title} ===")
        print(text[idx:idx+length])

get_section("# 88. AJUSTES", 1200)
get_section("# 94. CHAT IA", 1200)
get_section("# 123. UI KIT", 1200)
get_section("# 138. BEFORE / AFTER REPORT", 1000)
