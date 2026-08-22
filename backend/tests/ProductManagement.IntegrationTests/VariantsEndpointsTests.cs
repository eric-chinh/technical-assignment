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
        { name = "Tee", slug, categoryId = category!.Id, brand = (string?)null, items = Array.Empty<object>() });
        var product = await productResponse.Content.ReadFromJsonAsync<IdRef>();
        return product!.Id;
    }

    [Fact]
    public async Task CreateVariant_WithDuplicateSku_Returns409()
    {
        var productId = await CreateProductAsync("tee-v1");
        var payload = new { sku = "TEE-M", price = 20.00m, qtyInStock = 10 };
        await _client.PostAsJsonAsync($"/api/v1/products/{productId}/items", payload);

        var response = await _client.PostAsJsonAsync($"/api/v1/products/{productId}/items", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteVariant_SoftDeletes_NotHardDelete()
    {
        var productId = await CreateProductAsync("tee-v2");
        var createResponse = await _client.PostAsJsonAsync($"/api/v1/products/{productId}/items", new
        { sku = "TEE-L", price = 22.00m, qtyInStock = 5 });
        var item = await createResponse.Content.ReadFromJsonAsync<IdRef>();

        var deleteResponse = await _client.DeleteAsync($"/api/v1/product-items/{item!.Id}");
        var listResponse = await _client.GetAsync($"/api/v1/products/{productId}/items");
        var list = await listResponse.Content.ReadFromJsonAsync<List<ItemRef>>();

        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        list.Should().ContainSingle(v => v.Id == item.Id && !v.IsActive);
    }

    [Fact]
    public async Task CreateVariant_AfterProductWasAlreadyCached_GetProductReturnsIt_NotStale()
    {
        var productId = await CreateProductAsync("tee-v3");
        // Populates the Redis product:{id} cache entry with an empty items list, BEFORE
        // the create - reproduces the exact sequence a user hits: view the product, then add an item.
        await _client.GetAsync($"/api/v1/products/{productId}");

        await _client.PostAsJsonAsync($"/api/v1/products/{productId}/items", new
        { sku = "TEE-XL", price = 25.00m, qtyInStock = 8 });

        var getResponse = await _client.GetAsync($"/api/v1/products/{productId}");
        var product = await getResponse.Content.ReadFromJsonAsync<ProductRef>();

        product!.Items.Should().Contain(v => v.Sku == "TEE-XL");
    }

    private sealed record IdRef(long Id);
    private sealed record ItemRef(long Id, string Sku, bool IsActive);
    private sealed record ProductRef(long Id, List<ItemRef> Items);
}
