namespace ProductManagement.Application.Common.Exceptions;

public sealed class DuplicateSlugException : Exception
{
    public DuplicateSlugException(string slug) : base($"Slug '{slug}' already exists.") { }
}
