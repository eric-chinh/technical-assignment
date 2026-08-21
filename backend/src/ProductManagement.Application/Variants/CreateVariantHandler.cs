using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public class CreateVariantHandler
{
    private readonly IVariantRepository _variants;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IValidator<CreateVariantRequest> _validator;

    public CreateVariantHandler(
        IVariantRepository variants, IProductRepository products,
        IUnitOfWork unitOfWork, ICacheService cache, IValidator<CreateVariantRequest> validator)
    {
        _variants = variants;
        _products = products;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _validator = validator;
    }

    public async Task<VariantDto> HandleAsync(long productId, CreateVariantRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var product = await _products.GetByIdWithVariantsAsync(productId, ct)
            ?? throw new EntityNotFoundException(nameof(Product), productId);

        var variant = ProductVariant.Create(
            productId, request.Sku, request.Size, request.Color,
            request.Price, request.StockQuantity, request.CompareAtPrice, request.Barcode);

        _variants.Add(variant);
        await _unitOfWork.SaveChangesAsync(ct); // duplicate SKU -> DuplicateSkuException via UnitOfWork translation (Task 5)
        await _cache.RemoveAsync(ProductCacheKeys.Product(productId), ct); // the cached product's variants list must include the new one
        return variant.ToDto();
    }
}
