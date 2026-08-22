using ProductManagement.Domain.Exceptions;

namespace ProductManagement.Domain.Entities;

public class ProductItem
{
    public long Id { get; private set; }
    public long ProductId { get; private set; }
    public string Sku { get; private set; } = default!;
    public int QtyInStock { get; private set; }
    public decimal Price { get; private set; }
    public string? ProductImage { get; private set; }
    public bool IsActive { get; private set; } = true;
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private readonly List<ProductConfiguration> _configurations = new();
    public IReadOnlyCollection<ProductConfiguration> Configurations => _configurations.AsReadOnly();

    private ProductItem() { }

    public static ProductItem Create(long productId, string sku, decimal price, int qtyInStock)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new InvalidProductItemException("SKU is required.");
        if (price < 0)
            throw new InvalidProductItemException("Price must be >= 0.");
        if (qtyInStock < 0)
            throw new InvalidProductItemException("Stock must be >= 0.");

        var now = DateTimeOffset.UtcNow;
        return new ProductItem
        {
            ProductId = productId,
            Sku = sku.Trim(),
            Price = price,
            QtyInStock = qtyInStock,
            IsActive = true,
            Version = 0,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateDetails(decimal price, string? productImage)
    {
        if (price < 0)
            throw new InvalidProductItemException("Price must be >= 0.");
        Price = price;
        ProductImage = productImage;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public bool TryAdjustStock(int delta)
    {
        if (QtyInStock + delta < 0) return false;
        QtyInStock += delta;
        UpdatedAt = DateTimeOffset.UtcNow;
        return true;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
