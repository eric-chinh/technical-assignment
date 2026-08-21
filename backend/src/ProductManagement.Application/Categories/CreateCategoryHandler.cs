using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Categories;

public class CreateCategoryHandler
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateCategoryRequest> _validator;

    public CreateCategoryHandler(ICategoryRepository categories, IUnitOfWork unitOfWork, IValidator<CreateCategoryRequest> validator)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<CategoryDto> HandleAsync(CreateCategoryRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        if (request.ParentCategoryId is { } parentId)
        {
            var parent = await _categories.GetByIdAsync(parentId, ct);
            if (parent is null) throw new EntityNotFoundException(nameof(Category), parentId);
        }

        var category = Category.Create(request.Name, request.Slug, request.ParentCategoryId, request.DisplayOrder);
        _categories.Add(category);
        await _unitOfWork.SaveChangesAsync(ct);
        return category.ToDto();
    }
}
