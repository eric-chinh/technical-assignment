using ProductManagement.Application.Common;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdWithItemsAsync(long id, CancellationToken ct);
    Task<Product?> GetBySlugWithItemsAsync(string slug, CancellationToken ct);
    Task<uint> GetXminAsync(long id, CancellationToken ct);
    void SetExpectedVersion(Product product, uint expectedXmin);
    Task<PagedResult<Product>> ListAsync(ProductListQuery query, CancellationToken ct);
    void Add(Product product);
}

public sealed record ProductListQuery(
    long? CategoryId,
    short? Status,
    string? SearchText,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? AttributesJson,
    string? Cursor,
    int Limit);
