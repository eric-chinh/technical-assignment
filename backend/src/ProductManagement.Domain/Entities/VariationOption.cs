namespace ProductManagement.Domain.Entities;

public class VariationOption
{
    public long Id { get; private set; }
    public long VariationId { get; private set; }
    public string Value { get; private set; } = default!;

    private VariationOption() { }

    public static VariationOption Create(long variationId, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Option value is required.", nameof(value));
        return new VariationOption { VariationId = variationId, Value = value.Trim() };
    }
}
