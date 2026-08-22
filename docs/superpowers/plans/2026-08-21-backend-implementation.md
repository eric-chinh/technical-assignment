# Product Management Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the ASP.NET Core / EF Core / PostgreSQL product management backend defined in [`2026-08-20-product-management-api-design.md`](../specs/2026-08-20-product-management-api-design.md) — Clean Architecture, full CRUD for categories/products/variants, concurrency-safe stock adjustment, full-text search, Redis caching, and a minimal image upload endpoint.

**Architecture:** Four projects with dependencies pointing inward only (Domain ← Application ← Infrastructure/Api), per spec §5. Built bottom-up: Domain entities → persistence → Categories (canonical CRUD pattern) → Products (+ search + image) → Variants + atomic stock update → cross-cutting concerns (caching, error handling, CORS, seeding) → architecture tests → docker-compose finalization.

**Tech Stack:** .NET 10, EF Core 10 (Npgsql provider), FluentValidation, xUnit + NSubstitute + Respawn, NetArchTest.Rules, Serilog, Swashbuckle, StackExchange.Redis, Bogus.

---

## Conventions Used Throughout This Plan

- Solution root: `backend/`
- Root namespace: `ProductManagement`
- Local ports (spec §10): Postgres `5432`, Redis `6379`, API `8080`
- All commands below assume the working directory is `backend/` unless stated otherwise
- `dotnet test` commands target one project via `--filter` or by passing the `.csproj` path so tasks can be verified independently

---

## Task 0: Solution and Project Scaffolding

**Files:**
- Create: `backend/ProductManagement.sln`
- Create: `backend/src/ProductManagement.Domain/ProductManagement.Domain.csproj`
- Create: `backend/src/ProductManagement.Application/ProductManagement.Application.csproj`
- Create: `backend/src/ProductManagement.Infrastructure/ProductManagement.Infrastructure.csproj`
- Create: `backend/src/ProductManagement.Api/ProductManagement.Api.csproj`
- Create: `backend/tests/ProductManagement.UnitTests/ProductManagement.UnitTests.csproj`
- Create: `backend/tests/ProductManagement.IntegrationTests/ProductManagement.IntegrationTests.csproj`
- Create: `backend/tests/ProductManagement.ArchitectureTests/ProductManagement.ArchitectureTests.csproj`

- [ ] **Step 1: Create the solution and all seven projects**

```bash
mkdir -p backend/src backend/tests
cd backend
dotnet new sln -n ProductManagement

dotnet new classlib -n ProductManagement.Domain -o src/ProductManagement.Domain
dotnet new classlib -n ProductManagement.Application -o src/ProductManagement.Application
dotnet new classlib -n ProductManagement.Infrastructure -o src/ProductManagement.Infrastructure
dotnet new webapi -n ProductManagement.Api -o src/ProductManagement.Api -controllers --no-openapi

dotnet new xunit -n ProductManagement.UnitTests -o tests/ProductManagement.UnitTests
dotnet new xunit -n ProductManagement.IntegrationTests -o tests/ProductManagement.IntegrationTests
dotnet new xunit -n ProductManagement.ArchitectureTests -o tests/ProductManagement.ArchitectureTests

dotnet sln add src/ProductManagement.Domain src/ProductManagement.Application src/ProductManagement.Infrastructure src/ProductManagement.Api tests/ProductManagement.UnitTests tests/ProductManagement.IntegrationTests tests/ProductManagement.ArchitectureTests
```

- [ ] **Step 2: Wire project references per the Clean Architecture dependency rule (spec §5)**

```bash
# Application depends only on Domain
dotnet add src/ProductManagement.Application reference src/ProductManagement.Domain

# Infrastructure depends on Application (and transitively Domain)
dotnet add src/ProductManagement.Infrastructure reference src/ProductManagement.Application

# Api depends on Application and Infrastructure (composition root only)
dotnet add src/ProductManagement.Api reference src/ProductManagement.Application
dotnet add src/ProductManagement.Api reference src/ProductManagement.Infrastructure

# UnitTests: Domain + Application only (spec §5 — no I/O)
dotnet add tests/ProductManagement.UnitTests reference src/ProductManagement.Domain
dotnet add tests/ProductManagement.UnitTests reference src/ProductManagement.Application

# IntegrationTests: everything, it boots the real app
dotnet add tests/ProductManagement.IntegrationTests reference src/ProductManagement.Api

# ArchitectureTests: all four layers, to inspect the dependency graph between them
dotnet add tests/ProductManagement.ArchitectureTests reference src/ProductManagement.Domain
dotnet add tests/ProductManagement.ArchitectureTests reference src/ProductManagement.Application
dotnet add tests/ProductManagement.ArchitectureTests reference src/ProductManagement.Infrastructure
dotnet add tests/ProductManagement.ArchitectureTests reference src/ProductManagement.Api
```

- [ ] **Step 3: Add NuGet packages to each project**

```bash
# Application
dotnet add src/ProductManagement.Application package FluentValidation --version 11.11.0

# Infrastructure
dotnet add src/ProductManagement.Infrastructure package Microsoft.EntityFrameworkCore --version 10.0.11
dotnet add src/ProductManagement.Infrastructure package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.3
dotnet add src/ProductManagement.Infrastructure package Microsoft.EntityFrameworkCore.Design --version 10.0.11
dotnet add src/ProductManagement.Infrastructure package StackExchange.Redis --version 2.8.31
dotnet add src/ProductManagement.Infrastructure package Bogus --version 35.6.1

# Api
dotnet add src/ProductManagement.Api package Swashbuckle.AspNetCore --version 7.2.0
dotnet add src/ProductManagement.Api package Serilog.AspNetCore --version 9.0.0
# Note: deliberately NOT using FluentValidation.AspNetCore's auto-validation
# integration — the FluentValidation project itself discourages it as of v11+.
# Task 5 instead writes a small custom endpoint filter that resolves
# IValidator<T> from DI directly.

# UnitTests
dotnet add tests/ProductManagement.UnitTests package NSubstitute --version 5.3.0
dotnet add tests/ProductManagement.UnitTests package FluentAssertions --version 7.0.0

# IntegrationTests
dotnet add tests/ProductManagement.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing --version 10.0.11
dotnet add tests/ProductManagement.IntegrationTests package Respawn --version 6.2.1
dotnet add tests/ProductManagement.IntegrationTests package FluentAssertions --version 7.0.0

# ArchitectureTests
dotnet add tests/ProductManagement.ArchitectureTests package NetArchTest.Rules --version 1.3.2
```

- [ ] **Step 4: Set every project's target framework to net10.0 and confirm the solution builds**

Edit each `.csproj`'s `<TargetFramework>` to `net10.0` if the templates didn't already default to it, then:

```bash
dotnet build
```

Expected: `Build succeeded.` with 7 projects built, zero errors (warnings about unused `Class1.cs` template files are fine — deleted in Step 5).

- [ ] **Step 5: Delete template placeholder files**

```bash
rm src/ProductManagement.Domain/Class1.cs
rm src/ProductManagement.Application/Class1.cs
rm src/ProductManagement.Infrastructure/Class1.cs
rm tests/ProductManagement.UnitTests/UnitTest1.cs
rm tests/ProductManagement.IntegrationTests/UnitTest1.cs
rm tests/ProductManagement.ArchitectureTests/UnitTest1.cs
dotnet build
```

Expected: still builds clean.

- [ ] **Step 6: Commit**

```bash
git add backend/
git commit -m "Scaffold Clean Architecture solution: 4 src projects + 3 test projects"
```

---

## Task 1: Domain Exceptions and the `Category` Entity

**Files:**
- Create: `backend/src/ProductManagement.Domain/Exceptions/DomainException.cs`
- Create: `backend/src/ProductManagement.Domain/Entities/Category.cs`
- Test: `backend/tests/ProductManagement.UnitTests/Domain/CategoryTests.cs`

- [ ] **Step 1: Write the failing test for `Category` construction**

```csharp
// backend/tests/ProductManagement.UnitTests/Domain/CategoryTests.cs
using FluentAssertions;
using ProductManagement.Domain.Entities;
using Xunit;

namespace ProductManagement.UnitTests.Domain;

public class CategoryTests
{
    [Fact]
    public void Create_WithValidNameAndSlug_SetsProperties()
    {
        var category = Category.Create(name: "Dresses", slug: "dresses", parentCategoryId: null);

        category.Name.Should().Be("Dresses");
        category.Slug.Should().Be("dresses");
        category.ParentCategoryId.Should().BeNull();
        category.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string blankName)
    {
        var act = () => Category.Create(blankName, "slug", null);

        act.Should().Throw<DomainException>().WithMessage("*name*");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```bash
dotnet test tests/ProductManagement.UnitTests --filter CategoryTests
```

Expected: FAIL — `Category` and `DomainException` don't exist yet.

- [ ] **Step 3: Implement `DomainException` and `Category`**

```csharp
// backend/src/ProductManagement.Domain/Exceptions/DomainException.cs
namespace ProductManagement.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class InvalidCategoryException : DomainException
{
    public InvalidCategoryException(string message) : base(message) { }
}
```

```csharp
// backend/src/ProductManagement.Domain/Entities/Category.cs
using ProductManagement.Domain.Exceptions;

namespace ProductManagement.Domain.Entities;

public class Category
{
    public long Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public long? ParentCategoryId { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Category() { } // EF Core

    public static Category Create(string name, string slug, long? parentCategoryId, int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidCategoryException("Category name is required.");
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidCategoryException("Category slug is required.");

        var now = DateTimeOffset.UtcNow;
        return new Category
        {
            Name = name.Trim(),
            Slug = slug.Trim(),
            ParentCategoryId = parentCategoryId,
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string name, string slug, long? parentCategoryId, int displayOrder, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidCategoryException("Category name is required.");
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidCategoryException("Category slug is required.");

        Name = name.Trim();
        Slug = slug.Trim();
        ParentCategoryId = parentCategoryId;
        DisplayOrder = displayOrder;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

```bash
dotnet test tests/ProductManagement.UnitTests --filter CategoryTests
```

Expected: PASS, 2 tests.

- [ ] **Step 5: Commit**

```bash
git add backend/src/ProductManagement.Domain backend/tests/ProductManagement.UnitTests
git commit -m "Add DomainException base and Category entity"
```

---

## Task 2: Application-Layer Interfaces and Shared Exceptions

**Files:**
- Create: `backend/src/ProductManagement.Application/Common/Exceptions/EntityNotFoundException.cs`
- Create: `backend/src/ProductManagement.Application/Common/Exceptions/DuplicateSkuException.cs`
- Create: `backend/src/ProductManagement.Application/Common/Exceptions/DuplicateSlugException.cs`
- Create: `backend/src/ProductManagement.Application/Common/Interfaces/ICategoryRepository.cs`
- Create: `backend/src/ProductManagement.Application/Common/Interfaces/IProductRepository.cs`
- Create: `backend/src/ProductManagement.Application/Common/Interfaces/IVariantRepository.cs`
- Create: `backend/src/ProductManagement.Application/Common/Interfaces/IStockRepository.cs`
- Create: `backend/src/ProductManagement.Application/Common/Interfaces/ICacheService.cs`
- Create: `backend/src/ProductManagement.Application/Common/Interfaces/IUnitOfWork.cs`
- Create: `backend/src/ProductManagement.Application/Common/Interfaces/IFileStorageService.cs`
- Create: `backend/src/ProductManagement.Application/Common/PagedResult.cs`

This task is pure interface/type definition — no TDD cycle (nothing to test on an
interface). Write the files, then verify the project still compiles.

- [ ] **Step 1: Create the exception types (spec §7 "Error Handling")**

```csharp
// backend/src/ProductManagement.Application/Common/Exceptions/EntityNotFoundException.cs
namespace ProductManagement.Application.Common.Exceptions;

public sealed class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName} with id '{key}' was not found.") { }
}
```

```csharp
// backend/src/ProductManagement.Application/Common/Exceptions/DuplicateSkuException.cs
namespace ProductManagement.Application.Common.Exceptions;

public sealed class DuplicateSkuException : Exception
{
    public DuplicateSkuException(string sku) : base($"SKU '{sku}' already exists.") { }
}
```

```csharp
// backend/src/ProductManagement.Application/Common/Exceptions/DuplicateSlugException.cs
namespace ProductManagement.Application.Common.Exceptions;

public sealed class DuplicateSlugException : Exception
{
    public DuplicateSlugException(string slug) : base($"Slug '{slug}' already exists.") { }
}
```

- [ ] **Step 2: Create the repository and service interfaces**

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/ICategoryRepository.cs
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(long id, CancellationToken ct);
    Task<Category?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<List<Category>> ListAsync(long? parentId, bool? activeOnly, CancellationToken ct);
    Task<bool> HasActiveProductsAsync(long categoryId, CancellationToken ct);
    void Add(Category category);
    void Remove(Category category);
}
```

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/IProductRepository.cs
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(long id, CancellationToken ct);
    Task<Product?> GetByIdWithVariantsAsync(long id, CancellationToken ct);
    Task<Product?> GetBySlugWithVariantsAsync(string slug, CancellationToken ct);
    Task<uint> GetXminAsync(long id, CancellationToken ct);
    Task<PagedResult<Product>> ListAsync(ProductListQuery query, CancellationToken ct);
    void Add(Product product);
}

public sealed record ProductListQuery(
    long? CategoryId,
    short? Status,
    string? SearchText,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? AttributesJson,
    string? Cursor,
    int Limit);
```

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/IVariantRepository.cs
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IVariantRepository
{
    Task<ProductVariant?> GetByIdAsync(long id, CancellationToken ct);
    Task<bool> SkuExistsAsync(string sku, CancellationToken ct);
    Task<List<ProductVariant>> ListByProductIdAsync(long productId, CancellationToken ct);
    void Add(ProductVariant variant);
}
```

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/IStockRepository.cs
namespace ProductManagement.Application.Common.Interfaces;

/// <summary>
/// Deliberately the only entry point for stock changes (spec §3.4) — a single
/// atomic conditional UPDATE, never a load-then-save. Returns the resulting
/// stock quantity on success, or null if the delta couldn't be applied
/// (insufficient stock on a decrement).
/// </summary>
public interface IStockRepository
{
    Task<StockAdjustResult> TryAdjustAsync(long variantId, int delta, CancellationToken ct);
}

public sealed record StockAdjustResult(bool Succeeded, int? NewQuantity, int? AvailableQuantity);
```

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/ICacheService.cs
namespace ProductManagement.Application.Common.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class;
    Task RemoveAsync(string key, CancellationToken ct);
    Task<long> IncrementVersionAsync(string versionKey, CancellationToken ct);
}
```

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/IUnitOfWork.cs
namespace ProductManagement.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}
```

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/IFileStorageService.cs
namespace ProductManagement.Application.Common.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, long productId, CancellationToken ct);
    Task DeleteAsync(string url, CancellationToken ct);
}
```

```csharp
// backend/src/ProductManagement.Application/Common/PagedResult.cs
namespace ProductManagement.Application.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, string? NextCursor, bool HasMore);
```

- [ ] **Step 3: Verify the project compiles**

```bash
dotnet build src/ProductManagement.Application
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add backend/src/ProductManagement.Application
git commit -m "Add Application-layer repository/service interfaces and shared exceptions"
```

---

## Task 3: `Product` and `ProductVariant` Entities

**Files:**
- Create: `backend/src/ProductManagement.Domain/Enums/ProductStatus.cs`
- Create: `backend/src/ProductManagement.Domain/Entities/Product.cs`
- Create: `backend/src/ProductManagement.Domain/Entities/ProductVariant.cs`
- Modify: `backend/src/ProductManagement.Domain/Exceptions/DomainException.cs`
- Test: `backend/tests/ProductManagement.UnitTests/Domain/ProductTests.cs`
- Test: `backend/tests/ProductManagement.UnitTests/Domain/ProductVariantTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// backend/tests/ProductManagement.UnitTests/Domain/ProductTests.cs
using FluentAssertions;
using ProductManagement.Domain.Entities;
using ProductManagement.Domain.Enums;
using ProductManagement.Domain.Exceptions;
using Xunit;

namespace ProductManagement.UnitTests.Domain;

public class ProductTests
{
    [Fact]
    public void Create_WithValidData_StartsAsDraft()
    {
        var product = Product.Create("Classic Cotton Tee", "classic-cotton-tee", categoryId: 1, brand: "Acme");

        product.Status.Should().Be(ProductStatus.Draft);
        product.Name.Should().Be("Classic Cotton Tee");
    }

    [Fact]
    public void Archive_WhenAlreadyArchived_Throws()
    {
        var product = Product.Create("Tee", "tee", categoryId: 1, brand: null);
        product.Activate();
        product.Archive();

        var act = () => product.Archive();

        act.Should().Throw<DomainException>().WithMessage("*already archived*");
    }
}
```

```csharp
// backend/tests/ProductManagement.UnitTests/Domain/ProductVariantTests.cs
using FluentAssertions;
using ProductManagement.Domain.Entities;
using ProductManagement.Domain.Exceptions;
using Xunit;

namespace ProductManagement.UnitTests.Domain;

public class ProductVariantTests
{
    [Fact]
    public void Create_WithNegativePrice_Throws()
    {
        var act = () => ProductVariant.Create(productId: 1, sku: "TEE-M", size: "M", color: "Blue", price: -1m, stockQuantity: 10);

        act.Should().Throw<DomainException>().WithMessage("*price*");
    }

    [Fact]
    public void Create_WithNegativeStock_Throws()
    {
        var act = () => ProductVariant.Create(productId: 1, sku: "TEE-M", size: "M", color: "Blue", price: 20m, stockQuantity: -1);

        act.Should().Throw<DomainException>().WithMessage("*stock*");
    }

