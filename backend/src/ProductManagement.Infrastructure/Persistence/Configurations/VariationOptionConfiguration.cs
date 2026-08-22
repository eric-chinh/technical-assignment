using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Configurations;

public class VariationOptionConfiguration : IEntityTypeConfiguration<VariationOption>
{
    public void Configure(EntityTypeBuilder<VariationOption> builder)
    {
        builder.ToTable("variation_option");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.VariationId).HasColumnName("variation_id");
        builder.Property(o => o.Value).HasColumnName("value").HasMaxLength(100).IsRequired();

        builder.HasIndex(o => new { o.VariationId, o.Value }).IsUnique();
    }
}
