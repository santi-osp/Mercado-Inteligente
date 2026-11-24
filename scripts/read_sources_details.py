import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/pages/sources.html', 'r', encoding='utf-8') as f:
    text = f.read()

print("Length:", len(text))
print("First 1500 chars:")
print(text[:1500])
print("Middle 1500 chars:")
print(text[1500:3000])
