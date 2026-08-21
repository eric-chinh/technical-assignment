using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;

namespace ProductManagement.Application.Variants;

public sealed record AdjustStockRequest(int Delta);
public sealed record AdjustStockResult(bool Succeeded, int? NewQuantity, int? AvailableQuantity);

public class AdjustStockHandler
{
    private static readonly TimeSpan IdempotencyTtl = TimeSpan.FromMinutes(5);
    private readonly IStockRepository _stock;
    private readonly ICacheService _cache;

    public AdjustStockHandler(IStockRepository stock, ICacheService cache)
    {
        _stock = stock;
        _cache = cache;
    }

    public async Task<AdjustStockResult> HandleAsync(
        long productId, long variantId, AdjustStockRequest request, string? idempotencyKey, CancellationToken ct)
    {
        AdjustStockResult result;

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var direct = await _stock.TryAdjustAsync(variantId, request.Delta, ct);
            result = new AdjustStockResult(direct.Succeeded, direct.NewQuantity, direct.AvailableQuantity);
        }
        else
        {
            var cacheKey = $"stock-adjust:{idempotencyKey}";
            var cached = await _cache.GetAsync<AdjustStockResult>(cacheKey, ct);
            if (cached is not null) return cached; // retried request -> never re-applied, no cache side-effect either

            var adjusted = await _stock.TryAdjustAsync(variantId, request.Delta, ct);
            result = new AdjustStockResult(adjusted.Succeeded, adjusted.NewQuantity, adjusted.AvailableQuantity);
            await _cache.SetAsync(cacheKey, result, IdempotencyTtl, ct);
        }

        if (result.Succeeded)
            await _cache.RemoveAsync(ProductCacheKeys.Product(productId), ct); // stock must never be served stale

        return result;
    }
}
