namespace ProductManagement.Application.Products;

public sealed record ProductItemDto(
    long Id, string Sku, decimal Price, int QtyInStock,
    string? ProductImage, bool IsActive, int Version, long[] VariationOptionIds);

public sealed record ProductDto(
    long Id, string Name, string Slug, string? Description, long CategoryId,
    string? Brand, string Status, string Attributes, string? ImageUrl,
    List<ProductItemDto> Items);

public sealed record ProductListItemDto(
    long Id, string Name, string Slug, long CategoryId, string? Brand, string Status,
    decimal? MinPrice, decimal? MaxPrice, int TotalStock, string? ImageUrl);
