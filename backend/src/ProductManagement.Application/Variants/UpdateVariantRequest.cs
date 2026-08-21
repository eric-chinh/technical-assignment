using FluentValidation;

namespace ProductManagement.Application.Variants;

public sealed record UpdateVariantRequest(
    string? Size, string? Color, decimal Price, decimal? CompareAtPrice, string? Barcode);

public sealed class UpdateVariantRequestValidator : AbstractValidator<UpdateVariantRequest>
{
    public UpdateVariantRequestValidator()
    {
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => x.CompareAtPrice is null || x.CompareAtPrice >= x.Price)
            .WithMessage("compareAtPrice must be >= price.");
    }
}
