using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MercadoInteligente.Tests;

public class Rc1SearchTests
{
    [Fact]
    public async Task FtsIndexesExistingAndUpdatedProductsWithAccentInsensitiveRanking()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-fts-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            await using var db = new MarketDb(new DbContextOptionsBuilder<MarketDb>()
                .UseSqlite($"Data Source={paths.Database}").Options);
            await db.Database.MigrateAsync();
            var retailer = new Retailer { Code = "TEST", Name = "Test" };
            db.Retailers.Add(retailer);
            var exact = new ExternalProduct { RetailerId = retailer.Id, ExternalId = "1", Name = "Café molido", Brand = "Marca" };
            var other = new ExternalProduct { RetailerId = retailer.Id, ExternalId = "2", Name = "Té", Brand = "Café" };
            db.ExternalProducts.AddRange(exact, other);
            await db.SaveChangesAsync();
            var results = await ProductSearchService.SearchAsync(db, "cafe");
            Assert.Equal(2, results.Count);
            Assert.Equal(exact.Id, results[0].Id);
            exact.Name = "Chocolate";
            await db.SaveChangesAsync();
            Assert.Equal(exact.Id, Assert.Single(await ProductSearchService.SearchAsync(db, "chocolate")).Id);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
