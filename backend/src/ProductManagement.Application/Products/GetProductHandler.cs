using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public sealed record ProductResult(ProductDto Product, uint Xmin);

public class GetProductHandler
{
    private static readonly TimeSpan ProductTtl = TimeSpan.FromMinutes(10);
    private readonly IProductRepository _products;
    private readonly ICacheService _cache;

    public GetProductHandler(IProductRepository products, ICacheService cache)
    {
        _products = products;
        _cache = cache;
    }

    public async Task<ProductResult> ByIdAsync(long id, CancellationToken ct)
    {
        var cacheKey = ProductCacheKeys.Product(id);
        var cached = await _cache.GetAsync<ProductResult>(cacheKey, ct);
        if (cached is not null) return cached;

        var product = await _products.GetByIdWithVariantsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);
        var xmin = await _products.GetXminAsync(id, ct);
        var result = new ProductResult(product.ToDto(), xmin);

        await _cache.SetAsync(cacheKey, result, ProductTtl, ct);
        return result;
    }

    public async Task<ProductResult> BySlugAsync(string slug, CancellationToken ct)
    {
        var product = await _products.GetBySlugWithVariantsAsync(slug, ct)
            ?? throw new EntityNotFoundException(nameof(Product), slug);
        return await ByIdAsync(product.Id, ct); // reuses the id-keyed cache entry
    }
}
