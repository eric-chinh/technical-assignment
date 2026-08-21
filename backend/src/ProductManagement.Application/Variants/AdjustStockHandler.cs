using ProductManagement.Application.Common.Interfaces;

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

    public async Task<AdjustStockResult> HandleAsync(long variantId, AdjustStockRequest request, string? idempotencyKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var direct = await _stock.TryAdjustAsync(variantId, request.Delta, ct);
            return new AdjustStockResult(direct.Succeeded, direct.NewQuantity, direct.AvailableQuantity);
        }

        var cacheKey = $"stock-adjust:{idempotencyKey}";
        var cached = await _cache.GetAsync<AdjustStockResult>(cacheKey, ct);
        if (cached is not null) return cached; // retried request with the same key -> never re-applied

        var result = await _stock.TryAdjustAsync(variantId, request.Delta, ct);
        var mapped = new AdjustStockResult(result.Succeeded, result.NewQuantity, result.AvailableQuantity);
        await _cache.SetAsync(cacheKey, mapped, IdempotencyTtl, ct);
        return mapped;
    }
}
