using FluentAssertions;
using ProductManagement.Application.Products;
using Xunit;

namespace ProductManagement.UnitTests.Application;

public class CreateProductRequestValidatorTests
{
    private readonly CreateProductRequestValidator _validator = new();

    [Fact]
    public async Task Validate_WithBlankName_Fails()
    {
        var request = new CreateProductRequest("", "valid-slug", 1, "Acme");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public async Task Validate_WithSlugContainingUppercase_Fails()
    {
        var request = new CreateProductRequest("Tee", "Invalid-Slug", 1, "Acme");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Slug");
    }

    [Fact]
    public async Task Validate_WithVariantCompareAtPriceBelowPrice_Fails()
    {
        var request = new CreateProductRequest("Tee", "tee", 1, "Acme",
            Variants: new List<CreateVariantRequest> { new("SKU-1", "M", "Blue", 20m, 10, CompareAtPrice: 15m) });

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_WithValidRequest_Passes()
    {
        var request = new CreateProductRequest("Tee", "tee", 1, "Acme");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }
}
