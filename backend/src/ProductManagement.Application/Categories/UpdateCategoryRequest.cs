using FluentValidation;

namespace ProductManagement.Application.Categories;

public sealed record UpdateCategoryRequest(
    string Name,
    string Slug,
    long? ParentCategoryId,
    int DisplayOrder,
    bool IsActive);

public sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(120).Matches("^[a-z0-9-]+$");
    }
}
