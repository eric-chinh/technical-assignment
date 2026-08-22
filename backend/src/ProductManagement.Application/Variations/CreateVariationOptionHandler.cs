using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variations;

public class CreateVariationOptionHandler(
    IVariationRepository variations,
    IUnitOfWork unitOfWork,
    IValidator<CreateVariationOptionRequest> validator)
{
    public async Task<VariationOptionDto> HandleAsync(long variationId, CreateVariationOptionRequest request, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);

        var variation = await variations.GetByIdAsync(variationId, ct)
            ?? throw new EntityNotFoundException(nameof(Variation), variationId);

        if (await variations.OptionExistsAsync(variationId, request.Value, ct))
            throw new ValidationException($"Option '{request.Value}' already exists for this variation.");

        var option = VariationOption.Create(variationId, request.Value);
        variations.AddOption(option);
        await unitOfWork.SaveChangesAsync(ct);

        return option.ToDto();
    }
}
