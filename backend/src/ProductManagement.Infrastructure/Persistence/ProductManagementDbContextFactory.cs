using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProductManagement.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used only by `dotnet ef` tooling to construct the DbContext for
/// generating/applying migrations. The API's composition root (wired up in a later task)
/// registers the DbContext through <see cref="DependencyInjection.AddInfrastructure"/>
/// instead - this factory is not used at runtime.
///
/// The connection string here is only used for design-time operations (e.g. `dotnet ef
/// migrations add`, which doesn't actually connect to the database). It can be overridden
/// with the CONNECTIONSTRINGS__DEFAULT environment variable, e.g. for `dotnet ef database
/// update` against a real instance.
/// </summary>
public class ProductManagementDbContextFactory : IDesignTimeDbContextFactory<ProductManagementDbContext>
{
    public ProductManagementDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULT")
            ?? "Host=localhost;Port=5432;Database=productdb;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<ProductManagementDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ProductManagementDbContext(optionsBuilder.Options);
    }
}
