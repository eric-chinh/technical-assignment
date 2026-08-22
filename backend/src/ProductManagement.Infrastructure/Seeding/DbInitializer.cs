using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ProductManagement.Domain.Entities;
using ProductManagement.Infrastructure.Persistence;

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

            var itemCount = random.Next(2, maxVariantsPerProduct + 1);
            for (var v = 0; v < itemCount; v++)
            {
                product.AddItem(ProductItem.Create(
                    product.Id,
                    sku: $"SKU-{i}-{v}-{Guid.NewGuid():N}"[..24],
                    price: faker.Random.Decimal(10, 200),
                    qtyInStock: faker.Random.Int(0, 200)));
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
