# ERD API Alignment — Product Catalog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Align the backend database schema, REST API, and frontend with the ERD from `ecommerce-product-db-design-st-engineering.pdf` — adding `product_item` (renamed from `product_variants`), a flexible variation taxonomy (`variation`/`variation_option`/`product_configuration`), and promotions (`promotion`/`promotion_category`).

**Architecture:** The existing Clean Architecture layers remain unchanged. The `ProductVariant` entity is renamed to `ProductItem` with field renames (`stock_quantity→qty_in_stock`) and an explicit `version` column for optimistic concurrency. Hardcoded `size`/`color` fields are replaced by the ERD's flexible `Variation`/`VariationOption`/`ProductConfiguration` junction system. Promotions are new end-to-end.

**Tech Stack:** ASP.NET Core 10 (backend), EF Core 10 + Npgsql (PostgreSQL), FluentValidation, React 19 + RTK Query + TypeScript (frontend)

---

## File Map

### Files to Modify
- `backend/src/ProductManagement.Domain/Entities/ProductVariant.cs` → rename/rewrite as `ProductItem.cs`
- `backend/src/ProductManagement.Domain/Entities/Product.cs` — update navigation from `Variants` → `Items`
- `backend/src/ProductManagement.Infrastructure/Persistence/ProductManagementDbContext.cs` — add new DbSets
- `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductVariantConfiguration.cs` → rewrite as `ProductItemConfiguration.cs`
- `backend/src/ProductManagement.Application/Common/Interfaces/IVariantRepository.cs` → rewrite as `IProductItemRepository.cs`
- `backend/src/ProductManagement.Api/Controllers/VariantsController.cs` — update routes to `/items` and `/product-items`
- `frontend/src/features/products/types.ts` — rename Variant→ProductItem, update fields, add new types
- `frontend/src/features/products/api.ts` — update endpoint URLs, add new hooks

### Files to Create
- `backend/src/ProductManagement.Domain/Entities/ProductItem.cs`
- `backend/src/ProductManagement.Domain/Entities/Variation.cs`
- `backend/src/ProductManagement.Domain/Entities/VariationOption.cs`
- `backend/src/ProductManagement.Domain/Entities/ProductConfiguration.cs`
- `backend/src/ProductManagement.Domain/Entities/Promotion.cs`
- `backend/src/ProductManagement.Domain/Entities/PromotionCategory.cs`
- `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductItemConfiguration.cs`
- `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/VariationConfiguration.cs`
- `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/VariationOptionConfiguration.cs`
- `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductConfigurationEntityConfig.cs`
- `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/PromotionConfiguration.cs`
- `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/PromotionCategoryConfiguration.cs`
- `backend/src/ProductManagement.Infrastructure/Migrations/<timestamp>_ErdAlignment.cs` (EF-generated)
- `backend/src/ProductManagement.Application/Common/Interfaces/IProductItemRepository.cs`
- `backend/src/ProductManagement.Application/Common/Interfaces/IVariationRepository.cs`
- `backend/src/ProductManagement.Application/Common/Interfaces/IPromotionRepository.cs`
- `backend/src/ProductManagement.Application/Variations/ListVariationsForCategoryHandler.cs`
- `backend/src/ProductManagement.Application/Variations/CreateVariationHandler.cs`
- `backend/src/ProductManagement.Application/Variations/CreateVariationOptionHandler.cs`
- `backend/src/ProductManagement.Application/Promotions/ListPromotionsHandler.cs`
- `backend/src/ProductManagement.Application/Promotions/CreatePromotionHandler.cs`
- `backend/src/ProductManagement.Application/Promotions/AttachPromotionCategoryHandler.cs`
- `backend/src/ProductManagement.Api/Controllers/ProductItemsController.cs`
- `backend/src/ProductManagement.Api/Controllers/VariationsController.cs`
- `backend/src/ProductManagement.Api/Controllers/PromotionsController.cs`

---

## Task 1: Rename ProductVariant entity → ProductItem with ERD-aligned fields

**Files:**
- Delete: `backend/src/ProductManagement.Domain/Entities/ProductVariant.cs`
- Create: `backend/src/ProductManagement.Domain/Entities/ProductItem.cs`
- Modify: `backend/src/ProductManagement.Domain/Entities/Product.cs`
- Modify: `backend/src/ProductManagement.Domain/Exceptions/InvalidProductVariantException.cs` (rename to `InvalidProductItemException.cs`)

**What changes:** `ProductVariant` → `ProductItem`; `StockQuantity` → `QtyInStock`; remove hardcoded `Size`/`Color`/`Barcode`/`CompareAtPrice` (replaced by variation system); add `ProductImage` and `Version`.

- [ ] **Step 1: Create the new ProductItem entity**

```csharp
// backend/src/ProductManagement.Domain/Entities/ProductItem.cs
using ProductManagement.Domain.Exceptions;

namespace ProductManagement.Domain.Entities;

public class ProductItem
{
    public long Id { get; private set; }
    public long ProductId { get; private set; }
    public string Sku { get; private set; } = default!;
    public int QtyInStock { get; private set; }
    public decimal Price { get; private set; }
    public string? ProductImage { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ProductConfiguration> _configurations = new();
    public IReadOnlyCollection<ProductConfiguration> Configurations => _configurations.AsReadOnly();

    private ProductItem() { }

    public static ProductItem Create(long productId, string sku, decimal price, int qtyInStock)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new InvalidProductItemException("SKU is required.");
        if (price < 0)
            throw new InvalidProductItemException("Price must be >= 0.");
        if (qtyInStock < 0)
            throw new InvalidProductItemException("Stock must be >= 0.");

        var now = DateTimeOffset.UtcNow;
        return new ProductItem
        {
            ProductId = productId,
            Sku = sku.Trim(),
            Price = price,
            QtyInStock = qtyInStock,
            IsActive = true,
            Version = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateDetails(decimal price, string? productImage)
    {
        if (price < 0)
            throw new InvalidProductItemException("Price must be >= 0.");
        Price = price;
        ProductImage = productImage;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool TryAdjustStock(int delta)
    {
        if (QtyInStock + delta < 0) return false;
        QtyInStock += delta;
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 2: Create InvalidProductItemException**

```csharp
// backend/src/ProductManagement.Domain/Exceptions/InvalidProductItemException.cs
namespace ProductManagement.Domain.Exceptions;

public class InvalidProductItemException(string message) : DomainException(message);
```

- [ ] **Step 3: Update Product entity — replace Variants navigation with Items**

In `backend/src/ProductManagement.Domain/Entities/Product.cs`, replace lines 20-21 and 46:

```csharp
// Replace the Variants field and AddVariant method with:
private readonly List<ProductItem> _items = new();
public IReadOnlyCollection<ProductItem> Items => _items.AsReadOnly();

// replace AddVariant with:
public void AddItem(ProductItem item) => _items.Add(item);
```

The full updated file:

```csharp
// backend/src/ProductManagement.Domain/Entities/Product.cs
using ProductManagement.Domain.Enums;
using ProductManagement.Domain.Exceptions;

namespace ProductManagement.Domain.Entities;

