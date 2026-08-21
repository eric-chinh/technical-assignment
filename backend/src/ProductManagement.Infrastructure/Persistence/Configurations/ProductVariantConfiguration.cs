using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Configurations;

public class ProductVariantConfiguration : IEntityTypeConfiguration<ProductVariant>
{
    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants", t =>
        {
            t.HasCheckConstraint("ck_product_variants_price", "price >= 0");
            t.HasCheckConstraint("ck_product_variants_stock", "stock_quantity >= 0");
            t.HasCheckConstraint(
                "ck_product_variants_compare_at_price",
                "compare_at_price IS NULL OR compare_at_price >= price");
        });
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.ProductId).HasColumnName("product_id");
        builder.Property(v => v.Sku).HasColumnName("sku").HasMaxLength(64).IsRequired();
        builder.Property(v => v.Size).HasColumnName("size").HasMaxLength(20);
        builder.Property(v => v.Color).HasColumnName("color").HasMaxLength(40);
        builder.Property(v => v.Price).HasColumnName("price").HasColumnType("numeric(12,2)");
        builder.Property(v => v.CompareAtPrice).HasColumnName("compare_at_price").HasColumnType("numeric(12,2)");
        builder.Property(v => v.StockQuantity).HasColumnName("stock_quantity").HasDefaultValue(0);
        builder.Property(v => v.Barcode).HasColumnName("barcode").HasMaxLength(64);
        builder.Property(v => v.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(v => v.CreatedAt).HasColumnName("created_at");
        builder.Property(v => v.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(v => v.Sku).IsUnique();
        builder.HasIndex(v => v.ProductId)
            .HasFilter("is_active AND stock_quantity > 0")
            .HasDatabaseName("ix_product_variants_active_in_stock");
    }
}
