using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Configurations;

public class VariationConfiguration : IEntityTypeConfiguration<Variation>
{
    public void Configure(EntityTypeBuilder<Variation> builder)
    {
        builder.ToTable("variation");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("id");
        builder.Property(v => v.CategoryId).HasColumnName("category_id");
        builder.Property(v => v.Name).HasColumnName("name").HasMaxLength(100).IsRequired();

        builder.HasIndex(v => new { v.CategoryId, v.Name }).IsUnique();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(v => v.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(v => v.Options)
            .WithOne()
            .HasForeignKey(o => o.VariationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(v => v.Options).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
