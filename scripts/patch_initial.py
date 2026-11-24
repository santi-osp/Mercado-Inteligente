with open("src/frontend/src/app/feature-pages.ts", "r", encoding="utf-8") as f:
    c = f.read()
c = c.replace(
    'import { FormsModule } from "@angular/forms";',
    'import { FormsModule } from "@angular/forms";\nimport { RouterLink } from "@angular/router";'
)
c = c.replace(
    '@Component({ templateUrl: "./pages/home.html" })\nexport class HomePage {',
    '@Component({ imports: [FormsModule, RouterLink], templateUrl: "./pages/home.html" })\nexport class HomePage {'
)
with open("src/frontend/src/app/feature-pages.ts", "w", encoding="utf-8") as f:
    f.write(c)

with open("src/frontend/src/app/pages/home.html", "r", encoding="utf-8") as f:
    h = f.read()
h = h.replace(
    "<strong>{{ host.baskets()?.length ?? 1 }}</strong>",
    "<strong>{{ host.baskets().length || 1 }}</strong>"
)
with open("src/frontend/src/app/pages/home.html", "w", encoding="utf-8") as f:
    f.write(h)

with open("src/frontend/src/app/pages/search.html", "r", encoding="utf-8") as f:
    s = f.read()
s = s.replace(
    "@for (item of catalogSearch.results(); track item.id) {",
    "@for (item of catalogSearch.results(); track item.productId) {"
)
s = s.replace(
    '<article class="product-card" [attr.data-id]="item.id">',
    '<article class="product-card" [attr.data-id]="item.productId">'
)
s = s.replace(
    "@if (item.presentation || item.category) {\n                <p class=\"product-presentation\">{{ item.presentation || item.category }}</p>\n              }",
    "@if (item.presentation || item.categoryPath) {\n                <p class=\"product-presentation\">{{ item.presentation || item.categoryPath }}</p>\n              }"
)
s = s.replace(
    "@if (item.formattedUnitPrice || item.unitPrice) {\n                  <span class=\"unit-price-tag\">\n                    {{ item.formattedUnitPrice || ('$' + (item.unitPrice | number:'1.0-0') + ' / ' + (item.unitMeasure || 'ud')) }}\n                  </span>\n                }",
    "@if (item.unitPrice) {\n                  <span class=\"unit-price-tag\">\n                    ${{ item.unitPrice | number:'1.0-0' }} / {{ item.unitLabel || 'ud' }}\n                  </span>\n                }"
)
s = s.replace(
    "@if (det.presentation || det.category) {\n          <p class=\"detail-pres\"><span class=\"muted\">Categoría / Formato:</span> {{ det.presentation || det.category }}</p>\n        }",
    "@if (det.presentation || det.categoryPath) {\n          <p class=\"detail-pres\"><span class=\"muted\">Categoría / Formato:</span> {{ det.presentation || det.categoryPath }}</p>\n        }"
)
s = s.replace(
    "@if (det.formattedUnitPrice || det.unitPrice) {\n            <p class=\"unit-price-line\">\n              <span class=\"muted\">Precio unitario:</span>\n              <strong>{{ det.formattedUnitPrice || ('$' + (det.unitPrice | number:'1.0-0') + ' / ' + (det.unitMeasure || 'ud')) }}</strong>\n            </p>\n          }",
    "@if (det.unitPrice) {\n            <p class=\"unit-price-line\">\n              <span class=\"muted\">Precio unitario:</span>\n              <strong>${{ det.unitPrice | number:'1.0-0' }} / {{ det.unitLabel || 'ud' }}</strong>\n            </p>\n          }"
)
with open("src/frontend/src/app/pages/search.html", "w", encoding="utf-8") as f:
    f.write(s)
print("Updated feature-pages, home.html, and search.html")
