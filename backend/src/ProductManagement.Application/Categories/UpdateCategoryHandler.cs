using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class UpdateCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IValidator<UpdateCategoryRequest> _validator;

    public UpdateCategoryHandler(
        ICategoryRepository categories, IUnitOfWork unitOfWork, ICacheService cache, IValidator<UpdateCategoryRequest> validator)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _validator = validator;
    }

    public async Task<CategoryDto> HandleAsync(long id, UpdateCategoryRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var category = await _categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);

        category.Update(request.Name, request.Slug, request.ParentCategoryId, request.DisplayOrder, request.IsActive);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(CategoryCacheKeys.ListKey, ct);
        return category.ToDto();
    }
}
