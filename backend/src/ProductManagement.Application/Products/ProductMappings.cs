using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public static class ProductMappings
{
    public static ProductItemDto ToDto(this ProductItem i) => new(
        i.Id, i.Sku, i.Price, i.QtyInStock, i.ProductImage, i.IsActive, i.Version,
        i.Configurations.Select(c => c.VariationOptionId).ToArray());

    public static ProductDto ToDto(this Product p) => new(
        p.Id, p.Name, p.Slug, p.Description, p.CategoryId, p.Brand,
        p.Status.ToString(), p.Attributes, p.ImageUrl,
        p.Items.Select(i => i.ToDto()).ToList());

    public static ProductListItemDto ToListItemDto(this Product p)
    {
        var active = p.Items.Where(i => i.IsActive).ToList();
        return new(
            p.Id, p.Name, p.Slug, p.CategoryId, p.Brand, p.Status.ToString(),
            active.Count > 0 ? active.Min(i => i.Price) : null,
            active.Count > 0 ? active.Max(i => i.Price) : null,
            active.Sum(i => i.QtyInStock),
            p.ImageUrl);
    }
}
