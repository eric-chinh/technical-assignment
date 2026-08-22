using ProductManagement.Domain.Enums;
using ProductManagement.Domain.Exceptions;

namespace ProductManagement.Domain.Entities;

public class Product
{
    public long Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public string? Description { get; private set; }
    public long CategoryId { get; private set; }
    public string? Brand { get; private set; }
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    public string Attributes { get; private set; } = "{}"; // raw jsonb text, parsed at the edges
    public string? ImageUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ProductItem> _items = new();
    public IReadOnlyCollection<ProductItem> Items => _items.AsReadOnly();

    private Product() { }

    public static Product Create(string name, string slug, long categoryId, string? brand, string attributes = "{}")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidProductException("Product name is required.");
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidProductException("Product slug is required.");

        var now = DateTimeOffset.UtcNow;
        return new Product
        {
            Name = name.Trim(),
            Slug = slug.Trim(),
            CategoryId = categoryId,
            Brand = brand,
            Attributes = attributes,
            Status = ProductStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void AddItem(ProductItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        _items.Add(item);
    }

    public void Activate()
    {
        Status = ProductStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Archive()
    {
        if (Status == ProductStatus.Archived)
            throw new InvalidProductException("Product is already archived.");

        Status = ProductStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDetails(string name, string? description, long categoryId, string? brand, string attributes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidProductException("Product name is required.");

        Name = name.Trim();
        Description = description;
        CategoryId = categoryId;
        Brand = brand;
        Attributes = attributes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetImageUrl(string? imageUrl)
    {
        ImageUrl = imageUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
