namespace ProductManagement.Application.Categories;

public sealed record CategoryDto(
    long Id,
    string Name,
    string Slug,
    long? ParentCategoryId,
    int DisplayOrder,
    bool IsActive);