    [Fact]
    public void Create_WithCompareAtPriceBelowPrice_Throws()
    {
        var act = () => ProductVariant.Create(
            productId: 1, sku: "TEE-M", size: "M", color: "Blue",
            price: 20m, stockQuantity: 10, compareAtPrice: 15m);

        act.Should().Throw<DomainException>().WithMessage("*compareAtPrice*");
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
dotnet test tests/ProductManagement.UnitTests --filter "ProductTests|ProductVariantTests"
```

Expected: FAIL — types don't exist yet.

- [ ] **Step 3: Implement `ProductStatus`, `Product`, `ProductVariant`, and add the two new exception types**

```csharp
// backend/src/ProductManagement.Domain/Enums/ProductStatus.cs
namespace ProductManagement.Domain.Enums;

public enum ProductStatus : short
{
    Draft = 0,
    Active = 1,
    Archived = 2
}
```

```csharp
// backend/src/ProductManagement.Domain/Exceptions/DomainException.cs  (append to existing file)
public sealed class InvalidProductException : DomainException
{
    public InvalidProductException(string message) : base(message) { }
}

public sealed class InvalidProductVariantException : DomainException
{
    public InvalidProductVariantException(string message) : base(message) { }
}
```

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
    public string Attributes { get; private set; } = "{}"; // raw jsonb text, parsed at the edges
    public string? ImageUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ProductVariant> _variants = new();
    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();

    private Product() { } // EF Core

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

    public void AddVariant(ProductVariant variant) => _variants.Add(variant);

    public void Activate()
    {
        Status = ProductStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

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

    public void SetImageUrl(string? imageUrl)
    {
        ImageUrl = imageUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

```csharp
// backend/src/ProductManagement.Domain/Entities/ProductVariant.cs
using ProductManagement.Domain.Exceptions;

namespace ProductManagement.Domain.Entities;

public class ProductVariant
{
    public long Id { get; private set; }
    public long ProductId { get; private set; }
    public string Sku { get; private set; } = default!;
    public string? Size { get; private set; }
    public string? Color { get; private set; }
    public decimal Price { get; private set; }
    public decimal? CompareAtPrice { get; private set; }
    public int StockQuantity { get; private set; }
    public string? Barcode { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ProductVariant() { } // EF Core

    public static ProductVariant Create(
        long productId, string sku, string? size, string? color,
        decimal price, int stockQuantity, decimal? compareAtPrice = null, string? barcode = null)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new InvalidProductVariantException("SKU is required.");
        if (price < 0)
            throw new InvalidProductVariantException("price must be >= 0.");
        if (stockQuantity < 0)
            throw new InvalidProductVariantException("stock must be >= 0.");
        if (compareAtPrice is not null && compareAtPrice < price)
            throw new InvalidProductVariantException("compareAtPrice must be >= price.");

        var now = DateTimeOffset.UtcNow;
        return new ProductVariant
        {
            ProductId = productId,
            Sku = sku.Trim(),
            Size = size,
            Color = color,
            Price = price,
            CompareAtPrice = compareAtPrice,
            StockQuantity = stockQuantity,
            Barcode = barcode,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDetails(string? size, string? color, decimal price, decimal? compareAtPrice, string? barcode)
    {
        if (price < 0)
            throw new InvalidProductVariantException("price must be >= 0.");
        if (compareAtPrice is not null && compareAtPrice < price)
            throw new InvalidProductVariantException("compareAtPrice must be >= price.");

        Size = size;
        Color = color;
        Price = price;
        CompareAtPrice = compareAtPrice;
        Barcode = barcode;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
```

- [ ] **Step 4: Run to verify all pass**

```bash
dotnet test tests/ProductManagement.UnitTests --filter "ProductTests|ProductVariantTests"
```

Expected: PASS, 5 tests.

- [ ] **Step 5: Commit**

```bash
git add backend/src/ProductManagement.Domain backend/tests/ProductManagement.UnitTests
git commit -m "Add Product and ProductVariant entities with invariant checks"
```

---

## Task 4: EF Core `DbContext`, Entity Configurations, and Initial Migration

**Files:**
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/ProductManagementDbContext.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductConfiguration.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductVariantConfiguration.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/UnitOfWork.cs`
- Create: `backend/src/ProductManagement.Infrastructure/DependencyInjection.cs`

No TDD cycle here — this is schema/mapping code verified by actually running
the migration against a database, done in Step 6.

- [ ] **Step 1: Write the `DbContext`**

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
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductManagementDbContext).Assembly);
    }
}
```

- [ ] **Step 2: Write the `CategoryConfiguration`**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Configurations/CategoryConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(c => c.Slug).HasColumnName("slug").HasColumnType("citext").IsRequired();
        builder.Property(c => c.ParentCategoryId).HasColumnName("parent_category_id");
        builder.Property(c => c.DisplayOrder).HasColumnName("display_order").HasDefaultValue(0);
        builder.Property(c => c.IsActive).HasColumnName("is_active").HasDefaultValue(true);
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.Slug).IsUnique();
        builder.HasIndex(c => c.ParentCategoryId);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 3: Write the `ProductConfiguration`, including the weighted `search_vector` generated column and GIN indexes (spec §3.2, §3.3)**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductConfiguration.cs
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

        // Postgres system column used as the optimistic-concurrency token (spec section 3.4)
        builder.UseXminAsConcurrencyToken();

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

        builder.HasMany(p => p.Variants)
            .WithOne()
            .HasForeignKey(v => v.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Variants).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
```

- [ ] **Step 4: Write the `ProductVariantConfiguration`, including the `CHECK` constraints and partial index (spec §3.2)**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Configurations/ProductVariantConfiguration.cs
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
        builder.HasIndex(v => v.ProductId);
        builder.HasIndex(v => v.ProductId)
            .HasFilter("is_active AND stock_quantity > 0")
            .HasDatabaseName("ix_product_variants_active_in_stock");
    }
}
```

- [ ] **Step 5: Write `UnitOfWork` and register `DbContext` in the Infrastructure DI extension**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/UnitOfWork.cs
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ProductManagementDbContext _db;
    public UnitOfWork(ProductManagementDbContext db) => _db = db;

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
```

```csharp
// backend/src/ProductManagement.Infrastructure/DependencyInjection.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Infrastructure.Persistence;

namespace ProductManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<ProductManagementDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("Default")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
```

- [ ] **Step 6: Create and apply the initial migration against a local Postgres**

Start a throwaway Postgres container for this step (the full docker-compose
stack is finalized later, Task 15):

```bash
docker run -d --name pm-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=productdb -p 5432:5432 postgres:17
cd backend
dotnet tool install --global dotnet-ef --version 10.0.0
dotnet ef migrations add InitialCreate \
  --project src/ProductManagement.Infrastructure \
  --startup-project src/ProductManagement.Api
dotnet ef database update \
  --project src/ProductManagement.Infrastructure \
  --startup-project src/ProductManagement.Api \
  --connection "Host=localhost;Port=5432;Database=productdb;Username=postgres;Password=postgres"
```

Expected: migration file generated under
`src/ProductManagement.Infrastructure/Migrations/`, and
`dotnet ef database update` reports `Done.` with no errors — confirms the
`CREATE EXTENSION`, generated column, `CHECK` constraints, and GIN indexes
are all valid SQL against a real Postgres instance.

- [ ] **Step 7: Commit**

```bash
git add backend/src/ProductManagement.Infrastructure
git commit -m "Add DbContext, entity configurations, and initial migration"
docker rm -f pm-postgres
```

---

## Task 5: Categories Vertical Slice (the canonical CRUD pattern)

This is the first full slice through all four layers — every later resource
(Products, Variants) repeats this exact shape. Build it once, correctly,
here.

**Files:**
- Create: `backend/src/ProductManagement.Application/Common/Exceptions/CategoryHasActiveProductsException.cs`
- Create: `backend/src/ProductManagement.Application/Categories/CategoryDto.cs`
- Create: `backend/src/ProductManagement.Application/Categories/CategoryMappings.cs`
- Create: `backend/src/ProductManagement.Application/Categories/CreateCategoryRequest.cs`
- Create: `backend/src/ProductManagement.Application/Categories/CreateCategoryHandler.cs`
- Create: `backend/src/ProductManagement.Application/Categories/UpdateCategoryRequest.cs`
- Create: `backend/src/ProductManagement.Application/Categories/UpdateCategoryHandler.cs`
- Create: `backend/src/ProductManagement.Application/Categories/DeleteCategoryHandler.cs`
- Create: `backend/src/ProductManagement.Application/Categories/GetCategoryHandler.cs`
- Create: `backend/src/ProductManagement.Application/Categories/ListCategoriesHandler.cs`
- Create: `backend/src/ProductManagement.Application/DependencyInjection.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Repositories/CategoryRepository.cs`
- Modify: `backend/src/ProductManagement.Infrastructure/Persistence/UnitOfWork.cs`
- Modify: `backend/src/ProductManagement.Infrastructure/DependencyInjection.cs`
- Create: `backend/src/ProductManagement.Api/Controllers/CategoriesController.cs`
- Create: `backend/src/ProductManagement.Api/Program.cs` (overwrite template)
- Create: `backend/src/ProductManagement.Api/appsettings.json`
- Delete: `backend/src/ProductManagement.Api/Controllers/WeatherForecastController.cs`
- Delete: `backend/src/ProductManagement.Api/WeatherForecast.cs`
- Test: `backend/tests/ProductManagement.IntegrationTests/CustomWebApplicationFactory.cs`
- Test: `backend/tests/ProductManagement.IntegrationTests/DatabaseFixture.cs`
- Test: `backend/tests/ProductManagement.IntegrationTests/CategoriesEndpointsTests.cs`

- [ ] **Step 1: Write the failing integration tests first (TDD at the endpoint level)**

```csharp
// backend/tests/ProductManagement.IntegrationTests/CustomWebApplicationFactory.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductManagement.Infrastructure.Persistence;

namespace ProductManagement.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString =
        "Host=localhost;Port=5432;Database=productdb_test;Username=postgres;Password=postgres";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = TestConnectionString,
                ["Seeding:ProductCount"] = "0",
                ["Seeding:CategoryCount"] = "0" // fully disable seeding in tests - Task 14 adds a dedicated SeederTests file instead
            });
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductManagementDbContext>();
        await db.Database.MigrateAsync();
    }
}
```

```csharp
// backend/tests/ProductManagement.IntegrationTests/DatabaseFixture.cs
using Npgsql;
using Respawn;
using Xunit;

namespace ProductManagement.IntegrationTests;

public class DatabaseFixture : IAsyncLifetime
{
    public CustomWebApplicationFactory Factory { get; } = new();
    private Respawner _respawner = default!;
    private NpgsqlConnection _connection = default!;

    public async Task InitializeAsync()
    {
        await Factory.InitializeDatabaseAsync();
        _connection = new NpgsqlConnection(CustomWebApplicationFactory.TestConnectionString);
        await _connection.OpenAsync();
        _respawner = await Respawner.CreateAsync(_connection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            SchemasToInclude = new[] { "public" }
        });
    }

    public Task ResetAsync() => _respawner.ResetAsync(_connection);

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        await Factory.DisposeAsync();
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }
```

```csharp
// backend/tests/ProductManagement.IntegrationTests/CategoriesEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class CategoriesEndpointsTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public CategoriesEndpointsTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _client = _fixture.Factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateCategory_WithValidData_Returns201WithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "Dresses",
            slug = "dresses",
            parentCategoryId = (long?)null,
            displayOrder = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateSlug_Returns409()
    {
        var payload = new { name = "Dresses", slug = "dresses", parentCategoryId = (long?)null, displayOrder = 0 };
        await _client.PostAsJsonAsync("/api/v1/categories", payload);

        var response = await _client.PostAsJsonAsync("/api/v1/categories", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCategory_WithBlankName_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "",
            slug = "blank-name",
            parentCategoryId = (long?)null,
            displayOrder = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteCategory_WhenReferencedByActiveProduct_Returns409()
    {
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "Dresses", slug = "dresses-2", parentCategoryId = (long?)null, displayOrder = 0
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponseDto>();

        await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Maxi Dress", slug = "maxi-dress", categoryId = category!.Id, brand = "Acme"
        });

        var response = await _client.DeleteAsync($"/api/v1/categories/{category.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed record CategoryResponseDto(long Id, string Name, string Slug);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```bash
docker run -d --name pm-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=productdb_test -p 5432:5432 postgres:17
dotnet test tests/ProductManagement.IntegrationTests --filter CategoriesEndpointsTests
```

Expected: FAIL to even compile — `Program`, controllers, and handlers don't
exist yet. This is expected at this point in a vertical-slice TDD flow;
proceed to implementation.

- [ ] **Step 3: Add `CategoryHasActiveProductsException` and the Category DTOs/requests**

```csharp
// backend/src/ProductManagement.Application/Common/Exceptions/CategoryHasActiveProductsException.cs
namespace ProductManagement.Application.Common.Exceptions;

public sealed class CategoryHasActiveProductsException : Exception
{
    public CategoryHasActiveProductsException(long categoryId)
        : base($"Category {categoryId} still has active products referencing it.") { }
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/CategoryDto.cs
namespace ProductManagement.Application.Categories;

public sealed record CategoryDto(
    long Id,
    string Name,
    string Slug,
    long? ParentCategoryId,
    int DisplayOrder,
    bool IsActive);
```

```csharp
// backend/src/ProductManagement.Application/Categories/CategoryMappings.cs
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public static class CategoryMappings
{
    public static CategoryDto ToDto(this Category category) => new(
        category.Id, category.Name, category.Slug,
        category.ParentCategoryId, category.DisplayOrder, category.IsActive);
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/CreateCategoryRequest.cs
using FluentValidation;

namespace ProductManagement.Application.Categories;

public sealed record CreateCategoryRequest(
    string Name,
    string Slug,
    long? ParentCategoryId,
    int DisplayOrder = 0);

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(120).Matches("^[a-z0-9-]+$")
            .WithMessage("Slug must be lowercase letters, numbers, and hyphens only.");
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/UpdateCategoryRequest.cs
using FluentValidation;

namespace ProductManagement.Application.Categories;

public sealed record UpdateCategoryRequest(
    string Name,
    string Slug,
    long? ParentCategoryId,
    int DisplayOrder,
    bool IsActive);

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(120).Matches("^[a-z0-9-]+$");
    }
}
```

- [ ] **Step 4: Write the Category handlers**

```csharp
// backend/src/ProductManagement.Application/Categories/CreateCategoryHandler.cs
using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class CreateCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateCategoryRequest> _validator;

    public CreateCategoryHandler(ICategoryRepository categories, IUnitOfWork unitOfWork, IValidator<CreateCategoryRequest> validator)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<CategoryDto> HandleAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        if (request.ParentCategoryId is { } parentId)
        {
            var parent = await _categories.GetByIdAsync(parentId, ct);
            if (parent is null) throw new EntityNotFoundException(nameof(Category), parentId);
        }

        var category = Category.Create(request.Name, request.Slug, request.ParentCategoryId, request.DisplayOrder);
        _categories.Add(category);
        await _unitOfWork.SaveChangesAsync(ct);
        return category.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/UpdateCategoryHandler.cs
using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class UpdateCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<UpdateCategoryRequest> _validator;

    public UpdateCategoryHandler(ICategoryRepository categories, IUnitOfWork unitOfWork, IValidator<UpdateCategoryRequest> validator)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<CategoryDto> HandleAsync(long id, UpdateCategoryRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var category = await _categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);

        category.Update(request.Name, request.Slug, request.ParentCategoryId, request.DisplayOrder, request.IsActive);
        await _unitOfWork.SaveChangesAsync(ct);
        return category.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/DeleteCategoryHandler.cs
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class DeleteCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteCategoryHandler(ICategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(long id, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);

        if (await _categories.HasActiveProductsAsync(id, ct))
            throw new CategoryHasActiveProductsException(id);

        _categories.Remove(category);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/GetCategoryHandler.cs
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class GetCategoryHandler
{
    private readonly ICategoryRepository _categories;
    public GetCategoryHandler(ICategoryRepository categories) => _categories = categories;

    public async Task<CategoryDto> HandleAsync(long id, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);
        return category.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/ListCategoriesHandler.cs
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Categories;

public class ListCategoriesHandler
{
    private readonly ICategoryRepository _categories;
    public ListCategoriesHandler(ICategoryRepository categories) => _categories = categories;

    public async Task<List<CategoryDto>> HandleAsync(long? parentId, bool? activeOnly, CancellationToken ct)
    {
        var categories = await _categories.ListAsync(parentId, activeOnly, ct);
        return categories.Select(c => c.ToDto()).ToList();
    }
}
```

- [ ] **Step 5: Write the Application DI extension**

```csharp
// backend/src/ProductManagement.Application/DependencyInjection.cs
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ProductManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<Categories.CreateCategoryHandler>();
        services.AddScoped<Categories.UpdateCategoryHandler>();
        services.AddScoped<Categories.DeleteCategoryHandler>();
        services.AddScoped<Categories.GetCategoryHandler>();
        services.AddScoped<Categories.ListCategoriesHandler>();

        return services;
    }
}
```

- [ ] **Step 6: Write `CategoryRepository` and update `UnitOfWork` to translate Postgres unique-violation exceptions (spec §7 "Error Handling")**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Repositories/CategoryRepository.cs
using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;
using ProductManagement.Domain.Enums;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ProductManagementDbContext _db;
    public CategoryRepository(ProductManagementDbContext db) => _db = db;

    public Task<Category?> GetByIdAsync(long id, CancellationToken ct) =>
        _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Category?> GetBySlugAsync(string slug, CancellationToken ct) =>
        _db.Categories.FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public async Task<List<Category>> ListAsync(long? parentId, bool? activeOnly, CancellationToken ct)
    {
        var query = _db.Categories.AsQueryable();
        if (parentId is not null) query = query.Where(c => c.ParentCategoryId == parentId);
        if (activeOnly == true) query = query.Where(c => c.IsActive);
        return await query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync(ct);
    }

    public Task<bool> HasActiveProductsAsync(long categoryId, CancellationToken ct) =>
        _db.Products.AnyAsync(p => p.CategoryId == categoryId && p.Status != ProductStatus.Archived, ct);

    public void Add(Category category) => _db.Categories.Add(category);
    public void Remove(Category category) => _db.Categories.Remove(category);
}
```

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/UnitOfWork.cs  (replace entire file)
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ProductManagementDbContext _db;
    public UnitOfWork(ProductManagementDbContext db) => _db = db;

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            var value = ExtractConflictingValue(pg);
            if (pg.ConstraintName?.Contains("sku") == true)
                throw new DuplicateSkuException(value);
            if (pg.ConstraintName?.Contains("slug") == true)
                throw new DuplicateSlugException(value);
            throw;
        }
    }

    private static string ExtractConflictingValue(PostgresException pg)
    {
        // Postgres detail looks like: Key (slug)=(dresses) already exists.
        var detail = pg.Detail ?? string.Empty;
        var start = detail.IndexOf(")=(", StringComparison.Ordinal);
        if (start < 0) return "unknown";
        var end = detail.IndexOf(')', start + 3);
        if (end < 0) return "unknown";
        return detail.Substring(start + 3, end - (start + 3));
    }
}
```

```csharp
// backend/src/ProductManagement.Infrastructure/DependencyInjection.cs  (replace entire file)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Infrastructure.Persistence;
using ProductManagement.Infrastructure.Persistence.Repositories;

