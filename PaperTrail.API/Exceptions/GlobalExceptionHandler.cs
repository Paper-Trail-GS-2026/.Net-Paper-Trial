using System.Data.Common;
using Microsoft.AspNetCore.Diagnostics;
using PaperTrail.Application.Exceptions;
using PaperTrail.Domain.Exceptions;

namespace PaperTrail.API.Exceptions;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, detail) = exception switch
        {
            DomainException domain => (StatusCodes.Status400BadRequest, domain.Message),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "Invalid request."),
            ResourceNotFoundException notFound => (StatusCodes.Status404NotFound, notFound.Message),
            ConflictException conflict => (StatusCodes.Status409Conflict, conflict.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Authentication required."),
            CatalogUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Catalog service is unavailable."),
            DbException => (StatusCodes.Status503ServiceUnavailable, "Database is unavailable."),
            _ => (StatusCodes.Status500InternalServerError, "Unable to complete the request.")
        };
        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError("Request failed with {ExceptionType}. TraceId: {TraceId}", exception.GetType().Name, context.TraceIdentifier);

        await Results.Problem(statusCode: status, detail: detail, instance: context.Request.Path).ExecuteAsync(context);
        return true;
    }
}
