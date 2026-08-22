namespace ProductManagement.Domain.Entities;

public class PromotionCategory
{
    public long PromotionId { get; private set; }
    public long CategoryId { get; private set; }

    private PromotionCategory() { }

    public static PromotionCategory Create(long promotionId, long categoryId)
        => new() { PromotionId = promotionId, CategoryId = categoryId };
}