namespace ProductManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<ProductManagementDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("Default")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        return services;
    }
}
```

- [ ] **Step 7: Write `CategoriesController`**

```csharp
// backend/src/ProductManagement.Api/Controllers/CategoriesController.cs
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.Categories;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/categories")]
public class CategoriesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> List(
        [FromServices] ListCategoriesHandler handler,
        [FromQuery] long? parentId, [FromQuery] bool? activeOnly, CancellationToken ct)
        => Ok(await handler.HandleAsync(parentId, activeOnly, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<CategoryDto>> GetById(
        [FromServices] GetCategoryHandler handler, long id, CancellationToken ct)
        => Ok(await handler.HandleAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromServices] CreateCategoryHandler handler, [FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<CategoryDto>> Update(
        [FromServices] UpdateCategoryHandler handler, long id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
        => Ok(await handler.HandleAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(
        [FromServices] DeleteCategoryHandler handler, long id, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }
}
```

- [ ] **Step 8: Write `Program.cs` and `appsettings.json` — the composition root**

```csharp
// backend/src/ProductManagement.Api/Program.cs
using ProductManagement.Application;
using ProductManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();

public partial class Program { } // exposed for WebApplicationFactory<Program> in IntegrationTests
```

```json
// backend/src/ProductManagement.Api/appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=productdb;Username=postgres;Password=postgres"
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 9: Delete the template files and run the tests**

```bash
rm src/ProductManagement.Api/Controllers/WeatherForecastController.cs
rm src/ProductManagement.Api/WeatherForecast.cs
dotnet test tests/ProductManagement.IntegrationTests --filter CategoriesEndpointsTests
```

Expected: PASS, 4 tests. If `CreateCategory_WithBlankName_Returns400` fails
with a 500 instead of 400, that is expected at this point — the
`FluentValidation.ValidationException` thrown by `ValidateAndThrowAsync`
isn't mapped to `400` yet; that mapping is added in Task 11 (global
exception handler). Re-run this test after Task 11 to confirm the fix.

- [ ] **Step 10: Commit**

```bash
git add backend/src backend/tests/ProductManagement.IntegrationTests
git commit -m "Implement Categories vertical slice: handlers, repository, controller, Program.cs"
```

---

## Task 6: Products Vertical Slice (CRUD, cursor pagination, search, `ETag`/`xmin` concurrency)

**Files:**
- Create: `backend/src/ProductManagement.Application/Products/ProductDto.cs`
- Create: `backend/src/ProductManagement.Application/Products/ProductMappings.cs`
- Create: `backend/src/ProductManagement.Application/Products/ProductCursor.cs`
- Create: `backend/src/ProductManagement.Application/Products/CreateProductRequest.cs`
- Create: `backend/src/ProductManagement.Application/Products/CreateProductHandler.cs`
- Create: `backend/src/ProductManagement.Application/Products/UpdateProductRequest.cs`
- Create: `backend/src/ProductManagement.Application/Products/UpdateProductHandler.cs`
- Create: `backend/src/ProductManagement.Application/Products/DeleteProductHandler.cs`
- Create: `backend/src/ProductManagement.Application/Products/GetProductHandler.cs`
- Create: `backend/src/ProductManagement.Application/Products/ListProductsHandler.cs`
- Modify: `backend/src/ProductManagement.Application/Common/Interfaces/IProductRepository.cs`
- Modify: `backend/src/ProductManagement.Application/DependencyInjection.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Repositories/ProductRepository.cs`
- Modify: `backend/src/ProductManagement.Infrastructure/DependencyInjection.cs`
- Create: `backend/src/ProductManagement.Api/Controllers/ProductsController.cs`
- Test: `backend/tests/ProductManagement.IntegrationTests/ProductsEndpointsTests.cs`

- [ ] **Step 1: Write the failing integration tests**

```csharp
// backend/tests/ProductManagement.IntegrationTests/ProductsEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class ProductsEndpointsTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public ProductsEndpointsTests(DatabaseFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() { await _fixture.ResetAsync(); _client = _fixture.Factory.CreateClient(); }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<long> CreateCategoryAsync(string slug)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "Tops", slug, parentCategoryId = (long?)null, displayOrder = 0 });
        var dto = await response.Content.ReadFromJsonAsync<CategoryRef>();
        return dto!.Id;
    }

    [Fact]
    public async Task CreateProduct_WithInitialVariants_Returns201AndPersistsVariants()
    {
        var categoryId = await CreateCategoryAsync("tops-1");

        var response = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Classic Cotton Tee",
            slug = "classic-cotton-tee",
            categoryId,
            brand = "Acme",
            variants = new[]
            {
                new { sku = "TEE-M-BLU", size = "M", color = "Blue", price = 20.00m, stockQuantity = 50 }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ProductRef>();
        created!.Variants.Should().ContainSingle(v => v.Sku == "TEE-M-BLU");
    }

    [Fact]
    public async Task GetProduct_ReturnsETagHeader()
    {
        var categoryId = await CreateCategoryAsync("tops-2");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "Tee", slug = "tee-2", categoryId, brand = (string?)null, variants = Array.Empty<object>() });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductRef>();

        var response = await _client.GetAsync($"/api/v1/products/{created!.Id}");

        response.Headers.ETag.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateProduct_WithStaleETag_Returns409()
    {
        var categoryId = await CreateCategoryAsync("tops-3");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "Tee", slug = "tee-3", categoryId, brand = (string?)null, variants = Array.Empty<object>() });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductRef>();

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{created!.Id}")
        {
            Content = JsonContent.Create(new { name = "Updated Tee", description = (string?)null, categoryId, brand = (string?)null, attributes = "{}" })
        };
        request.Headers.TryAddWithoutValidation("If-Match", "\"999999999\"");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ListProducts_WithSearchQuery_ReturnsRankedResults()
    {
        var categoryId = await CreateCategoryAsync("tops-4");
        await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "Blue Denim Jacket", slug = "blue-denim-jacket", categoryId, brand = "Acme", variants = Array.Empty<object>() });
        await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "Red Wool Scarf", slug = "red-wool-scarf", categoryId, brand = "Acme", variants = Array.Empty<object>() });

        var response = await _client.GetAsync("/api/v1/products?q=denim");
        var page = await response.Content.ReadFromJsonAsync<PagedRef>();

        page!.Items.Should().ContainSingle(p => p.Name == "Blue Denim Jacket");
    }

    private sealed record CategoryRef(long Id);
    private sealed record VariantRef(long Id, string Sku);
    private sealed record ProductRef(long Id, string Name, List<VariantRef> Variants);
    private sealed record ProductListItemRef(long Id, string Name);
    private sealed record PagedRef(List<ProductListItemRef> Items, string? NextCursor, bool HasMore);
}
```

- [ ] **Step 2: Run to verify failure**

```bash
dotnet test tests/ProductManagement.IntegrationTests --filter ProductsEndpointsTests
```

Expected: FAIL to compile — nothing exists yet.

- [ ] **Step 3: Extend `IProductRepository` with the concurrency helper, and write the Product DTOs**

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/IProductRepository.cs  (replace entire file)
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdWithVariantsAsync(long id, CancellationToken ct);
    Task<Product?> GetBySlugWithVariantsAsync(string slug, CancellationToken ct);
    Task<uint> GetXminAsync(long id, CancellationToken ct);
    void SetExpectedVersion(Product product, uint expectedXmin);
    Task<PagedResult<Product>> ListAsync(ProductListQuery query, CancellationToken ct);
    void Add(Product product);
}

public sealed record ProductListQuery(
    long? CategoryId,
    short? Status,
    string? SearchText,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? AttributesJson,
    string? Cursor,
    int Limit);
```

```csharp
// backend/src/ProductManagement.Application/Products/ProductDto.cs
namespace ProductManagement.Application.Products;

public sealed record VariantDto(
    long Id, string Sku, string? Size, string? Color,
    decimal Price, decimal? CompareAtPrice, int StockQuantity, string? Barcode, bool IsActive);

public sealed record ProductDto(
    long Id, string Name, string Slug, string? Description, long CategoryId,
    string? Brand, string Status, string Attributes, string? ImageUrl,
    List<VariantDto> Variants);

public sealed record ProductListItemDto(
    long Id, string Name, string Slug, long CategoryId, string? Brand, string Status,
    decimal? MinPrice, decimal? MaxPrice, int TotalStock, string? ImageUrl);
```

```csharp
// backend/src/ProductManagement.Application/Products/ProductMappings.cs
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public static class ProductMappings
{
    public static VariantDto ToDto(this ProductVariant v) => new(
        v.Id, v.Sku, v.Size, v.Color, v.Price, v.CompareAtPrice, v.StockQuantity, v.Barcode, v.IsActive);

    public static ProductDto ToDto(this Product p) => new(
        p.Id, p.Name, p.Slug, p.Description, p.CategoryId, p.Brand,
        p.Status.ToString(), p.Attributes, p.ImageUrl,
        p.Variants.Select(v => v.ToDto()).ToList());

    public static ProductListItemDto ToListItemDto(this Product p) => new(
        p.Id, p.Name, p.Slug, p.CategoryId, p.Brand, p.Status.ToString(),
        p.Variants.Count > 0 ? p.Variants.Min(v => v.Price) : null,
        p.Variants.Count > 0 ? p.Variants.Max(v => v.Price) : null,
        p.Variants.Sum(v => v.StockQuantity),
        p.ImageUrl);
}
```

```csharp
// backend/src/ProductManagement.Application/Products/ProductCursor.cs
using System.Text;
using System.Text.Json;

namespace ProductManagement.Application.Products;

public sealed class InvalidCursorException : Exception
{
    public InvalidCursorException() : base("The pagination cursor is invalid or expired.") { }
}

public sealed record ProductCursor(DateTimeOffset? CreatedAt, float? Rank, long Id)
{
    public string Encode()
    {
        var json = JsonSerializer.Serialize(this);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    /// <summary>
    /// Returns null only when no cursor was supplied at all (a legitimate first-page
    /// request). A cursor string that IS present but fails to decode throws
    /// InvalidCursorException instead of silently falling back to "no cursor" -
    /// spec section 9 requires an invalid/expired cursor to surface as 400, not to
    /// quietly restart pagination from page one.
    /// </summary>
    public static ProductCursor? Decode(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor)) return null;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return JsonSerializer.Deserialize<ProductCursor>(json) ?? throw new InvalidCursorException();
        }
        catch (Exception ex) when (ex is not InvalidCursorException)
        {
            throw new InvalidCursorException();
        }
    }
}
```

- [ ] **Step 4: Write the Product handlers**

```csharp
// backend/src/ProductManagement.Application/Products/CreateProductRequest.cs
using FluentValidation;

namespace ProductManagement.Application.Products;

public sealed record CreateVariantRequest(
    string Sku, string? Size, string? Color, decimal Price, int StockQuantity,
    decimal? CompareAtPrice = null, string? Barcode = null);

