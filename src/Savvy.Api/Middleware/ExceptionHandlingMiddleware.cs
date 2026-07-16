using Microsoft.AspNetCore.Mvc;
using Savvy.Application.Common;

namespace Savvy.Api.Middleware;

/// <summary>
/// Translates unhandled exceptions into RFC 7807 <see cref="ProblemDetails"/> responses.
/// Known application exceptions map to specific status codes; anything else becomes a
/// generic 500 with no internal detail leaked. Expanded with structured logging in Section 7.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (status, title) = Map(ex);

            if (status == StatusCodes.Status500InternalServerError)
                _logger.LogError(ex, "Unhandled exception processing {Method} {Path}",
                    context.Request.Method, context.Request.Path);

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                // Safe to surface for expected app exceptions; generic text for 500s.
                Detail = status == StatusCodes.Status500InternalServerError
                    ? "An unexpected error occurred."
                    : ex.Message,
                Type = $"https://httpstatuses.io/{status}",
                Instance = context.Request.Path
            };
            // Correlation id so a client-facing error can be tied back to server logs.
            problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.TraceIdentifier;

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problem, context.RequestAborted);
        }
    }

    private static (int Status, string Title) Map(Exception ex) => ex switch
    {
        NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
        UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
        ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
        ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
        ValidationException => (StatusCodes.Status400BadRequest, "Invalid request"),
        _ => (StatusCodes.Status500InternalServerError, "Server error")
    };
}
