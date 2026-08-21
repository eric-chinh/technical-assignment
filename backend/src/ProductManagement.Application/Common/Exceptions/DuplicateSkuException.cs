namespace ProductManagement.Application.Common.Exceptions;

public sealed class DuplicateSkuException : Exception
{
    public DuplicateSkuException(string sku) : base($"SKU '{sku}' already exists.") { }
}