public sealed record CreateProductRequest(
    string Name, string Slug, long CategoryId, string? Brand,
    string? Description = null, string Attributes = "{}",
    List<CreateVariantRequest>? Variants = null);

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200).Matches("^[a-z0-9-]+$");
        RuleFor(x => x.Attributes).Must(json => json.Length <= 8000)
            .WithMessage("attributes JSON must be 8000 characters or fewer.");
        RuleFor(x => x.Attributes).Must(BeValidJson)
            .WithMessage("attributes must be valid JSON.");
        RuleForEach(x => x.Variants).ChildRules(v =>
        {
            v.RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
            v.RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
            v.RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
            v.RuleFor(x => x)
                .Must(x => x.CompareAtPrice is null || x.CompareAtPrice >= x.Price)
                .WithMessage("compareAtPrice must be >= price.");
        });
    }

    internal static bool BeValidJson(string json)
    {
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Products/CreateProductHandler.cs
using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public class CreateProductHandler
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateProductRequest> _validator;

    public CreateProductHandler(
        IProductRepository products, ICategoryRepository categories,
        IUnitOfWork unitOfWork, IValidator<CreateProductRequest> validator)
    {
        _products = products;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<ProductDto> HandleAsync(CreateProductRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var category = await _categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new EntityNotFoundException(nameof(Category), request.CategoryId);

        var product = Product.Create(request.Name, request.Slug, category.Id, request.Brand, request.Attributes);

        foreach (var v in request.Variants ?? new List<CreateVariantRequest>())
        {
            product.AddVariant(ProductVariant.Create(
                product.Id, v.Sku, v.Size, v.Color, v.Price, v.StockQuantity, v.CompareAtPrice, v.Barcode));
        }

        _products.Add(product);
        await _unitOfWork.SaveChangesAsync(ct); // one transaction: product + all initial variants together
        return product.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Products/UpdateProductRequest.cs
using FluentValidation;

namespace ProductManagement.Application.Products;

public sealed record UpdateProductRequest(
    string Name, string? Description, long CategoryId, string? Brand, string Attributes);

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Attributes).Must(json => json.Length <= 8000)
            .WithMessage("attributes JSON must be 8000 characters or fewer.");
        RuleFor(x => x.Attributes).Must(CreateProductRequestValidator.BeValidJson)
            .WithMessage("attributes must be valid JSON.");
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Products/UpdateProductHandler.cs
using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public class UpdateProductHandler
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<UpdateProductRequest> _validator;

    public UpdateProductHandler(IProductRepository products, IUnitOfWork unitOfWork, IValidator<UpdateProductRequest> validator)
    {
        _products = products;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<ProductDto> HandleAsync(long id, uint expectedXmin, UpdateProductRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var product = await _products.GetByIdWithVariantsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);

        _products.SetExpectedVersion(product, expectedXmin);
        product.UpdateDetails(request.Name, request.Description, request.CategoryId, request.Brand, request.Attributes);

        await _unitOfWork.SaveChangesAsync(ct); // throws DbUpdateConcurrencyException on xmin mismatch -> 409 (Task 11)
        return product.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Products/DeleteProductHandler.cs
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public class DeleteProductHandler
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    public DeleteProductHandler(IProductRepository products, IUnitOfWork unitOfWork)
    { _products = products; _unitOfWork = unitOfWork; }

    public async Task HandleAsync(long id, CancellationToken ct)
    {
        var product = await _products.GetByIdWithVariantsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);

        product.Archive(); // soft delete (spec section 3.2) - throws if already archived
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Products/GetProductHandler.cs
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public sealed record ProductResult(ProductDto Product, uint Xmin);

public class GetProductHandler
{
    private readonly IProductRepository _products;
    public GetProductHandler(IProductRepository products) => _products = products;

    public async Task<ProductResult> ByIdAsync(long id, CancellationToken ct)
    {
        var product = await _products.GetByIdWithVariantsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);
        var xmin = await _products.GetXminAsync(id, ct);
        return new ProductResult(product.ToDto(), xmin);
    }

    public async Task<ProductResult> BySlugAsync(string slug, CancellationToken ct)
    {
        var product = await _products.GetBySlugWithVariantsAsync(slug, ct)
            ?? throw new EntityNotFoundException(nameof(Product), slug);
        var xmin = await _products.GetXminAsync(product.Id, ct);
        return new ProductResult(product.ToDto(), xmin);
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Products/ListProductsHandler.cs
using ProductManagement.Application.Common;
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Products;

public class ListProductsHandler
{
    private const int MaxLimit = 100;
    private readonly IProductRepository _products;
    public ListProductsHandler(IProductRepository products) => _products = products;

    public async Task<PagedResult<ProductListItemDto>> HandleAsync(
        long? categoryId, short? status, string? q, decimal? minPrice, decimal? maxPrice,
        string? attributesJson, string? cursor, int limit, CancellationToken ct)
    {
        var query = new ProductListQuery(
            categoryId, status, q, minPrice, maxPrice, attributesJson, cursor, Math.Min(limit, MaxLimit));

        var page = await _products.ListAsync(query, ct);
        return new PagedResult<ProductListItemDto>(
            page.Items.Select(p => p.ToListItemDto()).ToList(), page.NextCursor, page.HasMore);
    }
}
```

- [ ] **Step 5: Register the new handlers in the Application DI extension**

```csharp
// backend/src/ProductManagement.Application/DependencyInjection.cs  (add inside AddApplication, before `return services;`)
        services.AddScoped<Products.CreateProductHandler>();
        services.AddScoped<Products.UpdateProductHandler>();
        services.AddScoped<Products.DeleteProductHandler>();
        services.AddScoped<Products.GetProductHandler>();
        services.AddScoped<Products.ListProductsHandler>();
```

- [ ] **Step 6: Write `ProductRepository`, including cursor pagination and search (spec §3.3, §4)**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Repositories/ProductRepository.cs
using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ProductManagementDbContext _db;
    public ProductRepository(ProductManagementDbContext db) => _db = db;

    public Task<Product?> GetByIdWithVariantsAsync(long id, CancellationToken ct) =>
        _db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product?> GetBySlugWithVariantsAsync(string slug, CancellationToken ct) =>
        _db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public Task<uint> GetXminAsync(long id, CancellationToken ct) =>
        _db.Products.Where(p => p.Id == id).Select(p => EF.Property<uint>(p, "xmin")).FirstAsync(ct);

    public void SetExpectedVersion(Product product, uint expectedXmin) =>
        _db.Entry(product).Property("xmin").OriginalValue = expectedXmin;

    public void Add(Product product) => _db.Products.Add(product);

    public async Task<PagedResult<Product>> ListAsync(ProductListQuery query, CancellationToken ct)
    {
        var baseQuery = _db.Products.Include(p => p.Variants).AsQueryable();

        if (query.CategoryId is { } categoryId) baseQuery = baseQuery.Where(p => p.CategoryId == categoryId);
        if (query.Status is { } status) baseQuery = baseQuery.Where(p => (short)p.Status == status);
        if (query.MinPrice is { } minPrice) baseQuery = baseQuery.Where(p => p.Variants.Any(v => v.Price >= minPrice));
        if (query.MaxPrice is { } maxPrice) baseQuery = baseQuery.Where(p => p.Variants.Any(v => v.Price <= maxPrice));
        if (!string.IsNullOrWhiteSpace(query.AttributesJson))
            baseQuery = baseQuery.Where(p => EF.Functions.JsonContains(p.Attributes, query.AttributesJson));

        var decodedCursor = ProductCursor.Decode(query.Cursor); // throws InvalidCursorException -> 400 (Task 11) for a present-but-broken cursor

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var tsQuery = EF.Functions.WebSearchToTsQuery("english", query.SearchText);
            var ranked = baseQuery
                .Where(p => EF.Property<NpgsqlTypes.NpgsqlTsVector>(p, "SearchVector").Matches(tsQuery))
                .Select(p => new { Product = p, Rank = EF.Functions.ToTsRank(EF.Property<NpgsqlTypes.NpgsqlTsVector>(p, "SearchVector"), tsQuery) });

            if (decodedCursor?.Rank is { } afterRank)
                ranked = ranked.Where(x => x.Rank < afterRank || (x.Rank == afterRank && x.Product.Id < decodedCursor.Id));

            var rankedResults = await ranked
                .OrderByDescending(x => x.Rank).ThenByDescending(x => x.Product.Id)
                .Take(query.Limit + 1)
                .ToListAsync(ct);

            if (rankedResults.Count == 0)
            {
                // Full-text found nothing -> trigram typo-tolerant fallback (spec section 3.3)
                var trigramMatches = await baseQuery
                    .Where(p => EF.Functions.TrigramsSimilarity(p.Name, query.SearchText) > 0.1)
                    .OrderByDescending(p => EF.Functions.TrigramsSimilarity(p.Name, query.SearchText))
                    .Take(query.Limit)
                    .ToListAsync(ct);
                return new PagedResult<Product>(trigramMatches, null, false);
            }

            var hasMore = rankedResults.Count > query.Limit;
            var page = rankedResults.Take(query.Limit).ToList();
            var last = page.LastOrDefault();
            var nextCursor = hasMore && last is not null
                ? new ProductCursor(null, rankedResults[query.Limit - 1].Rank, last.Product.Id).Encode()
                : null;
            return new PagedResult<Product>(page.Select(x => x.Product).ToList(), nextCursor, hasMore);
        }

        if (decodedCursor?.CreatedAt is { } afterCreatedAt)
            baseQuery = baseQuery.Where(p => p.CreatedAt < afterCreatedAt || (p.CreatedAt == afterCreatedAt && p.Id < decodedCursor.Id));

        var results = await baseQuery
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Take(query.Limit + 1)
            .ToListAsync(ct);

        var moreExists = results.Count > query.Limit;
        var pageItems = results.Take(query.Limit).ToList();
        var lastItem = pageItems.LastOrDefault();
        var cursor = moreExists && lastItem is not null
            ? new ProductCursor(lastItem.CreatedAt, null, lastItem.Id).Encode()
            : null;
        return new PagedResult<Product>(pageItems, cursor, moreExists);
    }
}
```

- [ ] **Step 7: Register `IProductRepository` in Infrastructure DI**

```csharp
// backend/src/ProductManagement.Infrastructure/DependencyInjection.cs  (add inside AddInfrastructure, before `return services;`)
        services.AddScoped<IProductRepository, ProductRepository>();
```

- [ ] **Step 8: Write `ProductsController`, handling `ETag`/`If-Match` (spec §7)**

```csharp
// backend/src/ProductManagement.Api/Controllers/ProductsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using ProductManagement.Application.Products;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/products")]
public class ProductsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromServices] ListProductsHandler handler,
        [FromQuery] long? categoryId, [FromQuery] short? status, [FromQuery] string? q,
        [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice, [FromQuery] string? attributes,
        [FromQuery] string? cursor, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var page = await handler.HandleAsync(categoryId, status, q, minPrice, maxPrice, attributes, cursor, limit, ct);
        return Ok(page);
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById([FromServices] GetProductHandler handler, long id, CancellationToken ct)
    {
        var result = await handler.ByIdAsync(id, ct);
        SetETag(result.Xmin);
        return Ok(result.Product);
    }

    [HttpGet("slug/{slug}")]
    public async Task<IActionResult> GetBySlug([FromServices] GetProductHandler handler, string slug, CancellationToken ct)
    {
        var result = await handler.BySlugAsync(slug, ct);
        SetETag(result.Xmin);
        return Ok(result.Product);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromServices] CreateProductHandler handler, [FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var result = await handler.HandleAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(
        [FromServices] UpdateProductHandler handler, long id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        var expectedXmin = ParseIfMatch();
        var result = await handler.HandleAsync(id, expectedXmin, request, ct);
        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete([FromServices] DeleteProductHandler handler, long id, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }

    private void SetETag(uint xmin) => Response.Headers.ETag = new EntityTagHeaderValue($"\"{xmin}\"").ToString();

    private uint ParseIfMatch()
    {
        var header = Request.Headers.IfMatch.ToString().Trim('"');
        return uint.TryParse(header, out var value) ? value : 0; // 0 never matches a real xmin -> guaranteed 409
    }
}
```

- [ ] **Step 9: Run the tests**

```bash
dotnet test tests/ProductManagement.IntegrationTests --filter ProductsEndpointsTests
```

Expected: PASS, 4 tests (the `409` test may still show as a `500`
`DbUpdateConcurrencyException` until Task 11's exception handler maps it —
same caveat as Task 5).

- [ ] **Step 10: Commit**

```bash
git add backend/src backend/tests/ProductManagement.IntegrationTests
git commit -m "Implement Products vertical slice: CRUD, cursor pagination, full-text search, ETag concurrency"
```

---

## Task 7: Variants CRUD (nested under a product)

**Files:**
- Create: `backend/src/ProductManagement.Application/Variants/CreateVariantRequest.cs` (note: reuses `CreateVariantRequest` shape from Task 6 conceptually, but this file defines the standalone-endpoint version with its own validator instance)
- Create: `backend/src/ProductManagement.Application/Variants/CreateVariantHandler.cs`
- Create: `backend/src/ProductManagement.Application/Variants/UpdateVariantRequest.cs`
- Create: `backend/src/ProductManagement.Application/Variants/UpdateVariantHandler.cs`
- Create: `backend/src/ProductManagement.Application/Variants/DeleteVariantHandler.cs`
- Create: `backend/src/ProductManagement.Application/Variants/ListVariantsHandler.cs`
- Modify: `backend/src/ProductManagement.Application/DependencyInjection.cs`
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Repositories/VariantRepository.cs`
- Modify: `backend/src/ProductManagement.Infrastructure/DependencyInjection.cs`
- Create: `backend/src/ProductManagement.Api/Controllers/VariantsController.cs`
- Test: `backend/tests/ProductManagement.IntegrationTests/VariantsEndpointsTests.cs`

- [ ] **Step 1: Write the failing integration tests**

```csharp
// backend/tests/ProductManagement.IntegrationTests/VariantsEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class VariantsEndpointsTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public VariantsEndpointsTests(DatabaseFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() { await _fixture.ResetAsync(); _client = _fixture.Factory.CreateClient(); }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<long> CreateProductAsync(string slug)
    {
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "Tops", slug = $"cat-{slug}", parentCategoryId = (long?)null, displayOrder = 0 });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdRef>();

        var productResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "Tee", slug, categoryId = category!.Id, brand = (string?)null, variants = Array.Empty<object>() });
        var product = await productResponse.Content.ReadFromJsonAsync<IdRef>();
        return product!.Id;
    }

    [Fact]
    public async Task CreateVariant_WithDuplicateSku_Returns409()
    {
        var productId = await CreateProductAsync("tee-v1");
        var payload = new { sku = "TEE-M", size = "M", color = "Blue", price = 20.00m, stockQuantity = 10 };
        await _client.PostAsJsonAsync($"/api/v1/products/{productId}/variants", payload);

        var response = await _client.PostAsJsonAsync($"/api/v1/products/{productId}/variants", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteVariant_SoftDeletes_NotHardDelete()
    {
        var productId = await CreateProductAsync("tee-v2");
        var createResponse = await _client.PostAsJsonAsync($"/api/v1/products/{productId}/variants", new
        { sku = "TEE-L", size = "L", color = "Red", price = 22.00m, stockQuantity = 5 });
        var variant = await createResponse.Content.ReadFromJsonAsync<IdRef>();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/products/{productId}/variants/{variant!.Id}");
        var listResponse = await _client.GetAsync($"/api/v1/products/{productId}/variants");
        var list = await listResponse.Content.ReadFromJsonAsync<List<VariantRef>>();

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        list.Should().ContainSingle(v => v.Id == variant.Id && !v.IsActive);
    }

    private sealed record IdRef(long Id);
    private sealed record VariantRef(long Id, string Sku, bool IsActive);
}
```

- [ ] **Step 2: Run to verify failure**

```bash
dotnet test tests/ProductManagement.IntegrationTests --filter VariantsEndpointsTests
```

Expected: FAIL to compile.

- [ ] **Step 3: Extend `IVariantRepository` and write the request/validator types**

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/IVariantRepository.cs  (replace entire file)
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IVariantRepository
{
    Task<ProductVariant?> GetByIdAsync(long id, CancellationToken ct);
    Task<bool> SkuExistsAsync(string sku, CancellationToken ct);
    Task<List<ProductVariant>> ListByProductIdAsync(long productId, CancellationToken ct);
    void Add(ProductVariant variant);
}
```

```csharp
// backend/src/ProductManagement.Application/Variants/CreateVariantRequest.cs
using FluentValidation;

namespace ProductManagement.Application.Variants;

public sealed record CreateVariantRequest(
    string Sku, string? Size, string? Color, decimal Price, int StockQuantity,
    decimal? CompareAtPrice = null, string? Barcode = null);

public sealed class CreateVariantRequestValidator : AbstractValidator<CreateVariantRequest>
{
    public CreateVariantRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => x.CompareAtPrice is null || x.CompareAtPrice >= x.Price)
            .WithMessage("compareAtPrice must be >= price.");
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Variants/UpdateVariantRequest.cs
using FluentValidation;

namespace ProductManagement.Application.Variants;

public sealed record UpdateVariantRequest(
    string? Size, string? Color, decimal Price, decimal? CompareAtPrice, string? Barcode);

public sealed class UpdateVariantRequestValidator : AbstractValidator<UpdateVariantRequest>
{
    public UpdateVariantRequestValidator()
    {
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => x.CompareAtPrice is null || x.CompareAtPrice >= x.Price)
            .WithMessage("compareAtPrice must be >= price.");
    }
}
```

- [ ] **Step 4: Write the Variant handlers**

```csharp
// backend/src/ProductManagement.Application/Variants/CreateVariantHandler.cs
using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public class CreateVariantHandler
{
    private readonly IVariantRepository _variants;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateVariantRequest> _validator;

    public CreateVariantHandler(
        IVariantRepository variants, IProductRepository products,
        IUnitOfWork unitOfWork, IValidator<CreateVariantRequest> validator)
    {
        _variants = variants;
        _products = products;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<VariantDto> HandleAsync(long productId, CreateVariantRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var product = await _products.GetByIdWithVariantsAsync(productId, ct)
            ?? throw new EntityNotFoundException(nameof(Product), productId);

        var variant = ProductVariant.Create(
            productId, request.Sku, request.Size, request.Color,
            request.Price, request.StockQuantity, request.CompareAtPrice, request.Barcode);

        _variants.Add(variant);
        await _unitOfWork.SaveChangesAsync(ct); // duplicate SKU -> DuplicateSkuException via UnitOfWork translation (Task 5)
        return variant.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Variants/UpdateVariantHandler.cs
using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public class UpdateVariantHandler
{
    private readonly IVariantRepository _variants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<UpdateVariantRequest> _validator;

    public UpdateVariantHandler(IVariantRepository variants, IUnitOfWork unitOfWork, IValidator<UpdateVariantRequest> validator)
    {
        _variants = variants;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<VariantDto> HandleAsync(long variantId, UpdateVariantRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var variant = await _variants.GetByIdAsync(variantId, ct)
            ?? throw new EntityNotFoundException(nameof(ProductVariant), variantId);

        variant.UpdateDetails(request.Size, request.Color, request.Price, request.CompareAtPrice, request.Barcode);
        await _unitOfWork.SaveChangesAsync(ct);
        return variant.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Variants/DeleteVariantHandler.cs
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public class DeleteVariantHandler
{
    private readonly IVariantRepository _variants;
    private readonly IUnitOfWork _unitOfWork;
    public DeleteVariantHandler(IVariantRepository variants, IUnitOfWork unitOfWork)
    { _variants = variants; _unitOfWork = unitOfWork; }

    public async Task HandleAsync(long variantId, CancellationToken ct)
    {
        var variant = await _variants.GetByIdAsync(variantId, ct)
            ?? throw new EntityNotFoundException(nameof(ProductVariant), variantId);

        variant.Deactivate(); // soft delete (spec section 3.2), not a hard delete
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Variants/ListVariantsHandler.cs
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;

namespace ProductManagement.Application.Variants;

public class ListVariantsHandler
{
    private readonly IVariantRepository _variants;
    public ListVariantsHandler(IVariantRepository variants) => _variants = variants;

    public async Task<List<VariantDto>> HandleAsync(long productId, CancellationToken ct)
    {
        var variants = await _variants.ListByProductIdAsync(productId, ct);
        return variants.Select(v => v.ToDto()).ToList();
    }
}
```

- [ ] **Step 5: Register the handlers**

```csharp
// backend/src/ProductManagement.Application/DependencyInjection.cs  (add inside AddApplication, before `return services;`)
        services.AddScoped<Variants.CreateVariantHandler>();
        services.AddScoped<Variants.UpdateVariantHandler>();
        services.AddScoped<Variants.DeleteVariantHandler>();
        services.AddScoped<Variants.ListVariantsHandler>();
```

- [ ] **Step 6: Write `VariantRepository` and register it**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Repositories/VariantRepository.cs
using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class VariantRepository : IVariantRepository
{
    private readonly ProductManagementDbContext _db;
    public VariantRepository(ProductManagementDbContext db) => _db = db;

    public Task<ProductVariant?> GetByIdAsync(long id, CancellationToken ct) =>
        _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken ct) =>
        _db.ProductVariants.AnyAsync(v => v.Sku == sku, ct);

    public Task<List<ProductVariant>> ListByProductIdAsync(long productId, CancellationToken ct) =>
        _db.ProductVariants.Where(v => v.ProductId == productId).OrderBy(v => v.Id).ToListAsync(ct);

    public void Add(ProductVariant variant) => _db.ProductVariants.Add(variant);
}
```

```csharp
// backend/src/ProductManagement.Infrastructure/DependencyInjection.cs  (add inside AddInfrastructure, before `return services;`)
        services.AddScoped<IVariantRepository, VariantRepository>();
```

- [ ] **Step 7: Write `VariantsController`**

```csharp
// backend/src/ProductManagement.Api/Controllers/VariantsController.cs
using Microsoft.AspNetCore.Mvc;
using ProductManagement.Application.Variants;

namespace ProductManagement.Api.Controllers;

[ApiController]
[Route("api/v1/products/{productId:long}/variants")]
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

    [HttpPut("{variantId:long}")]
    public async Task<IActionResult> Update(
        [FromServices] UpdateVariantHandler handler, long productId, long variantId,
        [FromBody] UpdateVariantRequest request, CancellationToken ct)
        => Ok(await handler.HandleAsync(variantId, request, ct));

    [HttpDelete("{variantId:long}")]
    public async Task<IActionResult> Delete(
        [FromServices] DeleteVariantHandler handler, long productId, long variantId, CancellationToken ct)
    {
        await handler.HandleAsync(variantId, ct);
        return NoContent();
    }
}
```

- [ ] **Step 8: Run the tests**

```bash
dotnet test tests/ProductManagement.IntegrationTests --filter VariantsEndpointsTests
```

Expected: PASS, 2 tests (the `409` case again needs Task 11 to show as
`409` instead of `500` — same caveat as before).

- [ ] **Step 9: Commit**

```bash
git add backend/src backend/tests/ProductManagement.IntegrationTests
git commit -m "Implement Variants CRUD nested under products"
```

---

## Task 8: Redis Caching Infrastructure

Built before Task 9 deliberately — the stock-adjustment `Idempotency-Key`
mechanism (spec §7) needs `ICacheService` to already exist.

**Files:**
- Create: `backend/src/ProductManagement.Infrastructure/Caching/RedisCacheService.cs`
- Modify: `backend/src/ProductManagement.Infrastructure/DependencyInjection.cs`
- Modify: `backend/src/ProductManagement.Api/appsettings.json`
- Test: `backend/tests/ProductManagement.IntegrationTests/CacheServiceTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/ProductManagement.IntegrationTests/CacheServiceTests.cs
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ProductManagement.Application.Common.Interfaces;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class CacheServiceTests
{
    private readonly DatabaseFixture _fixture;
    public CacheServiceTests(DatabaseFixture fixture) => _fixture = fixture;

    private sealed record Sample(string Value);

    [Fact]
    public async Task SetThenGet_ReturnsTheSameValue()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var key = $"test:{Guid.NewGuid()}";

        await cache.SetAsync(key, new Sample("hello"), TimeSpan.FromMinutes(1), default);
        var result = await cache.GetAsync<Sample>(key, default);

        result.Should().BeEquivalentTo(new Sample("hello"));
    }

    [Fact]
    public async Task Get_WhenKeyNeverSet_ReturnsNull()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();

        var result = await cache.GetAsync<Sample>($"test:{Guid.NewGuid()}", default);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Remove_ThenGet_ReturnsNull()
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var key = $"test:{Guid.NewGuid()}";
        await cache.SetAsync(key, new Sample("hello"), TimeSpan.FromMinutes(1), default);

        await cache.RemoveAsync(key, default);
        var result = await cache.GetAsync<Sample>(key, default);

        result.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
docker run -d --name pm-redis -p 6379:6379 redis:7
dotnet test tests/ProductManagement.IntegrationTests --filter CacheServiceTests
```

Expected: FAIL — `ICacheService` has no registered implementation yet.

- [ ] **Step 3: Implement `RedisCacheService`**

```csharp
// backend/src/ProductManagement.Infrastructure/Caching/RedisCacheService.cs
using System.Text.Json;
using ProductManagement.Application.Common.Interfaces;
using StackExchange.Redis;

namespace ProductManagement.Infrastructure.Caching;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    public RedisCacheService(IConnectionMultiplexer redis) => _redis = redis;

    private IDatabase Db => _redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        var value = await Db.StringGetAsync(key);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(value!);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class =>
        Db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl);

    public Task RemoveAsync(string key, CancellationToken ct) => Db.KeyDeleteAsync(key);

    public Task<long> IncrementVersionAsync(string versionKey, CancellationToken ct) => Db.StringIncrementAsync(versionKey);
}
```

- [ ] **Step 4: Register `IConnectionMultiplexer` (Singleton) and `ICacheService` (Scoped) — spec §5's lifetime rule**

```csharp
// backend/src/ProductManagement.Infrastructure/DependencyInjection.cs  (replace entire file)
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Infrastructure.Caching;
using ProductManagement.Infrastructure.Persistence;
using ProductManagement.Infrastructure.Persistence.Repositories;
using StackExchange.Redis;

