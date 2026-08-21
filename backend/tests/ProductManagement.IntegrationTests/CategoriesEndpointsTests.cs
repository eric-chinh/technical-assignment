using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ProductManagement.IntegrationTests;

[Collection("Database")]
public class CategoriesEndpointsTests : IAsyncLifetime
{
    private readonly DatabaseFixture _fixture;
    private HttpClient _client = default!;

    public CategoriesEndpointsTests(DatabaseFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        await _fixture.ResetAsync();
        _client = _fixture.Factory.CreateClient();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateCategory_WithValidData_Returns201WithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "Dresses",
            slug = "dresses",
            parentCategoryId = (long?)null,
            displayOrder = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCategory_WithDuplicateSlug_Returns409()
    {
        var payload = new { name = "Dresses", slug = "dresses", parentCategoryId = (long?)null, displayOrder = 0 };
        await _client.PostAsJsonAsync("/api/v1/categories", payload);

        var response = await _client.PostAsJsonAsync("/api/v1/categories", payload);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateCategory_WithBlankName_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "",
            slug = "blank-name",
            parentCategoryId = (long?)null,
            displayOrder = 0
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeleteCategory_WhenReferencedByActiveProduct_Returns409()
    {
        var categoryResponse = await _client.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "Dresses", slug = "dresses-2", parentCategoryId = (long?)null, displayOrder = 0
        });
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryResponseDto>();

        await _client.PostAsJsonAsync("/api/v1/products", new
        {
            name = "Maxi Dress", slug = "maxi-dress", categoryId = category!.Id, brand = "Acme"
        });

        var response = await _client.DeleteAsync($"/api/v1/categories/{category.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private sealed record CategoryResponseDto(long Id, string Name, string Slug);
}
