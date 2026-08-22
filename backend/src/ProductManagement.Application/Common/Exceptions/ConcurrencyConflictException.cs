namespace ProductManagement.Application.Common.Exceptions;

public sealed class ConcurrencyConflictException(string message) : Exception(message);
