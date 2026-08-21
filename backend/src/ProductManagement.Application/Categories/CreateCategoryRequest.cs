using FluentValidation;

namespace ProductManagement.Application.Categories;

public sealed record CreateCategoryRequest(
    string Name,
    string Slug,
    long? ParentCategoryId,
    int DisplayOrder = 0);

public sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(120).Matches("^[a-z0-9-]+$")
            .WithMessage("Slug must be lowercase letters, numbers, and hyphens only.");
    }
}
