using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace PosFlow.Api.Middleware;

/// <summary>
/// Central place that turns exceptions thrown anywhere in the request
/// pipeline into a consistent JSON error response. This lets services
/// and controllers throw plain exceptions (ArgumentException,
/// KeyNotFoundException, InvalidOperationException, ...) instead of
/// every controller action needing its own try/catch.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, message) = MapException(exception);

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;

        await httpContext.Response.WriteAsJsonAsync(
            new { message },
            cancellationToken);

        return true;
    }

    private static (int StatusCode, string Message) MapException(
        Exception exception)
    {
        return exception switch
        {
            ValidationException validationException =>
                (StatusCodes.Status400BadRequest,
                 string.Join(
                     " ",
                     validationException.Errors.Select(
                         e => e.ErrorMessage))),

            ArgumentOutOfRangeException or ArgumentException =>
                (StatusCodes.Status400BadRequest, exception.Message),

            KeyNotFoundException =>
                (StatusCodes.Status404NotFound, exception.Message),

            InvalidOperationException =>
                (StatusCodes.Status409Conflict, exception.Message),

            UnauthorizedAccessException =>
                (StatusCodes.Status403Forbidden, exception.Message),

            _ =>
                (StatusCodes.Status500InternalServerError,
                 "حدث خطأ غير متوقع، حاول مرة أخرى.")
        };
    }
}
