using System.Text.Encodings.Web;
using System.Text.Json;
using Operia.Application.Common.Exceptions;
using Operia.Domain.Exceptions;
using Operia.SharedKernel.Errors;

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
        var language = RequestLanguageResolver.Resolve(
            context.Request.Headers.AcceptLanguage.ToString());

        var (statusCode, title, errors, errorCodes) = exception switch
        {
            UnprocessableEntityException validation => (
                StatusCodes.Status422UnprocessableEntity,
                "Unprocessable Entity",
                LocalizeValidationErrors(validation, language),
                validation.ErrorCodes.Count > 0 ? validation.ErrorCodes : null),

            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Resource Not Found",
                new Dictionary<string, string[]> { ["detail"] = [notFound.Message] },
                null),

            ValidationException validation => (
                StatusCodes.Status400BadRequest,
                "Validation Failed",
                LocalizeValidationErrors(validation, language),
                validation.ErrorCodes.Count > 0 ? validation.ErrorCodes : null),

            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                "Conflict",
                new Dictionary<string, string[]> { ["detail"] = [conflict.Message] },
                null),

            UnauthorizedException unauthorized => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                new Dictionary<string, string[]>
                {
                    [unauthorized.Field] = [
                        unauthorized.ErrorCode is not null
                            ? ApiErrorCatalog.GetMessage(unauthorized.ErrorCode, language)
                            : unauthorized.Message
                    ]
                },
                unauthorized.ErrorCode is not null
                    ? new Dictionary<string, string[]> { [unauthorized.Field] = [unauthorized.ErrorCode] }
                    : null),

            ArgumentException argument => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                new Dictionary<string, string[]> { ["detail"] = [argument.Message] },
                null),

            InvalidOperationException invalidOperation => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                new Dictionary<string, string[]> { ["detail"] = [invalidOperation.Message] },
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                new Dictionary<string, string[]> { ["detail"] = ["An internal server error occurred."] },
                null)
        };

        if (context.Response.HasStarted)
            throw exception;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        IReadOnlyList<BranchDependencyInfo> dependencies = exception is ConflictException conflictException
            ? conflictException.Dependencies
            : [];

        var problemDetails = new
        {
            type = $"https://httpstatuses.com/{statusCode}",
            title,
            status = statusCode,
            errors,
            errorCodes,
            dependencies
        };

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        await context.Response.WriteAsync(json);
    }

    private static Dictionary<string, string[]> LocalizeValidationErrors(
        ValidationException validation,
        string language)
    {
        if (validation.ErrorCodes.Count == 0)
        {
            return validation.Errors.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value);
        }

        return validation.ErrorCodes.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value
                .Select(code => ApiErrorCatalog.GetMessage(code, language))
                .ToArray());
    }
}
