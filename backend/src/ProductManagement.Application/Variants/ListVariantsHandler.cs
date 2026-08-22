using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;

namespace ProductManagement.Application.Variants;

public class ListVariantsHandler
{
    private readonly IProductItemRepository _items;
    public ListVariantsHandler(IProductItemRepository items) => _items = items;

    public async Task<List<ProductItemDto>> HandleAsync(long productId, CancellationToken ct)
    {
        var items = await _items.ListByProductIdAsync(productId, ct);
        return items.Select(i => i.ToDto()).ToList();
    }
}
