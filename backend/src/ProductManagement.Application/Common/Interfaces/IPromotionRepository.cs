using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IPromotionRepository
{
    Task<Promotion?> GetByIdAsync(long id, CancellationToken ct);
    Task<List<Promotion>> ListAsync(CancellationToken ct);
    Task<bool> CategoryAlreadyAttachedAsync(long promotionId, long categoryId, CancellationToken ct);
    void Add(Promotion promotion);
    void AddCategory(PromotionCategory promotionCategory);
}
