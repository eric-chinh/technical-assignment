using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public class DeleteVariantHandler
{
    private readonly IVariantRepository _variants;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    public DeleteVariantHandler(IVariantRepository variants, IUnitOfWork unitOfWork, ICacheService cache)
    { _variants = variants; _unitOfWork = unitOfWork; _cache = cache; }

    public async Task HandleAsync(long variantId, CancellationToken ct)
    {
        var variant = await _variants.GetByIdAsync(variantId, ct)
            ?? throw new EntityNotFoundException(nameof(ProductVariant), variantId);

        variant.Deactivate(); // soft delete (spec section 3.2), not a hard delete
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.RemoveAsync(ProductCacheKeys.Product(variant.ProductId), ct); // the cached product's variants list must drop the deactivated one
    }
}
