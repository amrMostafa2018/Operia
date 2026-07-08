using FluentValidation.Results;
using Operia.Application.Common.Exceptions;

namespace Operia.Infrastructure.Services;

internal static class BalancePlatformValidation
{
    internal static void EnsureScreenshotProvided(string? screenShotUrl, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(screenShotUrl))
        {
            throw new ValidationException(
            [
                new ValidationFailure("screenShotUrl", errorMessage)
            ]);
        }
    }
}
