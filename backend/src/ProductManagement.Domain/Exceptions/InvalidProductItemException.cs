namespace ProductManagement.Domain.Exceptions;

public class InvalidProductItemException(string message) : DomainException(message);
