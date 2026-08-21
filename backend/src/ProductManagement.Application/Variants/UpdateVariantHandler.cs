using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public class UpdateVariantHandler
{
    private readonly IVariantRepository _variants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IValidator<UpdateVariantRequest> _validator;

    public UpdateVariantHandler(IVariantRepository variants, IUnitOfWork unitOfWork, ICacheService cache, IValidator<UpdateVariantRequest> validator)
    {
        _variants = variants;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _validator = validator;
    }

    public async Task<VariantDto> HandleAsync(long variantId, UpdateVariantRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var variant = await _variants.GetByIdAsync(variantId, ct)
            ?? throw new EntityNotFoundException(nameof(ProductVariant), variantId);

        variant.UpdateDetails(request.Size, request.Color, request.Price, request.CompareAtPrice, request.Barcode);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(ProductCacheKeys.Product(variant.ProductId), ct); // the cached product's variant details must reflect the update
        return variant.ToDto();
    }
}
