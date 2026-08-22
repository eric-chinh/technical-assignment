using FluentValidation;

namespace ProductManagement.Application.Variations;

public record CreateVariationOptionRequest(string Value);

public class CreateVariationOptionRequestValidator : AbstractValidator<CreateVariationOptionRequest>
{
    public CreateVariationOptionRequestValidator()
    {
        RuleFor(x => x.Value).NotEmpty().MaximumLength(100);
    }
}
