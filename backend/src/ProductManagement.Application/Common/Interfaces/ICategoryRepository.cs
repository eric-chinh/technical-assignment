using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface ICategoryRepository
{
    Task<Category?> GetByIdAsync(long id, CancellationToken ct);
    Task<Category?> GetBySlugAsync(string slug, CancellationToken ct);
    Task<List<Category>> ListAsync(long? parentId, bool? activeOnly, CancellationToken ct);
    Task<bool> HasActiveProductsAsync(long categoryId, CancellationToken ct);
    void Add(Category category);
    void Remove(Category category);
}
