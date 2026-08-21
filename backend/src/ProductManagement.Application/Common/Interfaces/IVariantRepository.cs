using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IVariantRepository
{
    Task<ProductVariant?> GetByIdAsync(long id, CancellationToken ct);
    Task<bool> SkuExistsAsync(string sku, CancellationToken ct);
    Task<List<ProductVariant>> ListByProductIdAsync(long productId, CancellationToken ct);
    void Add(ProductVariant variant);
}
