using MercadoInteligente.Application;
using MercadoInteligente.Domain;
using MercadoInteligente.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace MercadoInteligente.Tests;
public class FoundationTests
{
    [Fact] public void QuantitiesRespectDimensionsAndPacks()
    {
        Assert.Equal(Units.Parse("1 kg")!.Amount, Units.Parse("1000 g")!.Amount);
        Assert.Equal(Units.Parse("1 L")!.Amount, Units.Parse("1000 ml")!.Amount);
        Assert.NotEqual(Units.Parse("500 ml")!.Dimension, Units.Parse("500 g")!.Dimension);
        Assert.Equal(6, Units.Parse("6 x 1 L")!.Amount);
        Assert.Null(Units.UnitPrice(0, Units.Parse("1 kg")));
    }
    [Fact] public void SettingsAndUnknownStockHaveSafeDefaults()
    {
        var settings = new AppSettings(); Assert.Equal(2, settings.MaxStores); Assert.False(new AiSettingsService(new LocalPaths(Path.GetTempPath())).Load().Enabled);
        Assert.Equal(AvailabilityStatus.Unknown, new Offer().Availability);
    }
    [Fact] public async Task MigrationSettingsRawBackupAndIntegrity()
    {
        var root = Path.Combine(Path.GetTempPath(), "market-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new LocalPaths(root);
            var options = new DbContextOptionsBuilder<MarketDb>().UseSqlite($"Data Source={paths.Database};Foreign Keys=True").Options;
            await using (var db = new MarketDb(options))
            {
                await db.Database.MigrateAsync(); db.Settings.Add(new AppSettings { MaxStores = 3 }); await db.SaveChangesAsync();
                var raw = new RawStore(paths, db); var first = await raw.SaveAsync("{\"hello\":1}"u8.ToArray());
                var second = await raw.SaveAsync("{\"hello\":1}"u8.ToArray()); Assert.Equal(first.Id, second.Id);
                var rawPath = Path.Combine(paths.Raw, first.RelativePath);
                Assert.True(File.Exists(rawPath));
                File.Delete(rawPath);
                var recovered = await raw.SaveAsync("{\"hello\":1}"u8.ToArray());
                Assert.Equal(first.Id, recovered.Id);
                Assert.True(File.Exists(rawPath));
                var backup = new BackupService(paths, db); Assert.Equal("ok", await backup.IntegrityAsync());
                Assert.True(File.Exists(Path.Combine(paths.Backups, await backup.CreateAsync())));
            }
            await using var reopened = new MarketDb(options);
            Assert.Equal(3, (await reopened.Settings.SingleAsync()).MaxStores);
            Assert.Single(await reopened.RawCaptures.ToListAsync());
        }
        finally { Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}

public class ContextRulesTests
{
    [Fact] public void UnknownStockIsNotAvailable()
    {
        Assert.Equal(AvailabilityStatus.Unknown, FoundationRules.AvailabilityFromStock(null));
        Assert.NotEqual(AvailabilityStatus.InStock, FoundationRules.AvailabilityFromStock(null));
    }
    [Fact] public void RegionalAndMarketplaceAreNotPhysicalDirectOffers()
    {
        Assert.False(FoundationRules.IsPhysicalPrice(PriceScope.FulfillmentRegion));
        Assert.False(FoundationRules.IsEligibleSeller(SellerKind.Marketplace));
        Assert.True(FoundationRules.IsEligibleSeller(SellerKind.RetailerDirect));
        Assert.False(FoundationRules.Comparable(Units.Parse("500 ml"), Units.Parse("500 g")));
    }
}
