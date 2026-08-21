using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class StockRepository : IStockRepository
{
    private readonly ProductManagementDbContext _db;
    public StockRepository(ProductManagementDbContext db) => _db = db;

    public async Task<StockAdjustResult> TryAdjustAsync(long variantId, int delta, CancellationToken ct)
    {
        if (delta >= 0)
        {
            var incrementedRows = await _db.ProductVariants
                .Where(v => v.Id == variantId)
                .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity + delta), ct);

            if (incrementedRows == 0) return new StockAdjustResult(false, null, null);

            var afterIncrement = await _db.ProductVariants
                .Where(v => v.Id == variantId).Select(v => v.StockQuantity).FirstAsync(ct);
            return new StockAdjustResult(true, afterIncrement, null);
        }

        var decrementAmount = -delta;

        // The single atomic statement: the WHERE clause is the guard against overselling.
        // No prior read, no window for a concurrent request to interleave (spec section 3.4).
        var affectedRows = await _db.ProductVariants
            .Where(v => v.Id == variantId && v.StockQuantity >= decrementAmount)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.StockQuantity, v => v.StockQuantity - decrementAmount), ct);

        if (affectedRows == 1)
        {
            var afterDecrement = await _db.ProductVariants
                .Where(v => v.Id == variantId).Select(v => v.StockQuantity).FirstAsync(ct);
            return new StockAdjustResult(true, afterDecrement, null);
        }

        var currentStock = await _db.ProductVariants
            .Where(v => v.Id == variantId).Select(v => (int?)v.StockQuantity).FirstOrDefaultAsync(ct);
        return new StockAdjustResult(false, null, currentStock);
    }
}