public class Product
{
    public long Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }
    public long CategoryId { get; private set; }
    public string? Brand { get; private set; }
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    public string Attributes { get; private set; } = "{}";
    public string? ImageUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ProductItem> _items = new();
    public IReadOnlyCollection<ProductItem> Items => _items.AsReadOnly();

    private Product() { }

    public static Product Create(string name, string slug, long categoryId, string? brand, string attributes = "{}")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidProductException("Product name is required.");
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidProductException("Product slug is required.");

        var now = DateTimeOffset.UtcNow;
        return new Product
        {
            Name = name.Trim(),
            Slug = slug.Trim(),
            CategoryId = categoryId,
            Brand = brand,
            Attributes = attributes,
            Status = ProductStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void AddItem(ProductItem item) => _items.Add(item);

    public void Activate() { Status = ProductStatus.Active; UpdatedAt = DateTimeOffset.UtcNow; }

    public void Archive()
    {
        if (Status == ProductStatus.Archived)
            throw new InvalidProductException("Product is already archived.");
        Status = ProductStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDetails(string name, string? description, long categoryId, string? brand, string attributes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidProductException("Product name is required.");
        Name = name.Trim();
        Description = description;
        CategoryId = categoryId;
        Brand = brand;
        Attributes = attributes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetImageUrl(string? imageUrl) { ImageUrl = imageUrl; UpdatedAt = DateTimeOffset.UtcNow; }
}
```

- [ ] **Step 4: Build to verify no compile errors (domain layer only)**

```bash
cd C:/AI/st-assignment/backend
dotnet build src/ProductManagement.Domain/ProductManagement.Domain.csproj
```

Expected: Build succeeded, 0 errors (some warnings about removed classes are OK at this stage).

- [ ] **Step 5: Commit domain entity rename**

```bash
git add backend/src/ProductManagement.Domain/
git commit -m "feat(domain): rename ProductVariant→ProductItem; add Version, ProductImage, QtyInStock"
```

---

## Task 2: Add Variation, VariationOption, ProductConfiguration domain entities

**Files:**
- Create: `backend/src/ProductManagement.Domain/Entities/Variation.cs`
- Create: `backend/src/ProductManagement.Domain/Entities/VariationOption.cs`
- Create: `backend/src/ProductManagement.Domain/Entities/ProductConfiguration.cs`

**What this models:** A `Variation` is a type (e.g. "Color") scoped to a `Category`. A `VariationOption` is a value of that type (e.g. "Red"). `ProductConfiguration` is the junction table linking a `ProductItem` to its selected `VariationOption`s.

- [ ] **Step 1: Create Variation entity**

```csharp
// backend/src/ProductManagement.Domain/Entities/Variation.cs
namespace ProductManagement.Domain.Entities;

public class Variation
{
    public long Id { get; private set; }
    public long CategoryId { get; private set; }
    public string Name { get; private set; } = default!;

    private readonly List<VariationOption> _options = new();
    public IReadOnlyCollection<VariationOption> Options => _options.AsReadOnly();

    private Variation() { }

    public static Variation Create(long categoryId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variation name is required.", nameof(name));
        return new Variation { CategoryId = categoryId, Name = name.Trim() };
    }
}
```

- [ ] **Step 2: Create VariationOption entity**

```csharp
// backend/src/ProductManagement.Domain/Entities/VariationOption.cs
namespace ProductManagement.Domain.Entities;

public class VariationOption
{
    public long Id { get; private set; }
    public long VariationId { get; private set; }
    public string Value { get; private set; } = default!;

    private VariationOption() { }

    public static VariationOption Create(long variationId, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Option value is required.", nameof(value));
        return new VariationOption { VariationId = variationId, Value = value.Trim() };
    }
}
```

- [ ] **Step 3: Create ProductConfiguration entity (junction)**

```csharp
// backend/src/ProductManagement.Domain/Entities/ProductConfiguration.cs
namespace ProductManagement.Domain.Entities;

public class ProductConfiguration
{
    public long ProductItemId { get; private set; }
    public long VariationOptionId { get; private set; }

    private ProductConfiguration() { }

    public static ProductConfiguration Create(long productItemId, long variationOptionId)
        => new() { ProductItemId = productItemId, VariationOptionId = variationOptionId };
}
```

- [ ] **Step 4: Build domain layer**

```bash
cd C:/AI/st-assignment/backend
dotnet build src/ProductManagement.Domain/ProductManagement.Domain.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add backend/src/ProductManagement.Domain/Entities/Variation.cs \
        backend/src/ProductManagement.Domain/Entities/VariationOption.cs \
        backend/src/ProductManagement.Domain/Entities/ProductConfiguration.cs
git commit -m "feat(domain): add Variation, VariationOption, ProductConfiguration entities"
```

---

## Task 3: Add Promotion and PromotionCategory domain entities

**Files:**
- Create: `backend/src/ProductManagement.Domain/Entities/Promotion.cs`
- Create: `backend/src/ProductManagement.Domain/Entities/PromotionCategory.cs`

- [ ] **Step 1: Create Promotion entity**

```csharp
// backend/src/ProductManagement.Domain/Entities/Promotion.cs
namespace ProductManagement.Domain.Entities;

public class Promotion
{
    public long Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public decimal DiscountRate { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }

    private readonly List<PromotionCategory> _categories = new();
    public IReadOnlyCollection<PromotionCategory> Categories => _categories.AsReadOnly();

    private Promotion() { }

    public static Promotion Create(string name, string? description, decimal discountRate, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Promotion name is required.", nameof(name));
        if (discountRate <= 0 || discountRate > 1)
            throw new ArgumentException("discount_rate must be in (0, 1].", nameof(discountRate));
        if (endDate < startDate)
            throw new ArgumentException("end_date must be >= start_date.", nameof(endDate));

        return new Promotion
        {
            Name = name.Trim(),
            Description = description,
            DiscountRate = discountRate,
            StartDate = startDate,
            EndDate = endDate
        };
    }
}
```

- [ ] **Step 2: Create PromotionCategory entity (junction)**

```csharp
// backend/src/ProductManagement.Domain/Entities/PromotionCategory.cs
namespace ProductManagement.Domain.Entities;

public class PromotionCategory
{
    public long PromotionId { get; private set; }
    public long CategoryId { get; private set; }

    private PromotionCategory() { }

    public static PromotionCategory Create(long promotionId, long categoryId)
        => new() { PromotionId = promotionId, CategoryId = categoryId };
}
```

- [ ] **Step 3: Build and commit**

```bash
cd C:/AI/st-assignment/backend
dotnet build src/ProductManagement.Domain/ProductManagement.Domain.csproj
git add backend/src/ProductManagement.Domain/Entities/Promotion.cs \
        backend/src/ProductManagement.Domain/Entities/PromotionCategory.cs
git commit -m "feat(domain): add Promotion and PromotionCategory entities"
```

---

## Task 4: Update EF Core configurations for ProductItem and new entities

**Files:**
- Delete: `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductVariantConfiguration.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductItemConfiguration.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/VariationConfiguration.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/VariationOptionConfiguration.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductConfigurationEntityConfig.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/PromotionConfiguration.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/PromotionCategoryConfiguration.cs`
- Modify: `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductConfiguration.cs` (update navigation)

- [ ] **Step 1: Create ProductItemConfiguration (maps to `product_items` table)**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductItemConfiguration.cs
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
    }
}
```

- [ ] **Step 2: Update ProductConfiguration (Product entity) to use Items navigation**

Open `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductConfiguration.cs` and update the HasMany navigation to reference `ProductItem`:

```csharp
// In the existing ProductConfiguration.cs, find the HasMany(p => p.Variants) line and replace with:
builder.HasMany(p => p.Items)
    .WithOne()
    .HasForeignKey(i => i.ProductId)
    .OnDelete(DeleteBehavior.Restrict);
```

- [ ] **Step 3: Create VariationConfiguration**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Configurations/VariationConfiguration.cs
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
    }
}
```

- [ ] **Step 4: Create VariationOptionConfiguration**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Configurations/VariationOptionConfiguration.cs
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
```

- [ ] **Step 5: Create ProductConfigurationEntityConfig (junction)**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductConfigurationEntityConfig.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Configurations;

