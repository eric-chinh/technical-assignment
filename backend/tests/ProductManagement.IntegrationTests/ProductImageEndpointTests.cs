using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class ProductImageEndpointTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public ProductImageEndpointTests(DatabaseFixture fixture) => _fixture = fixture;
    public async Task InitializeAsync() { await _fixture.ResetAsync(); _client = _fixture.Factory.CreateClient(); }
    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<long> CreateProductAsync()
    {
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        { name = "Cat", slug = $"cat-{Guid.NewGuid()}", parentCategoryId = (long?)null, displayOrder = 0 });
        var category = await categoryResponse.Content.ReadFromJsonAsync<IdRef>();
        var productResponse = await _client.PostAsJsonAsync("/api/v1/products", new
        { name = "Item", slug = $"item-{Guid.NewGuid()}", categoryId = category!.Id, brand = (string?)null, variants = Array.Empty<object>() });
        var product = await productResponse.Content.ReadFromJsonAsync<IdRef>();
        return product!.Id;
    }

    private static MultipartFormDataContent BuildImageForm(byte[] bytes, string contentType, string fileName)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", fileName);
        return form;
    }

    [Fact]
    public async Task UploadImage_WithValidJpeg_Returns200WithUrl()
    {
        var productId = await CreateProductAsync();
        using var form = BuildImageForm(new byte[] { 1, 2, 3, 4 }, "image/jpeg", "photo.jpg");

        var response = await _client.PostAsync($"/api/v1/products/{productId}/image", form);
        var body = await response.Content.ReadFromJsonAsync<ImageResponse>();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.ImageUrl.Should().Contain($"/uploads/products/{productId}/");
    }

    [Fact]
    public async Task UploadImage_WithWrongContentType_Returns400()
    {
        var productId = await CreateProductAsync();
        using var form = BuildImageForm(new byte[] { 1, 2, 3, 4 }, "application/pdf", "doc.pdf");

        var response = await _client.PostAsync($"/api/v1/products/{productId}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UploadImage_OverSizeLimit_Returns400()
    {
        var productId = await CreateProductAsync();
        var oversized = new byte[6 * 1024 * 1024]; // 6 MB, over the 5 MB limit
        using var form = BuildImageForm(oversized, "image/jpeg", "big.jpg");

        var response = await _client.PostAsync($"/api/v1/products/{productId}/image", form);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteImage_WhenNoneSet_Returns404()
    {
        var productId = await CreateProductAsync();

        var response = await _client.DeleteAsync($"/api/v1/products/{productId}/image");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UploadImage_AfterProductWasAlreadyCached_GetProductReturnsTheNewImageUrl_NotStale()
    {
        var productId = await CreateProductAsync();
        // Populates the Redis product:{id} cache entry with imageUrl: null, BEFORE the upload -
        // reproduces the exact sequence a user hits: view the product, then upload an image.
        await _client.GetAsync($"/api/v1/products/{productId}");

        using var form = BuildImageForm(new byte[] { 1, 2, 3, 4 }, "image/jpeg", "photo.jpg");
        var uploadResponse = await _client.PostAsync($"/api/v1/products/{productId}/image", form);
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ImageResponse>();

        var getResponse = await _client.GetAsync($"/api/v1/products/{productId}");
        var product = await getResponse.Content.ReadFromJsonAsync<ProductRef>();

        product!.ImageUrl.Should().Be(uploaded!.ImageUrl);
    }

    [Fact]
    public async Task DeleteImage_AfterProductWasAlreadyCached_GetProductReturnsNullImageUrl_NotStale()
    {
        var productId = await CreateProductAsync();
        using var form = BuildImageForm(new byte[] { 1, 2, 3, 4 }, "image/jpeg", "photo.jpg");
        await _client.PostAsync($"/api/v1/products/{productId}/image", form);
        // Populates the cache with the image still set, BEFORE the delete.
        await _client.GetAsync($"/api/v1/products/{productId}");

        await _client.DeleteAsync($"/api/v1/products/{productId}/image");

        var getResponse = await _client.GetAsync($"/api/v1/products/{productId}");
        var product = await getResponse.Content.ReadFromJsonAsync<ProductRef>();

        product!.ImageUrl.Should().BeNull();
    }

    private sealed record IdRef(long Id);
    private sealed record ImageResponse(string ImageUrl);
    private sealed record ProductRef(long Id, string? ImageUrl);
}
