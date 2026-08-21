namespace ProductManagement.Application.Products;

public static class ProductCacheKeys
{
    public static string Product(long id) => $"product:{id}";
    public const string ListVersionKey = "products:list:version";
    public static string List(long version, string queryHash) => $"products:list:v{version}:{queryHash}";
}
