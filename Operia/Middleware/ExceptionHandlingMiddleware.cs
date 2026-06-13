using System.Text.Json;
using Operia.Application.Common.Exceptions;
using Operia.Domain.Exceptions;

namespace Operia.Middleware;

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
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, errors) = exception switch
        {
            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                new Dictionary<string, string[]> { ["detail"] = [notFound.Message] }),

            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Validation Failed",
                validation.Errors),

            UnauthorizedException unauthorized => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                new Dictionary<string, string[]> { ["detail"] = [unauthorized.Message] }),

            ArgumentException argument => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                new Dictionary<string, string[]> { ["detail"] = [argument.Message] }),

            InvalidOperationException invalidOperation => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                new Dictionary<string, string[]> { ["detail"] = [invalidOperation.Message] }),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                new Dictionary<string, string[]> { ["detail"] = ["An internal server error occurred."] })
        };

        if (context.Response.HasStarted)
            throw exception;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var problemDetails = new
        {
            type = $"https://httpstatuses.com/{statusCode}",
            title,
            status = statusCode,
            errors
        };

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
