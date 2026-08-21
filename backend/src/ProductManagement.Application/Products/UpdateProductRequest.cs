using FluentValidation;

namespace ProductManagement.Application.Products;

public sealed record UpdateProductRequest(
    string Name, string? Description, long CategoryId, string? Brand, string Attributes);

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Attributes).Must(json => json.Length <= 8000)
            .WithMessage("attributes JSON must be 8000 characters or fewer.");
        RuleFor(x => x.Attributes).Must(CreateProductRequestValidator.BeValidJson)
            .WithMessage("attributes must be valid JSON.");
    }
}
