namespace ProductManagement.Application.Promotions;

public record PromotionCategoryDto(long PromotionId, long CategoryId);
public record PromotionDto(long Id, string Name, string? Description, decimal DiscountRate,
    DateOnly StartDate, DateOnly EndDate, List<PromotionCategoryDto> Categories);