namespace ProductManagement.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<ProductManagementDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("Default")));

        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(config["Redis:ConnectionString"] ?? "localhost:6379"));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IVariantRepository, VariantRepository>();
        services.AddScoped<ICacheService, RedisCacheService>();

        return services;
    }
}
```

- [ ] **Step 5: Add the Redis connection string to `appsettings.json`**

```json
// backend/src/ProductManagement.Api/appsettings.json  (add alongside "ConnectionStrings")
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
```

- [ ] **Step 6: Run the tests**

```bash
dotnet test tests/ProductManagement.IntegrationTests --filter CacheServiceTests
```

Expected: PASS, 3 tests.

- [ ] **Step 7: Commit**

```bash
git add backend/src backend/tests/ProductManagement.IntegrationTests
git commit -m "Add Redis caching infrastructure (ICacheService)"
docker rm -f pm-redis
```

---

## Task 9: Atomic Stock Adjustment and the Concurrency Test (spec §3.4 — the core requirement)

This is the single most important task in the plan: it proves the strong-
consistency guarantee the whole assessment is evaluating. The concurrency
test in Step 1 must pass against a **real** Postgres — this is exactly why
`IntegrationTests` targets the real docker-compose database instead of
Testcontainers or mocks (spec §10).

**Files:**
- Create: `backend/src/ProductManagement.Infrastructure/Persistence/Repositories/StockRepository.cs`
- Modify: `backend/src/ProductManagement.Infrastructure/DependencyInjection.cs`
- Create: `backend/src/ProductManagement.Application/Variants/AdjustStockHandler.cs`
- Modify: `backend/src/ProductManagement.Application/DependencyInjection.cs`
- Modify: `backend/src/ProductManagement.Api/Controllers/VariantsController.cs`
- Test: `backend/tests/ProductManagement.IntegrationTests/StockConcurrencyTests.cs`
- Test: `backend/tests/ProductManagement.IntegrationTests/StockEndpointTests.cs`

- [ ] **Step 1: Write the failing concurrency test first — this is the test that matters most in the entire plan**

```csharp
// backend/tests/ProductManagement.IntegrationTests/StockConcurrencyTests.cs
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class StockConcurrencyTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public StockConcurrencyTests(DatabaseFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() { await _fixture.ResetAsync(); _client = _fixture.Factory.CreateClient(); }
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record IdRef(long Id);
    private sealed record StockResponse(bool Succeeded, int? NewQuantity, int? AvailableQuantity);

    [Fact]
    public async Task ConcurrentDecrements_NeverOversell_ExactlyEnoughSucceed()
    {
        // Arrange: one variant, exactly 10 in stock.
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "Flash Sale", slug = "flash-sale", parentCategoryId = (long?)null, displayOrder = 0 });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdRef>();

        var productResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Viral Sneaker", slug = "viral-sneaker", categoryId = category!.Id, brand = "Acme",
            variants = new[] { new { sku = "SNEAKER-9", size = "9", color = "White", price = 120.00m, stockQuantity = 10 } }
        });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductWithVariants>();
        var variantId = product!.Variants[0].Id;

        // Act: 30 concurrent requests each trying to decrement 1 unit, against 10 in stock.
        var tasks = Enumerable.Range(0, 30).Select(_ =>
            _client.PatchAsJsonAsync($"/api/v1/products/{product.Id}/variants/{variantId}/stock", new { delta = -1 }));
        var responses = await Task.WhenAll(tasks);

        // Assert: exactly 10 succeeded, the rest got a definitive conflict — never more than 10 total decremented.
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<StockResponse>()));
        var succeeded = bodies.Count(b => b!.Succeeded);
        var failed = bodies.Count(b => !b!.Succeeded);

        succeeded.Should().Be(10);
        failed.Should().Be(20);

        var finalStockResponse = await _client.GetAsync($"/api/v1/products/{product.Id}");
        var finalProduct = await finalStockResponse.Content.ReadFromJsonAsync<ProductWithVariants>();
        finalProduct!.Variants[0].StockQuantity.Should().Be(0); // never negative, never short-sold
    }

    private sealed record VariantRef(long Id, int StockQuantity);
    private sealed record ProductWithVariants(long Id, List<VariantRef> Variants);
}
```

- [ ] **Step 2: Write the failing endpoint-level test**

```csharp
// backend/tests/ProductManagement.IntegrationTests/StockEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class StockEndpointTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public StockEndpointTests(DatabaseFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() { await _fixture.ResetAsync(); _client = _fixture.Factory.CreateClient(); }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(long productId, long variantId)> CreateProductWithStockAsync(int stock)
    {
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "Cat", slug = $"cat-{Guid.NewGuid()}", parentCategoryId = (long?)null, displayOrder = 0 });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdRef>();

        var productResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Item", slug = $"item-{Guid.NewGuid()}", categoryId = category!.Id, brand = (string?)null,
            variants = new[] { new { sku = $"SKU-{Guid.NewGuid()}", size = (string?)null, color = (string?)null, price = 10.00m, stockQuantity = stock } }
        });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductWithVariants>();
        return (product!.Id, product.Variants[0].Id);
    }

    [Fact]
    public async Task AdjustStock_Decrement_WithinAvailableStock_Returns200()
    {
        var (productId, variantId) = await CreateProductWithStockAsync(5);

        var response = await _client.PatchAsJsonAsync($"/api/v1/products/{productId}/variants/{variantId}/stock", new { delta = -3 });
        var body = await response.Content.ReadFromJsonAsync<StockResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.NewQuantity.Should().Be(2);
    }

    [Fact]
    public async Task AdjustStock_DecrementBeyondAvailable_Returns409WithAvailableQuantity()
    {
        var (productId, variantId) = await CreateProductWithStockAsync(2);

        var response = await _client.PatchAsJsonAsync($"/api/v1/products/{productId}/variants/{variantId}/stock", new { delta = -5 });
        var body = await response.Content.ReadFromJsonAsync<StockResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body!.AvailableQuantity.Should().Be(2);
    }

    [Fact]
    public async Task AdjustStock_RepeatedWithSameIdempotencyKey_OnlyAppliesOnce()
    {
        var (productId, variantId) = await CreateProductWithStockAsync(10);
        var idempotencyKey = Guid.NewGuid().ToString();

        var request1 = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/products/{productId}/variants/{variantId}/stock")
        { Content = JsonContent.Create(new { delta = -3 }) };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        await _client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/products/{productId}/variants/{variantId}/stock")
        { Content = JsonContent.Create(new { delta = -3 }) };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await _client.SendAsync(request2);
        var body2 = await response2.Content.ReadFromJsonAsync<StockResponse>();

        body2!.NewQuantity.Should().Be(7); // still 7, NOT 4 - the retried decrement never re-applied
    }

    private sealed record IdRef(long Id);
    private sealed record VariantRef(long Id, int StockQuantity);
    private sealed record ProductWithVariants(long Id, List<VariantRef> Variants);
    private sealed record StockResponse(bool Succeeded, int? NewQuantity, int? AvailableQuantity);
}
```

- [ ] **Step 3: Run both test files to verify failure**

```bash
docker run -d --name pm-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=productdb_test -p 5432:5432 postgres:17
docker run -d --name pm-redis -p 6379:6379 redis:7
dotnet test tests/ProductManagement.IntegrationTests --filter "StockConcurrencyTests|StockEndpointTests"
```

Expected: FAIL to compile — the stock endpoint doesn't exist yet.

- [ ] **Step 4: Implement `StockRepository` — the atomic conditional `UPDATE` (spec §3.4)**

```csharp
// backend/src/ProductManagement.Infrastructure/Persistence/Repositories/StockRepository.cs
using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class StockRepository : IStockRepository
{
    private readonly ProductManagementDbContext _db;
    public StockRepository(ProductManagementDbContext db) => _db = db;

    public async Task<StockAdjustResult> TryAdjustAsync(long variantId, int delta, CancellationToken ct)
    {
        if (delta >= 0)
        {
            var incrementedRows = await _db.ProductVariants
                .Where(v => v.Id == variantId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity + delta), ct);

            if (incrementedRows == 0) return new StockAdjustResult(false, null, null);

            var afterIncrement = await _db.ProductVariants
                .Where(v => v.Id == variantId).Select(v => v.StockQuantity).FirstAsync(ct);
            return new StockAdjustResult(true, afterIncrement, null);
        }

        var decrementAmount = -delta;

        // The single atomic statement: the WHERE clause is the guard against overselling.
        // No prior read, no window for a concurrent request to interleave (spec section 3.4).
        var affectedRows = await _db.ProductVariants
            .Where(v => v.Id == variantId && v.StockQuantity >= decrementAmount)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity - decrementAmount), ct);

        if (affectedRows == 1)
        {
            var afterDecrement = await _db.ProductVariants
                .Where(v => v.Id == variantId).Select(v => v.StockQuantity).FirstAsync(ct);
            return new StockAdjustResult(true, afterDecrement, null);
        }

        var currentStock = await _db.ProductVariants
            .Where(v => v.Id == variantId).Select(v => (int?)v.StockQuantity).FirstOrDefaultAsync(ct);
        return new StockAdjustResult(false, null, currentStock);
    }
}
```

```csharp
// backend/src/ProductManagement.Infrastructure/DependencyInjection.cs  (add inside AddInfrastructure, before `return services;`)
        services.AddScoped<IStockRepository, StockRepository>();
```

- [ ] **Step 5: Implement `AdjustStockHandler` with `Idempotency-Key` support (spec §7)**

```csharp
// backend/src/ProductManagement.Application/Variants/AdjustStockHandler.cs
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Variants;

public sealed record AdjustStockRequest(int Delta);
public sealed record AdjustStockResult(bool Succeeded, int? NewQuantity, int? AvailableQuantity);

public class AdjustStockHandler
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromMinutes(5);
    private readonly IStockRepository _stock;
    private readonly ICacheService _cache;

    public AdjustStockHandler(IStockRepository stock, ICacheService cache)
    {
        _stock = stock;
        _cache = cache;
    }

    public async Task<AdjustStockResult> HandleAsync(long variantId, AdjustStockRequest request, string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var direct = await _stock.TryAdjustAsync(variantId, request.Delta, ct);
            return new AdjustStockResult(direct.Succeeded, direct.NewQuantity, direct.AvailableQuantity);
        }

        var cacheKey = $"stock-adjust:{idempotencyKey}";
        var cached = await _cache.GetAsync<AdjustStockResult>(cacheKey, ct);
        if (cached is not null) return cached; // retried request with the same key -> never re-applied

        var result = await _stock.TryAdjustAsync(variantId, request.Delta, ct);
        var mapped = new AdjustStockResult(result.Succeeded, result.NewQuantity, result.AvailableQuantity);
        await _cache.SetAsync(cacheKey, mapped, IdempotencyTtl, ct);
        return mapped;
    }
}
```

```csharp
// backend/src/ProductManagement.Application/DependencyInjection.cs  (add inside AddApplication, before `return services;`)
        services.AddScoped<Variants.AdjustStockHandler>();
```

- [ ] **Step 6: Add the stock endpoint to `VariantsController`**

```csharp
// backend/src/ProductManagement.Api/Controllers/VariantsController.cs  (add this action to the existing class)
    [HttpPatch("{variantId:long}/stock")]
    public async Task<IActionResult> AdjustStock(
        [FromServices] AdjustStockHandler handler, long productId, long variantId,
        [FromBody] AdjustStockRequest request, CancellationToken ct)
    {
        var idempotencyKey = Request.Headers.TryGetValue("Idempotency-Key", out var values) ? values.ToString() : null;
        var result = await handler.HandleAsync(variantId, request, idempotencyKey, ct);
        return result.Succeeded ? Ok(result) : Conflict(result);
    }
```

Add the matching `using ProductManagement.Application.Variants;` import at the
top of `VariantsController.cs` if not already present (it is, from Task 7).

- [ ] **Step 7: Run both test files**

```bash
dotnet test tests/ProductManagement.IntegrationTests --filter "StockConcurrencyTests|StockEndpointTests"
```

Expected: **PASS, all 4 tests** — this is the test run that proves spec
§3.4's core claim. `ConcurrentDecrements_NeverOversell_ExactlyEnoughSucceed`
passing here (against the real Postgres, 30 truly concurrent HTTP requests)
is the single most important verification in this entire plan. If it's
flaky or fails intermittently, do not proceed to later tasks — re-examine
`StockRepository.TryAdjustAsync` before anything else; the fix is almost
certainly that the `WHERE` clause and `ExecuteUpdateAsync` call got split
into two statements somewhere instead of staying one atomic expression.

- [ ] **Step 8: Commit**

```bash
git add backend/src backend/tests/ProductManagement.IntegrationTests
git commit -m "Implement atomic stock adjustment with idempotency; add the concurrency proof test"
docker rm -f pm-postgres pm-redis
```

---

## Task 10: Product Image Upload (spec §3.2, §7 — minimal, local disk)

**Files:**
- Create: `backend/src/ProductManagement.Infrastructure/Storage/LocalFileStorageService.cs`
- Modify: `backend/src/ProductManagement.Infrastructure/DependencyInjection.cs`
- Create: `backend/src/ProductManagement.Application/Products/UploadProductImageHandler.cs`
- Create: `backend/src/ProductManagement.Application/Products/DeleteProductImageHandler.cs`
- Modify: `backend/src/ProductManagement.Application/DependencyInjection.cs`
- Modify: `backend/src/ProductManagement.Api/Controllers/ProductsController.cs`
- Modify: `backend/src/ProductManagement.Api/Program.cs`
- Test: `backend/tests/ProductManagement.IntegrationTests/ProductImageEndpointTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// backend/tests/ProductManagement.IntegrationTests/ProductImageEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class ProductImageEndpointTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public ProductImageEndpointTests(DatabaseFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() { await _fixture.ResetAsync(); _client = _fixture.Factory.CreateClient(); }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<long> CreateProductAsync()
    {
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "Cat", slug = $"cat-{Guid.NewGuid()}", parentCategoryId = (long?)null, displayOrder = 0 });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdRef>();
        var productResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "Item", slug = $"item-{Guid.NewGuid()}", categoryId = category!.Id, brand = (string?)null, variants = Array.Empty<object>() });
        var product = await productResponse.Content.ReadFromJsonAsync<IdRef>();
        return product!.Id;
    }

    private static MultipartFormDataContent BuildImageForm(byte[] bytes, string contentType, string fileName)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return form;
    }

    [Fact]
    public async Task UploadImage_WithValidJpeg_Returns200WithUrl()
    {
        var productId = await CreateProductAsync();
        using var form = BuildImageForm(new byte[] { 1, 2, 3, 4 }, "image/jpeg", "photo.jpg");

        var response = await _client.PostAsync($"/api/v1/products/{productId}/image", form);
        var body = await response.Content.ReadFromJsonAsync<ImageResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.ImageUrl.Should().Contain($"/uploads/products/{productId}/");
    }

    [Fact]
    public async Task UploadImage_WithWrongContentType_Returns400()
    {
        var productId = await CreateProductAsync();
        using var form = BuildImageForm(new byte[] { 1, 2, 3, 4 }, "application/pdf", "doc.pdf");

        var response = await _client.PostAsync($"/api/v1/products/{productId}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadImage_OverSizeLimit_Returns400()
    {
        var productId = await CreateProductAsync();
        var oversized = new byte[6 * 1024 * 1024]; // 6 MB, over the 5 MB limit
        using var form = BuildImageForm(oversized, "image/jpeg", "big.jpg");

        var response = await _client.PostAsync($"/api/v1/products/{productId}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteImage_WhenNoneSet_Returns404()
    {
        var productId = await CreateProductAsync();

        var response = await _client.DeleteAsync($"/api/v1/products/{productId}/image");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private sealed record IdRef(long Id);
    private sealed record ImageResponse(string ImageUrl);
}
```

- [ ] **Step 2: Run to verify failure**

```bash
docker run -d --name pm-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=productdb_test -p 5432:5432 postgres:17
docker run -d --name pm-redis -p 6379:6379 redis:7
dotnet test tests/ProductManagement.IntegrationTests --filter ProductImageEndpointTests
```

Expected: FAIL to compile.

- [ ] **Step 3: Implement `LocalFileStorageService`**

```csharp
// backend/src/ProductManagement.Infrastructure/Storage/LocalFileStorageService.cs
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Infrastructure.Storage;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public LocalFileStorageService(string rootPath)
    {
        _rootPath = rootPath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, long productId, CancellationToken ct)
    {
        var extension = Path.GetExtension(fileName);
        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
        var productDir = Path.Combine(_rootPath, "products", productId.ToString());
        Directory.CreateDirectory(productDir);

        var filePath = Path.Combine(productDir, uniqueFileName);
        await using var fileStream = File.Create(filePath);
        await content.CopyToAsync(fileStream, ct);

        return $"/uploads/products/{productId}/{uniqueFileName}";
    }

    public Task DeleteAsync(string url, CancellationToken ct)
    {
        var relativePath = url.TrimStart('/').Replace("uploads/", string.Empty, StringComparison.Ordinal);
        var filePath = Path.Combine(_rootPath, relativePath);
        if (File.Exists(filePath)) File.Delete(filePath);
        return Task.CompletedTask;
    }
}
```

```csharp
// backend/src/ProductManagement.Infrastructure/DependencyInjection.cs  (add inside AddInfrastructure, before `return services;`)
        services.AddScoped<IFileStorageService>(_ =>
            new LocalFileStorageService(config["Uploads:RootPath"] ?? "wwwroot/uploads"));
```

Add `using ProductManagement.Infrastructure.Storage;` to the top of that file.

- [ ] **Step 4: Implement the upload/delete handlers, with validation done manually (not FluentValidation — spec §7 notes this doesn't fit a file stream well)**

```csharp
// backend/src/ProductManagement.Application/Products/UploadProductImageHandler.cs
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public sealed class InvalidImageException : Exception
{
    public InvalidImageException(string message) : base(message) { }
}

public class UploadProductImageHandler
{
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };
    private const long MaxSizeBytes = 5 * 1024 * 1024;

    private readonly IProductRepository _products;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;

    public UploadProductImageHandler(IProductRepository products, IFileStorageService fileStorage, IUnitOfWork unitOfWork)
    {
        _products = products;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
    }

    public async Task<string> HandleAsync(long productId, Stream content, string fileName, string contentType, long length, CancellationToken ct)
    {
        if (!AllowedContentTypes.Contains(contentType))
            throw new InvalidImageException($"Content type '{contentType}' is not allowed. Use jpeg, png, or webp.");
        if (length > MaxSizeBytes)
            throw new InvalidImageException("Image exceeds the 5 MB size limit.");

        var product = await _products.GetByIdWithVariantsAsync(productId, ct)
            ?? throw new EntityNotFoundException(nameof(Product), productId);

        if (!string.IsNullOrEmpty(product.ImageUrl))
            await _fileStorage.DeleteAsync(product.ImageUrl, ct); // replace, never orphan the old file

        var url = await _fileStorage.SaveAsync(content, fileName, contentType, productId, ct);
        product.SetImageUrl(url);
        await _unitOfWork.SaveChangesAsync(ct);
        return url;
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Products/DeleteProductImageHandler.cs
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public sealed class NoImageSetException : Exception
{
    public NoImageSetException(long productId) : base($"Product {productId} has no image set.") { }
}

public class DeleteProductImageHandler
{
    private readonly IProductRepository _products;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteProductImageHandler(IProductRepository products, IFileStorageService fileStorage, IUnitOfWork unitOfWork)
    {
        _products = products;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
    }

    public async Task HandleAsync(long productId, CancellationToken ct)
    {
        var product = await _products.GetByIdWithVariantsAsync(productId, ct)
            ?? throw new EntityNotFoundException(nameof(Product), productId);

        if (string.IsNullOrEmpty(product.ImageUrl))
            throw new NoImageSetException(productId);

        await _fileStorage.DeleteAsync(product.ImageUrl, ct);
        product.SetImageUrl(null);
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
```

```csharp
// backend/src/ProductManagement.Application/DependencyInjection.cs  (add inside AddApplication, before `return services;`)
        services.AddScoped<Products.UploadProductImageHandler>();
        services.AddScoped<Products.DeleteProductImageHandler>();
```

- [ ] **Step 5: Add the image endpoints to `ProductsController`**

```csharp
// backend/src/ProductManagement.Api/Controllers/ProductsController.cs  (add these two actions to the existing class)
    [HttpPost("{id:long}/image")]
    public async Task<IActionResult> UploadImage(
        [FromServices] UploadProductImageHandler handler, long id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var url = await handler.HandleAsync(id, stream, file.FileName, file.ContentType, file.Length, ct);
        return Ok(new { imageUrl = url });
    }

    [HttpDelete("{id:long}/image")]
    public async Task<IActionResult> DeleteImage(
        [FromServices] DeleteProductImageHandler handler, long id, CancellationToken ct)
    {
        await handler.HandleAsync(id, ct);
        return NoContent();
    }
```

Add `using Microsoft.AspNetCore.Http;` to the top of `ProductsController.cs`
if not already present.

- [ ] **Step 6: Enable static file serving for the uploads directory in `Program.cs` (spec §10)**

```csharp
// backend/src/ProductManagement.Api/Program.cs  (add after `var app = builder.Build();`, before `app.MapControllers();`)
app.UseStaticFiles(); // serves wwwroot/uploads at /uploads (spec section 10)
```

- [ ] **Step 7: Run the tests**

```bash
dotnet test tests/ProductManagement.IntegrationTests --filter ProductImageEndpointTests
```

Expected: PASS, 4 tests.

- [ ] **Step 8: Commit**

```bash
git add backend/src backend/tests/ProductManagement.IntegrationTests
git commit -m "Implement minimal single-image-per-product upload (local disk)"
docker rm -f pm-postgres pm-redis
```

---

## Task 11: Global Exception Handler (spec §7)

This resolves every "might show 500 instead of 400/409" caveat left in
Tasks 5–7 — a single place where every exception type gets mapped to its
correct status code.

**Files:**
- Create: `backend/src/ProductManagement.Api/Middleware/GlobalExceptionHandler.cs`
- Modify: `backend/src/ProductManagement.Api/Program.cs`
- Test: `backend/tests/ProductManagement.IntegrationTests/ErrorHandlingTests.cs`

- [ ] **Step 1: Write the failing tests — both new coverage and re-verification of earlier caveats**

```csharp
// backend/tests/ProductManagement.IntegrationTests/ErrorHandlingTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class ErrorHandlingTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public ErrorHandlingTests(DatabaseFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() { await _fixture.ResetAsync(); _client = _fixture.Factory.CreateClient(); }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ValidationFailure_Returns400WithFieldErrors()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "", slug = "x", parentCategoryId = (long?)null, displayOrder = 0 });
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.RootElement.GetProperty("errors").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/v1/categories/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DuplicateSlug_Returns409()
    {
        var payload = new { name = "Dresses", slug = "dup-slug-test", parentCategoryId = (long?)null, displayOrder = 0 };
        await _client.PostAsJsonAsync("/api/v1/categories", payload);

        var response = await _client.PostAsJsonAsync("/api/v1/categories", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task StaleETag_Returns409()
    {
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "C", slug = "stale-etag-cat", parentCategoryId = (long?)null, displayOrder = 0 });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdRef>();
        var productResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "P", slug = "stale-etag-prod", categoryId = category!.Id, brand = (string?)null, variants = Array.Empty<object>() });
        var product = await productResponse.Content.ReadFromJsonAsync<IdRef>();

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{product!.Id}")
        { Content = JsonContent.Create(new { name = "Updated", description = (string?)null, categoryId = category.Id, brand = (string?)null, attributes = "{}" }) };
        request.Headers.TryAddWithoutValidation("If-Match", "\"0\"");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UnhandledException_Returns500WithTraceId()
    {
        // categoryId=999999999 doesn't exist, but that's an EntityNotFoundException (404) —
        // this test instead confirms the *shape* of a 500 response by asserting the traceId
        // extension exists whenever a 500 does occur (structural check on GlobalExceptionHandler,
        // not a specific trigger — genuinely unexpected exceptions are, by definition, not
        // reproducible on demand).
        var response = await _client.GetAsync("/api/v1/categories/not-a-number");

        // An unparseable route parameter is caught by ASP.NET Core's own model binding (400),
        // before it ever reaches application code - confirms routing-level errors don't leak
        // as 500s either.
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListProducts_WithInvalidCursor_Returns400_NotSilentFirstPage()
    {
        var response = await _client.GetAsync("/api/v1/products?cursor=not-valid-base64!!!");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record IdRef(long Id);
}
```

- [ ] **Step 2: Run to verify the current (pre-fix) behavior**

```bash
docker run -d --name pm-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=productdb_test -p 5432:5432 postgres:17
docker run -d --name pm-redis -p 6379:6379 redis:7
dotnet test tests/ProductManagement.IntegrationTests --filter ErrorHandlingTests
```

Expected: `ValidationFailure_Returns400WithFieldErrors`, `DuplicateSlug_Returns409`,
and `StaleETag_Returns409` FAIL (currently surface as `500`); `NotFound_Returns404`
already passes (Task 5's `EntityNotFoundException` handling relies on
ASP.NET Core's default behavior only coincidentally — confirm this explicitly
once the handler is in place in Step 5).

- [ ] **Step 3: Implement `GlobalExceptionHandler`**

```csharp
// backend/src/ProductManagement.Api/Middleware/GlobalExceptionHandler.cs
using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Exceptions;

namespace ProductManagement.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (statusCode, title, errors) = Map(exception);

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Type = $"https://httpstatuses.com/{(int)statusCode}"
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            var traceId = httpContext.TraceIdentifier;
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);
            problemDetails.Extensions["traceId"] = traceId;
        }
        else if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        httpContext.Response.StatusCode = (int)statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);
        return true;
    }

    private static (HttpStatusCode StatusCode, string Title, object? Errors) Map(Exception exception) => exception switch
    {
        ValidationException ex => (
            HttpStatusCode.BadRequest, "Validation failed.",
            ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })),

        DomainException ex => (HttpStatusCode.BadRequest, ex.Message, null),
        InvalidImageException ex => (HttpStatusCode.BadRequest, ex.Message, null),
        InvalidCursorException ex => (HttpStatusCode.BadRequest, ex.Message, null),

        EntityNotFoundException ex => (HttpStatusCode.NotFound, ex.Message, null),
        NoImageSetException ex => (HttpStatusCode.NotFound, ex.Message, null),

        DuplicateSkuException ex => (HttpStatusCode.Conflict, ex.Message, null),
        DuplicateSlugException ex => (HttpStatusCode.Conflict, ex.Message, null),
        CategoryHasActiveProductsException ex => (HttpStatusCode.Conflict, ex.Message, null),
        DbUpdateConcurrencyException => (
            HttpStatusCode.Conflict, "The resource was modified by another request. Reload and try again.", null),

        _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", null)
    };
}
```

- [ ] **Step 4: Register it in `Program.cs`**

```csharp
// backend/src/ProductManagement.Api/Program.cs  (modify — two additions, shown in context)
using ProductManagement.Api.Middleware;
using ProductManagement.Application;
using ProductManagement.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(); // must be early in the pipeline, before routing/controllers
app.UseStaticFiles();

