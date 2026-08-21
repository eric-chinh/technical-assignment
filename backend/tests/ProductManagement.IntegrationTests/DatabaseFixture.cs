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
