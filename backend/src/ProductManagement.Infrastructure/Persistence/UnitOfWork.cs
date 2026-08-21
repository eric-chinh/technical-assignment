using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ProductManagementDbContext _db;
    public UnitOfWork(ProductManagementDbContext db) => _db = db;

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            var value = ExtractConflictingValue(pg);
            if (pg.ConstraintName?.Contains("sku") == true)
                throw new DuplicateSkuException(value);
            if (pg.ConstraintName?.Contains("slug") == true)
                throw new DuplicateSlugException(value);
            throw;
        }
    }

    private static string ExtractConflictingValue(PostgresException pg)
    {
        // Postgres detail looks like: Key (slug)=(dresses) already exists.
        var detail = pg.Detail ?? string.Empty;
        var start = detail.IndexOf(")=(", StringComparison.Ordinal);
        if (start < 0) return "unknown";
        var end = detail.IndexOf(')', start + 3);
        if (end < 0) return "unknown";
        return detail.Substring(start + 3, end - (start + 3));
    }
}
