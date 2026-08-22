namespace ProductManagement.Domain.Entities;

public class ProductConfiguration
{
    public long ProductItemId { get; private set; }
    public long VariationOptionId { get; private set; }

    private ProductConfiguration() { }

    public static ProductConfiguration Create(long productItemId, long variationOptionId)
        => new() { ProductItemId = productItemId, VariationOptionId = variationOptionId };
}
