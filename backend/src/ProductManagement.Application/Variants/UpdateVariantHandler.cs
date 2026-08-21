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
    private readonly IValidator<UpdateVariantRequest> _validator;

    public UpdateVariantHandler(IVariantRepository variants, IUnitOfWork unitOfWork, IValidator<UpdateVariantRequest> validator)
    {
        _variants = variants;
        _unitOfWork = unitOfWork;
        _validator = validator;
    }

    public async Task<VariantDto> HandleAsync(long variantId, UpdateVariantRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var variant = await _variants.GetByIdAsync(variantId, ct)
            ?? throw new EntityNotFoundException(nameof(ProductVariant), variantId);

        variant.UpdateDetails(request.Size, request.Color, request.Price, request.CompareAtPrice, request.Barcode);
        await _unitOfWork.SaveChangesAsync(ct);
        return variant.ToDto();
    }
}
