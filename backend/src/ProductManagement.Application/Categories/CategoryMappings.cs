using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public static class CategoryMappings
{
    public static CategoryDto ToDto(this Category category) => new(
        category.Id, category.Name, category.Slug,
        category.ParentCategoryId, category.DisplayOrder, category.IsActive);
}
