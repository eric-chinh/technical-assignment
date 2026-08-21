using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Categories;

public class ListCategoriesHandler
{
    private static readonly TimeSpan ListTtl = TimeSpan.FromMinutes(30);
    private readonly ICategoryRepository _categories;
    private readonly ICacheService _cache;

    public ListCategoriesHandler(ICategoryRepository categories, ICacheService cache)
    {
        _categories = categories;
        _cache = cache;
    }

    public async Task<List<CategoryDto>> HandleAsync(long? parentId, bool? activeOnly, CancellationToken ct)
    {
        // Only the common "list everything" call is cached - filtered variants (parentId/activeOnly
        // set) are infrequent enough that caching every combination isn't worth the complexity.
        if (parentId is null && activeOnly is null)
        {
            var cached = await _cache.GetAsync<List<CategoryDto>>(CategoryCacheKeys.ListKey, ct);
            if (cached is not null) return cached;
        }

        var categories = await _categories.ListAsync(parentId, activeOnly, ct);
        var result = categories.Select(c => c.ToDto()).ToList();

        if (parentId is null && activeOnly is null)
            await _cache.SetAsync(CategoryCacheKeys.ListKey, result, ListTtl, ct);

        return result;
    }
}
