using FluentAssertions;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;
using Xunit;

namespace ProductManagement.UnitTests.Application;

public class ProductMappingsTests
{
    [Fact]
    public void ToDto_WithNoVariants_ReturnsEmptyVariantsList()
    {
        var product = Product.Create("Tee", "tee", categoryId: 1, brand: null);

        var dto = product.ToDto();

        dto.Variants.Should().BeEmpty();
        dto.Brand.Should().BeNull();
    }

    [Fact]
    public void ToListItemDto_WithVariants_ComputesMinMaxPriceAndTotalStock()
    {
        var product = Product.Create("Tee", "tee", categoryId: 1, brand: "Acme");
        product.AddVariant(ProductVariant.Create(product.Id, "SKU-1", "S", "Blue", 15m, 5));
        product.AddVariant(ProductVariant.Create(product.Id, "SKU-2", "M", "Blue", 20m, 10));

        var dto = product.ToListItemDto();

        dto.MinPrice.Should().Be(15m);
        dto.MaxPrice.Should().Be(20m);
        dto.TotalStock.Should().Be(15);
    }
}
