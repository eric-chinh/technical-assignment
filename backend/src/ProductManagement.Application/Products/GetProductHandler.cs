using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public sealed record ProductResult(ProductDto Product, uint Xmin);

public class GetProductHandler
{
    private readonly IProductRepository _products;
    public GetProductHandler(IProductRepository products) => _products = products;

    public async Task<ProductResult> ByIdAsync(long id, CancellationToken ct)
    {
        var product = await _products.GetByIdWithVariantsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);
        var xmin = await _products.GetXminAsync(id, ct);
        return new ProductResult(product.ToDto(), xmin);
    }

    public async Task<ProductResult> BySlugAsync(string slug, CancellationToken ct)
    {
        var product = await _products.GetBySlugWithVariantsAsync(slug, ct)
            ?? throw new EntityNotFoundException(nameof(Product), slug);
        var xmin = await _products.GetXminAsync(product.Id, ct);
        return new ProductResult(product.ToDto(), xmin);
    }
}
