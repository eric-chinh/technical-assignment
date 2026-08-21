using FluentAssertions;
using ProductManagement.Domain.Entities;
using ProductManagement.Domain.Exceptions;
using Xunit;

namespace ProductManagement.UnitTests.Domain;

public class ProductVariantTests
{
    [Fact]
    public void Create_WithNegativePrice_Throws()
    {
        var act = () => ProductVariant.Create(productId: 1, sku: "TEE-M", size: "M", color: "Blue", price: -1m, stockQuantity: 10);

        act.Should().Throw<DomainException>().WithMessage("*price*");
    }

    [Fact]
    public void Create_WithNegativeStock_Throws()
    {
        var act = () => ProductVariant.Create(productId: 1, sku: "TEE-M", size: "M", color: "Blue", price: 20m, stockQuantity: -1);

        act.Should().Throw<DomainException>().WithMessage("*stock*");
    }

    [Fact]
    public void Create_WithCompareAtPriceBelowPrice_Throws()
    {
        var act = () => ProductVariant.Create(
            productId: 1, sku: "TEE-M", size: "M", color: "Blue",
            price: 20m, stockQuantity: 10, compareAtPrice: 15m);

        act.Should().Throw<DomainException>().WithMessage("*compareAtPrice*");
    }
}
