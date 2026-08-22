namespace ProductManagement.Domain.Entities;

public class Promotion
{
    public long Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public decimal DiscountRate { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }

    private readonly List<PromotionCategory> _categories = new();
    public IReadOnlyCollection<PromotionCategory> Categories => _categories.AsReadOnly();

    private Promotion() { }

    public static Promotion Create(string name, string? description, decimal discountRate, DateOnly startDate, DateOnly endDate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Promotion name is required.", nameof(name));
        if (discountRate <= 0 || discountRate > 1)
            throw new ArgumentException("discount_rate must be in (0, 1].", nameof(discountRate));
        if (endDate < startDate)
            throw new ArgumentException("end_date must be >= start_date.", nameof(endDate));

        return new Promotion
        {
            Name = name.Trim(),
            Description = description,
            DiscountRate = discountRate,
            StartDate = startDate,
            EndDate = endDate
        };
    }
}
