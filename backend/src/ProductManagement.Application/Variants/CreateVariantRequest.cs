using FluentValidation;

namespace ProductManagement.Application.Variants;

public sealed record CreateVariantRequest(
    string Sku, string? Size, string? Color, decimal Price, int StockQuantity,
    decimal? CompareAtPrice = null, string? Barcode = null);

public sealed class CreateVariantRequestValidator : AbstractValidator<CreateVariantRequest>
{
    public CreateVariantRequestValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => x.CompareAtPrice is null || x.CompareAtPrice >= x.Price)
            .WithMessage("compareAtPrice must be >= price.");
    }
}
