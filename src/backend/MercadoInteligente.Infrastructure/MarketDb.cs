using MercadoInteligente.Domain;
using Microsoft.EntityFrameworkCore;
namespace MercadoInteligente.Infrastructure;
public class MarketDb(DbContextOptions<MarketDb> options) : DbContext(options)
{
    public DbSet<Retailer> Retailers => Set<Retailer>(); public DbSet<Store> Stores => Set<Store>();
    public DbSet<ExternalProduct> ExternalProducts => Set<ExternalProduct>(); public DbSet<ExternalSku> ExternalSkus => Set<ExternalSku>();
    public DbSet<Seller> Sellers => Set<Seller>(); public DbSet<Offer> Offers => Set<Offer>(); public DbSet<PriceObservation> PriceObservations => Set<PriceObservation>();
    public DbSet<RawCapture> RawCaptures => Set<RawCapture>(); public DbSet<CanonicalProduct> CanonicalProducts => Set<CanonicalProduct>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>(); public DbSet<ProductMatch> ProductMatches => Set<ProductMatch>(); public DbSet<MatchOverride> MatchOverrides => Set<MatchOverride>();
    public DbSet<Basket> Baskets => Set<Basket>(); public DbSet<BasketItem> BasketItems => Set<BasketItem>();
    public DbSet<LocationProfile> LocationProfiles => Set<LocationProfile>(); public DbSet<ComparisonProfile> ComparisonProfiles => Set<ComparisonProfile>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>(); public DbSet<ProviderHealth> ProviderHealth => Set<ProviderHealth>(); public DbSet<AppSettings> Settings => Set<AppSettings>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Retailer>().HasIndex(x => x.Code).IsUnique();
        model.Entity<Store>().HasIndex(x => new { x.RetailerId, x.ExternalId }).IsUnique();
        model.Entity<ExternalProduct>().HasIndex(x => new { x.RetailerId, x.ExternalId }).IsUnique();
        model.Entity<ExternalSku>().HasIndex(x => new { x.ExternalProductId, x.ExternalId }).IsUnique();
        model.Entity<ExternalSku>().HasIndex(x => x.Ean);
        model.Entity<Seller>().HasIndex(x => new { x.RetailerId, x.ExternalId }).IsUnique();
        model.Entity<Offer>().HasIndex(x => new { x.ExternalSkuId, x.SellerId, x.Context }).IsUnique();
        model.Entity<Offer>().HasIndex(x => new { x.Scope, x.Context });
        model.Entity<Offer>().HasIndex(x => x.CheckedAt);
        model.Entity<LocationProfile>().Ignore(x => x.StreetNumber);
        model.Entity<LocationProfile>().HasIndex(x => x.Label);
        model.Entity<Offer>().ToTable(t => t.HasCheckConstraint("CK_Offer_ValidPrice", "CAST(Price AS NUMERIC) > 0 AND (ListPrice IS NULL OR CAST(ListPrice AS NUMERIC) > 0)"));
        model.Entity<PriceObservation>().ToTable(t => t.HasCheckConstraint("CK_PriceObservation_ValidPrice", "CAST(Price AS NUMERIC) > 0"));
        model.Entity<PriceObservation>().HasIndex(x => new { x.OfferId, x.CheckedAt });
        model.Entity<PriceObservation>().Ignore(x => x.ObservedAt);
        model.Entity<RawCapture>().HasIndex(x => x.Sha256).IsUnique();
        model.Entity<ProductVariant>().HasIndex(x => x.Ean);
        model.Entity<Basket>().HasMany(x => x.Items).WithOne().HasForeignKey(x => x.BasketId).OnDelete(DeleteBehavior.Cascade);
        model.Entity<Store>().HasOne(x => x.Retailer).WithMany().HasForeignKey(x => x.RetailerId);
        model.Entity<ExternalProduct>().HasOne<Retailer>().WithMany().HasForeignKey(x => x.RetailerId);
        model.Entity<ExternalSku>().HasOne<ExternalProduct>().WithMany().HasForeignKey(x => x.ExternalProductId);
        model.Entity<Seller>().HasOne<Retailer>().WithMany().HasForeignKey(x => x.RetailerId);
        model.Entity<Offer>().HasOne<ExternalSku>().WithMany().HasForeignKey(x => x.ExternalSkuId);
        model.Entity<Offer>().HasOne<Seller>().WithMany().HasForeignKey(x => x.SellerId);
        model.Entity<Offer>().HasOne<Store>().WithMany().HasForeignKey(x => x.StoreId);
        model.Entity<Offer>().HasOne<RawCapture>().WithMany().HasForeignKey(x => x.RawCaptureId);
        model.Entity<PriceObservation>().HasOne<Offer>().WithMany().HasForeignKey(x => x.OfferId);
        model.Entity<ProductVariant>().HasOne<CanonicalProduct>().WithMany().HasForeignKey(x => x.CanonicalProductId);
        model.Entity<ProductMatch>().HasOne<ExternalSku>().WithMany().HasForeignKey(x => x.ExternalSkuId);
        model.Entity<ProductMatch>().HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.ProductVariantId);
        model.Entity<MatchOverride>().HasOne<ExternalSku>().WithMany().HasForeignKey(x => x.ExternalSkuId);
        model.Entity<MatchOverride>().HasOne<ProductVariant>().WithMany().HasForeignKey(x => x.ProductVariantId);
    }
}