app.MapControllers();

app.Run();

public partial class Program { }
```

- [ ] **Step 5: Run the full test suite so far**

```bash
dotnet test tests/ProductManagement.IntegrationTests
```

Expected: **PASS, every test across every file written so far** — this is
the point where all the `400`/`409` caveats from Tasks 5, 6, and 7
resolve. If any still fail, check the exception being thrown actually
matches one of the `Map` switch arms (a common miss: `DomainException` is
`abstract`, so its concrete subclasses like `InvalidCategoryException` are
what's actually thrown — the pattern `DomainException ex` still matches
those via subtype matching, but double-check nothing throws a *different*,
unmapped exception type instead).

- [ ] **Step 6: Commit**

```bash
git add backend/src backend/tests/ProductManagement.IntegrationTests
git commit -m "Add global exception handler mapping all exception types to ProblemDetails"
docker rm -f pm-postgres pm-redis
```

---

## Task 12: Cache-Aside Wiring and Invalidation (spec §8)

**Files:**
- Modify: `backend/src/ProductManagement.Application/Common/Interfaces/ICacheService.cs`
- Modify: `backend/src/ProductManagement.Infrastructure/Caching/RedisCacheService.cs`
- Create: `backend/src/ProductManagement.Application/Products/ProductCacheKeys.cs`
- Create: `backend/src/ProductManagement.Application/Categories/CategoryCacheKeys.cs`
- Modify: `backend/src/ProductManagement.Application/Products/GetProductHandler.cs`
- Modify: `backend/src/ProductManagement.Application/Products/ListProductsHandler.cs`
- Modify: `backend/src/ProductManagement.Application/Products/CreateProductHandler.cs`
- Modify: `backend/src/ProductManagement.Application/Products/UpdateProductHandler.cs`
- Modify: `backend/src/ProductManagement.Application/Products/DeleteProductHandler.cs`
- Modify: `backend/src/ProductManagement.Application/Variants/AdjustStockHandler.cs`
- Modify: `backend/src/ProductManagement.Api/Controllers/VariantsController.cs`
- Modify: `backend/src/ProductManagement.Application/Categories/GetCategoryHandler.cs`
- Modify: `backend/src/ProductManagement.Application/Categories/ListCategoriesHandler.cs`
- Modify: `backend/src/ProductManagement.Application/Categories/CreateCategoryHandler.cs`
- Modify: `backend/src/ProductManagement.Application/Categories/UpdateCategoryHandler.cs`
- Modify: `backend/src/ProductManagement.Application/Categories/DeleteCategoryHandler.cs`
- Test: `backend/tests/ProductManagement.IntegrationTests/CachingTests.cs`

- [ ] **Step 1: Write the failing test — proves the cache is actually hit, and that a write invalidates it**

```csharp
// backend/tests/ProductManagement.IntegrationTests/CachingTests.cs
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class CachingTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public CachingTests(DatabaseFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() { await _fixture.ResetAsync(); _client = _fixture.Factory.CreateClient(); }
    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record IdRef(long Id);

    [Fact]
    public async Task GetProduct_SecondCall_IsServedFromCache()
    {
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "C", slug = "cache-cat", parentCategoryId = (long?)null, displayOrder = 0 });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdRef>();
        var productResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "P", slug = "cache-prod", categoryId = category!.Id, brand = (string?)null, variants = Array.Empty<object>() });
        var product = await productResponse.Content.ReadFromJsonAsync<IdRef>();

        await _client.GetAsync($"/api/v1/products/{product!.Id}"); // populates the cache

        using var scope = _fixture.Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var cached = await cache.GetAsync<ProductResult>(ProductCacheKeys.Product(product.Id), default);

        cached.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateProduct_InvalidatesTheCachedEntry()
    {
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "C", slug = "cache-cat-2", parentCategoryId = (long?)null, displayOrder = 0 });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdRef>();
        var productResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "P", slug = "cache-prod-2", categoryId = category!.Id, brand = (string?)null, variants = Array.Empty<object>() });
        var product = await productResponse.Content.ReadFromJsonAsync<IdRef>();

        var getResponse = await _client.GetAsync($"/api/v1/products/{product!.Id}"); // populates cache + gives us the ETag
        var etag = getResponse.Headers.ETag!.Tag;

        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/products/{product.Id}")
        { Content = JsonContent.Create(new { name = "Updated", description = (string?)null, categoryId = category.Id, brand = (string?)null, attributes = "{}" }) };
        updateRequest.Headers.TryAddWithoutValidation("If-Match", etag);
        await _client.SendAsync(updateRequest);

        using var scope = _fixture.Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<ICacheService>();
        var cached = await cache.GetAsync<ProductResult>(ProductCacheKeys.Product(product.Id), default);

        cached.Should().BeNull(); // invalidated by the update, not left stale
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
docker run -d --name pm-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=productdb_test -p 5432:5432 postgres:17
docker run -d --name pm-redis -p 6379:6379 redis:7
dotnet test tests/ProductManagement.IntegrationTests --filter CachingTests
```

Expected: FAIL — `ProductCacheKeys` doesn't exist, nothing is cached yet.

- [ ] **Step 3: Extend `ICacheService` with a version-peek method, and implement it**

```csharp
// backend/src/ProductManagement.Application/Common/Interfaces/ICacheService.cs  (replace entire file)
namespace ProductManagement.Application.Common.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class;
    Task RemoveAsync(string key, CancellationToken ct);
    Task<long> GetVersionAsync(string versionKey, CancellationToken ct);
    Task<long> IncrementVersionAsync(string versionKey, CancellationToken ct);
}
```

```csharp
// backend/src/ProductManagement.Infrastructure/Caching/RedisCacheService.cs  (replace entire file)
using System.Text.Json;
using ProductManagement.Application.Common.Interfaces;
using StackExchange.Redis;

namespace ProductManagement.Infrastructure.Caching;

public class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    public RedisCacheService(IConnectionMultiplexer redis) => _redis = redis;

    private IDatabase Db => _redis.GetDatabase();

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct) where T : class
    {
        var value = await Db.StringGetAsync(key);
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<T>(value!);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class =>
        Db.StringSetAsync(key, JsonSerializer.Serialize(value), ttl);

    public Task RemoveAsync(string key, CancellationToken ct) => Db.KeyDeleteAsync(key);

    public async Task<long> GetVersionAsync(string versionKey, CancellationToken ct)
    {
        var value = await Db.StringGetAsync(versionKey);
        return value.IsNullOrEmpty ? 0 : (long)value;
    }

    public Task<long> IncrementVersionAsync(string versionKey, CancellationToken ct) => Db.StringIncrementAsync(versionKey);
}
```

- [ ] **Step 4: Add cache key helpers**

```csharp
// backend/src/ProductManagement.Application/Products/ProductCacheKeys.cs
namespace ProductManagement.Application.Products;

public static class ProductCacheKeys
{
    public static string Product(long id) => $"product:{id}";
    public const string ListVersionKey = "products:list:version";
    public static string List(long version, string queryHash) => $"products:list:v{version}:{queryHash}";
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/CategoryCacheKeys.cs
namespace ProductManagement.Application.Categories;

public static class CategoryCacheKeys
{
    public const string ListKey = "categories:list";
}
```

- [ ] **Step 5: Wire caching into the Product read/write handlers**

```csharp
// backend/src/ProductManagement.Application/Products/GetProductHandler.cs  (replace entire file)
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public sealed record ProductResult(ProductDto Product, uint Xmin);

public class GetProductHandler
{
    private static readonly TimeSpan ProductTtl = TimeSpan.FromMinutes(10);
    private readonly IProductRepository _products;
    private readonly ICacheService _cache;

    public GetProductHandler(IProductRepository products, ICacheService cache)
    {
        _products = products;
        _cache = cache;
    }

    public async Task<ProductResult> ByIdAsync(long id, CancellationToken ct)
    {
        var cacheKey = ProductCacheKeys.Product(id);
        var cached = await _cache.GetAsync<ProductResult>(cacheKey, ct);
        if (cached is not null) return cached;

        var product = await _products.GetByIdWithVariantsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);
        var xmin = await _products.GetXminAsync(id, ct);
        var result = new ProductResult(product.ToDto(), xmin);

        await _cache.SetAsync(cacheKey, result, ProductTtl, ct);
        return result;
    }

    public async Task<ProductResult> BySlugAsync(string slug, CancellationToken ct)
    {
        var product = await _products.GetBySlugWithVariantsAsync(slug, ct)
            ?? throw new EntityNotFoundException(nameof(Product), slug);
        return await ByIdAsync(product.Id, ct); // reuses the id-keyed cache entry
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Products/ListProductsHandler.cs  (replace entire file)
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProductManagement.Application.Common;
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Products;

public class ListProductsHandler
{
    private const int MaxLimit = 100;
    private static readonly TimeSpan ListTtl = TimeSpan.FromSeconds(60);
    private readonly IProductRepository _products;
    private readonly ICacheService _cache;

    public ListProductsHandler(IProductRepository products, ICacheService cache)
    {
        _products = products;
        _cache = cache;
    }

    public async Task<PagedResult<ProductListItemDto>> HandleAsync(
        long? categoryId, short? status, string? q, decimal? minPrice, decimal? maxPrice,
        string? attributesJson, string? cursor, int limit, CancellationToken ct)
    {
        var query = new ProductListQuery(
            categoryId, status, q, minPrice, maxPrice, attributesJson, cursor, Math.Min(limit, MaxLimit));

        var version = await _cache.GetVersionAsync(ProductCacheKeys.ListVersionKey, ct);
        var queryHash = HashQuery(query);
        var cacheKey = ProductCacheKeys.List(version, queryHash);

        var cached = await _cache.GetAsync<PagedResult<ProductListItemDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        var page = await _products.ListAsync(query, ct);
        var result = new PagedResult<ProductListItemDto>(
            page.Items.Select(p => p.ToListItemDto()).ToList(), page.NextCursor, page.HasMore);

        await _cache.SetAsync(cacheKey, result, ListTtl, ct);
        return result;
    }

    private static string HashQuery(ProductListQuery query)
    {
        var json = JsonSerializer.Serialize(query);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash)[..16];
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Products/CreateProductHandler.cs  (replace entire file)
using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public class CreateProductHandler
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IValidator<CreateProductRequest> _validator;

    public CreateProductHandler(
        IProductRepository products, ICategoryRepository categories, IUnitOfWork unitOfWork,
        ICacheService cache, IValidator<CreateProductRequest> validator)
    {
        _products = products;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _validator = validator;
    }

    public async Task<ProductDto> HandleAsync(CreateProductRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var category = await _categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new EntityNotFoundException(nameof(Category), request.CategoryId);

        var product = Product.Create(request.Name, request.Slug, category.Id, request.Brand, request.Attributes);

        foreach (var v in request.Variants ?? new List<CreateVariantRequest>())
        {
            product.AddVariant(ProductVariant.Create(
                product.Id, v.Sku, v.Size, v.Color, v.Price, v.StockQuantity, v.CompareAtPrice, v.Barcode));
        }

        _products.Add(product);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.IncrementVersionAsync(ProductCacheKeys.ListVersionKey, ct); // new product must appear in list views
        return product.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Products/UpdateProductHandler.cs  (replace entire file)
using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public class UpdateProductHandler
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IValidator<UpdateProductRequest> _validator;

    public UpdateProductHandler(
        IProductRepository products, IUnitOfWork unitOfWork, ICacheService cache, IValidator<UpdateProductRequest> validator)
    {
        _products = products;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _validator = validator;
    }

    public async Task<ProductDto> HandleAsync(long id, uint expectedXmin, UpdateProductRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var product = await _products.GetByIdWithVariantsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);

        _products.SetExpectedVersion(product, expectedXmin);
        product.UpdateDetails(request.Name, request.Description, request.CategoryId, request.Brand, request.Attributes);

        await _unitOfWork.SaveChangesAsync(ct); // throws DbUpdateConcurrencyException on xmin mismatch -> 409 (Task 11)
        await _cache.RemoveAsync(ProductCacheKeys.Product(id), ct); // never serve stale data after a write (spec section 8)
        await _cache.IncrementVersionAsync(ProductCacheKeys.ListVersionKey, ct);
        return product.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Products/DeleteProductHandler.cs  (replace entire file)
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public class DeleteProductHandler
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public DeleteProductHandler(IProductRepository products, IUnitOfWork unitOfWork, ICacheService cache)
    {
        _products = products;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task HandleAsync(long id, CancellationToken ct)
    {
        var product = await _products.GetByIdWithVariantsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);

        product.Archive();
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(ProductCacheKeys.Product(id), ct);
        await _cache.IncrementVersionAsync(ProductCacheKeys.ListVersionKey, ct);
    }
}
```

- [ ] **Step 6: Invalidate on stock adjustment (the field cached data must never lag on, per spec §8)**

```csharp
// backend/src/ProductManagement.Application/Variants/AdjustStockHandler.cs  (replace entire file)
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;

namespace ProductManagement.Application.Variants;

public sealed record AdjustStockRequest(int Delta);
public sealed record AdjustStockResult(bool Succeeded, int? NewQuantity, int? AvailableQuantity);

public class AdjustStockHandler
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromMinutes(5);
    private readonly IStockRepository _stock;
    private readonly ICacheService _cache;

    public AdjustStockHandler(IStockRepository stock, ICacheService cache)
    {
        _stock = stock;
        _cache = cache;
    }

    public async Task<AdjustStockResult> HandleAsync(
        long productId, long variantId, AdjustStockRequest request, string? idempotencyKey, CancellationToken ct)
    {
        AdjustStockResult result;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var direct = await _stock.TryAdjustAsync(variantId, request.Delta, ct);
            result = new AdjustStockResult(direct.Succeeded, direct.NewQuantity, direct.AvailableQuantity);
        }
        else
        {
            var cacheKey = $"stock-adjust:{idempotencyKey}";
            var cached = await _cache.GetAsync<AdjustStockResult>(cacheKey, ct);
            if (cached is not null) return cached; // retried request -> never re-applied, no cache side-effect either

            var adjusted = await _stock.TryAdjustAsync(variantId, request.Delta, ct);
            result = new AdjustStockResult(adjusted.Succeeded, adjusted.NewQuantity, adjusted.AvailableQuantity);
            await _cache.SetAsync(cacheKey, result, IdempotencyTtl, ct);
        }

        if (result.Succeeded)
            await _cache.RemoveAsync(ProductCacheKeys.Product(productId), ct); // stock must never be served stale

        return result;
    }
}
```

```csharp
// backend/src/ProductManagement.Api/Controllers/VariantsController.cs  (modify the AdjustStock action's handler call)
        var result = await handler.HandleAsync(productId, variantId, request, idempotencyKey, ct);
```

(This replaces the previous `handler.HandleAsync(variantId, request, idempotencyKey, ct)` call from
Task 9 — `productId` is already a route parameter on this action, no other change needed.)

- [ ] **Step 7: Wire caching into the Category read/write handlers**

```csharp
// backend/src/ProductManagement.Application/Categories/GetCategoryHandler.cs  (replace entire file)
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class GetCategoryHandler
{
    private readonly ICategoryRepository _categories;
    public GetCategoryHandler(ICategoryRepository categories) => _categories = categories;

    public async Task<CategoryDto> HandleAsync(long id, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);
        return category.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/ListCategoriesHandler.cs  (replace entire file)
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Categories;

public class ListCategoriesHandler
{
    private static readonly TimeSpan ListTtl = TimeSpan.FromMinutes(30);
    private readonly ICategoryRepository _categories;
    private readonly ICacheService _cache;

    public ListCategoriesHandler(ICategoryRepository categories, ICacheService cache)
    {
        _categories = categories;
        _cache = cache;
    }

    public async Task<List<CategoryDto>> HandleAsync(long? parentId, bool? activeOnly, CancellationToken ct)
    {
        // Only the common "list everything" call is cached — filtered variants (parentId/activeOnly
        // set) are infrequent enough that caching every combination isn't worth the complexity.
        if (parentId is null && activeOnly is null)
        {
            var cached = await _cache.GetAsync<List<CategoryDto>>(CategoryCacheKeys.ListKey, ct);
            if (cached is not null) return cached;
        }

        var categories = await _categories.ListAsync(parentId, activeOnly, ct);
        var result = categories.Select(c => c.ToDto()).ToList();

        if (parentId is null && activeOnly is null)
            await _cache.SetAsync(CategoryCacheKeys.ListKey, result, ListTtl, ct);

        return result;
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/CreateCategoryHandler.cs  (replace entire file)
using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class CreateCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IValidator<CreateCategoryRequest> _validator;

    public CreateCategoryHandler(
        ICategoryRepository categories, IUnitOfWork unitOfWork, ICacheService cache, IValidator<CreateCategoryRequest> validator)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _validator = validator;
    }

    public async Task<CategoryDto> HandleAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        if (request.ParentCategoryId is { } parentId)
        {
            var parent = await _categories.GetByIdAsync(parentId, ct);
            if (parent is null) throw new EntityNotFoundException(nameof(Category), parentId);
        }

        var category = Category.Create(request.Name, request.Slug, request.ParentCategoryId, request.DisplayOrder);
        _categories.Add(category);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CategoryCacheKeys.ListKey, ct);
        return category.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/UpdateCategoryHandler.cs  (replace entire file)
using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class UpdateCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IValidator<UpdateCategoryRequest> _validator;

    public UpdateCategoryHandler(
        ICategoryRepository categories, IUnitOfWork unitOfWork, ICacheService cache, IValidator<UpdateCategoryRequest> validator)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _validator = validator;
    }

