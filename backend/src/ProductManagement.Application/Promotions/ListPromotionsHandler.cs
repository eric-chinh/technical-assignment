using ProductManagement.Application.Common.Interfaces;

namespace ProductManagement.Application.Promotions;

public class ListPromotionsHandler(IPromotionRepository promotions)
{
    public async Task<List<PromotionDto>> HandleAsync(CancellationToken ct)
    {
        var list = await promotions.ListAsync(ct);
        return list.Select(p => p.ToDto()).ToList();
    }
}
