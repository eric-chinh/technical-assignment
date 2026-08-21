using FluentAssertions;
using ProductManagement.Domain.Entities;
using ProductManagement.Domain.Exceptions;
using Xunit;

namespace ProductManagement.UnitTests.Domain;

public class CategoryTests
{
    [Fact]
    public void Create_WithValidNameAndSlug_SetsProperties()
    {
        var category = Category.Create(name: "Dresses", slug: "dresses", parentCategoryId: null);

        category.Name.Should().Be("Dresses");
        category.Slug.Should().Be("dresses");
        category.ParentCategoryId.Should().BeNull();
        category.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankName_Throws(string blankName)
    {
        var act = () => Category.Create(blankName, "slug", null);

        act.Should().Throw<DomainException>().WithMessage("*name*");
    }
}
