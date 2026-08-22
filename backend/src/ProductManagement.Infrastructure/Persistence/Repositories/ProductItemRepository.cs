using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class ProductItemRepository(ProductManagementDbContext db) : IProductItemRepository
{
    public Task<ProductItem?> GetByIdAsync(long id, CancellationToken ct) =>
        db.ProductItems.Include(i => i.Configurations).FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<bool> SkuExistsAsync(string sku, CancellationToken ct) =>
        db.ProductItems.AnyAsync(i => i.Sku == sku, ct);

    public Task<List<ProductItem>> ListByProductIdAsync(long productId, CancellationToken ct) =>
        db.ProductItems.Include(i => i.Configurations)
            .Where(i => i.ProductId == productId)
            .ToListAsync(ct);

    public void Add(ProductItem item) => db.ProductItems.Add(item);
}
