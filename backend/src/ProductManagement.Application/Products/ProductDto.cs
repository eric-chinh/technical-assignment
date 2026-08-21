namespace ProductManagement.Application.Products;

public sealed record VariantDto(
    long Id, string Sku, string? Size, string? Color,
    decimal Price, decimal? CompareAtPrice, int StockQuantity, string? Barcode, bool IsActive);

public sealed record ProductDto(
    long Id, string Name, string Slug, string? Description, long CategoryId,
    string? Brand, string Status, string Attributes, string? ImageUrl,
    List<VariantDto> Variants);

public sealed record ProductListItemDto(
    long Id, string Name, string Slug, long CategoryId, string? Brand, string Status,
    decimal? MinPrice, decimal? MaxPrice, int TotalStock, string? ImageUrl);
