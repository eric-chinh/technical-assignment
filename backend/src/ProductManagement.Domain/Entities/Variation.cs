namespace ProductManagement.Domain.Entities;

public class Variation
{
    public long Id { get; private set; }
    public long CategoryId { get; private set; }
    public string Name { get; private set; } = default!;

    private readonly List<VariationOption> _options = new();
    public IReadOnlyCollection<VariationOption> Options => _options.AsReadOnly();

    private Variation() { }

    public static Variation Create(long categoryId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Variation name is required.", nameof(name));
        return new Variation { CategoryId = categoryId, Name = name.Trim() };
    }
}
