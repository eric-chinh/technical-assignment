using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(p => p.Slug).HasColumnName("slug").HasColumnType("citext").IsRequired();
        builder.Property(p => p.Description).HasColumnName("description");
        builder.Property(p => p.CategoryId).HasColumnName("category_id");
        builder.Property(p => p.Brand).HasColumnName("brand").HasMaxLength(100);
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<short>();
        builder.Property(p => p.Attributes).HasColumnName("attributes").HasColumnType("jsonb").HasDefaultValueSql("'{}'");
        builder.Property(p => p.ImageUrl).HasColumnName("image_url").HasMaxLength(500);
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        // Postgres system column used as the optimistic-concurrency token (spec section 3.4).
        // Npgsql.EntityFrameworkCore.PostgreSQL 10.x removed the old UseXminAsConcurrencyToken()
        // convenience helper, so xmin is mapped by hand as a shadow property. The Npgsql
        // migrations SQL generator recognizes the name/type/rowVersion combination and omits
        // it from the generated CREATE TABLE DDL, since Postgres maintains this column itself
        // on every table - confirmed by inspecting `dotnet ef migrations script` output and by
        // applying the migration against a real Postgres instance (Step 6).
        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .IsRowVersion()
            .ValueGeneratedOnAddOrUpdate();

        // Weighted full-text search column, computed by Postgres itself (spec section 3.3) -
        // a shadow property since Domain never needs to know this column exists. Typed as
        // NpgsqlTsVector (not string) so Task 6's search query can use the Npgsql EF Core
        // provider's LINQ-translatable full-text operators (.Matches(), EF.Functions.ToTsRank).
        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasColumnName("search_vector")
            .HasColumnType("tsvector")
            .HasComputedColumnSql(
                "setweight(to_tsvector('english', coalesce(name, '')), 'A') || " +
                "setweight(to_tsvector('english', coalesce(brand, '')), 'B') || " +
                "setweight(to_tsvector('english', coalesce(description, '')), 'C')",
                stored: true);

        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.Attributes).HasMethod("gin").HasOperators("jsonb_path_ops");
        builder.HasIndex("SearchVector").HasMethod("gin");
        builder.HasIndex(p => p.Name).HasMethod("gin").HasOperators("gin_trgm_ops");

        builder.HasMany(p => p.Items)
            .WithOne()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(p => p.Items).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
