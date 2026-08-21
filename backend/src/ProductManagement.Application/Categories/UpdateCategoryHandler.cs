using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class UpdateCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<UpdateCategoryRequest> _validator;

    public UpdateCategoryHandler(ICategoryRepository categories, IUnitOfWork unitOfWork, IValidator<UpdateCategoryRequest> validator)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<CategoryDto> HandleAsync(long id, UpdateCategoryRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var category = await _categories.GetByIdAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Category), id);

        category.Update(request.Name, request.Slug, request.ParentCategoryId, request.DisplayOrder, request.IsActive);
        await _unitOfWork.SaveChangesAsync(ct);
        return category.ToDto();
    }
}
