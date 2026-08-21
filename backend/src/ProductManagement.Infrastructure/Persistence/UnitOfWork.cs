using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly ProductManagementDbContext _db;
    public UnitOfWork(ProductManagementDbContext db) => _db = db;

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
