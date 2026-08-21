using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variants;

public class DeleteVariantHandler
{
    private readonly IVariantRepository _variants;
    private readonly IUnitOfWork _unitOfWork;
    public DeleteVariantHandler(IVariantRepository variants, IUnitOfWork unitOfWork)
    { _variants = variants; _unitOfWork = unitOfWork; }

    public async Task HandleAsync(long variantId, CancellationToken ct)
    {
        var variant = await _variants.GetByIdAsync(variantId, ct)
            ?? throw new EntityNotFoundException(nameof(ProductVariant), variantId);

        variant.Deactivate(); // soft delete (spec section 3.2), not a hard delete
        await _unitOfWork.SaveChangesAsync(ct);
    }
}
