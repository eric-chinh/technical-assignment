namespace ProductManagement.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class InvalidCategoryException : DomainException
{
    public InvalidCategoryException(string message) : base(message) { }
}
