using FluentValidation;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Promotions;

public class CreatePromotionHandler(
    IPromotionRepository promotions,
    IUnitOfWork unitOfWork,
    IValidator<CreatePromotionRequest> validator)
{
    public async Task<PromotionDto> HandleAsync(CreatePromotionRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var promotion = Promotion.Create(request.Name, request.Description, request.DiscountRate,
            request.StartDate, request.EndDate);
        promotions.Add(promotion);
        await unitOfWork.SaveChangesAsync(ct);

        return promotion.ToDto();
    }
}
