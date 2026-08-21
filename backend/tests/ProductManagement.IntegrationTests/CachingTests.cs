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
