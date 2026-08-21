using FluentAssertions;
using ProductManagement.Domain.Entities;
using ProductManagement.Domain.Enums;
using ProductManagement.Domain.Exceptions;
using Xunit;

namespace ProductManagement.UnitTests.Domain;

public class ProductTests
{
    [Fact]
    public void Create_WithValidData_StartsAsDraft()
    {
        var product = Product.Create("Classic Cotton Tee", "classic-cotton-tee", categoryId: 1, brand: "Acme");

        product.Status.Should().Be(ProductStatus.Draft);
        product.Name.Should().Be("Classic Cotton Tee");
    }

    [Fact]
    public void Archive_WhenAlreadyArchived_Throws()
    {
        var product = Product.Create("Tee", "tee", categoryId: 1, brand: null);
        product.Activate();
        product.Archive();

        var act = () => product.Archive();

        act.Should().Throw<DomainException>().WithMessage("*already archived*");
    }
}
