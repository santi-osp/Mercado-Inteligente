with open('src/backend/MercadoInteligente.Infrastructure/ProductSearchService.cs', 'r', encoding='utf-8') as f:
    content = f.read()

target = '''    public static async Task<IReadOnlyList<ExternalProduct>> SearchAsync(
        MarketDb db,
        string query,
        int? limit = null,
        int? offset = null,
        CancellationToken ct = default)'''

replacement = '''    public static Task<IReadOnlyList<ExternalProduct>> SearchAsync(
        MarketDb db,
        string query,
        CancellationToken ct) => SearchAsync(db, query, null, null, ct);

    public static async Task<IReadOnlyList<ExternalProduct>> SearchAsync(
        MarketDb db,
        string query,
        int? limit = null,
        int? offset = null,
        CancellationToken ct = default)'''

assert target in content, 'target not found'
content = content.replace(target, replacement, 1)

with open('src/backend/MercadoInteligente.Infrastructure/ProductSearchService.cs', 'w', encoding='utf-8') as f:
    f.write(content)

print('Added SearchAsync overload to ProductSearchService.cs')