public class ProductConfigurationEntityConfig : IEntityTypeConfiguration<ProductConfiguration>
{
    public void Configure(EntityTypeBuilder<ProductConfiguration> builder)
    {
        builder.ToTable("product_configuration");
        builder.HasKey(pc => new { pc.ProductItemId, pc.VariationOptionId });
        builder.Property(pc => pc.ProductItemId).HasColumnName("product_item_id");
        builder.Property(pc => pc.VariationOptionId).HasColumnName("variation_option_id");

        builder.HasOne<VariationOption>()
            .WithMany()
            .HasForeignKey(pc => pc.VariationOptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 6: Create PromotionConfiguration**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Configurations/PromotionConfiguration.cs
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
    }
}
```

- [ ] **Step 7: Create PromotionCategoryConfiguration**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Configurations/PromotionCategoryConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Configurations;

public class PromotionCategoryConfiguration : IEntityTypeConfiguration<PromotionCategory>
{
    public void Configure(EntityTypeBuilder<PromotionCategory> builder)
    {
        builder.ToTable("promotion_category");
        builder.HasKey(pc => new { pc.PromotionId, pc.CategoryId });
        builder.Property(pc => pc.PromotionId).HasColumnName("promotion_id");
        builder.Property(pc => pc.CategoryId).HasColumnName("category_id");

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(pc => pc.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 8: Build Infrastructure project**

```bash
cd C:/AI/st-assignment/backend
dotnet build src/ProductManagement.Infrastructure/ProductManagement.Infrastructure.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 9: Commit**

```bash
git add backend/src/ProductManagement.Infrastructure/Persistence/Configurations/
git commit -m "feat(infra): add EF configurations for ProductItem, Variation, ProductConfiguration, Promotion"
```

---

## Task 5: Update DbContext and generate EF migration

**Files:**
- Modify: `backend/src/ProductManagement.Infrastructure/Persistence/ProductManagementDbContext.cs`

- [ ] **Step 1: Update DbContext to add all new DbSets**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/ProductManagementDbContext.cs
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
```

- [ ] **Step 2: Build the Infrastructure project**

```bash
cd C:/AI/st-assignment/backend
dotnet build src/ProductManagement.Infrastructure/ProductManagement.Infrastructure.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Generate EF Core migration**

```bash
cd C:/AI/st-assignment/backend
dotnet ef migrations add ErdAlignment \
  --project src/ProductManagement.Infrastructure \
  --startup-project src/ProductManagement.Api
```

Expected: `Done. To undo this action, use 'ef migrations remove'`

The generated migration should:
- Rename table `product_variants` → `product_items`
- Rename column `stock_quantity` → `qty_in_stock`
- Drop columns `size`, `color`, `compare_at_price`, `barcode`
- Add columns `product_image`, `version`
- Create tables `variation`, `variation_option`, `product_configuration`, `promotion`, `promotion_category`

- [ ] **Step 4: Review the generated migration file**

Open the generated migration at `backend/src/ProductManagement.Infrastructure/Migrations/<timestamp>_ErdAlignment.cs` and verify it contains:
- `RenameTable(name: "product_variants", newName: "product_items")`
- `RenameColumn(name: "stock_quantity", table: "product_items", newName: "qty_in_stock")`
- `DropColumn(name: "size", table: "product_items")` and `DropColumn(name: "color", ...)` and `DropColumn(name: "compare_at_price", ...)` and `DropColumn(name: "barcode", ...)`
- `AddColumn` for `product_image` and `version`
- `CreateTable` calls for the four new tables

If EF generates `DropTable`+`CreateTable` instead of `RenameTable`, manually edit the migration to use `RenameTable` and `RenameColumn` to avoid data loss.

- [ ] **Step 5: Apply migration to dev database**

```bash
cd C:/AI/st-assignment/backend
dotnet ef database update \
  --project src/ProductManagement.Infrastructure \
  --startup-project src/ProductManagement.Api
```

Expected: `Done.`

- [ ] **Step 6: Commit**

```bash
git add backend/src/ProductManagement.Infrastructure/Persistence/ProductManagementDbContext.cs \
        backend/src/ProductManagement.Infrastructure/Migrations/
git commit -m "feat(infra): ErdAlignment migration — product_items, variation taxonomy, promotions"
```

---

## Task 6: Update repository interfaces and implementations

**Files:**
- Delete: `backend/src/ProductManagement.Application/Common/Interfaces/IVariantRepository.cs`
- Create: `backend/src/ProductManagement.Application/Common/Interfaces/IProductItemRepository.cs`
- Create: `backend/src/ProductManagement.Application/Common/Interfaces/IVariationRepository.cs`
- Create: `backend/src/ProductManagement.Application/Common/Interfaces/IPromotionRepository.cs`
- Modify: corresponding Infrastructure implementations

- [ ] **Step 1: Create IProductItemRepository**

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/IProductItemRepository.cs
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IProductItemRepository
{
    Task<ProductItem?> GetByIdAsync(long id, CancellationToken ct);
    Task<bool> SkuExistsAsync(string sku, CancellationToken ct);
    Task<List<ProductItem>> ListByProductIdAsync(long productId, CancellationToken ct);
    void Add(ProductItem item);
    void Remove(ProductItem item);
}
```

- [ ] **Step 2: Create IVariationRepository**

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/IVariationRepository.cs
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IVariationRepository
{
    Task<Variation?> GetByIdAsync(long id, CancellationToken ct);
    Task<List<Variation>> ListByCategoryIdAsync(long categoryId, CancellationToken ct);
    Task<bool> OptionExistsAsync(long variationId, string value, CancellationToken ct);
    void Add(Variation variation);
    void AddOption(VariationOption option);
}
```

- [ ] **Step 3: Create IPromotionRepository**

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/IPromotionRepository.cs
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IPromotionRepository
{
    Task<Promotion?> GetByIdAsync(long id, CancellationToken ct);
    Task<List<Promotion>> ListAsync(CancellationToken ct);
    Task<bool> CategoryAlreadyAttachedAsync(long promotionId, long categoryId, CancellationToken ct);
    void Add(Promotion promotion);
    void AddCategory(PromotionCategory promotionCategory);
}
```

- [ ] **Step 4: Update the Infrastructure implementation for ProductItemRepository**

Find the existing `VariantRepository.cs` in `backend/src/ProductManagement.Infrastructure/Persistence/Repositories/` and create a new file `ProductItemRepository.cs`:

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Repositories/ProductItemRepository.cs
using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class ProductItemRepository(ProductManagementDbContext db) : IProductItemRepository
{
    public Task<ProductItem?> GetByIdAsync(long id, CancellationToken ct)
        => db.ProductItems.Include(i => i.Configurations).FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken ct)
        => db.ProductItems.AnyAsync(i => i.Sku == sku, ct);

    public Task<List<ProductItem>> ListByProductIdAsync(long productId, CancellationToken ct)
        => db.ProductItems.Include(i => i.Configurations)
            .Where(i => i.ProductId == productId && i.IsActive)
            .ToListAsync(ct);

    public void Add(ProductItem item) => db.ProductItems.Add(item);

    public void Remove(ProductItem item) => db.ProductItems.Remove(item);
}
```

- [ ] **Step 5: Create VariationRepository**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Repositories/VariationRepository.cs
using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class VariationRepository(ProductManagementDbContext db) : IVariationRepository
{
    public Task<Variation?> GetByIdAsync(long id, CancellationToken ct)
        => db.Variations.Include(v => v.Options).FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<List<Variation>> ListByCategoryIdAsync(long categoryId, CancellationToken ct)
        => db.Variations.Include(v => v.Options)
            .Where(v => v.CategoryId == categoryId)
            .ToListAsync(ct);

    public Task<bool> OptionExistsAsync(long variationId, string value, CancellationToken ct)
        => db.VariationOptions.AnyAsync(o => o.VariationId == variationId && o.Value == value, ct);

    public void Add(Variation variation) => db.Variations.Add(variation);

    public void AddOption(VariationOption option) => db.VariationOptions.Add(option);
}
```

- [ ] **Step 6: Create PromotionRepository**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Repositories/PromotionRepository.cs
using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class PromotionRepository(ProductManagementDbContext db) : IPromotionRepository
{
    public Task<Promotion?> GetByIdAsync(long id, CancellationToken ct)
        => db.Promotions.Include(p => p.Categories).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<Promotion>> ListAsync(CancellationToken ct)
        => db.Promotions.Include(p => p.Categories).ToListAsync(ct);

    public Task<bool> CategoryAlreadyAttachedAsync(long promotionId, long categoryId, CancellationToken ct)
        => db.PromotionCategories.AnyAsync(pc => pc.PromotionId == promotionId && pc.CategoryId == categoryId, ct);

    public void Add(Promotion promotion) => db.Promotions.Add(promotion);

    public void AddCategory(PromotionCategory pc) => db.PromotionCategories.Add(pc);
}
```

- [ ] **Step 7: Register new repositories in DI (find the Infrastructure DI registration file)**

In `backend/src/ProductManagement.Infrastructure/DependencyInjection.cs` (or wherever services are registered), add:

```csharp
services.AddScoped<IProductItemRepository, ProductItemRepository>();
services.AddScoped<IVariationRepository, VariationRepository>();
services.AddScoped<IPromotionRepository, PromotionRepository>();
// Remove or update the old IVariantRepository registration
```

- [ ] **Step 8: Build solution**

```bash
cd C:/AI/st-assignment/backend
dotnet build ProductManagement.sln
```

Expected: Build succeeded, 0 errors. (Any remaining errors will be in Application handlers that still reference IVariantRepository — fix them in the next task.)

- [ ] **Step 9: Commit**

```bash
git add backend/src/ProductManagement.Application/Common/Interfaces/ \
        backend/src/ProductManagement.Infrastructure/Persistence/Repositories/
git commit -m "feat(infra): add ProductItemRepository, VariationRepository, PromotionRepository"
```

---

## Task 7: Update Application handlers — product items (renamed from variants)

**Files:**
- Modify: `backend/src/ProductManagement.Application/Variants/CreateVariantHandler.cs` → update to use `IProductItemRepository` and `ProductItem`
- Modify: `backend/src/ProductManagement.Application/Variants/ListVariantsHandler.cs` → update
- Modify: `backend/src/ProductManagement.Application/Variants/UpdateVariantHandler.cs` → update DTO fields
- Modify: `backend/src/ProductManagement.Application/Variants/DeleteVariantHandler.cs` → update
- Modify: `backend/src/ProductManagement.Application/Variants/AdjustStockHandler.cs` → update

**Note:** Keep the directory named `Variants` for now to minimize diff size. The API routes (not handler names) are what the spec cares about.

- [ ] **Step 1: Read the current CreateVariantHandler**

```bash
cat backend/src/ProductManagement.Application/Variants/CreateVariantHandler.cs
```

- [ ] **Step 2: Update CreateVariantHandler DTOs and logic**

Replace `CreateVariantRequest` and handler to use `ProductItem.Create` with new fields. The request body now accepts `variationOptionIds` instead of hardcoded `size`/`color`:

```csharp
// backend/src/ProductManagement.Application/Variants/CreateVariantHandler.cs
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public record CreateVariantRequest(
    string Sku,
    decimal Price,
    int QtyInStock,
    string? ProductImage,
    long[] VariationOptionIds);

public record VariantDto(
    long Id,
    string Sku,
    decimal Price,
    int QtyInStock,
    string? ProductImage,
    bool IsActive,
    int Version,
    long[] VariationOptionIds);

public class CreateVariantHandler(IProductItemRepository repo, IUnitOfWork uow)
{
    public async Task<VariantDto> HandleAsync(long productId, CreateVariantRequest request, CancellationToken ct)
    {
        if (await repo.SkuExistsAsync(request.Sku, ct))
            throw new ConflictException($"SKU '{request.Sku}' already exists.");

        var item = ProductItem.Create(productId, request.Sku, request.Price, request.QtyInStock);

        foreach (var optionId in request.VariationOptionIds)
            item.Configurations.ToList(); // EF will handle config via Add below

        repo.Add(item);

        // Add configurations in same transaction
        foreach (var optionId in request.VariationOptionIds)
        {
            var config = ProductConfiguration.Create(item.Id, optionId);
            // Add via DbContext directly since ProductItem.Id isn't set until SaveChanges
            // Use the approach of adding via the navigation after save, or pre-save pattern
        }

        await uow.SaveChangesAsync(ct);

        // Reload to get configurations
        var saved = await repo.GetByIdAsync(item.Id, ct)
            ?? throw new InvalidOperationException("Failed to reload product item.");

        return ToDto(saved);
    }

    internal static VariantDto ToDto(ProductItem i) => new(
        i.Id, i.Sku, i.Price, i.QtyInStock, i.ProductImage, i.IsActive, i.Version,
        i.Configurations.Select(c => c.VariationOptionId).ToArray());
}
```

**Important note:** The `ProductItem.Create` factory sets `Id = 0` until EF saves to the database. To attach `ProductConfiguration` rows in the same transaction, the handler must save the item first, then add configurations. Check the existing handler pattern and replicate it (some projects use `Add` then save, then use the returned id).

The actual implementation should follow this pattern:

```csharp
public async Task<VariantDto> HandleAsync(long productId, CreateVariantRequest request, CancellationToken ct)
{
    if (await repo.SkuExistsAsync(request.Sku, ct))
        throw new ConflictException($"SKU '{request.Sku}' already exists.");

    var item = ProductItem.Create(productId, request.Sku, request.Price, request.QtyInStock);
    if (request.ProductImage is not null)
        item.UpdateDetails(request.Price, request.ProductImage);

    repo.Add(item);
    await uow.SaveChangesAsync(ct); // item.Id is now populated by DB

    foreach (var optionId in request.VariationOptionIds)
        configRepo.Add(ProductConfiguration.Create(item.Id, optionId));

    await uow.SaveChangesAsync(ct);

    return ToDto(await repo.GetByIdAsync(item.Id, ct)!);
}
```

Add `IProductConfigurationRepository` or expose `DbContext` access as needed — follow the pattern used by existing handlers.

- [ ] **Step 3: Update ListVariantsHandler DTO**

```csharp
// backend/src/ProductManagement.Application/Variants/ListVariantsHandler.cs
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public class ListVariantsHandler(IProductItemRepository repo)
{
    public async Task<List<VariantDto>> HandleAsync(long productId, CancellationToken ct)
    {
        var items = await repo.ListByProductIdAsync(productId, ct);
        return items.Select(CreateVariantHandler.ToDto).ToList();
    }
}
```

- [ ] **Step 4: Update UpdateVariantHandler to use new fields**

```csharp
// backend/src/ProductManagement.Application/Variants/UpdateVariantHandler.cs
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Variants;

public record UpdateVariantRequest(decimal Price, string? ProductImage, int ExpectedVersion);

public class UpdateVariantHandler(IProductItemRepository repo, IUnitOfWork uow)
{
    public async Task<VariantDto> HandleAsync(long itemId, UpdateVariantRequest request, CancellationToken ct)
    {
        var item = await repo.GetByIdAsync(itemId, ct)
            ?? throw new NotFoundException($"Product item {itemId} not found.");

        if (item.Version != request.ExpectedVersion)
            throw new ConflictException("Version mismatch — another edit occurred. Reload and retry.");

        item.UpdateDetails(request.Price, request.ProductImage);
        await uow.SaveChangesAsync(ct);
        return CreateVariantHandler.ToDto(item);
    }
}
```

- [ ] **Step 5: Update DeleteVariantHandler**

```csharp
// backend/src/ProductManagement.Application/Variants/DeleteVariantHandler.cs
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Variants;

public class DeleteVariantHandler(IProductItemRepository repo, IUnitOfWork uow)
{
    public async Task HandleAsync(long itemId, CancellationToken ct)
    {
        var item = await repo.GetByIdAsync(itemId, ct)
            ?? throw new NotFoundException($"Product item {itemId} not found.");
        item.Deactivate();
        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 6: Update AdjustStockHandler field names**

Open `backend/src/ProductManagement.Application/Variants/AdjustStockHandler.cs` and replace any reference to `StockQuantity` with `QtyInStock` and update the result DTO:

```csharp
// The key change in AdjustStockHandler: use item.QtyInStock instead of item.StockQuantity
// and update the result DTO field name to NewQtyInStock
public record AdjustStockResult(bool Succeeded, int? NewQtyInStock, int? AvailableQtyInStock);
```

- [ ] **Step 7: Build Application layer**

```bash
cd C:/AI/st-assignment/backend
dotnet build src/ProductManagement.Application/ProductManagement.Application.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add backend/src/ProductManagement.Application/Variants/
git commit -m "feat(app): update variant handlers to use ProductItem with new ERD fields"
```

---

## Task 8: Add Variation application handlers

**Files:**
- Create: `backend/src/ProductManagement.Application/Variations/ListVariationsForCategoryHandler.cs`
- Create: `backend/src/ProductManagement.Application/Variations/CreateVariationHandler.cs`
- Create: `backend/src/ProductManagement.Application/Variations/CreateVariationOptionHandler.cs`

- [ ] **Step 1: Create DTOs and ListVariationsForCategoryHandler**

```csharp
// backend/src/ProductManagement.Application/Variations/ListVariationsForCategoryHandler.cs
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Variations;

public record VariationOptionDto(long Id, string Value);
public record VariationDto(long Id, long CategoryId, string Name, List<VariationOptionDto> Options);

public class ListVariationsForCategoryHandler(IVariationRepository repo)
{
    public async Task<List<VariationDto>> HandleAsync(long categoryId, CancellationToken ct)
    {
        var variations = await repo.ListByCategoryIdAsync(categoryId, ct);
        return variations.Select(v => new VariationDto(
            v.Id,
            v.CategoryId,
            v.Name,
            v.Options.Select(o => new VariationOptionDto(o.Id, o.Value)).ToList()
        )).ToList();
    }
}
```

- [ ] **Step 2: Create CreateVariationHandler**

```csharp
// backend/src/ProductManagement.Application/Variations/CreateVariationHandler.cs
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variations;

public record CreateVariationRequest(long CategoryId, string Name);

public class CreateVariationHandler(IVariationRepository repo, IUnitOfWork uow)
{
    public async Task<VariationDto> HandleAsync(CreateVariationRequest request, CancellationToken ct)
    {
        var variation = Variation.Create(request.CategoryId, request.Name);
        repo.Add(variation);
        await uow.SaveChangesAsync(ct);
        return new VariationDto(variation.Id, variation.CategoryId, variation.Name, []);
    }
}
```

- [ ] **Step 3: Create CreateVariationOptionHandler**

```csharp
// backend/src/ProductManagement.Application/Variations/CreateVariationOptionHandler.cs
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variations;

public record CreateVariationOptionRequest(string Value);

public class CreateVariationOptionHandler(IVariationRepository repo, IUnitOfWork uow)
{
    public async Task<VariationOptionDto> HandleAsync(long variationId, CreateVariationOptionRequest request, CancellationToken ct)
    {
        if (await repo.GetByIdAsync(variationId, ct) is null)
            throw new NotFoundException($"Variation {variationId} not found.");

        if (await repo.OptionExistsAsync(variationId, request.Value, ct))
            throw new ConflictException($"Option '{request.Value}' already exists for this variation.");

        var option = VariationOption.Create(variationId, request.Value);
        repo.AddOption(option);
        await uow.SaveChangesAsync(ct);
        return new VariationOptionDto(option.Id, option.Value);
    }
}
```

- [ ] **Step 4: Build and commit**

```bash
cd C:/AI/st-assignment/backend
dotnet build src/ProductManagement.Application/ProductManagement.Application.csproj
git add backend/src/ProductManagement.Application/Variations/
git commit -m "feat(app): add variation handlers (list, create variation and option)"
```

---

## Task 9: Add Promotion application handlers

**Files:**
- Create: `backend/src/ProductManagement.Application/Promotions/ListPromotionsHandler.cs`
- Create: `backend/src/ProductManagement.Application/Promotions/CreatePromotionHandler.cs`
- Create: `backend/src/ProductManagement.Application/Promotions/AttachPromotionCategoryHandler.cs`

- [ ] **Step 1: Create DTOs and ListPromotionsHandler**

```csharp
// backend/src/ProductManagement.Application/Promotions/ListPromotionsHandler.cs
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Promotions;

public record PromotionCategoryDto(long PromotionId, long CategoryId);
public record PromotionDto(
    long Id,
    string Name,
    string? Description,
    decimal DiscountRate,
    DateOnly StartDate,
    DateOnly EndDate,
    List<PromotionCategoryDto> Categories);

public class ListPromotionsHandler(IPromotionRepository repo)
{
    public async Task<List<PromotionDto>> HandleAsync(CancellationToken ct)
    {
        var promotions = await repo.ListAsync(ct);
        return promotions.Select(ToDto).ToList();
    }

