using FluentValidation;

namespace ProductManagement.Application.Variants;

public sealed record CreateVariantRequest(
    string Sku, decimal Price, int QtyInStock,
    string? ProductImage = null, long[]? VariationOptionIds = null);

public sealed class CreateVariantRequestValidator : AbstractValidator<CreateVariantRequest>
{
    public CreateVariantRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.QtyInStock).GreaterThanOrEqualTo(0);
    }
}
