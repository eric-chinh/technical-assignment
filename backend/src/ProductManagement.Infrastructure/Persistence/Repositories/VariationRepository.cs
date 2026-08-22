using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class VariationRepository(ProductManagementDbContext db) : IVariationRepository
{
    public Task<Variation?> GetByIdAsync(long id, CancellationToken ct) =>
        db.Variations.Include(v => v.Options).FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<List<Variation>> ListByCategoryIdAsync(long categoryId, CancellationToken ct) =>
        db.Variations.Include(v => v.Options).Where(v => v.CategoryId == categoryId).ToListAsync(ct);

    public Task<bool> OptionExistsAsync(long variationId, string value, CancellationToken ct) =>
        db.VariationOptions.AnyAsync(o => o.VariationId == variationId && o.Value == value, ct);

    public void Add(Variation variation) => db.Variations.Add(variation);

    public void AddOption(VariationOption option) => db.VariationOptions.Add(option);
}
