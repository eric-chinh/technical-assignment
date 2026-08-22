using FluentValidation;

namespace ProductManagement.Application.Variants;

public sealed record UpdateVariantRequest(decimal Price, string? ProductImage, int ExpectedVersion);

public sealed class UpdateVariantRequestValidator : AbstractValidator<UpdateVariantRequest>
{
    public UpdateVariantRequestValidator()
    {
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExpectedVersion).GreaterThanOrEqualTo(0);
    }
}
