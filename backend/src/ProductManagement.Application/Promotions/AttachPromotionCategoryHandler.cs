using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Promotions;

public class AttachPromotionCategoryHandler(IPromotionRepository promotions, IUnitOfWork unitOfWork)
{
    public async Task<PromotionDto> HandleAsync(long promotionId, long categoryId, CancellationToken ct)
    {
        var promotion = await promotions.GetByIdAsync(promotionId, ct)
            ?? throw new EntityNotFoundException(nameof(Promotion), promotionId);

        if (await promotions.CategoryAlreadyAttachedAsync(promotionId, categoryId, ct))
            return promotion.ToDto(); // idempotent — category already linked

        var link = PromotionCategory.Create(promotionId, categoryId);
        promotions.AddCategory(link);
        await unitOfWork.SaveChangesAsync(ct);

        var updated = await promotions.GetByIdAsync(promotionId, ct);
        return updated!.ToDto();
    }
}
