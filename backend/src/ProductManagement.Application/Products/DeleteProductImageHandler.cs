using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public sealed class NoImageSetException : Exception
{
    public NoImageSetException(long productId) : base($"Product {productId} has no image set.") { }
}

public class DeleteProductImageHandler
{
    private readonly IProductRepository _products;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public DeleteProductImageHandler(IProductRepository products, IFileStorageService fileStorage, IUnitOfWork unitOfWork, ICacheService cache)
    {
        _products = products;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task HandleAsync(long productId, CancellationToken ct)
    {
        var product = await _products.GetByIdWithItemsAsync(productId, ct)
            ?? throw new EntityNotFoundException(nameof(Product), productId);

        if (string.IsNullOrEmpty(product.ImageUrl))
            throw new NoImageSetException(productId);

        await _fileStorage.DeleteAsync(product.ImageUrl, ct);
        product.SetImageUrl(null);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(ProductCacheKeys.Product(productId), ct); // never serve the pre-delete cached entry
    }
}
