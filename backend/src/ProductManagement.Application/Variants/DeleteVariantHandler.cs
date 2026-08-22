using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public class DeleteVariantHandler
{
    private readonly IProductItemRepository _items;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;

    public DeleteVariantHandler(IProductItemRepository items, IUnitOfWork unitOfWork, ICacheService cache)
    {
        _items = items;
        _unitOfWork = unitOfWork;
        _cache = cache;
    }

    public async Task HandleAsync(long itemId, CancellationToken ct)
    {
        var item = await _items.GetByIdAsync(itemId, ct)
            ?? throw new EntityNotFoundException(nameof(ProductItem), itemId);

        item.Deactivate();
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(ProductCacheKeys.Product(item.ProductId), ct);
    }
}
