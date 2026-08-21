using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class VariantRepository : IVariantRepository
{
    private readonly ProductManagementDbContext _db;
    public VariantRepository(ProductManagementDbContext db) => _db = db;

    public Task<ProductVariant?> GetByIdAsync(long id, CancellationToken ct) =>
        _db.ProductVariants.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken ct) =>
        _db.ProductVariants.AnyAsync(v => v.Sku == sku, ct);

    public Task<List<ProductVariant>> ListByProductIdAsync(long productId, CancellationToken ct) =>
        _db.ProductVariants.Where(v => v.ProductId == productId).OrderBy(v => v.Id).ToListAsync(ct);

    public void Add(ProductVariant variant) => _db.ProductVariants.Add(variant);
}
