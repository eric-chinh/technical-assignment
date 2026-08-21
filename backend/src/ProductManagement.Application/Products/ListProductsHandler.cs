using ProductManagement.Application.Common;
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Products;

public class ListProductsHandler
{
    private const int MaxLimit = 100;
    private readonly IProductRepository _products;
    public ListProductsHandler(IProductRepository products) => _products = products;

    public async Task<PagedResult<ProductListItemDto>> HandleAsync(
        long? categoryId, short? status, string? q, decimal? minPrice, decimal? maxPrice,
        string? attributesJson, string? cursor, int limit, CancellationToken ct)
    {
        var query = new ProductListQuery(
            categoryId, status, q, minPrice, maxPrice, attributesJson, cursor, Math.Min(limit, MaxLimit));

        var page = await _products.ListAsync(query, ct);
        return new PagedResult<ProductListItemDto>(
            page.Items.Select(p => p.ToListItemDto()).ToList(), page.NextCursor, page.HasMore);
    }
}
