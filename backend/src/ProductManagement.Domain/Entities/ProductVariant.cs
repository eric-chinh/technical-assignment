using ProductManagement.Domain.Exceptions;

namespace ProductManagement.Domain.Entities;

public class ProductVariant
{
    public long Id { get; private set; }
    public long ProductId { get; private set; }
    public string Sku { get; private set; } = default!;
    public string? Size { get; private set; }
    public string? Color { get; private set; }
    public decimal Price { get; private set; }
    public decimal? CompareAtPrice { get; private set; }
    public int StockQuantity { get; private set; }
    public string? Barcode { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ProductVariant() { } // EF Core

    public static ProductVariant Create(
        long productId, string sku, string? size, string? color,
        decimal price, int stockQuantity, decimal? compareAtPrice = null, string? barcode = null)
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new InvalidProductVariantException("SKU is required.");
        if (price < 0)
            throw new InvalidProductVariantException("price must be >= 0.");
        if (stockQuantity < 0)
            throw new InvalidProductVariantException("stock must be >= 0.");
        if (compareAtPrice is not null && compareAtPrice < price)
            throw new InvalidProductVariantException("compareAtPrice must be >= price.");

        var now = DateTimeOffset.UtcNow;
        return new ProductVariant
        {
            ProductId = productId,
            Sku = sku.Trim(),
            Size = size,
            Color = color,
            Price = price,
            CompareAtPrice = compareAtPrice,
            StockQuantity = stockQuantity,
            Barcode = barcode,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDetails(string? size, string? color, decimal price, decimal? compareAtPrice, string? barcode)
    {
        if (price < 0)
            throw new InvalidProductVariantException("price must be >= 0.");
        if (compareAtPrice is not null && compareAtPrice < price)
            throw new InvalidProductVariantException("compareAtPrice must be >= price.");

        Size = size;
        Color = color;
        Price = price;
        CompareAtPrice = compareAtPrice;
        Barcode = barcode;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
