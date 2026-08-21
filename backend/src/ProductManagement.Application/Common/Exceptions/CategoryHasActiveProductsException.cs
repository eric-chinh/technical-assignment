namespace ProductManagement.Application.Common.Exceptions;

public sealed class CategoryHasActiveProductsException : Exception
{
    public CategoryHasActiveProductsException(long categoryId)
        : base($"Category {categoryId} still has active products referencing it.") { }
}
