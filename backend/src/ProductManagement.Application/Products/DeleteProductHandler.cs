using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public class DeleteProductHandler
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public DeleteProductHandler(IProductRepository products, IUnitOfWork unitOfWork, ICacheService cache)
    {
        _products = products;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task HandleAsync(long id, CancellationToken ct)
    {
        var product = await _products.GetByIdWithVariantsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);

        product.Archive(); // soft delete (spec section 3.2) - throws if already archived
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(ProductCacheKeys.Product(id), ct);
        await _cache.IncrementVersionAsync(ProductCacheKeys.ListVersionKey, ct);
    }
}
