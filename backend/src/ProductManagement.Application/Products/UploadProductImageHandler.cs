using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public sealed class InvalidImageException : Exception
{
    public InvalidImageException(string message) : base(message) { }
}

public class UploadProductImageHandler
{
    private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp" };
    private const long MaxSizeBytes = 5 * 1024 * 1024;

    private readonly IProductRepository _products;
    private readonly IFileStorageService _fileStorage;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public UploadProductImageHandler(IProductRepository products, IFileStorageService fileStorage, IUnitOfWork unitOfWork, ICacheService cache)
    {
        _products = products;
        _fileStorage = fileStorage;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task<string> HandleAsync(long productId, Stream content, string fileName, string contentType, long length, CancellationToken ct)
    {
        if (!AllowedContentTypes.Contains(contentType))
            throw new InvalidImageException($"Content type '{contentType}' is not allowed. Use jpeg, png, or webp.");
        if (length > MaxSizeBytes)
            throw new InvalidImageException("Image exceeds the 5 MB size limit.");

        var product = await _products.GetByIdWithItemsAsync(productId, ct)
            ?? throw new EntityNotFoundException(nameof(Product), productId);

        if (!string.IsNullOrEmpty(product.ImageUrl))
            await _fileStorage.DeleteAsync(product.ImageUrl, ct); // replace, never orphan the old file

        var url = await _fileStorage.SaveAsync(content, fileName, contentType, productId, ct);
        product.SetImageUrl(url);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(ProductCacheKeys.Product(productId), ct); // never serve the pre-upload cached entry
        return url;
    }
}
