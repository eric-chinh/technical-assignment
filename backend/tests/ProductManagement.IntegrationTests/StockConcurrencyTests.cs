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
        // Arrange: one item, exactly 10 in stock.
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "Flash Sale", slug = "flash-sale", parentCategoryId = (long?)null, displayOrder = 0 });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdRef>();

        var productResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Viral Sneaker", slug = "viral-sneaker", categoryId = category!.Id, brand = "Acme",
            items = new[] { new { sku = "SNEAKER-9", price = 120.00m, qtyInStock = 10 } }
        });
        var product = await productResponse.Content.ReadFromJsonAsync<ProductWithItems>();
        var itemId = product!.Items[0].Id;

        // Act: 30 concurrent requests each trying to decrement 1 unit, against 10 in stock.
        var tasks = Enumerable.Range(0, 30).Select(_ =>
            _client.PostAsJsonAsync($"/api/v1/product-items/{itemId}/inventory/adjust", new { delta = -1 }));
        var responses = await Task.WhenAll(tasks);

        // Assert: exactly 10 succeeded, the rest got a definitive conflict - never more than 10 total decremented.
        var bodies = await Task.WhenAll(responses.Select(r => r.Content.ReadFromJsonAsync<StockResponse>()));
        var succeeded = bodies.Count(b => b!.Succeeded);
        var failed = bodies.Count(b => !b!.Succeeded);

        succeeded.Should().Be(10);
        failed.Should().Be(20);

        var finalStockResponse = await _client.GetAsync($"/api/v1/products/{product.Id}");
        var finalProduct = await finalStockResponse.Content.ReadFromJsonAsync<ProductWithItems>();
        finalProduct!.Items[0].QtyInStock.Should().Be(0); // never negative, never short-sold
    }

    private sealed record ItemRef(long Id, int QtyInStock);
    private sealed record ProductWithItems(long Id, List<ItemRef> Items);
}
