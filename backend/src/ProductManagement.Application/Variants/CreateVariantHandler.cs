using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public class CreateVariantHandler
{
    private readonly IProductItemRepository _items;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IValidator<CreateVariantRequest> _validator;

    public CreateVariantHandler(
        IProductItemRepository items, IProductRepository products,
        IUnitOfWork unitOfWork, ICacheService cache, IValidator<CreateVariantRequest> validator)
    {
        _items = items;
        _products = products;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _validator = validator;
    }

    public async Task<ProductItemDto> HandleAsync(long productId, CreateVariantRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var product = await _products.GetByIdWithItemsAsync(productId, ct)
            ?? throw new EntityNotFoundException(nameof(Product), productId);

        if (await _items.SkuExistsAsync(request.Sku, ct))
            throw new DuplicateSkuException(request.Sku);

        var item = ProductItem.Create(productId, request.Sku, request.Price, request.QtyInStock);
        if (request.ProductImage is not null)
            item.UpdateDetails(request.Price, request.ProductImage);

        _items.Add(item);
        await _unitOfWork.SaveChangesAsync(ct);

        await _cache.RemoveAsync(ProductCacheKeys.Product(productId), ct);
        return item.ToDto();
    }
}
