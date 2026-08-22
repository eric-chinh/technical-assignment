using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Configurations;

public class ProductItemConfiguration : IEntityTypeConfiguration<ProductItem>
{
    public void Configure(EntityTypeBuilder<ProductItem> builder)
    {
        builder.ToTable("product_items", t =>
        {
            t.HasCheckConstraint("ck_product_items_price", "price >= 0");
            t.HasCheckConstraint("ck_product_items_qty", "qty_in_stock >= 0");
        });
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).HasColumnName("id");
        builder.Property(i => i.ProductId).HasColumnName("product_id");
        builder.Property(i => i.Sku).HasColumnName("sku").HasMaxLength(64).IsRequired();
        builder.Property(i => i.QtyInStock).HasColumnName("qty_in_stock").HasDefaultValue(0);
        builder.Property(i => i.Price).HasColumnName("price").HasColumnType("numeric(12,2)");
        builder.Property(i => i.ProductImage).HasColumnName("product_image").HasMaxLength(500);
        builder.Property(i => i.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(i => i.Version).HasColumnName("version").HasDefaultValue(0).IsConcurrencyToken();
        builder.Property(i => i.CreatedAt).HasColumnName("created_at");
        builder.Property(i => i.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(i => i.Sku).IsUnique();
        builder.HasIndex(i => i.ProductId)
            .HasFilter("is_active = true AND qty_in_stock > 0")
            .HasDatabaseName("ix_product_items_active_in_stock");

        builder.HasMany(i => i.Configurations)
            .WithOne()
            .HasForeignKey(pc => pc.ProductItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.Configurations).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
