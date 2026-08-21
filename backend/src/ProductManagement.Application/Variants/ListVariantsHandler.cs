using ProductManagement.Application.Common.Interfaces;
using ProductManagement.Application.Products;

namespace ProductManagement.Application.Variants;

public class ListVariantsHandler
{
    private readonly IVariantRepository _variants;
    public ListVariantsHandler(IVariantRepository variants) => _variants = variants;

    public async Task<List<VariantDto>> HandleAsync(long productId, CancellationToken ct)
    {
        var variants = await _variants.ListByProductIdAsync(productId, ct);
        return variants.Select(v => v.ToDto()).ToList();
    }
}
