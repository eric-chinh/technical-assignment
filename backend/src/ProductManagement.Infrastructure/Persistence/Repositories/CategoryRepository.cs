using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;
using ProductManagement.Domain.Enums;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly ProductManagementDbContext _db;
    public CategoryRepository(ProductManagementDbContext db) => _db = db;

    public Task<Category?> GetByIdAsync(long id, CancellationToken ct) =>
        _db.Categories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Category?> GetBySlugAsync(string slug, CancellationToken ct) =>
        _db.Categories.FirstOrDefaultAsync(c => c.Slug == slug, ct);

    public async Task<List<Category>> ListAsync(long? parentId, bool? activeOnly, CancellationToken ct)
    {
        var query = _db.Categories.AsQueryable();
        if (parentId is not null) query = query.Where(c => c.ParentCategoryId == parentId);
        if (activeOnly == true) query = query.Where(c => c.IsActive);
        return await query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name).ToListAsync(ct);
    }

    public Task<bool> HasActiveProductsAsync(long categoryId, CancellationToken ct) =>
        _db.Products.AnyAsync(p => p.CategoryId == categoryId && p.Status != ProductStatus.Archived, ct);

    public void Add(Category category) => _db.Categories.Add(category);
    public void Remove(Category category) => _db.Categories.Remove(category);
}
