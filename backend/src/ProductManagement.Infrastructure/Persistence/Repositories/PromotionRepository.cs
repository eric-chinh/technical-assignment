using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class PromotionRepository(ProductManagementDbContext db) : IPromotionRepository
{
    public Task<Promotion?> GetByIdAsync(long id, CancellationToken ct) =>
        db.Promotions.Include(p => p.Categories).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<List<Promotion>> ListAsync(CancellationToken ct) =>
        db.Promotions.Include(p => p.Categories).ToListAsync(ct);

    public Task<bool> CategoryAlreadyAttachedAsync(long promotionId, long categoryId, CancellationToken ct) =>
        db.PromotionCategories.AnyAsync(pc => pc.PromotionId == promotionId && pc.CategoryId == categoryId, ct);

    public void Add(Promotion promotion) => db.Promotions.Add(promotion);

    public void AddCategory(PromotionCategory pc) => db.PromotionCategories.Add(pc);
}
