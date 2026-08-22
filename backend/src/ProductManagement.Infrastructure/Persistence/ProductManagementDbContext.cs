using Microsoft.EntityFrameworkCore;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence;

public class ProductManagementDbContext : DbContext
{
    public ProductManagementDbContext(DbContextOptions<ProductManagementDbContext> options) : base(options) { }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductItem> ProductItems => Set<ProductItem>();
    public DbSet<Variation> Variations => Set<Variation>();
    public DbSet<VariationOption> VariationOptions => Set<VariationOption>();
    public DbSet<ProductConfiguration> ProductConfigurations => Set<ProductConfiguration>();
    public DbSet<Promotion> Promotions => Set<Promotion>();
    public DbSet<PromotionCategory> PromotionCategories => Set<PromotionCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductManagementDbContext).Assembly);
    }
}
