using FluentValidation;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Products;

public class CreateProductHandler
{
    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cache;
    private readonly IValidator<CreateProductRequest> _validator;

    public CreateProductHandler(
        IProductRepository products, ICategoryRepository categories, IUnitOfWork unitOfWork,
        ICacheService cache, IValidator<CreateProductRequest> validator)
    {
        _products = products;
        _categories = categories;
        _unitOfWork = unitOfWork;
        _cache = cache;
        _validator = validator;
    }

    public async Task<ProductDto> HandleAsync(CreateProductRequest request, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(request, ct);

        var category = await _categories.GetByIdAsync(request.CategoryId, ct)
            ?? throw new EntityNotFoundException(nameof(Category), request.CategoryId);

        var product = Product.Create(request.Name, request.Slug, category.Id, request.Brand, request.Attributes);

        foreach (var v in request.Items ?? [])
        {
            product.AddItem(ProductItem.Create(product.Id, v.Sku, v.Price, v.QtyInStock));
        }

        _products.Add(product);
        await _unitOfWork.SaveChangesAsync(ct);
        await _cache.IncrementVersionAsync(ProductCacheKeys.ListVersionKey, ct);
        return product.ToDto();
    }
}
