namespace ProductManagement.Domain.Exceptions;

public sealed class InvalidProductItemException : DomainException
{
    public InvalidProductItemException(string message) : base(message) { }
}
