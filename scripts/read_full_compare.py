import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/pages/compare.html', 'r', encoding='utf-8') as f:
    text = f.read()

print("Lines:", len(text.splitlines()))
print(text[:3000])
print("... middle ...")
print(text[3000:6000])
print("... end ...")
print(text[6000:])
