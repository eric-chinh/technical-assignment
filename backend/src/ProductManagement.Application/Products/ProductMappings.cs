using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public static class ProductMappings
{
    public static VariantDto ToDto(this ProductVariant v) => new(
        v.Id, v.Sku, v.Size, v.Color, v.Price, v.CompareAtPrice, v.StockQuantity, v.Barcode, v.IsActive);

    public static ProductDto ToDto(this Product p) => new(
        p.Id, p.Name, p.Slug, p.Description, p.CategoryId, p.Brand,
        p.Status.ToString(), p.Attributes, p.ImageUrl,
        p.Variants.Select(v => v.ToDto()).ToList());

    public static ProductListItemDto ToListItemDto(this Product p) => new(
        p.Id, p.Name, p.Slug, p.CategoryId, p.Brand, p.Status.ToString(),
        p.Variants.Count > 0 ? p.Variants.Min(v => v.Price) : null,
        p.Variants.Count > 0 ? p.Variants.Max(v => v.Price) : null,
        p.Variants.Sum(v => v.StockQuantity),
        p.ImageUrl);
}
