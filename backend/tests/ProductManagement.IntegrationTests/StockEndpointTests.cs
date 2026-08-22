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

    private async Task<(long productId, long itemId)> CreateProductWithStockAsync(int stock)
    {
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "Cat", slug = $"cat-{Guid.NewGuid()}", parentCategoryId = (long?)null, displayOrder = 0 });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdRef>();

        var productResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Item", slug = $"item-{Guid.NewGuid()}", categoryId = category!.Id, brand = (string?)null,
            items = new[] { new { sku = $"SKU-{Guid.NewGuid()}", price = 10.00m, qtyInStock = stock } }
        });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductWithItems>();
        return (product!.Id, product.Items[0].Id);
    }

    [Fact]
    public async Task AdjustStock_Decrement_WithinAvailableStock_Returns200()
    {
        var (_, itemId) = await CreateProductWithStockAsync(5);

        var response = await _client.PostAsJsonAsync($"/api/v1/product-items/{itemId}/inventory/adjust", new { delta = -3 });
        var body = await response.Content.ReadFromJsonAsync<StockResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.NewQuantity.Should().Be(2);
    }

    [Fact]
    public async Task AdjustStock_DecrementBeyondAvailable_Returns409WithAvailableQuantity()
    {
        var (_, itemId) = await CreateProductWithStockAsync(2);

        var response = await _client.PostAsJsonAsync($"/api/v1/product-items/{itemId}/inventory/adjust", new { delta = -5 });
        var body = await response.Content.ReadFromJsonAsync<StockResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        body!.AvailableQuantity.Should().Be(2);
    }

    [Fact]
    public async Task AdjustStock_RepeatedWithSameIdempotencyKey_OnlyAppliesOnce()
    {
        var (_, itemId) = await CreateProductWithStockAsync(10);
        var idempotencyKey = Guid.NewGuid().ToString();

        var request1 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/product-items/{itemId}/inventory/adjust")
        { Content = JsonContent.Create(new { delta = -3 }) };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        await _client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/product-items/{itemId}/inventory/adjust")
        { Content = JsonContent.Create(new { delta = -3 }) };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await _client.SendAsync(request2);
        var body2 = await response2.Content.ReadFromJsonAsync<StockResponse>();

        body2!.NewQuantity.Should().Be(7); // still 7, NOT 4 - the retried decrement never re-applied
    }

    private sealed record IdRef(long Id);
    private sealed record ItemRef(long Id, int QtyInStock);
    private sealed record ProductWithItems(long Id, List<ItemRef> Items);
    private sealed record StockResponse(bool Succeeded, int? NewQuantity, int? AvailableQuantity);
}