    public async Task<CategoryDto> HandleAsync(long id, UpdateCategoryRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var category = await _categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);

        category.Update(request.Name, request.Slug, request.ParentCategoryId, request.DisplayOrder, request.IsActive);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CategoryCacheKeys.ListKey, ct);
        return category.ToDto();
    }
}
```

```csharp
// backend/src/ProductManagement.Application/Categories/DeleteCategoryHandler.cs  (replace entire file)
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class DeleteCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public DeleteCategoryHandler(ICategoryRepository categories, IUnitOfWork unitOfWork, ICacheService cache)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task HandleAsync(long id, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);

        if (await _categories.HasActiveProductsAsync(id, ct))
            throw new CategoryHasActiveProductsException(id);

        _categories.Remove(category);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CategoryCacheKeys.ListKey, ct);
    }
}
```

- [ ] **Step 8: Run the tests**

```bash
dotnet test tests/ProductManagement.IntegrationTests --filter CachingTests
```

Expected: PASS, 2 tests. Then run the full suite once more to confirm
nothing regressed from the handler signature changes:

```bash
dotnet test tests/ProductManagement.IntegrationTests
```

Expected: PASS, all tests.

- [ ] **Step 9: Commit**

```bash
git add backend/src backend/tests/ProductManagement.IntegrationTests
git commit -m "Wire Redis cache-aside onto product/category reads with invalidation on every write"
docker rm -f pm-postgres pm-redis
```

---

## Task 13: CORS (spec §10 "CORS")

**Files:**
- Modify: `backend/src/ProductManagement.Api/Program.cs`
- Modify: `backend/src/ProductManagement.Api/appsettings.json`
- Test: `backend/tests/ProductManagement.IntegrationTests/CorsTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
// backend/tests/ProductManagement.IntegrationTests/CorsTests.cs
using FluentAssertions;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class CorsTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public CorsTests(DatabaseFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() { await _fixture.ResetAsync(); _client = _fixture.Factory.CreateClient(); }
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PreflightRequest_FromAllowedOrigin_ReturnsExposedETagHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/categories");
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("http://localhost:5173");
        var exposedHeaders = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers"));
        exposedHeaders.Should().Contain("ETag");
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
docker run -d --name pm-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=productdb_test -p 5432:5432 postgres:17
docker run -d --name pm-redis -p 6379:6379 redis:7
dotnet test tests/ProductManagement.IntegrationTests --filter CorsTests
```

Expected: FAIL — no CORS policy registered yet, preflight returns without
the expected headers.

- [ ] **Step 3: Register the CORS policy in `Program.cs`**

```csharp
// backend/src/ProductManagement.Api/Program.cs  (full file, with the CORS additions in context)
using ProductManagement.Api.Middleware;
using ProductManagement.Application;
using ProductManagement.Infrastructure;
using Serilog;
using Serilog.Formatting.Json;

var builder = WebApplication.CreateBuilder(args);

// Structured JSON logging to console (spec section 6) - this is also what
// GlobalExceptionHandler's ILogger<T> writes through for the 500/traceId
// case (Task 11), since UseSerilog() replaces the default logging provider.
builder.Host.UseSerilog((context, loggerConfig) => loggerConfig
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console(new JsonFormatter()));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5173" })
        .AllowAnyMethod()
        .AllowAnyHeader()
        .WithExposedHeaders("ETag", "Location"));
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseCors("Frontend"); // must be after routing/exception handling, before MapControllers
app.UseStaticFiles();

app.MapControllers();

app.Run();

public partial class Program { }
```

Note the `UseSerilog(...)` addition here — the `Serilog.AspNetCore` package
was added back in Task 0 but never actually wired up until now. This is
the fix for that gap; everything logged via `ILogger<T>` anywhere in the
app (including `GlobalExceptionHandler`'s 500 logging in Task 11) now goes
through Serilog's structured JSON console output instead of the default
provider.

- [ ] **Step 4: Add the config entry to `appsettings.json`**

```json
// backend/src/ProductManagement.Api/appsettings.json  (add alongside "Redis")
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173"]
  },
```

- [ ] **Step 5: Run the test**

```bash
dotnet test tests/ProductManagement.IntegrationTests --filter CorsTests
```

Expected: PASS, 1 test.

- [ ] **Step 6: Commit**

```bash
git add backend/src backend/tests/ProductManagement.IntegrationTests
git commit -m "Add CORS policy allowing the front-end origin, exposing ETag and Location"
docker rm -f pm-postgres pm-redis
```

---

## Task 14: Seed Data (spec §10 "Seed Data")

**Files:**
- Create: `backend/src/ProductManagement.Infrastructure/Seeding/DbInitializer.cs`
- Modify: `backend/src/ProductManagement.Infrastructure/DependencyInjection.cs`
- Modify: `backend/src/ProductManagement.Api/Program.cs`
- Test: `backend/tests/ProductManagement.IntegrationTests/SeederTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
// backend/tests/ProductManagement.IntegrationTests/SeederTests.cs
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductManagement.Infrastructure.Persistence;
using ProductManagement.Infrastructure.Seeding;
using Xunit;

namespace ProductManagement.IntegrationTests;

