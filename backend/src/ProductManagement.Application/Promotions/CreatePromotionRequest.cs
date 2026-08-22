using FluentValidation;

namespace ProductManagement.Application.Promotions;

public record CreatePromotionRequest(
    string Name,
    string? Description,
    decimal DiscountRate,
    DateOnly StartDate,
    DateOnly EndDate);

public class CreatePromotionRequestValidator : AbstractValidator<CreatePromotionRequest>
{
    public CreatePromotionRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DiscountRate).GreaterThan(0).LessThanOrEqualTo(1);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate)
            .WithMessage("end_date must be >= start_date");
    }
}
