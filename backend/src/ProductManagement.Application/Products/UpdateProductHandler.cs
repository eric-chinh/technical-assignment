using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public class UpdateProductHandler
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IValidator<UpdateProductRequest> _validator;

    public UpdateProductHandler(
        IProductRepository products, IUnitOfWork unitOfWork, ICacheService cache, IValidator<UpdateProductRequest> validator)
    {
        _products = products;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _validator = validator;
    }

    public async Task<ProductDto> HandleAsync(long id, uint expectedXmin, UpdateProductRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var product = await _products.GetByIdWithVariantsAsync(id, ct)
            ?? throw new EntityNotFoundException(nameof(Product), id);

        _products.SetExpectedVersion(product, expectedXmin);
        product.UpdateDetails(request.Name, request.Description, request.CategoryId, request.Brand, request.Attributes);

        await _unitOfWork.SaveChangesAsync(ct); // throws DbUpdateConcurrencyException on xmin mismatch -> 409 (Task 11)
        await _cache.RemoveAsync(ProductCacheKeys.Product(id), ct); // never serve stale data after a write (spec section 8)
        await _cache.IncrementVersionAsync(ProductCacheKeys.ListVersionKey, ct);
        return product.ToDto();
    }
}
