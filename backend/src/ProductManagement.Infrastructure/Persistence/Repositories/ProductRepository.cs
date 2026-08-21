using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly ProductManagementDbContext _db;
    public ProductRepository(ProductManagementDbContext db) => _db = db;

    public Task<Product?> GetByIdWithVariantsAsync(long id, CancellationToken ct) =>
        _db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task<Product?> GetBySlugWithVariantsAsync(string slug, CancellationToken ct) =>
        _db.Products.Include(p => p.Variants).FirstOrDefaultAsync(p => p.Slug == slug, ct);

    public Task<uint> GetXminAsync(long id, CancellationToken ct) =>
        _db.Products.Where(p => p.Id == id).Select(p => EF.Property<uint>(p, "xmin")).FirstAsync(ct);

    public void SetExpectedVersion(Product product, uint expectedXmin) =>
        _db.Entry(product).Property("xmin").OriginalValue = expectedXmin;

    public void Add(Product product) => _db.Products.Add(product);

    public async Task<PagedResult<Product>> ListAsync(ProductListQuery query, CancellationToken ct)
    {
        var baseQuery = _db.Products.Include(p => p.Variants).AsQueryable();

        if (query.CategoryId is { } categoryId) baseQuery = baseQuery.Where(p => p.CategoryId == categoryId);
        if (query.Status is { } status) baseQuery = baseQuery.Where(p => (short)p.Status == status);
        if (query.MinPrice is { } minPrice) baseQuery = baseQuery.Where(p => p.Variants.Any(v => v.Price >= minPrice));
        if (query.MaxPrice is { } maxPrice) baseQuery = baseQuery.Where(p => p.Variants.Any(v => v.Price <= maxPrice));
        if (!string.IsNullOrWhiteSpace(query.AttributesJson))
            baseQuery = baseQuery.Where(p => EF.Functions.JsonContains(p.Attributes, query.AttributesJson));

        var decodedCursor = ProductCursor.Decode(query.Cursor); // throws InvalidCursorException -> 400 (Task 11) for a present-but-broken cursor

        if (!string.IsNullOrWhiteSpace(query.SearchText))
        {
            var searchText = query.SearchText;
            var matchedQuery = baseQuery
                .Where(p => EF.Property<NpgsqlTypes.NpgsqlTsVector>(p, "SearchVector")
                    .Matches(EF.Functions.WebSearchToTsQuery("english", searchText)));

            // Total count over the filter + full-text match, before the cursor's rank
            // paging window - so it stays stable across pages instead of shrinking.
            var searchTotalCount = await matchedQuery.LongCountAsync(ct);

            var ranked = matchedQuery
                .Select(p => new
                {
                    Product = p,
                    Rank = EF.Property<NpgsqlTypes.NpgsqlTsVector>(p, "SearchVector")
                        .Rank(EF.Functions.WebSearchToTsQuery("english", searchText))
                });

            if (decodedCursor?.Rank is { } afterRank)
                ranked = ranked.Where(x => x.Rank < afterRank || (x.Rank == afterRank && x.Product.Id < decodedCursor.Id));

            var rankedResults = await ranked
                .OrderByDescending(x => x.Rank).ThenByDescending(x => x.Product.Id)
                .Take(query.Limit + 1)
                .ToListAsync(ct);

            if (rankedResults.Count == 0)
            {
                // Full-text found nothing -> trigram typo-tolerant fallback (spec section 3.3)
                var trigramQuery = baseQuery.Where(p => EF.Functions.TrigramsSimilarity(p.Name, query.SearchText) > 0.1);
                var trigramTotalCount = await trigramQuery.LongCountAsync(ct);
                var trigramMatches = await trigramQuery
                    .OrderByDescending(p => EF.Functions.TrigramsSimilarity(p.Name, query.SearchText))
                    .Take(query.Limit)
                    .ToListAsync(ct);
                return new PagedResult<Product>(trigramMatches, null, false, trigramTotalCount);
            }

            var hasMore = rankedResults.Count > query.Limit;
            var page = rankedResults.Take(query.Limit).ToList();
            var last = page.LastOrDefault();
            var nextCursor = hasMore && last is not null
                ? new ProductCursor(null, rankedResults[query.Limit - 1].Rank, last.Product.Id).Encode()
                : null;
            return new PagedResult<Product>(page.Select(x => x.Product).ToList(), nextCursor, hasMore, searchTotalCount);
        }

        // Total count over the filters, before the cursor's created_at paging window -
        // so it stays stable across pages instead of shrinking as you page forward.
        var totalCount = await baseQuery.LongCountAsync(ct);

        if (decodedCursor?.CreatedAt is { } afterCreatedAt)
            baseQuery = baseQuery.Where(p => p.CreatedAt < afterCreatedAt || (p.CreatedAt == afterCreatedAt && p.Id < decodedCursor.Id));

        var results = await baseQuery
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Take(query.Limit + 1)
            .ToListAsync(ct);

        var moreExists = results.Count > query.Limit;
        var pageItems = results.Take(query.Limit).ToList();
        var lastItem = pageItems.LastOrDefault();
        var cursor = moreExists && lastItem is not null
            ? new ProductCursor(lastItem.CreatedAt, null, lastItem.Id).Encode()
            : null;
        return new PagedResult<Product>(pageItems, cursor, moreExists, totalCount);
    }
}
