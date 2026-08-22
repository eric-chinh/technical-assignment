using ProductManagement.Domain.Entities;

namespace ProductManagement.Application.Common.Interfaces;

public interface IVariationRepository
{
    Task<Variation?> GetByIdAsync(long id, CancellationToken ct);
    Task<List<Variation>> ListByCategoryIdAsync(long categoryId, CancellationToken ct);
    Task<bool> OptionExistsAsync(long variationId, string value, CancellationToken ct);
    void Add(Variation variation);
    void AddOption(VariationOption option);
}