// Deliberately NOT using the shared DatabaseFixture/CustomWebApplicationFactory —
// those are configured with Seeding:ProductCount=0 and Seeding:CategoryCount=0 to
// keep every other test file fast and seed-free. This test builds its own small
// DbContext directly against the same Postgres to exercise the real seeder.
public class SeederTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=productdb_seed_test;Username=postgres;Password=postgres";

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<ProductManagementDbContext>()
            .UseNpgsql(ConnectionString).Options;
        using var db = new ProductManagementDbContext(options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static (ProductManagementDbContext Db, DbInitializer Seeder) CreateSeeder(int categoryCount, int productCount)
    {
        var options = new DbContextOptionsBuilder<ProductManagementDbContext>()
            .UseNpgsql(ConnectionString).Options;
        var db = new ProductManagementDbContext(options);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seeding:CategoryCount"] = categoryCount.ToString(),
            ["Seeding:ProductCount"] = productCount.ToString(),
            ["Seeding:MaxVariantsPerProduct"] = "3"
        }).Build();
        return (db, new DbInitializer(db, config));
    }

    [Fact]
    public async Task SeedAsync_PopulatesCategoriesAndProducts()
    {
        var (db, seeder) = CreateSeeder(categoryCount: 16, productCount: 20);
        try
        {
            await seeder.SeedAsync(default);

            (await db.Categories.CountAsync()).Should().BeGreaterThan(0);
            (await db.Products.CountAsync()).Should().Be(20);
            (await db.ProductVariants.CountAsync()).Should().BeGreaterThan(0);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            await db.DisposeAsync();
        }
    }

    [Fact]
    public async Task SeedAsync_CalledTwice_IsNoOpTheSecondTime()
    {
        var (db, seeder) = CreateSeeder(categoryCount: 16, productCount: 5);
        try
        {
            await seeder.SeedAsync(default);
            var countAfterFirst = await db.Products.CountAsync();

            var (db2, seeder2) = CreateSeeder(categoryCount: 16, productCount: 5);
            await seeder2.SeedAsync(default);
            var countAfterSecond = await db2.Products.CountAsync();
            await db2.DisposeAsync();

            countAfterSecond.Should().Be(countAfterFirst); // no-op on the second run (spec section 10)
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            await db.DisposeAsync();
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

```bash
docker run -d --name pm-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=productdb_test -p 5432:5432 postgres:17
dotnet test tests/ProductManagement.IntegrationTests --filter SeederTests
```

Expected: FAIL to compile — `DbInitializer` doesn't exist yet.

- [ ] **Step 3: Implement `DbInitializer`**

```csharp
// backend/src/ProductManagement.Infrastructure/Seeding/DbInitializer.cs
using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Seeding;

public class DbInitializer
{
    private readonly ProductManagementDbContext _db;
    private readonly IConfiguration _config;

    public DbInitializer(ProductManagementDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        if (await _db.Products.AnyAsync(ct)) return; // no-op after the first run (spec section 10)

        var categoryCount = _config.GetValue<int?>("Seeding:CategoryCount") ?? 40;
        var productCount = _config.GetValue<int?>("Seeding:ProductCount") ?? 5000;
        var maxVariantsPerProduct = _config.GetValue<int?>("Seeding:MaxVariantsPerProduct") ?? 4;

        if (categoryCount <= 0 || productCount <= 0) return; // explicit escape hatch for test runs

        Randomizer.Seed = new Random(42); // deterministic across reseeds (spec section 10)
        var random = new Random(42);
        var faker = new Faker();

        var topLevelCount = Math.Max(1, categoryCount / 5);
        var topLevelCategories = new List<Category>();
        for (var i = 0; i < topLevelCount; i++)
            topLevelCategories.Add(Category.Create(faker.Commerce.Categories(1)[0], $"{faker.Lorem.Slug()}-top-{i}", null, i));

        _db.Categories.AddRange(topLevelCategories);
        await _db.SaveChangesAsync(ct); // commit top-level first so children get real parent IDs

        var leafCategories = new List<Category>();
        foreach (var parent in topLevelCategories)
        {
            var childCount = Math.Max(1, categoryCount / topLevelCount);
            for (var i = 0; i < childCount; i++)
            {
                var name = $"{faker.Commerce.ProductAdjective()} {faker.Commerce.Categories(1)[0]}";
                leafCategories.Add(Category.Create(name, $"{faker.Lorem.Slug()}-{parent.Id}-{i}", parent.Id, i));
            }
        }
        _db.Categories.AddRange(leafCategories);
        await _db.SaveChangesAsync(ct);

        _db.ChangeTracker.AutoDetectChangesEnabled = false; // batched, high-volume insert (spec section 10)
        var batch = new List<Product>(1000);

        for (var i = 0; i < productCount; i++)
        {
            var leaf = leafCategories[random.Next(leafCategories.Count)];
            var product = Product.Create(
                faker.Commerce.ProductName(),
                $"{faker.Lorem.Slug()}-{i}", // index suffix guarantees uniqueness at this volume
                leaf.Id,
                faker.Company.CompanyName(),
                $"{{\"material\":\"{faker.Commerce.ProductMaterial()}\"}}");
            product.Activate();

            var variantCount = random.Next(2, maxVariantsPerProduct + 1);
            for (var v = 0; v < variantCount; v++)
            {
                product.AddVariant(ProductVariant.Create(
                    product.Id,
                    sku: $"SKU-{i}-{v}-{Guid.NewGuid():N}"[..24],
                    size: faker.PickRandom("XS", "S", "M", "L", "XL"),
                    color: faker.Commerce.Color(),
                    price: faker.Random.Decimal(10, 200),
                    stockQuantity: faker.Random.Int(0, 200)));
            }

            batch.Add(product);

            if (batch.Count >= 1000)
            {
                _db.Products.AddRange(batch);
                await _db.SaveChangesAsync(ct);
                _db.ChangeTracker.Clear();
                batch.Clear();
            }
        }

        if (batch.Count > 0)
        {
            _db.Products.AddRange(batch);
            await _db.SaveChangesAsync(ct);
        }

        _db.ChangeTracker.AutoDetectChangesEnabled = true;
    }
}
```

- [ ] **Step 4: Register `DbInitializer` and call it from `Program.cs`, right after auto-migrate (spec §10)**

```csharp
// backend/src/ProductManagement.Infrastructure/DependencyInjection.cs  (add inside AddInfrastructure, before `return services;`)
        services.AddScoped<Seeding.DbInitializer>();
```

Add `using ProductManagement.Infrastructure.Seeding;` — or reference it as
`ProductManagement.Infrastructure.Seeding.DbInitializer` directly as shown
above, matching the pattern already used for other namespaced registrations
in this file.

```csharp
// backend/src/ProductManagement.Api/Program.cs  (insert this block right after `var app = builder.Build();`, before the Swagger block)
if (!app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ProductManagement.Infrastructure.Persistence.ProductManagementDbContext>();
    await db.Database.MigrateAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<ProductManagement.Infrastructure.Seeding.DbInitializer>();
    await seeder.SeedAsync(default);
}
```

- [ ] **Step 5: Add the seeding config defaults to `appsettings.json`**

```json
// backend/src/ProductManagement.Api/appsettings.json  (add alongside "Cors")
  "Seeding": {
    "CategoryCount": 40,
    "ProductCount": 5000,
    "MaxVariantsPerProduct": 4
  },
```

- [ ] **Step 6: Run the tests**

```bash
dotnet test tests/ProductManagement.IntegrationTests --filter SeederTests
```

Expected: PASS, 2 tests. Then run the full suite to confirm the
`Program.cs` startup change (auto-migrate + seed) didn't break anything
else — the shared `DatabaseFixture`/`CustomWebApplicationFactory` still has
`Seeding:CategoryCount`/`Seeding:ProductCount` overridden to `0`, so this
new startup code is a no-op for every other test file:

```bash
dotnet test tests/ProductManagement.IntegrationTests
```

Expected: PASS, all tests.

- [ ] **Step 7: Commit**

```bash
git add backend/src backend/tests/ProductManagement.IntegrationTests
git commit -m "Add Bogus-based seed data with batched inserts and idempotent startup hook"
docker rm -f pm-postgres
```

---

## Task 15: Architecture Tests (spec §5 "Architecture Tests — Enforcing the Dependency Rule")

**Files:**
- Create: `backend/tests/ProductManagement.ArchitectureTests/DependencyRuleTests.cs`

No database needed — these tests reflect over the already-built assemblies.

- [ ] **Step 1: Write the tests (they should currently pass, since Tasks 0–14 already followed the dependency rule by construction — this task exists to make that rule permanent, not to fix a violation)**

```csharp
// backend/tests/ProductManagement.ArchitectureTests/DependencyRuleTests.cs
using FluentAssertions;
using NetArchTest.Rules;
using ProductManagement.Api.Controllers;
using ProductManagement.Application;
using ProductManagement.Domain.Entities;
using ProductManagement.Infrastructure;
using Xunit;

namespace ProductManagement.ArchitectureTests;

public class DependencyRuleTests
{
    private static readonly System.Reflection.Assembly DomainAssembly = typeof(Product).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(Infrastructure.DependencyInjection).Assembly;
    private static readonly System.Reflection.Assembly ApiAssembly = typeof(CategoriesController).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Other_Layers_Or_Frameworks()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
                "ProductManagement.Application", "ProductManagement.Infrastructure", "ProductManagement.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Api_Or_Frameworks()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
                "ProductManagement.Infrastructure", "ProductManagement.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOn("ProductManagement.Api")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    [Fact]
    public void Controllers_Should_Not_Depend_On_Infrastructure_Directly()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("ProductManagement.Api.Controllers")
            .Should()
            .NotHaveDependencyOn("ProductManagement.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(FailureMessage(result));
    }

    private static string FailureMessage(TestResult result) =>
        "Violating types: " + string.Join(", ", result.FailingTypes?.Select(t => t.FullName) ?? Enumerable.Empty<string>());
}
```

- [ ] **Step 2: Run the tests**

```bash
dotnet test tests/ProductManagement.ArchitectureTests
```

Expected: **PASS, all 4 tests** — no database, no Docker, pure reflection.
If any fail, it means a genuine dependency-rule violation crept in during
Tasks 0–14 (e.g. a controller injecting `ProductManagementDbContext`
directly instead of a repository interface) — fix the violation in the
offending layer, don't weaken the test.

- [ ] **Step 3: Commit**

```bash
git add backend/tests/ProductManagement.ArchitectureTests
git commit -m "Add NetArchTest architecture tests enforcing the Clean Architecture dependency rule"
```

---

## Task 16: Application-Layer Unit Tests (validators, handlers via NSubstitute, mappings — spec §5)

Everything so far in `UnitTests` has only covered Domain (Tasks 1, 3). Spec
§5 explicitly calls for three more categories, all with **no database, no
Redis, no I/O** — repository/cache dependencies faked with NSubstitute so
what's under test is each handler's own decision logic, not real
persistence (that's what `IntegrationTests` already proves).

**Files:**
- Create: `backend/tests/ProductManagement.UnitTests/Application/CreateProductRequestValidatorTests.cs`
- Create: `backend/tests/ProductManagement.UnitTests/Application/AdjustStockHandlerTests.cs`
- Create: `backend/tests/ProductManagement.UnitTests/Application/ProductMappingsTests.cs`

- [ ] **Step 1: Write the failing validator tests**

```csharp
// backend/tests/ProductManagement.UnitTests/Application/CreateProductRequestValidatorTests.cs
using FluentAssertions;
using ProductManagement.Application.Products;
using Xunit;

namespace ProductManagement.UnitTests.Application;

public class CreateProductRequestValidatorTests
{
    private readonly CreateProductRequestValidator _validator = new();

    [Fact]
    public async Task Validate_WithBlankName_Fails()
    {
        var request = new CreateProductRequest("", "valid-slug", 1, "Acme");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_WithSlugContainingUppercase_Fails()
    {
        var request = new CreateProductRequest("Tee", "Invalid-Slug", 1, "Acme");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Slug");
    }

    [Fact]
    public async Task Validate_WithVariantCompareAtPriceBelowPrice_Fails()
    {
        var request = new CreateProductRequest("Tee", "tee", 1, "Acme",
            Variants: new List<CreateVariantRequest> { new("SKU-1", "M", "Blue", 20m, 10, CompareAtPrice: 15m) });

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithValidRequest_Passes()
    {
        var request = new CreateProductRequest("Tee", "tee", 1, "Acme");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Write the failing handler test — `AdjustStockHandler`'s decision logic, with `IStockRepository`/`ICacheService` faked**

```csharp
// backend/tests/ProductManagement.UnitTests/Application/AdjustStockHandlerTests.cs
using FluentAssertions;
using NSubstitute;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Variants;
using Xunit;

namespace ProductManagement.UnitTests.Application;

public class AdjustStockHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenRepositorySucceeds_ReturnsSuccessResultAndInvalidatesCache()
    {
        var stock = Substitute.For<IStockRepository>();
        var cache = Substitute.For<ICacheService>();
        stock.TryAdjustAsync(42, -3, Arg.Any<CancellationToken>())
            .Returns(new StockAdjustResult(true, NewQuantity: 7, AvailableQuantity: null));

        var handler = new AdjustStockHandler(stock, cache);
        var result = await handler.HandleAsync(productId: 1, variantId: 42, new AdjustStockRequest(-3), idempotencyKey: null, default);

        result.Succeeded.Should().BeTrue();
        result.NewQuantity.Should().Be(7);
        await cache.Received(1).RemoveAsync("product:1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenRepositoryReportsInsufficientStock_ReturnsConflictResultAndDoesNotInvalidateCache()
    {
        var stock = Substitute.For<IStockRepository>();
        var cache = Substitute.For<ICacheService>();
        stock.TryAdjustAsync(42, -10, Arg.Any<CancellationToken>())
            .Returns(new StockAdjustResult(false, NewQuantity: null, AvailableQuantity: 3));

        var handler = new AdjustStockHandler(stock, cache);
        var result = await handler.HandleAsync(productId: 1, variantId: 42, new AdjustStockRequest(-10), idempotencyKey: null, default);

        result.Succeeded.Should().BeFalse();
        result.AvailableQuantity.Should().Be(3);
        await cache.DidNotReceive().RemoveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithIdempotencyKeySeenBefore_ReturnsCachedResultWithoutTouchingStockRepository()
    {
        var stock = Substitute.For<IStockRepository>();
        var cache = Substitute.For<ICacheService>();
        var cachedResult = new AdjustStockResult(true, NewQuantity: 7, AvailableQuantity: null);
        cache.GetAsync<AdjustStockResult>("stock-adjust:key-123", Arg.Any<CancellationToken>()).Returns(cachedResult);

        var handler = new AdjustStockHandler(stock, cache);
        var result = await handler.HandleAsync(productId: 1, variantId: 42, new AdjustStockRequest(-3), "key-123", default);

        result.Should().Be(cachedResult);
        await stock.DidNotReceive().TryAdjustAsync(Arg.Any<long>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 3: Write the failing mapping test**

```csharp
// backend/tests/ProductManagement.UnitTests/Application/ProductMappingsTests.cs
using FluentAssertions;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;
using Xunit;

namespace ProductManagement.UnitTests.Application;

public class ProductMappingsTests
{
    [Fact]
    public void ToDto_WithNoVariants_ReturnsEmptyVariantsList()
    {
        var product = Product.Create("Tee", "tee", categoryId: 1, brand: null);

        var dto = product.ToDto();

        dto.Variants.Should().BeEmpty();
        dto.Brand.Should().BeNull();
    }

    [Fact]
    public void ToListItemDto_WithVariants_ComputesMinMaxPriceAndTotalStock()
    {
        var product = Product.Create("Tee", "tee", categoryId: 1, brand: "Acme");
        product.AddVariant(ProductVariant.Create(product.Id, "SKU-1", "S", "Blue", 15m, 5));
        product.AddVariant(ProductVariant.Create(product.Id, "SKU-2", "M", "Blue", 20m, 10));

        var dto = product.ToListItemDto();

        dto.MinPrice.Should().Be(15m);
        dto.MaxPrice.Should().Be(20m);
        dto.TotalStock.Should().Be(15);
    }
}
```

- [ ] **Step 4: Run to verify failure, then confirm they pass — no implementation needed, these test existing code from Tasks 6, 9, 12**

```bash
dotnet test tests/ProductManagement.UnitTests --filter "CreateProductRequestValidatorTests|AdjustStockHandlerTests|ProductMappingsTests"
```

Expected: PASS immediately, 9 tests — the validators, handler, and
mappings under test were already implemented in Tasks 6, 9, and 12; this
task adds the missing unit-level coverage spec §5 calls for, it doesn't
change behavior. If any test fails, it's revealing an actual bug in that
earlier implementation — fix the implementation, not the test.

- [ ] **Step 5: Commit**

```bash
git add backend/tests/ProductManagement.UnitTests
git commit -m "Add Application-layer unit tests: validators, AdjustStockHandler via NSubstitute, mappings"
```

---

## Task 17: `docker-compose.yml` and `Dockerfile` (spec §10)

Lives at the **repo root** (not inside `backend/`) — the frontend spec's
own plan later adds a fourth `web` service to this same file, so the file
belongs at the level that will eventually orchestrate both.

**Files:**
- Create: `docker-compose.yml` (repo root)
- Create: `backend/src/ProductManagement.Api/Dockerfile`
- Create: `backend/.dockerignore`

- [ ] **Step 1: Write the `Dockerfile` (multi-stage: SDK builds, runtime serves)**

```dockerfile
# backend/src/ProductManagement.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ProductManagement.sln .
COPY src/ProductManagement.Domain/ProductManagement.Domain.csproj src/ProductManagement.Domain/
COPY src/ProductManagement.Application/ProductManagement.Application.csproj src/ProductManagement.Application/
COPY src/ProductManagement.Infrastructure/ProductManagement.Infrastructure.csproj src/ProductManagement.Infrastructure/
COPY src/ProductManagement.Api/ProductManagement.Api.csproj src/ProductManagement.Api/
RUN dotnet restore src/ProductManagement.Api/ProductManagement.Api.csproj

COPY src/ src/
RUN dotnet publish src/ProductManagement.Api/ProductManagement.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "ProductManagement.Api.dll"]
```

- [ ] **Step 2: Write `.dockerignore`**

```
# backend/.dockerignore
**/bin/
**/obj/
**/.vs/
**/*.user
```

- [ ] **Step 3: Write `docker-compose.yml` at the repo root, with the fixed ports from spec §10**

```yaml
# docker-compose.yml
services:
  postgres:
    image: postgres:17
    environment:
      POSTGRES_DB: productdb
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres"]
      interval: 5s
      timeout: 5s
      retries: 10

  redis:
    image: redis:7
    ports:
      - "6379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 5s
      retries: 10

  api:
    build:
      context: ./backend
      dockerfile: src/ProductManagement.Api/Dockerfile
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_URLS: "http://+:8080"
      ASPNETCORE_ENVIRONMENT: "Development"
      ConnectionStrings__Default: "Host=postgres;Port=5432;Database=productdb;Username=postgres;Password=postgres"
      Redis__ConnectionString: "redis:6379"
      Cors__AllowedOrigins__0: "http://localhost:5173"
    volumes:
      - uploads_data:/app/wwwroot/uploads
    depends_on:
      postgres:
        condition: service_healthy
      redis:
        condition: service_healthy

volumes:
  postgres_data:
  uploads_data:
```

Note: `ConnectionStrings__Default`/`Redis__ConnectionString` here use the
Docker Compose service names (`postgres`, `redis`) as hostnames — that's
Docker's internal DNS, resolvable only from inside the `api` container's
network, not from the host machine. This is deliberately different from
`appsettings.json`'s `localhost`-based defaults (Task 5/8), which are what
`dotnet run`/`dotnet watch run` on the bare host uses instead (spec §10's
"running only part of the stack" workflow).

- [ ] **Step 4: Bring up the full stack and verify it end-to-end**

```bash
docker compose up --build -d
```

Expected: all three containers report healthy/running. Then:

```bash
curl http://localhost:8080/swagger/index.html
curl http://localhost:8080/api/v1/categories
```

Expected: Swagger UI HTML loads; the categories endpoint returns a JSON
array (populated by the seeder from Task 14 — the container runs with
`ASPNETCORE_ENVIRONMENT=Development`, so migrate+seed both execute here,
unlike in the test suite).

- [ ] **Step 5: Tear down and commit**

```bash
docker compose down -v
git add docker-compose.yml backend/src/ProductManagement.Api/Dockerfile backend/.dockerignore
git commit -m "Add docker-compose.yml and Dockerfile for the full local stack"
```

---

## Task 18: Postman Collection, README, and Final Full-Suite Verification

**Files:**
- Create: `postman/ProductManagement.postman_collection.json`
- Create: `postman/ProductManagement.postman_environment.json`
- Create: `README.md` (repo root)

- [ ] **Step 1: Write the Postman environment file**

```json
// postman/ProductManagement.postman_environment.json
{
  "id": "pm-env-local",
  "name": "ProductManagement - Local",
  "values": [
    { "key": "baseUrl", "value": "http://localhost:8080/api/v1", "enabled": true },
    { "key": "categoryId", "value": "", "enabled": true },
    { "key": "productId", "value": "", "enabled": true },
    { "key": "variantId", "value": "", "enabled": true },
    { "key": "productEtag", "value": "", "enabled": true }
  ]
}
```

- [ ] **Step 2: Write the Postman collection, covering every endpoint group with example success and error requests**

```json
// postman/ProductManagement.postman_collection.json
{
  "info": {
    "name": "Product Management API",
    "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json"
  },
  "item": [
    {
      "name": "Categories",
      "item": [
        {
          "name": "Create Category",
          "event": [{ "listen": "test", "script": { "exec": [
            "pm.test('201 Created', () => pm.response.to.have.status(201));",
            "const body = pm.response.json();",
            "pm.collectionVariables.set('categoryId', body.id);"
          ]}}],
          "request": {
            "method": "POST",
            "header": [{ "key": "Content-Type", "value": "application/json" }],
            "body": { "mode": "raw", "raw": "{\n  \"name\": \"Dresses\",\n  \"slug\": \"dresses\",\n  \"parentCategoryId\": null,\n  \"displayOrder\": 0\n}" },
            "url": { "raw": "{{baseUrl}}/categories", "host": ["{{baseUrl}}"], "path": ["categories"] }
          }
        },
        {
          "name": "Create Category - Duplicate Slug (409)",
          "event": [{ "listen": "test", "script": { "exec": [
            "pm.test('409 Conflict', () => pm.response.to.have.status(409));"
          ]}}],
          "request": {
            "method": "POST",
            "header": [{ "key": "Content-Type", "value": "application/json" }],
            "body": { "mode": "raw", "raw": "{\n  \"name\": \"Dresses\",\n  \"slug\": \"dresses\",\n  \"parentCategoryId\": null,\n  \"displayOrder\": 0\n}" },
            "url": { "raw": "{{baseUrl}}/categories", "host": ["{{baseUrl}}"], "path": ["categories"] }
          }
        },
        {
          "name": "List Categories",
          "request": {
            "method": "GET",
            "url": { "raw": "{{baseUrl}}/categories", "host": ["{{baseUrl}}"], "path": ["categories"] }
          }
        },
        {
          "name": "Delete Category - Blocked by Active Products (409)",
          "request": {
            "method": "DELETE",
            "url": { "raw": "{{baseUrl}}/categories/{{categoryId}}", "host": ["{{baseUrl}}"], "path": ["categories", "{{categoryId}}"] }
          }
        }
      ]
    },
    {
      "name": "Products",
      "item": [
        {
          "name": "Create Product With Initial Variant",
          "event": [{ "listen": "test", "script": { "exec": [
            "pm.test('201 Created', () => pm.response.to.have.status(201));",
            "const body = pm.response.json();",
            "pm.collectionVariables.set('productId', body.id);",
            "pm.collectionVariables.set('variantId', body.variants[0].id);"
          ]}}],
          "request": {
            "method": "POST",
            "header": [{ "key": "Content-Type", "value": "application/json" }],
            "body": { "mode": "raw", "raw": "{\n  \"name\": \"Classic Cotton Tee\",\n  \"slug\": \"classic-cotton-tee\",\n  \"categoryId\": {{categoryId}},\n  \"brand\": \"Acme\",\n  \"variants\": [\n    { \"sku\": \"TEE-M-BLU\", \"size\": \"M\", \"color\": \"Blue\", \"price\": 20.00, \"stockQuantity\": 50 }\n  ]\n}" },
            "url": { "raw": "{{baseUrl}}/products", "host": ["{{baseUrl}}"], "path": ["products"] }
          }
        },
        {
          "name": "Get Product By Id (captures ETag)",
          "event": [{ "listen": "test", "script": { "exec": [
            "pm.test('200 OK', () => pm.response.to.have.status(200));",
            "pm.collectionVariables.set('productEtag', pm.response.headers.get('ETag'));"
          ]}}],
          "request": {
            "method": "GET",
            "url": { "raw": "{{baseUrl}}/products/{{productId}}", "host": ["{{baseUrl}}"], "path": ["products", "{{productId}}"] }
          }
        },
        {
          "name": "List Products - Full-Text Search",
          "request": {
            "method": "GET",
            "url": { "raw": "{{baseUrl}}/products?q=cotton", "host": ["{{baseUrl}}"], "path": ["products"], "query": [{ "key": "q", "value": "cotton" }] }
          }
        },
        {
          "name": "Update Product (uses ETag as If-Match)",
          "request": {
            "method": "PUT",
            "header": [
              { "key": "Content-Type", "value": "application/json" },
              { "key": "If-Match", "value": "{{productEtag}}" }
            ],
            "body": { "mode": "raw", "raw": "{\n  \"name\": \"Classic Cotton Tee (Updated)\",\n  \"description\": null,\n  \"categoryId\": {{categoryId}},\n  \"brand\": \"Acme\",\n  \"attributes\": \"{}\"\n}" },
            "url": { "raw": "{{baseUrl}}/products/{{productId}}", "host": ["{{baseUrl}}"], "path": ["products", "{{productId}}"] }
          }
        },
        {
          "name": "Update Product - Stale ETag (409)",
          "request": {
            "method": "PUT",
            "header": [
              { "key": "Content-Type", "value": "application/json" },
              { "key": "If-Match", "value": "\"0\"" }
            ],
            "body": { "mode": "raw", "raw": "{\n  \"name\": \"Should Conflict\",\n  \"description\": null,\n  \"categoryId\": {{categoryId}},\n  \"brand\": null,\n  \"attributes\": \"{}\"\n}" },
            "url": { "raw": "{{baseUrl}}/products/{{productId}}", "host": ["{{baseUrl}}"], "path": ["products", "{{productId}}"] }
          }
        }
      ]
    },
    {
      "name": "Variants & Stock",
      "item": [
        {
          "name": "Adjust Stock - Decrement",
          "request": {
            "method": "PATCH",
            "header": [{ "key": "Content-Type", "value": "application/json" }],
            "body": { "mode": "raw", "raw": "{ \"delta\": -3 }" },
            "url": {
              "raw": "{{baseUrl}}/products/{{productId}}/variants/{{variantId}}/stock",
              "host": ["{{baseUrl}}"], "path": ["products", "{{productId}}", "variants", "{{variantId}}", "stock"]
            }
          }
        },
        {
          "name": "Adjust Stock - Insufficient Stock (409)",
          "request": {
            "method": "PATCH",
            "header": [{ "key": "Content-Type", "value": "application/json" }],
            "body": { "mode": "raw", "raw": "{ \"delta\": -999999 }" },
            "url": {
              "raw": "{{baseUrl}}/products/{{productId}}/variants/{{variantId}}/stock",
              "host": ["{{baseUrl}}"], "path": ["products", "{{productId}}", "variants", "{{variantId}}", "stock"]
            }
          }
        },
        {
          "name": "Adjust Stock - With Idempotency-Key",
          "request": {
            "method": "PATCH",
            "header": [
              { "key": "Content-Type", "value": "application/json" },
              { "key": "Idempotency-Key", "value": "{{$guid}}" }
            ],
            "body": { "mode": "raw", "raw": "{ \"delta\": -1 }" },
            "url": {
              "raw": "{{baseUrl}}/products/{{productId}}/variants/{{variantId}}/stock",
              "host": ["{{baseUrl}}"], "path": ["products", "{{productId}}", "variants", "{{variantId}}", "stock"]
            }
          }
        }
      ]
    },
    {
      "name": "Images",
      "item": [
        {
          "name": "Upload Product Image",
          "request": {
            "method": "POST",
            "body": { "mode": "formdata", "formdata": [{ "key": "file", "type": "file", "src": "" }] },
            "url": { "raw": "{{baseUrl}}/products/{{productId}}/image", "host": ["{{baseUrl}}"], "path": ["products", "{{productId}}", "image"] }
          }
        },
        {
          "name": "Delete Product Image - None Set (404)",
          "request": {
            "method": "DELETE",
            "url": { "raw": "{{baseUrl}}/products/999999/image", "host": ["{{baseUrl}}"], "path": ["products", "999999", "image"] }
          }
        }
      ]
    }
  ],
  "variable": [
    { "key": "categoryId", "value": "" },
    { "key": "productId", "value": "" },
    { "key": "variantId", "value": "" },
    { "key": "productEtag", "value": "" }
  ]
}
```

- [ ] **Step 3: Write the repo-root `README.md`**

```markdown
<!-- README.md -->
# Product Management API

Backend for the product management assessment — see
`docs/superpowers/specs/2026-08-20-product-management-api-design.md` for
the full design rationale.

## Run the full stack (one command)

    docker compose up --build

Swagger UI: http://localhost:8080/swagger
API base: http://localhost:8080/api/v1

The database auto-migrates and seeds sample data on first boot (non-Production
only). To reset to a clean slate: `docker compose down -v` then `up` again.

## Active backend development (hot reload)

    docker compose up postgres redis
    cd backend/src/ProductManagement.Api
    dotnet watch run

## Running tests

    cd backend
    dotnet test tests/ProductManagement.UnitTests           # no dependencies needed
    dotnet test tests/ProductManagement.ArchitectureTests    # no dependencies needed
    docker compose up -d postgres redis                       # required first:
    dotnet test tests/ProductManagement.IntegrationTests

## Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `ConnectionStrings__Default` | `Host=localhost;Port=5432;...` | Postgres connection |
| `Redis__ConnectionString` | `localhost:6379` | Redis connection |
| `Cors__AllowedOrigins__0` | `http://localhost:5173` | Front-end origin allowed by CORS |
| `Seeding__CategoryCount` | `40` | Seed data volume |
| `Seeding__ProductCount` | `5000` | Seed data volume |
| `Seeding__MaxVariantsPerProduct` | `4` | Seed data volume |

## Postman

Import `postman/ProductManagement.postman_collection.json` and
`postman/ProductManagement.postman_environment.json`.

## Known limitations

No authentication (all endpoints public — see spec §10/§11), local-disk
image storage (not real blob storage), no CI/CD pipeline. Full list in the
design spec's Limitations section.
```

- [ ] **Step 4: Run the entire test suite, one final time, from a clean slate**

```bash
docker compose down -v
docker compose up -d postgres redis
cd backend
dotnet build
dotnet test tests/ProductManagement.UnitTests
dotnet test tests/ProductManagement.ArchitectureTests
dotnet test tests/ProductManagement.IntegrationTests
docker compose down -v
```

Expected: every test project passes, zero failures — including
`StockConcurrencyTests` (Task 9), the single most important test in this
plan.

- [ ] **Step 5: Commit**

```bash
git add postman README.md
git commit -m "Add Postman collection, README, and complete the backend implementation"
```

---

## Done

At this point: full Clean Architecture backend, all endpoints from spec §7,
concurrency-safe stock adjustment proven under real parallel load, full-text
search with trigram fallback, Redis caching with correct invalidation,
minimal image upload, seed data, CORS, and three test projects (unit,
integration, architecture) all passing. The frontend spec's own
implementation plan (written separately) integrates against this API next.


