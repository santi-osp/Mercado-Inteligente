import sys
sys.stdout.reconfigure(encoding='utf-8')
with open('src/frontend/src/app/pages/analysis.html', 'r', encoding='utf-8') as f:
    text = f.read()

# Replace ValueScore mentions
text = text.replace('algoritmos de ValueScore, descuentos e historial', 'precios por unidad, descuentos e historial')
text = text.replace('(ValueScore alto)', '(Mayor conveniencia)')
text = text.replace('Puntaje de Valor (ValueScore)', 'Valor de compra')
text = text.replace('<th>ValueScore</th>', '<th>Valor de compra</th>')
text = text.replace('ValueScore', 'Valor de compra')
text = text.replace('Value Score', 'Valor de compra')

# Update subtitle to include 1.520 productos analizados · 5 tiendas
old_sub = '<p class="mini-hero-subtitle">\n      Compara precios, tamaños, promociones e historial para descubrir qué opciones ofrecen más por tu dinero.\n    </p>'
new_sub = '<p class="mini-hero-subtitle">\n      1.520 productos analizados · 5 tiendas · Descubre qué opciones ofrecen el mejor valor por tu dinero.\n    </p>'
if old_sub in text:
    text = text.replace(old_sub, new_sub)

with open('src/frontend/src/app/pages/analysis.html', 'w', encoding='utf-8') as f:
    f.write(text)
print("Updated analysis.html successfully")
