using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IProductItemRepository
{
    Task<ProductItem?> GetByIdAsync(long id, CancellationToken ct);
    Task<bool> SkuExistsAsync(string sku, CancellationToken ct);
    Task<List<ProductItem>> ListByProductIdAsync(long productId, CancellationToken ct);
    void Add(ProductItem item);
}
