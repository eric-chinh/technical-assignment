using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Configurations;

public class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable("promotion", t =>
        {
            t.HasCheckConstraint("ck_promotion_discount_rate", "discount_rate > 0 AND discount_rate <= 1");
            t.HasCheckConstraint("ck_promotion_dates", "end_date >= start_date");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasColumnName("description");
        builder.Property(p => p.DiscountRate).HasColumnName("discount_rate").HasColumnType("numeric(5,4)");
        builder.Property(p => p.StartDate).HasColumnName("start_date");
        builder.Property(p => p.EndDate).HasColumnName("end_date");

        builder.HasMany(p => p.Categories)
            .WithOne()
            .HasForeignKey(pc => pc.PromotionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Categories).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
