using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProductManagement.Application.Common;
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Products;

public class ListProductsHandler
{
    private const int MaxLimit = 100;
    private static readonly TimeSpan ListTtl = TimeSpan.FromSeconds(60);
    private readonly IProductRepository _products;
    private readonly ICacheService _cache;

    public ListProductsHandler(IProductRepository products, ICacheService cache)
    {
        _products = products;
        _cache = cache;
    }

    public async Task<PagedResult<ProductListItemDto>> HandleAsync(
        long? categoryId, short? status, string? q, decimal? minPrice, decimal? maxPrice,
        string? attributesJson, string? cursor, int limit, CancellationToken ct)
    {
        var query = new ProductListQuery(
            categoryId, status, q, minPrice, maxPrice, attributesJson, cursor, Math.Min(limit, MaxLimit));

        var version = await _cache.GetVersionAsync(ProductCacheKeys.ListVersionKey, ct);
        var queryHash = HashQuery(query);
        var cacheKey = ProductCacheKeys.List(version, queryHash);

        var cached = await _cache.GetAsync<PagedResult<ProductListItemDto>>(cacheKey, ct);
        if (cached is not null) return cached;

        var page = await _products.ListAsync(query, ct);
        var result = new PagedResult<ProductListItemDto>(
            page.Items.Select(p => p.ToListItemDto()).ToList(), page.NextCursor, page.HasMore);

        await _cache.SetAsync(cacheKey, result, ListTtl, ct);
        return result;
    }

    private static string HashQuery(ProductListQuery query)
    {
        var json = JsonSerializer.Serialize(query);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash)[..16];
    }
}
