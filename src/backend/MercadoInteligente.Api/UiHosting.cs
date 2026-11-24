namespace MercadoInteligente.Api;

public static class UiHosting
{
    public static string? FindBuiltUi(string contentRoot)
    {
        // 1. AppContext.BaseDirectory wwwroot (where the executable/assembly resides)
        var baseWwwroot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "wwwroot"));
        if (File.Exists(Path.Combine(baseWwwroot, "index.html"))) return baseWwwroot;

        // 2. Local wwwroot if published together in content root
        var wwwroot = Path.GetFullPath(Path.Combine(contentRoot, "wwwroot"));
        if (File.Exists(Path.Combine(wwwroot, "index.html"))) return wwwroot;

        // 3. Relative from project directory: src/backend/MercadoInteligente.Api -> src/frontend/...
        var candidate1 = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "frontend", "dist", "mercado-inteligente", "browser"));
        if (File.Exists(Path.Combine(candidate1, "index.html"))) return candidate1;

        // 4. Relative from solution or bin directory: bin/Debug/net10.0 -> ...
        var candidate2 = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "..", "..", "src", "frontend", "dist", "mercado-inteligente", "browser"));
        if (File.Exists(Path.Combine(candidate2, "index.html"))) return candidate2;

        // 5. Alternative without browser subfolder
        var candidate3 = Path.GetFullPath(Path.Combine(contentRoot, "..", "..", "frontend", "dist", "mercado-inteligente"));
        if (File.Exists(Path.Combine(candidate3, "index.html"))) return candidate3;

        return null;
    }
}
