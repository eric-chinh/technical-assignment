using FluentValidation;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variations;

public class CreateVariationHandler(
    IVariationRepository variations,
    IUnitOfWork unitOfWork,
    IValidator<CreateVariationRequest> validator)
{
    public async Task<VariationDto> HandleAsync(long categoryId, CreateVariationRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var variation = Variation.Create(categoryId, request.Name);
        variations.Add(variation);
        await unitOfWork.SaveChangesAsync(ct);

        return variation.ToDto();
    }
}
