using System.Net;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProductManagement.Application.Common.Exceptions;
using ProductManagement.Application.Products;
using ProductManagement.Domain.Exceptions;

namespace ProductManagement.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (statusCode, title, errors) = Map(exception);

        var problemDetails = new ProblemDetails
        {
            Status = (int)statusCode,
            Title = title,
            Type = $"https://httpstatuses.com/{(int)statusCode}"
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            var traceId = httpContext.TraceIdentifier;
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);
            problemDetails.Extensions["traceId"] = traceId;
        }
        else if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        httpContext.Response.StatusCode = (int)statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);
        return true;
    }

    private static (HttpStatusCode StatusCode, string Title, object? Errors) Map(Exception exception) => exception switch
    {
        ValidationException ex => (
            HttpStatusCode.BadRequest, "Validation failed.",
            ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage })),

        DomainException ex => (HttpStatusCode.BadRequest, ex.Message, null),
        InvalidImageException ex => (HttpStatusCode.BadRequest, ex.Message, null),
        InvalidCursorException ex => (HttpStatusCode.BadRequest, ex.Message, null),

        EntityNotFoundException ex => (HttpStatusCode.NotFound, ex.Message, null),
        NoImageSetException ex => (HttpStatusCode.NotFound, ex.Message, null),

        DuplicateSkuException ex => (HttpStatusCode.Conflict, ex.Message, null),
        DuplicateSlugException ex => (HttpStatusCode.Conflict, ex.Message, null),
        CategoryHasActiveProductsException ex => (HttpStatusCode.Conflict, ex.Message, null),
        DbUpdateConcurrencyException => (
            HttpStatusCode.Conflict, "The resource was modified by another request. Reload and try again.", null),

        _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", null)
    };
}
