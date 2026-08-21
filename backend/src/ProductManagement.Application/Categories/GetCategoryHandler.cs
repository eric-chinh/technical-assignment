using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class GetCategoryHandler
{
    private readonly ICategoryRepository _categories;
    public GetCategoryHandler(ICategoryRepository categories) => _categories = categories;

    public async Task<CategoryDto> HandleAsync(long id, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);
        return category.ToDto();
    }
}
