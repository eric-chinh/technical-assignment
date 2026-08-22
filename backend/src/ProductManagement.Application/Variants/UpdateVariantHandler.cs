using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public class UpdateVariantHandler
{
    private readonly IProductItemRepository _items;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IValidator<UpdateVariantRequest> _validator;

    public UpdateVariantHandler(IProductItemRepository items, IUnitOfWork unitOfWork, ICacheService cache, IValidator<UpdateVariantRequest> validator)
    {
        _items = items;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _validator = validator;
    }

    public async Task<ProductItemDto> HandleAsync(long itemId, UpdateVariantRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var item = await _items.GetByIdAsync(itemId, ct)
            ?? throw new EntityNotFoundException(nameof(ProductItem), itemId);

        if (item.Version != request.ExpectedVersion)
            throw new ConcurrencyConflictException($"Version mismatch on product item {itemId}. Reload and retry.");

        item.UpdateDetails(request.Price, request.ProductImage);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(ProductCacheKeys.Product(item.ProductId), ct);
        return item.ToDto();
    }
}
