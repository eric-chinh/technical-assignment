namespace ProductManagement.Application.Variations;

public record VariationOptionDto(long Id, string Value);
public record VariationDto(long Id, long CategoryId, string Name, List<VariationOptionDto> Options);
