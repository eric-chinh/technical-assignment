using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Promotions;

public static class PromotionMappings
{
    public static PromotionCategoryDto ToDto(this PromotionCategory pc) =>
        new(pc.PromotionId, pc.CategoryId);

    public static PromotionDto ToDto(this Promotion promotion) =>
        new(promotion.Id, promotion.Name, promotion.Description, promotion.DiscountRate,
            promotion.StartDate, promotion.EndDate,
            promotion.Categories.Select(c => c.ToDto()).ToList());
}
