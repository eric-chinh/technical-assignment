using ProductManagement.Domain.Exceptions;

namespace ProductManagement.Domain.Entities;

public class Category
{
    public long Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Slug { get; private set; } = default!;
    public long? ParentCategoryId { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Category() { } // EF Core

    public static Category Create(string name, string slug, long? parentCategoryId, int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidCategoryException("Category name is required.");
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidCategoryException("Category slug is required.");

        var now = DateTimeOffset.UtcNow;
        return new Category
        {
            Name = name.Trim(),
            Slug = slug.Trim(),
            ParentCategoryId = parentCategoryId,
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string name, string slug, long? parentCategoryId, int displayOrder, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidCategoryException("Category name is required.");
        if (string.IsNullOrWhiteSpace(slug))
            throw new InvalidCategoryException("Category slug is required.");

        Name = name.Trim();
        Slug = slug.Trim();
        ParentCategoryId = parentCategoryId;
        DisplayOrder = displayOrder;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
