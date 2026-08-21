using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class DeleteCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public DeleteCategoryHandler(ICategoryRepository categories, IUnitOfWork unitOfWork, ICacheService cache)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task HandleAsync(long id, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);

        if (await _categories.HasActiveProductsAsync(id, ct))
            throw new CategoryHasActiveProductsException(id);

        _categories.Remove(category);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CategoryCacheKeys.ListKey, ct);
    }
}
