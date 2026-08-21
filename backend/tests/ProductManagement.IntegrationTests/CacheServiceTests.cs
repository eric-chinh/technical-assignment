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
