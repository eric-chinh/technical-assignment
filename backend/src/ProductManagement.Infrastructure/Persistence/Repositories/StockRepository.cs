using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Infrastructure.Persistence.Repositories;

public class StockRepository : IStockRepository
{
    private readonly ProductManagementDbContext _db;
    public StockRepository(ProductManagementDbContext db) => _db = db;

    public async Task<StockAdjustResult> TryAdjustAsync(long itemId, int delta, CancellationToken ct)
    {
        if (delta >= 0)
        {
            var incrementedRows = await _db.ProductItems
                .Where(i => i.Id == itemId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.QtyInStock, i => i.QtyInStock + delta), ct);

            if (incrementedRows == 0) return new StockAdjustResult(false, null, null);

            var afterIncrement = await _db.ProductItems
                .Where(i => i.Id == itemId).Select(i => i.QtyInStock).FirstAsync(ct);
            return new StockAdjustResult(true, afterIncrement, null);
        }

        var decrementAmount = -delta;

        // Single atomic statement — WHERE clause guards against overselling (spec §7 Strong Consistency).
        var affectedRows = await _db.ProductItems
            .Where(i => i.Id == itemId && i.QtyInStock >= decrementAmount)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.QtyInStock, i => i.QtyInStock - decrementAmount), ct);

        if (affectedRows == 1)
        {
            var afterDecrement = await _db.ProductItems
                .Where(i => i.Id == itemId).Select(i => i.QtyInStock).FirstAsync(ct);
            return new StockAdjustResult(true, afterDecrement, null);
        }

        var currentStock = await _db.ProductItems
            .Where(i => i.Id == itemId).Select(i => (int?)i.QtyInStock).FirstOrDefaultAsync(ct);
        return new StockAdjustResult(false, null, currentStock);
    }
}
