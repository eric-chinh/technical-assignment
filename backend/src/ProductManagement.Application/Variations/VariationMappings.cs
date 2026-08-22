using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Variations;

public static class VariationMappings
{
    public static VariationOptionDto ToDto(this VariationOption option) =>
        new(option.Id, option.Value);

    public static VariationDto ToDto(this Variation variation) =>
        new(variation.Id, variation.CategoryId, variation.Name,
            variation.Options.Select(o => o.ToDto()).ToList());
}
