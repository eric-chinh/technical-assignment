using FluentValidation;

namespace ProductManagement.Application.Products;

public sealed record CreateVariantRequest(
    string Sku, string? Size, string? Color, decimal Price, int StockQuantity,
    decimal? CompareAtPrice = null, string? Barcode = null);

public sealed record CreateProductRequest(
    string Name, string Slug, long CategoryId, string? Brand,
    string? Description = null, string Attributes = "{}",
    List<CreateVariantRequest>? Variants = null);

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(200).Matches("^[a-z0-9-]+$");
        RuleFor(x => x.Attributes).Must(json => json.Length <= 8000)
            .WithMessage("attributes JSON must be 8000 characters or fewer.");
        RuleFor(x => x.Attributes).Must(BeValidJson)
            .WithMessage("attributes must be valid JSON.");
        RuleForEach(x => x.Variants).ChildRules(v =>
        {
            v.RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
            v.RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
            v.RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
            v.RuleFor(x => x)
                .Must(x => x.CompareAtPrice is null || x.CompareAtPrice >= x.Price)
                .WithMessage("compareAtPrice must be >= price.");
        });
    }

    internal static bool BeValidJson(string json)
    {
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
