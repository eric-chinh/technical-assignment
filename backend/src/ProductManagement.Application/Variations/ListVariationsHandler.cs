using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Variations;

public class ListVariationsHandler(IVariationRepository variations)
{
    public async Task<List<VariationDto>> HandleAsync(long categoryId, CancellationToken ct)
    {
        var list = await variations.ListByCategoryIdAsync(categoryId, ct);
        return list.Select(v => v.ToDto()).ToList();
    }
}
