using ProductManagement.Application.Common;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(long id, CancellationToken ct);
    Task<Product?> GetByIdWithVariantsAsync(long id, CancellationToken ct);
    Task<Product?> GetBySlugWithVariantsAsync(string slug, CancellationToken ct);
    Task<uint> GetXminAsync(long id, CancellationToken ct);
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
