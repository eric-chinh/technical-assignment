using FluentValidation;

namespace ProductManagement.Application.Products;

public sealed record CreateProductItemRequest(
    string Sku, decimal Price, int QtyInStock,
    string? ProductImage = null, long[]? VariationOptionIds = null);

public sealed record CreateProductRequest(
    string Name, string Slug, long CategoryId, string? Brand,
    string? Description = null, string Attributes = "{}",
    List<CreateProductItemRequest>? Items = null);

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
        RuleForEach(x => x.Items).ChildRules(v =>
        {
            v.RuleFor(x => x.Sku).NotEmpty().MaximumLength(64);
            v.RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
            v.RuleFor(x => x.QtyInStock).GreaterThanOrEqualTo(0);
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
