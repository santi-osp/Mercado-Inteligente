with open("src/frontend/src/app/feature-pages.ts", "r", encoding="utf-8") as f:
    c = f.read()
c = c.replace(
    '@Component({ imports: [FormsModule, RouterLink], templateUrl: "./pages/home.html" })',
    '@Component({ imports: [FormsModule], templateUrl: "./pages/home.html" })'
)
with open("src/frontend/src/app/feature-pages.ts", "w", encoding="utf-8") as f:
    f.write(c)
print("Updated HomePage imports")