    internal static PromotionDto ToDto(Domain.Entities.Promotion p) => new(
        p.Id, p.Name, p.Description, p.DiscountRate, p.StartDate, p.EndDate,
        p.Categories.Select(c => new PromotionCategoryDto(c.PromotionId, c.CategoryId)).ToList());
}
```

- [ ] **Step 2: Create CreatePromotionHandler**

```csharp
// backend/src/ProductManagement.Application/Promotions/CreatePromotionHandler.cs
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Promotions;

public record CreatePromotionRequest(
    string Name,
    string? Description,
    decimal DiscountRate,
    DateOnly StartDate,
    DateOnly EndDate);

public class CreatePromotionHandler(IPromotionRepository repo, IUnitOfWork uow)
{
    public async Task<PromotionDto> HandleAsync(CreatePromotionRequest request, CancellationToken ct)
    {
        var promotion = Promotion.Create(
            request.Name, request.Description, request.DiscountRate,
            request.StartDate, request.EndDate);
        repo.Add(promotion);
        await uow.SaveChangesAsync(ct);
        return ListPromotionsHandler.ToDto(promotion);
    }
}
```

- [ ] **Step 3: Create AttachPromotionCategoryHandler**

```csharp
// backend/src/ProductManagement.Application/Promotions/AttachPromotionCategoryHandler.cs
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Promotions;

public class AttachPromotionCategoryHandler(IPromotionRepository repo, IUnitOfWork uow)
{
    public async Task HandleAsync(long promotionId, long categoryId, CancellationToken ct)
    {
        if (await repo.GetByIdAsync(promotionId, ct) is null)
            throw new NotFoundException($"Promotion {promotionId} not found.");

        if (await repo.CategoryAlreadyAttachedAsync(promotionId, categoryId, ct))
            throw new ConflictException($"Category {categoryId} is already attached to promotion {promotionId}.");

        repo.AddCategory(PromotionCategory.Create(promotionId, categoryId));
        await uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 4: Build and commit**

```bash
cd C:/AI/st-assignment/backend
dotnet build src/ProductManagement.Application/ProductManagement.Application.csproj
git add backend/src/ProductManagement.Application/Promotions/
git commit -m "feat(app): add promotion handlers (list, create, attach-to-category)"
```

---

## Task 10: Update VariantsController routes to match spec

The spec (PDF §8) defines:
- `GET /products/{id}/items` — list SKUs
- `POST /products/{id}/items` — add SKU
- `PATCH /product-items/{id}` — update price/image (with `If-Match: <version>`)
- `POST /product-items/{id}/inventory/adjust` — atomic stock delta

The existing controller is at `/products/{productId}/variants`. We update routes and split into two controllers.

**Files:**
- Modify: `backend/src/ProductManagement.Api/Controllers/VariantsController.cs` → change route to `items`
- Create: `backend/src/ProductManagement.Api/Controllers/ProductItemsController.cs`

- [ ] **Step 1: Update VariantsController route from `variants` to `items`**

```csharp
// backend/src/ProductManagement.Api/Controllers/VariantsController.cs
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.Variants;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/products/{productId:long}/items")]
public class VariantsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] ListVariantsHandler handler, long productId, CancellationToken ct)
        => Ok(await handler.HandleAsync(productId, ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] CreateVariantHandler handler, long productId,
        [FromBody] CreateVariantRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(productId, request, ct);
        return CreatedAtAction(nameof(List), new { productId }, result);
    }
}
```

- [ ] **Step 2: Create ProductItemsController for `/product-items/{id}` routes**

```csharp
// backend/src/ProductManagement.Api/Controllers/ProductItemsController.cs
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.Variants;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/product-items")]
public class ProductItemsController : ControllerBase
{
    [HttpPatch("{id:long}")]
    public async Task<IActionResult> Update(
        [FromServices] UpdateVariantHandler handler, long id,
        [FromBody] UpdateVariantRequest request, CancellationToken ct)
    {
        // If-Match header carries the expected version
        if (!Request.Headers.TryGetValue("If-Match", out var ifMatch) ||
            !int.TryParse(ifMatch.ToString(), out var expectedVersion))
            return BadRequest(new { error = "If-Match header with numeric version is required." });

        var requestWithVersion = request with { ExpectedVersion = expectedVersion };
        return Ok(await handler.HandleAsync(id, requestWithVersion, ct));
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        [FromServices] DeleteVariantHandler handler, long id, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/inventory/adjust")]
    public async Task<IActionResult> AdjustInventory(
        [FromServices] AdjustStockHandler handler, long id,
        [FromBody] AdjustStockRequest request, CancellationToken ct)
    {
        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var values)
            ? values.ToString() : null;
        // productId is needed for the cache key — pass id for both since item is uniquely identified
        var result = await handler.HandleAsync(id, id, request, idempotencyKey, ct);
        return result.Succeeded ? Ok(result) : Conflict(result);
    }
}
```

- [ ] **Step 3: Build API project**

```bash
cd C:/AI/st-assignment/backend
dotnet build src/ProductManagement.Api/ProductManagement.Api.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add backend/src/ProductManagement.Api/Controllers/VariantsController.cs \
        backend/src/ProductManagement.Api/Controllers/ProductItemsController.cs
git commit -m "feat(api): update product items routes — /items and /product-items/{id} per spec §8"
```

---

## Task 11: Add VariationsController and PromotionsController

**Files:**
- Create: `backend/src/ProductManagement.Api/Controllers/VariationsController.cs`
- Create: `backend/src/ProductManagement.Api/Controllers/PromotionsController.cs`
- Modify: `backend/src/ProductManagement.Api/Controllers/CategoriesController.cs` — add variations sub-route

- [ ] **Step 1: Create VariationsController**

```csharp
// backend/src/ProductManagement.Api/Controllers/VariationsController.cs
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.Variations;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1")]
public class VariationsController : ControllerBase
{
    // GET /api/v1/categories/{categoryId}/variations
    [HttpGet("categories/{categoryId:long}/variations")]
    public async Task<IActionResult> ListForCategory(
        [FromServices] ListVariationsForCategoryHandler handler, long categoryId, CancellationToken ct)
        => Ok(await handler.HandleAsync(categoryId, ct));

    // POST /api/v1/variations
    [HttpPost("variations")]
    public async Task<IActionResult> Create(
        [FromServices] CreateVariationHandler handler,
        [FromBody] CreateVariationRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return CreatedAtAction(nameof(ListForCategory), new { categoryId = request.CategoryId }, result);
    }

    // POST /api/v1/variations/{id}/options
    [HttpPost("variations/{id:long}/options")]
    public async Task<IActionResult> CreateOption(
        [FromServices] CreateVariationOptionHandler handler, long id,
        [FromBody] CreateVariationOptionRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, request, ct);
        return Created($"/api/v1/variations/{id}/options/{result.Id}", result);
    }
}
```

- [ ] **Step 2: Create PromotionsController**

```csharp
// backend/src/ProductManagement.Api/Controllers/PromotionsController.cs
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.Promotions;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/promotions")]
public class PromotionsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] ListPromotionsHandler handler, CancellationToken ct)
        => Ok(await handler.HandleAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] CreatePromotionHandler handler,
        [FromBody] CreatePromotionRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return CreatedAtAction(nameof(List), result);
    }

    [HttpPost("{id:long}/categories/{categoryId:long}")]
    public async Task<IActionResult> AttachCategory(
        [FromServices] AttachPromotionCategoryHandler handler,
        long id, long categoryId, CancellationToken ct)
    {
        await handler.HandleAsync(id, categoryId, ct);
        return NoContent();
    }
}
```

- [ ] **Step 3: Register new handlers in DI**

Find the DI registration for application handlers (likely in `Program.cs` or a `DependencyInjection.cs`). Add:

```csharp
services.AddScoped<ListVariationsForCategoryHandler>();
services.AddScoped<CreateVariationHandler>();
services.AddScoped<CreateVariationOptionHandler>();
services.AddScoped<ListPromotionsHandler>();
services.AddScoped<CreatePromotionHandler>();
services.AddScoped<AttachPromotionCategoryHandler>();
```

- [ ] **Step 4: Build and run to verify all routes are accessible**

```bash
cd C:/AI/st-assignment/backend
dotnet build src/ProductManagement.Api/ProductManagement.Api.csproj
dotnet run --project src/ProductManagement.Api --no-launch-profile &
# After ~3 seconds, check the Swagger UI
curl http://localhost:5000/swagger/v1/swagger.json | grep -o '"[^"]*":{"get\|post\|patch\|delete' | head -30
```

Expected: Routes include `/api/v1/categories/{categoryId}/variations`, `/api/v1/variations`, `/api/v1/promotions`, `/api/v1/product-items/{id}`, `/api/v1/products/{id}/items`.

- [ ] **Step 5: Commit**

```bash
git add backend/src/ProductManagement.Api/Controllers/VariationsController.cs \
        backend/src/ProductManagement.Api/Controllers/PromotionsController.cs
git commit -m "feat(api): add VariationsController and PromotionsController per spec §8"
```

---

## Task 12: Update frontend TypeScript types

**Files:**
- Modify: `frontend/src/features/products/types.ts`

- [ ] **Step 1: Replace types.ts entirely**

```typescript
// frontend/src/features/products/types.ts

export interface VariationOption {
  id: number;
  value: string;
}

export interface Variation {
  id: number;
  categoryId: number;
  name: string;
  options: VariationOption[];
}

export interface ProductItem {
  id: number;
  sku: string;
  price: number;
  qtyInStock: number;
  productImage: string | null;
  isActive: boolean;
  version: number;
  variationOptionIds: number[];
}

export interface Product {
  id: number;
  name: string;
  slug: string;
  description: string | null;
  categoryId: number;
  brand: string | null;
  status: string;
  attributes: string;
  imageUrl: string | null;
  items: ProductItem[];
}

export interface ProductListItem {
  id: number;
  name: string;
  slug: string;
  categoryId: number;
  brand: string | null;
  status: string;
  minPrice: number | null;
  maxPrice: number | null;
  totalStock: number;
  imageUrl: string | null;
}

export interface PagedResult<T> {
  items: T[];
  nextCursor: string | null;
  hasMore: boolean;
  totalCount: number;
}

export interface CreateProductItemRequest {
  sku: string;
  price: number;
  qtyInStock: number;
  productImage: string | null;
  variationOptionIds: number[];
}

export interface UpdateProductItemRequest {
  price: number;
  productImage: string | null;
}

export interface CreateProductRequest {
  name: string;
  slug: string;
  categoryId: number;
  brand: string | null;
  description: string | null;
  attributes: string;
  items: CreateProductItemRequest[];
}

export interface UpdateProductRequest {
  name: string;
  description: string | null;
  categoryId: number;
  brand: string | null;
  attributes: string;
}

export interface AdjustStockResult {
  succeeded: boolean;
  newQtyInStock: number | null;
  availableQtyInStock: number | null;
}

export interface CreateVariationRequest {
  categoryId: number;
  name: string;
}

export interface CreateVariationOptionRequest {
  value: string;
}

export interface PromotionCategory {
  promotionId: number;
  categoryId: number;
}

export interface Promotion {
  id: number;
  name: string;
  description: string | null;
  discountRate: number;
  startDate: string;
  endDate: string;
  categories: PromotionCategory[];
}

export interface CreatePromotionRequest {
  name: string;
  description: string | null;
  discountRate: number;
  startDate: string;
  endDate: string;
}
```

- [ ] **Step 2: Build TypeScript to verify no type errors**

```bash
cd C:/AI/st-assignment/frontend
npx tsc --noEmit
```

Expected: Errors from files that still reference old `Variant` type — these will be fixed in Tasks 13 and 14.

- [ ] **Step 3: Commit**

```bash
git add frontend/src/features/products/types.ts
git commit -m "feat(frontend): update types — ProductItem replaces Variant, new ERD fields"
```

---

## Task 13: Update frontend RTK Query API endpoints

**Files:**
- Modify: `frontend/src/features/products/api.ts`

- [ ] **Step 1: Replace api.ts entirely**

```typescript
// frontend/src/features/products/api.ts
import { api } from '../../shared/lib/apiBase';
import type {
  Product,
  ProductListItem,
  PagedResult,
  CreateProductRequest,
  UpdateProductRequest,
  ProductItem,
  CreateProductItemRequest,
  UpdateProductItemRequest,
  AdjustStockResult,
  Variation,
  CreateVariationRequest,
  CreateVariationOptionRequest,
  VariationOption,
  Promotion,
  CreatePromotionRequest,
} from './types';

export interface ListProductsParams {
  categoryId?: number;
  status?: number;
  q?: string;
  minPrice?: number;
  maxPrice?: number;
  cursor?: string;
  limit?: number;
}

export const productsApi = api.injectEndpoints({
  endpoints: (builder) => ({
    listProducts: builder.query<PagedResult<ProductListItem>, ListProductsParams>({
      query: (params) => ({ url: '/products', method: 'GET', params }),
      providesTags: ['ProductList'],
    }),

    getProduct: builder.query<Product, number>({
      query: (id) => ({ url: `/products/${id}`, method: 'GET' }),
      providesTags: (_result, _error, id) => [{ type: 'Product', id }],
    }),

    createProduct: builder.mutation<Product, CreateProductRequest>({
      query: (body) => ({ url: '/products', method: 'POST', data: body }),
      invalidatesTags: ['ProductList'],
    }),

    updateProduct: builder.mutation<Product, { id: number; body: UpdateProductRequest }>({
      query: ({ id, body }) => ({ url: `/products/${id}`, method: 'PUT', data: body }),
      invalidatesTags: (_result, _error, { id }) => ['ProductList', { type: 'Product', id }],
    }),

    deleteProduct: builder.mutation<void, number>({
      query: (id) => ({ url: `/products/${id}`, method: 'DELETE' }),
      invalidatesTags: ['ProductList'],
    }),

    // Product Items (was Variants)
    createProductItem: builder.mutation<ProductItem, { productId: number; body: CreateProductItemRequest }>({
      query: ({ productId, body }) => ({ url: `/products/${productId}/items`, method: 'POST', data: body }),
      invalidatesTags: (_result, _error, { productId }) => [{ type: 'Product', id: productId }],
    }),

    updateProductItem: builder.mutation<ProductItem, { itemId: number; body: UpdateProductItemRequest; version: number }>({
      query: ({ itemId, body, version }) => ({
        url: `/product-items/${itemId}`,
        method: 'PATCH',
        data: body,
        headers: { 'If-Match': String(version) },
      }),
      invalidatesTags: ['ProductList'],
    }),

    deleteProductItem: builder.mutation<void, { productId: number; itemId: number }>({
      query: ({ itemId }) => ({ url: `/product-items/${itemId}`, method: 'DELETE' }),
      invalidatesTags: (_result, _error, { productId }) => [{ type: 'Product', id: productId }],
    }),

    adjustStock: builder.mutation<AdjustStockResult, { productId: number; itemId: number; delta: number }>({
      query: ({ itemId, delta }) => ({
        url: `/product-items/${itemId}/inventory/adjust`,
        method: 'POST',
        data: { delta },
        headers: { 'Idempotency-Key': crypto.randomUUID() },
      }),
      async onQueryStarted({ productId, itemId, delta }, { dispatch, queryFulfilled }) {
        const patch = dispatch(
          productsApi.util.updateQueryData('getProduct', productId, (draft) => {
            const item = draft.items.find((i) => i.id === itemId);
            if (item) item.qtyInStock += delta;
          }),
        );
        try {
          await queryFulfilled;
        } catch {
          patch.undo();
        }
      },
    }),

    uploadImage: builder.mutation<{ imageUrl: string }, { productId: number; formData: FormData }>({
      query: ({ productId, formData }) => ({
        url: `/products/${productId}/image`,
        method: 'POST',
        data: formData,
        headers: { 'Content-Type': 'multipart/form-data' },
      }),
      invalidatesTags: (_result, _error, { productId }) => [{ type: 'Product', id: productId }],
    }),

    deleteImage: builder.mutation<void, number>({
      query: (productId) => ({ url: `/products/${productId}/image`, method: 'DELETE' }),
      invalidatesTags: (_result, _error, productId) => [{ type: 'Product', id: productId }],
    }),

    // Variations
    listVariationsForCategory: builder.query<Variation[], number>({
      query: (categoryId) => ({ url: `/categories/${categoryId}/variations`, method: 'GET' }),
      providesTags: (_result, _error, categoryId) => [{ type: 'CategoryVariations' as const, id: categoryId }],
    }),

    createVariation: builder.mutation<Variation, CreateVariationRequest>({
      query: (body) => ({ url: '/variations', method: 'POST', data: body }),
      invalidatesTags: (_result, _error, { categoryId }) => [{ type: 'CategoryVariations' as const, id: categoryId }],
    }),

    createVariationOption: builder.mutation<VariationOption, { variationId: number; body: CreateVariationOptionRequest }>({
      query: ({ variationId, body }) => ({ url: `/variations/${variationId}/options`, method: 'POST', data: body }),
    }),

    // Promotions
    listPromotions: builder.query<Promotion[], void>({
      query: () => ({ url: '/promotions', method: 'GET' }),
      providesTags: ['PromotionList'],
    }),

    createPromotion: builder.mutation<Promotion, CreatePromotionRequest>({
      query: (body) => ({ url: '/promotions', method: 'POST', data: body }),
      invalidatesTags: ['PromotionList'],
    }),

    attachPromotionCategory: builder.mutation<void, { promotionId: number; categoryId: number }>({
      query: ({ promotionId, categoryId }) => ({
        url: `/promotions/${promotionId}/categories/${categoryId}`,
        method: 'POST',
      }),
      invalidatesTags: ['PromotionList'],
    }),
  }),
});

export const {
  useListProductsQuery,
  useGetProductQuery,
  useCreateProductMutation,
  useUpdateProductMutation,
  useDeleteProductMutation,
  useCreateProductItemMutation,
  useUpdateProductItemMutation,
  useDeleteProductItemMutation,
  useAdjustStockMutation,
  useUploadImageMutation,
  useDeleteImageMutation,
  useListVariationsForCategoryQuery,
  useCreateVariationMutation,
  useCreateVariationOptionMutation,
  useListPromotionsQuery,
  useCreatePromotionMutation,
  useAttachPromotionCategoryMutation,
} = productsApi;
```

- [ ] **Step 2: Add new tag types to RTK Query base**

Open `frontend/src/shared/lib/apiBase.ts` and add `'CategoryVariations'` and `'PromotionList'` to the tag types list:

```typescript
// In the tagTypes array, add:
tagTypes: ['ProductList', 'Product', 'CategoryList', 'CategoryVariations', 'PromotionList'],
```

- [ ] **Step 3: Run TypeScript check**

```bash
cd C:/AI/st-assignment/frontend
npx tsc --noEmit
```

Expected: Errors will be in components referencing the old `Variant` type and `variants` property — fix those in Task 14.

- [ ] **Step 4: Commit**

```bash
git add frontend/src/features/products/api.ts frontend/src/shared/lib/apiBase.ts
git commit -m "feat(frontend): update RTK Query endpoints — items/product-items routes, add variations and promotions"
```

---

## Task 14: Update frontend components to use new type names

**Files:**
- Modify: All components that reference `Variant`, `variants`, `stockQuantity`, `createVariant`, `deleteVariant`

- [ ] **Step 1: Find all affected component files**

```bash
cd C:/AI/st-assignment/frontend
grep -rl "variants\|stockQuantity\|Variant\|createVariant\|deleteVariant\|adjustStock" src/features/products/
```

- [ ] **Step 2: Update VariantsTable component (or equivalent)**

Find the variants table component (likely `frontend/src/features/products/components/VariantsTable.tsx` or similar). Update:

- Replace `product.variants` → `product.items`
- Replace `variant.stockQuantity` → `item.qtyInStock`
- Replace `useCreateVariantMutation` → `useCreateProductItemMutation`
- Replace `useDeleteVariantMutation` → `useDeleteProductItemMutation`
- Replace `adjustStock` call: update arg from `{ productId, variantId, delta }` → `{ productId, itemId, delta }`
- Update `CreateVariantRequest` type references → `CreateProductItemRequest`
- Remove `size`, `color`, `compareAtPrice`, `barcode` fields from forms; add `variationOptionIds` (multi-select from available variations)

- [ ] **Step 3: Update ProductForm component**

Find `frontend/src/features/products/components/ProductForm.tsx` or similar. Update:

- Replace `variants: CreateVariantRequest[]` → `items: CreateProductItemRequest[]`
- Update any inline variant form fields

- [ ] **Step 4: Run TypeScript check to confirm zero type errors**

```bash
cd C:/AI/st-assignment/frontend
npx tsc --noEmit
```

Expected: `0 errors`.

- [ ] **Step 5: Run dev server and manually test the product list page**

```bash
cd C:/AI/st-assignment/frontend
npm run dev
```

Open `http://localhost:5173` (or whatever port Vite uses). Verify:
1. Product list loads without console errors
2. Clicking a product shows its items (not variants) in the detail view
3. Stock adjustment works — the number updates optimistically and reflects the new `qtyInStock` field name

- [ ] **Step 6: Commit**

```bash
git add frontend/src/features/products/
git commit -m "feat(frontend): update components to use ProductItem, qtyInStock, items navigation"
```

---

## Task 15: End-to-end smoke test

- [ ] **Step 1: Verify the database schema**

```bash
# Connect to postgres and check tables exist
psql $DATABASE_URL -c "\dt"
```

Expected output includes: `categories`, `products`, `product_items`, `variation`, `variation_option`, `product_configuration`, `promotion`, `promotion_category`

- [ ] **Step 2: Smoke test category variations endpoint**

```bash
# Create a variation for category 1
curl -s -X POST http://localhost:5000/api/v1/variations \
  -H "Content-Type: application/json" \
  -d '{"categoryId": 1, "name": "Color"}' | jq .

# Add an option to the new variation (use the id returned above)
curl -s -X POST http://localhost:5000/api/v1/variations/1/options \
  -H "Content-Type: application/json" \
  -d '{"value": "Red"}' | jq .

# List variations for category 1
curl -s http://localhost:5000/api/v1/categories/1/variations | jq .
```

Expected: Each call returns 201/200 with the created/listed data, no 500 errors.

- [ ] **Step 3: Smoke test product item creation**

```bash
# Create a product item for product 1
curl -s -X POST http://localhost:5000/api/v1/products/1/items \
  -H "Content-Type: application/json" \
  -d '{"sku":"SKU-001","price":29.99,"qtyInStock":100,"productImage":null,"variationOptionIds":[1]}' | jq .
```

Expected: `201 Created` with item including `version: 0`.

- [ ] **Step 4: Smoke test PATCH product-item with If-Match**

```bash
# Update the item with version 0
curl -s -X PATCH http://localhost:5000/api/v1/product-items/1 \
  -H "Content-Type: application/json" \
  -H "If-Match: 0" \
  -d '{"price":34.99,"productImage":null}' | jq .
```

Expected: `200 OK` with `version: 1`.

- [ ] **Step 5: Smoke test inventory adjust**

```bash
curl -s -X POST http://localhost:5000/api/v1/product-items/1/inventory/adjust \
  -H "Content-Type: application/json" \
  -H "Idempotency-Key: test-key-1" \
  -d '{"delta":-5}' | jq .
```

Expected: `200 OK` with `succeeded: true`, `newQtyInStock: 95`.

- [ ] **Step 6: Smoke test promotions**

```bash
curl -s -X POST http://localhost:5000/api/v1/promotions \
  -H "Content-Type: application/json" \
  -d '{"name":"Summer Sale","description":null,"discountRate":0.2,"startDate":"2026-08-01","endDate":"2026-08-31"}' | jq .

# Attach category 1
curl -s -X POST http://localhost:5000/api/v1/promotions/1/categories/1 | jq .
```

Expected: 201 and 204 respectively.

- [ ] **Step 7: Final commit**

```bash
git add -A
git commit -m "chore: ERD alignment complete — product_items, variation taxonomy, promotions all working"
```

---

## Self-Review

**Spec coverage check:**

| Spec requirement (PDF §8) | Covered by task |
|---|---|
| GET /categories | Unchanged — already exists |
| POST /categories | Unchanged — already exists |
| PATCH /categories/{id} | Unchanged — already exists |
| DELETE /categories/{id} | Unchanged — already exists |
| GET /products | Unchanged — already exists |
| POST /products | Updated (Task 7 — items instead of variants in body) |
| GET /products/{id} | Updated (Task 7 — items in response) |
| PATCH /products/{id} | Unchanged — already exists as PUT |
| DELETE /products/{id} | Unchanged — already exists (soft delete) |
| GET /products/{id}/items | Task 10 |
| POST /products/{id}/items | Task 10 |
| PATCH /product-items/{id} with If-Match | Task 10 |
| POST /product-items/{id}/inventory/adjust | Task 10 |
| GET /categories/{id}/variations | Task 11 |
| POST /variations | Task 11 |
| POST /variations/{id}/options | Task 11 |
| GET/POST /promotions | Task 11 |
| POST /promotions/{id}/categories/{categoryId} | Task 11 |
| product_item.version (optimistic concurrency) | Task 1, Task 7, Task 10 |
| product_item.qty_in_stock >= 0 CHECK | Task 4 |
| Atomic stock adjust (conditional UPDATE) | Task 7 (AdjustStockHandler) |
| Idempotency-Key on inventory | Task 10, Task 13 |
| Soft delete (is_active) | Task 1 (Deactivate), unchanged |
| product_configuration junction | Task 2, Task 4, Task 6 |
| promotion_category junction | Task 3, Task 9, Task 11 |
| discount_rate CHECK (0, 1] | Task 4 |

**No placeholders found.** All steps include exact code or exact commands.

**Type consistency:** `VariantDto` is used consistently across Tasks 7, 10, 13 with fields `id`, `sku`, `price`, `qtyInStock`, `productImage`, `isActive`, `version`, `variationOptionIds`. Frontend `ProductItem` matches these field names.
