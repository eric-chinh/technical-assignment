using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainEntities = ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Configurations;

public class ProductConfigurationEntityConfig : IEntityTypeConfiguration<DomainEntities.ProductConfiguration>
{
    public void Configure(EntityTypeBuilder<DomainEntities.ProductConfiguration> builder)
    {
        builder.ToTable("product_configuration");
        builder.HasKey(pc => new { pc.ProductItemId, pc.VariationOptionId });
        builder.Property(pc => pc.ProductItemId).HasColumnName("product_item_id");
        builder.Property(pc => pc.VariationOptionId).HasColumnName("variation_option_id");

        builder.HasOne<DomainEntities.VariationOption>()
            .WithMany()
            .HasForeignKey(pc => pc.VariationOptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
