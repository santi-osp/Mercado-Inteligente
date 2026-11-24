using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace MercadoInteligente.Infrastructure;
public class MarketDbFactory : IDesignTimeDbContextFactory<MarketDb>
{
    public MarketDb CreateDbContext(string[] args)
    {
        var paths = new LocalPaths(Environment.GetEnvironmentVariable("MARKET_DATA_DIR"));
        return new MarketDb(new DbContextOptionsBuilder<MarketDb>().UseSqlite($"Data Source={paths.Database};Foreign Keys=True;Default Timeout=10").Options);
    }
}
