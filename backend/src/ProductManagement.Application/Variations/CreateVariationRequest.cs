using FluentValidation;

namespace ProductManagement.Application.Variations;

public record CreateVariationRequest(string Name);

public class CreateVariationRequestValidator : AbstractValidator<CreateVariationRequest>
{
    public CreateVariationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
