using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProductManagement.Infrastructure.Persistence;
using ProductManagement.Infrastructure.Seeding;
using Xunit;

namespace ProductManagement.IntegrationTests;

// Deliberately NOT using the shared DatabaseFixture/CustomWebApplicationFactory -
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
            (await db.ProductItems.CountAsync()).Should().BeGreaterThan(0);
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
