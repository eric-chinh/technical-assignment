using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Categories;

public class ListCategoriesHandler
{
    private readonly ICategoryRepository _categories;
    public ListCategoriesHandler(ICategoryRepository categories) => _categories = categories;

    public async Task<List<CategoryDto>> HandleAsync(long? parentId, bool? activeOnly, CancellationToken ct)
    {
        var categories = await _categories.ListAsync(parentId, activeOnly, ct);
        return categories.Select(c => c.ToDto()).ToList();
    }
}
