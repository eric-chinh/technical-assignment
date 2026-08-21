namespace ProductManagement.Application.Common.Interfaces;

/// <summary>
/// Deliberately the only entry point for stock changes (spec §3.4) — a single
/// atomic conditional UPDATE, never a load-then-save. Returns the resulting
/// stock quantity on success, or null if the delta couldn't be applied
/// (insufficient stock on a decrement).
/// </summary>
public interface IStockRepository
{
    Task<StockAdjustResult> TryAdjustAsync(long variantId, int delta, CancellationToken ct);
}

public sealed record StockAdjustResult(bool Succeeded, int? NewQuantity, int? AvailableQuantity);
