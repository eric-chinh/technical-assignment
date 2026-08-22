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
            items = new[]
            {
                new { sku = "TEE-M-BLU", price = 20.00m, qtyInStock = 50 }
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<ProductRef>();
        created!.Items.Should().ContainSingle(v => v.Sku == "TEE-M-BLU");
    }

    [Fact]
    public async Task GetProduct_ReturnsETagHeader()
    {
        var categoryId = await CreateCategoryAsync("tops-2");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "Tee", slug = "tee-2", categoryId, brand = (string?)null, items = Array.Empty<object>() });
        var created = await createResponse.Content.ReadFromJsonAsync<ProductRef>();

        var response = await _client.GetAsync($"/api/v1/products/{created!.Id}");

        response.Headers.ETag.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateProduct_WithStaleETag_Returns409()
    {
        var categoryId = await CreateCategoryAsync("tops-3");
        var createResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "Tee", slug = "tee-3", categoryId, brand = (string?)null, items = Array.Empty<object>() });
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
        { name = "Blue Denim Jacket", slug = "blue-denim-jacket", categoryId, brand = "Acme", items = Array.Empty<object>() });
        await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "Red Wool Scarf", slug = "red-wool-scarf", categoryId, brand = "Acme", items = Array.Empty<object>() });

        var response = await _client.GetAsync("/api/v1/products?q=denim");
        var page = await response.Content.ReadFromJsonAsync<PagedRef>();

        page!.Items.Should().ContainSingle(p => p.Name == "Blue Denim Jacket");
    }

    [Fact]
    public async Task ListProducts_ReturnsTotalCount_StableAcrossPages()
    {
        var categoryId = await CreateCategoryAsync("tops-5");
        for (var i = 0; i < 3; i++)
        {
            await _client.PostAsJsonAsync("/api/v1/products", new
            { name = $"Paged Item {i}", slug = $"paged-item-{i}", categoryId, brand = (string?)null, items = Array.Empty<object>() });
        }

        var firstPage = await _client.GetAsync($"/api/v1/products?categoryId={categoryId}&limit=2");
        var firstPageBody = await firstPage.Content.ReadFromJsonAsync<PagedRef>();

        firstPageBody!.TotalCount.Should().Be(3);
        firstPageBody.HasMore.Should().BeTrue();

        var encodedCursor = Uri.EscapeDataString(firstPageBody.NextCursor!);
        var secondPage = await _client.GetAsync($"/api/v1/products?categoryId={categoryId}&limit=2&cursor={encodedCursor}");
        var secondPageBody = await secondPage.Content.ReadFromJsonAsync<PagedRef>();

        // Total count reflects all matching rows, not just what's left after paging -
        // it must not shrink from page to page.
        secondPageBody!.TotalCount.Should().Be(3);
    }

    private sealed record CategoryRef(long Id);
    private sealed record ItemRef(long Id, string Sku);
    private sealed record ProductRef(long Id, string Name, List<ItemRef> Items);
    private sealed record ProductListItemRef(long Id, string Name);
    private sealed record PagedRef(List<ProductListItemRef> Items, string? NextCursor, bool HasMore, long TotalCount);
}
